using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Scanner
{
    public sealed partial class MainPage : Page
    {

        private DeviceWatcher scannerWatcher;
        private double ColumnLeftDefaultMaxWidth;
        private ObservableCollection<ComboBoxItem> scannerList = new ObservableCollection<ComboBoxItem>();
        private List<DeviceInformation> deviceInformations = new List<DeviceInformation>();
        private ImageScanner selectedScanner = null;
        private StorageFolder scanFolder;


        public MainPage()
        {
            this.InitializeComponent();

            // initialize veriables
            ColumnLeftDefaultMaxWidth = ColumnLeft.MaxWidth;

            // populate the scanner list
            refreshScannerList();

            // get ScanFolder
            getScanFolder();

            CoreApplication.MainView.TitleBar.LayoutMetricsChanged += correct_titlebar;
        }

        private async void getScanFolder()
        {
            scanFolder = await KnownFolders.PicturesLibrary.GetFolderAsync("Scans");
        }

        public void refreshScannerList()
        {
            Debug.WriteLine("refreshing scanner list");
            ProgressRingRefresh.IsActive = true;
            // Create a Device Watcher class for type Image Scanner for enumerating scanners
            scannerWatcher = DeviceInformation.CreateWatcher(DeviceClass.ImageScanner);

            scannerWatcher.Added += OnScannerAdded;
            scannerWatcher.Removed += OnScannerRemoved;
            scannerWatcher.EnumerationCompleted += OnScannerEnumerationComplete;

            scannerWatcher.Start();
        }

        private async void OnScannerAdded(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            Debug.WriteLine("OnScannerAdded() with " + deviceInfo.ToString());

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
                {
                    bool duplicate = false;

                    foreach (var check in scannerList)
                    {
                        if (check.Tag.ToString() == deviceInfo.Id)
                        {
                            duplicate = true;
                            break;
                        }
                    }

                    if (!duplicate)
                    {
                        ComboBoxItem item = new ComboBoxItem();

                        if (deviceInfo.IsDefault)
                        {
                            item.Content = deviceInfo.Name + " (default)";
                        } else
                        {
                            item.Content = deviceInfo.Name;
                        }
                        item.Tag = deviceInfo.Id;

                        deviceInformations.Add(deviceInfo);
                        scannerList.Add(item);
                    }

                    if (!ComboBoxScanners.IsDropDownOpen && selectedScanner == null && deviceInformations.Count == 1)
                    {
                        // automatically select first detected scanner if the ComboBox dropdown isn't open
                        ComboBoxScanners.SelectedIndex = 0;
                    }

                    TextBlockRefreshHint.Text = " (" + scannerList.Count.ToString() + " found)";
                }
            );
        }

        private async void OnScannerRemoved(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            Debug.WriteLine("Lost scanner " + deviceInfoUpdate.Id + ", remove it from the scanner list");

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                // find lost scanner in scanner to remove the corresponding scanner and its list entry
                foreach (var item in scannerList)
                {
                    if (item.Tag.ToString() == deviceInfoUpdate.Id)
                    {
                        Debug.WriteLine("Attempt to remove scanner " + item.Content.ToString() + " from list index " + scannerList.IndexOf(item));
                        ComboBoxScanners.IsDropDownOpen = false;
                        if (item.IsSelected)
                        {
                            no_scanner_selected();
                            ComboBoxScanners.SelectedIndex = -1;
                            selectedScanner = null;
                        }

                        scannerList.Remove(item);

                        foreach (DeviceInformation check in deviceInformations)
                        {
                            if (check.Id == deviceInfoUpdate.Id)
                            {
                                deviceInformations.Remove(check);
                                break;
                            }
                        }
                        break;
                    }
                }

                TextBlockRefreshHint.Text = " (" + scannerList.Count.ToString() + " found)";
            });
        }
            

        private async void OnScannerEnumerationComplete(DeviceWatcher sender, Object theObject)
        {
            Debug.WriteLine("OnScannerEnumerationComplete()");
            
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
                {
                    TextBlockRefreshHint.Visibility = Visibility.Visible;
                    UI_enabled(true, false, false, false, false, false, false, false, false, false, true, true);
                    StackPanelTextRight.Opacity = 1.0;
                    HyperlinkSettings.IsTabStop = true;
                }
            );
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // responsive behavior
            if (((Page) sender).ActualWidth < 900)
            {
                StackPanelTextRight.Opacity = 0.0;
                HyperlinkSettings.IsTabStop = false;
                if (((Page) sender).ActualWidth < 700)
                {
                    ColumnLeft.MaxWidth = Double.PositiveInfinity;
                    DropShadowPanelRight.Visibility = Visibility.Collapsed;
                    LeftFrame.Visibility = Visibility.Collapsed;
                    ColumnRight.MaxWidth = 0;
                    TitleBarFrame.Visibility = Visibility.Collapsed;
                } else
                {
                    ColumnRight.MaxWidth = Double.PositiveInfinity;
                    ColumnLeft.MaxWidth = ColumnLeftDefaultMaxWidth;
                    TitleBarFrame.Visibility = Visibility.Visible;
                    DropShadowPanelRight.Visibility = Visibility.Visible;
                    LeftFrame.Visibility = Visibility.Visible;
                }
            } else 
            {
                if (selectedScanner == null)
                {
                    StackPanelTextRight.Opacity = 1.0;
                    HyperlinkSettings.IsTabStop = true;
                }
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder folder = await KnownFolders.PicturesLibrary.GetFolderAsync("Scans");
            await Launcher.LaunchFolderAsync(folder);
        }

        private async void UI_enabled(bool comboBoxScannerSource, bool radioButtonRadioButtonSourceAutomatic, bool radioButtonSourceFlatbed,
            bool radioButtonSourceFeeder, bool radioButtonColorModeColor, bool radioButtonColorModeGrayscale, bool radioButtonColorModeMonochrome,
            bool comboBoxResolution, bool comboBoxType, bool buttonScan, bool buttonSettings, bool buttonRecents)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                ComboBoxScanners.IsEnabled = comboBoxScannerSource;
                RadioButtonSourceAutomatic.IsEnabled = radioButtonRadioButtonSourceAutomatic;
                RadioButtonSourceFlatbed.IsEnabled = radioButtonSourceFlatbed;
                RadioButtonSourceFeeder.IsEnabled = radioButtonSourceFeeder;
                RadioButtonColorModeColor.IsEnabled = radioButtonColorModeColor;
                RadioButtonColorModeGrayscale.IsEnabled = radioButtonColorModeGrayscale;
                RadioButtonColorModeMonochrome.IsEnabled = radioButtonColorModeMonochrome;
                ComboBoxResolution.IsEnabled = comboBoxResolution;
                ComboBoxType.IsEnabled = comboBoxType;
                ButtonScan.IsEnabled = buttonScan;
                ButtonSettings.IsEnabled = buttonSettings;
                ButtonRecents.IsEnabled = buttonRecents;
            }
            );
            Debug.WriteLine("UI_enabled done, bool radioButtonRadioButtonSourceAutomatic was " + radioButtonRadioButtonSourceAutomatic.ToString());
        }

        private void no_scanner_selected()
        {
            UI_enabled(true, false, false, false, false, false, false, false, false, false, true, true);

            StackPanelTextRight.Opacity = 1.0;
            HyperlinkSettings.IsTabStop = true;
        }

        private async void scanner_selected()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            async () =>
            {
                foreach (DeviceInformation check in deviceInformations)
                {
                    if (check.Id == ((ComboBoxItem) ComboBoxScanners.SelectedItem).Tag.ToString())
                    {
                        selectedScanner = await ImageScanner.FromIdAsync(check.Id);
                        break;
                    }
                }

                bool autoAllowed = selectedScanner.IsScanSourceSupported(ImageScannerScanSource.AutoConfigured);
                bool flatbedAllowed = selectedScanner.IsScanSourceSupported(ImageScannerScanSource.Flatbed);
                bool feederAllowed = selectedScanner.IsScanSourceSupported(ImageScannerScanSource.Feeder);

                UI_enabled(true, autoAllowed, flatbedAllowed, feederAllowed, false, false, false, true, true, true, true, true);

                if (autoAllowed) RadioButtonSourceAutomatic.IsChecked = true;
                else if (flatbedAllowed) RadioButtonSourceFlatbed.IsChecked = true;
                else if (feederAllowed) RadioButtonSourceFeeder.IsChecked = true;

                StackPanelTextRight.Opacity = 0.0;
                HyperlinkSettings.IsTabStop = false;
            });
        }

        private async void ComboBoxScanners_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ComboBox) sender).SelectedIndex == -1)
            {
                no_scanner_selected();
            } else
            {
                string scannerID = ((ComboBoxItem)ComboBoxScanners.SelectedItem).Tag.ToString();
                selectedScanner = await ImageScanner.FromIdAsync(scannerID);
                scanner_selected();
            }
        }

        private void correct_titlebar(CoreApplicationViewTitleBar coreApplicationViewTitleBar, object theObject)
        {
            GridPanelRight.Margin = new Thickness(32, CoreApplication.MainView.TitleBar.Height, 0, 0);
        }

        private async void ButtonScan_Click(object sender, RoutedEventArgs e)
        {
            // lock entire left panel
            UI_enabled(false, false, false, false, false, false, false, false, false, false, false, false);

            // check folder and attempt to create it if necessary
            // TODO

            // start scan and show progress and cancel button
            var cancellationToken = new CancellationTokenSource();
            var progress = new Progress<UInt32>(scanProgress);

            ImageScannerScanResult result = null;
            if (RadioButtonSourceAutomatic.IsChecked.Value)
            {
                result = await selectedScanner.ScanFilesToFolderAsync(
                ImageScannerScanSource.AutoConfigured, scanFolder).AsTask(cancellationToken.Token, progress);
            } else if (RadioButtonSourceFlatbed.IsChecked.Value)
            {
                result = await selectedScanner.ScanFilesToFolderAsync(
                ImageScannerScanSource.Flatbed, scanFolder).AsTask(cancellationToken.Token, progress);
            } else if (RadioButtonSourceFeeder.IsChecked.Value)
            {
                result = await selectedScanner.ScanFilesToFolderAsync(
                ImageScannerScanSource.Feeder, scanFolder).AsTask(cancellationToken.Token, progress);
            } else
            {
                ContentDialog dialog = new ContentDialog();
                TextBlock dialogContent = new TextBlock();

                dialogContent.Text = "Please select a source mode.";
                dialogContent.TextWrapping = TextWrapping.WrapWholeWords;
                dialog.Title = "Error";
                dialog.Content = dialogContent;
                dialog.CloseButtonText = "Close";

                await dialog.ShowAsync();
            }


            // show result
            IRandomAccessStream stream = await result.ScannedFiles[0].OpenAsync(FileAccessMode.Read);
            BitmapImage bmp = new BitmapImage();
            await bmp.SetSourceAsync(stream);
            ImageScanViewer.Source = bmp;
        }

        private void scanProgress(UInt32 numberOfScannedDocuments)
        {
            // TODO
        }

        private void RadioButtonSourceChanged(object sender, RoutedEventArgs e)
        {
            if (sender == RadioButtonSourceAutomatic)
            {
                Debug.WriteLine("Calling UI_enabled with RadioButtonSourceAutomatic.IsEnabled = " + RadioButtonSourceAutomatic.IsEnabled.ToString());
                StackPanelColor.Visibility = Visibility.Collapsed;
                StackPanelResolution.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (sender == RadioButtonSourceFlatbed)
                {
                    StackPanelColor.Visibility = Visibility.Visible;
                    StackPanelResolution.Visibility = Visibility.Visible;
                    RadioButtonColorModeColor.IsEnabled = selectedScanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                    RadioButtonColorModeGrayscale.IsEnabled = selectedScanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                    RadioButtonColorModeMonochrome.IsEnabled = selectedScanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);
                }
                else if (sender == RadioButtonSourceFeeder)
                {
                    StackPanelColor.Visibility = Visibility.Visible;
                    StackPanelResolution.Visibility = Visibility.Visible;
                    RadioButtonColorModeColor.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                    RadioButtonColorModeGrayscale.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                    RadioButtonColorModeMonochrome.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);
                }

                // select first available color mode
                if (RadioButtonColorModeColor.IsEnabled) RadioButtonColorModeColor.IsChecked = true;
                else if (RadioButtonColorModeGrayscale.IsEnabled) RadioButtonColorModeGrayscale.IsChecked = true;
                else if (RadioButtonColorModeMonochrome.IsEnabled) RadioButtonColorModeMonochrome.IsChecked = true;
            }
        }
    }
}
