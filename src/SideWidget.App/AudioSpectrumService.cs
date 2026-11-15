using System.Diagnostics;
using System.Runtime.InteropServices;
using SideWidget.Core;

namespace SideWidget.App;

/// <summary>Default Windows render-endpoint mix only. Never opens a microphone or stores PCM.</summary>
public sealed class AudioSpectrumService : IDisposable
{
    private readonly object gate = new();
    private AudioSpectrumFrame latest = new(false, "Визуализация выключена", new float[32], 0, 0);
    private CancellationTokenSource? cancellation;
    private Thread? worker;
    private long generation, sequence;
    private bool disposed, enabled;

    // The public array contract remains compatible while callers cannot mutate shared state.
    public AudioSpectrumFrame Latest
    {
        get { var frame = Volatile.Read(ref latest); return frame with { Bands = (float[])frame.Bands.Clone() }; }
    }

    public void SetEnabled(bool enabled)
    {
        lock (gate)
        {
            if (disposed || enabled == this.enabled) return;
            this.enabled = enabled;
            var epoch = Interlocked.Increment(ref generation);
            if (!enabled)
            {
                cancellation?.Cancel();
                // Never join a COM worker on the UI thread. Its finally releases the stream;
                // a rapid re-enable starts only after that cleanup, keeping one worker maximum.
                Publish(new(false, "Визуализация выключена", new float[32], 0, NextSequence()), epoch);
                return;
            }
            Publish(new(false, "Подключение к системному звуку", new float[32], 0, NextSequence()), epoch);
            if (worker is null) StartWorker(epoch);
        }
    }

    public void Dispose()
    {
        lock (gate) { if (disposed) return; SetEnabled(false); disposed = true; }
    }

    private long NextSequence() => Interlocked.Increment(ref sequence);
    private void StartWorker(long epoch)
    {
        var source = cancellation = new CancellationTokenSource();
        worker = new Thread(() => Run(source, epoch)) { IsBackground = true, Name = "SideWidget system audio spectrum" };
        worker.SetApartmentState(ApartmentState.MTA);
        worker.Start();
    }

    private void Publish(AudioSpectrumFrame frame, long epoch)
    {
        // Pair generation check and write atomically with disable; no stale frame can win.
        lock (gate) if (generation == epoch) Volatile.Write(ref latest, frame);
    }

