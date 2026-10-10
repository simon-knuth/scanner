using System;
using Windows.System;
using Windows.UI.Xaml.Controls;


namespace Scanner.Views.Dialogs
{
    public sealed partial class AppPreviewDialogView : ContentDialog
    {
        public AppPreviewDialogView()
        {
            this.InitializeComponent();
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            await Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?productid=9PP7H98HPG40&cid=in-app-preview-dialog"));
        }
    }
}
