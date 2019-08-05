using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;

using static Globals;
using static ScannerOperation;
using static Utilities;

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
        private ObservableCollection<ComboBoxItem> formats = new ObservableCollection<ComboBoxItem>();
        private ObservableCollection<ComboBoxItem> resolutions = new ObservableCollection<ComboBoxItem>();
        private StorageFile scannedFile;
        private FlowState flowState = FlowState.initial;
        private UIstate uiState;
        DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();


        public MainPage()
        {
            this.InitializeComponent();

            Page_ActualThemeChanged(null, null);

            // initialize veriables
            ColumnLeftDefaultMaxWidth = ColumnLeft.MaxWidth;

            // populate the scanner list
            refreshScannerList();

            // get ScanFolder
            getScanFolder();

            dataTransferManager.DataRequested += DataTransferManager_DataRequested;
        }

        private void DataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            args.Request.Data.SetBitmap(RandomAccessStreamReference.CreateFromFile(scannedFile));
            args.Request.Data.Properties.Title = scannedFile.Name;
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
                        }
                        else
                        {
                            item.Content = deviceInfo.Name;
                        }
                        item.Tag = deviceInfo.Id;

                        deviceInformations.Add(deviceInfo);
                        scannerList.Add(item);
                    }
                    else return;

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
                // find lost scanner in scannerList to remove the corresponding scanner and its list entry
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
                    HyperlinkSettings.IsTabStop = true;
                }
            );
        }


        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // responsive behavior
            if (e.NewSize.Width < 900)
            {
                StackPanelTextRight.Visibility = Visibility.Collapsed;
                HyperlinkSettings.IsTabStop = false;
                if (e.NewSize.Width < 700)
                {
                    ColumnLeft.MaxWidth = Double.PositiveInfinity;
                    DropShadowPanelRight.Visibility = Visibility.Collapsed;
                    ColumnRight.MaxWidth = 0;
                }
                else
                {
                    ColumnRight.MaxWidth = Double.PositiveInfinity;
                    ColumnLeft.MaxWidth = ColumnLeftDefaultMaxWidth;
                    DropShadowPanelRight.Visibility = Visibility.Visible;
                }
            }
            else
            {
                ColumnRight.MaxWidth = Double.PositiveInfinity;
                ColumnLeft.MaxWidth = ColumnLeftDefaultMaxWidth;
                DropShadowPanelRight.Visibility = Visibility.Visible;

                if (selectedScanner == null)
                {
                    StackPanelTextRight.Visibility = Visibility.Visible;
                    HyperlinkSettings.IsTabStop = true;
                }
            }

            ImageScanViewer.MaxWidth = ScrollViewerScan.ActualWidth;
            ImageScanViewer.MaxHeight = ScrollViewerScan.ActualHeight;
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
                ComboBoxFormat.IsEnabled = comboBoxType;
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

            StackPanelTextRight.Visibility = Visibility.Visible;
            HyperlinkSettings.IsTabStop = true;
        }


        private async void scanner_selected()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            async () =>
            {
                StackPanelTextRight.Visibility = Visibility.Collapsed;
                HyperlinkSettings.IsTabStop = false;

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
            });
        }


        private async void ComboBoxScanners_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ComboBox) sender).SelectedIndex == -1)
            {
                selectedScanner = null;
                no_scanner_selected();
            } else
            {
                string scannerID = ((ComboBoxItem)ComboBoxScanners.SelectedItem).Tag.ToString();
                selectedScanner = await ImageScanner.FromIdAsync(scannerID);
                scanner_selected();
            }
        }


        private async void ButtonScan_Click(object sender, RoutedEventArgs e)
        {
            // lock (almost) entire left panel and clean up right side
            UI_enabled(false, false, false, false, false, false, false, false, false, false, false, true);
            CommandBarScan.Visibility = Visibility.Collapsed;
            ImageScanViewer.Visibility = Visibility.Collapsed;
            StackPanelScanText.Visibility = Visibility.Visible;

            // gather options
            if (RadioButtonSourceAutomatic.IsChecked.Value)
            {
                // user asked for automatic configuration

                if (ComboBoxFormat.SelectedIndex == -1)
                {
                    // TODO no format selected
                }
                selectedScanner.AutoConfiguration.Format = GetDesiredImageScannerFormat(ComboBoxScanners);

            } else if (RadioButtonSourceFlatbed.IsChecked.Value)
            {
                // user asked for flatbed configuration
                if (RadioButtonColorModeColor.IsChecked.Value == false
                    && RadioButtonColorModeGrayscale.IsChecked.Value == false
                    && RadioButtonColorModeMonochrome.IsChecked.Value == false)
                {
                    // TODO no color mode selected
                }
                selectedScanner.FlatbedConfiguration.ColorMode = GetDesiredColorMode();

                if (ComboBoxResolution.SelectedIndex == -1)
                {
                    // TODO no resolution selected
                }
                ImageScannerResolution res = new ImageScannerResolution {
                    DpiX = float.Parse(((ComboBoxItem) ComboBoxResolution.SelectedItem).Tag.ToString().Split(",")[0]),
                    DpiY = float.Parse(((ComboBoxItem)ComboBoxResolution.SelectedItem).Tag.ToString().Split(",")[1])};
                selectedScanner.FlatbedConfiguration.DesiredResolution = res;

                if (ComboBoxFormat.SelectedIndex == -1)
                {
                    // TODO no format selected
                }
                selectedScanner.FlatbedConfiguration.Format = GetDesiredImageScannerFormat(ComboBoxScanners);

            } else if (RadioButtonSourceFeeder.IsChecked.Value)
            {
                // user asked for feeder configuration

                if (RadioButtonColorModeColor.IsChecked.Value == false
                    && RadioButtonColorModeGrayscale.IsChecked.Value == false
                    && RadioButtonColorModeMonochrome.IsChecked.Value == false)
                {
                    // TODO no color mode selected
                }
                selectedScanner.FeederConfiguration.ColorMode = GetDesiredColorMode();

                if (ComboBoxResolution.SelectedIndex == -1)
                {
                    // TODO no resolution selected
                }
                ImageScannerResolution res = new ImageScannerResolution
                {
                    DpiX = float.Parse(((ComboBoxItem)ComboBoxResolution.SelectedItem).Tag.ToString().Split(",")[0]),
                    DpiY = float.Parse(((ComboBoxItem)ComboBoxResolution.SelectedItem).Tag.ToString().Split(",")[1])
                };
                selectedScanner.FeederConfiguration.DesiredResolution = res;

                if (ComboBoxFormat.SelectedIndex == -1)
                {
                    // TODO no format selected
                }
                selectedScanner.FeederConfiguration.Format = GetDesiredImageScannerFormat(ComboBoxScanners);

            } else
            {
                // TODO no configuration selected
            }

            // check folder and attempt to create it if necessary
            // TODO

            // start scan and show progress and cancel button
            var cancellationToken = new CancellationTokenSource();
            var progress = new Progress<UInt32>(scanProgress);

            ImageScannerScanResult result = await ScanInCorrectMode(RadioButtonSourceAutomatic, RadioButtonSourceFlatbed,
                RadioButtonSourceFeeder, scanFolder, cancellationToken, progress, selectedScanner);

            // show result
            scannedFile = result.ScannedFiles[0];
            StackPanelScanText.Visibility = Visibility.Collapsed;
            DisplayImageAsync(scannedFile, ImageScanViewer);
            await ImageCropper.LoadImageFromFile(scannedFile);
            flowState = FlowState.result;

            // unlock UI
            CommandBarScan.Visibility = Visibility.Visible;
            RadioButtonSourceChanged(null, null);
            UI_enabled(true, true, true, true, true, true, true, true, true, true, true, true);
        }


        /// <summary>
        ///     Returns the ImageScannerColorMode to the corresponding RadioButton checked by the user.
        /// </summary>
        /// <remarks>
        ///     Returns ImageScannerColorMode.Monochrome if no other option could be matched.
        /// </remarks>
        /// <returns>
        ///     The corresponding ImageScannerColorMode.
        /// </returns>
        private ImageScannerColorMode GetDesiredColorMode()
        {
            if (RadioButtonColorModeColor.IsChecked.Value) return ImageScannerColorMode.Color;
            if (RadioButtonColorModeGrayscale.IsChecked.Value) return ImageScannerColorMode.Grayscale;
            return ImageScannerColorMode.Monochrome;
        }

        private void scanProgress(UInt32 numberOfScannedDocuments)
        {
            // TODO
        }


        /// <summary>
        ///     Is called if another source mode was selected. Hides/shows available options in the left panel and updates the available file formats. 
        ///     The first available color mode and format are automatically selected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RadioButtonSourceChanged(object sender, RoutedEventArgs e)
        {
            if (sender == RadioButtonSourceAutomatic)
            {
                StackPanelColor.Visibility = Visibility.Collapsed;
                StackPanelResolution.Visibility = Visibility.Collapsed;

                // detect available file formats and update UI accordingly
                GetSupportedFormats(selectedScanner.AutoConfiguration, formats, selectedScanner);
                ComboBoxFormat.SelectedIndex = 0;
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

                    // detect available file formats and update UI accordingly
                    GetSupportedFormats(selectedScanner.FlatbedConfiguration, formats, selectedScanner);
                    ComboBoxFormat.SelectedIndex = 0;

                    // detect available resolutions and update UI accordingly
                    GenerateResolutions(selectedScanner.FlatbedConfiguration, ComboBoxResolution, resolutions);
                }
                else if (sender == RadioButtonSourceFeeder)
                {
                    StackPanelColor.Visibility = Visibility.Visible;
                    StackPanelResolution.Visibility = Visibility.Visible;
                    RadioButtonColorModeColor.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                    RadioButtonColorModeGrayscale.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                    RadioButtonColorModeMonochrome.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);

                    // detect available file formats and update UI accordingly
                    GetSupportedFormats(selectedScanner.FeederConfiguration, formats, selectedScanner);
                    ComboBoxFormat.SelectedIndex = 0;

                    // detect available resolutions and update UI accordingly
                    GenerateResolutions(selectedScanner.FeederConfiguration, ComboBoxResolution, resolutions);
                }

                // select first available color mode
                if (RadioButtonColorModeColor.IsEnabled) RadioButtonColorModeColor.IsChecked = true;
                else if (RadioButtonColorModeGrayscale.IsEnabled) RadioButtonColorModeGrayscale.IsChecked = true;
                else if (RadioButtonColorModeMonochrome.IsEnabled) RadioButtonColorModeMonochrome.IsChecked = true;
            }
        }

        private void AppBarButtonCopy_Click(object sender, RoutedEventArgs e)
        {
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;
            dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromFile(scannedFile));
            Clipboard.SetContent(dataPackage);
        }

        private async void ScrollViewerScan_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            ScrollViewer scrollViewer = sender as ScrollViewer;
            var doubleTapPoint = e.GetPosition(scrollViewer);

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High,
            () =>
            {
                if (scrollViewer.ZoomFactor != 1)
                {
                    scrollViewer.ChangeView(0, 0, 1);
                }
                else
                {
                    scrollViewer.ChangeView(doubleTapPoint.X, doubleTapPoint.Y, 2);
                }
            });
        }

        private void ButtonSettings_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage), null, new EntranceNavigationTransitionInfo());
        }

        private void AppBarButtonShare_Click(object sender, RoutedEventArgs e)
        {
            DataTransferManager.ShowShareUI();
        }

        private async void ButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            await scannedFile.DeleteAsync(StorageDeleteOption.Default);
            FlyoutAppBarButtonDelete.Hide();
            CommandBarScan.Visibility = Visibility.Collapsed;
            ImageScanViewer.Visibility = Visibility.Collapsed;
            flowState = FlowState.initial;
        }

        private async void ButtonRename_Click(object sender, RoutedEventArgs e)
        {
            await scannedFile.RenameAsync(TextBoxRename.Text + "." + scannedFile.Name.Split(".")[1], NameCollisionOption.FailIfExists);    // TODO process error
            FlyoutAppBarButtonRename.Hide();
        }

        private void FlyoutAppBarButtonRename_Opening(object sender, object e)
        {
            TextBoxRename.Text = scannedFile.Name.Split(".")[0];
        }

        private void AppBarButtonCrop_Checked(object sender, RoutedEventArgs e)
        {
            // deactivate other buttons
            LockCommandBar(CommandBarScan, AppBarButtonCrop);

            flowState = FlowState.crop;

            // show ImageCropper
            ImageCropper.Visibility = Visibility.Visible;
        }


        private async void AppBarButtonCrop_Unchecked(object sender, RoutedEventArgs e)
        {
            AppBarButtonCrop.IsEnabled = false;

            // save file
            IRandomAccessStream stream = await scannedFile.OpenAsync(FileAccessMode.ReadWrite);
            await ImageCropper.SaveAsync(stream, GetBitmapFileFormat(scannedFile), true);
            stream.Dispose();

            // refresh preview
            DisplayImageAsync(scannedFile, ImageScanViewer);

            // return UI to normal
            ImageCropper.Visibility = Visibility.Collapsed;
            AppBarButtonCrop.IsEnabled = true;
            UnlockCommandBar(CommandBarScan, null);
            flowState = FlowState.result;
        }

        /// <summary>
        ///     Reacts to the enter key while TextBoxRename is focused.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBoxRename_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Accept || e.Key == VirtualKey.Enter) ButtonRename_Click(sender, null);
        }

        private async void AppBarButtonRotate_Click(object sender, RoutedEventArgs e)
        {
            LockCommandBar(CommandBarScan, null);
            IRandomAccessStream stream = await scannedFile.OpenAsync(FileAccessMode.ReadWrite);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            Guid encoderId = GetBitmapEncoderId(scannedFile.Name.Split(".")[1]);

            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
            encoder.SetSoftwareBitmap(softwareBitmap);
            encoder.BitmapTransform.Rotation = BitmapRotation.Clockwise90Degrees;

            try { await encoder.FlushAsync(); }
            catch (Exception exc)
            {
                // TODO process error
            }

            DisplayImageAsync(scannedFile, ImageScanViewer);
            stream.Dispose();
            await ImageCropper.LoadImageFromFile(scannedFile);

            UnlockCommandBar(CommandBarScan, null);
        }

        private void Page_ActualThemeChanged(FrameworkElement sender, object args)
        {
            if (settingAppTheme == Theme.system)
            {
                if ((new UISettings()).GetColorValue(UIColorType.Background).ToString() == "#FF000000")
                {
                    // Dark mode is active
                    DropShadowPanelRight.ShadowOpacity = 0.6;
                }
                else
                {
                    // Light mode is active
                    DropShadowPanelRight.ShadowOpacity = 0.3;
                }
            }
            else
            {
                if (settingAppTheme == Theme.light)
                {
                    DropShadowPanelRight.ShadowOpacity = 0.3;
                }
                else
                {
                    DropShadowPanelRight.ShadowOpacity = 0.6;
                }
            }

        }
    }
}
