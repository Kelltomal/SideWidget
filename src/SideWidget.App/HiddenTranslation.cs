using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using SideWidget.Core;

namespace SideWidget.App;
public sealed partial class MainWindow
{
    CancellationTokenSource? websiteTranslation;
    static readonly string TranslationReader = LoadTranslationReader();
    static string LoadTranslationReader()
    {
        using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("SideWidget.App.Assets.Translation.read-result.js")
            ?? throw new InvalidOperationException("Translation reader resource missing");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    void CancelWebsiteTranslation() => websiteTranslation?.Cancel();

    async Task<object> TranslateWebsite(WebTranslationRequest request)
    {
        CancelWebsiteTranslation();
        using var cancellation = new CancellationTokenSource();
        websiteTranslation = cancellation;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token, timeout.Token);
        var token = linked.Token;
        var verificationVisible = false;
        var verificationRequested = false;
        var verificationShown = false;
        FrameworkElement? verificationHeader = null;
        var remote = new WebView2 { Width = 800, Height = 600, Opacity = 0, IsHitTestVisible = false, IsTabStop = false };
        // Keep a real layout viewport for website scripts, but paint no pixels
        // and expose no input surface. The local application WebView stays intact.
        var host = new Grid { Width = 800, Height = 600, Opacity = 0, IsHitTestVisible = false,
            Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, 0, 0) } };
        AutomationProperties.SetAccessibilityView(host, AccessibilityView.Raw);
        AutomationProperties.SetAccessibilityView(remote, AccessibilityView.Raw);
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        remote.Loaded += (_, _) => loaded.TrySetResult();
        remote.GotFocus += (_, _) => { if (!closing && !verificationVisible) web.Focus(FocusState.Programmatic); };
        host.Children.Add(remote); Grid.SetRow(host, 1);
        CommandFault? failure = null;
        async Task ShowVerification()
        {
            if (verificationVisible) return;
            await dialogs.WaitAsync(token);
            verificationVisible = true;
            if (!verificationShown) timeout.CancelAfter(TimeSpan.FromMinutes(10));
            verificationShown = true;
            dialogCount++; motion.Editing = true;
            token.ThrowIfCancellationRequested();
            OpenInteractive(true);
            var font = new FontFamily("ms-appx:///Assets/Fonts/MedeoSans.ttf#Medeo Sans");
            var bar = new Grid { Padding = new Thickness(16), ColumnSpacing = 12, Background = Brush("181A1C") };
            bar.ColumnDefinitions.Add(new()); bar.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            var label = new TextBlock { Text = $"Проверка {request.Name}\nПройдите капчу. Перевод продолжится автоматически.", FontFamily = font, FontSize = 14, TextWrapping = TextWrapping.Wrap, Foreground = Brush("F2F4F5") };
            var cancel = new Button { Content = "Отмена", FontFamily = font, VerticalAlignment = VerticalAlignment.Top };
            cancel.Click += (_, _) => cancellation.Cancel();
            Grid.SetColumn(cancel, 1); bar.Children.Add(label); bar.Children.Add(cancel);
            verificationHeader = bar;
            host.RowDefinitions.Add(new() { Height = GridLength.Auto });
            host.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(remote, 1); host.Children.Add(bar);
            host.Width = double.NaN; host.Height = double.NaN; host.Opacity = 1; host.Clip = null; host.IsHitTestVisible = true;
            remote.Width = double.NaN; remote.Height = double.NaN; remote.Opacity = 1; remote.IsHitTestVisible = true; remote.IsTabStop = true;
            AutomationProperties.SetAccessibilityView(host, AccessibilityView.Content);
            AutomationProperties.SetAccessibilityView(remote, AccessibilityView.Content);
            web.Visibility = Visibility.Collapsed;
            focusNativeDialog = () => cancel.Focus(FocusState.Programmatic);
            cancel.Focus(FocusState.Programmatic);
        }
        void HideVerification()
        {
            if (!verificationVisible) return;
            verificationVisible = false;
            focusNativeDialog = null;
            if (!closing)
            {
                host.Opacity = 0; host.IsHitTestVisible = false;
                host.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, 0, 0) };
                host.Width = 800; host.Height = 600;
                remote.Opacity = 0; remote.IsHitTestVisible = false; remote.IsTabStop = false; remote.Width = 800; remote.Height = 600;
                if (verificationHeader != null) host.Children.Remove(verificationHeader);
                host.RowDefinitions.Clear(); Grid.SetRow(remote, 0);
                AutomationProperties.SetAccessibilityView(host, AccessibilityView.Raw);
                AutomationProperties.SetAccessibilityView(remote, AccessibilityView.Raw);
                web.Visibility = Visibility.Visible; web.Focus(FocusState.Programmatic);
            }
            dialogCount--; motion.Editing = webEditing || dialogCount > 0; dialogs.Release();
        }
        host.KeyDown += (_, e) => { if (verificationVisible && e.Key == Windows.System.VirtualKey.Escape) { e.Handled = true; cancellation.Cancel(); } };
        var escape = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape, ScopeOwner = host };
        escape.Invoked += (_, e) => { if (verificationVisible) { e.Handled = true; cancellation.Cancel(); } };
        host.KeyboardAccelerators.Add(escape);
        try
        {
            if (closing) throw new OperationCanceledException();
            root.Children.Insert(0, host);
            await loaded.Task.WaitAsync(token);
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(BundledBrowser.Folder(),
                Path.Combine(controller.Store.DirectoryPath, "TranslationWebView2"), new CoreWebView2EnvironmentOptions()).AsTask(token);
            var options = environment.CreateCoreWebView2ControllerOptions();
            // Dedicated profile retains only this embedded service's session,
            // so a manual verification can survive subsequent translations.
            options.ProfileName = "Translation";
            await remote.EnsureCoreWebView2Async(environment, options).AsTask(token);
            token.ThrowIfCancellationRequested();
            var core = remote.CoreWebView2;
            core.Settings.IsWebMessageEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.IsMuted = true;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.NavigationStarting += (_, e) =>
            {
                if (!request.AllowsNavigation(e.Uri))
                { e.Cancel = true; failure = new("origin", $"{request.Name} перешёл на неподдерживаемый адрес. Вход в аккаунт здесь недоступен."); }
                else verificationRequested = request.RequiresVerification(e.Uri);
            };
            core.NavigationCompleted += (_, e) => { if (!e.IsSuccess && failure == null) failure = new("offline", $"Страница {request.Name} не загрузилась. Проверьте соединение и повторите."); };
            core.ProcessFailed += (_, _) => failure = new("translation", "Переводчик остановился. Повторите попытку.");
            // Serialization escapes quotes and script delimiters in user text.
            var script = TranslationReader.Replace("__REQUEST_JSON__", JsonSerializer.Serialize(new { provider = request.Provider, text = request.Text, target = request.Target }));
            core.Navigate(request.Uri.AbsoluteUri);
            string? previousResult = null;
            var stable = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (failure != null) throw failure;
                if (verificationRequested) await ShowVerification();
                if (request.Allows(core.Source) && !request.RequiresVerification(core.Source))
                {
                    var json = await core.ExecuteScriptAsync(script).AsTask(token);
                    token.ThrowIfCancellationRequested();
                    if (failure != null) throw failure;
                    if (!request.Allows(core.Source)) throw new CommandFault("origin", "Страница переводчика изменила адрес");
                    // A fresh view per request + matching source field prevents
                    // returning a preceding translation. Require stable output.
                    string? result = null;
                    try { result = json == "null" ? null : request.ReadResult(json); }
                    catch (CommandFault error) when (error.Code == "verification") { await ShowVerification(); }
                    if (result != null && result == previousResult) stable++; else stable = 0;
                    previousResult = result;
                    if (result != null && verificationVisible) HideVerification();
                    if (result != null && stable >= 2) return new { text = result, provider = request.Name };
                }
                await Task.Delay(350, token);
            }
        }
        catch (OperationCanceledException)
        {
            throw new CommandFault(cancellation.IsCancellationRequested || closing ? "cancelled" : "timeout",
                cancellation.IsCancellationRequested || closing ? "Перевод отменён" : verificationShown ? "Время проверки истекло. Нажмите «Перевести», чтобы повторить." : $"{request.Name} не ответил за 35 секунд. Повторите или выберите другой переводчик.");
        }
        catch (Exception error) when (error is COMException or InvalidOperationException)
        { throw new CommandFault("translation", $"Не удалось запустить {request.Name}. Повторите попытку."); }
        finally
        {
            HideVerification();
            if (websiteTranslation == cancellation) websiteTranslation = null;
            try { remote.Close(); }
            catch (Exception error) when (error is COMException or InvalidOperationException) { }
            if (!closing) root.Children.Remove(host);
        }
    }
}
