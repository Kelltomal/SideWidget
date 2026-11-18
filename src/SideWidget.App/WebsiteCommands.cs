using System.Text.Json;
using SideWidget.Core;
using SideWidget.Infrastructure;

namespace SideWidget.App;

sealed partial class Controller
{
    async Task<object> HandleWebsites(Command command)
    {
        var websites=desktopState.Websites;
        if(command.ActionId=="sites.get")return websites.Snapshot();
        if(command.ActionId=="sites.delete")return await websites.DeleteAsync(Protocol.Text(command.Params,"id",64));
        if(command.ActionId=="sites.check")return await websites.CheckAsync(command.Params.TryGetProperty("id",out _)?Protocol.Text(command.Params,"id",64):null);
        if(!command.Params.TryGetProperty("site",out var value)||value.ValueKind!=JsonValueKind.Object)throw new CommandFault("website","Проверьте адрес и интервал проверки сайта");
        WebsiteTarget target;
        try{target=value.Deserialize<WebsiteTarget>(Protocol.Json)??throw new JsonException();}
        catch(JsonException){throw new CommandFault("website","Проверьте адрес и интервал проверки сайта");}
        return await websites.SaveAsync(target);
    }
}
