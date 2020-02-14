using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

using static Enums;
using static Globals;
using static Utilities;


namespace Scanner
{
    public sealed partial class SettingsPage : Page
    {
        private string websiteUrl = "https://simon-knuth.github.io/scanner";
        private string privacyPolicyUrl = "https://simon-knuth.github.io/scanner/privacy-policy";
        private StorageFolder newScanFolder = null;

        public SettingsPage()
        {
            this.InitializeComponent();

            // localize hyperlinks
            ((Windows.UI.Xaml.Documents.Run)HyperlinkRestart.Inlines[0]).Text = ResourceLoader.GetForCurrentView().GetString("HyperlinkSettingsRestartHintLink");
            ((Windows.UI.Xaml.Documents.Run)HyperlinkFeedbackHub.Inlines[0]).Text = ResourceLoader.GetForCurrentView().GetString("HyperlinkSettingsFeedbackLink");
            ((Windows.UI.Xaml.Documents.Run)HyperlinkRate.Inlines[0]).Text = ResourceLoader.GetForCurrentView().GetString("HyperlinkSettingsRateLink");
            ((Windows.UI.Xaml.Documents.Run)HyperlinkWebsite.Inlines[0]).Text = ResourceLoader.GetForCurrentView().GetString("HyperlinkSettingsWebsiteLink");
            ((Windows.UI.Xaml.Documents.Run)HyperlinkLicenses.Inlines[0]).Text = ResourceLoader.GetForCurrentView().GetString("HyperlinkSettingsLicensesLink");
            ((Windows.UI.Xaml.Documents.Run)HyperlinkPrivacyPolicy.Inlines[0]).Text = ResourceLoader.GetForCurrentView().GetString("HyperlinkSettingsPrivacyPolicyLink");

            // register event listener
            CoreApplication.GetCurrentView().TitleBar.LayoutMetricsChanged += (titleBar, y) => {
                GridSettingsHeader.Padding = new Thickness(0, titleBar.Height, 0, 0);
            };
        }


        /// <summary>
        ///     The event listener for when <see cref="ButtonCancel"/> is clicked. Closes the settings page.
        /// </summary>
        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }


