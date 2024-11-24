using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Devices.Scanners;
using Windows.Foundation;

namespace ScanStack.Services;

public partial class ScanSettings : ObservableObject
{
    private readonly ScannerManager _scannerManager;

    public ScanSettings(ScannerManager scannerManager)
    {
        _scannerManager = scannerManager;
        _scannerManager.SelectedScannerChanged += () =>
        {
            SupportedDpi = GetSupportedDPIs();
            SetSupportedAreas();
        };
    }

    [ObservableProperty]
    private double _scale = 50;
    partial void OnScaleChanged(double value)
    {
        var percentage = value / 100;
        MaxHeight = MAX_HEIGHT * percentage;
        MaxWidth = MAX_WIDTH * percentage;
    }

    [ObservableProperty]
    private double _maxHeight = 350;

    [ObservableProperty]
    private double _maxWidth = 250;

    private const double MAX_HEIGHT = 450;
    private const double MAX_WIDTH = 350;

    [ObservableProperty]
    private IList<uint> _supportedDpi = [];
    private float MinSupportedDpi
    {
        get; set;
    }
    public float SelectedDpi => SelectedDpiIndex switch
    {
        0 => SupportedDpi.Contains(75) ? 75 : MinSupportedDpi,
        1 => SupportedDpi.Contains(100) ? 100 : MinSupportedDpi,
        2 => SupportedDpi.Contains(150) ? 150 : MinSupportedDpi,
        3 => SupportedDpi.Contains(200) ? 200 : MinSupportedDpi,
        4 => SupportedDpi.Contains(300) ? 300 : MinSupportedDpi,
        5 => SupportedDpi.Contains(600) ? 600 : MinSupportedDpi,
        6 => SupportedDpi.Contains(1200) ? 1200 : MinSupportedDpi,
        7 => SupportedDpi.Contains(1600) ? 1600 : MinSupportedDpi,
        _ => SupportedDpi.Contains(75) ? 75 : MinSupportedDpi
    };
    public IList<ImageScannerColorMode> ImageScannerColorModes = Enum.GetValues(typeof(ImageScannerColorMode)).Cast<ImageScannerColorMode>().ToList();

    [ObservableProperty]
    private ImageScannerColorMode _imageScannerColorMode = ImageScannerColorMode.Color;

    [ObservableProperty]
    private int _imageScannerScanSourceIndex = 0;
    partial void OnImageScannerScanSourceIndexChanged(int value)
    {
        // Update scan source and DPI settings based on the selected scan source
        if (_scannerManager.Scanner is null) return;
        SupportedDpi = GetSupportedDPIs();
    }

    [ObservableProperty]
    private int _selectedDpiIndex = 4;

    [ObservableProperty]
    private int _selectedScanAreaIndex = 0;
    public static Size ConvertMmToInches(Size sizeInMm)
    {
        var widthInInches = sizeInMm.Width / 25.4;
        var heightInInches = sizeInMm.Height / 25.4;
        return new Size(widthInInches, heightInInches);
    }

    public static bool IsInInches(Size size)
    {
        return size.Width < 25.4 && size.Height < 25.4;
    }
    public Dictionary<string, Size> GetSupportedArea()
    {
        if (_scannerManager.SelectedDevice == null)
        {
            return [];
        }
        dynamic? config = ImageScannerScanSourceIndex switch
        {
            1 => _scannerManager.Scanner?.FeederConfiguration,
            _ => _scannerManager.Scanner?.FlatbedConfiguration
        };

        if (config is null)
        {
            return [];
        }

        var minSize = (Size)config.MinScanArea;
        var maxSize = (Size)config.MaxScanArea;

        return new()
        {
            { "Min",minSize },
            { "Max", maxSize }
        };
    }

    public bool IsScanAreaSupported(Rect scanArea) => SupportedAreas.ContainsValue(scanArea);

    private Dictionary<string, Rect> SupportedAreas
    {
        get; set;
    } = [];

    public void SetSupportedAreas()
    {
        if (_scannerManager.Scanner == null)
        {
            return;
        }
        var maxSize = GetSupportedArea().GetValueOrDefault("Max");
        var minSize = GetSupportedArea().GetValueOrDefault("Min");
        var isScannerInInches = IsInInches(minSize);
        if (isScannerInInches)
        {
            maxSize = new Size(maxSize.Width * 25.4, maxSize.Height * 25.4);
        }
        var scanAreas = new Dictionary<string, Size>()
        {
            {"default", maxSize },  // Entire scan area
            {"Letter",new(216, 279)},   // Letter
            {"Legal",new(216, 356)},   // Legal
            {"A3",new(297, 420)},   // A3
            {"A4",new(210, 297)},   // A4
            {"B5",new(176, 250)},   // B5
            {"B4",new(250, 353)},   // B4
            {"A5",new(148, 210)},   // A5
        };
        dynamic? config = ImageScannerScanSourceIndex switch
        {
            1 => _scannerManager.Scanner.FeederConfiguration,
            _ => _scannerManager.Scanner.FlatbedConfiguration
        };
        if (config is null)
        {
            return;
        }
        foreach (var scanArea in scanAreas)
        {

            var scanAreaToUse = isScannerInInches ? ConvertMmToInches(scanArea.Value) : scanArea.Value;
            var selectedRegion = new Rect(0, 0, scanAreaToUse.Width, scanAreaToUse.Height);
            try
            {
                config.SelectedScanRegion = selectedRegion;
                SupportedAreas.Add(scanArea.Key, selectedRegion);
            }
            catch
            {
                continue;
            }
        }
    }
    private IList<uint> GetSupportedDPIs()
    {
        if (_scannerManager.SelectedDevice == null)
        {
            return [];
        }

        dynamic? config = ImageScannerScanSourceIndex switch
        {
            1 => _scannerManager.Scanner?.FeederConfiguration,
            _ => _scannerManager.Scanner?.FlatbedConfiguration
        };

        if (config is null)
        {
            return [];
        }

        var minDPI = (uint)config.MinResolution.DpiX;
        var maxDPI = (uint)config.MaxResolution.DpiX;

        uint stepSize = 50;
        IList<uint> result = [];
        for (var dpi = minDPI; dpi <= maxDPI; dpi += stepSize)
        {
            result.Add(dpi);
        }

        if (!result.Contains(maxDPI))
        {
            result.Add(maxDPI);
        }
        MinSupportedDpi = result.Min();
        return result;
    }
}