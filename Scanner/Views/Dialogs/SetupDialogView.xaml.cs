using System;
using Windows.System;
using Windows.UI.Xaml.Controls;

namespace Scanner.Views.Dialogs
{
    public sealed partial class SetupDialogView : ContentDialog
    {
        public SetupDialogView()
        {
            this.InitializeComponent();
        }

        private void ContentDialog_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // load actual content later to fix transitions
            FindName("GridContent");
        }

        private void ContentDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (args.Result != ContentDialogResult.Primary)
            {
                args.Cancel = true;
            }
            else
            {
                ViewModel.ConfirmSettingsCommand.Execute(null);
            }
        }
    }
}
