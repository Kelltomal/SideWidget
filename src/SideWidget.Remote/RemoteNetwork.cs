using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SideWidget.Remote;

public sealed record RemoteAdapter(string Id, string Name, IPAddress Address, IPAddress Mask);
public static class RemoteNetwork
{
    public static bool IsHomeAddress(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = address.GetAddressBytes();
        return b[0] == 10 || b[0] == 192 && b[1] == 168 || b[0] == 172 && b[1] is >= 16 and <= 31;
    }

    public static bool IsPhysicalCandidate(NetworkInterfaceType type, string description) =>
        type is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211 &&
        !new[] { "vpn", "tap-", "tap ", "tun ", "tunnel", "wireguard", "wintun", "tailscale", "zerotier", "virtual", "hyper-v", "loopback", "docker", "hamachi", "radmin", "veth" }
            .Any(word => description.Contains(word, StringComparison.OrdinalIgnoreCase));

    public static bool SameSubnet(IPAddress remote, RemoteAdapter adapter)
    {
        if (remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();
        if (remote.AddressFamily != AddressFamily.InterNetwork) return false;
        var a = remote.GetAddressBytes(); var b = adapter.Address.GetAddressBytes(); var mask = adapter.Mask.GetAddressBytes();
        return mask.Any(m => m != 0) && Enumerable.Range(0, 4).All(i => (a[i] & mask[i]) == (b[i] & mask[i]));
    }
}
