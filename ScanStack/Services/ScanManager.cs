using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;

namespace ScanStack.Services;

public partial class ScannerManager : ObservableObject
{
    public event Action? SelectedScannerChanged;
    private readonly DeviceWatcher _scannerWatcher;

    [ObservableProperty]
    private ImageScanner? _scanner;

    [ObservableProperty]
    private ObservableCollection<DeviceInformation> _devices = [];

    [ObservableProperty]
    private DeviceInformation? _selectedDevice;
    async partial void OnSelectedDeviceChanged(DeviceInformation? value)
    {
        await InitializeScannerAsync(value);
        SelectedScannerChanged?.Invoke();
    }

    public ScannerManager()
    {
        _scannerWatcher = DeviceInformation.CreateWatcher(DeviceClass.ImageScanner);
        _scannerWatcher.Added += OnScannerAdded;
        _scannerWatcher.Removed += OnScannerRemoved;
        _scannerWatcher.EnumerationCompleted += OnScannerEnumerationComplete;
        _scannerWatcher.Start();
    }

    private void OnScannerEnumerationComplete(DeviceWatcher sender, object args)
    {
    }

    private void OnScannerRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        if (Devices.Any(p => p.Id == args.Id))
        {
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                Devices.Remove(Devices.First(p => p.Id == args.Id));
            });
        }
    }

    private void OnScannerAdded(DeviceWatcher sender, DeviceInformation args)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            Devices.Add(args);
            SelectedDevice = args;
        });
    }

    public async Task InitializeScannerAsync(DeviceInformation? device)
    {
        if (device is null)
        {
            return;
        }

        Scanner = await ImageScanner.FromIdAsync(device.Id);
    }
}
