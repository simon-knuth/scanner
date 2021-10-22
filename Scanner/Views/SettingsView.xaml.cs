using Windows.UI.Xaml.Controls;
using System;
using Scanner.Views.Dialogs;
using Windows.UI.Core;
using static Utilities;

namespace Scanner.Views
{
    public sealed partial class SettingsView : Page
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SettingsView()
        {
            this.InitializeComponent();
            ViewModel.LogExportDialogRequested += ViewModel_LogExportDialogRequestedAsync;
            ViewModel.LicensesDialogRequested += ViewModel_LicensesDialogRequested;
            ViewModel.ChangelogRequested += ViewModel_ChangelogRequested;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private async void ViewModel_LicensesDialogRequested(object sender, EventArgs e)
        {
            LicensesDialogView dialog = new LicensesDialogView();
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, async () => await dialog.ShowAsync());
        }

        private async void ViewModel_LogExportDialogRequestedAsync(object sender, EventArgs e)
        {
            LogExportDialogView dialog = new LogExportDialogView();
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, async () => await dialog.ShowAsync());
        }

        private async void ViewModel_ChangelogRequested(object sender, EventArgs e)
        {
            ChangelogDialogView dialog = new ChangelogDialogView();
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, async () =>
            {
                ContentDialogResult result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    ViewModel.ShowDonateDialogCommand.Execute(null);
                }
            });
        }
    }
}
