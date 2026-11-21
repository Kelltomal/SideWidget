using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SideWidget.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
namespace SideWidget.App;
public sealed partial class MainWindow
{
    async Task LaunchNewBuild()
    {
        if(closing||dialogCount>0)return;
        dialogCount++;motion.Editing=true;
        string? error=null;
        try
        {
            var next=await Task.Run(()=>BuildSwitch.FindNewer(Environment.ProcessPath!));
            if(closing)return;
            if(next is null)error="Более свежая полная сборка не найдена.";
            else{Program.NextBuild=next;desktop.Stop();}
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){error="Не удалось проверить папки сборок. Проверьте доступ к ним и повторите.";}
        finally{dialogCount--;motion.Editing=webEditing||dialogCount>0;}
        if(error is not null&&!closing)Native.MessageBox(hwnd,DesktopLanguage.Text(error),DesktopLanguage.Text("Запустить новую сборку"),0x30);
    }
    async Task<string?> PickApplicationFile()
    {
        var picker=new FileOpenPicker();WinRT.Interop.InitializeWithWindow.Initialize(picker,hwnd);foreach(var ext in new[]{".exe",".lnk",".png",".ico",".svg"})picker.FileTypeFilter.Add(ext);return (await picker.PickSingleFileAsync())?.Path;
    }
    async Task<string?> PickDesktopFolder()
    {
        await dialogs.WaitAsync();
        dialogCount++;
        motion.Editing=true;
        try
        {
            EnterInteractive();
            var picker=new FolderPicker{SuggestedStartLocation=PickerLocationId.Desktop,CommitButtonText=DesktopLanguage.Text("Выбрать папку")};
            WinRT.Interop.InitializeWithWindow.Initialize(picker,hwnd);
            picker.FileTypeFilter.Add("*");
            return (await picker.PickSingleFolderAsync())?.Path;
        }
        finally
        {
            dialogCount--;
            motion.Editing=webEditing||dialogCount>0;
            dialogs.Release();
        }
    }
    async Task<object> AddApplication()
    {
        dialogCount++;motion.Editing=true;EnterInteractive();try{var file=await PickApplicationFile();if(file!=null)await AddSelectedApplication(file);return controller.Snapshot();}finally{dialogCount--;motion.Editing=webEditing||dialogCount>0;}
    }
    async Task AddSelectedApplication(string path)
    {
        string? icon=null;var ext=Path.GetExtension(path).ToLowerInvariant();
        if(ext is ".png" or ".ico" or ".svg"){
            icon=await ReadApplicationIcon(path);
            if(!await Confirm("Выберите приложение","Изображение станет значком. Теперь выберите программу или ярлык, который нужно запускать."))return;
            var picker=new FileOpenPicker();WinRT.Interop.InitializeWithWindow.Initialize(picker,hwnd);picker.FileTypeFilter.Add(".exe");picker.FileTypeFilter.Add(".lnk");var target=await picker.PickSingleFileAsync();if(target==null)return;path=target.Path;
        }
        await controller.Launcher.Add(path,icon);controller.Emit("launcher.changed",new{});
    }
    static async Task<string> ReadApplicationIcon(string path)
    {
        var extension=Path.GetExtension(path).ToLowerInvariant();
        if(extension is not (".png" or ".ico" or ".svg"))throw new CommandFault("icon","Выберите значок PNG, ICO или SVG");
        await using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,8192,true);
        if(input.Length>150000)throw new CommandFault("icon","Значок должен быть меньше 150 КБ");
        var buffer=new byte[150001];var length=0;int read;
        while(length<buffer.Length&&(read=await input.ReadAsync(buffer.AsMemory(length)))>0)length+=read;
        if(length>150000)throw new CommandFault("icon","Значок должен быть меньше 150 КБ");
        if(length==0)throw new CommandFault("icon","Файл значка пуст");
        var bytes=buffer[..length];if(extension==".svg")ValidateSvg(Encoding.UTF8.GetString(bytes));
        return "data:"+(extension==".svg"?"image/svg+xml":extension==".ico"?"image/x-icon":"image/png")+";base64,"+Convert.ToBase64String(bytes);
    }
    async Task<object> ChangeApplicationIcon(string id)
    {
        dialogCount++;motion.Editing=true;EnterInteractive();
        try
        {
            var picker=new FileOpenPicker();WinRT.Interop.InitializeWithWindow.Initialize(picker,hwnd);
            foreach(var extension in new[]{".png",".ico",".svg"})picker.FileTypeFilter.Add(extension);
            var file=await picker.PickSingleFileAsync();
            if(file!=null)
            {
                controller.Launcher.SetIcon(id,await ReadApplicationIcon(file.Path));
                controller.Emit("launcher.changed",new{});
            }
            return controller.Snapshot();
        }
        finally{dialogCount--;motion.Editing=webEditing||dialogCount>0;}
    }
    void AppDragOver(object sender,DragEventArgs e){if(e.DataView.Contains(StandardDataFormats.StorageItems)){e.AcceptedOperation=DataPackageOperation.Copy;e.Handled=true;}}
    async void AppDrop(object sender,DragEventArgs e)
    {
        var deferral=e.GetDeferral();dialogCount++;motion.Editing=true;
        try{if(e.DataView.Contains(StandardDataFormats.StorageItems)){var items=await e.DataView.GetStorageItemsAsync();foreach(var item in items.Take(10))if(item is StorageFile file)await AddSelectedApplication(file.Path);}}catch(Exception ex){SendEvent("notice",new{message=DesktopLanguage.Text(ex is CommandFault?ex.Message:"Не удалось закрепить файл")});}finally{dialogCount--;motion.Editing=webEditing||dialogCount>0;deferral.Complete();}
    }
    static void CopyText(string text){var package=new DataPackage();package.SetText(text);Clipboard.SetContent(package);Clipboard.Flush();}
    static byte[] ImageBytes(string data){var comma=data.IndexOf(',');if(comma<0||!(data.StartsWith("data:image/png;base64,")||data.StartsWith("data:image/jpeg;base64,")))throw new CommandFault("image","Ожидалось изображение PNG или JPEG");try{return Convert.FromBase64String(data[(comma+1)..]);}catch(FormatException){throw new CommandFault("image","Повреждённое изображение");}}
    static async Task CopyImage(string data)
    {
        var bytes=ImageBytes(data);using var stream=new InMemoryRandomAccessStream();using(var writer=new DataWriter(stream.GetOutputStreamAt(0))){writer.WriteBytes(bytes);await writer.StoreAsync();await writer.FlushAsync();writer.DetachStream();}stream.Seek(0);var package=new DataPackage();package.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));Clipboard.SetContent(package);Clipboard.Flush();
    }
    static void ValidateSvg(string svg)
    {
        using var reader=XmlReader.Create(new StringReader(svg),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1500000});var document=XDocument.Load(reader);
        if(document.Root?.Name.LocalName!="svg"||document.Descendants().Any(e=>!new[]{"svg","g","rect","path"}.Contains(e.Name.LocalName)||e.Attributes().Any(a=>!new[]{"xmlns","width","height","viewBox","fill","stroke","stroke-width","d","x","y","shape-rendering","version"}.Contains(a.Name.LocalName))))throw new CommandFault("svg","SVG содержит неподдерживаемые элементы");
        if(svg.Contains("url(",StringComparison.OrdinalIgnoreCase))throw new CommandFault("svg","Внешние ресурсы SVG не разрешены");
    }
    async Task<object> SaveQr(string format,string data)
    {
        byte[] bytes;if(format=="svg"){ValidateSvg(data);bytes=Encoding.UTF8.GetBytes(data);}else if(format is "png" or "jpeg")bytes=ImageBytes(data);else throw new CommandFault("format","Выберите SVG, PNG или JPEG");
        dialogCount++;motion.Editing=true;EnterInteractive();try{var picker=new FileSavePicker{SuggestedFileName="SideWidget-QR"};WinRT.Interop.InitializeWithWindow.Initialize(picker,hwnd);picker.FileTypeChoices.Add(format.ToUpperInvariant(),new List<string>{format=="jpeg"?".jpg":"."+format});var file=await picker.PickSaveFileAsync();if(file==null)return new{saved=false};await FileIO.WriteBytesAsync(file,bytes);return new{saved=true};}finally{dialogCount--;motion.Editing=webEditing||dialogCount>0;}
    }
    async Task<object> ConfigureTranslation(bool soniox)
    {
        await dialogs.WaitAsync();dialogCount++;motion.Editing=true;OpenInteractive(true);
        try{
            var font=new Microsoft.UI.Xaml.Media.FontFamily("ms-appx:///Assets/Fonts/MedeoSans.ttf#Medeo Sans");
            var key=new PasswordBox{Header=DesktopLanguage.Text(soniox?"Ключ Soniox API":"Ключ DeepL API"),FontFamily=font,MaxLength=200};
            var plan=new ComboBox{Header=DesktopLanguage.Text("Тариф API"),FontFamily=font,ItemsSource=new[]{"Free","Pro"},SelectedIndex=0};
            var stack=new StackPanel{Spacing=12};
            stack.Children.Add(new TextBlock{Text=DesktopLanguage.Text(soniox?"Создайте ключ в Soniox Console с доступом к распознаванию речи и озвучиванию. Windows DPAPI защищает сохранённый ключ.":"Текст отправляется в DeepL только по кнопке «Перевести». Ключ хранится в Windows DPAPI."),FontFamily=font,TextWrapping=TextWrapping.Wrap});
            stack.Children.Add(key);
            if(!soniox)stack.Children.Add(plan);
            var dialog=new ContentDialog{XamlRoot=root.XamlRoot,Title=DesktopLanguage.Text(soniox?"Перевод Soniox":"Перевод DeepL"),FontFamily=font,Content=stack,PrimaryButtonText=DesktopLanguage.Text("Сохранить"),CloseButtonText=DesktopLanguage.Text("Отмена"),IsPrimaryButtonEnabled=false,DefaultButton=ContentDialogButton.Close,RequestedTheme=ElementTheme.Dark};
            key.PasswordChanged+=(_,_)=>dialog.IsPrimaryButtonEnabled=!string.IsNullOrWhiteSpace(key.Password);
            if(await ShowNativeDialog(dialog)==ContentDialogResult.Primary)
            {
                if(soniox)controller.Online.ConfigureSoniox(key.Password);
                else controller.Online.ConfigureTranslation(key.Password,plan.SelectedIndex==0);
            }
            return controller.Online.TranslationInfo();
        }finally{dialogCount--;motion.Editing=webEditing||dialogCount>0;dialogs.Release();}
    }
}
