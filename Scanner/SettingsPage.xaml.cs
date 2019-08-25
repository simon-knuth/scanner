using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

using static Globals;
using static Utilities;


namespace Scanner
{
    public sealed partial class SettingsPage : Page
    {
        private string websiteUrl = "http://simon-knuth.github.io/scanner";
        private StorageFolder newScanFolder = null;

        public SettingsPage()
        {
            this.InitializeComponent();

            ((Windows.UI.Xaml.Documents.Run)HyperlinkRestart.Inlines[0]).Text = ResourceLoader.GetForCurrentView().GetString("HyperlinkSettingsRestartHintLink");
            ((Windows.UI.Xaml.Documents.Run)HyperlinkFeedbackHub.Inlines[0]).Text = ResourceLoader.GetForCurrentView().GetString("HyperlinkSettingsFeedbackLink");
            ((Windows.UI.Xaml.Documents.Run)HyperlinkRate.Inlines[0]).Text = ResourceLoader.GetForCurrentView().GetString("HyperlinkSettingsRateLink");
        }


        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }


        private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.GoBack || e.Key == Windows.System.VirtualKey.Escape)
            {
                ButtonCancel_Click(null, null);
            }
        }

        private void Page_Loading(FrameworkElement sender, object args)
        {
            if (scanFolder != null)
            {
                TextBlockSaveLocation.Text = scanFolder.Path;
            } else
            {
                // TODO do something if no scan folder has been selected due to an error
            }
            

            switch (settingAppTheme)
            {
                case Theme.system:
                    ComboBoxTheme.SelectedIndex = 0;
                    break;
                case Theme.light:
                    ComboBoxTheme.SelectedIndex = 1;
                    break;
                case Theme.dark:
                    ComboBoxTheme.SelectedIndex = 2;
                    break;
                default:
                    // TODO do something
                    break;
            }
            TextBlockRestart.Visibility = Visibility.Collapsed;
            ToggleSwitchSearchIndicator.IsOn = settingSearchIndicator;
            ToggleSwitchAutomaticScannerSelection.IsOn = settingAutomaticScannerSelection;
            ToggleSwitchNotificationScanComplete.IsOn = settingNotificationScanComplete;
            ToggleSwitchUnsupportedFileFormat.IsOn = settingUnsupportedFileFormat;

            PackageVersion version = Package.Current.Id.Version;
            TextBlockVersion.Text = String.Format("Version {0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            if (newScanFolder != null)
            {
                Windows.Storage.AccessCache.StorageApplicationPermissions.
                    FutureAccessList.AddOrReplace("scanFolder", newScanFolder);
                scanFolder = newScanFolder;
            }

            

            settingAppTheme = (Theme) int.Parse(((ComboBoxItem) ComboBoxTheme.SelectedItem).Tag.ToString());
            settingSearchIndicator = ToggleSwitchSearchIndicator.IsOn;
            settingAutomaticScannerSelection = ToggleSwitchAutomaticScannerSelection.IsOn;
            settingNotificationScanComplete = ToggleSwitchNotificationScanComplete.IsOn;
            settingUnsupportedFileFormat = ToggleSwitchUnsupportedFileFormat.IsOn;

            SaveSettings();

            Frame.GoBack();
        }

        private void ComboBoxTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TextBlockRestart.Visibility = Visibility.Visible;
        }

        private async void HyperlinkRestart_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            ButtonSave_Click(null, null);
            await CoreApplication.RequestRestartAsync("");
        }

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
                ShowMessageDialog("Error", "Something went wrong while picking a folder. The error message is:" + "\n" + exc.Message);
                return;
            }

            if (folder != null)
            {
                newScanFolder = folder;
                TextBlockSaveLocation.Text = newScanFolder.Path;
            }
        }

        private void ToggleSwitchUnsupportedFileFormat_Toggled(object sender, RoutedEventArgs e)
        {
            formatSettingChanged = true;
        }

        private async void HyperlinkWebsite_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(websiteUrl));
        }

        private async void ButtonResetLocation_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder folder;
            try
            {
                folder = await KnownFolders.PicturesLibrary.CreateFolderAsync("Scans", CreationCollisionOption.OpenIfExists);
            }
            catch (UnauthorizedAccessException)
            {
                ShowMessageDialog("Access denied", "Access to the pictures library has been denied.");
                return;
            }
            catch (Exception exc)
            {
                ShowMessageDialog("Something went wrong", "Resetting the folder location failed. The error message is:" + "\n" + exc.Message);
                return;
            }

            newScanFolder = folder;
            TextBlockSaveLocation.Text = newScanFolder.Path;
        }

        private async void HyperlinkFeedbackHub_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            try
            {
                var launcher = Microsoft.Services.Store.Engagement.StoreServicesFeedbackLauncher.GetDefault();
                await launcher.LaunchAsync();
            }
            catch (Exception exc)
            {
                ShowMessageDialog(LocalizedString("ErrorMessageFeedbackHubHeader"), 
                    LocalizedString("ErrorMessageFeedbackHubBody") + "\n" + exc.Message);
            }
            
        }
    }
}
