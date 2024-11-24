using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using ScanStack.Core.Models;
using ScanStack.ViewModels;
using Windows.Foundation.Metadata;
using Windows.Storage;
using BitmapFileFormat = CommunityToolkit.WinUI.Controls.BitmapFileFormat;


namespace ScanStack.Views;

public sealed partial class ScanPage : Page, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private readonly RelayCommand _cropCommand;
    public IRelayCommand CropCommand => _cropCommand;
    private bool IsImageOpened { get; set; } = false;
    private bool IsRuntime = true;
    public ScanViewModel ViewModel
    {
        get;
    }
    private FileModel? _clickedItem;
    private FileModel? ClickedItem
    {
        get => _clickedItem;
        set
        {
            _clickedItem = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ClickedItem)));
        }
    }
    public ScanPage()
    {
        _cropCommand = new RelayCommand(Crop, CanCrop);
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Enabled;
        ViewModel = App.GetService<ScanViewModel>();
        ViewModel.SelectAllItems += ContentGridView.SelectAll;
        ViewModel.DeselectAllItems += () =>
        {
            ContentGridView.DeselectRange(new Microsoft.UI.Xaml.Data.ItemIndexRange(0, (uint)ContentGridView.SelectedItems.Count));
        };
        CropCommand.CanExecute(false);
    }
    private bool CanCrop() => !IsImageOpened && ViewModel.ScanFileManager.SelectedItems.Count == 1;

    private void ContentGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is GridView gridView)
        {
            var selectedItems = gridView.SelectedItems.OfType<FileModel>().ToList();
            ViewModel.ScanFileManager.SelectedItems = selectedItems;
            CropCommand.NotifyCanExecuteChanged();
        }
    }

    private void ContentGridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        ViewModel.ScanFileManager.UpdateFileIndex();
    }

    private void PreviewCard_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (ClickedItem is null)
        {
            return;
        }
        RotateLeftButton.IsEnabled = false;
        RotateRightButton.IsEnabled = false;
        RemoveFileButton.IsEnabled = false;
        PickFileButton.IsEnabled = false;
        IsImageOpened = true;
        CropCommand.NotifyCanExecuteChanged();
        var animation = ContentGridView.PrepareConnectedAnimation("ForwardConnectedAnimation", ClickedItem, "ImageCtrl");
        SmokeGrid.Visibility = Visibility.Visible;
        animation.TryStart(destinationElement);
    }
    private void ContentGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (ContentGridView.ContainerFromItem(e.ClickedItem) is GridViewItem container)
        {
            ClickedItem = (FileModel)container.Content;
        }
    }
    private async void ContentGridView_Loaded(object sender, RoutedEventArgs e)
    {
        if (ClickedItem != null)
        {
            ContentGridView.ScrollIntoView(ClickedItem, ScrollIntoViewAlignment.Default);
            ContentGridView.UpdateLayout();
            var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("BackConnectedAnimation");
            if (animation != null)
            {
                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
                {
                    animation.Configuration = new DirectConnectedAnimationConfiguration();
                }

                await ContentGridView.TryStartConnectedAnimationAsync(animation, ClickedItem, "ImageCtrl");
            }
            ContentGridView.Focus(FocusState.Programmatic);
        }
        RotateLeftButton.IsEnabled = true;
        RotateRightButton.IsEnabled = true;
        RemoveFileButton.IsEnabled = true;
        PickFileButton.IsEnabled = true;
        IsImageOpened = false;
        CropCommand.NotifyCanExecuteChanged();
    }

    private void Animation_Completed(ConnectedAnimation sender, object args)
    {
        SmokeGrid.Visibility = Visibility.Collapsed;
        SmokeGrid.Children.Add(destinationElement);
    }

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        var animation = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("BackConnectedAnimation", destinationElement);
        SmokeGrid.Children.Remove(destinationElement);
        animation.Completed += Animation_Completed;
        ContentGridView.ScrollIntoView(ClickedItem, ScrollIntoViewAlignment.Default);
        ContentGridView.UpdateLayout();
        if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
        {
            animation.Configuration = new DirectConnectedAnimationConfiguration();
        }
        await ContentGridView.TryStartConnectedAnimationAsync(animation, ClickedItem, "ImageCtrl");
        IsImageOpened = false;
        RotateLeftButton.IsEnabled = true;
        RotateRightButton.IsEnabled = true;
        RemoveFileButton.IsEnabled = true;
        PickFileButton.IsEnabled = true;
        CropCommand.NotifyCanExecuteChanged();
    }

    private Action Crop => StartCropAnimation;

    private async void StartCropAnimation()
    {
        if (ClickedItem is null)
        {
            return;
        }

        var imageFile = await StorageFile.GetFileFromPathAsync(ClickedItem.Path);

        ContentGridView.PrepareConnectedAnimation("ForwardConnectedAnimation", ClickedItem, "ImageCtrl");
        Frame.Navigate(typeof(ImageCropperPage), imageFile, new SuppressNavigationTransitionInfo());
    }

    protected async override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ClickedItem is null)
        {
            return;
        }
        if (e.Parameter is ImageCropper imageCropper)
        {
            var pictures = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
            var savePath = Path.Combine(pictures.SaveFolder.Path, "ScanStack");
            if (!Path.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            var saveFolder = await StorageFolder.GetFolderFromPathAsync(savePath);
            var fileFormat = BitmapFileFormat.Png;
            switch (Path.GetExtension(ClickedItem.Path))
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
            var outputFile = await saveFolder.CreateFileAsync($"CopyOf_{Path.GetFileName(ClickedItem.Path)}", CreationCollisionOption.ReplaceExisting);
            using var outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
            await imageCropper.SaveAsync(outputStream, fileFormat);

            ViewModel.ScanFileManager.ImageEditor_FileChanged(ClickedItem, outputFile.Path);
        }

    }

    private void RefreshFilesButton_Click(object sender, RoutedEventArgs e)
    {
        ContentGridView.InitializeViewChange();
    }

    private void ImageCtrl_Loaded(object sender, RoutedEventArgs e)
    {
        if (IsRuntime)
        {
            IsRuntime = false;
            ViewModel.ScanSettings.Scale = 100;
        }
    }
}
