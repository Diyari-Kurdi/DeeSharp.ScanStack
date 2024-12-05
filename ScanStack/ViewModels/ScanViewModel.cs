using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Printing;
using ScanStack.Core.Models;
using ScanStack.Helpers;
using ScanStack.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Graphics.Printing;
using Windows.Storage;

namespace ScanStack.ViewModels;

public partial class ScanViewModel : ObservableObject
{

    public event Action? SelectAllItems;
    public event Action? DeselectAllItems;

    private IEnumerable<IStorageFile> StorageFiles = [];
    [ObservableProperty]
    private bool _isAllItemsSelected = false;
    partial void OnIsAllItemsSelectedChanged(bool value)
    {
        if (value)
        {
            SelectAllItems?.Invoke();
        }
        else
        {
            DeselectAllItems?.Invoke();
        }
    }
    public ScannerManager ScannerManager
    {
        get;
    }
    public ScanSettings ScanSettings
    {
        get;
    }
    public ScanFileManager ScanFileManager
    {
        get;
    }
    public ScanViewModel()
    {
        ScanFileManager = new();
        ScannerManager = new();
        ScanSettings = new(ScannerManager);
        ScanFileManager.SelectedFilesChanged += (selectedFiles) =>
        {
            ScanFileManager.ImageEditor.SelectedFile = selectedFiles.FirstOrDefault();
        };
        ScanFileManager.ScanFilesChanged += () =>
        {
            PrintCommand.NotifyCanExecuteChanged();
            ShareCommand.NotifyCanExecuteChanged();
        };
        ScannerManager.SelectedScannerChanged += ScanCommand.NotifyCanExecuteChanged;
        InitializeUI();
    }

    private nint HWnd { get; set; } = 0;
    private DataTransferManager? DataTransferManager
    {
        get; set;
    }
    private PrintManager? PrintManager
    {
        get; set;
    }
    private void InitializeUI()
    {
        HWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        try
        {
            DataTransferManager = DataTransferManagerInterop.GetForWindow(HWnd);
            DataTransferManager.DataRequested += DataTransferManager_DataRequested;
        }
        catch { }
        try
        {
            PrintManager = PrintManagerInterop.GetForWindow(HWnd);
            PrintManager.PrintTaskRequested += PrintManager_PrintTaskRequested;
        }
        catch { }
    }

