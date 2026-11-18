using System.Diagnostics;
using System.Text.Json;
using SideWidget.Core;
using SideWidget.Infrastructure;
namespace SideWidget.App;
sealed partial class Controller
{
    static readonly Version currentAppVersion=typeof(Controller).Assembly.GetName().Version??new Version(0,0,0);
    static readonly string appVersion=currentAppVersion.ToString(3);
    static readonly GitHubUpdates githubUpdates=new();
    readonly PersonalState state;
    readonly DesktopState desktopState;
    readonly bool ownsDesktopState;
    readonly MediaService media;readonly AudioService audio;readonly EverythingService search=new();readonly SystemMetrics metrics=new();readonly InternetService internet=new();
    public AudioSpectrumService Spectrum=>desktopState.Spectrum;
    public Action VisualizerChanged {get;set;}=()=>{};
    readonly HashSet<string> allowedSearch=new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> seen=new();readonly Queue<string> recent=new();int running;
    public Store Store=>state.Store;
    public Preferences Settings {get=>state.Settings;set=>state.Settings=value;}
    public LauncherService Launcher {get;}
    public OnlineServices Online {get;}
    int sharing;
    public bool Ready {get;set;}
    public Func<string,string,Task<bool>> Confirm {get;set;}=(_,_)=>Task.FromResult(false);
    public Func<Task<object>> AddApp {get;set;}=()=>Task.FromResult<object>(new{});
    public Func<string,Task<object>> ChangeAppIcon {get;set;}=_=>Task.FromResult<object>(new{});
    public Func<Task<object>> PickWindow {get;set;}=()=>Task.FromResult<object>(new{});
    public Func<Task<string?>> PickFolder {get;set;}=()=>Task.FromResult<string?>(null);
    public Func<Task<object>> PickColor {get;set;}=()=>Task.FromResult<object>(new{});
    public Func<WebTranslationRequest,Task<object>> TranslateWebsite {get;set;}=_=>throw new CommandFault("unsupported","Переводчик недоступен");
    public Action CancelWebsiteTranslation {get;set;}=()=>{};
    public Func<Task<object>> ConfigureTranslation {get;set;}=()=>Task.FromResult<object>(new{});
    public Func<string,string,Task<object>> SaveQr {get;set;}=(_,_)=>Task.FromResult<object>(new{});
    public Func<string,Task<object>> PrintQr {get;set;}=_=>throw new CommandFault("print","Печать недоступна");
    public Action<string> CopyText {get;set;}=_=>{};
    public Func<string,Task> CopyImage {get;set;}=_=>Task.CompletedTask;
    public Action<Preferences> SettingsChanged {get;set;}=_=>{};
    public Action<string,object> Emit {get;set;}=(_,_)=>{};
    public Action<string> Shell {get;set;}=_=>{};
    public Action ShowFullscreenClock {get;set;}=()=>{};
    public Action<TooltipContent?> Tooltip {get;set;}=_=>{};
    public Func<IReadOnlyList<DisplayDescriptor>> Displays {get;set;}=()=>[];
    public Controller(string directory):this(new DesktopState(directory)){ownsDesktopState=true;audio.LevelChanged+=(volume,muted)=>Emit("audio.level.changed",new{volume,muted});}
    internal Controller(DesktopState shared){desktopState=shared;state=shared.Personal;Timer=shared.Timer;Launcher=shared.Launcher;Online=shared.Online;audio=shared.Audio;media=shared.Media;}
    public object Snapshot()=>new {appVersion,language=DesktopLanguage.Language,systemLanguage=DesktopLanguage.SystemLanguage,settings=Settings,displays=Displays(),calculatorHistory=state.CalculatorHistory,colorHistory=state.ColorHistory,unitFavorites=state.UnitFavorites,apps=state.Apps,launcherUndoAvailable=Launcher.CanUndo,drafts=new{diary=state.DiaryDraft}};
    public void SavePlacement(Preferences p){state.SaveSettings(p);Emit("settings.changed",new{settings=p});}
    public async Task<object> Handle(Command c)
    {
        Protocol.ValidateParams(c);if(!seen.Add(c.RequestId))throw new CommandFault("duplicate","Повторное действие отклонено");recent.Enqueue(c.RequestId);if(recent.Count>2048)seen.Remove(recent.Dequeue());
        if(running>=32)throw new CommandFault("busy","Дождитесь завершения текущих действий");running++;
        var p=c.Params;string Text(string k,int max=10000)=>Protocol.Text(p,k,max);
        try {
            switch(c.ActionId){
                case "bridge.ready":Ready=true;return Snapshot();case "state.get":return Snapshot();
                case "startup.get":return LiteInstallation.Startup().Get();
                case "app.share":return await ShareApplication();
                case "updates.check":return await CheckUpdate();
                case "updates.defer":return new{};
                case "updates.skip":SkipUpdate(Text("version",32));return new{};
                case "updates.install":await InstallUpdate(Text("version",32));return new{};
                case "startup.set":
                    if(!p.TryGetProperty("enabled",out var startupEnabled)||startupEnabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False))throw new CommandFault("schema","Укажите состояние автозапуска");
                    return LiteInstallation.Startup().Set(startupEnabled.GetBoolean());
                case "shell.pin":Shell("pin");return new{};case "shell.collapse":Shell("collapse");return new{};case "shell.interactive":Shell("interactive");return new{};
                case "shell.editing":Shell(p.GetProperty("value").GetBoolean()?"editing":"idle");return new{};
                case "shell.tooltip":Tooltip(p.GetProperty("visible").GetBoolean()?TooltipContent.Read(p):null);return new{};
                case "settings.theme":var themed=Settings with{Theme=Text("theme",16)};themed.Validate();state.SaveSettings(themed);ThemeChanged(themed.Theme);return Snapshot();
                case "settings.clock":
                    var clockSettings=Settings with
                    {
                        ClockFontSize=p.TryGetProperty("clockFontSize",out var fontSize)?fontSize.GetInt32():Settings.ClockFontSize,
                        CompactStripHeight=p.TryGetProperty("compactStripHeight",out var stripHeight)?stripHeight.GetInt32():Settings.CompactStripHeight
                    };
                    clockSettings.ValidateClock();
                    if(clockSettings!=Settings)state.SaveSettings(clockSettings);
                    return Snapshot();
                case "clock.fullscreen":
                    ShowFullscreenClock();
                    return new{};
                case "settings.language":
                    var language=Text("language",128);
                    if(language!="system"&&!DesktopLanguage.IsSupported(language))throw new CommandFault("settings","Выберите язык приложения.");
                    state.SaveSettings(Settings with{Language=language});
                    DesktopLanguage.Select(language);
                    return Snapshot();
                case "settings.save":
                    var settings=p.Deserialize<Preferences>(Protocol.Json)??throw new CommandFault("settings","Некорректные настройки");
                    var previousSettings=Settings;
                    settings.Validate();
                    var positionChanges=settings.DisplayPositions;
                    settings=settings with{DisplayPositions=previousSettings.DisplayPositions,Language=previousSettings.Language,
                        ClockFontSize=previousSettings.ClockFontSize,CompactStripHeight=previousSettings.CompactStripHeight};
                    if(positionChanges is not null)
                        foreach(var entry in positionChanges)settings=settings.WithDisplayPosition(entry.Key,entry.Value);
                    SettingsChanged(settings);
                    try{state.SaveSettings(settings);}catch{SettingsChanged(previousSettings);throw;}
                    return Snapshot();
                case "history.add":state.AddCalculation(Text("expression",500),Text("result",200));return Snapshot();
                case "history.delete":state.CalculatorHistory.RemoveAll(x=>x.Id==Text("id",64));state.SaveCalculations();return Snapshot();
                case "history.clear":if(await Confirm("Очистить историю?","Все сохранённые вычисления будут удалены. Остальные данные останутся.")){state.CalculatorHistory.Clear();state.SaveCalculations();}return Snapshot();
                case "color.remember":state.RememberColor(Text("hex",7));return Snapshot();
                case "units.favorite":state.FavoriteUnit(Text("from",32),Text("to",32));return Snapshot();
                case "diary.draft":state.SaveDraft(Text("text",80000));return Snapshot();
                case "clipboard.text":CopyText(Text("text",100000));return new{copied=true};
                case "links.open":var url=WebLink.Validate(Text("url",2048));Process.Start(new ProcessStartInfo(url.AbsoluteUri){UseShellExecute=true});return new{opened=true};
                case "clipboard.image":await CopyImage(Text("dataUrl",1500000));return new{copied=true};
                case "qr.save":return await SaveQr(Text("format",4),Text("data",1500000));
                case "qr.print":return await PrintQr(Text("html",1500000));
                case "desktop.preview":
                    var cleanupPrompt=p.TryGetProperty("prompt",out _)?Text("prompt",DesktopCleanup.PromptLimit):"";
                    return await Task.Run(()=>desktopState.Cleanup.Preview(cleanupPrompt));
                case "desktop.undo":return await Task.Run(desktopState.Cleanup.Undo);
                case "desktop.icons.undo":return await Task.Run(desktopState.Cleanup.UndoFolderIcons);
                case "desktop.documents":return await Task.Run(desktopState.Cleanup.PreviewDocuments);
                case "desktop.icon":
                    if(!Guid.TryParseExact(Text("previewId",36),"D",out var iconPreviewId))
                        throw new CommandFault("schema","Примените запрос ещё раз.");
                    var folderPath=await desktopState.Cleanup.ChangeFolderIcon(iconPreviewId,PickFolder,
                        (folder,icon)=>Confirm("Изменить значок папки?",DesktopLanguage.Text("{0}\n\nНовый значок — «{1}».",folder,DesktopLanguage.Text(icon))));
                    return new{path=folderPath,canUndoIcons=desktopState.Cleanup.CanUndoFolderIcons};
                case "desktop.icons.preview":
                    if(!Guid.TryParseExact(Text("previewId",36),"D",out var iconRequestId))
                        throw new CommandFault("schema","Примените запрос ещё раз.");
                    return await Task.Run(()=>desktopState.Cleanup.PreviewDesktopFolderIcons(iconRequestId));
                case "desktop.icons.apply":
                    if(!Guid.TryParseExact(Text("previewId",36),"D",out var folderPreviewId))
                        throw new CommandFault("schema","Откройте список папок заново.");
                    var selectedFolders=ReadDesktopSelection(p,"Проверьте выбор папок.");
                    var styles=p.TryGetProperty("styles",out var styleValues)?ReadFolderIconStyles(styleValues):null;
                    var icons=p.TryGetProperty("icons",out var iconValues)?ReadFolderIconChoices(iconValues):null;
                    return await Task.Run(()=>desktopState.Cleanup.ApplyDesktopFolderIcons(folderPreviewId,selectedFolders,styles,icons));
                case "desktop.open":
                    Process.Start(new ProcessStartInfo(desktopState.Cleanup.ExistingFolder()){UseShellExecute=true});
                    return new{opened=true};
                case "desktop.move":
                    if(!Guid.TryParseExact(Text("previewId",36),"D",out var previewId))
                        throw new CommandFault("schema","Проверьте выбор файлов.");
                    var selected=ReadDesktopSelection(p,"Проверьте выбор файлов.");
                    Dictionary<int,string>? topics=null;
                    if(p.TryGetProperty("topics",out var topicValues))
                    {
                        if(topicValues.ValueKind!=JsonValueKind.Object || topicValues.EnumerateObject().Count()>200 ||
                            topicValues.EnumerateObject().Any(item=>item.Value.ValueKind!=JsonValueKind.String || item.Value.GetString()!.Length>64))
                            throw new CommandFault("schema","Проверьте темы документов.");
                        topics=topicValues.Deserialize<Dictionary<int,string>>(Protocol.Json);
                    }
                    return await Task.Run(()=>desktopState.Cleanup.Move(previewId,selected,topics));
                case "apps.undo":Launcher.Undo();return Snapshot();case "apps.add":return await AddApp();case "apps.pickWindow":return await PickWindow();case "color.pick":return await PickColor();
                case "apps.launch":Launcher.Launch(Text("id",64));return new{launched=true};
                case "apps.icon":return await ChangeAppIcon(Text("id",64));
                case "apps.remove":Launcher.Remove(Text("id",64));return Snapshot();
                case "apps.rename":Launcher.Rename(Text("id",64),Text("name",80));return Snapshot();
                case "apps.reorder":Launcher.Reorder(p.GetProperty("ids").Deserialize<string[]>(Protocol.Json)??[]);return Snapshot();
                case "media.get":return await media.Get();
                case "remote.get":case "remote.configure":case "remote.setup":case "remote.invite":case "remote.decide":case "remote.revoke":return await PhoneRemoteCommand(c);
                case "media.command":return await media.Command(Text("id",256),Text("command",20),p.TryGetProperty("positionSeconds",out var pos)?pos.GetDouble():null);
                case "visualizer.get":var frame=Spectrum.Latest;return new{frame.Available,frame.Message,frame.Bands,frame.Rms,frame.Sequence,settings=Settings.Visualizer??new VisualizerOptions()};
                case "visualizer.settings":var visualizer=p.Deserialize<VisualizerOptions>(Protocol.Json)??throw new CommandFault("visualizer","Некорректные настройки визуализации");visualizer.Validate();state.SaveSettings(Settings with{Visualizer=visualizer});Spectrum.SetEnabled(visualizer.Enabled);VisualizerChanged();Emit("settings.changed",new{settings=Settings});return Snapshot();
                case "audio.get":return await Task.Run(audio.Get);
                case "audio.set":return await Task.Run(()=>audio.Set(p.TryGetProperty("volume",out var vol)?vol.GetDouble():null,p.TryGetProperty("muted",out var mute)?mute.GetBoolean():null,p.TryGetProperty("microphoneMuted",out var mic)?mic.GetBoolean():null,p.TryGetProperty("sessionId",out var session)?session.GetString():null));
                case "system.get":return await Task.Run(metrics.Read);
                case "internet.get":return await Task.Run(internet.Get);
                case "internet.check":return await internet.Check(Text("method",5),Text("target",253));
                case "search.query":var found=await search.Query(Text("text",500));var data=JsonSerializer.SerializeToElement(found,Protocol.Json);if(data.TryGetProperty("results",out var rows)){if(allowedSearch.Count>950)allowedSearch.Clear();foreach(var row in rows.EnumerateArray())allowedSearch.Add(row.GetProperty("path").GetString()!);}return found;
                case "search.open":var path=Text("path",32768);if(!allowedSearch.Contains(path))throw new CommandFault("search","Обновите поиск перед открытием");LauncherService.OpenSearch(path,p.TryGetProperty("reveal",out var reveal)&&reveal.GetBoolean());return new{opened=true};
                case "quick.open":QuickActions.Open(Text("target",30));return new{opened=true};
                case "power.get" or "power.schedule" or "power.cancel":return (await HandlePowerSchedule(c))!;
                case "quick.power":var action=Text("action",20);if(await Confirm("Выполнить системное действие?",QuickActions.Description(action))){QuickActions.Power(action);return new{executed=true};}return new{executed=false};
                case "currency.get":return await Online.Currency();case "weather.get":return await Online.Weather(Text("city",120));
                case "translation.get":return Online.TranslationInfo();case "translation.configure":return await ConfigureTranslation();
                case "translation.run":
                    var provider=p.TryGetProperty("provider",out _)?Text("provider",20):"mymemory";
                    Online.CancelTranslation();CancelWebsiteTranslation();
                    if(provider is "google" or "yandex")return await TranslateWebsite(WebTranslationRequest.Create(provider,Text("text",10000),Text("source",10),Text("target",10)));
                    return await Online.Translate(Text("text",20000),Text("source",10),Text("target",10),provider);
                case "translation.cancel":CancelWebsiteTranslation();Online.CancelTranslation();return new{cancelled=true};
                case "timer.get" or "timer.start" or "timer.pause" or "timer.resume" or "timer.reset" or "timer.dismiss":return HandleTimer(c);
                default:throw new CommandFault("unknown_action","Действие не поддерживается");
            }
        }finally{running--;}
    }
    static int[] ReadDesktopSelection(JsonElement parameters,string error)
    {
        if(!parameters.TryGetProperty("ids",out var selection)||selection.ValueKind!=JsonValueKind.Array||
            selection.GetArrayLength() is <1 or >200||
            selection.EnumerateArray().Any(item=>item.ValueKind!=JsonValueKind.Number||!item.TryGetInt32(out _)))
            throw new CommandFault("schema",error);
        return selection.EnumerateArray().Select(item=>item.GetInt32()).ToArray();
    }
    static Dictionary<int,int> ReadFolderIconStyles(JsonElement values)
    {
        const string error="Выберите доступные стили папок.";
        if(values.ValueKind!=JsonValueKind.Object || values.EnumerateObject().Count()>200)
            throw new CommandFault("schema",error);
        var styles=new Dictionary<int,int>();
        foreach(var item in values.EnumerateObject())
        {
            if(!int.TryParse(item.Name,out var folder) || item.Value.ValueKind!=JsonValueKind.Number ||
                !item.Value.TryGetInt32(out var style) || !styles.TryAdd(folder,style))
                throw new CommandFault("schema",error);
        }
        return styles;
    }
    static Dictionary<int,string> ReadFolderIconChoices(JsonElement values)
    {
        const string error="Выберите значки из списка.";
        if(values.ValueKind!=JsonValueKind.Object || values.EnumerateObject().Count()>200)
            throw new CommandFault("schema",error);
        var icons=new Dictionary<int,string>();
        foreach(var item in values.EnumerateObject())
        {
            if(!int.TryParse(item.Name,out var folder) || item.Value.ValueKind!=JsonValueKind.String ||
                item.Value.GetString() is not {Length: > 0 and <= 64} icon || !icons.TryAdd(folder,icon))
                throw new CommandFault("schema",error);
        }
        return icons;
    }
    public void Stop(){Ready=false;CancelWebsiteTranslation();if(ownsDesktopState)desktopState.Dispose();}
    async Task<object> ShareApplication()
    {
        if(Interlocked.Exchange(ref sharing,1)!=0)throw new CommandFault("busy","Упаковка уже выполняется. Дождитесь готового архива.");
        try
        {
            var path=await Task.Run(()=>DistributionArchive.Create(LiteInstallation.ApplicationDirectory,
                LiteInstallation.Root ?? AppContext.BaseDirectory, LiteInstallation.Root));
            var revealed=await ExplorerReveal.TryRevealAsync(path);
            return new{path,revealed};
        }
        catch(InvalidDataException e){throw new CommandFault("share",e.Message);}
        catch(FileNotFoundException e){throw new CommandFault("share","Не хватает файла portable-сборки. Распакуйте полную новую публикацию SideWidget. "+e.Message);}
        catch(UnauthorizedAccessException){throw new CommandFault("share","Нет доступа для записи ZIP рядом с SideWidget. Переместите папку приложения в доступную для записи локальную папку и повторите попытку.");}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException)
        {throw new CommandFault("share","Не удалось создать ZIP. Проверьте свободное место и целостность папки приложения. "+e.Message);}
        finally{Interlocked.Exchange(ref sharing,0);}
    }
}





