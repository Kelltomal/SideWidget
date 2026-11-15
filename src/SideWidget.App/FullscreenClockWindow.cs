using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.System;

namespace SideWidget.App;

sealed class FullscreenClockWindow : Window
{
    readonly Grid surface=new();
    readonly TextBlock time=new()
    {
        FontFamily=new FontFamily("ms-appx:///Assets/Fonts/MedeoSans.ttf#Medeo Sans"),
        FontSize=64,
        FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,
        TextAlignment=TextAlignment.Center
    };
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)};

    public FullscreenClockWindow(bool light)
    {
        Title=DesktopLanguage.Text("Часы на весь экран");
        surface.Language=DesktopLanguage.Language;
        surface.RequestedTheme=light?ElementTheme.Light:ElementTheme.Dark;
        surface.Background=new SolidColorBrush(light?Color(0xf4,0xf5,0xf6):Color(0x18,0x1a,0x1c));
        time.Foreground=new SolidColorBrush(light?Color(0x20,0x26,0x2c):Color(0xf2,0xf4,0xf5));
        Typography.SetNumeralAlignment(time,FontNumeralAlignment.Tabular);
        var clockSurface=new Grid{Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent)};
        clockSurface.Children.Add(new Viewbox{Child=time,Margin=new Thickness(32,64,32,64),Stretch=Stretch.Uniform});
        clockSurface.Tapped+=(_,_)=>Close();
        surface.Children.Add(clockSurface);
        var exit=new Button
        {
            Content=DesktopLanguage.Text("Выйти из полноэкранного режима"),
            HorizontalAlignment=HorizontalAlignment.Right,
            VerticalAlignment=VerticalAlignment.Top,
            Margin=new Thickness(16)
        };
        exit.Click+=(_,_)=>Close();
        exit.Loaded+=(_,_)=>exit.Focus(FocusState.Programmatic);
        surface.Children.Add(exit);
        surface.KeyDown+=(_,e)=>
        {
            if(e.Key!=VirtualKey.Escape)return;
            e.Handled=true;
            Close();
        };
        Content=surface;
        AppWindow.IsShownInSwitchers=false;
        RefreshTime();
        timer.Tick+=(_,_)=>RefreshTime();
        Closed+=(_,_)=>timer.Stop();
    }

    public void ShowOn(Native.Rect monitor)
    {
        // Move onto the target display before requesting its fullscreen bounds.
        AppWindow.MoveAndResize(new RectInt32(monitor.Left,monitor.Top,monitor.Right-monitor.Left,monitor.Bottom-monitor.Top));
        AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        Activate();
        var hwnd=WinRT.Interop.WindowNative.GetWindowHandle(this);
        Native.SetWindowPos(hwnd,new nint(-1),0,0,0,0,0x0001|0x0002|0x0010);
        timer.Start();
    }

    void RefreshTime()
    {
        var value=DateTime.Now.ToString("HH':'mm",DesktopLanguage.Culture);
        if(time.Text!=value)time.Text=value;
    }

    static Windows.UI.Color Color(byte red,byte green,byte blue)=>Windows.UI.Color.FromArgb(255,red,green,blue);
}
