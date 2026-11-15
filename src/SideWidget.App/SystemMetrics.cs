using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SideWidget.App;

public sealed class SystemMetrics
{
    private readonly object gate = new();
    private (ulong Idle, ulong Total)? previousCpu;
    private (string Id, long Received, long Sent, long Time)? previousNetwork;
    private GpuCounters? gpuCounters;

    public object Read()
    {
        lock (gate)
        {
            double? cpu = null;
            if (GetSystemTimes(out var idle, out var kernel, out var user))
            {
                var total = kernel + user;
                if (previousCpu is { } old && total > old.Total && idle >= old.Idle)
                    cpu = Math.Clamp(100.0 * (1 - (double)(idle - old.Idle) / (total - old.Total)), 0, 100);
                previousCpu = (idle, total);
            }
            var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
            var memoryAvailable = GlobalMemoryStatusEx(ref memory);
            var disks = new List<object>();
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
            {
                try { if (drive.IsReady) disks.Add(new { name = drive.Name, total = drive.TotalSize, free = drive.AvailableFreeSpace }); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            var powerAvailable = GetSystemPowerStatus(out var power);
            var hasBattery = powerAvailable && power.BatteryFlag != 255 && (power.BatteryFlag & 128) == 0;
            var gpu = (gpuCounters ??= new GpuCounters()).Read();
            return new
            {
                cpuPercent = cpu,
                memoryUsed = memoryAvailable ? (ulong?)(memory.TotalPhysical - memory.AvailablePhysical) : null,
                memoryTotal = memoryAvailable ? (ulong?)memory.TotalPhysical : null,
                uptimeSeconds = GetTickCount64() / 1000.0,
                network = ReadNetwork(), disks,
                battery = new { available = hasBattery, percent = hasBattery && power.BatteryPercent <= 100 ? (int?)power.BatteryPercent : null,
                    charging = hasBattery ? (bool?)((power.BatteryFlag & 8) != 0) : null, ac = powerAvailable && power.AcLineStatus != 255 ? (bool?)(power.AcLineStatus == 1) : null },
                gpuAvailable = gpu.Percent.HasValue || gpu.MemoryUsed.HasValue,
                gpuPercent = gpu.Percent,
                gpuMemoryUsed = gpu.MemoryUsed,
                gpuMemoryTotal = ReadGpuMemoryTotal()
            };
        }
    }

    // Installed dedicated VRAM, not the smaller dynamic per-process DXGI memory budget.
    // Both usage and capacity describe all hardware adapters; shared system RAM is excluded.
    private static double? ReadGpuMemoryTotal()
    {
        var iid = new Guid("770aae78-f26f-4dba-a829-253c83d1b387"); // IDXGIFactory1
        if (CreateDXGIFactory1(ref iid, out var factory) < 0) return null;
        try
        {
            var enumerate = Marshal.GetDelegateForFunctionPointer<EnumAdapters>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(factory), 12 * IntPtr.Size));
            double total = 0;
            var found = false;
            for (uint index = 0; ; index++)
            {
                var status = enumerate(factory, index, out var adapter);
                if (status == unchecked((int)0x887A0002)) break; // DXGI_ERROR_NOT_FOUND
                if (status < 0) return null;
                try
                {
                    var describe = Marshal.GetDelegateForFunctionPointer<GetAdapterDescription>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(adapter), 10 * IntPtr.Size));
                    if (describe(adapter, out var description) < 0) return null;
                    if ((description.Flags & 2) != 0) continue; // DXGI_ADAPTER_FLAG_SOFTWARE
                    found = true;
                    total += description.DedicatedVideoMemory;
                }
                finally { Marshal.Release(adapter); }
            }
            return found ? total : null;
        }
        finally { Marshal.Release(factory); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct AdapterDescription
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint VendorId, DeviceId, SubSystemId, Revision;
        public nuint DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
        public long AdapterLuid;
        public uint Flags;
    }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int EnumAdapters(nint factory, uint index, out nint adapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetAdapterDescription(nint adapter, out AdapterDescription description);
    [DllImport("dxgi.dll")] private static extern int CreateDXGIFactory1(ref Guid iid, out nint factory);

    // English counter paths work on localized Windows too. PDH uses the installed WDDM driver.
    private sealed class GpuCounters
    {
        private readonly QueryHandle? query;
        private readonly nint engine, memory;
        private bool sampled;

        public GpuCounters()
        {
            if (PdhOpenQueryW(null, 0, out var handle) != 0) { handle?.Dispose(); return; }
            query = handle;
            PdhAddEnglishCounterW(query, @"\GPU Engine(*)\Utilization Percentage", 0, out engine);
            PdhAddEnglishCounterW(query, @"\GPU Adapter Memory(*)\Dedicated Usage", 0, out memory);
        }

        public (double? Percent, double? MemoryUsed) Read()
        {
            if (query is null || query.IsInvalid || PdhCollectQueryData(query) != 0)
            {
                sampled = false;
                return (null, null);
            }
            var engines = sampled ? Values(engine) : [];
            sampled = true;
            var memories = Values(memory);
            // Sum processes on each physical engine, then report the busiest engine.
            // Summing different engines would count parallel work more than once.
            var groups = engines.Where(item => item.Name.Contains("luid_", StringComparison.Ordinal))
                .GroupBy(item => item.Name[item.Name.IndexOf("luid_", StringComparison.Ordinal)..].Split('#')[0]);
            double? percent = groups.Any() ? Math.Clamp(groups.Max(group => group.Sum(item => item.Value)), 0, 100) : null;
            return (percent, memories.Count > 0 ? memories.Sum(item => item.Value) : null);
        }

        private static List<(string Name, double Value)> Values(nint counter)
        {
            var result = new List<(string, double)>();
            if (counter == 0) return result;
            uint size = 0;
            const uint moreData = 0x800007D2;
            const uint format = 0x200 | 0x8000; // PDH_FMT_DOUBLE | PDH_FMT_NOCAP100
            if (PdhGetFormattedCounterArrayW(counter, format, ref size, out _, 0) != moreData) return result;
            // Processes can appear between size query and fill. Retry with the returned size.
            for (var attempt = 0; attempt < 3 && size > 0; attempt++)
            {
                var buffer = Marshal.AllocHGlobal(checked((int)size));
                try
                {
                    var status = PdhGetFormattedCounterArrayW(counter, format, ref size, out var count, buffer);
                    if (status == moreData) continue;
                    if (status != 0) return result;
                    var stride = Marshal.SizeOf<CounterItem>();
                    for (var i = 0; i < count; i++)
                    {
                        var item = Marshal.PtrToStructure<CounterItem>(buffer + i * stride);
                        if (item.Status <= 1 && double.IsFinite(item.Value) && item.Value >= 0)
                            result.Add((Marshal.PtrToStringUni(item.Name) ?? "", item.Value));
                    }
                    return result;
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
            return result;
        }

        // x64 PDH_FMT_COUNTERVALUE_ITEM_W: pointer, DWORD status, aligned double union.
        [StructLayout(LayoutKind.Explicit, Size = 24)] private struct CounterItem
        {
            [FieldOffset(0)] public nint Name;
            [FieldOffset(8)] public uint Status;
            [FieldOffset(16)] public double Value;
        }
        private sealed class QueryHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public QueryHandle() : base(true) { }
            protected override bool ReleaseHandle() => PdhCloseQuery(handle) == 0;
        }
        [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhOpenQueryW(string? source, nuint userData, out QueryHandle query);
        [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhAddEnglishCounterW(QueryHandle query, string path, nuint userData, out nint counter);
        [DllImport("pdh.dll")] private static extern uint PdhCollectQueryData(QueryHandle query);
        [DllImport("pdh.dll")] private static extern uint PdhGetFormattedCounterArrayW(nint counter, uint format, ref uint size, out uint count, nint items);
        [DllImport("pdh.dll")] private static extern uint PdhCloseQuery(nint query);
    }

    private object ReadNetwork()
    {
        try
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up &&
                n.NetworkInterfaceType != NetworkInterfaceType.Loopback && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel).ToList();
            var adapter = candidates.OrderByDescending(n => n.GetIPProperties().GatewayAddresses.Count > 0).ThenByDescending(n => n.Speed).FirstOrDefault();
            if (adapter is not null)
            {
                var stats = adapter.GetIPStatistics();
                var now = Stopwatch.GetTimestamp();
                double? received = null, sent = null;
                if (previousNetwork is { } old && old.Id == adapter.Id && now > old.Time && stats.BytesReceived >= old.Received && stats.BytesSent >= old.Sent)
                {
                    var seconds = (now - old.Time) / (double)Stopwatch.Frequency;
                    received = (stats.BytesReceived - old.Received) / seconds;
                    sent = (stats.BytesSent - old.Sent) / seconds;
                }
                previousNetwork = (adapter.Id, stats.BytesReceived, stats.BytesSent, now);
                return new { name = adapter.Name, receiveBytesPerSecond = received, sendBytesPerSecond = sent };
            }
        }
        catch (NetworkInformationException) { }
        previousNetwork = null;
        return new { name = "Недоступно", receiveBytesPerSecond = (double?)null, sendBytesPerSecond = (double?)null };
    }

    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
    { public uint Length, Load; public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual; }
    [StructLayout(LayoutKind.Sequential)] private struct PowerStatus
    { public byte AcLineStatus, BatteryFlag, BatteryPercent, SystemStatus; public uint BatteryLifeTime, BatteryFullLifeTime; }
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemPowerStatus(out PowerStatus status);
    [DllImport("kernel32.dll")] private static extern ulong GetTickCount64();
}
