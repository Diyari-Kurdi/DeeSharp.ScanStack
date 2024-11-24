using Microsoft.UI.Xaml.Controls;

namespace ScanStack.Helpers;
public static class InfoDeliveryExt
{
    public static async void Show(this InfoBar infoBar, object? content, string itle = "",
        InfoBarSeverity Severity = InfoBarSeverity.Error, int Duration = 3_000)
    {
        infoBar.Severity = Severity;
        infoBar.Title = itle;
        infoBar.Content = content;
        infoBar.IsOpen = true;
        await Task.Delay(Duration);
        infoBar.IsOpen = false;
    }
}