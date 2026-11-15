using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using SideWidget.Core;

namespace SideWidget.App;

public sealed partial class MainWindow
{
    async Task<object> PrintQr(string html)
    {
        // This document has no bridge, script execution, host objects or external resources.
        // Keep the restrictive policy before all supplied markup, including any other CSP.
        const string policy="<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; img-src data:; base-uri 'none'; form-action 'none'\">";
        if(html.Length>1500000)throw new CommandFault("print","Документ печати слишком большой. Уменьшите количество стикеров.");
        var window=new Window{Title="SideWidget — печать QR-стикеров"};
        var view=new WebView2();
        var print=new Button{Content="Печать…",IsEnabled=false};
        var close=new Button{Content="Закрыть"};
        var controls=new StackPanel{Orientation=Orientation.Horizontal,Spacing=12};
        controls.Children.Add(print);controls.Children.Add(close);
        var status=new TextBlock{TextWrapping=TextWrapping.Wrap};
        var toolbar=new StackPanel{Spacing=8,Padding=new Thickness(12)};toolbar.Children.Add(controls);toolbar.Children.Add(status);
        var layout=new Grid();layout.RowDefinitions.Add(new(){Height=GridLength.Auto});layout.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        layout.Children.Add(toolbar);Grid.SetRow(view,1);layout.Children.Add(view);window.Content=layout;
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(700,750));
        var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var token=cancellation.Token;
        var loaded=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var navigated=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed=false;
        void ParentClosed(object sender,WindowEventArgs args){if(!closed)window.Close();}
        Closed+=ParentClosed;
        close.Click+=(_,_)=>window.Close();
        window.Closed+=(_,_)=>{closed=true;cancellation.Cancel();cancellation.Dispose();Closed-=ParentClosed;view.Close();};
        view.Loaded+=(_,_)=>loaded.TrySetResult();
        void OpenPrint()
        {
            if(closed)return;
            try{view.CoreWebView2?.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);status.Text="";}
            catch{status.Text="Не удалось открыть диалог печати. Проверьте принтер и нажмите «Печать…» ещё раз.";}
        }
        print.Click+=(_,_)=>OpenPrint();
        window.Activate();
        try
        {
            await loaded.Task.WaitAsync(token);
            var environment=await CoreWebView2Environment.CreateWithOptionsAsync(BundledBrowser.Folder(),Path.Combine(controller.Store.DirectoryPath,"QrPrintWebView2"),new CoreWebView2EnvironmentOptions()).AsTask(token);
            token.ThrowIfCancellationRequested();
            await view.EnsureCoreWebView2Async(environment).AsTask(token);
            token.ThrowIfCancellationRequested();
            var core=view.CoreWebView2;
            core.Settings.IsScriptEnabled=false;core.Settings.IsWebMessageEnabled=false;core.Settings.AreHostObjectsAllowed=false;
            core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreBrowserAcceleratorKeysEnabled=false;
            core.Settings.AreDefaultScriptDialogsEnabled=false;core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;
            core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
            core.DownloadStarting+=(_,e)=>e.Cancel=true;
            core.NewWindowRequested+=(_,e)=>e.Handled=true;
            core.NavigationStarting+=(_,e)=>{if(e.Uri!="about:blank")e.Cancel=true;};
            core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
            core.WebResourceRequested+=(_,e)=>e.Response=environment.CreateWebResourceResponse(null,403,"Forbidden","");
            core.NavigationCompleted+=(_,e)=>{if(e.IsSuccess)navigated.TrySetResult();else navigated.TrySetException(new CommandFault("print","Не удалось подготовить стикеры. Повторите печать."));};
            core.NavigateToString("<!doctype html>"+policy+html);
            await navigated.Task.WaitAsync(token);
            print.IsEnabled=true;
            // Queue print UI outside the WebView navigation callback.
            DispatcherQueue.TryEnqueue(OpenPrint);
            return new{opened=true};
        }
        catch(OperationCanceledException) when(closed){return new{opened=false};}
        catch{if(!closed)window.Close();throw new CommandFault("print","Не удалось открыть печать. Проверьте WebView2 и повторите.");}
    }
}