    private void Run(CancellationTokenSource source, long epoch)
    {
        var token = source.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                try { Capture(token, epoch); }
                catch (Exception ex) when (ex is COMException or NotSupportedException or ArgumentException or OverflowException)
                {
                    Publish(new(false, "Системный звук недоступен", new float[32], 0, NextSequence()), epoch);
                }
                if (token.WaitHandle.WaitOne(1000)) break;
            }
        }
        finally
        {
            lock (gate)
            {
                source.Dispose(); cancellation = null; worker = null;
                if (enabled && !disposed) StartWorker(generation);
            }
        }
    }

    private void Capture(CancellationToken token, long epoch)
    {
        CoreAudioInterop.IMMDeviceEnumerator? enumerator = null;
        CoreAudioInterop.IMMDevice? device = null;
        ILoopbackAudioClient? client = null;
        ILoopbackCaptureClient? capture = null;
        IntPtr wave = IntPtr.Zero;
        var started = false;
        try
        {
            enumerator = CoreAudioInterop.CreateEnumerator();
            // eRender=0, eMultimedia=1: the Windows default output mix.
            CoreAudioInterop.Check(enumerator.GetDefaultAudioEndpoint(0, 1, out device));
            CoreAudioInterop.Check(device.GetId(out var endpointId));
            var audioId = typeof(ILoopbackAudioClient).GUID;
            CoreAudioInterop.Check(device.Activate(ref audioId, 23, IntPtr.Zero, out var audioObject));
            client = (ILoopbackAudioClient)audioObject;
            CoreAudioInterop.Check(client.GetMixFormat(out wave));
            var formatBytes = new byte[18]; Marshal.Copy(wave, formatBytes, 0, 18);
            var extra = BitConverter.ToUInt16(formatBytes, 16);
            if (extra > 256) throw new NotSupportedException("Oversized audio format.");
            Array.Resize(ref formatBytes, 18 + extra); Marshal.Copy(wave, formatBytes, 0, formatBytes.Length);
            var format = AudioPcmFormat.Parse(formatBytes);
            var analyzer = new AudioSpectrumAnalyzer(format);
            CoreAudioInterop.Check(client.Initialize(0, 0x00020000, 1000000, 0, wave, IntPtr.Zero));
            var captureId = typeof(ILoopbackCaptureClient).GUID;
            CoreAudioInterop.Check(client.GetService(ref captureId, out var captureObject));
            capture = (ILoopbackCaptureClient)captureObject;
            CoreAudioInterop.Check(client.Start()); started = true;
            var clock = Stopwatch.StartNew();
            long lastFrame = -40, lastPacket = 0, lastDeviceCheck = 0;
            // Bound native copies independently of device packet size.
            var buffer = new byte[4096 * format.BlockAlign];
            while (!token.IsCancellationRequested)
            {
                CoreAudioInterop.Check(capture.GetNextPacketSize(out var pending));
                var packets = 0;
                while (pending > 0 && packets++ < 64 && !token.IsCancellationRequested)
                {
                    CoreAudioInterop.Check(capture.GetBuffer(out var data, out var frames, out var flags, out _, out _));
                    try
                    {
                        if ((flags & 1) != 0) analyzer.Reset(); // discontinuity must not retain old device samples
                        if ((flags & 2) != 0) analyzer.AppendSilence((int)Math.Min(frames, int.MaxValue));
                        else
                        {
                            // Older frames cannot affect this FFT; skip them without allocation.
                            var kept = (int)Math.Min(frames, 4096u);
                            var skipBytes = checked((long)(frames - kept) * format.BlockAlign);
                            var bytes = checked(kept * format.BlockAlign);
                            Marshal.Copy(new IntPtr(checked(data.ToInt64() + skipBytes)), buffer, 0, bytes);
                            analyzer.Append(buffer.AsSpan(0, bytes));
                        }
                        lastPacket = clock.ElapsedMilliseconds;
                    }
                    finally { CoreAudioInterop.Check(capture.ReleaseBuffer(frames)); }
                    CoreAudioInterop.Check(capture.GetNextPacketSize(out pending));
                }
                var now = clock.ElapsedMilliseconds;
                if (now - lastFrame >= 40)
                {
                    // Windows may stop delivering packets during silence.
                    if (now - lastPacket >= 100) analyzer.AppendSilence(4096);
                    Publish(analyzer.Snapshot(NextSequence()), epoch); lastFrame = now;
                }
                if (now - lastDeviceCheck >= 1000)
                {
                    CoreAudioInterop.IMMDevice? current = null;
                    try
                    {
                        CoreAudioInterop.Check(enumerator.GetDefaultAudioEndpoint(0, 1, out current));
                        CoreAudioInterop.Check(current.GetId(out var currentId));
                        if (currentId != endpointId) return;
                    }
                    finally { CoreAudioInterop.Release(current); }
                    lastDeviceCheck = now;
                }
                if (token.WaitHandle.WaitOne(10)) break;
            }
        }
        finally
        {
            if (started) client?.Stop();
            if (wave != IntPtr.Zero) Marshal.FreeCoTaskMem(wave);
            CoreAudioInterop.Release(capture); CoreAudioInterop.Release(client);
            CoreAudioInterop.Release(device); CoreAudioInterop.Release(enumerator);
        }
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ILoopbackAudioClient
    {
        [PreserveSig] int Initialize(int mode, uint flags, long duration, long periodicity, IntPtr format, IntPtr session);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint frames);
        [PreserveSig] int IsFormatSupported(int mode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long normal, out long minimum);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ILoopbackCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong position, out ulong counter);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }
}
