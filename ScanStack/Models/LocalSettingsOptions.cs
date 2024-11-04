namespace ScanStack.Models;

public class LocalSettingsOptions
{
    public string? ApplicationDataFolder
    {
        get; set;
    }

    public string? LocalSettingsFile
    {
        get; set;
    }

    public bool AllowDataCollecting { get; set; } = true;
}
