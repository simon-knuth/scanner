using System;
using Windows.System;
using Windows.UI.Xaml.Controls;

namespace Scanner.Views.Dialogs
{
    public sealed partial class ChangelogDialogView : ContentDialog
    {
        public ChangelogDialogView()
        {
            this.InitializeComponent();
        }

        private void ContentDialog_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // load actual content later to fix transitions
            FindName("GridContent");
        }
    }
}
