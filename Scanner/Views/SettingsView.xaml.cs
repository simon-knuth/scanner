using WinUI = Microsoft.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls;
using System;
using Scanner.Views.Dialogs;
using Windows.UI.Core;
using static Utilities;
using Windows.Globalization;
using Windows.UI.Xaml.Controls.Primitives;
using static Enums;
using System.Threading.Tasks;

namespace Scanner.Views
{
    public sealed partial class SettingsView : Page
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private TaskCompletionSource<bool> PageLoaded = new TaskCompletionSource<bool>();


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SettingsView()
        {
            this.InitializeComponent();
            ViewModel.LogExportDialogRequested += ViewModel_LogExportDialogRequestedAsync;
            ViewModel.LicensesDialogRequested += ViewModel_LicensesDialogRequested;
            ViewModel.ChangelogRequested += ViewModel_ChangelogRequested;
            ViewModel.SettingsSectionRequested += ViewModel_SettingsSectionRequested;
            ViewModel.CustomFileNamingDialogRequested += ViewModel_CustomFileNamingDialogRequested;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void Page_Unloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.LogExportDialogRequested -= ViewModel_LogExportDialogRequestedAsync;
            ViewModel.LicensesDialogRequested -= ViewModel_LicensesDialogRequested;
            ViewModel.ChangelogRequested -= ViewModel_ChangelogRequested;
            ViewModel.SettingsSectionRequested -= ViewModel_SettingsSectionRequested;
            ViewModel.CustomFileNamingDialogRequested -= ViewModel_CustomFileNamingDialogRequested;
        }

        private async void ViewModel_SettingsSectionRequested(object sender, SettingsSection section)
        {
            WinUI.Expander requestedExpander = ConvertSettingsSection(section);
            if (requestedExpander != null)
            {
                requestedExpander.IsExpanded = true;
                PageLoaded = new TaskCompletionSource<bool>();
                await PageLoaded.Task;
                requestedExpander.StartBringIntoView();
            }
        }

        private async void ViewModel_LicensesDialogRequested(object sender, EventArgs e)
        {
            LicensesDialogView dialog = new LicensesDialogView();
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, async () => await dialog.ShowAsync());
        }

        private async void ViewModel_CustomFileNamingDialogRequested(object sender, EventArgs e)
        {
            CustomFileNamingDialogView dialog = new CustomFileNamingDialogView();
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

                // highlight default/best language
                if ((ViewModel.AutoRotatorService.DefaultLanguage != null
                        && language.LanguageTag == ViewModel.AutoRotatorService.DefaultLanguage.LanguageTag)
                    || (ViewModel.AutoRotatorService.DefaultLanguage == null
                        && i == 0))
                {
                    item.FontWeight = Windows.UI.Text.FontWeights.SemiBold;
                }

                MenuFlyoutSettingAutoRotateLanguage.Items.Add(item);
            }

            if (ViewModel.AutoRotatorService.AvailableLanguages.Count > 0)
            {
                MenuFlyoutSettingAutoRotateLanguage.Items.Add(new MenuFlyoutSeparator());
            }


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

        /// <summary>
        ///     Maps a <see cref="SettingsSection"/> to the corresponding
        ///     <see cref="WinUI.Expander"/>.
        /// </summary>
        public WinUI.Expander ConvertSettingsSection(SettingsSection section)
        {
            switch (section)
            {
                case SettingsSection.SaveLocation:
                    return ExpanderSaveLocation;
                case SettingsSection.AutoRotation:
                    return ExpanderAutoRotate;
                case SettingsSection.FileNaming:
                    return ExpanderFileName;
                case SettingsSection.ScanOptions:
                    return ExpanderScanOptions;
                case SettingsSection.ScanAction:
                    return ExpanderScanAction;
                case SettingsSection.Theme:
                    return ExpanderTheme;
                case SettingsSection.EditorOrientation:
                    return ExpanderEditorOrientation;
                case SettingsSection.Animations:
                    return ExpanderAnimations;
                case SettingsSection.ErrorReports:
                    return ExpanderFeedbackReportsLogs;
                case SettingsSection.Surveys:
                    return ExpanderFeedbackSurveys;
                case SettingsSection.MeasurementUnits:
                    return ExpanderMeasurementUnits;
                default:
                    break;
            }
            return null;
        }
    }
}
