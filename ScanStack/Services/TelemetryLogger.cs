using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using ScanStack.Contracts.Services;

namespace ScanStack.Services;
public class TelemetryLogger
{
    public readonly TelemetryClient TelemetryClient;
    private readonly ILocalSettingsService _localSettingsService;

    public TelemetryLogger(IConfiguration configuration, ILocalSettingsService localSettingsService)
    {
        var telemetryConfig = new TelemetryConfiguration
        {
            ConnectionString = configuration["ApplicationInsights:ConnectionString"]
        };

        TelemetryClient = new TelemetryClient(telemetryConfig);
        _localSettingsService = localSettingsService;
    }

    public async Task Flush()
    {
        if (!await _localSettingsService.ReadSettingAsync<bool>("AllowDataCollecting"))
        {
            return;
        }

        TelemetryClient.Flush();
        await Task.Delay(1000);
    }
}