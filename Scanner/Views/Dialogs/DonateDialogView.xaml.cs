using System;
using Windows.System;
using Windows.UI.Xaml.Controls;

using static Scanner.Helpers.AppConstants;

namespace Scanner.Views.Dialogs
{
    public sealed partial class DonateDialogView : ContentDialog
    {
        public DonateDialogView()
        {
            this.InitializeComponent();
        }

        private async void DonateDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            await Launcher.LaunchUriAsync(UriDonation);
        }

        private void ContentDialog_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // load actual content later to fix transitions
            FindName("GridContent");
        }
    }
}
