using Windows.UI.Xaml.Controls;
using System;
using Scanner.Views.Dialogs;
using Windows.UI.Core;
using static Utilities;
using Windows.Globalization;
using Windows.UI.Xaml.Controls.Primitives;

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

        private void PrepareMenuFlyoutAutoRotateLanguages()
        {
            string desiredLanguage = ViewModel.SettingAutoRotateLanguage;

            MenuFlyoutSettingAutoRotateLanguage.Items.Clear();
            for (int i = 0; i < ViewModel.AutoRotatorService.AvailableLanguages.Count; i++)
            {
                Language language = ViewModel.AutoRotatorService.AvailableLanguages[i];

                var item = new ToggleMenuFlyoutItem
                {
                    Text = language.DisplayName,
                    Command = ViewModel.SetAutoRotateLanguageCommand,
                    CommandParameter = i.ToString(),
                    IsChecked = language.LanguageTag == desiredLanguage
                };

                if (language.LanguageTag == ViewModel.AutoRotatorService.DefaultLanguage.LanguageTag)
                {
                    item.FontWeight = Windows.UI.Text.FontWeights.SemiBold;
                }

                MenuFlyoutSettingAutoRotateLanguage.Items.Add(item);
            }
            MenuFlyoutSettingAutoRotateLanguage.Items.Add(new MenuFlyoutSeparator());
            MenuFlyoutSettingAutoRotateLanguage.Items.Add(MenuFlyoutItemAddLanguage);
        }

        private async void HyperlinkButtonSettingAutoRotateLanguage_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                PrepareMenuFlyoutAutoRotateLanguages();
                FlyoutBase.ShowAttachedFlyout((Windows.UI.Xaml.FrameworkElement)sender);
            });
        }
    }
}