        /// <summary>
        ///     The event listener for when a button is pressed. Allows to discard changes using the escape key.
        /// </summary>
        private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.GoBack || e.Key == Windows.System.VirtualKey.Escape)
            {
                ButtonCancel_Click(null, null);
            }
        }


        /// <summary>
        ///     The event listener for when the page is loading. Loads all current settings and updates
        ///     the version indicator at the bottom.
        /// </summary>
        private void Page_Loading(FrameworkElement sender, object args)
        {
            if (scanFolder != null)
            {
                TextBlockSaveLocation.Text = scanFolder.Path;
            } else
            {
                ButtonResetLocation_Click(null, null);
            }
            

            switch (settingAppTheme)
            {
                case Theme.light:
                    ComboBoxTheme.SelectedIndex = 1;
                    break;
                case Theme.dark:
                    ComboBoxTheme.SelectedIndex = 2;
                    break;
                default:
                    ComboBoxTheme.SelectedIndex = 0;
                    break;
            }
            TextBlockRestart.Visibility = Visibility.Collapsed;
            CheckBoxAutomaticScannerSelection.IsChecked = settingAutomaticScannerSelection;
            CheckBoxNotificationScanComplete.IsChecked = settingNotificationScanComplete;
            CheckBoxUnsupportedFileFormat.IsChecked = settingUnsupportedFileFormat;
            CheckBoxSettingsDrawPenDetected.IsChecked = settingDrawPenDetected;

            PackageVersion version = Package.Current.Id.Version;
            RunSettingsVersion.Text = String.Format("Version {0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
        }


        /// <summary>
        ///     The event listener for when the <see cref="ButtonSave"/> is clicked. Updates all setting variables and
        ///     then calls for <see cref="SaveSettings"/>.
        /// </summary>
        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            if (newScanFolder != null)
            {
                Windows.Storage.AccessCache.StorageApplicationPermissions.
                    FutureAccessList.AddOrReplace("scanFolder", newScanFolder);
                scanFolder = newScanFolder;
            }

            settingAppTheme = (Theme) int.Parse(((ComboBoxItem) ComboBoxTheme.SelectedItem).Tag.ToString());
            settingAutomaticScannerSelection = (bool) CheckBoxAutomaticScannerSelection.IsChecked;
            settingNotificationScanComplete = (bool) CheckBoxNotificationScanComplete.IsChecked;
            settingUnsupportedFileFormat = (bool) CheckBoxUnsupportedFileFormat.IsChecked;
            settingDrawPenDetected = (bool) CheckBoxSettingsDrawPenDetected.IsChecked;

            SaveSettings();

            Frame.GoBack();
        }


        /// <summary>
        ///     The event listener for when another <see cref="Theme"/> is selected from <see cref="ComboBoxTheme"/>.
        /// </summary>
        private void ComboBoxTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TextBlockRestart.Visibility = Visibility.Visible;
        }


        /// <summary>
        ///     The event listener for when the <see cref="HyperlinkRestart"/>, which saves the settings and restarts
        ///     the app after a theme change, is clicked.
        /// </summary>
        private async void HyperlinkRestart_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            ButtonSave_Click(null, null);
            await CoreApplication.RequestRestartAsync("");
        }


        /// <summary>
        ///     The event listener for when the <see cref="ButtonBrowse"/>, which allows the user to select a new
        ///     <see cref="scanFolder"/>, is clicked.
        /// </summary>
        private async void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            folderPicker.FileTypeFilter.Add("*");

            StorageFolder folder;
            try
            {
                folder = await folderPicker.PickSingleFolderAsync();
            }
            catch (Exception exc)
            {
                ShowContentDialog(LocalizedString("ErrorMessagePickFolderHeader"), LocalizedString("ErrorMessagePickFolderBody") + "\n" + exc.Message);
                return;
            }

            if (folder != null)
            {
                newScanFolder = folder;
                TextBlockSaveLocation.Text = newScanFolder.Path;
            }
        }


        /// <summary>
        ///     The event listener for when the <see cref="CheckBoxUnsupportedFileFormat"/> that changes
        ///     <see cref="settingUnsupportedFileFormat"/> is checked/unchecked.
        /// </summary>
        private void CheckBoxUnsupportedFileFormat_Toggled(object sender, RoutedEventArgs e)
        {
            formatSettingChanged = true;
        }


        /// <summary>
        ///     The event listener for when the <see cref="HyperlinkWebsite"/>, which opens the app's website, is clicked.
        /// </summary>
        private async void HyperlinkWebsite_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(websiteUrl));
        }


        /// <summary>
        ///     The event listener for when the <see cref="HyperlinkWebsite"/>, which opens the app's website, is clicked.
        /// </summary>
        private async void HyperlinkPrivacyPolicy_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(privacyPolicyUrl));
        }


        /// <summary>
        ///     The event listener for when the button that resets the current <see cref="scanFolder"/> to ..\Pictures\Scans
        ///     is clicked.
        /// </summary>
        private async void ButtonResetLocation_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder folder;
            try
            {
                folder = await KnownFolders.PicturesLibrary.CreateFolderAsync("Scans", CreationCollisionOption.OpenIfExists);
            }
            catch (UnauthorizedAccessException)
            {
                ShowContentDialog(LocalizedString("ErrorMessageResetFolderUnauthorizedHeader"), LocalizedString("ErrorMessageResetFolderUnauthorizedBody"));
                return;
            }
            catch (Exception exc)
            {
                ShowContentDialog(LocalizedString("ErrorMessageResetFolderHeader"), LocalizedString("ErrorMessageResetFolderBody") + "\n" + exc.Message);
                return;
            }

            newScanFolder = folder;
            TextBlockSaveLocation.Text = newScanFolder.Path;
        }


        /// <summary>
        ///     The event listener for when <see cref="HyperlinkFeedbackHub"/>, which opens the app's Feedback Hub section, is clicked.
        /// </summary>
        private async void HyperlinkFeedbackHub_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            try
            {
                var launcher = Microsoft.Services.Store.Engagement.StoreServicesFeedbackLauncher.GetDefault();
                await launcher.LaunchAsync();
            }
            catch (Exception exc)
            {
                ShowContentDialog(LocalizedString("ErrorMessageFeedbackHubHeader"), 
                    LocalizedString("ErrorMessageFeedbackHubBody") + "\n" + exc.Message);
            }
        }


        /// <summary>
        ///     Displays a <see cref="ContentDialog"/> consisting of a <paramref name="title"/>, <paramref name="message"/>
        ///     and a button that allows the user to close the <see cref="ContentDialog"/>.
        /// </summary>
        /// <param name="title">The title of the <see cref="ContentDialog"/>.</param>
        /// <param name="message">The body of the <see cref="ContentDialog"/>.</param>
        public async void ShowContentDialog(string title, string message)
        {
            ContentDialogBlank.Title = title;
            ContentDialogBlank.Content = message;

            ContentDialogBlank.CloseButtonText = LocalizedString("CloseButtonText");
            ContentDialogBlank.PrimaryButtonText = "";
            ContentDialogBlank.SecondaryButtonText = "";

            await ContentDialogBlank.ShowAsync();
        }


        /// <summary>
        ///     The event listener for when <see cref="HyperlinkLicenses"/>, which displays <see cref="ContentDialogLicenses"/>, is clicked.
        /// </summary>
        private async void HyperlinkLicenses_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            ContentDialogLicenses.CloseButtonText = LocalizedString("CloseButtonText");
            ContentDialogLicenses.PrimaryButtonText = "";
            ContentDialogLicenses.SecondaryButtonText = "";

            await ContentDialogLicenses.ShowAsync();
        }


        /// <summary>
        ///     The event listener for when a license hyperlink is clicked.
        /// </summary>
        private async void NavigateToLicenseWebsite(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            string url = null;

            if (sender == HyperlinkLicenseUniversalWindowsPlatform) url = "https://github.com/Microsoft/dotnet/blob/master/releases/UWP/LICENSE.TXT";
            else if (sender == HyperlinkLicenseStoreEngagement) url = "https://www.microsoft.com/en-us/legal/intellectualproperty/copyright/default.aspx";
            else if (sender == HyperlinkLicenseUwpNotifications) url = "https://www.microsoft.com/en-us/legal/intellectualproperty/copyright/default.aspx";
            else if (sender == HyperlinkLicenseUwpUiControls) url = "https://github.com/windows-toolkit/WindowsCommunityToolkit/blob/master/license.md";
            else if (sender == HyperlinkLicenseUiXaml) url = "https://www.nuget.org/packages/Microsoft.UI.Xaml/2.2.190917002/license";
            else if (sender == HyperlinkLicenseQueryStringNet) url = "https://raw.githubusercontent.com/WindowsNotifications/QueryString.NET/master/LICENSE";
            else if (sender == HyperlinkLicenseWin2dUwp) url = "https://www.microsoft.com/web/webpi/eula/eula_win2d_10012014.htm";
            else if (sender == HyperlinkLicensePDFsharp) url = "http://www.pdfsharp.net/PDFsharp_License.ashx";

            try { await Windows.System.Launcher.LaunchUriAsync(new Uri(url)); }
            catch (Exception) { }
        }


        /// <summary>
        ///     Page was loaded (possibly through navigation).
        /// </summary>
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ButtonBrowse.Focus(FocusState.Programmatic);
        }
    }
}
