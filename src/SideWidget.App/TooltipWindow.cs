using System.Security;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using SideWidget.Core;

namespace SideWidget.App;

// Separate non-activating, click-through surface outside the panel's WebView.
sealed class TooltipWindow : Window
{
    readonly nint handle;
    readonly Border surface;
    readonly TextBlock label=new(),description=new();
    readonly Viewbox preview=new(){Width=28,Height=28,VerticalAlignment=VerticalAlignment.Top};
    Microsoft.UI.Xaml.Shapes.Path? previewPath;
    readonly Native.SubclassProc subclass;
    bool initialized;
    readonly ThemeAnimator themeAnimator=new();
    public void ApplyTheme(bool light,bool animate=false)
    {
        surface.RequestedTheme=light?ElementTheme.Light:ElementTheme.Dark;
        var targets=new List<(SolidColorBrush,string)>{((SolidColorBrush)surface.Background,light?"E5E9ED":"141618"),((SolidColorBrush)label.Foreground,light?"20262C":"F2F4F5"),((SolidColorBrush)description.Foreground,light?"59636E":"A9B1BA")};
        if(previewPath?.Stroke is SolidColorBrush stroke)targets.Add((stroke,light?"59636E":"A9B1BA"));
        themeAnimator.Apply(animate,targets.ToArray());
    }
    TooltipBounds? placed;
    static SolidColorBrush Color(string hex)=>new(Windows.UI.Color.FromArgb(255,Convert.ToByte(hex[..2],16),Convert.ToByte(hex[2..4],16),Convert.ToByte(hex[4..],16)));
    public TooltipWindow()
    {
        Title="SideWidget — подсказка";
        var font=new FontFamily("ms-appx:///Assets/Fonts/MedeoSans.ttf#Medeo Sans");
        label.FontFamily=font;label.FontSize=14;label.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;label.Foreground=Color("F2F4F5");label.TextWrapping=TextWrapping.Wrap;
        description.FontFamily=font;description.FontSize=12;description.Foreground=Color("A9B1BA");description.TextWrapping=TextWrapping.Wrap;description.Margin=new Thickness(0,4,0,0);
        var text=new StackPanel();text.Children.Add(label);text.Children.Add(description);
        var layout=new Grid{ColumnSpacing=12};layout.ColumnDefinitions.Add(new(){Width=new GridLength(28)});layout.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});layout.Children.Add(preview);Grid.SetColumn(text,1);layout.Children.Add(text);
        surface=new Border{Background=Color("141618"),CornerRadius=new CornerRadius(0,0,16,16),Padding=new Thickness(14,12,14,12),Child=layout,RequestedTheme=ElementTheme.Dark,IsHitTestVisible=false};
        Content=surface;handle=WinRT.Interop.WindowNative.GetWindowHandle(this);
        var presenter=OverlappedPresenter.Create();presenter.SetBorderAndTitleBar(false,false);presenter.IsResizable=false;presenter.IsMinimizable=false;presenter.IsMaximizable=false;AppWindow.SetPresenter(presenter);AppWindow.IsShownInSwitchers=false;
        Native.SetWindowLong(handle,-20,(Native.GetWindowLong(handle,-20)&~0x00040301)|0x080000A0);
        Native.SetWindowLong(handle,-16,Native.GetWindowLong(handle,-16)&~0x00c40000);
        subclass=Procedure;
        Native.SetWindowSubclass(handle,subclass,1,0);
        var none=unchecked((int)0xFFFFFFFE);Native.DwmSetWindowAttribute(handle,34,ref none,4);
        var noCorners=1;Native.DwmSetWindowAttribute(handle,33,ref noCorners,4);
    }
    nint Procedure(nint window,uint message,nuint wParam,nint lParam,nuint id,nuint data)
    {
        if(message==0x0083&&wParam!=0)return 0;
        if(message==0x0084)return new nint(-1);
        if(message==0x0021)return new nint(3);
        if(message==0x007c&&unchecked((int)wParam)==-20&&lParam!=0)
        {
            var styles=Marshal.PtrToStructure<Native.StyleChange>(lParam);
            styles.NewStyle=(styles.NewStyle&~0x00040301u)|0x080000A0u;
            Marshal.StructureToPtr(styles,lParam,false);
        }
        return Native.DefSubclassProc(window,message,wParam,lParam);
    }
    public void Update(TooltipContent content)
    {
        label.Text=content.Label;description.Text=content.Description;
        var path=string.IsNullOrWhiteSpace(content.Path)?"M5 12h14m-5-5 5 5-5 5":content.Path;
        // Only validated SVG path commands reach XAML; never accept markup or URLs.
        previewPath=(Microsoft.UI.Xaml.Shapes.Path)XamlReader.Load("<Path xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Data=\""+SecurityElement.Escape(path)+"\" Stroke=\"#A9B1BA\" StrokeThickness=\"1.6\" StrokeStartLineCap=\"Round\" StrokeEndLineCap=\"Round\" StrokeLineJoin=\"Round\" Stretch=\"None\"/>");
        // Preserve the SVG's 24×24 viewBox, including whitespace and open paths.
        // Stretching the path bounds alone enlarges thin icons and moves their origin.
        var viewport=new Canvas{Width=24,Height=24};viewport.Children.Add(previewPath);preview.Child=viewport;
    }
    public void Place(TooltipBounds bounds,double scale)
    {
        if(placed==bounds)return;placed=bounds;
        surface.Width=bounds.Width/scale;surface.Height=bounds.Height/scale;
        if(!initialized){var foreground=Native.GetForegroundWindow();Activate();initialized=true;if(foreground!=0&&Native.GetForegroundWindow()!=foreground)Native.SetForegroundWindow(foreground);}
        Native.SetWindowPos(handle,new nint(-1),bounds.Left,bounds.Top,bounds.Width,bounds.Height,0x0010|0x0040);
        var corner=(int)Math.Round(32*scale);Native.SetWindowRgn(handle,Native.CreateRoundRectRgn(0,-corner,bounds.Width+1,bounds.Height+1,corner,corner),true);
    }
    public void Hide(){placed=null;Native.ShowWindow(handle,0);}
}

public sealed partial class MainWindow
{
    TooltipWindow? tooltip;
    TooltipContent? tooltipContent;
    void SetTooltip(TooltipContent? value)
    {
        tooltipContent=value;
        if(value==null){tooltip?.Hide();return;}
        tooltip??=new TooltipWindow();tooltip.Update(value);tooltip.ApplyTheme(currentLightTheme==true);PositionTooltip();
    }
    void PositionTooltip()
    {
        if(tooltipContent==null||tooltip==null)return;
        if(motion.State!=PanelState.Expanded||closing||dialogCount>0||motion.Suspended){tooltip.Hide();return;}
        var work=Monitor().Work;
        var desired=tooltipContent.Description.Length>140?132:108;
        var bounds=TooltipLayout.Below(x,y+(int)Math.Round(motion.Height*scale),width,work.Left,work.Right,work.Bottom,scale,desired);
        if(bounds is {} visible)tooltip.Place(visible,scale);else tooltip.Hide();
    }
}
