using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Pdf;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
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
        CancellationTokenSource cancellationToken = null;
        private bool canceledScan = false;
        ImageProperties imageProperties;


        public MainPage()
        {
            this.InitializeComponent();

            TextBlockHeader.Text = Package.Current.DisplayName.ToString();

            ((Windows.UI.Xaml.Documents.Run)HyperlinkSettings.Inlines[0]).Text = LocalizedString("HyperlinkScannerSelectionHintBodyLink");

            Page_ActualThemeChanged(null, null);

            // initialize veriables
            ColumnLeftDefaultMaxWidth = ColumnLeft.MaxWidth;
            ColumnLeftDefaultMinWidth = ColumnLeft.MinWidth;

            // populate the scanner list
            refreshScannerList();

            InkCanvasScan.InkPresenter.UnprocessedInput.PointerEntered += InkCanvasScan_PointerEntered;
            InkCanvasScan.InkPresenter.UnprocessedInput.PointerExited += InkCanvasScan_PointerExited;

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
                                ShowMessageDialog(LocalizedString("ErrorMessageScannerInformationHeader"),
                                    LocalizedString("ErrorMessageScannerInformationBody") + "\n" + exc.Message);

                                // (almost) start from scratch to hopefully get rid of dead scanners
                                possiblyDeadScanner = true;
                                scannerList.Clear();
                                scannerWatcher.Stop();
                                ComboBoxScanners.SelectedIndex = -1;
                                selectedScanner = null;
                                deviceInformations.Clear();
                                refreshLeftPanel();
                                scannerWatcher.Start();
                                ComboBoxScanners.IsEnabled = true;
                                return;
                            }
                            possiblyDeadScanner = false;
                            ComboBoxScanners.IsEnabled = true;
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
                            item.Content = deviceInfo.Name + " (" + LocalizedString("DefaultScannerIndicator") + ")";
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

                    if (!possiblyDeadScanner && !ComboBoxScanners.IsDropDownOpen && settingAutomaticScannerSelection
                        && selectedScanner == null && deviceInformations.Count == 1)
                    {
                        ComboBoxScanners.SelectedIndex = 0;
                    }

                    TextBlockFoundScannersHint.Text = " (" + LocalizedString("FoundScannersHintBeforeNumber") + scannerList.Count.ToString() + " " + LocalizedString("FoundScannersHintAfterNumber") + ")";
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

                TextBlockFoundScannersHint.Text = " (" + LocalizedString("FoundScannersHintBeforeNumber") + scannerList.Count.ToString() + " " + LocalizedString("FoundScannersHintAfterNumber") + ")";
            });
        }


        private async void OnScannerEnumerationComplete(DeviceWatcher sender, Object theObject)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
                {
                    TextBlockFoundScannersHint.Visibility = Visibility.Visible;
                    UI_enabled(true, false, false, false, false, false, false, false, false, false, true, true);                }
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
                if (flowState == FlowState.result || flowState == FlowState.crop || flowState == FlowState.draw)
                {
                    // small and result visible
                    if (uiState != UIstate.small_result)
                    {
                        ColumnLeft.MaxWidth = 0;
                        ColumnLeft.MinWidth = 0;
                        ColumnRight.MaxWidth = Double.PositiveInfinity;

                        DropShadowPanelRight.Visibility = Visibility.Visible;
                        switch (flowState)
                        {
                            case FlowState.result:
                                ShowSecondaryMenuConfig(SecondaryMenuConfig.done);
                                break;
                            case FlowState.crop:
                                ShowSecondaryMenuConfig(SecondaryMenuConfig.crop);
                                break;
                            case FlowState.draw:
                                ShowSecondaryMenuConfig(SecondaryMenuConfig.draw);
                                break;
                        }
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
                        ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
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

                    switch (flowState)
                    {
                        case FlowState.crop:
                            ShowSecondaryMenuConfig(SecondaryMenuConfig.crop);
                            break;
                        case FlowState.draw:
                            ShowSecondaryMenuConfig(SecondaryMenuConfig.draw);
                            break;
                        default:
                            ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
                            break;
                    }
                }

                StackPanelTextRight.Visibility = Visibility.Collapsed;
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
                    ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);

                    switch (flowState)
                    {
                        case FlowState.crop:
                            ShowSecondaryMenuConfig(SecondaryMenuConfig.crop);
                            break;
                        case FlowState.draw:
                            ShowSecondaryMenuConfig(SecondaryMenuConfig.draw);
                            break;
                        default:
                            ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
                            break;
                    }
                }

                if (selectedScanner == null) StackPanelTextRight.Visibility = Visibility.Visible;
                else StackPanelTextRight.Visibility = Visibility.Collapsed;

                uiState = UIstate.full;
            }
        }

        /// <summary>
        ///     Opens the currently selected scan folder and selects the currently visible result if possible.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ButtonRecents_Click(object sender, RoutedEventArgs e)
        {
            if (flowState != FlowState.result)
            {
                // simply open folder
                try { await Launcher.LaunchFolderAsync(scanFolder); }
                catch (Exception) { }
            } else
            {
                // open folder and select result in it
                FolderLauncherOptions launcherOptions = new FolderLauncherOptions();
                launcherOptions.ItemsToSelect.Add(scannedFile);

                try
                {
                    await scanFolder.GetFileAsync(scannedFile.Name);        // used to detect whether opening the file explorer with a selection will fail
                    await Launcher.LaunchFolderAsync(scanFolder, launcherOptions);
                }
                catch (Exception)
                {
                    try { await Launcher.LaunchFolderAsync(scanFolder); }
                    catch (Exception) { }
                }
            }
            
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
            ShowPrimaryMenuConfig(PrimaryMenuConfig.hidden);
            ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
            ImageScanViewer.Visibility = Visibility.Collapsed;
            TextBlockButtonScan.Visibility = Visibility.Collapsed;
            ProgressRingScan.Visibility = Visibility.Visible;
            ScrollViewerScan.ChangeView(0, 0, 1);
            AppBarButtonDiscard_Click(null, null);                              // cancel crop/drawing mode if necessary

            canceledScan = false;

            if (scanFolder == null)
            {
                MessageDialog dialog = new MessageDialog(LocalizedString("ErrorMessageScanFolderHeader"), LocalizedString("ErrorMessageScanFolderBody"));
                dialog.Commands.Add(new UICommand(LocalizedString("ErrorMessageScanFolderSettings"), new UICommandInvokedHandler(this.ButtonSettings_Click)));
                dialog.Commands.Add(new UICommand(LocalizedString("ErrorMessageScanFolderClose"), (x) => { }));
                dialog.DefaultCommandIndex = 0;
                dialog.CancelCommandIndex = 1;
                ScanCanceled();
                await dialog.ShowAsync();
                return;
            }

            // gather options
            Tuple<ImageScannerFormat, string> formatFlow = GetDesiredFormat(ComboBoxFormat, formats);
            if (formatFlow == null)
            {
                ShowMessageDialog(LocalizedString("ErrorMessageNoFormatHeader"), LocalizedString("ErrorMessageNoFormatBody"));
                ScanCanceled();
                return;
            }

            if (RadioButtonSourceAutomatic.IsChecked.Value)             // auto configuration ///////////////
            {
                // format
                selectedScanner.AutoConfiguration.Format = formatFlow.Item1;
            }
            else if (RadioButtonSourceFlatbed.IsChecked.Value)          // flatbed configuration ////////////
            {
                // color mode
                ImageScannerColorMode? selectedColorMode = GetDesiredColorMode();
                if (selectedColorMode == null)
                {
                    ShowMessageDialog(LocalizedString("ErrorMessageNoColorModeHeader"), LocalizedString("ErrorMessageNoColorModeBody"));
                    ScanCanceled();
                    return;
                }
                else selectedScanner.FlatbedConfiguration.ColorMode = (ImageScannerColorMode)selectedColorMode;

                // resolution
                ImageScannerResolution? selectedResolution = GetDesiredResolution();
                if (selectedResolution == null)
                {
                    ShowMessageDialog(LocalizedString("ErrorMessageNoResolutionHeader"), LocalizedString("ErrorMessageNoResolutionBody"));
                    ScanCanceled();
                    return;
                }
                selectedScanner.FlatbedConfiguration.DesiredResolution = (ImageScannerResolution) selectedResolution;

                // format
                selectedScanner.FlatbedConfiguration.Format = formatFlow.Item1;
            }
            else if (RadioButtonSourceFeeder.IsChecked.Value)           // feeder configuration /////////////
            {
                // color mode
                ImageScannerColorMode? selectedColorMode = GetDesiredColorMode();
                if (selectedColorMode == null)
                {
                    ShowMessageDialog(LocalizedString("ErrorMessageNoColorModeHeader"), LocalizedString("ErrorMessageNoColorModeBody"));
                    ScanCanceled();
                    return;
                }
                else selectedScanner.FeederConfiguration.ColorMode = (ImageScannerColorMode)selectedColorMode;

                // resolution
                ImageScannerResolution? selectedResolution = GetDesiredResolution();
                if (selectedResolution == null)
                {
                    ShowMessageDialog(LocalizedString("ErrorMessageNoResolutionHeader"), LocalizedString("ErrorMessageNoResolutionBody"));
                    ScanCanceled();
                    return;
                }
                selectedScanner.FeederConfiguration.DesiredResolution = (ImageScannerResolution)selectedResolution;

                // format
                selectedScanner.FeederConfiguration.Format = formatFlow.Item1;
            }
            else
            {
                ShowMessageDialog(LocalizedString("ErrorMessageNoConfigurationHeader"), LocalizedString("ErrorMessageNoConfigurationBody"));
            }

            // start scan and show progress and cancel button
            cancellationToken = new CancellationTokenSource();
            var progress = new Progress<UInt32>(scanProgress);

            ImageScannerScanResult result = null;

            try
            {
                ButtonCancel.Visibility = Visibility.Visible;
                result = await ScanInCorrectMode(RadioButtonSourceAutomatic, RadioButtonSourceFlatbed,
                RadioButtonSourceFeeder, scanFolder, cancellationToken, progress, selectedScanner);
            }
            catch (System.Runtime.InteropServices.COMException exc)
            {
                if (!canceledScan) ScannerError(exc);
                return;
            }
            catch (Exception)
            {
                if (!canceledScan) ScannerError();
                return;
            }

            if (!ScanResultValid(result))
            {
                if (!canceledScan) ScannerError();
                return;
            }

            if (formatFlow.Item2 != null)
            {
                // convert file
                IRandomAccessStream stream = await result.ScannedFiles[0].OpenAsync(FileAccessMode.ReadWrite);
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                Guid encoderId = GetBitmapEncoderId(formatFlow.Item2);

                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
                encoder.SetSoftwareBitmap(softwareBitmap);

                try { await encoder.FlushAsync(); }
                catch (Exception)
                {
                    ShowMessageDialog(LocalizedString("ErrorMessageConversionHeader"),
                        LocalizedString("ErrorMessageConversionBodyBeforeExtension") + result.ScannedFiles[0].FileType + LocalizedString("ErrorMessageConversionBodyAfterExtension"));
                    ScanCanceled();
                    return;
                }
                stream.Dispose();

                string newNameWithoutNumbering = RemoveNumbering(result.ScannedFiles[0].Name
                    .Replace("." + result.ScannedFiles[0].Name.Split(".")[1], "." + formatFlow.Item2));
                string newName = newNameWithoutNumbering;

                try { await result.ScannedFiles[0].RenameAsync(newName, NameCollisionOption.FailIfExists); }
                catch (Exception)
                {
                    // cycle through file numberings until one is not occupied
                    for (int i = 1; true; i++)
                    {
                        try
                        {
                            await result.ScannedFiles[0].RenameAsync(newNameWithoutNumbering.Split(".")[0] + " (" + i.ToString() 
                                + ")." + newNameWithoutNumbering.Split(".")[1], NameCollisionOption.FailIfExists);
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                        newName = newNameWithoutNumbering.Split(".")[0] + " (" + i.ToString() + ")." + newNameWithoutNumbering.Split(".")[1];
                        break;
                    }
                }

                scannedFile = await StorageFile
                    .GetFileFromPathAsync(result.ScannedFiles[0].Path.Replace(result.ScannedFiles[0].Name, newName));
            }
            else
            {
                // no need to convert
                scannedFile = result.ScannedFiles[0];
            }

            cancellationToken = null;
            imageProperties = await scannedFile.Properties.GetImagePropertiesAsync();

            if (scanNumber == 10) await ContentDialogFeedback.ShowAsync();
            localSettingsContainer.Values["scanNumber"] = ((int)localSettingsContainer.Values["scanNumber"]) + 1;

            // show result
            ButtonCancel.Visibility = Visibility.Collapsed;
            TextBlockButtonScan.Visibility = Visibility.Visible;
            ProgressRingScan.Visibility = Visibility.Collapsed;
            flowState = FlowState.result;

            // react differently to different formats
            switch (scannedFile.FileType)
            {
                case "pdf":     // result is a PDF file
                    try
                    {
                        PdfDocument doc = await PdfDocument.LoadFromFileAsync(scannedFile);
                        PdfPage page = doc.GetPage(0);
                        BitmapImage imageOfPdf = new BitmapImage();

                        using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
                        {
                            await page.RenderToStreamAsync(stream);
                            await imageOfPdf.SetSourceAsync(stream);
                        }

                        DisplayImage(imageOfPdf, ImageScanViewer);
                    }
                    catch (Exception)
                    {
                        MessageDialog errorDialog1 = new MessageDialog(LocalizedString("ErrorMessageShowResultBody"), LocalizedString("ErrorMessageShowResultHeader"));
                        errorDialog1.Commands.Add(new UICommand(LocalizedString("ErrorMessageShowResultOpenFolder"), (x) => { ButtonRecents_Click(null, null); }));
                        errorDialog1.Commands.Add(new UICommand(LocalizedString("ErrorMessageShowResultClose"), (x) => { }));
                        errorDialog1.DefaultCommandIndex = 0;
                        errorDialog1.CancelCommandIndex = 1;
                        await errorDialog1.ShowAsync();
                        ScanCanceled();
                        return;
                    }
                    ShowPrimaryMenuConfig(PrimaryMenuConfig.pdf);
                    break;
                case "xps":     // result is an XPS file
                case "oxps":    // result is an OXPS file
                    MessageDialog dialog = new MessageDialog(LocalizedString("MessageFileSavedBody"), LocalizedString("MessageFileSavedHeader"));
                    dialog.Commands.Add(new UICommand(LocalizedString("MessageFileSavedOpenFolder"), (x) => { ButtonRecents_Click(null, null); }));
                    dialog.Commands.Add(new UICommand(LocalizedString("MessageFileSavedClose"), (x) => { }));
                    dialog.DefaultCommandIndex = 0;
                    dialog.CancelCommandIndex = 1;
                    await dialog.ShowAsync();
                    flowState = FlowState.initial;
                    break;
                default:        // result is an image file (JPG/PNG/TIF/BMP)
                    try
                    {
                        DisplayImageAsync(scannedFile, ImageScanViewer);
                        await ImageCropper.LoadImageFromFile(scannedFile);
                    }
                    catch (Exception)
                    {
                        MessageDialog errorDialog2 = new MessageDialog(LocalizedString("ErrorMessageShowResultBody"), LocalizedString("ErrorMessageShowResultHeader"));
                        errorDialog2.Commands.Add(new UICommand(LocalizedString("ErrorMessageShowResultOpenFolder"), (x) => { ButtonRecents_Click(null, null); }));
                        errorDialog2.Commands.Add(new UICommand(LocalizedString("ErrorMessageShowResultClose"), (x) => { }));
                        errorDialog2.DefaultCommandIndex = 0;
                        errorDialog2.CancelCommandIndex = 1;
                        await errorDialog2.ShowAsync();
                        ScanCanceled();
                        return;
                    }
                    SetCustomAspectRatio(ToggleMenuFlyoutItemAspectRatioCustom, null);
                    ShowPrimaryMenuConfig(PrimaryMenuConfig.image);
                    break;
            }

            // send toast if the app isn't in the foreground
            if (settingNotificationScanComplete && !inForeground) SendToastNotification(LocalizedString("NotificationScanCompleteHeader"), LocalizedString("NotificationScanCompleteBody"), 5);

            // modify UI
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
        private ImageScannerColorMode? GetDesiredColorMode()
        {
            if (RadioButtonColorModeColor.IsChecked.Value) return ImageScannerColorMode.Color;
            if (RadioButtonColorModeGrayscale.IsChecked.Value) return ImageScannerColorMode.Grayscale;
            if (RadioButtonColorModeMonochrome.IsChecked.Value) return ImageScannerColorMode.Monochrome;
            return null;
        }

        private ImageScannerResolution? GetDesiredResolution()
        {
            if (ComboBoxResolution.SelectedIndex == -1) return null;
            else return new ImageScannerResolution
                            {
                                DpiX = float.Parse(((ComboBoxItem)ComboBoxResolution.SelectedItem).Tag.ToString().Split(",")[0]),
                                DpiY = float.Parse(((ComboBoxItem)ComboBoxResolution.SelectedItem).Tag.ToString().Split(",")[1])
                            };
        }

        private void ScanCanceled()
        {
            ShowPrimaryMenuConfig(PrimaryMenuConfig.hidden);
            ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
            ImageScanViewer.Visibility = Visibility.Collapsed;
            ProgressRingScan.Visibility = Visibility.Collapsed;
            ButtonCancel.Visibility = Visibility.Collapsed;
            TextBlockButtonScan.Visibility = Visibility.Visible;
            flowState = FlowState.initial;
            scannedFile = null;
            imageProperties = null;

            Page_SizeChanged(null, null);
            UI_enabled(true, true, true, true, true, true, true, true, true, true, true, true);

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
            SendToastNotification("Copied scan to the clipboard", "", 5, scannedFile.Path);
        }

        private void ButtonSettings_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage), null, new EntranceNavigationTransitionInfo());
        }

        private void ButtonSettings_Click(IUICommand command)
        {
            ButtonSettings_Click(null, null);
        }

        private void AppBarButtonShare_Click(object sender, RoutedEventArgs e)
        {
            DataTransferManager.ShowShareUI();
        }

        private async void ButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            LockCommandBar(CommandBarPrimary, null);
            LockCommandBar(CommandBarSecondary, null);
            try
            {
                await scannedFile.DeleteAsync(StorageDeleteOption.Default);
            }
            catch (Exception)
            {
                ShowMessageDialog(LocalizedString("ErrorMessageDeleteHeader"), LocalizedString("ErrorMessageDeleteBody"));
                UnlockCommandBar(CommandBarPrimary, null);
                UnlockCommandBar(CommandBarSecondary, null);
                return;
            }
            FlyoutAppBarButtonDelete.Hide();
            ShowPrimaryMenuConfig(PrimaryMenuConfig.hidden);
            ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
            ImageScanViewer.Visibility = Visibility.Collapsed;
            UnlockCommandBar(CommandBarPrimary, null);
            UnlockCommandBar(CommandBarSecondary, null);
            flowState = FlowState.initial;
        }

        private async void ButtonRename_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await scannedFile.RenameAsync(TextBoxRename.Text + "." + scannedFile.Name.Split(".")[1], NameCollisionOption.FailIfExists);
            }
            catch (Exception)
            {
                ShowMessageDialog(LocalizedString("ErrorMessageRenameHeader"), LocalizedString("ErrorMessageRenameBody"));
                return;
            }
            FlyoutAppBarButtonRename.Hide();
        }        

        private void FlyoutAppBarButtonRename_Opening(object sender, object e)
        {
            TextBoxRename.Text = scannedFile.Name.Split(".")[0];
        }

        private void AppBarButtonCrop_Checked(object sender, RoutedEventArgs e)
        {
            // deactivate all buttons
            LockCommandBar(CommandBarPrimary, null);

            flowState = FlowState.crop;

            // make sure that the ImageCropper won't be obstructed
            ImageCropper.Padding = new Thickness(24,
                24 + CoreApplication.GetCurrentView().TitleBar.Height + CommandBarSecondary.ActualHeight +
                DropShadowPanelCommandBarSecondary.Margin.Top, 24, 24 + CommandBarPrimary.ActualHeight +
                DropShadowPanelCommandBarPrimary.Margin.Bottom);

            // show ImageCropper and secondary commands
            ImageCropper.Visibility = Visibility.Visible;
            ShowSecondaryMenuConfig(SecondaryMenuConfig.crop);
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
            LockCommandBar(CommandBarPrimary, null);
            LockCommandBar(CommandBarSecondary, null);
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
                ShowMessageDialog(LocalizedString("ErrorMessageRotateHeader"), LocalizedString("ErrorMessageRotateBody"));
            }

            DisplayImageAsync(scannedFile, ImageScanViewer);
            stream.Dispose();
            await ImageCropper.LoadImageFromFile(scannedFile);

            UnlockCommandBar(CommandBarPrimary, null);
            UnlockCommandBar(CommandBarSecondary, null);
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
                    DropShadowPanelRight.ShadowOpacity = 0.2;
                }
            }
            else
            {
                if (settingAppTheme == Theme.light) DropShadowPanelRight.ShadowOpacity = 0.2;
                else DropShadowPanelRight.ShadowOpacity = 0.6;
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
            flowState = FlowState.initial;
            ShowPrimaryMenuConfig(PrimaryMenuConfig.hidden);
            ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
            ImageScanViewer.Visibility = Visibility.Collapsed;
            Page_SizeChanged(null, null);
        }

        private void ScrollViewerScan_LayoutUpdated(object sender, object e)
        {
            // fix image, might otherwise slip outside the window's boundaries
            ImageScanViewer.MaxWidth = ScrollViewerScan.ActualWidth;
            ImageScanViewer.MaxHeight = ScrollViewerScan.ActualHeight;

            ViewBoxScan.Width = ImageScanViewer.ActualWidth;
            ViewBoxScan.Height = ImageScanViewer.ActualHeight;
        }

        private void ComboBoxScanners_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxScanners.IsEnabled = false;
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

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            if (cancellationToken != null)
            {
                canceledScan = true;
                try { cancellationToken.Cancel(); }
                catch (Exception)
                {
                    ShowMessageDialog(LocalizedString("ErrorMessageScanCancelHeader"), LocalizedString("ErrorMessageScanCancelBody"));
                    return;
                }
            }
            cancellationToken = null;
            ScanCanceled();
        }


        private void ScannerError(System.Runtime.InteropServices.COMException exc)
        {
            // scanner error while scanning
            if (!inForeground)
            {
                SendToastNotification(LocalizedString("NotificationScanErrorHeader"),
                    LocalizedString("NotificationScanErrorBody"), 5);
            }
            ShowMessageDialog(LocalizedString("ErrorMessageScanScannerErrorHeader"),
                    LocalizedString("ErrorMessageScanScannerErrorBody") + "\n" + exc.HResult);
            ScanCanceled();
            return;
        }


        private void ScannerError()
        {
            // unknown error while scanning
            if (!inForeground)
            {
                SendToastNotification(LocalizedString("NotificationScanErrorHeader"),
                    LocalizedString("NotificationScanErrorBody"), 5);
            }
            ShowMessageDialog(LocalizedString("ErrorMessageScanErrorHeader"),
                    LocalizedString("ErrorMessageScanErrorBody"));
            ScanCanceled();
            return;
        }

        private void SetFixedAspectRatio(object sender, RoutedEventArgs e)
        {
            // only check selected item
            foreach (var item in MenuFlyoutAspectRatio.Items)
            {
                try { ((ToggleMenuFlyoutItem)item).IsChecked = false; }
                catch (InvalidCastException) { }
            }
            ((ToggleMenuFlyoutItem)sender).IsChecked = true;

            // set aspect ratio according to tag
            ImageCropper.AspectRatio = 1.0 / double.Parse(((ToggleMenuFlyoutItem)sender).Tag.ToString());
        }

        private void SetCustomAspectRatio(object sender, RoutedEventArgs e)
        {
            // only check selected item
            foreach (var item in MenuFlyoutAspectRatio.Items)
            {
                try { ((ToggleMenuFlyoutItem)item).IsChecked = false; }
                catch (InvalidCastException) { }
            }
            ((ToggleMenuFlyoutItem)sender).IsChecked = true;

            // set aspect ratio to custom
            ImageCropper.AspectRatio = null;
        }

        private void AppBarButtonDiscard_Click(object sender, RoutedEventArgs e)
        {
            switch (flowState)
            {
                case FlowState.crop:
                    // return UI to normal
                    ImageCropper.Visibility = Visibility.Collapsed;
                    flowState = FlowState.result;
                    AppBarButtonCrop.IsChecked = false;
                    UnlockCommandBar(CommandBarPrimary, null);

                    if (uiState != UIstate.small_result) ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
                    else ShowSecondaryMenuConfig(SecondaryMenuConfig.done);
                    break;
                case FlowState.draw:
                    // return UI to normal
                    InkCanvasScan.Visibility = Visibility.Collapsed;
                    InkCanvasScan.InkPresenter.StrokeContainer.Clear();
                    flowState = FlowState.result;
                    AppBarButtonDraw.IsChecked = false;
                    UnlockCommandBar(CommandBarPrimary, null);

                    if (uiState != UIstate.small_result) ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
                    else ShowSecondaryMenuConfig(SecondaryMenuConfig.done);
                    break;
            }
        }

        private async void AppBarButtonSave_Click(object sender, RoutedEventArgs e)
        {
            LockCommandBar(CommandBarPrimary, null);
            LockCommandBar(CommandBarSecondary, null);

            switch (flowState)
            {
                case FlowState.crop:
                    // save file
                    IRandomAccessStream stream = null;
                    try
                    {
                        stream = await scannedFile.OpenAsync(FileAccessMode.ReadWrite);
                        await ImageCropper.SaveAsync(stream, GetBitmapFileFormat(scannedFile), true);
                    }
                    catch (Exception)
                    {
                        ShowMessageDialog(LocalizedString("ErrorMessageSaveHeader"), LocalizedString("ErrorMessageSaveBody"));
                        try { stream.Dispose(); } catch (Exception) { }
                        return;
                    }
                    
                    stream.Dispose();

                    // refresh preview and properties
                    DisplayImageAsync(scannedFile, ImageScanViewer);
                    imageProperties = await scannedFile.Properties.GetImagePropertiesAsync();

                    // return UI to normal
                    if (uiState != UIstate.small_result) ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
                    else ShowSecondaryMenuConfig(SecondaryMenuConfig.done);
                    
                    flowState = FlowState.result;
                    AppBarButtonCrop.IsChecked = false;
                    ImageCropper.Visibility = Visibility.Collapsed;
                    break;
                case FlowState.draw:
                    // save file
                    stream = null;
                    try
                    {
                        CanvasDevice device = CanvasDevice.GetSharedDevice();
                        CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (int)InkCanvasScan.ActualWidth, (int)InkCanvasScan.ActualHeight, 96);
                        stream = await scannedFile.OpenAsync(FileAccessMode.ReadWrite);
                        CanvasBitmap canvasBitmap = await CanvasBitmap.LoadAsync(device, stream);

                        using (var ds = renderTarget.CreateDrawingSession())
                        {
                            ds.Clear(Windows.UI.Colors.White);

                            ds.DrawImage(canvasBitmap);
                            ds.DrawInk(InkCanvasScan.InkPresenter.StrokeContainer.GetStrokes());
                        }

                        await renderTarget.SaveAsync(stream, GetCanvasBitmapFileFormat(scannedFile), 1f);
                    } catch (Exception)
                    {
                        ShowMessageDialog(LocalizedString("ErrorMessageSaveHeader"), LocalizedString("ErrorMessageSaveBody"));
                        try { stream.Dispose(); } catch (Exception) { }
                        return;
                    }

                    stream.Dispose();

                    // refresh preview
                    DisplayImageAsync(scannedFile, ImageScanViewer);

                    // return UI to normal
                    if (uiState != UIstate.small_result) ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
                    else ShowSecondaryMenuConfig(SecondaryMenuConfig.done);

                    flowState = FlowState.result;
                    AppBarButtonDraw.IsChecked = false;
                    InkCanvasScan.Visibility = Visibility.Collapsed;
                    break;
            }

            UnlockCommandBar(CommandBarPrimary, null);
            UnlockCommandBar(CommandBarSecondary, null);
        }

        private async void AppBarButtonSaveCopy_Click(object sender, RoutedEventArgs e)
        {
            LockCommandBar(CommandBarSecondary, null);

            switch (flowState)
            {
                case FlowState.crop:
                    // save as new file
                    IRandomAccessStream stream = null;
                    try
                    {
                        StorageFolder folder = await scannedFile.GetParentAsync();
                        StorageFile file = await folder.CreateFileAsync(scannedFile.Name, CreationCollisionOption.GenerateUniqueName);
                        stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                        await ImageCropper.SaveAsync(stream, GetBitmapFileFormat(scannedFile), true);
                    }
                    catch (Exception)
                    {
                        ShowMessageDialog(LocalizedString("ErrorMessageSaveHeader"), LocalizedString("ErrorMessageSaveBody"));
                        try { stream.Dispose(); } catch (Exception) { }
                        return;
                    }

                    stream.Dispose();

                    break;
                case FlowState.draw:
                    // save as new file
                    stream = null;
                    try
                    {
                        CanvasDevice device = CanvasDevice.GetSharedDevice();
                        CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (int)InkCanvasScan.ActualWidth, (int)InkCanvasScan.ActualHeight, 96);

                        StorageFolder folder = await scannedFile.GetParentAsync();
                        StorageFile file = await folder.CreateFileAsync(scannedFile.Name, CreationCollisionOption.GenerateUniqueName);

                        stream = await scannedFile.OpenAsync(FileAccessMode.Read);
                        CanvasBitmap canvasBitmap = await CanvasBitmap.LoadAsync(device, stream);

                        using (var ds = renderTarget.CreateDrawingSession())
                        {
                            ds.Clear(Windows.UI.Colors.White);

                            ds.DrawImage(canvasBitmap);
                            ds.DrawInk(InkCanvasScan.InkPresenter.StrokeContainer.GetStrokes());
                        }

                        stream.Dispose();

                        stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                        await renderTarget.SaveAsync(stream, GetCanvasBitmapFileFormat(file), 1f);
                        stream.Dispose();
                    }
                    catch (Exception)
                    {
                        ShowMessageDialog(LocalizedString("ErrorMessageSaveHeader"), LocalizedString("ErrorMessageSaveBody"));
                        try { stream.Dispose(); } catch (Exception) { }
                        return;
                    }

                    break;
            }

            UnlockCommandBar(CommandBarSecondary, null);
        }

        private void ShowPrimaryMenuConfig(PrimaryMenuConfig config)
        {
            switch (config)
            {
                case PrimaryMenuConfig.hidden:
                    CommandBarPrimary.Visibility = Visibility.Collapsed;
                    break;
                case PrimaryMenuConfig.image:
                    AppBarButtonCrop.Visibility = Visibility.Visible;
                    AppBarButtonRotate.Visibility = Visibility.Visible;
                    AppBarButtonDraw.Visibility = Visibility.Visible;

                    ToolbarSeparatorPrimaryOne.Visibility = Visibility.Visible;

                    AppBarButtonRename.Visibility = Visibility.Visible;
                    AppBarButtonDelete.Visibility = Visibility.Visible;

                    ToolbarSeparatorPrimaryTwo.Visibility = Visibility.Visible;

                    AppBarButtonCopy.Visibility = Visibility.Visible;
                    AppBarButtonShare.Visibility = Visibility.Visible;

                    CommandBarPrimary.Visibility = Visibility.Visible;
                    break;
                case PrimaryMenuConfig.pdf:
                    AppBarButtonCrop.Visibility = Visibility.Collapsed;
                    AppBarButtonRotate.Visibility = Visibility.Collapsed;
                    AppBarButtonDraw.Visibility = Visibility.Collapsed;

                    ToolbarSeparatorPrimaryOne.Visibility = Visibility.Collapsed;

                    AppBarButtonRename.Visibility = Visibility.Visible;
                    AppBarButtonDelete.Visibility = Visibility.Visible;

                    ToolbarSeparatorPrimaryTwo.Visibility = Visibility.Visible;

                    AppBarButtonCopy.Visibility = Visibility.Visible;
                    AppBarButtonShare.Visibility = Visibility.Visible;

                    CommandBarPrimary.Visibility = Visibility.Visible;
                    break;
                default:
                    break;
            }
        }

        private void ShowSecondaryMenuConfig(SecondaryMenuConfig config)
        {
            switch (config)
            {
                case SecondaryMenuConfig.hidden:
                    CommandBarSecondary.Visibility = Visibility.Collapsed;
                    break;
                case SecondaryMenuConfig.done:
                    AppBarButtonDone.Visibility = Visibility.Visible;

                    ToolbarSeparatorSecondary.Visibility = Visibility.Collapsed;

                    InkToolbarScan.Visibility = Visibility.Collapsed;
                    AppBarButtonAspectRatio.Visibility = Visibility.Collapsed;
                    AppBarButtonSave.Visibility = Visibility.Collapsed;
                    AppBarButtonSaveCopy.Visibility = Visibility.Collapsed;
                    AppBarButtonDiscard.Visibility = Visibility.Collapsed;

                    CommandBarSecondary.Visibility = Visibility.Visible;
                    break;
                case SecondaryMenuConfig.crop:
                    AppBarButtonDone.Visibility = Visibility.Collapsed;

                    ToolbarSeparatorSecondary.Visibility = Visibility.Visible;

                    InkToolbarScan.Visibility = Visibility.Collapsed;
                    AppBarButtonAspectRatio.Visibility = Visibility.Visible;
                    AppBarButtonSave.Visibility = Visibility.Visible;
                    AppBarButtonSaveCopy.Visibility = Visibility.Visible;
                    AppBarButtonDiscard.Visibility = Visibility.Visible;

                    CommandBarSecondary.Visibility = Visibility.Visible;
                    break;
                case SecondaryMenuConfig.draw:
                    AppBarButtonDone.Visibility = Visibility.Collapsed;

                    ToolbarSeparatorSecondary.Visibility = Visibility.Visible;

                    InkToolbarScan.Visibility = Visibility.Visible;
                    AppBarButtonAspectRatio.Visibility = Visibility.Collapsed;
                    AppBarButtonSave.Visibility = Visibility.Visible;
                    AppBarButtonSaveCopy.Visibility = Visibility.Visible;
                    AppBarButtonDiscard.Visibility = Visibility.Visible;

                    CommandBarSecondary.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void ToggleMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {   
            ImageCropper.AspectRatio = ImageCropper.CroppedRegion.Height / ImageCropper.CroppedRegion.Width;
        }

        private void AppBarButtonDraw_Checked(object sender, RoutedEventArgs e)
        {
            // deactivate all buttons
            LockCommandBar(CommandBarPrimary, null);

            flowState = FlowState.draw;

            // show InkCanvas and secondary commands
            ShowSecondaryMenuConfig(SecondaryMenuConfig.draw);
            InitializeInkCanvas(InkCanvasScan, imageProperties);
            InkCanvasScan.Visibility = Visibility.Visible;
        }

        private void InkCanvasScan_PointerEntered(InkUnprocessedInput input, PointerEventArgs e)
        {
            if (flowState == FlowState.draw && e.CurrentPoint.PointerDevice.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Pen)
            {
                ShowPrimaryMenuConfig(PrimaryMenuConfig.hidden);
                ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
            }
        }

        private void InkCanvasScan_PointerExited(InkUnprocessedInput input, PointerEventArgs e)
        {
            if (flowState == FlowState.draw)
            {
                ShowPrimaryMenuConfig(PrimaryMenuConfig.image);
                ShowSecondaryMenuConfig(SecondaryMenuConfig.draw);
            }
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
