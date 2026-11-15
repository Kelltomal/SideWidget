using System.Diagnostics;
using Microsoft.UI.Xaml;

namespace SideWidget.App;
internal static class Program
{
    internal static bool RestartRequested { get; set; }
    internal static bool Updating { get; set; }
    internal static ProcessStartInfo? NextBuild { get; set; }
    [STAThread]
    static void Main(string[] args)
    {
        if(PortableVerification.TryRun(args))return;
        if(PhoneNetworkSetup.TryRun(args,out var networkExitCode)){Environment.ExitCode=networkExitCode;return;}
        using(var instance=new Mutex(true,"Local\\SideWidget.Desktop",out var first))
        {
            if(!first)return;
            try
            {
                WinRT.ComWrappersSupport.InitializeComWrappers();
                Application.Start(parameters => {
                    var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    _ = new DesktopApp();
                });
            }
            finally { instance.ReleaseMutex(); }
        }
        // Start only after window cleanup and release of the single-instance lock.
        if(NextBuild is not null)
        {
            try
            {
                using var launched=Process.Start(NextBuild);
                if(launched is not null)
                {
                    if(Updating)
                    {
                        try { new StartupService(Environment.ProcessPath!).TransferTo(NextBuild.FileName); }
                        catch(SideWidget.Core.CommandFault) { }
                    }
                    return;
                }
            }
            catch(System.ComponentModel.Win32Exception){}
            catch(InvalidOperationException){}
            RestartRequested=true;
        }
        if(!RestartRequested)return;
        var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,WorkingDirectory=Environment.CurrentDirectory};
        foreach(var argument in args)start.ArgumentList.Add(argument);
        using var restarted=Process.Start(start);
    }
}
sealed partial class DesktopApp : Application
{
    public DesktopApp(){InitializeComponent();}
    private DesktopWindows? windows;
    private Window? failureWindow;
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SideWidget");
            if(Environment.GetCommandLineArgs().Contains("--measure-opening")&&
               Environment.GetEnvironmentVariable("SIDEWIDGET_MEASUREMENT_DIRECTORY") is {Length:>0} measurementDirectory)
                directory=Path.GetFullPath(measurementDirectory);
            windows=new DesktopWindows(directory);
            windows.Start();
        }
        catch(Exception error)
        {
            var message=error is SideWidget.Core.CommandFault fault?DesktopLanguage.Text(fault.Message):
                DesktopLanguage.Text("Не удалось открыть SideWidget. Проверьте доступ к локальным данным и перезапустите приложение. Тип ошибки: {0}",error.GetType().Name);
            var text=new Microsoft.UI.Xaml.Controls.TextBlock{Text=message,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(24),FontSize=16,FontFamily=new Microsoft.UI.Xaml.Media.FontFamily("ms-appx:///Assets/Fonts/MedeoSans.ttf#Medeo Sans")};
            var failure=failureWindow=new Window{Title=DesktopLanguage.Text("SideWidget — ошибка запуска"),Content=text};
            failure.AppWindow.Resize(new Windows.Graphics.SizeInt32(520,260));failure.Activate();
        }
    }
}
