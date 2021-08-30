using WinUI = Microsoft.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls;
using Scanner.ViewModels;
using System;
using static HelpViewEnums;
using System.Threading.Tasks;
using Scanner.Views.Dialogs;
using Windows.UI.Core;
using static Utilities;

namespace Scanner.Views
{
    public sealed partial class SettingsView : Page
    {
        public SettingsView()
        {
            this.InitializeComponent();
            ViewModel.LogExportDialogRequested += ViewModel_LogExportDialogRequestedAsync;
        }

        private async void ViewModel_LogExportDialogRequestedAsync(object sender, EventArgs e)
        {
            LogExportDialogView dialog = new LogExportDialogView();
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, async () => await dialog.ShowAsync());
        }
    }
}
