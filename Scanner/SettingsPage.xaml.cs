using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

using static Globals;
using static Utilities;


namespace Scanner
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
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
            ToggleSwitchNotificationScanComplete.IsOn = settingNotificationScanComplete;
            ToggleSwitchUnsupportedFileFormat.IsOn = settingUnsupportedFileFormat;
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            settingAppTheme = (Theme) int.Parse(((ComboBoxItem) ComboBoxTheme.SelectedItem).Tag.ToString());
            settingSearchIndicator = ToggleSwitchSearchIndicator.IsOn;
            settingNotificationScanComplete = ToggleSwitchNotificationScanComplete.IsOn;
            settingUnsupportedFileFormat = ToggleSwitchUnsupportedFileFormat.IsOn;

            SaveSettings();

            Frame.GoBack();
        }

        private void ComboBoxTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TextBlockRestart.Visibility = Visibility.Visible;
        }

        private void HyperlinkRestart_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            ButtonSave_Click(null, null);
            CoreApplication.RequestRestartAsync("");
        }
    }
}
