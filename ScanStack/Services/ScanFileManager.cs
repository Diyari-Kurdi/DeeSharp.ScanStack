using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using ScanStack.Core.Models;
using ScanStack.Helpers;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace ScanStack.Services;

public partial class ScanFileManager : ObservableObject
{
    public event Action<IList<FileModel>>? SelectedFilesChanged;
    public event Action? ScanFilesChanged;
    public ImageEditor ImageEditor
    {
        get;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveFileCommand))]
    private ObservableRangeCollection<FileModel> _scanFiles;
    public async Task<List<StorageFile>> GetStorageFiles()
    {
        if (ScanFiles.Count == 0)
        {
            return [];
        }
        var files = ScanFiles;
        var storageFiles = new List<StorageFile>();
        foreach (var file in files)
        {
            storageFiles.Add(await StorageFile.GetFileFromPathAsync(file.Path));
        }
        return storageFiles;
    }
    partial void OnScanFilesChanged(ObservableRangeCollection<FileModel> value)
    {
        value.CollectionChanged += (s, e) =>
        {
            ScanFilesChanged?.Invoke();
            SaveFileCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(SelectedItemsString));
            RefreshCommand.NotifyCanExecuteChanged();
        };
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedItemsString))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedFilesCommand))]
    private IList<FileModel> _selectedItems;
    partial void OnSelectedItemsChanged(IList<FileModel> value)
    {
        SelectedFilesChanged?.Invoke(value);
    }

    public ScanFileManager()
    {
        SelectedItems = [];
        ScanFiles = [];
        ImageEditor = new();
        ImageEditor.FileChanged += ImageEditor_FileChanged;
    }

    public void ImageEditor_FileChanged(FileModel oldFile, string newFilePath)
    {
        var index = ScanFiles.IndexOf(oldFile);
        ScanFiles.Remove(oldFile);
        InsetFile(newFilePath, index);
    }

    public void AddFile(string filePath)
    {
        ScanFiles.Add(new(filePath, ScanFiles.Count + 1));
    }
    public void InsetFile(string filePath, int index)
    {
        ScanFiles.Insert(index, new(filePath, index + 1));
    }
    public void RemoveFile(IList<FileModel> files)
    {
        ScanFiles.RemoveRange(files);
    }

    [RelayCommand]
    private async Task PickFile()
    {
        var filePicker = new FileOpenPicker()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            ViewMode = PickerViewMode.Thumbnail,
        };
        filePicker.FileTypeFilter.Add(".png");
        filePicker.FileTypeFilter.Add(".jpg");
        filePicker.FileTypeFilter.Add(".jpeg");
        filePicker.FileTypeFilter.Add(".tiff");
        filePicker.FileTypeFilter.Add(".bmp");

        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);

        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hWnd);

        var files = await filePicker.PickMultipleFilesAsync();

        if (files is null)
        {
            return;
        }
        foreach (var file in files)
        {
            AddFile(file.Path);
        }
    }
    public string SelectedItemsString => $"{SelectedItems.Count} of {ScanFiles.Count} Items Selected";

    private bool CanChangeFiles => SelectedItems.Count > 0;
    [RelayCommand(CanExecute = nameof(CanChangeFiles))]
    private void RemoveSelectedFiles()
    {
        RemoveFile(SelectedItems);
        UpdateFileIndex();
    }

    public void UpdateFileIndex()
    {
        for (var i = 0; i < ScanFiles.Count; i++)
        {
            ScanFiles[i].Number = i + 1;
        }
    }

    private bool ScanFileAny => ScanFiles.Count > 0;
    [RelayCommand(CanExecute = nameof(ScanFileAny))]
    private async Task SaveFile()
    {
        var savePicker = new FileSavePicker()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = $"Document-{DateTime.Now:MM-dd-yyyy-hh-mm-ss tt}.pdf"
        };
        savePicker.FileTypeChoices.Add("PDF File", [".pdf"]);
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);

        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

        var savePickerResult = await savePicker.PickSaveFileAsync();

        if (savePickerResult is null)
        {
            return;
        }
        ConvertImagesToPdf(ScanFiles.Select(p => p.Path), savePickerResult.Path);
        var textBlock = new TextBlock
        {
            Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 17)
        };
        var hyperlink = new Hyperlink();
        hyperlink.Inlines.Add(new Run { Text = "Click here" });
        hyperlink.Click += (s, e) =>
        {
            OpenFileExplorer(savePickerResult.Path);
        };
        textBlock.Inlines.Add(hyperlink);
        var trailingText = new Run { Text = " to show in folder." };
        textBlock.Inlines.Add(trailingText);
        textBlock.DataContext = this;
        WeakReferenceMessenger.Default.Send(new DialogMessage(InfoBarMessage.Succuss, textBlock, $"{savePickerResult.Name} was saved"));
    }

    public static void ConvertImagesToPdf(IEnumerable<string> imagePaths, string outputPdfPath)
    {
        using var document = new PdfDocument();
        document.Info.Author = Environment.UserName;
        document.Info.Keywords = "PDF, metadata, ScanStack";
        document.Info.Creator = $"{AppInfo.GetVersionDescription()} by Diyari-Ismael";
        foreach (var imagePath in imagePaths)
        {
            var page = document.AddPage();
            using var gfx = XGraphics.FromPdfPage(page);
            using var image = XImage.FromFile(imagePath);
            // Get page size
            var pageWidth = page.Width.Point;
            var pageHeight = page.Height.Point;

            // Get image size
            var imageWidth = image.PixelWidth;
            var imageHeight = image.PixelHeight;

            // Calculate scaling factors
            var widthScale = pageWidth / imageWidth;
            var heightScale = pageHeight / imageHeight;
            var scale = Math.Min(widthScale, heightScale); // Maintain aspect ratio

            // Calculate new image dimensions
            var scaledWidth = imageWidth * scale;
            var scaledHeight = imageHeight * scale;

            // Center the image on the page
            var xPosition = (pageWidth - scaledWidth) / 2;
            var yPosition = (pageHeight - scaledHeight) / 2;

            // Draw the image scaled to fit the page
            gfx.DrawImage(image, xPosition, yPosition, scaledWidth, scaledHeight);
        }

        document.Save(outputPdfPath);
    }
    public static void OpenFileExplorer(string filePath)
    {
        try
        {
            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new DialogMessage(InfoBarMessage.Error, $"An error occurred while trying to open File Explorer: {ex.Message}", $"Error"));
        }
    }
    [RelayCommand(CanExecute = nameof(ScanFileAny))]
    private void Refresh()
    {
        var file = SelectedItems.FirstOrDefault();
        if (file is null)
        {
            return;
        }
        RemoveSelectedFiles();
        InsetFile(file.Path, file.Number - 1);
    }
}
