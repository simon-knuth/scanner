using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Resources;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI.Core;
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
        private bool allSettingsLoaded = false;


        public SettingsPage()
        {
            this.InitializeComponent();

            // register event listener
            CoreApplication.GetCurrentView().TitleBar.LayoutMetricsChanged += (titleBar, y) => {
                GridSettingsHeader.Padding = new Thickness(0, titleBar.Height, 0, 0);
            };
        }


        /// <summary>
        ///     The event listener for when a button is pressed. Allows to discard changes using the escape key.
        /// </summary>
        private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.GoBack || e.Key == Windows.System.VirtualKey.Escape)
            {
                GoBack();
            }
        }


        /// <summary>
        ///     The event listener for when the page is loading. Loads all current settings and updates
        ///     the version indicator at the bottom.
        /// </summary>
        private async void Page_Loading(FrameworkElement sender, object args)
        {
            if (scanFolder != null)
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => TextBlockSaveLocation.Text = scanFolder.Path);
            } else
            {
                await ResetScanLocation();
            }

            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, async () =>
            {
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
                CheckBoxAppendTime.IsChecked = settingAppendTime;
                CheckBoxNotificationScanComplete.IsChecked = settingNotificationScanComplete;

                if (await IsDefaultScanFolderSet() != true) ButtonResetLocation.IsEnabled = true;

                allSettingsLoaded = true;

                PackageVersion version = Package.Current.Id.Version;
                RunSettingsVersion.Text = String.Format("Version {0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
            });
        }



        /// <summary>
        ///     The event listener for when another <see cref="Theme"/> is selected from <see cref="ComboBoxTheme"/>.
        /// </summary>
        private async void ComboBoxTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (allSettingsLoaded)
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => TextBlockRestart.Visibility = Visibility.Visible);

                settingAppTheme = (Theme)int.Parse(((ComboBoxItem)ComboBoxTheme.SelectedItem).Tag.ToString());
                SaveSettings();
            }
        }


        /// <summary>
        ///     The event listener for when the <see cref="HyperlinkRestart"/>, which saves the settings and restarts
        ///     the app after a theme change, is clicked.
        /// </summary>
        private async void HyperlinkRestart_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
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
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessagePickFolderHeading"),
                    LocalizedString("ErrorMessagePickFolderBody") + "\n" + exc.Message));
                return;
            }

            if (folder != null && folder.Path != scanFolder.Path)
            {
                Windows.Storage.AccessCache.StorageApplicationPermissions.
                    FutureAccessList.AddOrReplace("scanFolder", folder);
                scanFolder = folder;
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => TextBlockSaveLocation.Text = scanFolder.Path);
            }

            if (await IsDefaultScanFolderSet() != true) await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => ButtonResetLocation.IsEnabled = true);
        }


        /// <summary>
        ///     The event listener for when the button that resets the current <see cref="scanFolder"/> to ..\Pictures\Scans
        ///     is clicked.
        /// </summary>
        private async void ButtonResetLocation_Click(object sender, RoutedEventArgs e)
        {
            await ResetScanLocation();
        }



        private async Task ResetScanLocation()
        {
            try
            {
                await ResetScanFolder();
            }
            catch (UnauthorizedAccessException)
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageResetFolderUnauthorizedHeading"),
                    LocalizedString("ErrorMessageResetFolderUnauthorizedBody")));
                return;
            }
            catch (Exception exc)
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageResetFolderHeading"),
                    LocalizedString("ErrorMessageResetFolderBody") + "\n" + exc.Message));
                return;
            }

            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                TextBlockSaveLocation.Text = scanFolder.Path;
                ButtonResetLocation.IsEnabled = false;
            });    
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
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageFeedbackHubHeading"),
                    LocalizedString("ErrorMessageFeedbackHubBody") + "\n" + exc.Message));
            }
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
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
            {
                StoryboardEnter.Begin();
                ButtonBrowse.Focus(FocusState.Programmatic);

                ScrollViewerSettings.Margin = new Thickness(0, GridSettingsHeader.ActualHeight, 0, 0);
                ScrollViewerSettings.Padding = new Thickness(0, -GridSettingsHeader.ActualHeight, 0, 0);

                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
                {
                    // v1809+
                    TeachingTipEmpty.ActionButtonStyle = RoundedButtonAccentStyle;
                }
                TeachingTipEmpty.CloseButtonContent = LocalizedString("CloseButtonText");
            });
        }



        private async void HyperlinkRate_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            await ShowRatingDialog();
        }



        private void ButtonBack_Click(object sender, RoutedEventArgs e)
        {
            GoBack();
        }



        private void GoBack()
        {
            Frame.GoBack();
        }



        private void SettingCheckboxChanged(object sender, RoutedEventArgs e)
        {
            if (allSettingsLoaded)
            {
                settingAppendTime = (bool)CheckBoxAppendTime.IsChecked;
                settingNotificationScanComplete = (bool)CheckBoxNotificationScanComplete.IsChecked;

                SaveSettings();
            }
        }


        /// <summary>
        ///     The event listener for when <see cref="HyperlinkButtonSettingsAboutLicenses"/>, which displays <see cref="ContentDialogLicenses"/>, is clicked.
        /// </summary>
        private async void HyperlinkButtonSettingsAboutLicenses_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, async () =>
            {
                ContentDialogLicenses.PrimaryButtonText = "";
                ContentDialogLicenses.SecondaryButtonText = "";

                await ContentDialogLicenses.ShowAsync();
            });
        }


        private async void HyperlinkButtonSettingsHelpScannerSettings_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:printers"));
        }


        private async void HyperlinkButtonSettingsTranslationsContributors_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, async () => await ContentDialogTranslationsContributors.ShowAsync());
        }

        private async void HyperlinkButtonSettingsAboutCredits_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, async () => await ContentDialogAboutCredits.ShowAsync());
        }
    }
}
