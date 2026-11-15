using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using SideWidget.Core;

namespace SideWidget.App;

/// <summary>Fixed, narrowly scoped elevated operation. No shell or arbitrary commands.</summary>
static class PhoneNetworkSetup
{
    const string Mode = "--phone-network-setup";
    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0 || args[0] != Mode) return false;
        exitCode = 64;
        if (args.Length != 5 || !Guid.TryParse(args[1], out var id) || !int.TryParse(args[2], out var port) ||
            !int.TryParse(args[3], out var setupPort) || !IPAddress.TryParse(args[4], out var address)) return true;
        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) { exitCode = 5; return true; }
        try
        {
            var candidate = PhoneRemoteService.SetupAdapters().SingleOrDefault(a => Guid.Parse(a.Id) == id && a.Address.Equals(address));
            if (candidate is null) { exitCode = 20; return true; }
            var plan = PhoneNetworkPlan.Create(candidate.Id, Environment.ProcessPath!, candidate.Address, candidate.Mask, port, setupPort);
            if (!File.Exists(plan.Application)) { exitCode = 20; return true; }
            exitCode = 30;
            WithNetwork(plan.AdapterId, network =>
            {
                var category = (int)network.GetCategory();
                if (category == 2) throw new InvalidOperationException("Domain profile cannot be changed");
                if (category != 1) network.SetCategory(1);
                return 0;
            });
            exitCode = 40;
            SetFirewall(plan);
            exitCode = IsReady(plan) ? 0 : 50;
        }
        catch (Exception error) when (error is not OutOfMemoryException) { }
        return true;
    }

    public static async Task Configure(PhoneNetworkPlan plan, string directory)
    {
        if (IsReady(plan)) return;
        Directory.CreateDirectory(directory);
        var audit = Path.Combine(directory, "network-setup-last.json");
        var before = Inspect(plan);
        File.WriteAllText(audit, JsonSerializer.Serialize(new { time = DateTimeOffset.UtcNow, plan, before, status = "requested" }));
        try
        {
            var start = new ProcessStartInfo(plan.Application) { UseShellExecute = true, Verb = "runas", WorkingDirectory = AppContext.BaseDirectory, WindowStyle = ProcessWindowStyle.Hidden };
            // Every argument is derived from validated GUID, integer or IPv4 values.
            start.Arguments = string.Join(' ', Mode, plan.AdapterId, plan.Ports.Replace(',', ' '), plan.LocalAddress);
            using var process = Process.Start(start) ?? throw new CommandFault("remote_network", "Windows не запустила настройку сети.");
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
            var ready = process.ExitCode == 0 && IsReady(plan);
            File.WriteAllText(audit, JsonSerializer.Serialize(new { time = DateTimeOffset.UtcNow, plan, before, after = Inspect(plan), status = ready ? "configured" : "failed", process.ExitCode }));
            if (!ready) throw new CommandFault("remote_network", $"Windows не завершила настройку сети (код {process.ExitCode}). Пульт не включён. Частный профиль мог уже примениться; повторите настройку после проверки прав Windows.");
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223)
        {
            File.WriteAllText(audit, JsonSerializer.Serialize(new { time = DateTimeOffset.UtcNow, plan, before, status = "cancelled" }));
            throw new CommandFault("remote_network", "Подтверждение Windows отменено. Для настройки домашней сети нужны права администратора.");
        }
        catch (TimeoutException)
        { throw new CommandFault("remote_network", "Windows ещё не завершила настройку. Проверьте её подтверждение; затем повторите кнопку, она проверит фактическое состояние."); }
    }

    public static bool IsReady(PhoneNetworkPlan plan)
    {
        try { return WithNetwork(plan.AdapterId, n => (int)n.GetCategory()) == 1 && WithFirewall(plan, Matches); }
        catch (Exception error) when (error is not OutOfMemoryException) { return false; }
    }

    static object Inspect(PhoneNetworkPlan plan)
    {
        int? category = null; object? rule = null; string? categoryError = null;
        try { category = WithNetwork(plan.AdapterId, n => (int)n.GetCategory()); } catch (Exception error) { categoryError = error.ToString(); }
        try { WithFirewall(plan, (current, _) => { if (current is not null) rule = new { application = (string)current.ApplicationName, profiles = (int)current.Profiles, localPorts = (string)current.LocalPorts, localAddresses = (string)current.LocalAddresses, remoteAddresses = (string)current.RemoteAddresses, enabled = (bool)current.Enabled }; return false; }); } catch { }
        return new { category, rule, categoryError };
    }
    static bool Matches(dynamic? rule, PhoneNetworkPlan plan) => rule is not null &&
        (string)rule.Grouping == PhoneNetworkPlan.RuleGroup && (bool)rule.Enabled && (int)rule.Profiles == 2 && (int)rule.Direction == 1 &&
        (int)rule.Action == 1 && (int)rule.Protocol == 6 && !(bool)rule.EdgeTraversal &&
        string.Equals((string)rule.ApplicationName, plan.Application, StringComparison.OrdinalIgnoreCase) &&
        ((string)rule.LocalPorts).Split(',').ToHashSet().SetEquals(plan.Ports.Split(',')) &&
        PhoneNetworkPlan.AddressScopeEquals((string)rule.LocalAddresses, plan.LocalAddress) &&
        PhoneNetworkPlan.AddressScopeEquals((string)rule.RemoteAddresses, plan.RemoteSubnet);
    static bool WithFirewall(PhoneNetworkPlan plan, Func<dynamic?, PhoneNetworkPlan, bool> action)
    {
        object? policy = null, rules = null, rule = null;
        try
        {
            policy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2")!); rules = ((dynamic)policy!).Rules;
            try { rule = ((dynamic)rules).Item(PhoneNetworkPlan.RuleName); } catch (Exception error) when (error.HResult == unchecked((int)0x80070002)) { }
            return action(rule, plan);
        }
        finally { Release(rule); Release(rules); Release(policy); }
    }
    static void SetFirewall(PhoneNetworkPlan plan)
    {
        object? policy = null, rules = null, existing = null, rule = null;
        try
        {
            policy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2")!); rules = ((dynamic)policy!).Rules;
            try { existing = ((dynamic)rules).Item(PhoneNetworkPlan.RuleName); } catch (Exception error) when (error.HResult == unchecked((int)0x80070002)) { }
            if (existing is not null && (string)((dynamic)existing).Grouping != PhoneNetworkPlan.RuleGroup)
                throw new InvalidOperationException("A foreign firewall rule has the reserved name");
            rule = existing ?? Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule")!);
            dynamic r = rule!;
            r.Enabled = false; r.Name = PhoneNetworkPlan.RuleName; r.Grouping = PhoneNetworkPlan.RuleGroup;
            r.Description = "SideWidget phone remote; selected home address and subnet only";
            r.ApplicationName = plan.Application; r.Protocol = 6; r.LocalPorts = plan.Ports;
            r.LocalAddresses = plan.LocalAddress; r.RemoteAddresses = plan.RemoteSubnet;
            r.Direction = 1; r.Action = 1; r.Profiles = 2; r.EdgeTraversal = false; r.Enabled = true;
            if (existing is null) ((dynamic)rules!).Add(r);
        }
        finally { if (rule != existing) Release(rule); Release(existing); Release(rules); Release(policy); }
    }
    static T WithNetwork<T>(string adapterId, Func<dynamic, T> action)
    {
        object? manager = null, networks = null;
        try
        {
            manager = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B"))!);
            networks = ((dynamic)manager!).GetNetworks(1);
            foreach (var network in (IEnumerable)networks)
            {
                object? connections = null;
                try
                {
                    connections = ((dynamic)network).GetNetworkConnections();
                    foreach (var connection in (IEnumerable)connections)
                    {
                        try { if (NetworkProfileInterop.AdapterId(connection) == Guid.Parse(adapterId)) return action(network); }
                        finally { Release(connection); }
                    }
                }
                finally { Release(connections); Release(network); }
            }
            throw new InvalidOperationException("Selected home network not connected");
        }
        finally { Release(networks); Release(manager); }
    }
    static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
}