    private bool CanScan => ScannerManager.Scanner != null;
    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task Scan()
    {
        if (ScannerManager.Scanner == null)
        {
            return;
        }
        if (ScannerManager.SelectedDevice?.Id != ScannerManager.Scanner.DeviceId)
        {
            await ScannerManager.InitializeScannerAsync(ScannerManager.SelectedDevice);
        }

        ScannerManager.Scanner.FeederConfiguration.ColorMode = ScanSettings.ImageScannerColorMode;
        ScannerManager.Scanner.FlatbedConfiguration.ColorMode = ScanSettings.ImageScannerColorMode;
        var dpi = ScanSettings.SelectedDpi;
        ScannerManager.Scanner.FlatbedConfiguration.DesiredResolution = new ImageScannerResolution(dpi, dpi);

        var picturesFolder = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
        var scanStackPath = Path.Combine(picturesFolder.SaveFolder.Path, "ScanStack");
        if (!Path.Exists(scanStackPath))
        {
            await picturesFolder.SaveFolder.CreateFolderAsync("ScanStack");
        }
        try
        {
            var scanSource = ScanSettings.ImageScannerScanSourceIndex switch
            {
                1 => ImageScannerScanSource.Feeder,
                _ => ImageScannerScanSource.Flatbed
            };
            var maxSize = ScanSettings.GetSupportedArea().GetValueOrDefault("Max");
            var minSize = ScanSettings.GetSupportedArea().GetValueOrDefault("Min");
            var isScannerInInches = ScanSettings.IsInInches(minSize);
            if (isScannerInInches)
            {
                maxSize = new Size(maxSize.Width * 25.4, maxSize.Height * 25.4);
            }
            var scanArea = ScanSettings.SelectedScanAreaIndex switch
            {
                0 => maxSize,  // Entire scan area
                1 => new Size(216, 279),   // Letter
                2 => new Size(216, 356),   // Legal
                3 => new Size(297, 420),   // A3
                4 => new Size(210, 297),   // A4
                5 => new Size(176, 250),   // B5
                6 => new Size(250, 353),   // B4
                7 => new Size(148, 210),   // A5
                _ => ScanSettings.GetSupportedArea().GetValueOrDefault("Max")
            };
            if (!ScanSettings.IsScanAreaSupported(new Rect(0, 0, scanArea.Width, scanArea.Height)))
            {
                scanArea = maxSize;
            }
            // If the scanner is using inches, convert the scan area to inches
            var scanAreaToUse = isScannerInInches ? ScanSettings.ConvertMmToInches(scanArea) : scanArea;

            var selectedRegion = new Rect(0, 0, scanAreaToUse.Width, scanAreaToUse.Height);

            if (ScanSettings.ImageScannerScanSourceIndex == 1)
            {
                ScannerManager.Scanner.FeederConfiguration.SelectedScanRegion = selectedRegion;
            }
            else
            {
                ScannerManager.Scanner.FlatbedConfiguration.SelectedScanRegion = selectedRegion;
            }
            WeakReferenceMessenger.Default.Send(new DialogMessage(InfoBarMessage.ScanStarted));
            var result = await ScannerManager.Scanner.ScanFilesToFolderAsync(scanSource, await StorageFolder.GetFolderFromPathAsync(scanStackPath));
            WeakReferenceMessenger.Default.Send(new DialogMessage(InfoBarMessage.ScanFinished));
            if (result.ScannedFiles.Count > 0)
            {
                foreach (var file in result.ScannedFiles)
                {
                    ScanFileManager.ScanFiles.Add(new FileModel(file.Path, ScanFileManager.ScanFiles.Count + 1));
                }
            }
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new DialogMessage(InfoBarMessage.Error, ex.Message, "Error"));
        }
    }

    private PrintDocument printDocument = null!;
    private IPrintDocumentSource printDocumentSource = null!;
    private bool ContainsAny => ScanFileManager.ScanFiles.Count > 0;
    [RelayCommand(CanExecute = nameof(ContainsAny))]
    private async Task Print()
    {
        printDocument = new();
        printDocumentSource = printDocument.DocumentSource;
        printDocument.Paginate += PrintDocument_Paginate;
        printDocument.GetPreviewPage += PrintDocument_GetPreviewPage;
        printDocument.AddPages += PrintDocument_AddPages;
        foreach (var item in ScanFileManager.ScanFiles)
        {
            printDocument.AddPage(new Image()
            {
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Source = new BitmapImage(new Uri(item.Path, UriKind.RelativeOrAbsolute)),
                Stretch = Stretch.Uniform,
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch
            });
        }

        if (PrintManager.IsSupported())
        {
            try
            {
                await PrintManagerInterop.ShowPrintUIForWindowAsync(HWnd);
            }
            catch
            {
                var noPrintingDialog = new ContentDialog()
                {
                    Title = "Printing error",
                    Content = "\nSorry, printing can' t proceed at this time.",
                    PrimaryButtonText = "OK"
                };
                await noPrintingDialog.ShowAsync();
            }
        }
        else
        {
            var noPrintingDialog = new ContentDialog()
            {
                Title = "Printing not supported",
                Content = "\nSorry, printing is not supported on this device.",
                PrimaryButtonText = "OK"
            };
            await noPrintingDialog.ShowAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(ContainsAny))]
    private async Task Share(string fileType)
    {
        if (fileType == "pdf")
        {
            var picturesFolder = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
            var savePath = Path.Combine(picturesFolder.SaveFolder.Path, "ScanStack", $"Document-{DateTime.Now:MM-dd-yyyy-hh-mm-ss tt}.pdf");
            ScanFileManager.ConvertImagesToPdf(ScanFileManager.ScanFiles.Select(f => f.Path), savePath);
            StorageFiles = [await StorageFile.GetFileFromPathAsync(savePath)];
        }
        else
        {
            StorageFiles = await ScanFileManager.GetStorageFiles();
        }
        DataTransferManagerInterop.ShowShareUIForWindow(HWnd);
    }

    private void DataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
    {
        var request = args.Request;
        request.Data.SetStorageItems(StorageFiles);
        request.Data.Properties.Title = "Share";
    }

    private void PrintDocument_AddPages(object sender, AddPagesEventArgs e)
    {
        foreach (var scanFile in ScanFileManager.ScanFiles)
        {
            var image = new Image
            {
                Source = new BitmapImage(new Uri(scanFile.Path, UriKind.RelativeOrAbsolute)),
                Stretch = Stretch.Uniform,
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch
            };
            printDocument.AddPage(image);
        }
        printDocument.AddPagesComplete();
    }
    private void PrintDocument_GetPreviewPage(object sender, GetPreviewPageEventArgs e)
    {
        var imagePath = ScanFileManager.ScanFiles[e.PageNumber - 1].Path;

        var image = new Image
        {
            Source = new BitmapImage(new Uri(imagePath, UriKind.RelativeOrAbsolute)),
            Stretch = Stretch.Uniform,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch
        };
        printDocument.SetPreviewPage(e.PageNumber, image);
    }
    private void PrintDocument_Paginate(object sender, PaginateEventArgs e)
    {
        var totalPages = ScanFileManager.ScanFiles.Count;
        printDocument.SetPreviewPageCount(totalPages, PreviewPageCountType.Final);
    }

    private void PrintManager_PrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
    {
        var printTask = args.Request.CreatePrintTask("ScanStack Printer", PrintTaskSourceRequested);
        //printTask.Completed += PrintTask_Completed;
    }

    private void PrintTaskSourceRequested(PrintTaskSourceRequestedArgs args)
    {
        args.SetSource(printDocumentSource);
    }
}
