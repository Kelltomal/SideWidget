using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using SideWidget.Core;

namespace SideWidget.App;

public sealed partial class MainWindow
{
    readonly CodexQuotaView codexQuotaView=new(){Visibility=Visibility.Collapsed};
    CodexQuotaIndicator? codexQuota;
    void InitializeCodexHeader()
    {
        Grid.SetColumn(codexQuotaView,2);
        codexQuotaView.HorizontalAlignment=HorizontalAlignment.Left;
        codexQuotaView.Margin=new Thickness(8,0,0,0);
        header.Children.Add(codexQuotaView);
    }
    void UpdateCodexHeader()
    {
        var next=controller.ReadCompactCodexQuota();
        var widthChanged=(codexQuota is null)!=(next is null);
        codexQuota=next;
        var visible=next is not null&&motion.State==PanelState.Collapsed;
        codexQuotaView.Update(next,currentLightTheme==true);
        codexQuotaView.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
        if(visible)AutomationProperties.SetName(header,"SideWidget, "+clock.Text+", "+CodexQuotaView.Description(next!));
        if(widthChanged&&clockOnlyHeader&&!compactHidden)ApplyGeometry();
    }
}
