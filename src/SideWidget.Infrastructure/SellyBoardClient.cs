using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SideWidget.Core;

namespace SideWidget.Infrastructure;
public sealed class SellyBoardClient:IDisposable
{
    readonly HttpClient http;readonly bool ownsHttp;readonly Uri server;readonly string token;
    public SellyBoardClient(string address,string key,HttpClient? transport=null)
    {
        server=ValidateBase(address);
        if(string.IsNullOrWhiteSpace(key)||key.Length>512||key.Any(char.IsControl))throw new CommandFault("credential","Проверьте API-ключ SellyBoard");
        token=key;ownsHttp=transport==null;
        http=transport??new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){Timeout=TimeSpan.FromSeconds(20)};
    }
    public static Uri ValidateBase(string value)
    {
        if(value!=value.Trim()||value.Any(char.IsControl)||!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.UserInfo!=""||uri.Query!=""||uri.Fragment!=""||
           !(uri.Scheme=="https"||uri.Scheme=="http"&&(uri.Host=="127.0.0.1"||uri.Host=="localhost"||uri.Host=="[::1]")))
            throw new CommandFault("address","Укажите HTTPS-адрес SellyBoard. HTTP разрешён только для локальной проверки.");
        return new Uri(uri.AbsoluteUri.TrimEnd('/')+"/");
    }
    public Task<JsonObject> Options()=>Send("GET","calendar/options");
    public Task<JsonObject> Events(string start,string end,string zone)
    {
        if(!DateOnly.TryParseExact(start,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var a)||!DateOnly.TryParseExact(end,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var b)||b<=a||b.DayNumber-a.DayNumber>62||zone.Length>100)
            throw new CommandFault("range","Выберите период от 1 до 62 дней");
        return Send("GET","calendar/events?start="+start+"&end="+end+"&timezone="+Uri.EscapeDataString(zone));
    }
    public Task<JsonObject> Detail(string id,string? occurrence)
    {
        ValidateId(id);
        if(occurrence?.Length>100)throw new CommandFault("occurrence","Некорректный повтор");
        return Send("GET","calendar/tasks/"+id+(string.IsNullOrEmpty(occurrence)?"":"?occurrence="+Uri.EscapeDataString(occurrence)));
    }
    static void ValidateId(string id){if(!Regex.IsMatch(id,"^[1-9][0-9]{0,18}$")||!long.TryParse(id,out _))throw new CommandFault("id","Некорректный номер задачи");}
    public async Task<JsonObject> SetStatus(JsonObject card,string status)
    {
        if(status is not ("todo" or "in_progress" or "done" or "cancelled"))throw new CommandFault("status","Некорректный статус");
        if(card["recurrence"]!=null)throw new CommandFault("recurrence","Повторяющиеся задачи редактируйте в SellyBoard");
        if(card["can_write"]?.GetValue<bool>()!=true)throw new CommandFault("readonly","Задача доступна только для чтения");
        var draft=(JsonObject)card.DeepClone();draft["status"]=status;draft["scope"]="series";
        return await Save(draft);
    }
    public async Task<JsonObject> Save(JsonObject data)
    {
        var result=await Send("POST","calendar/tasks/save",data);
        try
        {
            var task=result["task"]?.AsObject()??throw new System.Text.Json.JsonException();
            var id=task["id"]?.GetValue<string>()??"";
            if(!Regex.IsMatch(id,"^[1-9][0-9]{0,18}$")||!long.TryParse(id,out _)||task["version"]?.GetValue<long>() is not >0||
               data["id"] is JsonNode expectedId&&expectedId.GetValue<string>()!=id)
                throw new System.Text.Json.JsonException();
            return result;
        }
        catch(Exception error)when(error is System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {throw new CommandFault("response","Сервер не подтвердил сохранённую задачу. Проверьте результат перед повтором.");}
    }
    async Task<JsonObject> Send(string method,string path,JsonObject? body=null)
    {
        using var request=new HttpRequestMessage(new HttpMethod(method),new Uri(server,"api/v1/"+path));
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if(body!=null)request.Content=new StringContent(body.ToJsonString(),Encoding.UTF8,"application/json");
        try
        {
            using var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(25));
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellation.Token);
            var status=(int)response.StatusCode;
            if(status is >=300 and <400)throw new CommandFault("transport","Сервер перенаправил запрос. Проверьте точный адрес SellyBoard.");
            if(!response.IsSuccessStatusCode)throw status switch{
                401=>new CommandFault("unauthorized","Ключ истёк или отозван. Подключите SellyBoard заново."),
                403=>new CommandFault("forbidden","Недостаточно прав или доступ только для чтения."),
                409=>new CommandFault("conflict","Задача уже изменена. Обновите карточку перед сохранением."),
                404=>new CommandFault("not_found","Задача или календарный API не найдены. Проверьте адрес и версию сервера."),
                429=>new CommandFault("rate_limited","Слишком много запросов. Повторите позже."),
                >=500=>new CommandFault("unavailable","Сервер временно недоступен. Обновите данные перед повтором записи."),
                _=>new CommandFault("invalid","Сервер отклонил данные. Проверьте поля задачи.")};
            if(response.Content.Headers.ContentLength>1024*1024)throw new CommandFault("limit","Ответ SellyBoard слишком большой");
            await using var stream=await response.Content.ReadAsStreamAsync(cancellation.Token);
            using var memory=new MemoryStream();var buffer=new byte[8192];int read;
            while((read=await stream.ReadAsync(buffer,cancellation.Token))>0){if(memory.Length+read>1024*1024)throw new CommandFault("limit","Ответ SellyBoard слишком большой");memory.Write(buffer,0,read);}
            var envelope=JsonNode.Parse(memory.ToArray())?.AsObject()??throw new System.Text.Json.JsonException();
            if(envelope["error"]!=null||envelope["ok"]?.GetValue<bool>()==false)throw new System.Text.Json.JsonException();
            // Unified service API uses data; calendar adapter tests/older local draft use result.
            return (envelope["data"]??envelope["result"])?.DeepClone().AsObject()??throw new System.Text.Json.JsonException();
        }
        catch(Exception error)when(error is HttpRequestException or OperationCanceledException or IOException)
        {throw new CommandFault(method=="POST"?"unknown_outcome":"network",method=="POST"?"Связь прервана. Результат записи неизвестен; обновите данные перед повтором.":"Не удалось связаться с SellyBoard. Проверьте подключение.");}
        catch(Exception error)when(error is System.Text.Json.JsonException or InvalidOperationException)
        {throw new CommandFault("response","Некорректный ответ SellyBoard. Проверьте версию API.");}
    }
    public void Dispose(){if(ownsHttp)http.Dispose();}
}
