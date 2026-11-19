using SideWidget.Infrastructure;

namespace SideWidget.App;

// One owner of persisted data and long-lived shared services, regardless of window count.
sealed class DesktopState : IDisposable
{
    public PersonalState Personal {get;}
    public LauncherService Launcher {get;}
    public DesktopCleanup Cleanup {get;}
    public FocusTimerService Timer {get;}
    public SideWidget.Core.PowerSchedule PowerSchedule {get;}=new();
    public OnlineServices Online {get;}
    public AudioSpectrumService Spectrum {get;}=new();
    public AudioService Audio {get;}=new();
    public MediaService Media {get;}=new();
    public PhoneRemoteService? PhoneRemote {get;set;}
    public string? PhoneRemoteError {get;set;}
    public bool PhoneRemoteInitialized {get;set;}
    public DesktopState(string directory)
    {
        Personal=new(directory);Launcher=new(Personal);Timer=new(Personal.Store);
        Cleanup=new(Personal.Store,Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),path=>{
            try {return LauncherService.ReadShortcutTarget(path);}
            catch (Exception error) when (error is System.Runtime.InteropServices.COMException or SideWidget.Core.CommandFault or UnauthorizedAccessException) {return null;}
        },Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
        Online=new(Personal.Store,new SecretVault(Path.Combine(directory,"secrets")));
        Spectrum.SetEnabled((Personal.Settings.Visualizer??new()).Enabled);
    }
    public void Dispose(){PhoneRemote?.Dispose();Spectrum.Dispose();Audio.Dispose();Media.Dispose();Online.Dispose();}
}
