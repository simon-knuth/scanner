using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Windows.ApplicationModel;
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
        private double ColumnLeftDefaultMinWidth;
        private ObservableCollection<ComboBoxItem> scannerList = new ObservableCollection<ComboBoxItem>();
        private List<DeviceInformation> deviceInformations = new List<DeviceInformation>();
        private ImageScanner selectedScanner = null;
        private ObservableCollection<ComboBoxItem> formats = new ObservableCollection<ComboBoxItem>();
        private ObservableCollection<ComboBoxItem> resolutions = new ObservableCollection<ComboBoxItem>();
        private StorageFile scannedFile;
        private UIstate uiState = UIstate.unset;
        private FlowState flowState = FlowState.initial;
        DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
        private bool inForeground = true;


        public MainPage()
        {
            this.InitializeComponent();

            TextBlockHeader.Text = Package.Current.DisplayName.ToString();

            Page_ActualThemeChanged(null, null);

            // initialize veriables
            ColumnLeftDefaultMaxWidth = ColumnLeft.MaxWidth;
            ColumnLeftDefaultMinWidth = ColumnLeft.MinWidth;

            // populate the scanner list
            refreshScannerList();

            dataTransferManager.DataRequested += DataTransferManager_DataRequested;

            CoreApplication.EnteredBackground += (x, y) => { inForeground = false; };
            CoreApplication.LeavingBackground += (x, y) => { inForeground = true; };
        }

        private void DataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            args.Request.Data.SetBitmap(RandomAccessStreamReference.CreateFromFile(scannedFile));
            args.Request.Data.Properties.Title = scannedFile.Name;
        }


        public void refreshScannerList()
        {
            if (settingSearchIndicator) ProgressBarRefresh.Visibility = Visibility.Visible;
            // Create a Device Watcher class for type Image Scanner for enumerating scanners
            scannerWatcher = DeviceInformation.CreateWatcher(DeviceClass.ImageScanner);

            scannerWatcher.Added += OnScannerAdded;
            scannerWatcher.Removed += OnScannerRemoved;
            scannerWatcher.EnumerationCompleted += OnScannerEnumerationComplete;

            scannerWatcher.Start();
        }


        public async void refreshLeftPanel()
        {
            if (ComboBoxScanners.SelectedIndex == -1)
            {
                // no scanner selected /////////////////////////////////////////////////////////////////////
                // disable most controls
                UI_enabled(true, false, false, false, false, false, false, false, false, false, true, true);

                // reset variables
                selectedScanner = null;

                // reset selections
                RadioButtonSourceAutomatic.IsChecked = false;
                RadioButtonSourceFlatbed.IsChecked = false;
                RadioButtonSourceFeeder.IsChecked = false;
                RadioButtonColorModeColor.IsChecked = false;
                RadioButtonColorModeGrayscale.IsChecked = false;
                RadioButtonColorModeMonochrome.IsChecked = false;
                ComboBoxResolution.SelectedIndex = -1;

                // hide flatbed/feeder-specific options
                StackPanelColor.Visibility = Visibility.Collapsed;
                StackPanelResolution.Visibility = Visibility.Collapsed;
            }
            else
            {
                // scanner selected ///////////////////////////////////////////////////////////////////////
                // hide text on the right side
                StackPanelTextRight.Visibility = Visibility.Collapsed;
                HyperlinkSettings.IsTabStop = false;

                if (selectedScanner == null)
                {
                    // previously no scanner selected ////////////////////////////////////////////////////
                    // get scanner's DeviceInformation
                    foreach (DeviceInformation check in deviceInformations)
                    {
                        if (check.Id == ((ComboBoxItem)ComboBoxScanners.SelectedItem).Tag.ToString())
                        {
                            try
                            {
                                selectedScanner = await ImageScanner.FromIdAsync(check.Id);
                            }
                            catch (Exception exc)
                            {
                                // notify user that something went wrong
                                ShowMessageDialog("Unable to use scanner",
                                    "No information about the scanner you selected could be retrieved. The scanner list will be reinitialized. The error description is:" + "\n" + exc.Message);

                                // (almost) start from scratch to hopefully get rid of dead scanners
                                autoSelectScanner = false;
                                scannerList.Clear();
                                scannerWatcher.Stop();
                                ComboBoxScanners.SelectedIndex = -1;
                                selectedScanner = null;
                                deviceInformations.Clear();
                                refreshLeftPanel();
                                scannerWatcher.Start();
                                return;
                            }
                            autoSelectScanner = true;
                            break;
                        }
                    }
                }

                // refresh source modes
                bool autoAllowed = selectedScanner.IsScanSourceSupported(ImageScannerScanSource.AutoConfigured);
                bool flatbedAllowed = selectedScanner.IsScanSourceSupported(ImageScannerScanSource.Flatbed);
                bool feederAllowed = selectedScanner.IsScanSourceSupported(ImageScannerScanSource.Feeder);

                // select first available source mode if none was selected previously
                if (RadioButtonSourceAutomatic.IsChecked != true 
                    && RadioButtonSourceFlatbed.IsChecked != true 
                    && RadioButtonSourceFeeder.IsChecked != true)
                {
                    // no source mode was selected before
                    if (autoAllowed) RadioButtonSourceAutomatic.IsChecked = true;
                    else if (flatbedAllowed) RadioButtonSourceFlatbed.IsChecked = true;
                    else if (feederAllowed) RadioButtonSourceFeeder.IsChecked = true;
                }
                else
                {
                    // a source mode was already selected
                    if (RadioButtonSourceFlatbed.IsChecked == true || RadioButtonSourceFeeder.IsChecked == true)
                    {
                        StackPanelColor.Visibility = Visibility.Visible;
                        StackPanelResolution.Visibility = Visibility.Visible;
                    } else if (RadioButtonSourceAutomatic.IsChecked == true)
                    {
                        StackPanelColor.Visibility = Visibility.Collapsed;
                        StackPanelResolution.Visibility = Visibility.Collapsed;

                        RadioButtonColorModeColor.IsChecked = false;
                        RadioButtonColorModeGrayscale.IsChecked = false;
                        RadioButtonColorModeMonochrome.IsChecked = false;
                    }
                }

                UI_enabled(true, autoAllowed, flatbedAllowed, feederAllowed, true, true, true, true, true, true, true, true);
            }
        }


        private async void OnScannerAdded(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
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

                    if (autoSelectScanner && !ComboBoxScanners.IsDropDownOpen && selectedScanner == null && deviceInformations.Count == 1)
                    {
                        ComboBoxScanners.SelectedIndex = 0;
                    }

                    TextBlockRefreshHint.Text = " (" + scannerList.Count.ToString() + " found)";
                }
            );
        }


        private async void OnScannerRemoved(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                // find lost scanner in scannerList to remove the corresponding scanner and its list entry
                foreach (var item in scannerList)
                {
                    if (item.Tag.ToString() == deviceInfoUpdate.Id)
                    {
                        ComboBoxScanners.IsDropDownOpen = false;
                        if (item.IsSelected) refreshLeftPanel();

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
            double width = ((Frame)Window.Current.Content).ActualWidth;
            if (width < 700)
            {
                // small ////////////////////////////////////////////////////////
                StackPanelTextRight.Visibility = Visibility.Collapsed;
                HyperlinkSettings.IsTabStop = false;

                if (flowState == FlowState.result || flowState == FlowState.crop)
                {
                    // small and result visible
                    if (uiState != UIstate.small_result)
                    {
                        ColumnLeft.MaxWidth = 0;
                        ColumnLeft.MinWidth = 0;
                        ColumnRight.MaxWidth = Double.PositiveInfinity;

                        DropShadowPanelRight.Visibility = Visibility.Visible;
                        CommandBarDone.Visibility = Visibility.Visible;
                    }
                    uiState = UIstate.small_result;
                }
                else
                {
                    // small and no result visible
                    if (uiState != UIstate.small_initial)
                    {
                        ColumnLeft.MaxWidth = Double.PositiveInfinity;
                        ColumnLeft.MinWidth = ColumnLeftDefaultMinWidth;
                        ColumnRight.MaxWidth = 0;

                        DropShadowPanelRight.Visibility = Visibility.Collapsed;
                        CommandBarDone.Visibility = Visibility.Collapsed;
                    }
                    uiState = UIstate.small_initial;
                }
            }
            else if (700 < width && width < 900)
            {
                // medium ///////////////////////////////////////////////////////
                if (uiState != UIstate.full)
                {
                    ColumnRight.MaxWidth = Double.PositiveInfinity;
                    ColumnLeft.MaxWidth = ColumnLeftDefaultMaxWidth;
                    ColumnLeft.MinWidth = ColumnLeftDefaultMinWidth;

                    DropShadowPanelRight.Visibility = Visibility.Visible;
                    CommandBarDone.Visibility = Visibility.Collapsed;
                    StackPanelTextRight.Visibility = Visibility.Collapsed;
                    HyperlinkSettings.IsTabStop = false;
                }
                uiState = UIstate.full;
            }
            else if (900 < width)
            {
                // large ////////////////////////////////////////////////////////
                if (uiState != UIstate.full)
                {
                    ColumnRight.MaxWidth = Double.PositiveInfinity;
                    ColumnLeft.MaxWidth = ColumnLeftDefaultMaxWidth;
                    ColumnLeft.MinWidth = ColumnLeftDefaultMinWidth;

                    DropShadowPanelRight.Visibility = Visibility.Visible;
                    CommandBarDone.Visibility = Visibility.Collapsed;
                    if (selectedScanner == null)
                    {
                        StackPanelTextRight.Visibility = Visibility.Visible;
                        HyperlinkSettings.IsTabStop = true;
                    }
                }
                uiState = UIstate.full;
            }
        }


        private async void ButtonRecents_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchFolderAsync(scanFolder);
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
                if (buttonScan) TextBlockButtonScan.Opacity = 1; else TextBlockButtonScan.Opacity = 0.5;
                ButtonSettings.IsEnabled = buttonSettings;
                ButtonRecents.IsEnabled = buttonRecents;
            }
            );
        }


        private async void ButtonScan_Click(object sender, RoutedEventArgs e)
        {
            // lock (almost) entire left panel and clean up right side
            UI_enabled(false, false, false, false, false, false, false, false, false, false, false, true);
            CommandBarScan.Visibility = Visibility.Collapsed;
            ImageScanViewer.Visibility = Visibility.Collapsed;
            TextBlockButtonScan.Visibility = Visibility.Collapsed;
            ProgressRingScan.Visibility = Visibility.Visible;

            if (scanFolder == null)
            {
                // TODO no scan folder selected yet
            }

            // gather options
            Tuple<ImageScannerFormat, string> formatFlow = GetDesiredImageScannerFormat(ComboBoxFormat, formats);

            if (RadioButtonSourceAutomatic.IsChecked.Value)
            {
                // user asked for automatic configuration

                if (ComboBoxFormat.SelectedIndex == -1)
                {
                    // TODO no format selected
                }
                selectedScanner.AutoConfiguration.Format = formatFlow.Item1;

            }
            else if (RadioButtonSourceFlatbed.IsChecked.Value)
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
                ImageScannerResolution res = new ImageScannerResolution
                {
                    DpiX = float.Parse(((ComboBoxItem)ComboBoxResolution.SelectedItem).Tag.ToString().Split(",")[0]),
                    DpiY = float.Parse(((ComboBoxItem)ComboBoxResolution.SelectedItem).Tag.ToString().Split(",")[1])
                };
                selectedScanner.FlatbedConfiguration.DesiredResolution = res;

                if (ComboBoxFormat.SelectedIndex == -1)
                {
                    // TODO no format selected
                }
                selectedScanner.FlatbedConfiguration.Format = formatFlow.Item1;

            }
            else if (RadioButtonSourceFeeder.IsChecked.Value)
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
                selectedScanner.FeederConfiguration.Format = formatFlow.Item1;

            }
            else
            {
                // TODO no configuration selected
            }

            // start scan and show progress and cancel button
            var cancellationToken = new CancellationTokenSource();
            var progress = new Progress<UInt32>(scanProgress);


            if (formatFlow.Item2 != "")
            {
                // save file in base format to later convert it
                ImageScannerScanResult result = await ScanInCorrectMode(RadioButtonSourceAutomatic, RadioButtonSourceFlatbed,
                RadioButtonSourceFeeder, scanFolder, cancellationToken, progress, selectedScanner);

                IRandomAccessStream stream = await result.ScannedFiles[0].OpenAsync(FileAccessMode.ReadWrite);
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                Guid encoderId = GetBitmapEncoderId(formatFlow.Item2);

                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
                encoder.SetSoftwareBitmap(softwareBitmap);

                try { await encoder.FlushAsync(); }
                catch (Exception)
                {
                    // TODO notify user that conversion has failed
                    return;
                }
                stream.Dispose();

                string path = result.ScannedFiles[0].Path
                    .Replace("." + result.ScannedFiles[0].Name.Split(".")[1], "." + formatFlow.Item2);

                await result.ScannedFiles[0].RenameAsync(result.ScannedFiles[0].Name
                    .Replace("." + result.ScannedFiles[0].Name.Split(".")[1], "." + formatFlow.Item2));     // TODO catch exceptions

                scannedFile = await StorageFile.GetFileFromPathAsync(path);     // TODO catch exceptions
            }
            else
            {
                // no need to convert
                ImageScannerScanResult result = await ScanInCorrectMode(RadioButtonSourceAutomatic, RadioButtonSourceFlatbed,
                RadioButtonSourceFeeder, scanFolder, cancellationToken, progress, selectedScanner);
                scannedFile = result.ScannedFiles[0];
            }

            // show result
            TextBlockButtonScan.Visibility = Visibility.Visible;
            ProgressRingScan.Visibility = Visibility.Collapsed;
            DisplayImageAsync(scannedFile, ImageScanViewer);
            await ImageCropper.LoadImageFromFile(scannedFile);
            flowState = FlowState.result;

            // send toast if the app isn't in the foreground
            if (settingNotificationScanComplete && !inForeground) SendToastNotification("Scan complete", "Return to the app for editing and more.", null, 5);

            // modify UI
            CommandBarScan.Visibility = Visibility.Visible;
            Page_SizeChanged(null, null);
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
            if (RadioButtonSourceAutomatic.IsChecked == true)
            {
                refreshLeftPanel();

                // detect available file formats and update UI accordingly
                GetSupportedFormats(selectedScanner.AutoConfiguration, formats, selectedScanner, ComboBoxFormat);

                RadioButtonColorModeColor.IsEnabled = false;
                RadioButtonColorModeGrayscale.IsEnabled = false;
                RadioButtonColorModeMonochrome.IsEnabled = false;

                RadioButtonColorModeColor.IsChecked = false;
                RadioButtonColorModeGrayscale.IsChecked = false;
                RadioButtonColorModeMonochrome.IsChecked = false;
            }
            else if (RadioButtonSourceFlatbed.IsChecked == true)
            {
                refreshLeftPanel();

                // detect available file formats and update UI accordingly
                GetSupportedFormats(selectedScanner.FlatbedConfiguration, formats, selectedScanner, ComboBoxFormat);

                // detect available resolutions and update UI accordingly
                GenerateResolutions(selectedScanner.FlatbedConfiguration, ComboBoxResolution, resolutions);

                // detect available color modes and update UI accordingly
                RadioButtonColorModeColor.IsEnabled = selectedScanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                RadioButtonColorModeGrayscale.IsEnabled = selectedScanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                RadioButtonColorModeMonochrome.IsEnabled = selectedScanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);

                if (RadioButtonColorModeColor.IsEnabled) RadioButtonColorModeColor.IsChecked = true;
                else if (RadioButtonColorModeGrayscale.IsEnabled) RadioButtonColorModeGrayscale.IsChecked = true;
                else if (RadioButtonColorModeMonochrome.IsEnabled) RadioButtonColorModeMonochrome.IsChecked = true;
            }
            else if (RadioButtonSourceFeeder.IsChecked == true)
            {
                refreshLeftPanel();

                // detect available file formats and update UI accordingly
                GetSupportedFormats(selectedScanner.FeederConfiguration, formats, selectedScanner, ComboBoxFormat);

                // detect available resolutions and update UI accordingly
                GenerateResolutions(selectedScanner.FeederConfiguration, ComboBoxResolution, resolutions);
                    
                // detect available color modes and update UI accordingly
                RadioButtonColorModeColor.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                RadioButtonColorModeGrayscale.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                RadioButtonColorModeMonochrome.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);

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
            SendToastNotification("Copied scan to the clipboard", "", scannedFile.Path, 5);
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
            try
            {
                await scannedFile.RenameAsync(TextBoxRename.Text + "." + scannedFile.Name.Split(".")[1], NameCollisionOption.FailIfExists);    // TODO process error
            }
            catch (Exception)
            {
                ShowMessageDialog("Rename unsuccessful", "Perhaps a file with that name already exists?");
            }
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

            // make sure that the ImageCropper won't be obstructed
            ImageCropper.Padding = new Thickness(24,
                24 + CoreApplication.GetCurrentView().TitleBar.Height + CommandBarDone.ActualHeight +
                DropShadowPanelCommandBarDone.Margin.Top, 24, 24 + CommandBarScan.ActualHeight +
                DropShadowPanelCommandBar.Margin.Bottom);

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
            catch (Exception)
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


        /// <summary>
        ///     Page was loaded (possibly through navigation).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (formatSettingChanged)
            {
                formatSettingChanged = false;
                RadioButtonSourceChanged(null, null);
            }

            if (settingSearchIndicator) ProgressBarRefresh.Visibility = Visibility.Visible;
            else ProgressBarRefresh.Visibility = Visibility.Collapsed;
        }

        private void AppBarButtonDone_Click(object sender, RoutedEventArgs e)
        {
            if (flowState == FlowState.crop)
            {
                AppBarButtonCrop.IsChecked = false;
            }
            else
            {
                flowState = FlowState.initial;
                CommandBarScan.Visibility = Visibility.Collapsed;
                ImageScanViewer.Visibility = Visibility.Collapsed;
                Page_SizeChanged(null, null);
            }
        }

        private void ScrollViewerScan_LayoutUpdated(object sender, object e)
        {
            // fix image, might otherwise slip outside the window's boundaries
            ImageScanViewer.MaxWidth = ScrollViewerScan.ActualWidth;
            ImageScanViewer.MaxHeight = ScrollViewerScan.ActualHeight;
        }

        private void ComboBoxScanners_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            refreshLeftPanel();
        }


        private void GridMainPage_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (IsCtrlKeyPressed())
            {
                switch (e.Key)
                {
                    case VirtualKey.C:
                        // shortcut Copy
                        if (flowState == FlowState.result) AppBarButtonCopy_Click(null, null);
                        break;
                    case VirtualKey.S:
                        // shortcut share
                        if (flowState == FlowState.result) AppBarButtonShare_Click(null, null);
                        break;
                }
            }
        }
    }
}
