using Microsoft.UI.Xaml.Controls;

using ScanStack.ViewModels;
using Windows.System;

namespace ScanStack.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel
    {
        get;
    }

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
    }

    private async void BugRequestCard_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("https://github.com/Diyari-Kurdi/DeeSharp.ScanStack/issues"));
    }
}
