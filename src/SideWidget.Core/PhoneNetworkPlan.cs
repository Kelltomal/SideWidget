using System.Net;

namespace SideWidget.Core;

public sealed record PhoneNetworkPlan(string AdapterId, string Application, string LocalAddress, string RemoteSubnet, string Ports)
{
    public const string RuleName = "SideWidget.PhoneRemote.Home";
    public const string RuleGroup = "SideWidget Phone Remote";
    public static bool AddressScopeEquals(string actual, string expected)
        => Scope(actual) is { } left && Scope(expected) is { } right && left == right;

    static (uint Network, int Prefix)? Scope(string value)
    {
        var parts = value.Trim().Split('/');
        if (parts.Length > 2 || !IPAddress.TryParse(parts[0], out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return null;
        var prefix = 32;
        if (parts.Length == 2)
        {
            if (!int.TryParse(parts[1], out prefix))
            {
                if (!IPAddress.TryParse(parts[1], out var netmask) || netmask.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return null;
                prefix = 0; var ended = false;
                foreach (var octet in netmask.GetAddressBytes()) for (var bit = 7; bit >= 0; bit--)
                {
                    if ((octet & (1 << bit)) != 0) { if (ended) return null; prefix++; }
                    else ended = true;
                }
            }
            if (prefix is < 0 or > 32) return null;
        }
        var bytes = address.GetAddressBytes();
        var packed = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        var mask = prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);
        return (packed & mask, prefix);
    }
    public static PhoneNetworkPlan Create(string adapterId, string application, IPAddress address, IPAddress mask, int port, int setupPort)
    {
        if (!Guid.TryParse(adapterId, out var id) || !Path.IsPathFullyQualified(application) || application.StartsWith(@"\\") ||
            !string.Equals(Path.GetFileName(application), "SideWidget.App.exe", StringComparison.OrdinalIgnoreCase) ||
            address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || mask.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
            port is < 1024 or > 65535 || setupPort is < 1024 or > 65535 || port == setupPort)
            throw new CommandFault("remote_network", "Некорректные параметры домашней сети.");
        var a = address.GetAddressBytes(); var m = mask.GetAddressBytes();
        if (!(a[0] == 10 || a[0] == 192 && a[1] == 168 || a[0] == 172 && a[1] is >= 16 and <= 31))
            throw new CommandFault("remote_network", "Нужен локальный IPv4 домашнего адаптера.");
        var prefix = 0; var ended = false;
        foreach (var octet in m) for (var bit = 7; bit >= 0; bit--)
        {
            if ((octet & (1 << bit)) != 0) { if (ended) throw new CommandFault("remote_network", "Некорректная маска подсети."); prefix++; }
            else ended = true;
        }
        if (prefix is < 8 or > 30) throw new CommandFault("remote_network", "Слишком широкая или некорректная домашняя подсеть.");
        var network = new IPAddress(a.Zip(m, (value, subnet) => (byte)(value & subnet)).ToArray());
        return new(id.ToString(), Path.GetFullPath(application), address.ToString(), network + "/" + prefix, port + "," + setupPort);
    }
}
