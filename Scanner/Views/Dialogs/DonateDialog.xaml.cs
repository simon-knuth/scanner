using System;
using Windows.System;
using Windows.UI.Xaml.Controls;

namespace Scanner.Views.Dialogs
{
    public sealed partial class DonateDialog : ContentDialog
    {
        public DonateDialog()
        {
            this.InitializeComponent();
        }

        private async void DonateDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            await Launcher.LaunchUriAsync(new Uri("https://www.paypal.com/donate?hosted_button_id=TLR5GM8NKE3L2&amp;source=url"));
        }

        private void ContentDialog_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // load actual content later to fix transitions
            FindName("GridContent");
        }
    }
}
