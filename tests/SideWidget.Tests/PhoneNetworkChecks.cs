using System.Net;
using SideWidget.Core;

static class PhoneNetworkChecks
{
    public static int Run()
    {
        var count = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); count++; }
        void Reject(Action action) { try { action(); } catch (CommandFault) { count++; return; } throw new Exception("unsafe network plan accepted"); }
        var id = Guid.NewGuid().ToString();
        var exe = Path.GetFullPath("SideWidget.App.exe");
        var plan = PhoneNetworkPlan.Create(id, exe, IPAddress.Parse("192.168.31.29"), IPAddress.Parse("255.255.255.0"), 47831, 47830);
        Check(plan.RemoteSubnet == "192.168.31.0/24", "firewall only home subnet");
        Check(plan.LocalAddress == "192.168.31.29" && plan.Ports == "47831,47830", "firewall exact local endpoint and ports");
        Check(plan.Application == exe && plan.AdapterId == id, "firewall fixed application and adapter");
        Check(PhoneNetworkPlan.AddressScopeEquals("192.168.31.29/255.255.255.255", "192.168.31.29"), "Windows canonical local host mask equals exact IPv4");
        Check(PhoneNetworkPlan.AddressScopeEquals("192.168.31.0/255.255.255.0", "192.168.31.0/24"), "Windows canonical subnet mask equals CIDR");
        Check(!PhoneNetworkPlan.AddressScopeEquals("192.168.0.0/16", "192.168.31.0/24"), "broader firewall scope rejected");
        Check(!PhoneNetworkPlan.AddressScopeEquals("*", "192.168.31.29"), "wildcard firewall scope rejected");
        Check(!PhoneNetworkPlan.AddressScopeEquals("192.168.31.30/32", "192.168.31.29"), "different local address rejected");
        Reject(() => PhoneNetworkPlan.Create(id, exe, IPAddress.Parse("8.8.8.8"), IPAddress.Parse("255.255.255.0"), 47831, 47830));
        Reject(() => PhoneNetworkPlan.Create(id, exe, IPAddress.Parse("192.168.31.29"), IPAddress.Any, 47831, 47830));
        Reject(() => PhoneNetworkPlan.Create(id, exe, IPAddress.Parse("192.168.31.29"), IPAddress.Parse("255.0.255.0"), 47831, 47830));
        Reject(() => PhoneNetworkPlan.Create("bad id", exe, IPAddress.Parse("192.168.31.29"), IPAddress.Parse("255.255.255.0"), 47831, 47830));
        Reject(() => PhoneNetworkPlan.Create(id, exe, IPAddress.Parse("192.168.31.29"), IPAddress.Parse("255.255.255.0"), 47831, 47831));
        Console.WriteLine($"PASS {count} phone one-click network scope checks"); return count;
    }
}
