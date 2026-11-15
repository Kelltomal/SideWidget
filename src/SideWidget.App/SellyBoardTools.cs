using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace SideWidget.App;
public sealed partial class MainWindow
{
    async Task<object> ConfigureSellyBoard()
    {
        await dialogs.WaitAsync();dialogCount++;motion.Editing=true;OpenInteractive(true);
        try{
            var font=new Microsoft.UI.Xaml.Media.FontFamily("ms-appx:///Assets/Fonts/MedeoSans.ttf#Medeo Sans");
            var address=new TextBox{Header=DesktopLanguage.Text("Адрес SellyBoard"),Text=controller.Selly.Address,FontFamily=font,MaxLength=2048,PlaceholderText=DesktopLanguage.Text("https://ваш-сервер/")};
            var key=new PasswordBox{Header=DesktopLanguage.Text("Персональный API-ключ"),FontFamily=font,MaxLength=512};
            var stack=new StackPanel{Spacing=12};stack.Children.Add(new TextBlock{Text=DesktopLanguage.Text("Создайте ключ календаря в SellyBoard: Настройки · API. Он должен быть привязан к вашему пользователю. Ключ хранится в Windows DPAPI; в интерфейс и браузер не передаётся."),FontFamily=font,TextWrapping=TextWrapping.Wrap});stack.Children.Add(address);stack.Children.Add(key);
            var dialog=new ContentDialog{XamlRoot=root.XamlRoot,Title=DesktopLanguage.Text("Подключить SellyBoard"),FontFamily=font,Content=stack,PrimaryButtonText=DesktopLanguage.Text("Подключить"),CloseButtonText=DesktopLanguage.Text("Отмена"),IsPrimaryButtonEnabled=false,RequestedTheme=ElementTheme.Dark};
            void Validate()=>dialog.IsPrimaryButtonEnabled=!string.IsNullOrWhiteSpace(address.Text)&&!string.IsNullOrWhiteSpace(key.Password);
            key.PasswordChanged+=(_,_)=>Validate();address.TextChanged+=(_,_)=>Validate();
            if(await ShowNativeDialog(dialog)==ContentDialogResult.Primary){var secret=key.Password;key.Password="";await controller.Selly.Connect(address.Text,secret);}
            return controller.Selly.Info();
        }finally{dialogCount--;motion.Editing=webEditing||dialogCount>0;dialogs.Release();}
    }
}
