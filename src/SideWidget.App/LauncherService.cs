using System.Diagnostics;
using System.Runtime.InteropServices;
using SideWidget.Core;
using SideWidget.Infrastructure;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
namespace SideWidget.App;
public sealed class LauncherService(PersonalState state)
{
    List<PinnedApp>? undo;
    public bool CanUndo=>undo!=null&&!undo.SequenceEqual(state.Apps);
    public void CaptureUndo()=>undo=state.Apps.ToList();
    public void Undo(){if(!CanUndo)return;state.SaveApps(undo!);undo=null;}
    public Task Toggle(string path)=>ToggleCore(path,false);
    public Task ToggleWithUndo(string path)=>ToggleCore(path,true);
    async Task ToggleCore(string path,bool recordUndo)
    {
        path=Path.GetFullPath(path);
        var app=state.Apps.FirstOrDefault(item=>item.Path.Equals(path,StringComparison.OrdinalIgnoreCase));
        if(app==null)
        {
            var identity=ExecutableIdentity(path);
            if(identity!=null)app=state.Apps.FirstOrDefault(item=>!item.Packaged&&string.Equals(AppIdentity(item),identity,StringComparison.OrdinalIgnoreCase));
        }
        if(app!=null)Commit(state.Apps.Where(item=>item.Id!=app.Id),recordUndo);
        else await AddCore(path,null,recordUndo);
    }
    public void TogglePackaged(string id,string name)=>TogglePackagedCore(id,name,false);
    public void TogglePackagedWithUndo(string id,string name)=>TogglePackagedCore(id,name,true);
    void TogglePackagedCore(string id,string name,bool recordUndo)
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(id,@"^[\w.!-]{1,256}$"))throw new CommandFault("application","Не удалось определить идентификатор приложения");
        var path="aumid:"+id;var app=state.Apps.FirstOrDefault(item=>item.Path==path);
        if(app!=null)Commit(state.Apps.Where(item=>item.Id!=app.Id),recordUndo);
        else
        {
            if(state.Apps.Count>=40)throw new CommandFault("limit","Можно закрепить до 40 приложений");
            Commit(state.Apps.Append(new(Guid.NewGuid().ToString("N"),name,path,null,true)),recordUndo);
        }
    }
    void Commit(IEnumerable<PinnedApp> candidate,bool recordUndo)
    {
        var before=recordUndo?state.Apps.ToList():null;
        state.SaveApps(candidate);
        if(before!=null&&!before.SequenceEqual(state.Apps))undo=before;
    }
    public Task Add(string path,string? customIcon=null)=>AddCore(path,customIcon,false);
    async Task AddCore(string path,string? customIcon,bool recordUndo)
    {
        path=Path.GetFullPath(path);if(path.StartsWith("\\\\",StringComparison.Ordinal)||!File.Exists(path))throw new CommandFault("application","Выберите существующий локальный файл");
        var extension=Path.GetExtension(path).ToLowerInvariant();if(extension is not (".exe" or ".lnk"))throw new CommandFault("application","Нужна программа .exe или ярлык .lnk");
        var identity=extension==".lnk"?ValidateShortcut(path):ExecutableIdentity(path);
        if(state.Apps.Any(a=>a.Path.Equals(path,StringComparison.OrdinalIgnoreCase)))return;
        if(state.Apps.Count>=40)throw new CommandFault("limit","Можно закрепить до 40 приложений");
        string? icon=customIcon;
        if(icon==null)try{var file=await StorageFile.GetFileFromPathAsync(path);using var thumbnail=await file.GetThumbnailAsync(ThumbnailMode.SingleItem,64);if(thumbnail!=null&&thumbnail.Size>0&&thumbnail.Size<150000){using var reader=new DataReader(thumbnail.GetInputStreamAt(0));await reader.LoadAsync((uint)thumbnail.Size);var bytes=new byte[(int)thumbnail.Size];reader.ReadBytes(bytes);var candidate="data:"+thumbnail.ContentType+";base64,"+Convert.ToBase64String(bytes);if(System.Text.Json.JsonSerializer.Serialize(candidate,Protocol.Json).Length<=220*1024)icon=candidate;}}catch(Exception){/* Icon failure never chooses a different executable. */}
        // Thumbnail extraction yields: another add/remove may have completed in the meantime.
        if(state.Apps.Any(a=>a.Path.Equals(path,StringComparison.OrdinalIgnoreCase)))return;
        if(state.Apps.Count>=40)throw new CommandFault("limit","Можно закрепить до 40 приложений");
        Commit(state.Apps.Append(new(Guid.NewGuid().ToString("N"),Path.GetFileNameWithoutExtension(path),path,icon,Identity:identity)),recordUndo);
    }
    static string? ExecutableIdentity(string path)=>Path.IsPathFullyQualified(path)&&!path.StartsWith("\\\\",StringComparison.Ordinal)&&Path.GetExtension(path).Equals(".exe",StringComparison.OrdinalIgnoreCase)?Path.GetFullPath(path):null;
    static string? AppIdentity(PinnedApp app)
    {
        if(app.Identity!=null)return app.Identity;
        if(Path.GetExtension(app.Path).Equals(".lnk",StringComparison.OrdinalIgnoreCase)&&File.Exists(app.Path))
        {
            try{return ValidateShortcut(app.Path);}
            catch(Exception){return null;} // An unresolved legacy shortcut is not proof of identity.
        }
        return ExecutableIdentity(app.Path);
    }
    static string? ValidateShortcut(string path)
    {
        var target = ReadShortcutTarget(path);
        // Packaged shortcuts may have no filesystem target; ShellExecute handles their registered identity.
        if(target!=""&&(!File.Exists(target)&&!Directory.Exists(target)))throw new CommandFault("shortcut","Цель ярлыка не найдена");
        return ExecutableIdentity(target);
    }

    internal static string ReadShortcutTarget(string path)
    {
        object? shell=null;object? shortcut=null;
        try{var type=Type.GetTypeFromProgID("WScript.Shell")??throw new CommandFault("shortcut","Не удалось прочитать ярлык");shell=Activator.CreateInstance(type);dynamic link=((dynamic)shell!).CreateShortcut(path);shortcut=link;
            return link.TargetPath;
        }finally{if(shortcut!=null)Marshal.FinalReleaseComObject(shortcut);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
    }
    public void Launch(string id){var app=state.Apps.FirstOrDefault(a=>a.Id==id)??throw new CommandFault("application","Приложение не найдено");if(app.Packaged){var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe")){UseShellExecute=true};start.ArgumentList.Add("shell:AppsFolder\\"+app.Path[6..]);Process.Start(start);return;}if(!File.Exists(app.Path))throw new CommandFault("application","Файл перемещён или удалён. Закрепите его заново.");Process.Start(new ProcessStartInfo(app.Path){UseShellExecute=true});}
    public void Remove(string id)=>state.SaveApps(state.Apps.Where(app=>app.Id!=id));
    public void SetIcon(string id,string icon)
    {
        if(!state.Apps.Any(app=>app.Id==id))throw new CommandFault("application","Приложение не найдено");
        state.SaveApps(state.Apps.Select(app=>app.Id==id?app with{Icon=icon}:app));
    }
    public void Rename(string id,string name){if(string.IsNullOrWhiteSpace(name)||name.Length>80)throw new CommandFault("application","Введите название до 80 символов");if(!state.Apps.Any(app=>app.Id==id))throw new CommandFault("application","Приложение не найдено");state.SaveApps(state.Apps.Select(app=>app.Id==id?app with{Name=name.Trim()}:app));}
    public void Reorder(string[] ids){if(ids.Length!=state.Apps.Count||ids.Distinct().Count()!=ids.Length||ids.Any(id=>!state.Apps.Any(a=>a.Id==id)))throw new CommandFault("application","Некорректный порядок");state.SaveApps(ids.Select(id=>state.Apps.Single(a=>a.Id==id)));}
    public static void OpenSearch(string path,bool reveal){if(!File.Exists(path)&&!Directory.Exists(path))throw new CommandFault("search","Файл больше не существует");if(path.StartsWith("\\\\"))throw new CommandFault("search","Сетевые пути открывайте в Проводнике самостоятельно");if(reveal){var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe")){UseShellExecute=true};start.Arguments="/select,\""+path.Replace("\"","")+"\"";Process.Start(start);}else Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}
}

