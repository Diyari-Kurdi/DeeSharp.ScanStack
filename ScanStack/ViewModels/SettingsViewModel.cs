using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using ScanStack.Contracts.Services;
using ScanStack.Helpers;

namespace ScanStack.ViewModels;

public partial class SettingsViewModel : ObservableRecipient
{
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly ILocalSettingsService _localSettingsService;

    [ObservableProperty]
    private ElementTheme _elementTheme;

    [ObservableProperty]
    private bool _allowDataCollecting = true;
    partial void OnAllowDataCollectingChanged(bool value)
    {
        _localSettingsService.SaveSettingAsync("AllowDataCollecting", value);
    }
    public string Version { get; } = AppInfo.GetVersion();

    public ICommand SwitchThemeCommand
    {
        get;
    }

    public SettingsViewModel(IThemeSelectorService themeSelectorService, ILocalSettingsService localSettingsService)
    {
        _themeSelectorService = themeSelectorService;
        _elementTheme = _themeSelectorService.Theme;

        SwitchThemeCommand = new RelayCommand<ElementTheme>(
            async (param) =>
            {
                if (ElementTheme != param)
                {
                    ElementTheme = param;
                    await _themeSelectorService.SetThemeAsync(param);
                }
            });
        _localSettingsService = localSettingsService;
        _ = LoadDCFromSettingsAsync();
    }

    private async Task LoadDCFromSettingsAsync()
    {
        AllowDataCollecting = await _localSettingsService.ReadSettingAsync<bool>("AllowDataCollecting");
    }
}
