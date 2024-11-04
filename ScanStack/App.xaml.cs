using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;

using ScanStack.Activation;
using ScanStack.Contracts.Services;
using ScanStack.Core.Contracts.Services;
using ScanStack.Core.Services;
using ScanStack.Models;
using ScanStack.Notifications;
using ScanStack.Services;
using ScanStack.ViewModels;
using ScanStack.Views;
using Windows.Storage;

namespace ScanStack;

public partial class App : Application
{
    private static Mutex? _mutex;
    public IHost Host
    {
        get;
    }

    public static T GetService<T>()
        where T : class
    {
        if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }

    public static WindowEx MainWindow { get; } = new MainWindow();
    private TelemetryLogger TelemetryLogger
    {
        get;
    }
    public static UIElement? AppTitlebar
    {
        get; set;
    }
    public App()
    {
        InitializeComponent();
        bool isNewInstance;
        _mutex = new Mutex(true, "ScanStack", out isNewInstance);

        if (!isNewInstance)
        {
            Environment.Exit(0);
        }
        Host = Microsoft.Extensions.Hosting.Host.
        CreateDefaultBuilder().
        UseContentRoot(AppContext.BaseDirectory).
        ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        }).
        ConfigureServices((context, services) =>
        {
            // Default Activation Handler
            services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

            // Other Activation Handlers
            services.AddTransient<IActivationHandler, AppNotificationActivationHandler>();

            // Services
            services.AddSingleton<IAppNotificationService, AppNotificationService>();
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            services.AddTransient<INavigationViewService, NavigationViewService>();

            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();

            // Core Services
            services.AddSingleton<IFileService, FileService>();

            // Views and ViewModels
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SettingsPage>();
            services.AddSingleton<ScanViewModel>();
            services.AddSingleton<ScanPage>();
            services.AddTransient<ShellPage>();
            services.AddTransient<ShellViewModel>();

            services.AddScoped<TelemetryLogger>();

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        }).
        Build();

        TelemetryLogger = GetService<TelemetryLogger>();
        TelemetryLogger.TelemetryClient.TrackEvent("AppLaunched");
        MainWindow.Closed += async (s, e) =>
        {
            _mutex?.ReleaseMutex();
            await TelemetryLogger.Flush();
        };
        App.GetService<IAppNotificationService>().Initialize();

        UnhandledException += App_UnhandledException;
    }
    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        TelemetryLogger.TelemetryClient.TrackException(e.Exception);
    }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        //App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationSamplePayload".GetLocalized(), AppContext.BaseDirectory));
        var pictures = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
        var path = Path.Combine(pictures.SaveFolder.Path, "ScanStack");
        if (!Path.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        await App.GetService<IActivationService>().ActivateAsync(args);
    }
}
