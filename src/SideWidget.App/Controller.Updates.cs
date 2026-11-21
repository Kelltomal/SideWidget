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
    CancellationTokenSource? githubSignIn;
    public Action<ProcessStartInfo> RestartForUpdate { get; set; } = _ => { };
    public Func<IProgress<int?>> BeginUpdate { get; set; } = () => new Progress<int?>();
    public Action RestoreUpdatePrompt { get; set; } = () => { };

    internal async Task<GitHubUpdateResult> CheckUpdate()
    {
        var lite = LiteInstallation.IsLite;
        var preferLite = !lite && await Task.Run(LiteDependencies.AreAvailable);
        var connection = desktopState.GitHub;
        var (token, reauthenticate) = await connection.AccessAsync();
        if (reauthenticate)
        {
            offeredUpdate = null;
            return new(GitHubUpdateStatus.AuthRequired, appVersion, ReleaseUrl: GitHubUpdates.ReleasesPage);
        }
        var update = await githubUpdates.CheckAsync(currentAppVersion, lite, preferLite: preferLite, accessToken: token);
        if (update.Status == GitHubUpdateStatus.AuthRequired)
            await connection.DisconnectAsync(token);
        offeredUpdate = update;
        return update;
    }

    async Task<object> ConnectGitHub()
    {
        if (githubSignIn is not null) throw new CommandFault("github_busy", "Вход в GitHub уже открыт.");
        using var cancellation = new CancellationTokenSource();
        githubSignIn = cancellation;
        IProgress<string> progress = new Progress<string>(code =>
        {
            if (!cancellation.IsCancellationRequested)
                Emit("updates.github.code", new { code });
        });
        try
        {
            await desktopState.GitHub.ConnectAsync(progress.Report, cancellation.Token);
            return new { connected = true };
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return new { connected = desktopState.GitHub.Connected };
        }
        finally { githubSignIn = null; }
    }

    async Task<object> DisconnectGitHub()
    {
        githubSignIn?.Cancel();
        await desktopState.GitHub.DisconnectAsync();
        return new { connected = false };
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
        var restartScheduled = false;
        try
        {
            var progress = BeginUpdate();
            if (asset.IsLite && !LiteInstallation.IsLite &&
                !await Task.Run(LiteDependencies.AreAvailable))
                throw new InvalidDataException("Shared Lite dependencies are unavailable.");

            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            var executable = await portableUpdate.PrepareAsync(asset, Version.Parse(versionText),
                Path.Combine(Store.DirectoryPath, "Versions"), progress.Report,
                asset.IsLite, deadline.Token);
            if (asset.IsLite)
            {
                var prepare = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! };
                prepare.ArgumentList.Add("--install-dependencies");
                using var launcher = Process.Start(prepare) ?? throw new IOException("Could not start the Lite launcher.");
                await launcher.WaitForExitAsync(deadline.Token);
                if (launcher.ExitCode != 0) throw new IOException("Could not prepare Lite dependencies.");
            }
            RestartForUpdate(new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! });
            restartScheduled = true;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or HttpRequestException or OperationCanceledException or BadImageFormatException or System.ComponentModel.Win32Exception or System.Text.Json.JsonException)
        {
            throw new CommandFault("update_failed", "Не удалось установить обновление. Повторите попытку или скачайте архив с GitHub.");
        }
        finally
        {
            Interlocked.Exchange(ref installingUpdate, 0);
            if (!restartScheduled) RestoreUpdatePrompt();
        }
    }
}
