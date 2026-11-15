using System.Text.Json;
using SideWidget.Core;
internal static class VisualizerSettingsChecks
{
 public static int Run(){var count=0;
 void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
 void Reject(Action action,string name){try{action();}catch(CommandFault){count++;return;}throw new Exception(name);}
 var defaults=JsonSerializer.Deserialize<Preferences>("{}",Protocol.Json)!;
 Check(defaults.Visualizer is null,"old settings migrate without invented visualizer object");
 var options=defaults.Visualizer??new VisualizerOptions();options.Validate();Check(options.Enabled&&options.ShowInHeader,"visualizer enabled defaults");
 foreach(var style in new[]{"wave","bars"}){var p=new Preferences(Visualizer:new VisualizerOptions(Brightness:.17,Sensitivity:2.5,Style:style));p.Validate();var restored=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(p,Protocol.Json),Protocol.Json)!;Check(restored.Visualizer==p.Visualizer,"visualizer settings survive JSON roundtrip");}
 Reject(()=>new VisualizerOptions(Brightness:1.01).Validate(),"brightness upper bound");Reject(()=>new VisualizerOptions(Brightness:double.NaN).Validate(),"brightness finite");Reject(()=>new VisualizerOptions(Sensitivity:.1).Validate(),"sensitivity lower bound");Reject(()=>new VisualizerOptions(Sensitivity:double.PositiveInfinity).Validate(),"sensitivity finite");Reject(()=>new VisualizerOptions(Style:"fake").Validate(),"unknown visualizer style");
 string Envelope(string action,object args)=>JsonSerializer.Serialize(new{protocolVersion=1,requestId=Guid.NewGuid().ToString(),actionId=action,@params=args},Protocol.Json);
 Check(Protocol.Parse(Protocol.Origin,Envelope("visualizer.get",new{})).ActionId=="visualizer.get","spectrum get contract");
 Check(Protocol.Parse(Protocol.Origin,Envelope("visualizer.settings",options)).ActionId=="visualizer.settings","visualizer settings contract");
 Reject(()=>Protocol.Parse(Protocol.Origin,Envelope("visualizer.get",new{rawPcm=true})),"spectrum rejects extra data");
 return count;
 }
}
