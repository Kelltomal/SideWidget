using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SideWidget.Remote;

public sealed class RemoteFault(string message) : Exception(message);
public sealed record RemoteInvite(string Token, DateTimeOffset Expires);
public sealed record PairStarted(string Id, string Secret, string Code);
public sealed record PairResult(string Status, string? Token = null, string? DeviceId = null);
public sealed record RemoteDevice(string Id, string Name, string KeyHash, DateTimeOffset Created, DateTimeOffset LastSeen);
public sealed record PendingDevice(string Id, string Name, string Code, DateTimeOffset Expires);
public sealed class RemoteAccess
{
    readonly object gate = new();
    readonly string path;
    readonly Func<DateTimeOffset> clock;
    readonly List<RemoteDevice> devices;
    readonly Dictionary<string, Pending> pending = new(StringComparer.Ordinal);
    string? inviteHash;
    DateTimeOffset inviteExpires;
    sealed record Saved(int Version, List<RemoteDevice> Devices);
    sealed class Pending(PendingDevice info, string secretHash)
    {
        public PendingDevice Info { get; } = info;
        public string SecretHash { get; } = secretHash;
        public PairResult Result { get; set; } = new("pending");
    }

    public RemoteAccess(string path, Func<DateTimeOffset>? clock = null)
    {
        this.path = path; this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        if (!File.Exists(path)) { devices = []; return; }
        if (new FileInfo(path).Length > 65536) throw new RemoteFault("Файл допущенных телефонов повреждён.");
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(path));
        if (saved is not { Version: 1, Devices: not null } || saved.Devices.Count > 8 ||
            saved.Devices.Any(d => d is null || d.KeyHash is not { Length: 64 } || d.Name is null || d.Name.Length > 60 || !Guid.TryParseExact(d.Id, "N", out _)) ||
            saved.Devices.Select(d => d.Id).Distinct().Count() != saved.Devices.Count)
            throw new RemoteFault("Файл допущенных телефонов повреждён или создан новой версией.");
        devices = saved.Devices;
    }

    public RemoteInvite CreateInvite()
    {
        lock (gate) { var token = Key(); inviteHash = Hash(token); inviteExpires = clock().AddMinutes(5); return new(token, inviteExpires); }
    }

    public PairStarted BeginPair(string invite, string name)
    {
        lock (gate)
        {
            Prune();
            if (inviteHash is null || clock() >= inviteExpires || !Matches(invite, inviteHash)) throw new RemoteFault("QR недействителен. Создайте новый в SideWidget.");
            if (devices.Count >= 8 || pending.Values.Count(p => p.Result.Status == "pending") >= 4) throw new RemoteFault("Лимит телефонов достигнут. Удалите старый пульт на ПК.");
            if (pending.Count >= 16) pending.Remove(pending.First(p => p.Value.Result.Status != "pending").Key);
            if (string.IsNullOrWhiteSpace(name) || name.Length > 60 || name.Any(char.IsControl)) throw new RemoteFault("Укажите имя телефона до 60 символов.");
            inviteHash = null;
            var id = Guid.NewGuid().ToString("N"); var secret = Key();
            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            pending.Add(id, new(new(id, name.Trim(), code, clock().AddMinutes(5)), Hash(secret)));
            return new(id, secret, code);
        }
    }

    public PairResult PollPair(string id, string secret)
    {
        lock (gate)
        {
            Prune();
            if (!pending.TryGetValue(id, out var request) || !Matches(secret, request.SecretHash)) throw new RemoteFault("Подключение истекло. Отсканируйте новый QR.");
            if (request.Result.DeviceId is { } deviceId && !devices.Any(d => d.Id == deviceId)) return new("denied");
            return request.Result;
        }
    }

    public void Decide(string id, bool approved)
    {
        lock (gate)
        {
            Prune();
            if (!pending.TryGetValue(id, out var request) || request.Result.Status != "pending") throw new RemoteFault("Запрос подключения уже завершён.");
            if (!approved) { request.Result = new("denied"); return; }
            if (devices.Count >= 8) throw new RemoteFault("Удалите один из восьми допущенных телефонов.");
            var token = Key(); var device = new RemoteDevice(Guid.NewGuid().ToString("N"), request.Info.Name, Hash(token), clock(), clock());
            devices.Add(device);
            try { Save(); } catch { devices.Remove(device); throw; }
            request.Result = new("approved", token, device.Id);
        }
    }

    public string? Authorize(string token)
    {
        lock (gate)
        {
            var index = devices.FindIndex(d => Matches(token, d.KeyHash));
            if (index < 0) return null;
            devices[index] = devices[index] with { LastSeen = clock() };
            return devices[index].Id;
        }
    }

    public void Revoke(string id)
    {
        lock (gate)
        {
            var index = devices.FindIndex(d => d.Id == id);
            if (index < 0) throw new RemoteFault("Телефон уже удалён.");
            var device = devices[index]; devices.RemoveAt(index);
            try { Save(); } catch { devices.Insert(index, device); throw; }
        }
    }

    public object Info()
    {
        lock (gate)
        {
            Prune();
            return new { devices = devices.Select(d => new { d.Id, d.Name, d.Created, d.LastSeen }).ToArray(),
                pending = pending.Values.Where(p => p.Result.Status == "pending").Select(p => p.Info).ToArray() };
        }
    }
    public void ClearEphemeral() { lock (gate) { inviteHash = null; pending.Clear(); } }
    void Prune() { foreach (var id in pending.Where(p => p.Value.Info.Expires <= clock()).Select(p => p.Key).ToArray()) pending.Remove(id); }
    void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(new Saved(1, devices))); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    static string Key() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    static bool Matches(string value, string hash) => value.Length == 64 && hash.Length == 64 &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(value)), Encoding.ASCII.GetBytes(hash));
}
