using System.Collections;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using SideWidget.Infrastructure;
using SideWidget.Core;
using SideWidget.Remote;

namespace SideWidget.App;

/// <summary>Owns the LAN listener. Never selects Windows' current internet/VPN route.</summary>
sealed class PhoneRemoteService : IDisposable
{
    readonly string directory;
    readonly RemoteAccess access;
    readonly Func<Task<object>> snapshot;
    readonly Func<RemoteCommand, Task> command;
    readonly SemaphoreSlim gate = new(1, 1);
    readonly SemaphoreSlim setupGate = new(1, 1);
    long networkGeneration;
    readonly System.Threading.Timer retry;
    RemoteConfiguration config;
    RemoteServer? server;
    RemoteCertificates? certificates;
    X509Certificate2? leaf;
    RemoteAdapter? listening;
    string? error;
    volatile bool disposed;
    string SettingsPath => Path.Combine(directory, "settings.json");

    public PhoneRemoteService(string directory, Func<Task<object>> snapshot, Func<RemoteCommand, Task> command)
    {
        this.directory = directory; this.snapshot = snapshot; this.command = command;
        Directory.CreateDirectory(directory);
        access = new RemoteAccess(Path.Combine(directory, "devices.json"));
        config = File.Exists(SettingsPath) ? JsonSerializer.Deserialize<RemoteConfiguration>(File.ReadAllText(SettingsPath)) ?? new() : new();
        config.Validate();
        retry = new(async _ => await Refresh(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    public async Task<object> Info()
    {
        await gate.WaitAsync();
        try { return InfoUnsafe(); } finally { gate.Release(); }
    }
    object InfoUnsafe() => new
    {
        enabled = config.Enabled, running = server?.Running == true,
        adapterId = config.AdapterId, config.Port, config.SetupPort,
        adapters = Adapters(false).Select(a => new { a.Id, a.Name, address = a.Address.ToString() }).ToArray(),
        secureUrl = listening is null ? null : $"https://{listening.Address}:{config.Port}/",
        setupUrl = listening is null ? null : $"http://{listening.Address}:{config.SetupPort}/",
        fingerprint = certificates?.Fingerprint, access = access.Info(), message = error,
        startup = new StartupService(Environment.ProcessPath!).Get()
    };

    public Task<object> Configure(bool enabled, string? adapterId, int port, int setupPort, bool startup)
        => ConfigureCore(enabled, adapterId, port, setupPort, startup, Interlocked.Increment(ref networkGeneration));

    async Task<object> ConfigureCore(bool enabled, string? adapterId, int port, int setupPort, bool startup, long generation)
    {
        var proposed = new RemoteConfiguration(Enabled: enabled, AdapterId: adapterId, Port: port, SetupPort: setupPort);
        proposed.Validate();
        await gate.WaitAsync();
        try
        {
            if (disposed) throw new ObjectDisposedException(nameof(PhoneRemoteService));
            if (generation != Volatile.Read(ref networkGeneration)) throw new CommandFault("remote_network", "Настройка отменена более новым действием в другом окне.");
            if (enabled && !Adapters().Any(a => a.Id == adapterId)) throw new RemoteFault("Выберите доступный домашний адаптер. Сеть Windows должна быть частной.");
            new StartupService(Environment.ProcessPath!).Set(startup);
            var temporary = SettingsPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(proposed)); File.Move(temporary, SettingsPath, true);
            config = proposed; access.ClearEphemeral(); await StopListener();
            await RefreshUnsafe(); return InfoUnsafe();
        }
        finally { gate.Release(); }
    }

    public async Task<object> Setup(string? adapterId, int port, int setupPort, bool startup)
    {
        if (!await setupGate.WaitAsync(0)) throw new CommandFault("remote_network", "Настройка сети уже выполняется. Завершите подтверждение Windows.");
        try
        {
            if (disposed) throw new ObjectDisposedException(nameof(PhoneRemoteService));
            var generation = Interlocked.Increment(ref networkGeneration);
            var candidates = SetupAdapters();
            var candidate = candidates.FirstOrDefault(a => a.Id == adapterId);
            if (candidate is null && string.IsNullOrEmpty(adapterId) && candidates.Select(a => a.Id).Distinct().Count() == 1) candidate = candidates[0];
            if (candidate is null) throw new CommandFault("remote_network", "Выберите домашний Wi-Fi или Ethernet с подключением к роутеру.");
            var plan = SideWidget.Core.PhoneNetworkPlan.Create(candidate.Id, Environment.ProcessPath!, candidate.Address, candidate.Mask, port, setupPort);
            await PhoneNetworkSetup.Configure(plan, directory);
            return await ConfigureCore(true, candidate.Id, port, setupPort, startup, generation);
        }
        finally { setupGate.Release(); }
    }

    public async Task<object> Invite()
    {
        await gate.WaitAsync();
        try
        {
            if (listening is null || server is null) throw new RemoteFault("Сначала включите пульт в домашней сети.");
            var invite = access.CreateInvite();
            return new { setupUrl = $"http://{listening.Address}:{config.SetupPort}/#invite={invite.Token}",
                secureUrl = $"https://{listening.Address}:{config.Port}/#invite={invite.Token}", invite.Expires };
        }
        finally { gate.Release(); }
    }
    public void Decide(string id, bool approved) => access.Decide(id, approved);
    public void Revoke(string id) => access.Revoke(id);

    async Task Refresh()
    {
        if (disposed || !await gate.WaitAsync(0)) return;
        try { if (!disposed) await RefreshUnsafe(); }
        finally { gate.Release(); }
    }
    async Task RefreshUnsafe()
    {
        try
        {
            if (!config.Enabled) { await StopListener(); error = null; return; }
            var candidate = Adapters().FirstOrDefault(a => a.Id == config.AdapterId);
            if (candidate is null) { await StopListener(); error = "Домашняя сеть недоступна. Подключитесь к ней и задайте профиль «Частная сеть» в Windows. VPN не используется для пульта."; return; }
            if (server is not null && Equals(listening, candidate) && !RemoteCertificates.NeedsRenewal(leaf, DateTimeOffset.UtcNow)) return;
            await StopListener();
            certificates ??= new RemoteCertificates(Path.Combine(directory, "certificates"));
            leaf = certificates.Get(candidate.Address);
            var next = new RemoteServer(access, candidate, config, leaf, certificates.ExportRoot(), Path.Combine(AppContext.BaseDirectory, "Web"), snapshot, command);
            try { await next.StartAsync(); server = next; listening = candidate; error = null; }
            catch { await next.DisposeAsync(); leaf.Dispose(); leaf = null; throw; }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            error = "Не удалось открыть пульт: " + (ex is IOException ? "проверьте занятость портов, папку приложения и Windows Firewall для частной сети." : "проверьте домашнюю сеть, порты и сертификат. Тип: " + ex.GetType().Name);
        }
    }
    async Task StopListener()
    {
        listening = null;
        if (server is { } previous) { server = null; await previous.DisposeAsync(); }
        leaf?.Dispose(); leaf = null;
    }

    internal static RemoteAdapter[] SetupAdapters() => Adapters(false);
    static RemoteAdapter[] Adapters(bool privateOnly = true)
    {
        var result = new List<RemoteAdapter>();
        try
        {
            var trusted = privateOnly ? PrivateAdapters() : [];
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || privateOnly && !trusted.Contains(nic.Id.Trim('{', '}')) ||
                    !RemoteNetwork.IsPhysicalCandidate(nic.NetworkInterfaceType, nic.Description + " " + nic.Name)) continue;
                var properties = nic.GetIPProperties();
                if (!properties.GatewayAddresses.Any(g => RemoteNetwork.IsHomeAddress(g.Address))) continue;
                foreach (var address in properties.UnicastAddresses)
                    if (RemoteNetwork.IsHomeAddress(address.Address)) result.Add(new(nic.Id, nic.Name, address.Address, address.IPv4Mask));
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException) { }
        // Stable order; never order by the default route, metric or VPN's internet profile.
        return result.OrderBy(a => a.Id, StringComparer.Ordinal).ThenBy(a => a.Address.ToString(), StringComparer.Ordinal).ToArray();
    }

    static HashSet<string> PrivateAdapters()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        object? manager = null, networks = null;
        try
        {
            manager = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B"))!);
            networks = ((dynamic)manager!).GetNetworks(1); // NLM_ENUM_NETWORK_CONNECTED
            foreach (var network in (IEnumerable)networks)
            {
                object? connections = null;
                try
                {
                    if ((int)((dynamic)network).GetCategory() is not (1 or 2)) continue; // Private / domain authenticated.
                    connections = ((dynamic)network).GetNetworkConnections();
                    foreach (var connection in (IEnumerable)connections)
                    {
                        try { result.Add(NetworkProfileInterop.AdapterId(connection).ToString()); }
                        finally { if (Marshal.IsComObject(connection)) Marshal.ReleaseComObject(connection); }
                    }
                }
                finally
                {
                    if (connections is not null && Marshal.IsComObject(connections)) Marshal.ReleaseComObject(connections);
                    if (Marshal.IsComObject(network)) Marshal.ReleaseComObject(network);
                }
            }
        }
        finally
        {
            if (networks is not null && Marshal.IsComObject(networks)) Marshal.ReleaseComObject(networks);
            if (manager is not null && Marshal.IsComObject(manager)) Marshal.ReleaseComObject(manager);
        }
        return result;
    }

    public void Dispose()
    {
        if (disposed) return; disposed = true; retry.Dispose(); access.ClearEphemeral();
        _ = Shutdown();
    }
    async Task Shutdown()
    {
        await gate.WaitAsync();
        try { await StopListener(); certificates?.Dispose(); }
        finally { gate.Release(); }
    }
}
