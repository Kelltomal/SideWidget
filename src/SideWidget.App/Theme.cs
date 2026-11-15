using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace SideWidget.App;

// Keep native brush instances: animations can reverse from their displayed colors.
sealed class ThemeAnimator
{
    readonly Storyboard storyboard=new();
    public void Apply(bool animate,params (SolidColorBrush Brush,string Hex)[] targets)
    {
        var from=targets.Select(target=>target.Brush.Color).ToArray();
        storyboard.Stop();storyboard.Children.Clear();
        for(var i=0;i<targets.Length;i++){
            var (brush,hex)=targets[i];
            var to=Color.FromArgb(255,Convert.ToByte(hex[..2],16),Convert.ToByte(hex[2..4],16),Convert.ToByte(hex[4..],16));
            brush.Color=to;
            if(!animate)continue;
            var animation=new ColorAnimation{From=from[i],To=to,Duration=new Duration(TimeSpan.FromMilliseconds(240)),EnableDependentAnimation=true};
            Storyboard.SetTarget(animation,brush);Storyboard.SetTargetProperty(animation,"Color");storyboard.Children.Add(animation);
        }
        if(animate)storyboard.Begin();
    }
}

sealed partial class Controller
{
    public Action<string> ThemeChanged {get;set;}=_=>{};
}

public sealed partial class MainWindow
{
    readonly UISettings themeSettings=new();
    readonly ThemeAnimator themeAnimator=new();
    bool? currentLightTheme;
    bool SystemLightTheme(){var color=themeSettings.GetColorValue(UIColorType.Background);return color.R*.299+color.G*.587+color.B*.114>128;}
    void InitializeTheme()
    {
        controller.ThemeChanged=_=>ApplyTheme();ApplyTheme(false);
        themeSettings.ColorValuesChanged+=SystemThemeChanged;
        Closed+=(_,_)=>themeSettings.ColorValuesChanged-=SystemThemeChanged;
    }
    void SystemThemeChanged(UISettings sender,object args)=>DispatcherQueue.TryEnqueue(()=>{
        if(closing)return;
        if(controller.Settings.Theme=="system")ApplyTheme();
        SendEvent("theme.system",new{light=SystemLightTheme()});
    });
    void ApplyTheme(bool animate=true)
    {
        var light=controller.Settings.Theme=="light"||controller.Settings.Theme=="system"&&SystemLightTheme();
        if(currentLightTheme==light)return;currentLightTheme=light;
        root.RequestedTheme=light?ElementTheme.Light:ElementTheme.Dark;
        clock.Foreground??=Brush("F2F4F5");
        var targets=new List<(SolidColorBrush,string)>{((SolidColorBrush)root.Background,light?"F4F5F6":"181A1C"),((SolidColorBrush)clock.Foreground,light?"20262C":"F2F4F5"),((SolidColorBrush)day.Foreground,light?"59636E":"A9B1BA"),((SolidColorBrush)date.Foreground,light?"59636E":"A9B1BA"),((SolidColorBrush)timerTint.Background,light?"455D74":"B6C3D0")};
        if(resizeGrip.Children[0] is Microsoft.UI.Xaml.Controls.TextBlock resize)targets.Add(((SolidColorBrush)resize.Foreground,light?"59636E":"A9B1BA"));
        themeAnimator.Apply(animate&&!ReducedMotion(),targets.ToArray());
        web.DefaultBackgroundColor=light?Color.FromArgb(255,244,245,246):Color.FromArgb(255,24,26,28);
        tooltip?.ApplyTheme(light,animate&&!ReducedMotion());
        RefreshAudioVisualizerSettings();
    }
}
