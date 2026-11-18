using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using SideWidget.Core;
namespace SideWidget.App;

public sealed partial class MainWindow
{
    readonly Border timerTint=new(){Background=Brush("B6C3D0"),CornerRadius=new CornerRadius(10),Height=38,VerticalAlignment=VerticalAlignment.Center,Opacity=0,IsHitTestVisible=false};
    readonly Storyboard timerGlow=new();
    readonly Storyboard timerFade=new();
    Grid? timerHeader;
    bool timerRinging,timerReduced;
    void InitializeTimerHeader(Grid header)
    {
        timerHeader=header;Grid.SetColumnSpan(timerTint,3);header.Children.Insert(0,timerTint);
        var breathe=new DoubleAnimationUsingKeyFrames{Duration=new Duration(TimeSpan.FromSeconds(3)),RepeatBehavior=RepeatBehavior.Forever};
        breathe.KeyFrames.Add(new LinearDoubleKeyFrame{KeyTime=KeyTime.FromTimeSpan(TimeSpan.Zero),Value=0});
        breathe.KeyFrames.Add(new EasingDoubleKeyFrame{KeyTime=KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200)),Value=.03,EasingFunction=new ExponentialEase{Exponent=4,EasingMode=EasingMode.EaseOut}});
        breathe.KeyFrames.Add(new EasingDoubleKeyFrame{KeyTime=KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.5)),Value=.16,EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}});
        breathe.KeyFrames.Add(new EasingDoubleKeyFrame{KeyTime=KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3)),Value=0,EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}});
        Storyboard.SetTarget(breathe,timerTint);Storyboard.SetTargetProperty(breathe,"Opacity");timerGlow.Children.Add(breathe);
    }
    void UpdateTimerHeader()
    {
        controller.TickTimer();
        var timer=FocusTimerPresentation.Create(controller.Timer.State,controller.Timer.Remaining(DateTimeOffset.Now));
        if(timer==null&&controller.PowerSchedule.State is {} power)
            timer=new(DesktopLanguage.Text(power.Action=="restart"?"До перезагрузки ПК":"До выключения ПК"),
                FocusTimer.Format(controller.PowerSchedule.Remaining(DateTimeOffset.Now)),"","running",0);
        var ringing=timer?.Ringing==true;var reduced=ReducedMotion();
        var fontSize=controller.Settings.ClockFontSize;
        if(ringing!=timerRinging||reduced!=timerReduced){
            var opacity=timerTint.Opacity;timerGlow.Stop();timerFade.Stop();timerFade.Children.Clear();
            timerTint.Opacity=ringing?.12:0;
            if(ringing&&!reduced)timerGlow.Begin();
            else if(!ringing&&!reduced&&opacity>0){
                var fade=new DoubleAnimation{From=opacity,To=0,Duration=new Duration(TimeSpan.FromMilliseconds(200))};
                Storyboard.SetTarget(fade,timerTint);Storyboard.SetTargetProperty(fade,"Opacity");timerFade.Children.Add(fade);timerFade.Begin();
            }
            timerRinging=ringing;timerReduced=reduced;
        }
        if(clockOnlyHeader||timer==null)
        {
            clock.FontSize=fontSize;
            if(timerHeader!=null)Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(timerHeader,
                ringing?timer!.AccessibleName:"SideWidget, "+clock.Text);
            return;
        }
        day.Text=timer.Caption;
        clock.FontSize=ringing?fontSize*18d/22:fontSize;
        clock.Text=timer.Value;
        if(ringing)date.Text=timer.Action;
        if(timerHeader!=null)Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(timerHeader,timer.AccessibleName);
    }
    bool DismissTimerSignal()
    {
        if(controller.Timer.State.Status!="ringing")return false;
        try{controller.ChangeTimer(timer=>timer.Dismiss());outsideMs=0;UpdateClock();}
        catch(Exception e) when(e is Microsoft.Data.Sqlite.SqliteException or IOException){SendEvent("notice",new{message=DesktopLanguage.Text("Не удалось остановить сигнал. Проверьте свободное место и повторите.")});}
        return true;
    }
}

