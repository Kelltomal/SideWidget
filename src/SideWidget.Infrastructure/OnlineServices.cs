using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SideWidget.Core;
namespace SideWidget.Infrastructure;
public sealed record CurrencyRate(string Code,string Name,decimal Nominal,decimal Value,decimal Rate);
public sealed record CurrencySnapshot(string Date,string Source,DateTimeOffset FetchedAt,bool Stale,List<CurrencyRate> Rates,string? Message=null);
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class OnlineServices:IDisposable
{
    readonly Store store;readonly SecretVault vault;readonly HttpClient client;readonly bool ownsClient;
    CancellationTokenSource? translation;DateTimeOffset lastWeather;readonly SemaphoreSlim weatherGate=new(1);
    public OnlineServices(Store store,SecretVault vault,HttpClient? client=null){this.store=store;this.vault=vault;ownsClient=client==null;this.client=client??new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){Timeout=TimeSpan.FromSeconds(15)};}
    async Task<byte[]> Send(HttpRequestMessage request,CancellationToken cancellation=default)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
        if(!response.IsSuccessStatusCode)throw new CommandFault("network","Источник вернул HTTP "+(int)response.StatusCode+". Повторите позже.");
        await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);using var output=new MemoryStream();var buffer=new byte[8192];int read;
        while((read=await stream.ReadAsync(buffer,timeout.Token))>0){if(output.Length+read>1024*1024)throw new CommandFault("limit","Ответ источника слишком большой");output.Write(buffer,0,read);}return output.ToArray();
    }
    public static CurrencySnapshot ParseCurrency(byte[] xml,DateTimeOffset fetchedAt)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);using var stream=new MemoryStream(xml);using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1024*1024});var root=XDocument.Load(reader).Root??throw new CommandFault("currency","Пустой ответ курса");
        var date=DateTime.ParseExact((string?)root.Attribute("Date")??"","dd.MM.yyyy",CultureInfo.InvariantCulture).ToString("yyyy-MM-dd");var rates=new List<CurrencyRate>{new("RUB","Российский рубль",1,1,1)};
        foreach(var row in root.Elements("Valute")){var code=(string?)row.Element("CharCode")??"";var name=(string?)row.Element("Name")??code;var nominal=decimal.Parse((string?)row.Element("Nominal")??"0",CultureInfo.InvariantCulture);var value=decimal.Parse(((string?)row.Element("Value")??"0").Replace(',','.'),CultureInfo.InvariantCulture);if(nominal<=0||value<=0||!Regex.IsMatch(code,"^[A-Z]{3}$"))throw new CommandFault("currency","Некорректная котировка");rates.Add(new(code,name,nominal,value,value/nominal));}
        return new(date,"Банк России · ежедневные официальные курсы",fetchedAt,false,rates);
    }
    public async Task<object> Currency()
    {
        var cache=store.Read<CurrencySnapshot?>("personal.currency",null);
        if(cache!=null&&DateTimeOffset.UtcNow-cache.FetchedAt<TimeSpan.FromMinutes(30))return cache;
        try{using var request=new HttpRequestMessage(HttpMethod.Get,"https://www.cbr.ru/scripts/XML_daily.asp");var result=ParseCurrency(await Send(request),DateTimeOffset.UtcNow);store.Save("personal.currency",result);return result;}
        catch(Exception e) when(e is HttpRequestException or TaskCanceledException or CommandFault or XmlException or FormatException){if(cache!=null)return cache with{Stale=true,Message="Не удалось обновить. Показан сохранённый курс."};throw new CommandFault("offline","Курсы недоступны. Проверьте соединение и повторите.");}
    }
    public async Task<object> Weather(string city)
    {
        city=city.Trim();if(city.Length is <2 or >120)throw new CommandFault("city","Введите город, при необходимости добавьте страну через запятую");var cacheKey="personal.weather."+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(city.ToLowerInvariant())))[..24];
        await weatherGate.WaitAsync();try{
            var cache=store.Read<JsonElement>(cacheKey,default);if(DateTimeOffset.UtcNow-lastWeather<TimeSpan.FromSeconds(10))throw new CommandFault("rate","Повторите обновление через несколько секунд");lastWeather=DateTimeOffset.UtcNow;
            try{
                using var locationRequest=new HttpRequestMessage(HttpMethod.Get,"https://geocoding-api.open-meteo.com/v1/search?count=1&language=ru&name="+Uri.EscapeDataString(city));using var locations=JsonDocument.Parse(await Send(locationRequest));
                if(!locations.RootElement.TryGetProperty("results",out var items)||items.GetArrayLength()==0)throw new CommandFault("city","Город не найден. Уточните название и страну.");var location=items[0];var latitude=location.GetProperty("latitude").GetDouble().ToString(CultureInfo.InvariantCulture);var longitude=location.GetProperty("longitude").GetDouble().ToString(CultureInfo.InvariantCulture);
                using var forecastRequest=new HttpRequestMessage(HttpMethod.Get,$"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m&daily=temperature_2m_max,temperature_2m_min,weather_code&forecast_days=3&timezone=auto");using var forecast=JsonDocument.Parse(await Send(forecastRequest));var current=forecast.RootElement.GetProperty("current");var daily=forecast.RootElement.GetProperty("daily");var days=Enumerable.Range(0,daily.GetProperty("time").GetArrayLength()).Select(i=>new{date=daily.GetProperty("time")[i].GetString(),min=daily.GetProperty("temperature_2m_min")[i].GetDouble(),max=daily.GetProperty("temperature_2m_max")[i].GetDouble(),code=daily.GetProperty("weather_code")[i].GetInt32()}).ToArray();
                var result=new{city=location.GetProperty("name").GetString()+", "+(location.TryGetProperty("country",out var country)?country.GetString():""),temperature=current.GetProperty("temperature_2m").GetDouble(),apparent=current.GetProperty("apparent_temperature").GetDouble(),wind=current.GetProperty("wind_speed_10m").GetDouble(),precipitation=current.GetProperty("precipitation").GetDouble(),code=current.GetProperty("weather_code").GetInt32(),time=current.GetProperty("time").GetString(),fetchedAt=DateTimeOffset.UtcNow,source="Open-Meteo · модельный прогноз · CC BY 4.0",forecast=days,stale=false};store.Save(cacheKey,result);return result;
            // Valid JSON can still violate the provider schema: missing properties,
            // wrong value kinds, out-of-range numbers or unequal daily arrays.
            // Treat these as unusable upstream data, retaining the last real forecast.
            }catch(Exception e) when(e is HttpRequestException or TaskCanceledException or JsonException or CommandFault or KeyNotFoundException or InvalidOperationException or FormatException or IndexOutOfRangeException){if(cache.ValueKind==JsonValueKind.Object){var result=System.Text.Json.Nodes.JsonNode.Parse(cache.GetRawText())!;result["stale"]=true;return result;}if(e is CommandFault fault&&fault.Code=="city")throw;throw new CommandFault("offline","Погода недоступна. Проверьте соединение и повторите.");}
        }finally{weatherGate.Release();}
    }
    public object TranslationInfo()=>new{configured=store.Read<string?>("personal.deepl.secret",null)!=null,provider="DeepL API",defaultProvider="mymemory",free=store.Read("personal.deepl.free",true)};
    public void ConfigureTranslation(string key,bool free){if(string.IsNullOrWhiteSpace(key)||key.Length>200||key.Contains('\r')||key.Contains('\n'))throw new CommandFault("key","Введите действующий ключ DeepL API");store.Save("personal.deepl.secret",vault.Save(key.Trim()));store.Save("personal.deepl.free",free);}
    public void CancelTranslation(){translation?.Cancel();}
    public async Task<object> Translate(string text,string source,string target,string provider="mymemory")
    {
        if(string.IsNullOrWhiteSpace(text)||text.Length>20000)throw new CommandFault("translation","Введите текст до 20 000 символов");source=source.ToUpperInvariant();target=target.ToUpperInvariant();if(!Regex.IsMatch(target,"^[A-Z]{2}(-[A-Z]{2})?$")||source!="AUTO"&&source!=""&&!Regex.IsMatch(source,"^[A-Z]{2}(-[A-Z]{2})?$"))throw new CommandFault("language","Выберите язык");
        if(provider is not ("mymemory" or "deepl"))throw new CommandFault("provider","Выберите переводчик");
        if(provider=="mymemory"){
            if(source is "" or "AUTO")throw new CommandFault("language","Для MyMemory выберите исходный язык");
            if(Encoding.UTF8.GetByteCount(text)>500)throw new CommandFault("limit","MyMemory принимает до 500 байт за запрос. Сократите текст.");
        }
        string? key=null;
        if(provider=="deepl"){
            var reference=store.Read<string?>("personal.deepl.secret",null)??throw new CommandFault("key","Настройте ключ DeepL API");key=vault.Read(reference);
        }
        translation?.Cancel();var cancellation=new CancellationTokenSource();translation=cancellation;
        try{
            if(provider=="mymemory"){
                using var request=new HttpRequestMessage(HttpMethod.Get,"https://api.mymemory.translated.net/get?q="+Uri.EscapeDataString(text)+"&langpair="+Uri.EscapeDataString(source.ToLowerInvariant()+"|"+target.ToLowerInvariant()));
                using var result=JsonDocument.Parse(await Send(request,cancellation.Token));CheckCurrent();var root=result.RootElement;
                if(root.TryGetProperty("quotaFinished",out var quota)&&quota.ValueKind==JsonValueKind.True)throw new CommandFault("quota","Дневной лимит MyMemory исчерпан. Повторите завтра.");
                if(!root.TryGetProperty("responseStatus",out var status)||status.ToString()!="200")throw new CommandFault("translation","MyMemory не выполнил перевод. Проверьте языки или выберите другой сервис.");
                var translated=root.GetProperty("responseData").GetProperty("translatedText").GetString();
                if(string.IsNullOrWhiteSpace(translated))throw new JsonException();
                return new{text=System.Net.WebUtility.HtmlDecode(translated),provider="MyMemory"};
            }
            var host=store.Read("personal.deepl.free",true)?"api-free.deepl.com":"api.deepl.com";using var deepRequest=new HttpRequestMessage(HttpMethod.Post,$"https://{host}/v2/translate");deepRequest.Headers.Authorization=new AuthenticationHeaderValue("DeepL-Auth-Key",key);var body=new Dictionary<string,object>{{"text",new[]{text}},{"target_lang",target},{"preserve_formatting",true}};if(source!=""&&source!="AUTO")body["source_lang"]=source;deepRequest.Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json");using var deepResult=JsonDocument.Parse(await Send(deepRequest,cancellation.Token));CheckCurrent();var translatedText=deepResult.RootElement.GetProperty("translations")[0].GetProperty("text").GetString();if(string.IsNullOrWhiteSpace(translatedText))throw new JsonException();return new{text=translatedText,provider="DeepL API"};
            void CheckCurrent(){cancellation.Token.ThrowIfCancellationRequested();if(translation!=cancellation)throw new OperationCanceledException();}
        }
        catch(OperationCanceledException){throw new CommandFault("cancelled","Перевод отменён или время ожидания истекло");}
        catch(HttpRequestException){throw new CommandFault("offline","Переводчик недоступен. Проверьте соединение или выберите другой сервис.");}
        catch(Exception e) when(e is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException){throw new CommandFault("translation","Переводчик прислал некорректный ответ. Повторите позже или выберите другой сервис.");}
        finally{if(translation==cancellation)translation=null;cancellation.Dispose();}
    }
    public void Dispose(){CancelTranslation();if(ownsClient)client.Dispose();}
}


