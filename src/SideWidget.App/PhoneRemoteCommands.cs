using System.Text.Json;
using SideWidget.Core;
using SideWidget.Remote;

namespace SideWidget.App;

sealed partial class Controller
{
    PhoneRemoteService? phoneRemote {get=>desktopState.PhoneRemote;set=>desktopState.PhoneRemote=value;}
    public void InitializePhoneRemote(Func<Func<Task<object>>, Task<object>> onUi)
    {
        if(desktopState.PhoneRemoteInitialized)return;
        desktopState.PhoneRemoteInitialized=true;
        try
        {
            phoneRemote = new(Path.Combine(Store.DirectoryPath, "phone-remote"),
                () => onUi(async () =>
                {
                    var playing = await media.Get();
                    var levels = JsonSerializer.SerializeToElement(await Task.Run(audio.Get), Protocol.Json);
                    return new { media = playing, audio = new { volume = levels.GetProperty("volume"), muted = levels.GetProperty("muted"), available = levels.GetProperty("available") } };
                }),
                async request => { await onUi(async () =>
                {
                    void CheckAccess() { if (request.StillAuthorized?.Invoke() != true) throw new RemoteFault("Доступ к пульту отозван или пульт выключен."); }
                    CheckAccess();
                    if (request.Command == "volume") await Task.Run(() => { CheckAccess(); return audio.Set(request.Volume, null, null, null); });
                    else if (request.Command == "mute") await Task.Run(() => { CheckAccess(); return audio.Set(null, request.Muted, null, null); });
                    else await media.Command(request.Id!, request.Command, request.PositionSeconds, request.StillAuthorized);
                    return new { };
                }); });
        }
        catch (Exception error) when (error is IOException or JsonException or RemoteFault or UnauthorizedAccessException)
        { phoneRemoteError = "Настройки пульта недоступны. Данные сохранены; проверьте папку phone-remote. Тип: " + error.GetType().Name; }
    }
    string? phoneRemoteError {get=>desktopState.PhoneRemoteError;set=>desktopState.PhoneRemoteError=value;}
    async Task<object> PhoneRemoteCommand(Command command)
    {
        var remote = phoneRemote ?? throw new CommandFault("remote", phoneRemoteError ?? "Пульт ещё не готов.");
        var p = command.Params;
        try
        {
            switch (command.ActionId)
            {
                case "remote.get": return await remote.Info();
                case "remote.configure":
                    return await remote.Configure(p.GetProperty("enabled").GetBoolean(), Protocol.Text(p, "adapterId", 256), p.GetProperty("port").GetInt32(), p.GetProperty("setupPort").GetInt32(), p.GetProperty("startup").GetBoolean());
                case "remote.setup":
                    return await remote.Setup(Protocol.Text(p, "adapterId", 256), p.GetProperty("port").GetInt32(), p.GetProperty("setupPort").GetInt32(), p.GetProperty("startup").GetBoolean());
                case "remote.invite": return await remote.Invite();
                case "remote.decide": remote.Decide(Protocol.Text(p, "id", 32), p.GetProperty("approved").GetBoolean()); return await remote.Info();
                case "remote.revoke": remote.Revoke(Protocol.Text(p, "id", 32)); return await remote.Info();
                default: throw new CommandFault("remote", "Неизвестная команда пульта.");
            }
        }
        catch (RemoteFault error) { throw new CommandFault("remote", error.Message); }
    }
}
