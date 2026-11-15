namespace SideWidget.Core;
public sealed record VisualizerOptions(bool Enabled=true,bool ShowInHeader=true,double Brightness=.35,double Sensitivity=1,string Style="wave")
{
 public void Validate(){if(!double.IsFinite(Brightness)||Brightness is <0 or >1||!double.IsFinite(Sensitivity)||Sensitivity is <.25 or >4||Style is not ("wave" or "bars"))throw new CommandFault("visualizer","Проверьте яркость, чувствительность и вид визуализации");}
}
