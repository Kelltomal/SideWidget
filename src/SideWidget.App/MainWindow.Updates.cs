namespace SideWidget.App;

public sealed partial class MainWindow
{
    static bool startupUpdateStarted;
    object? startupUpdateOffer;

    async Task MaybeShowStartupUpdate()
    {
        controller.RestartForUpdate = launch => DispatcherQueue.TryEnqueue(() =>
        {
            if (closing) return;
            Program.NextBuild = launch;
            Program.Updating = true;
            desktop.Stop();
        });
        if (startupUpdateOffer is { } pendingOffer)
        {
            SendEvent("updates.available", pendingOffer);
            return;
        }
        if (!primaryWindow || startupUpdateStarted ||
            Environment.GetCommandLineArgs().Any(argument => argument is "--diagnostics" or "--measure-opening" or "--prototype")) return;
        startupUpdateStarted = true;
        try
        {
            if (await controller.CheckStartupUpdate() is not { } update || closing || !controller.Ready) return;
            startupUpdateOffer = update;
            motion.Editing = true;
            compactPanel.Reveal();
            OpenInteractive(true);
            SendEvent("updates.available", update);
        }
        catch (Exception error) when (error is Microsoft.Data.Sqlite.SqliteException or System.IO.IOException or UnauthorizedAccessException)
        {
            // Checking once at startup must never interrupt use of local tools.
        }
    }
}
