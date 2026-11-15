using SideWidget.Core;
namespace SideWidget.App;
public sealed partial class MainWindow
{
    async Task<object> PickScreenColor()
    {
        dialogCount++;motion.Editing=true;try{var picked=await ScreenPicker.PickAsync(hwnd,true);return new{hex=picked.Hex};}catch(OperationCanceledException){throw new CommandFault("cancelled","Выбор цвета отменён");}catch(InvalidOperationException e){throw new CommandFault("picker",e.Message);}finally{dialogCount--;motion.Editing=webEditing||dialogCount>0;}
    }
    async Task<object> PickApplicationWindow()
    {
        dialogCount++;motion.Editing=true;try{var picked=await ScreenPicker.PickAsync(hwnd,false);var name=picked.Name??"приложение";if(await Confirm("Избранные приложения",$"Изменить закрепление «{name}»? Уже закреплённое приложение будет убрано. Изменение можно отменить в разделе «Приложения».")){if(picked.AppUserModelId!=null)controller.Launcher.TogglePackagedWithUndo(picked.AppUserModelId,name);else if(picked.ExecutablePath!=null)await controller.Launcher.ToggleWithUndo(picked.ExecutablePath);controller.Emit("launcher.changed",new{});}return controller.Snapshot();}catch(OperationCanceledException){throw new CommandFault("cancelled","Выбор окна отменён");}catch(InvalidOperationException e){throw new CommandFault("picker",e.Message);}finally{dialogCount--;motion.Editing=webEditing||dialogCount>0;}
    }
}
