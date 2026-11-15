using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Nodes;
using SideWidget.Core;

namespace SideWidget.Infrastructure;
[SupportedOSPlatform("windows")]
public sealed class SellyBoardConnection(Store store,SecretVault vault,HttpClient? transport=null):IDisposable
{
    const string CredentialKey="personal.selly.credential";
    const string PendingKey="personal.selly.pendingCreate";
    readonly SemaphoreSlim gate=new(1,1);
    SellyBoardClient? client;
    JsonObject? options;
    readonly Dictionary<string,JsonObject> cards=new();
    string account="";string company="";string address="";
    public string Address=>address;
    public object Info()=>new{configured=client!=null,address,accountId=account,canWrite=options?["can_write"]?.GetValue<bool>()??false,
        timezone=options?["timezone"]?.GetValue<string>()??"Europe/Moscow",pendingCreate=store.Read<JsonObject?>(PendingKey,null)!=null,
        message=client==null?"Подключите персональный API-ключ SellyBoard.":"Подключение проверено. Данные обновляются по кнопке."};
    public async Task Restore()
    {
        var reference=store.Read<string?>(CredentialKey,null);if(reference==null)return;
        try{var credential=JsonNode.Parse(vault.Read(reference))!;await Connect(credential["address"]!.GetValue<string>(),credential["key"]!.GetValue<string>(),false);}
        catch(Exception error)when(error is CommandFault or IOException or System.Security.Cryptography.CryptographicException or JsonException){/* Reconnect explicitly; never expose credential failures. */}
    }
    public async Task Connect(string url,string key,bool persist=true)
    {
        await gate.WaitAsync();try{
            var candidate=new SellyBoardClient(url,key,transport);
            try{
                var next=await candidate.Options();var identity=next["account_id"]?.GetValue<string>();var organization=next["company_id"]?.GetValue<string>();
                if(string.IsNullOrEmpty(identity)||string.IsNullOrEmpty(organization))throw new CommandFault("identity","Нужен персональный календарный API-ключ с привязкой к пользователю и команде.");
                var normalized=SellyBoardClient.ValidateBase(url).AbsoluteUri;
                if(persist){var secret=vault.Save(JsonSerializer.Serialize(new{address=normalized,key}));store.Save(CredentialKey,secret);}
                client?.Dispose();client=candidate;options=next;account=identity;company=organization;address=normalized;cards.Clear();
            }catch{candidate.Dispose();throw;}
        }finally{gate.Release();}
    }
    public async Task Disconnect()
    {
        await gate.WaitAsync();try{store.Save<string?>(CredentialKey,null);client?.Dispose();client=null;options=null;account="";address="";cards.Clear();}
        finally{gate.Release();}
    }
    SellyBoardClient Require()=>client??throw new CommandFault("disconnected","Сначала подключите SellyBoard");
    public async Task<JsonObject> Events(string start,string end)
    {await gate.WaitAsync();try{return await Require().Events(start,end,options?["timezone"]?.GetValue<string>()??"Europe/Moscow");}finally{gate.Release();}}
    public async Task<JsonObject> Detail(string id,string? occurrence)
    {await gate.WaitAsync();try{var card=await Require().Detail(id,occurrence);cards[id+"|"+occurrence]=card;return (JsonObject)card.DeepClone();}finally{gate.Release();}}
    public async Task<JsonObject> SetStatus(string id,string status)
    {
        await gate.WaitAsync();try{
            var api=Require();if(!cards.TryGetValue(id+"|",out var card))throw new CommandFault("refresh","Откройте карточку перед изменением");
            // Remove before sending: unknown outcomes and 409 always require an explicit refresh.
            cards.Remove(id+"|");return await api.SetStatus(card,status);
        }finally{gate.Release();}
    }
    public async Task<JsonObject> Create(string title,string day)
    {
        await gate.WaitAsync();try{
            Require();if(store.Read<JsonObject?>(PendingKey,null)!=null)throw new CommandFault("pending","Сначала проверьте результат предыдущего создания.");
            title=title.Trim();if(title.Length is <1 or >250)throw new CommandFault("title","Название: от 1 до 250 символов");
            if(!DateOnly.TryParseExact(day,"yyyy-MM-dd",out var date))throw new CommandFault("date","Выберите дату");
            var data=new JsonObject{["client_key"]=Guid.NewGuid().ToString("N"),["title"]=title,["visibility"]="private",["assignee_ids"]=new JsonArray(account),
                ["status"]="todo",["priority"]="normal",["marketplace"]="",["all_day"]=true,["start"]=day,["end"]=date.AddDays(1).ToString("yyyy-MM-dd"),["timezone"]=options?["timezone"]?.DeepClone()};
            store.Save(PendingKey,new JsonObject{["address"]=address,["account"]=account,["company"]=company,["data"]=data});
            return await SendPending();
        }finally{gate.Release();}
    }
    public async Task<JsonObject> RetryCreate(){await gate.WaitAsync();try{return await SendPending();}finally{gate.Release();}}
    async Task<JsonObject> SendPending()
    {
        var api=Require();var pending=store.Read<JsonObject?>(PendingKey,null)??throw new CommandFault("pending","Нет ожидающего создания");
        if(pending["address"]?.GetValue<string>()!=address||pending["account"]?.GetValue<string>()!=account||pending["company"]?.GetValue<string>()!=company)throw new CommandFault("identity","Ожидающая задача принадлежит другому подключению. Подключите исходную учётную запись и команду.");
        var result=await api.Save(pending["data"]!.AsObject());store.Save<JsonObject?>(PendingKey,null);return result;
    }
    public void Dispose(){client?.Dispose();}
}
