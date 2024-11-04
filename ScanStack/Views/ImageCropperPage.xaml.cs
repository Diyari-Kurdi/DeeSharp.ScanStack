using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;

namespace ScanStack.Views;

public sealed partial class ImageCropperPage : Page
{
    public ImageCropperPage()
    {
        this.InitializeComponent();
        GoBackButton.Loaded += GoBackButton_Loaded;
    }
    private void GoBackButton_Loaded(object sender, RoutedEventArgs e)
    {
        GoBackButton.Focus(FocusState.Programmatic);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ImageCropperControl.LoadImageFromFile((StorageFile)e.Parameter);
        var imageAnimation = ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation");
        imageAnimation?.TryStart(ImageCropperControl, [ImageCropperControl]);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);
        ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("BackConnectedAnimation", ImageCropperControl);
    }


    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.GoBack();
    }

    private void AcceptButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(ScanPage), ImageCropperControl);
    }
}
