using System.Text.Json;
using SideWidget.Core;

static class TimerProtocolChecks
{
    public static void Run()
    {
        var requestId=Guid.NewGuid().ToString();
        string Envelope(string action,object parameters)=>JsonSerializer.Serialize(new{protocolVersion=1,requestId,actionId=action,@params=parameters},Protocol.Json);
        foreach(var action in new[]{"timer.pause","timer.resume","timer.reset","timer.dismiss"}){
            var command=Protocol.Parse(Protocol.Origin,Envelope(action,new{id="selected-timer"}));
            if(Protocol.Text(command.Params,"id")!="selected-timer")throw new Exception("Timer ID lost: "+action);
            Protocol.Parse(Protocol.Origin,Envelope(action,new{})); // Native header/legacy primary-timer action.
        }
        try{Protocol.Parse(Protocol.Origin,Envelope("timer.reset",new{id="selected-timer",extra=true}));throw new Exception("Unexpected timer parameter accepted");}
        catch(CommandFault error){if(error.RequestId!=requestId)throw new Exception("Rejected request cannot receive an immediate response");}
        try{Protocol.Parse("https://untrusted.test",Envelope("timer.reset",new{id="selected-timer"}));throw new Exception("Untrusted request accepted");}
        catch(CommandFault error){if(error.RequestId!=null)throw new Exception("Untrusted request must not receive a correlated response");}
        Console.WriteLine("PASS timer protocol: selected/primary actions and correlated errors");
    }
}
