using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using SideWidget.Core;
using SideWidget.Infrastructure;

static class SellyBoardChecks
{
    public static async Task<int> Run()
    {
        var count=0;
        void Check(bool condition,string name){if(!condition)throw new Exception("SellyBoard: "+name);count++;}
        foreach(var url in new[]{"http://example.com/","https://name:secret@example.com/","https://example.com/?x=1","file:///C:/data","http://127.0.0.2/"})
        {
            var rejected=false;try{SellyBoardClient.ValidateBase(url);}catch(CommandFault){rejected=true;}
            Check(rejected,"reject unsafe base "+url);
        }
        var calls=new List<(string Path,string Body,string? Token)>();
        using var http=new HttpClient(new TestHttp(async request=>{
            var body=request.Content==null?"":await request.Content.ReadAsStringAsync();
            calls.Add((request.RequestUri!.AbsolutePath,body,request.Headers.Authorization?.Parameter));
            var result=request.RequestUri.AbsolutePath.EndsWith("options")
                ? "{\"account_id\":\"9223372036854775807\",\"timezone\":\"Europe/Moscow\",\"can_write\":true}"
                : request.RequestUri.AbsolutePath.EndsWith("save") ? "{\"task\":{\"id\":\"9223372036854775807\",\"version\":8,\"title\":\"Test\"}}"
                : "{\"id\":\"9223372036854775807\",\"version\":7,\"title\":\"Test\",\"notes\":\"keep notes\",\"description\":\"keep description\",\"marketplace\":\"Ozon\",\"observer_ids\":[\"45\"],\"recurrence\":null,\"can_write\":true}";
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"ok\":true,\"result\":"+result+"}",Encoding.UTF8,"application/json")};
        }));
        using var client=new SellyBoardClient("https://example.com/sb/","private-token",http);
        var options=await client.Options();Check(options["account_id"]!.GetValue<string>()=="9223372036854775807","int64 stays string");
        Check(calls[0].Path=="/sb/api/v1/calendar/options"&&calls[0].Token=="private-token","base path and bearer");
        await client.Events("2026-11-01","2026-12-01","Europe/Moscow");
        var card=await client.Detail("9223372036854775807",null);
        await client.SetStatus(card,"done");
        var payload=JsonNode.Parse(calls[^1].Body)!;
        Check(payload["notes"]!.GetValue<string>()=="keep notes"&&payload["marketplace"]!.GetValue<string>()=="Ozon"&&payload["version"]!.GetValue<int>()==7,"full card/version preserved");
        Check(payload["observer_ids"]![0]!.GetValue<string>()=="45"&&payload["status"]!.GetValue<string>()=="done","observers and status");
        var recurring=(JsonObject)card.DeepClone();recurring["recurrence"]=new JsonObject{["frequency"]="daily"};
        var repeatRejected=false;try{await client.SetStatus(recurring,"done");}catch(CommandFault e){repeatRejected=e.Code=="recurrence";}Check(repeatRejected,"no implicit series update");
        using var conflictHttp=new HttpClient(new TestHttp(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict){Content=new StringContent("{\"ok\":false,\"error\":\"private body\"}")})));
        using var conflictClient=new SellyBoardClient("https://example.com/","token",conflictHttp);
        var conflict=false;try{await conflictClient.Options();}catch(CommandFault e){conflict=e.Code=="conflict"&&!e.Message.Contains("private body");}Check(conflict,"409 safe error");
        using var redirectHttp=new HttpClient(new TestHttp(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect){Headers={Location=new Uri("https://evil.example/")}})));
        using var redirectClient=new SellyBoardClient("https://example.com/","token",redirectHttp);
        var redirect=false;try{await redirectClient.Options();}catch(CommandFault e){redirect=e.Code=="transport";}Check(redirect,"redirect refused");
        var directory=Path.Combine(Path.GetTempPath(),"sidewidget-selly-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);var store=new Store(directory);var vault=new SecretVault(Path.Combine(directory,"secrets"));
        var reject=false;var unknown=true;var malformed=false;var posted=new List<string>();
        using var pendingHttp=new HttpClient(new TestHttp(async request=>{
            var key=request.Headers.Authorization!.Parameter;
            if(request.Method==HttpMethod.Get)return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"data\":{\"account_id\":\"9\",\"company_id\":\""+(key=="company-b"?"4":"3")+"\",\"timezone\":\"Europe/Moscow\",\"can_write\":true}}")};
            posted.Add(await request.Content!.ReadAsStringAsync());
            if(reject)return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            if(unknown){unknown=false;throw new HttpRequestException("synthetic lost response");}
            if(malformed)return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"data\":{}}")};
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"data\":{\"task\":{\"id\":\"7\",\"version\":1}}}")};
        }));
        using var connection=new SellyBoardConnection(store,vault,pendingHttp);
        await connection.Connect("https://example.com/","company-a");
        try{await connection.Create("Не потерять","2026-11-05");}catch(CommandFault e)when(e.Code=="unknown_outcome"){}
        var first=store.Read<JsonObject?>("personal.selly.pendingCreate",null);Check(first!=null,"unknown create remains durable");
        reject=true;try{await connection.RetryCreate();}catch(CommandFault e)when(e.Code=="unauthorized"){}
        Check(store.Read<JsonObject?>("personal.selly.pendingCreate",null)!=null,"401 does not erase uncertain write");
        await connection.Connect("https://example.com/","company-b");var changed=false;try{await connection.RetryCreate();}catch(CommandFault e){changed=e.Code=="identity";}Check(changed,"same account different company cannot replay pending");
        reject=false;await connection.Connect("https://example.com/","company-a");
        malformed=true;var malformedRejected=false;try{await connection.RetryCreate();}catch(CommandFault e){malformedRejected=e.Code=="response";}
        Check(malformedRejected&&store.Read<JsonObject?>("personal.selly.pendingCreate",null)!=null,"HTTP200 without saved task must preserve pending write");
        malformed=false;await connection.RetryCreate();
        Check(posted.All(body=>body==posted[0]),"exact payload and key survive reconnect/error");
        Check(store.Read<JsonObject?>("personal.selly.pendingCreate",null)==null,"confirmed creation clears pending");
        foreach(var invalid in new[]{"{\"data\":{}}", "{\"data\":{\"task\":{\"id\":\"2\",\"version\":8}}}", "{\"data\":{\"task\":{\"id\":\"9223372036854775807\"}}}", "{\"data\":{\"task\":{\"id\":\"9223372036854775807\",\"version\":\"8\"}}}", "{\"error\":{\"code\":\"failure\"},\"data\":{\"task\":{\"id\":\"9223372036854775807\",\"version\":8}}}"})
        {
            using var invalidHttp=new HttpClient(new TestHttp(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(invalid)})));
            using var invalidClient=new SellyBoardClient("https://example.com/","token",invalidHttp);
            var refused=false;try{await invalidClient.SetStatus(card,"done");}catch(CommandFault error){refused=error.Code=="response";}
            Check(refused,"invalid saved task acknowledgment refused");
        }
        return count;
    }
    sealed class TestHttp(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)=>send(request);}
}
