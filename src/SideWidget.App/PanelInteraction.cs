using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using SideWidget.Core;
using System.Runtime.InteropServices;
namespace SideWidget.App;
public sealed partial class MainWindow
{
    UIElement? dragOwner;
    DesktopDisplay? dragDisplay;
    double? dragPosition;
    int dragPointerY,dragWindowY;
    Native.Point EventScreenPoint(PointerRoutedEventArgs e)
    {
        // Use this event's position: GetCursorPos can already be at mouse-up
        // when Windows delivers a queued batch of fast pointer events.
        var point=e.GetCurrentPoint(root).Position;
        return new Native.Point{X=hostX+(int)Math.Round(point.X*scale),Y=y+(int)Math.Round(point.Y*scale)};
    }
    void TracePointer(string phase,PointerRoutedEventArgs e)
    {
        if(!Environment.GetCommandLineArgs().Contains("--diagnostics"))return;
        Native.GetCursorPos(out var cursor);var point=e.GetCurrentPoint(root);
        File.AppendAllText(Path.Combine(controller.Store.DirectoryPath,"pointer.ndjson"),System.Text.Json.JsonSerializer.Serialize(new{phase,cursorX=cursor.X,cursorY=cursor.Y,eventX=point.Position.X,eventY=point.Position.Y,x,y,dragging,dragMoved})+"\n");
    }
    bool resizing;UIElement? resizeOwner;Preferences? resizeStart,resizePreview;Native.Point resizePointer;int resizeLeft;
    void ResizePressed(object sender,PointerRoutedEventArgs e)
    {
        if(dialogCount>0)return;
        if(!e.GetCurrentPoint((UIElement)sender).Properties.IsLeftButtonPressed)return;
        EnterInteractive();resizing=true;resizeStart=controller.Settings;resizeLeft=x;resizePointer=EventScreenPoint(e);resizeOwner=(UIElement)sender;resizeOwner.CapturePointer(e.Pointer);motion.Editing=true;e.Handled=true;
    }
    void ResizeMoved(object sender,PointerRoutedEventArgs e)
    {
        if(!resizing||resizeStart==null)return;var pointer=EventScreenPoint(e);var monitor=Monitor();
        var nextWidth=Math.Clamp(resizeStart.Width+(int)Math.Round((pointer.X-resizePointer.X)/scale/PanelGeometry.ExpandedWidthFactor),360,900);
        var nextHeight=Math.Clamp(resizeStart.Height+(int)Math.Round((pointer.Y-resizePointer.Y)/scale),360,1200);
        var position=PanelGeometry.PositionForLeft(monitor.Work.Left,monitor.Work.Right,(int)Math.Round(nextWidth*scale),resizeLeft,1);
        resizePreview=(controller.Settings with{Width=nextWidth,Height=nextHeight}).WithDisplayPosition(DisplayId,position);
        motion.Target(true,MaximumHeight(),true);Place();e.Handled=true;
    }
    void FinishResize(bool cancel)
    {
        if(!resizing||resizeStart==null)return;resizing=false;
        try{if(!cancel&&resizePreview is not null)controller.SavePlacement(resizePreview);}
        catch(Exception){SendEvent("notice",new{message=DesktopLanguage.Text("Не удалось сохранить размер панели")});}
        resizePreview=null;
        motion.Target(true,MaximumHeight(),true);Place();resizeOwner?.ReleasePointerCaptures();resizeOwner=null;resizeStart=null;motion.Editing=webEditing||dialogCount>0;
    }
    void HeaderPressed(object sender,PointerRoutedEventArgs e)
    {
        if(dialogCount>0){focusNativeDialog?.Invoke();e.Handled=true;return;}
        TracePointer("press",e);
        var buttons=e.GetCurrentPoint((UIElement)sender).Properties;
        if(!buttons.IsLeftButtonPressed)
        {
            if(!updateInProgress&&buttons.IsRightButtonPressed&&motion.State==PanelState.Collapsed)
            {
                var point=EventScreenPoint(e);
                if(compactPanel.Click(watch.ElapsedMilliseconds,point.X,point.Y,Native.GetSystemMetrics(36),Native.GetSystemMetrics(37),right:true))
                    ApplyGeometry();
                e.Handled=true;
            }
            return;
        }
        compactPanel.ResetClicks();
        if(!updateInProgress&&DismissTimerSignal()){e.Handled=true;return;}
        EnterInteractive();
        var pointer=EventScreenPoint(e);
        dragPointerX=pointer.X;dragWindowX=x;dragPointerY=pointer.Y;dragWindowY=y;
        headerGesture.Press(pointer.X,pointer.Y,watch.ElapsedMilliseconds);dragging=true;dragMoved=false;motion.Editing=true;dragOwner=(UIElement)sender;dragOwner.CapturePointer(e.Pointer);e.Handled=true;
    }
    void PanelPointerReleased(object sender,PointerRoutedEventArgs e)
    {
        if(dialogCount>0||motion.State==PanelState.Collapsed)return;
        var pointer=e.GetCurrentPoint(root);
        if(pointer.Properties.PointerUpdateKind!=Microsoft.UI.Input.PointerUpdateKind.RightButtonReleased)
        {
            compactPanel.ResetClicks();
            return;
        }
        var point=EventScreenPoint(e);
        if(compactPanel.Click(watch.ElapsedMilliseconds,point.X,point.Y,Native.GetSystemMetrics(36),Native.GetSystemMetrics(37),right:true,expanded:true))
        {
            e.Handled=true;
            DispatcherQueue.TryEnqueue(()=>{if(!closing&&motion.State!=PanelState.Collapsed)Collapse();});
        }
    }
    void HeaderMoved(object sender,PointerRoutedEventArgs e)
    {
        if(dragging)TracePointer("move",e);
        if(!dragging)return;
        Native.GetCursorPos(out var point);
        UpdateDrag(point);e.Handled=true;
    }
    void UpdateDrag(Native.Point point)
    {
        headerGesture.Move(point.X,point.Y);dragMoved=headerGesture.Moved;if(!dragMoved)return;
        dragDisplay=desktop.CanTransferDisplay?desktop.Display(MonitorFromPoint(point,2)):null;
        var work=Monitor().Work;
        var baseWidth=(int)Math.Round(controller.Settings.Width*scale);
        var proposed=dragWindowX+point.X-dragPointerX;
        if(!updateInProgress&&compactPanel.ClockOnly&&motion.State==PanelState.Collapsed)
            proposed-=(Math.Min(baseWidth,work.Right-work.Left)-Math.Min(baseWidth,(int)Math.Round(100*scale)))/2;
        if(updateInProgress)updateTop=dragWindowY+point.Y-dragPointerY;
        dragPosition=PanelGeometry.PositionForLeft(work.Left,work.Right,baseWidth,proposed,motion.Expansion);
        Place();
    }
    void HeaderReleased(object sender,PointerRoutedEventArgs e)
    {
        TracePointer("release",e);if(!dragging)return;
        var point=EventScreenPoint(e);
        if(dragMoved)Native.GetCursorPos(out point);
        UpdateDrag(point);
        var click=headerGesture.Release(point.X,point.Y,watch.ElapsedMilliseconds);
        FinishDrag(false);if(click)OpenInteractive();e.Handled=true;
    }
    void FinishDrag(bool cancel)
    {
        if(!dragging)return;dragging=false;headerGesture.Cancel();
        if(cancel&&updateInProgress)updateTop=dragWindowY;
        try
        {
            if(!cancel&&dragPosition is double position)
            {
                var target=dragDisplay?.Id??DisplayId;
                var settings=controller.Settings.WithDisplayPosition(target,position);
                if(target!=DisplayId)settings=settings with{DisplayIds=[target]};
                controller.SavePlacement(settings);
                DisplayId=target;
            }
        }
        catch(Exception){SendEvent("notice",new{message=DesktopLanguage.Text("Не удалось сохранить положение панели")});}
        dragPosition=null;dragDisplay=null;Place();
        dragOwner?.ReleasePointerCaptures();dragOwner=null;motion.Editing=webEditing||dialogCount>0;
        if(updateInProgress)RestoreFocus();
    }
    [DllImport("user32.dll")]static extern nint MonitorFromPoint(Native.Point point,uint flags);
}
