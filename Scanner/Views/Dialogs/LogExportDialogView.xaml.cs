using System;
using Windows.System;
using Windows.UI.Xaml.Controls;

namespace Scanner.Views.Dialogs
{
    public sealed partial class LogExportDialogView : ContentDialog
    {
        public LogExportDialogView()
        {
            this.InitializeComponent();
        }

        private void Button_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ((Button)sender).Command = ViewModel.LogExportCommand;
        }
    }
}
