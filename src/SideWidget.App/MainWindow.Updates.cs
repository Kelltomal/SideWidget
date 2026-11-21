using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace SideWidget.App;

public sealed partial class MainWindow
{
    static bool startupUpdateStarted;
    object? startupUpdateOffer;
    readonly TextBlock updateLabel = new() { FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly ProgressBar updateProgress = new() { Maximum = 100 };
    readonly Border updateHeader = new() { Visibility = Visibility.Collapsed };
    bool updateInProgress;
    int updateGeneration;
    int? updateTop;

    void InitializeUpdateHeader(FontFamily font)
    {
        updateLabel.FontFamily = font;
        updateLabel.Foreground = day.Foreground;
        updateProgress.Foreground = clock.Foreground;
        updateHeader.Background = root.Background;
        updateHeader.Padding = new Thickness(0, 0, 0, 3);
        updateHeader.Child = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Spacing = 5,
            Children = { updateLabel, updateProgress }
        };
        Grid.SetColumnSpan(updateHeader, 3);
        header.Children.Add(updateHeader);
        controller.BeginUpdate = BeginUpdate;
        controller.RestoreUpdatePrompt = RestoreUpdatePrompt;
        controller.RestartForUpdate = launch => DispatcherQueue.TryEnqueue(() =>
        {
            if (closing) return;
            Program.NextBuild = launch;
            Program.Updating = true;
            desktop.Stop();
        });
    }

    IProgress<int?> BeginUpdate()
    {
        var generation = ++updateGeneration;
        updateTop = y;
        updateInProgress = true;
        updateHeader.Visibility = Visibility.Visible;
        SetUpdateProgress(0);
        tooltip?.Hide();
        Collapse();
        return new Progress<int?>(percent =>
        {
            if (!closing && updateInProgress && generation == updateGeneration) SetUpdateProgress(percent);
        });
    }

    void SetUpdateProgress(int? percent)
    {
        updateLabel.Text = percent is int value
            ? DesktopLanguage.Text("Загружаем обновление… {0}%", value)
            : DesktopLanguage.Text("Устанавливаем обновление…");
        // The archive is fully downloaded during verification and installation.
        updateProgress.Value = percent ?? 100;
    }

    void RestoreUpdatePrompt()
    {
        if (closing || !updateInProgress) return;
        if (dragging) FinishDrag(false);
        updateInProgress = false;
        updateTop = null;
        updateHeader.Visibility = Visibility.Collapsed;
        compactPanel.Reveal();
        OpenInteractive(true);
        motion.Editing = true;
    }

    async Task MaybeShowStartupUpdate()
    {
        if (updateInProgress) return;
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
