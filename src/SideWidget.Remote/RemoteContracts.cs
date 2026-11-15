namespace SideWidget.Remote;

public sealed record RemoteCommand(string RequestId, string Command, string? Id = null,
    double? PositionSeconds = null, double? Volume = null, bool? Muted = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public Func<bool>? StillAuthorized { get; init; }
}

public sealed record RemoteConfiguration(int Version = 1, bool Enabled = false, string? AdapterId = null, int Port = 47831, int SetupPort = 47830)
{
    public void Validate()
    {
        if (Version != 1 || Port is < 1024 or > 65535 || SetupPort is < 1024 or > 65535 || Port == SetupPort || AdapterId?.Length > 256)
            throw new RemoteFault("Проверьте сетевой адаптер и разные порты от 1024 до 65535.");
    }
}
