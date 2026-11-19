using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using SideWidget.Core;

namespace SideWidget.App;

sealed class CodexQuotaView:Canvas
{
    CodexQuotaIndicator? previous;
    bool? previousLight;
    public CodexQuotaView(){Width=30;Height=30;IsHitTestVisible=false;VerticalAlignment=VerticalAlignment.Center;}
    public static string Description(CodexQuotaIndicator quota)
    {
        string Value(double? value)=>value is null?DesktopLanguage.Text("Неизвестно"):DesktopLanguage.Text("Осталось {0}%",value);
        return "Codex · "+DesktopLanguage.Text("Неделя")+": "+Value(quota.WeeklyRemaining)+" · "+
            DesktopLanguage.Text("5 часов")+": "+Value(quota.FiveHourRemaining);
    }
    public void Update(CodexQuotaIndicator? quota,bool light)
    {
        if(previous==quota&&previousLight==light)return;
        previous=quota;previousLight=light;Children.Clear();
        if(quota is null)return;
        SolidColorBrush Brush(string hex)=>new(Windows.UI.Color.FromArgb(255,Convert.ToByte(hex[..2],16),Convert.ToByte(hex[2..4],16),Convert.ToByte(hex[4..],16)));
        var muted=Brush(light?"59636E":"A9B1BA");
        foreach(var (radius,remaining) in new[]{(13d,quota.WeeklyRemaining),(9d,quota.FiveHourRemaining)})
        {
            var track=new Ellipse{Width=radius*2,Height=radius*2,Stroke=Brush(light?"C9CED3":"30373D"),StrokeThickness=1.6};
            SetLeft(track,15-radius);SetTop(track,15-radius);Children.Add(track);
            var stroke=remaining is null?muted:Brush(remaining<=15?(light?"A9392A":"FFC8C1"):(light?"455D74":"B6C3D0"));
            if(remaining is null||remaining>=100)
            {
                var ring=new Ellipse{Width=radius*2,Height=radius*2,Stroke=stroke,StrokeThickness=1.6};
                if(remaining is null)ring.StrokeDashArray=new(){2,4};
                SetLeft(ring,15-radius);SetTop(ring,15-radius);Children.Add(ring);
            }
            else if(remaining>0)
            {
                var angle=remaining.Value/100*Math.PI*2;
                var figure=new PathFigure{StartPoint=new(15,15-radius),IsClosed=false};
                figure.Segments.Add(new ArcSegment{Point=new(15+radius*Math.Sin(angle),15-radius*Math.Cos(angle)),
                    Size=new(radius,radius),IsLargeArc=remaining>50,SweepDirection=SweepDirection.Clockwise});
                var geometry=new PathGeometry();geometry.Figures.Add(figure);
                Children.Add(new Microsoft.UI.Xaml.Shapes.Path{Data=geometry,Stroke=stroke,StrokeThickness=1.6});
            }
        }
        var value=new TextBlock{Text=quota.WeeklyText,FontSize=quota.WeeklyText.Length>2?8:10,
            FontFamily=new FontFamily("ms-appx:///Assets/Fonts/MedeoSans.ttf#Medeo Sans"),
            FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,TextAlignment=TextAlignment.Center,
            VerticalAlignment=VerticalAlignment.Center,Foreground=Brush(light?"20262C":"F2F4F5")};
        var valueBox=new Border{Width=18,Height=18,Child=value};
        SetLeft(valueBox,6);SetTop(valueBox,6);Children.Add(valueBox);
        AutomationProperties.SetName(this,Description(quota));
    }
}
