using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media.Imaging;
using ScanStack.Core.Models;
using ScanStack.Helpers;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ScanStack.Services;
public partial class ImageEditor : ObservableObject
{
    public delegate void FileModelEventHandler(FileModel oldFile, string newFilePath);
    public event FileModelEventHandler? FileChanged;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RotateImageCommand))]
    private FileModel? _selectedFile;

    private bool CanExecute => SelectedFile != null;
    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task RotateImage(object angleParam)
    {
        var selectedFile = SelectedFile!;
        var angle = float.Parse(angleParam.ToString()!);
        var pictures = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
        var savePath = Path.Combine(pictures.SaveFolder.Path, "ScanStack");
        if (!Path.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }

        var saveFolder = await StorageFolder.GetFolderFromPathAsync(savePath);
        var inputFile = await StorageFile.GetFileFromPathAsync(selectedFile.Path);
        var fileFormat = BitmapFileFormat.Png;
        switch (Path.GetExtension(selectedFile.Path))
        {
            case ".jpg" or ".jpeg":
                fileFormat = BitmapFileFormat.Jpeg;
                break;
            case ".png":
                fileFormat = BitmapFileFormat.Png;
                break;
            case ".tiff":
                fileFormat = BitmapFileFormat.Tiff;
                break;
            case ".bmp":
                fileFormat = BitmapFileFormat.Bmp;
                break;
            default:
                break;
        }
        var outputFile = await saveFolder.CreateFileAsync($"CopyOf_{Path.GetFileName(selectedFile.Path)}", CreationCollisionOption.ReplaceExisting);
        using var inputStream = await inputFile.OpenAsync(FileAccessMode.Read);
        using var outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
        await RotateImageAsync(inputStream, outputStream, angle, fileFormat);
        FileChanged?.Invoke(selectedFile, outputFile.Path);
    }

    public static async Task RotateImageAsync(IRandomAccessStream inputStream, IRandomAccessStream outputStream, float rotationAngle, BitmapFileFormat bitmapFileFormat)
    {
        var device = CanvasDevice.GetSharedDevice();

        using var sourceBitmap = await CanvasBitmap.LoadAsync(device, inputStream);
        var sourceWidth = (float)sourceBitmap.Size.Width;
        var sourceHeight = (float)sourceBitmap.Size.Height;

        float targetWidth, targetHeight;

        if (rotationAngle == 90 || rotationAngle == 270)
        {
            targetWidth = sourceHeight;
            targetHeight = sourceWidth;
        }
        else
        {
            targetWidth = sourceWidth;
            targetHeight = sourceHeight;
        }

        var center = new Vector2(targetWidth / 2, targetHeight / 2);

        using var renderTarget = new CanvasRenderTarget(device, targetWidth, targetHeight, sourceBitmap.Dpi);
        using (var session = renderTarget.CreateDrawingSession())
        {
            session.Clear(Colors.Transparent);
            session.Transform = Matrix3x2.CreateTranslation((targetWidth - sourceWidth) / 2, (targetHeight - sourceHeight) / 2) *
                                Matrix3x2.CreateRotation(rotationAngle * (float)Math.PI / 180, center);
            session.DrawImage(sourceBitmap, new Rect(0, 0, sourceWidth, sourceHeight));
        }
        var pixelBytes = renderTarget.GetPixelBytes();
        var bitmapEncoder = await BitmapEncoder.CreateAsync(GetEncoderId(bitmapFileFormat), outputStream);
        bitmapEncoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)targetWidth, (uint)targetHeight, sourceBitmap.Dpi, sourceBitmap.Dpi, pixelBytes);
        await bitmapEncoder.FlushAsync();
    }
    public static async Task SaveCroppedWriteableBitmapAsync(WriteableBitmap croppedBitmap, StorageFile file, BitmapFileFormat bitmapFileFormat)
    {
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        // Create a BitmapEncoder for encoding the image
        var encoder = await BitmapEncoder.CreateAsync(GetEncoderId(bitmapFileFormat), stream);

        // Get pixel data from WriteableBitmap
        using var pixelStream = croppedBitmap.PixelBuffer.AsStream();
        var pixels = new byte[pixelStream.Length];
        await pixelStream.ReadAsync(pixels, 0, pixels.Length);

        // Set the pixel data to the encoder
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)croppedBitmap.PixelWidth,
            (uint)croppedBitmap.PixelHeight,
            96, 96,
            pixels);

        // Flush the encoder to save the image
        await encoder.FlushAsync();
    }
    private static Guid GetEncoderId(BitmapFileFormat bitmapFileFormat)
    {
        switch (bitmapFileFormat)
        {
            case BitmapFileFormat.Bmp:
                return BitmapEncoder.BmpEncoderId;
            case BitmapFileFormat.Png:
                return BitmapEncoder.PngEncoderId;
            case BitmapFileFormat.Jpeg:
                return BitmapEncoder.JpegEncoderId;
            case BitmapFileFormat.Tiff:
                return BitmapEncoder.TiffEncoderId;
            case BitmapFileFormat.JpegXR:
                return BitmapEncoder.JpegXREncoderId;
            default:
                break;
        }

        return BitmapEncoder.PngEncoderId;
    }
}
