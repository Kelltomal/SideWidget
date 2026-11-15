using System.Diagnostics;
using SideWidget.Core;
using SideWidget.Infrastructure;

namespace SideWidget.App;

sealed partial class Controller
{
    const string SkippedUpdateKey = "personal.updates.skippedVersion";
    static readonly PortableUpdate portableUpdate = new();
    static int installingUpdate;
    GitHubUpdateResult? offeredUpdate;
    public Action<ProcessStartInfo> RestartForUpdate { get; set; } = _ => { };

    internal async Task<GitHubUpdateResult> CheckUpdate()
    {
        offeredUpdate = await githubUpdates.CheckAsync(currentAppVersion);
        return offeredUpdate;
    }

    internal async Task<GitHubUpdateResult?> CheckStartupUpdate()
    {
        var update = await CheckUpdate();
        return update.Status == GitHubUpdateStatus.Available &&
            update.LatestVersion != Store.Read<string?>(SkippedUpdateKey, null) ? update : null;
    }

    void SkipUpdate(string version)
    {
        ValidateOfferedVersion(version);
        Store.Save(SkippedUpdateKey, version);
    }

    GitHubUpdateResult ValidateOfferedVersion(string version)
    {
        return offeredUpdate is { Status: GitHubUpdateStatus.Available } offer && offer.LatestVersion == version
            ? offer : throw new CommandFault("update_version", "Проверьте обновления ещё раз.");
    }

    async Task InstallUpdate(string versionText)
    {
        var offer = ValidateOfferedVersion(versionText);
        if (offer.PortableAsset is not { } asset)
            throw new CommandFault("update_archive", "Откройте релиз на GitHub, чтобы скачать архив.");
        if (Interlocked.Exchange(ref installingUpdate, 1) != 0)
            throw new CommandFault("update_busy", "Обновление уже загружается.");
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            Emit("updates.progress", new { percent = 0 });
            var executable = await portableUpdate.PrepareAsync(asset, Version.Parse(versionText),
                Path.Combine(Store.DirectoryPath, "Versions"), percent => Emit("updates.progress", new { percent }), deadline.Token);
            RestartForUpdate(new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! });
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or HttpRequestException or OperationCanceledException or BadImageFormatException or System.Text.Json.JsonException)
        {
            throw new CommandFault("update_failed", "Не удалось установить обновление. Повторите попытку или скачайте архив с GitHub.");
        }
        finally
        {
            Interlocked.Exchange(ref installingUpdate, 0);
        }
    }
}
