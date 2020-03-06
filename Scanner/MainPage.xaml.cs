using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Pdf;
using Windows.Devices.Enumeration;
using Windows.Devices.Input;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

using static Enums;
using static Globals;
using static ScannerOperation;
using static Utilities;


namespace Scanner
{
    public sealed partial class MainPage : Page
    {
        private DeviceWatcher scannerWatcher;
        private ObservableCollection<ComboBoxItem> scannerList = new ObservableCollection<ComboBoxItem>();
        private List<DeviceInformation> deviceInformation = new List<DeviceInformation>();
        CancellationTokenSource cancellationToken = null;

        private ImageScanner selectedScanner = null;
        private StorageFile scannedFile {
            get { return _scannedFile; }
            set { 
                _scannedFile = value;
                if (value != null && _scannedFile.DisplayName != null)
                {
                    TextBlockCommandBarPrimaryFileName.Text = _scannedFile.DisplayName;
                    TextBlockCommandBarPrimaryFileExtension.Text = _scannedFile.FileType;
                }
            }
        }
        private StorageFile _scannedFile;
        private Tuple<double, double> imageMeasurements;

        private double ColumnLeftDefaultMaxWidth;
        private double ColumnLeftDefaultMinWidth;
        private bool inForeground = true;
        private bool debugShortcutActive = false;

        private ObservableCollection<ComboBoxItem> formats = new ObservableCollection<ComboBoxItem>();
        private ObservableCollection<ComboBoxItem> resolutions = new ObservableCollection<ComboBoxItem>();
        
        private UIstate uiState = UIstate.unset;
        private FlowState flowState = FlowState.initial;
        private bool canceledScan = false;
        private bool firstLoaded = true;

        private event TypedEventHandler<DeviceWatcher, object> eventHandlerScannerWatcherStopped;

        DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();


        public MainPage()
        {
            this.InitializeComponent();

            // localize hyperlink
            ((Windows.UI.Xaml.Documents.Run)HyperlinkSettings.Inlines[0]).Text = LocalizedString("HyperlinkScannerSelectionHintBodyLink");
            ((Windows.UI.Xaml.Documents.Run)HyperlinkFeedbackHub.Inlines[0]).Text = LocalizedString("HyperlinkSettingsFeedbackLink");
            ((Windows.UI.Xaml.Documents.Run)HyperlinkRate.Inlines[0]).Text = LocalizedString("HyperlinkSettingsRateLink");

            Page_ActualThemeChanged(null, null);

            // initialize veriables
            ColumnLeftDefaultMaxWidth = ColumnLeft.MaxWidth;
            ColumnLeftDefaultMinWidth = ColumnLeft.MinWidth;

            // populate the scanner list
            AddIndicatorComboBoxItem(scannerList);
            scannerWatcher = DeviceInformation.CreateWatcher(DeviceClass.ImageScanner);
            scannerWatcher.Added += OnScannerAdded;
            scannerWatcher.Removed += OnScannerRemoved;
            scannerWatcher.EnumerationCompleted += OnScannerEnumerationComplete;
            eventHandlerScannerWatcherStopped = async (x, y) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    ComboBoxScanners.SelectedIndex = -1;
                    selectedScanner = null;
                    deviceInformation.Clear();
                    refreshLeftPanel();
                    scannerWatcher.Start();
                    ComboBoxScanners.IsEnabled = true;
                    scannerWatcher.Stopped -= eventHandlerScannerWatcherStopped;
                });
            };
            scannerWatcher.Stopped += eventHandlerScannerWatcherStopped;
            scannerWatcher.Start();

            // allow for mouse input on the InkCanvas
            InkCanvasScan.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Pen;

            // setup hyperlinks
            HyperlinkSettings.Click += async (x, y) =>
            {
                await Launcher.LaunchUriAsync(new Uri("ms-settings:printers"));
            };

            // setup TeachingTips
            TeachingTipError.ActionButtonClick += (x, y) =>
            {
                TeachingTipError.IsOpen = false;
                ButtonSettings_Click(null, null);
            };
            TeachingTipDevices.ActionButtonClick += async (x, y) =>
            {
                await Launcher.LaunchUriAsync(new Uri("ms-settings:printers"));
            };

            // compatibility stuff /////////////////////////////////////////////////////////////////////
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
            {
                // v1809+
                TeachingTipError.ActionButtonStyle = RoundedButtonAccentStyle;
                TeachingTipError.CloseButtonStyle = RoundedButtonStyle;

                TeachingTipSaveLocation.ActionButtonStyle = RoundedButtonAccentStyle;

                TeachingTipDevices.ActionButtonStyle = RoundedButtonAccentStyle;
            }

            // register event listeners ////////////////////////////////////////////////////////////////
            InkCanvasScan.InkPresenter.UnprocessedInput.PointerEntered += InkCanvasScan_PointerEntered;
            InkCanvasScan.InkPresenter.UnprocessedInput.PointerExited += InkCanvasScan_PointerExited;
            dataTransferManager.DataRequested += DataTransferManager_DataRequested;
            CoreApplication.EnteredBackground += (x, y) => { inForeground = false; };
            CoreApplication.LeavingBackground += (x, y) => 
            {
                inForeground = true;
                Page_ActualThemeChanged(null, null);
            };
            CoreApplication.GetCurrentView().TitleBar.LayoutMetricsChanged += (titleBar, y) => {
                ScrollViewerLeftPanel.Margin = new Thickness(0, titleBar.Height, 0, 0);
            };
            Window.Current.CoreWindow.KeyDown += MainPage_KeyDown;
            Window.Current.CoreWindow.KeyUp += MainPage_KeyUp;

            LoadScanFolder();
        }


        /// <summary>
        ///     Opens the Windows 10 sharing panel with <see cref="scannedFile.Name"/> as title.
        /// </summary>
        private void DataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            args.Request.Data.SetBitmap(RandomAccessStreamReference.CreateFromFile(scannedFile));
            args.Request.Data.Properties.Title = scannedFile.Name;
        }


        /// <summary>
        ///     Refreshes the left panel according to the currently selected item of <see cref="ComboBoxScanners"/>.
        ///     If necessary, it loads the correct <see cref="ImageScanner"/> into <see cref="selectedScanner"/>.
        ///     Enables all scanning mode <see cref="RadioButton"/>s according to the supported modes of the selected
        ///     scanner.
        ///     If an error occurs while attempting to retrieve the new scanner, an error message is displayed, the
        ///     scanner list is emptied and automatic scanner selection is disabled through <see cref="possiblyDeadScanner"/>
        ///     until a scanner's information could be successfully retrieved again.
        /// </summary>
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
                ComboBoxFormat.SelectedIndex = -1;

                // hide flatbed/feeder-specific options
                StackPanelColor.Visibility = Visibility.Collapsed;
                StackPanelResolution.Visibility = Visibility.Collapsed;
            }
            else
            {
                // scanner selected ///////////////////////////////////////////////////////////////////////
                // hide text on the right side
                StackPanelTextRight.Visibility = Visibility.Collapsed;
                ButtonDevices.Visibility = Visibility.Visible;

                if (selectedScanner == null || selectedScanner.DeviceId.ToLower() != ((string) ((ComboBoxItem) ComboBoxScanners.SelectedItem).Tag).ToLower())
                {
                    // previously different/no scanner selected ////////////////////////////////////////////////////
                    // get scanner's DeviceInformation
                    RadioButtonSourceAutomatic.IsChecked = false;
                    RadioButtonSourceFlatbed.IsChecked = false;
                    RadioButtonSourceFeeder.IsChecked = false;

                    foreach (DeviceInformation check in deviceInformation)
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
                                ShowContentDialog(LocalizedString("ErrorMessageScannerInformationHeader"),
                                    LocalizedString("ErrorMessageScannerInformationBody") + "\n" + exc.Message);

                                // (almost) start from scratch to hopefully get rid of dead scanners
                                possiblyDeadScanner = true;
                                scannerList.Clear();
                                AddIndicatorComboBoxItem(scannerList);
                                Page_SizeChanged(null, null);
                                scannerWatcher.Stop();
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

                // refresh color modes
                bool colorAllowed = false, grayscaleAllowed = false, monochromeAllowed = false;

                if (RadioButtonSourceFlatbed.IsChecked == true)
                {
                    colorAllowed = selectedScanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                    grayscaleAllowed = selectedScanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                    monochromeAllowed = selectedScanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);
                } else if (RadioButtonSourceFeeder.IsChecked == true)
                {
                    colorAllowed = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                    grayscaleAllowed = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                    monochromeAllowed = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);
                }

                UI_enabled(true, autoAllowed, flatbedAllowed, feederAllowed, colorAllowed, grayscaleAllowed, monochromeAllowed, true, true, true, true, true);
            }
        }


        /// <summary>
        ///     The event listener for when the <see cref="scannerWatcher"/> finds a new scanner. Adds the scanner to
        ///     <see cref="scannerList"/> (as <see cref="ComboBoxItem"/>) and its <paramref name="deviceInfo"/> 
        ///     to <see cref="deviceInformations"/> if it isn't identified as duplicate.
        /// </summary>
        /// <remarks>
        ///     The <see cref="ComboBoxItem"/>s added to <see cref="scannerList"/> contain the scanner's name as
        ///     content and its ID as tag. 
        /// </remarks>
        private async void OnScannerAdded(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
                {
                    bool duplicate = false;

                    foreach (var check in scannerList)
                    {
                        if ((string) check.Tag == deviceInfo.Id)
                        {
                            duplicate = true;
                            break;
                        }
                    }

                    if (!duplicate)
                    {
                        ComboBoxItem item = new ComboBoxItem();
                        item.Content = deviceInfo.Name;
                        item.Tag = deviceInfo.Id;

                        deviceInformation.Add(deviceInfo);
                        scannerList.Insert(0, item);
                    }
                    else return;

                    // auto select first added scanner (if some requirements are met)
                    if (!possiblyDeadScanner && !ComboBoxScanners.IsDropDownOpen && settingAutomaticScannerSelection
                        && selectedScanner == null && deviceInformation.Count == 1)
                    {
                        ComboBoxScanners.SelectedIndex = 0;
                    }
                }
            );
        }


        /// <summary>
        ///     The event listener for when the <see cref="scannerWatcher"/> removes a previously found scanner. The
        ///     <see cref="scannerList"/> is searched by ID (added to the <see cref="ComboBoxItem"/>s as tag and
        ///     if a matching item is found, it is removed.
        /// </summary>
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
                        ComboBoxScanners.SelectedIndex = -1;
                        scannerList.Remove(item);

                        foreach (DeviceInformation check in deviceInformation)
                        {
                            if (check.Id == deviceInfoUpdate.Id)
                            {
                                deviceInformation.Remove(check);
                                break;
                            }
                        }
                        break;
                    }
                }

            });
        }


        /// <summary>
        ///     The event listener for when the <see cref="scannerWatcher"/> has finished adding all initially
        ///     available scanners.
        /// </summary>
        private async void OnScannerEnumerationComplete(DeviceWatcher sender, Object theObject)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
                {
                    if (ComboBoxScanners.SelectedIndex == -1)
                    {
                        UI_enabled(true, false, false, false, false, false, false, false, false, false, true, true);
                    }
                }
            );
        }


        /// <summary>
        ///     The event listener for when the page's size has changed. Responsible for the responsive design.
        /// </summary>
        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // responsive behavior
            double width = ((Frame)Window.Current.Content).ActualWidth;
            if (width < 700)
            {
                // small ////////////////////////////////////////////////////////
                StackPanelTextRight.Visibility = Visibility.Collapsed;
                ButtonDevices.Visibility = Visibility.Visible;
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
                ButtonDevices.Visibility = Visibility.Visible;
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

                if (selectedScanner == null && flowState == FlowState.initial)
                {
                    StackPanelTextRight.Visibility = Visibility.Visible;
                    ButtonDevices.Visibility = Visibility.Collapsed;
                }
                else
                {
                    StackPanelTextRight.Visibility = Visibility.Collapsed;
                    ButtonDevices.Visibility = Visibility.Visible;
                }

                uiState = UIstate.full;
            }
        }


        /// <summary>
        ///     Opens the currently selected scan folder and selects the currently visible result if possible.
        /// </summary>
        private async void ButtonRecents_Click(object sender, RoutedEventArgs e)
        {
            if (flowState != FlowState.result && flowState != FlowState.crop && flowState != FlowState.draw)
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


        /// <summary>
        ///     Enables and disables controls in the left panel.
        /// </summary>
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
        }


        /// <summary>
        ///     Gathers all options, runs multiple checks and then conducts a scan using <see cref="selectedScanner"/>. The result
        ///     is saved to <see cref="scanFolder"/> and afterwards available as <see cref="scannedFile"/>. 
        ///     Includes a debugging exit, which opens a file picker instead of scanning, if <see cref="debugShortcutActive"/>
        ///     is true.
        /// </summary>
        /// <remarks>
        ///     Returns the app to its initial state and disables/hides all buttons apart from the <see cref="ButtonRecents"/>.
        ///     If <see cref="scanFolder"/> is null, an error is shown and the scan doesn't commence.
        ///     If gathering the options (configuration, color mode, resolution, file format) fails, an error message
        ///     is shown and the scan doesn't commence.
        ///     Catches exceptions that may occur while scanning, displays an error message and returns to the initial
        ///     state - the same happens if the result is deemed invalid by <see cref="ScanResultValid(ImageScannerScanResult)"/>.
        ///     If the target format is only supported through conversion, the method converts the base file and replaces it with
        ///     the new one. In case the conversion fails, an error message is shown and the base file left intact.
        ///     Shows the result in the right pane if it's an image or pdf file, otherwise a <see cref="MessageDialog"/> is
        ///     shown that allows the user to show the file in its folder. Pdf files are displaying by converting the first page
        ///     to a bitmap file.
        ///     Triggers the transition to the <see cref="FlowState.result"/> (if a preview is available) and updates the UI
        ///     accordingly with <see cref="CommandBar"/>s and all of the other fancy stuff.
        ///     If the scan is completed while <see cref="inForeground"/> is false, a ToastNotification is sent.
        ///     As the final step the buttons in the left panel are reenabled.
        ///     Simple, right?
        /// 
        ///     Should support multi-page scans, but this is more of an experimental feature.
        /// </remarks>
        private async void Scan(object sender, RoutedEventArgs e)
        {
            // DEBUGGING EXIT
            if (debugShortcutActive)
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".tif");
                picker.FileTypeFilter.Add(".bmp");
                picker.FileTypeFilter.Add(".pdf");
                picker.FileTypeFilter.Add(".xps");
                picker.FileTypeFilter.Add(".oxps");
                scannedFile = await picker.PickSingleFileAsync();
                if (scannedFile == null) return;
            } else {
                // lock (almost) entire left panel and clean up right side
                UI_enabled(false, false, false, false, false, false, false, false, false, false, false, true);
                ShowPrimaryMenuConfig(PrimaryMenuConfig.hidden);
                ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
                ImageScanViewer.Visibility = Visibility.Collapsed;
                TextBlockButtonScan.Visibility = Visibility.Collapsed;
                ProgressRingScan.Visibility = Visibility.Visible;
                ScrollViewerScan.ChangeView(0, 0, 1);
                AppBarButtonDiscard_Click(null, null);                              // cancel crop/drawing mode if necessary
                TeachingTipDevices.IsOpen = false;
                TeachingTipSaveLocation.IsOpen = false;
                ButtonDevices.IsEnabled = false;

                canceledScan = false;

                if (scanFolder == null)
                {
                    TeachingTipError.Target = ButtonScan;
                    TeachingTipError.Title = LocalizedString("ErrorMessageScanFolderHeader");
                    TeachingTipError.Subtitle = LocalizedString("ErrorMessageScanFolderBody");
                    TeachingTipError.ActionButtonContent = LocalizedString("ErrorMessageScanFolderSettings");
                    ScanCanceled();
                    TeachingTipError.IsOpen = true;
                    return;
                }

                // gather options ///////////////////////////////////////////////////////////////////////////////
                Tuple<ImageScannerFormat, SupportedFormat?> formatFlow = GetDesiredFormat(ComboBoxFormat, formats);
                if (formatFlow == null)
                {
                    ShowFeedbackContentDialog(LocalizedString("ErrorMessageNoFormatHeader"), LocalizedString("ErrorMessageNoFormatBody"));
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
                        ShowFeedbackContentDialog(LocalizedString("ErrorMessageNoColorModeHeader"), LocalizedString("ErrorMessageNoColorModeBody"));
                        ScanCanceled();
                        return;
                    }
                    else selectedScanner.FlatbedConfiguration.ColorMode = (ImageScannerColorMode)selectedColorMode;

                    // resolution
                    ImageScannerResolution? selectedResolution = GetDesiredResolution(ComboBoxResolution);
                    if (selectedResolution == null)
                    {
                        ShowFeedbackContentDialog(LocalizedString("ErrorMessageNoResolutionHeader"), LocalizedString("ErrorMessageNoResolutionBody"));
                        ScanCanceled();
                        return;
                    }
                    selectedScanner.FlatbedConfiguration.DesiredResolution = (ImageScannerResolution)selectedResolution;

                    // format
                    selectedScanner.FlatbedConfiguration.Format = formatFlow.Item1;
                }
                else if (RadioButtonSourceFeeder.IsChecked.Value)           // feeder configuration /////////////
                {
                    // color mode
                    ImageScannerColorMode? selectedColorMode = GetDesiredColorMode();
                    if (selectedColorMode == null)
                    {
                        ShowFeedbackContentDialog(LocalizedString("ErrorMessageNoColorModeHeader"), LocalizedString("ErrorMessageNoColorModeBody"));
                        ScanCanceled();
                        return;
                    }
                    else selectedScanner.FeederConfiguration.ColorMode = (ImageScannerColorMode)selectedColorMode;

                    // resolution
                    ImageScannerResolution? selectedResolution = GetDesiredResolution(ComboBoxResolution);
                    if (selectedResolution == null)
                    {
                        ShowFeedbackContentDialog(LocalizedString("ErrorMessageNoResolutionHeader"), LocalizedString("ErrorMessageNoResolutionBody"));
                        ScanCanceled();
                        return;
                    }
                    selectedScanner.FeederConfiguration.DesiredResolution = (ImageScannerResolution)selectedResolution;

                    // format
                    selectedScanner.FeederConfiguration.Format = formatFlow.Item1;
                }
                else
                {
                    ShowFeedbackContentDialog(LocalizedString("ErrorMessageNoConfigurationHeader"), LocalizedString("ErrorMessageNoConfigurationBody"));
                }

                // start scan ///////////////////////////////////////////////////////////////////////////////////
                cancellationToken = new CancellationTokenSource();
                var progress = new Progress<UInt32>(scanProgress);

                ImageScannerScanResult result = null;
                ButtonCancel.Visibility = Visibility.Visible;

                StorageItemAccessList futureAccessList = StorageApplicationPermissions.FutureAccessList;
                try { scanFolder = await futureAccessList.GetFolderAsync("scanFolder"); }
                catch (Exception)
                {
                    TeachingTipError.Target = ButtonScan;
                    TeachingTipError.Title = LocalizedString("ErrorMessageScanFolderHeader");
                    TeachingTipError.Subtitle = LocalizedString("ErrorMessageScanFolderBody");
                    TeachingTipError.ActionButtonContent = LocalizedString("ErrorMessageScanFolderSettings");
                    ScanCanceled();
                    TeachingTipError.IsOpen = true;
                    return;
                }

                try
                {
                    if (formatFlow.Item2 == SupportedFormat.PDF)
                    {
                        // conversion to PDF: save result to temporary files for win32 component
                        result = await ScanInCorrectMode(RadioButtonSourceAutomatic, RadioButtonSourceFlatbed,
                            RadioButtonSourceFeeder, ApplicationData.Current.TemporaryFolder, cancellationToken,
                            progress, selectedScanner);
                    }
                    else
                    {
                        // save result to target folder right away
                        result = await ScanInCorrectMode(RadioButtonSourceAutomatic, RadioButtonSourceFlatbed,
                            RadioButtonSourceFeeder, scanFolder, cancellationToken, progress, selectedScanner);
                    }
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

                if (result.ScannedFiles.Count > 1)
                {
                    ShowFeedbackContentDialog(LocalizedString("ErrorMessageMultipleDocumentsUnsupportedHeader"), LocalizedString("ErrorMessageMultipleDocumentsUnsupportedBody"));
                }

                if (formatFlow.Item2 != null)
                {
                    // files need to be converted
                    string convertedFileName;
                    bool firstFile = true;
                    foreach (StorageFile scan in result.ScannedFiles)
                    {
                        try
                        {
                            convertedFileName = await ConvertScannedFile(scan, formatFlow.Item2, scanFolder);

                            if (firstFile)
                            {
                                // make first file of possible batch the one that can be edited
                                scannedFile = await scanFolder.GetFileAsync(convertedFileName);
                                firstFile = false;
                            }
                        }
                        catch (Exception)
                        {
                            if (formatFlow.Item2 == SupportedFormat.PDF)
                            {
                                ShowFeedbackContentDialog(LocalizedString("ErrorMessageConversionHeader"), LocalizedString("ErrorMessageConversionBodyPDF"));
                            }
                            else
                            {
                                ShowFeedbackContentDialog(LocalizedString("ErrorMessageConversionHeader"),
                                    LocalizedString("ErrorMessageConversionBodyBeforeExtension") + scan.FileType + LocalizedString("ErrorMessageConversionBodyAfterExtension"));
                            }
                            ScanCanceled();
                            return;
                        }
                    }
                }
                else
                {
                    // no need to convert
                    scannedFile = result.ScannedFiles[0];
                }
            }

            // show result //////////////////////////////////////////////////////////////////////////////////
            ButtonCancel.Visibility = Visibility.Collapsed;
            cancellationToken = null;

            // react differently to different formats
            switch (ConvertFormatStringToSupportedFormat(scannedFile.FileType))
            {
                case SupportedFormat.PDF:     // result is a PDF file
                    try
                    {
                        // convert PDF to image for preview
                        using (var sourceStream = await scannedFile.OpenReadAsync())
                        {
                            PdfDocument doc = await PdfDocument.LoadFromStreamAsync(sourceStream);
                            PdfPage page = doc.GetPage(0);
                            BitmapImage imageOfPdf = new BitmapImage();

                            using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
                            {
                                await page.RenderToStreamAsync(stream);
                                await imageOfPdf.SetSourceAsync(stream);
                            }

                            imageMeasurements = RefreshImageMeasurements(imageOfPdf);
                            DisplayImage(imageOfPdf, ImageScanViewer);
                        }
                    }
                    catch (Exception)
                    {
                        MessageDialog errorDialog1 = new MessageDialog(LocalizedString("ErrorMessageShowResultBody"), LocalizedString("ErrorMessageShowResultHeader"));
                        errorDialog1.Commands.Add(new UICommand(LocalizedString("ErrorMessageShowResultOpenFolder"), (x) => ButtonRecents_Click(null, null)));
                        errorDialog1.Commands.Add(new UICommand(LocalizedString("ErrorMessageShowResultClose"), (x) => { }));
                        errorDialog1.DefaultCommandIndex = 0;
                        errorDialog1.CancelCommandIndex = 1;
                        try { await errorDialog1.ShowAsync(); }
                        catch (Exception) { }
                        ScanCanceled();
                        return;
                    }
                    ShowPrimaryMenuConfig(PrimaryMenuConfig.pdf);
                    flowState = FlowState.result;
                    FixResultPositioning();
                    break;
                case SupportedFormat.XPS:     // result is an XPS file
                case SupportedFormat.OpenXPS:    // result is an OXPS file
                    MessageDialog dialog = new MessageDialog(LocalizedString("MessageFileSavedBody"), LocalizedString("MessageFileSavedHeader"));
                    dialog.Commands.Add(new UICommand(LocalizedString("MessageFileSavedOpenFolder"), (x) => ButtonRecents_Click(null, null)));
                    dialog.Commands.Add(new UICommand(LocalizedString("MessageFileSavedClose"), (x) => { }));
                    dialog.DefaultCommandIndex = 0;
                    dialog.CancelCommandIndex = 1;
                    try { await dialog.ShowAsync(); }
                    catch (Exception) { }
                    flowState = FlowState.initial;
                    break;
                default:        // result is an image file (JPG/PNG/TIF/BMP)
                    try
                    {
                        DisplayImageAsync(scannedFile, ImageScanViewer);
                    }
                    catch (Exception)
                    {
                        MessageDialog errorDialog2 = new MessageDialog(LocalizedString("ErrorMessageShowResultBody"), LocalizedString("ErrorMessageShowResultHeader"));
                        errorDialog2.Commands.Add(new UICommand(LocalizedString("ErrorMessageShowResultOpenFolder"), (x) => ButtonRecents_Click(null, null)));
                        errorDialog2.Commands.Add(new UICommand(LocalizedString("ErrorMessageShowResultClose"), (x) => { }));
                        errorDialog2.DefaultCommandIndex = 0;
                        errorDialog2.CancelCommandIndex = 1;
                        try { await errorDialog2.ShowAsync(); }
                        catch (Exception) { }
                        ScanCanceled();
                        return;
                    }
                    SetCustomAspectRatio(ToggleMenuFlyoutItemAspectRatioCustom, null);
                    ShowPrimaryMenuConfig(PrimaryMenuConfig.image);
                    flowState = FlowState.result;

                    imageMeasurements = await RefreshImageMeasurementsAsync(scannedFile);

                    FixResultPositioning();
                    break;
            }

            TextBlockButtonScan.Visibility = Visibility.Visible;
            ProgressRingScan.Visibility = Visibility.Collapsed;

            // send toast if the app is minimized
            if (settingNotificationScanComplete && !inForeground) SendToastNotification(LocalizedString("NotificationScanCompleteHeader"), LocalizedString("NotificationScanCompleteBody"), 5);

            scanNumber++;
            if (scanNumber == 10) try { await ContentDialogFeedback.ShowAsync(); } catch (Exception) { }
            localSettingsContainer.Values["scanNumber"] = ((int)localSettingsContainer.Values["scanNumber"]) + 1;

            // update UI
            Page_SizeChanged(null, null);
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, () => refreshLeftPanel());
            ButtonDevices.IsEnabled = true;
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


        /// <summary>
        ///     Reverts UI and variable changes that were made by commencing a scan.
        /// </summary>
        private async void ScanCanceled()
        {
            ShowPrimaryMenuConfig(PrimaryMenuConfig.hidden);
            ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
            ImageScanViewer.Visibility = Visibility.Collapsed;
            ProgressRingScan.Visibility = Visibility.Collapsed;
            ButtonCancel.Visibility = Visibility.Collapsed;
            TextBlockButtonScan.Visibility = Visibility.Visible;
            flowState = FlowState.initial;
            scannedFile = null;
            ButtonDevices.IsEnabled = true;

            Page_SizeChanged(null, null);
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, () => refreshLeftPanel());
        }


        private void scanProgress(UInt32 numberOfScannedDocuments)
        {
            // TODO
        }


        /// <summary>
        ///     Is called if another source mode was selected. Hides/shows available options in the left panel and updates the available file formats. 
        ///     The first available color mode and format are automatically selected.
        /// </summary>
        private async void RadioButtonSourceChanged(object sender, RoutedEventArgs e)
        {
            IAsyncAction leftPanelRefresh;

            if (RadioButtonSourceAutomatic.IsChecked == true)
            {
                StackPanelColor.Visibility = Visibility.Collapsed;
                StackPanelResolution.Visibility = Visibility.Collapsed;

                leftPanelRefresh = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, () => refreshLeftPanel());

                RadioButtonColorModeColor.IsEnabled = false;
                RadioButtonColorModeGrayscale.IsEnabled = false;
                RadioButtonColorModeMonochrome.IsEnabled = false;

                RadioButtonColorModeColor.IsChecked = false;
                RadioButtonColorModeGrayscale.IsChecked = false;
                RadioButtonColorModeMonochrome.IsChecked = false;

                await leftPanelRefresh;

                // detect available file formats and update UI accordingly
                GetSupportedFormats(selectedScanner.AutoConfiguration, formats, selectedScanner, ComboBoxFormat);
                ComboBoxFormat.IsEnabled = true;
            }
            else if (RadioButtonSourceFlatbed.IsChecked == true)
            {
                leftPanelRefresh = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, () => refreshLeftPanel());

                // detect available color modes and update UI accordingly
                RadioButtonColorModeColor.IsEnabled = selectedScanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                RadioButtonColorModeGrayscale.IsEnabled = selectedScanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                RadioButtonColorModeMonochrome.IsEnabled = selectedScanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);

                await leftPanelRefresh;

                if (RadioButtonColorModeColor.IsEnabled) RadioButtonColorModeColor.IsChecked = true;
                else if (RadioButtonColorModeGrayscale.IsEnabled) RadioButtonColorModeGrayscale.IsChecked = true;
                else if (RadioButtonColorModeMonochrome.IsEnabled) RadioButtonColorModeMonochrome.IsChecked = true;

                // detect available resolutions and update UI accordingly
                GenerateResolutions(selectedScanner.FlatbedConfiguration, ComboBoxResolution, resolutions);
                ComboBoxResolution.IsEnabled = true;

                // show flatbed/feeder-specific options
                StackPanelColor.Visibility = Visibility.Visible;
                StackPanelResolution.Visibility = Visibility.Visible;

                // detect available file formats and update UI accordingly
                GetSupportedFormats(selectedScanner.FlatbedConfiguration, formats, selectedScanner, ComboBoxFormat);
                ComboBoxFormat.IsEnabled = true;
            }
            else if (RadioButtonSourceFeeder.IsChecked == true)
            {
                leftPanelRefresh = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, () => refreshLeftPanel());

                // detect available color modes and update UI accordingly
                RadioButtonColorModeColor.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                RadioButtonColorModeGrayscale.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                RadioButtonColorModeMonochrome.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);

                await leftPanelRefresh;

                if (RadioButtonColorModeColor.IsEnabled) RadioButtonColorModeColor.IsChecked = true;
                else if (RadioButtonColorModeGrayscale.IsEnabled) RadioButtonColorModeGrayscale.IsChecked = true;
                else if (RadioButtonColorModeMonochrome.IsEnabled) RadioButtonColorModeMonochrome.IsChecked = true;

                // detect available resolutions and update UI accordingly
                GenerateResolutions(selectedScanner.FeederConfiguration, ComboBoxResolution, resolutions);
                ComboBoxResolution.IsEnabled = true;

                // show flatbed/feeder-specific options
                StackPanelColor.Visibility = Visibility.Visible;
                StackPanelResolution.Visibility = Visibility.Visible;

                // detect available file formats and update UI accordingly
                GetSupportedFormats(selectedScanner.FeederConfiguration, formats, selectedScanner, ComboBoxFormat);
                ComboBoxFormat.IsEnabled = true;
            }
        }


        /// <summary>
        ///     The event listener for when <see cref="AppBarButtonCopy"/> is clicked. Copies the currently visible
        ///     result file to the clipboard. And sends a toast notification as confirmation.
        /// </summary>
        private async void AppBarButtonCopy_Click(object sender, RoutedEventArgs e)
        {
            // create DataPackage for clipboard
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;

            // set contents according to file type and copy to clipboard
            string fileExtension = scannedFile.FileType;
            try
            {
                await scannedFile.OpenAsync(FileAccessMode.Read);           // check whether file still available

                switch (fileExtension)
                {
                    case ".jpg":
                    case ".jpeg":
                    case ".png":
                    case ".tif":
                    case ".bmp":
                        dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromFile(scannedFile));
                        Clipboard.SetContent(dataPackage);
                        SendToastNotification(LocalizedString("NotificationCopyHeader"), "", 5, scannedFile.Path);
                        break;
                    default:
                        List<StorageFile> list = new List<StorageFile>();
                        list.Add(scannedFile);
                        dataPackage.SetStorageItems(list);
                        Clipboard.SetContent(dataPackage);
                        SendToastNotification(LocalizedString("NotificationCopyHeader"), "", 5);
                        break;
                }
            }
            catch (Exception)
            {
                ShowContentDialog(LocalizedString("ErrorMessageCopyHeader"), LocalizedString("ErrorMessageCopyBody"));
                return;
            }
        }


        /// <summary>
        ///     The event listener for when the settings button is clicked. Transitions to the settings page.
        /// </summary>
        private void ButtonSettings_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage), null, new EntranceNavigationTransitionInfo());
        }


        /// <summary>
        ///     Opens up the Windows 10 share menu next to <see cref="AppBarButtonShare"/> using the <see cref="dataTransferManager"/>.
        /// </summary>
        /// <remarks>
        ///     If the <see cref="AppBarButtonShare"/> is in the overflow menu, the share menu will not be placed next to it.
        /// </remarks>
        private void AppBarButtonShare_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (((AppBarButton) sender).IsInOverflow)
                {
                    DataTransferManager.ShowShareUI();
                } else
                {
                    GeneralTransform transform = AppBarButtonShare.TransformToVisual(null);
                    Windows.Foundation.Rect rectangle = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, AppBarButtonShare.ActualWidth, AppBarButtonShare.ActualHeight));
            
                    ShareUIOptions shareUIOptions = new ShareUIOptions();
                    shareUIOptions.SelectionRect = rectangle;

                    DataTransferManager.ShowShareUI(shareUIOptions);
                }
            }
            catch (Exception)
            {
                return;
            }
            
        }


        /// <summary>
        ///     The event listener for when the <see cref="AppBarButtonDelete"/> is clicked.
        ///     Disables both CommandBars while working and attempts to delete the <see cref="scannedFile"/>.
        ///     If it fails, an error message is shown.
        /// </summary>
        private async void ButtonDelete_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            LockCommandBar(CommandBarPrimary);
            LockCommandBar(CommandBarSecondary);
            try
            {
                await scannedFile.DeleteAsync(StorageDeleteOption.Default);
            }
            catch (Exception)
            {
                ShowContentDialog(LocalizedString("ErrorMessageDeleteHeader"), LocalizedString("ErrorMessageDeleteBody"));
                UnlockCommandBar(CommandBarPrimary, null);
                UnlockCommandBar(CommandBarSecondary, null);
                return;
            }
            ContentDialogDelete.Hide();
            ShowPrimaryMenuConfig(PrimaryMenuConfig.hidden);
            ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
            ImageScanViewer.Visibility = Visibility.Collapsed;
            UnlockCommandBar(CommandBarPrimary, null);
            UnlockCommandBar(CommandBarSecondary, null);
            flowState = FlowState.initial;
            Page_SizeChanged(null, null);
        }


        /// <summary>
        ///     The event listener for when the <see cref="AppBarButtonRename"/> is clicked.
        ///     Shows an error message if it fails (e.g. if the file name is already occupied).
        /// </summary>
        private async void ButtonRename_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (TextBoxRename.Text + "." + scannedFile.Name.Split(".")[1] == scannedFile.Name) return;
            try
            {
                await scannedFile.RenameAsync(TextBoxRename.Text + "." + scannedFile.Name.Split(".")[1], NameCollisionOption.FailIfExists);
            }
            catch (Exception)
            {
                ShowContentDialog(LocalizedString("ErrorMessageRenameHeader"), LocalizedString("ErrorMessageRenameBody"));
                return;
            }
            ContentDialogRename.Hide();
            TextBlockCommandBarPrimaryFileName.Text = scannedFile.DisplayName;
        }        


        /// <summary>
        ///     The event listener for when the <see cref="AppBarButtonCrop"/> is clicked/checked. Transitions
        ///     to the crop state and disables the <see cref="CommandBarPrimary"/>.
        /// </summary>
        private async void AppBarButtonCrop_Checked(object sender, RoutedEventArgs e)
        {
            // deactivate all buttons
            LockCommandBar(CommandBarPrimary);

            try
            {
                await ImageCropper.LoadImageFromFile(scannedFile);
            }
            catch (Exception)
            {
                ShowContentDialog(LocalizedString("ErrorMessageCropHeader"), LocalizedString("ErrorMessageCropBody"));
                AppBarButtonCrop.IsChecked = false;
                UnlockCommandBar(CommandBarPrimary);
                return;
            }
            
            flowState = FlowState.crop;

            // make sure that the ImageCropper won't be obstructed
            ImageCropper.Padding = new Thickness(18,
                18 + CommandBarPrimary.ActualHeight +           // use CommandBarPrimary for top padding because the other one is not rendered yet
                DropShadowPanelCommandBarSecondary.Margin.Top, 18, 18 + CommandBarPrimary.ActualHeight +
                StackPanelTextBlockCommandBarPrimaryFile.ActualHeight + DropShadowPanelCommandBarPrimary.Margin.Bottom);

            // show ImageCropper and secondary commands
            ImageCropper.Visibility = Visibility.Visible;
            ShowSecondaryMenuConfig(SecondaryMenuConfig.crop);
        }


        /// <summary>
        ///     The event listener for when the <see cref="AppBarButtonRotate"/> is clicked.
        ///     Disables all CommandBars while working and rotates the current result 90° clockwise.
        /// </summary>
        private async void AppBarButtonRotate_Click(object sender, RoutedEventArgs e)
        {
            LockCommandBar(CommandBarPrimary);

            IRandomAccessStream stream = null;

            try
            {
                stream = await scannedFile.OpenAsync(FileAccessMode.ReadWrite);
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                Guid encoderId = GetBitmapEncoderId(scannedFile.Name.Split(".")[1]);

                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
                encoder.SetSoftwareBitmap(softwareBitmap);
                encoder.BitmapTransform.Rotation = BitmapRotation.Clockwise90Degrees;
                await encoder.FlushAsync();
            }
            catch (Exception)
            {
                ShowContentDialog(LocalizedString("ErrorMessageRotateHeader"), LocalizedString("ErrorMessageRotateBody"));
                UnlockCommandBar(CommandBarPrimary);
                return;
            }

            DisplayImageAsync(scannedFile, ImageScanViewer);
            stream.Dispose();

            UnlockCommandBar(CommandBarPrimary, null);

            // refresh image properties
            imageMeasurements = await RefreshImageMeasurementsAsync(scannedFile);
        }


        /// <summary>
        ///     The event listener for when the app theme changed. Makes sure that the shadows are correctly
        ///     displayed while in dark or light mode.
        /// </summary>
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
        ///     Page was loaded (possibly through navigation). Reacts to settings changes if necessary.
        /// </summary>
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (formatSettingChanged)
            {
                formatSettingChanged = false;
                RadioButtonSourceChanged(null, null);
            }

            bool? defaultFolder = await IsDefaultScanFolderSet();
            if (firstLoaded)
            {
                // page visible for the first time this session
                if (firstAppLaunchWithThisVersion == true) ShowUpdateMessage();

                if (firstAppLaunchWithThisVersion == null)
                {
                    TeachingTipSaveLocation.ActionButtonClick += (x, y) =>
                    {
                        TeachingTipSaveLocation.IsOpen = false;
                        ButtonSettings_Click(null, null);
                    };
                    TeachingTipSaveLocation.IsOpen = true;
                }

                firstLoaded = false;
            }
            
            if (defaultFolder == true || defaultFolder == null) FontIconButtonRecents.Glyph = glyphButtonRecentsDefault;
            else FontIconButtonRecents.Glyph = glyphButtonRecentsCustom;
        }


        /// <summary>
        ///     The event listener for when the <see cref="AppBarButtonDone"/> is clicked.
        ///     Hides the result and returns the app to its initial state.
        /// </summary>
        private void AppBarButtonDone_Click(object sender, RoutedEventArgs e)
        {
            flowState = FlowState.initial;
            ShowPrimaryMenuConfig(PrimaryMenuConfig.hidden);
            ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
            ImageScanViewer.Visibility = Visibility.Collapsed;
            Page_SizeChanged(null, null);
        }


        /// <summary>
        ///     Ensures that the <see cref="Image"/> and <see cref="InkCanvas"/> are sized correctly.
        /// </summary>
        private void FixResultPositioning()
        {
            // fix image
            ImageScanViewer.MaxWidth = ScrollViewerScan.ActualWidth;
            try { ImageScanViewer.MaxHeight = ScrollViewerScan.ActualHeight - CoreApplication.GetCurrentView().TitleBar.Height; }
            catch (Exception)
            {
                try { ImageScanViewer.MaxHeight = ScrollViewerScan.ActualHeight - 32; }
                catch (Exception) { ; }
            }

            // fix viewbox containing Image and InkCanvas
            ViewBoxScan.Width = ImageScanViewer.ActualWidth;
            try { ViewBoxScan.MaxHeight = ScrollViewerScan.ActualHeight - CoreApplication.GetCurrentView().TitleBar.Height; }
            catch (Exception)
            {
                try { ViewBoxScan.MaxHeight = ScrollViewerScan.ActualHeight - 32; }
                catch (Exception) {; }
            }
        }


        /// <summary>
        ///     The event listener for when a new scanner has been selected from the <see cref="ComboBoxScanners"/>.
        ///     Disabled the <see cref="ComboBox"/> to let <see cref="refreshLeftPanel"/> deal with this safely.
        /// </summary>
        private async void ComboBoxScanners_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxScanners.IsEnabled = false;
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, () => refreshLeftPanel());
        }


        /// <summary>
        ///     The event listener for when a key is pressed on the MainPage. Used to process shortcuts.
        /// </summary>
        private void MainPage_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (IsCtrlKeyPressed())
            {
                switch (args.VirtualKey)
                {
                    case VirtualKey.C:
                        // shortcut Copy
                        if (flowState == FlowState.result) AppBarButtonCopy_Click(AppBarButtonCopy, null);
                        break;
                    case VirtualKey.S:
                        // shortcut share
                        if (flowState == FlowState.result) AppBarButtonShare_Click(AppBarButtonShare, null);
                        break;
                    case VirtualKey.D:
                        // shortcut debug
                        debugShortcutActive = true;
                        ButtonScan.IsEnabled = true;
                        break;
                }
            }
        }


        /// <summary>
        ///     The event listener for when a key is lifted on the MainPage. Used to process shortcuts.
        /// </summary>
        private async void MainPage_KeyUp(CoreWindow sender, KeyEventArgs args)
        {
            if ((!IsCtrlKeyPressed() || args.VirtualKey == VirtualKey.D) && debugShortcutActive)
            {
                debugShortcutActive = false;
                ButtonScan.IsEnabled = false;
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, () => refreshLeftPanel());
            }
        }


        /// <summary>
        ///     The event listener for when the <see cref="ButtonCancel"/> is pressed during a scan.
        ///     Attempts to cancel the currently running scan and displays an error if it failed.
        /// </summary>
        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            if (cancellationToken != null)
            {
                canceledScan = true;
                try { cancellationToken.Cancel(); }
                catch (Exception)
                {
                    ShowContentDialog(LocalizedString("ErrorMessageScanCancelHeader"), LocalizedString("ErrorMessageScanCancelBody"));
                    return;
                }
            }
            cancellationToken = null;
            ScanCanceled();
        }


        /// <summary>
        ///     Informs the user that an error occured during the scan. Sends a toast if <see cref="inForeground"/> == false.
        /// </summary>
        /// <param name="exc">The exception of wich the HResult shall be printed as well.</param>
        private void ScannerError(System.Runtime.InteropServices.COMException exc)
        {
            // scanner error while scanning
            if (!inForeground)
            {
                SendToastNotification(LocalizedString("NotificationScanErrorHeader"),
                    LocalizedString("NotificationScanErrorBody"), 5);
            }
            ShowContentDialog(LocalizedString("ErrorMessageScanScannerErrorHeader"),
                    LocalizedString("ErrorMessageScanScannerErrorBody") + "\n" + exc.HResult);
            ScanCanceled();
            return;
        }


        /// <summary>
        ///     Informs the user that an error occured during the scan. Sends a toast if <see cref="inForeground"/> == false.
        /// </summary>
        private void ScannerError()
        {
            // unknown error while scanning
            if (!inForeground)
            {
                SendToastNotification(LocalizedString("NotificationScanErrorHeader"),
                    LocalizedString("NotificationScanErrorBody"), 5);
            }
            ShowContentDialog(LocalizedString("ErrorMessageScanErrorHeader"),
                    LocalizedString("ErrorMessageScanErrorBody"));
            ScanCanceled();
            return;
        }


        /// <summary>
        ///     Sets the aspect Ratio of the <see cref="ImageCropper"/> to a fixed value according to which
        ///     <see cref="ToggleMenuFlyoutItem"/> triggered the method.
        /// </summary>
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
            ImageCropper.AspectRatio = 1.0 / double.Parse(((ToggleMenuFlyoutItem)sender).Tag.ToString(), new System.Globalization.CultureInfo("en-EN"));
            AppBarButtonAspectRatio.Label = ((ToggleMenuFlyoutItem)sender).Text;
        }


        /// <summary>
        ///     Resets the aspect ratio of <see cref="ImageCropper"/> to allow for free cropping.
        /// </summary>
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
            AppBarButtonAspectRatio.Label = ((ToggleMenuFlyoutItem)sender).Text;
        }


        /// <summary>
        ///     The event listener for when the <see cref="AppBarButtonDiscard"/> is clicked.
        ///     Discards the changes of the currently active crop/drawing and returns the UI
        ///     to a neutral state.
        /// </summary>
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


        /// <summary>
        ///     The event listener for when the <see cref="AppBarButtonSave"/> is clicked. Saves the changes of
        ///     the current crop/drawing to the <see cref="scannedFile"/> and shows an error message if it fails.
        ///     Afterwards refreshes the <see cref="ImageScanViewer"/> and returns the UI to normal.
        ///     The CommandBars are disabled while the method is working.
        /// </summary>
        private async void AppBarButtonSave_Click(object sender, RoutedEventArgs e)
        {
            LockCommandBar(CommandBarSecondary);

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
                        ShowContentDialog(LocalizedString("ErrorMessageSaveHeader"), LocalizedString("ErrorMessageSaveBody"));
                        try { stream.Dispose(); } catch (Exception) { }
                        UnlockCommandBar(CommandBarSecondary);
                        return;
                    }
                    
                    stream.Dispose();

                    // refresh preview and properties
                    ImageScanViewer.SizeChanged += FinishSaving;
                    DisplayImageAsync(scannedFile, ImageScanViewer);
                    imageMeasurements = await RefreshImageMeasurementsAsync(scannedFile);

                    flowState = FlowState.result;
                    AppBarButtonCrop.IsChecked = false;
                    break;
                case FlowState.draw:
                    // save file
                    stream = null;
                    try
                    {
                        CanvasDevice device = CanvasDevice.GetSharedDevice();
                        CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (int)InkCanvasScan.ActualWidth, (int)InkCanvasScan.ActualHeight, 192); stream = await scannedFile.OpenAsync(FileAccessMode.ReadWrite);
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
                        ShowContentDialog(LocalizedString("ErrorMessageSaveHeader"), LocalizedString("ErrorMessageSaveBody"));
                        try { stream.Dispose(); } catch (Exception) { }
                        UnlockCommandBar(CommandBarSecondary);
                        return;
                    }

                    stream.Dispose();

                    // refresh preview
                    ImageScanViewer.SizeChanged += FinishSaving;
                    DisplayImageAsync(scannedFile, ImageScanViewer);

                    flowState = FlowState.result;
                    AppBarButtonDraw.IsChecked = false;
                    break;
            }

            // return UI to normal
            if (uiState != UIstate.small_result) ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
            else ShowSecondaryMenuConfig(SecondaryMenuConfig.done);

            // reload file with new properties
            imageMeasurements = await RefreshImageMeasurementsAsync(scannedFile);

            UnlockCommandBar(CommandBarSecondary);
            UnlockCommandBar(CommandBarPrimary);
        }


        /// <summary>
        ///     The part of <see cref="AppBarButtonSave_Click(object, RoutedEventArgs)"/>
        ///     responsible for making the <see cref="Image"/> the transition to the changed file smooth.
        /// </summary>
        private void FinishSaving(object sender, SizeChangedEventArgs args)
        {
            if (!(imageLoading && ((Image)sender).Source == null))
            {
                ImageScanViewer.SizeChanged -= FinishSaving;
                ImageCropper.Visibility = Visibility.Collapsed;
                InkCanvasScan.Visibility = Visibility.Collapsed;
            }
        }
        

        /// <summary>
        ///     The event listener for when the <see cref="AppBarButtonSaveCopy"/> is clicked. Saves the changes of
        ///     the current crop/drawing to a new file in the same folder as the <see cref="scannedFile"/> and 
        ///     shows an error message if it fails.
        ///     The <see cref="CommandBarSecondary"/> is disabled while the method is working.
        /// </summary>
        private async void AppBarButtonSaveCopy_Click(object sender, RoutedEventArgs e)
        {
            LockCommandBar(CommandBarSecondary);

            switch (flowState)
            {
                case FlowState.crop:
                    // save crop as new file
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
                        ShowContentDialog(LocalizedString("ErrorMessageSaveHeader"), LocalizedString("ErrorMessageSaveBody"));
                        try { stream.Dispose(); } catch (Exception) { }
                        return;
                    }
                    stream.Dispose();
                    break;
                case FlowState.draw:
                    // save drawing as new file
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
                        ShowContentDialog(LocalizedString("ErrorMessageSaveHeader"), LocalizedString("ErrorMessageSaveBody"));
                        try { stream.Dispose(); } catch (Exception) { }
                        UnlockCommandBar(CommandBarSecondary);
                        return;
                    }
                    break;
            }

            UnlockCommandBar(CommandBarSecondary, null);
        }


        /// <summary>
        ///     Shows a pre-defined configuration of the <see cref="CommandBarPrimary"/>.
        /// </summary>
        /// <param name="config">The <see cref="PrimaryMenuConfig"/> that shall be shown.</param>
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


        /// <summary>
        ///     Shows a pre-defined configuration of the <see cref="CommandBarSecondary"/> without unlocking it.
        /// </summary>
        /// <param name="config">The <see cref="SecondaryMenuConfig"/> that shall be shown.</param>
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
                    AppBarButtonTouchDraw.Visibility = Visibility.Collapsed;
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
                    AppBarButtonTouchDraw.Visibility = Visibility.Collapsed;
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
                    try
                    {
                        IReadOnlyList<PointerDevice> pointerDevices = PointerDevice.GetPointerDevices();
                        foreach (var device in pointerDevices)
                        {
                            if (device.PointerDeviceType == PointerDeviceType.Touch)
                            {
                                AppBarButtonTouchDraw.Visibility = Visibility.Visible;
                                break;
                            }
                        }
                    }
                    catch (Exception) { }
                    AppBarButtonAspectRatio.Visibility = Visibility.Collapsed;
                    AppBarButtonSave.Visibility = Visibility.Visible;
                    AppBarButtonSaveCopy.Visibility = Visibility.Visible;
                    AppBarButtonDiscard.Visibility = Visibility.Visible;

                    CommandBarSecondary.Visibility = Visibility.Visible;
                    break;
            }
        }


        /// <summary>
        ///     Flips the aspect ratio of the <see cref="ImageCropper"/>.
        /// </summary>
        private void FlipAspectRatio(object sender, RoutedEventArgs e)
        {   
            ImageCropper.AspectRatio = ImageCropper.CroppedRegion.Height / ImageCropper.CroppedRegion.Width;
        }


        /// <summary>
        ///     The event listener for when the <see cref="AppBarButtonDraw"/> is clicked/checked.
        /// </summary>
        private void AppBarButtonDraw_Checked(object sender, RoutedEventArgs e)
        {
            // deactivate all buttons
            LockCommandBar(CommandBarPrimary);

            flowState = FlowState.draw;

            // show InkCanvas and secondary commands
            InitializeInkCanvas(InkCanvasScan, imageMeasurements.Item1, imageMeasurements.Item2);
            FixResultPositioning();
            InkCanvasScan.Visibility = Visibility.Visible;
            ShowSecondaryMenuConfig(SecondaryMenuConfig.draw);
            UnlockCommandBar(CommandBarSecondary);
        }


        /// <summary>
        ///     The event listener for when the pointer entered the <see cref="InkCanvasScan"/>. If the pointer
        ///     belongs to a pen and the app is in drawing mode or the pointer belongs to a touch and drawing with 
        ///     touch is enabled, the <see cref="CommandBarPrimary"/> and <see cref="CommandBarSecondary"/> are hidden.
        /// </summary>
        private void InkCanvasScan_PointerEntered(InkUnprocessedInput input, PointerEventArgs e)
        {
            if (flowState == FlowState.draw 
                && (e.CurrentPoint.PointerDevice.PointerDeviceType == PointerDeviceType.Pen
                    || (e.CurrentPoint.PointerDevice.PointerDeviceType == PointerDeviceType.Touch
                        && AppBarButtonTouchDraw.IsChecked == true)))
            {
                ShowPrimaryMenuConfig(PrimaryMenuConfig.hidden);
                ShowSecondaryMenuConfig(SecondaryMenuConfig.hidden);
            }
        }


        /// <summary>
        ///     The event listener for when the pointer exited the <see cref="InkCanvasScan"/>. If the app is
        ///     in drawing mode, the corresponding <see cref="PrimaryMenuConfig"/> and <see cref="SecondaryMenuConfig"/>
        ///     are loaded.
        /// </summary>
        private void InkCanvasScan_PointerExited(InkUnprocessedInput input, PointerEventArgs e)
        {
            if (flowState == FlowState.draw)
            {
                ShowPrimaryMenuConfig(PrimaryMenuConfig.image);
                ShowSecondaryMenuConfig(SecondaryMenuConfig.draw);
            }
        }


        /// <summary>
        ///     Launches the Feedback Hub and navigates it to the app's category.
        /// </summary>
        private async void LaunchFeedbackHub(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
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
        ///     The event listener for when <see cref="ButtonScan"/> is enabled or disabled. Adapts the button's
        ///     label opacity accordingly.
        /// </summary>
        private void ButtonScan_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool) e.NewValue == true) TextBlockButtonScan.Opacity = 1;
            else TextBlockButtonScan.Opacity = 0.5;
        }


        /// <summary>
        ///     The event listener for when the layout of <see cref="ScrollViewerScan"/> changed.
        /// </summary>
        private void ScrollViewerScan_LayoutUpdated(object sender, object e)
        {
            FixResultPositioning();
        }


        /// <summary>
        ///     The event listener for when the <see cref="AppBarButtonRename"/> is clicked.
        /// </summary>
        private async void AppBarButtonRename_Click(object sender, RoutedEventArgs e)
        {
            try { await ContentDialogRename.ShowAsync(); }
            catch (Exception) { }
        }


        /// <summary>
        ///     The event listener for when the <see cref="ContentDialogRename"/> is opened. Fills in
        ///     the current file name.
        /// </summary>
        private void ContentDialogRename_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            TextBoxRename.Text = scannedFile.DisplayName;
            TextBlockRenameExtension.Text = "." + scannedFile.Name.Split(".")[1];
            TextBoxRename.SelectAll();
        }


        /// <summary>
        ///     The event listener for when the <see cref="AppBarButtonDelete"/> is clicked.
        /// </summary>
        private async void AppBarButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            try { await ContentDialogDelete.ShowAsync(); }
            catch (Exception) { }
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

            try { await ContentDialogBlank.ShowAsync(); }
            catch (Exception) { }
        }


        /// <summary>
        ///     Displays a <see cref="ContentDialog"/> consisting of a <paramref name="title"/>, <paramref name="message"/>,
        ///     a button that opens the Feedback Hub and a button that allows the user to close the <see cref="ContentDialog"/>.
        /// </summary>
        /// <param name="title">The title of the <see cref="ContentDialog"/>.</param>
        /// <param name="message">The body of the <see cref="ContentDialog"/>.</param>
        public async void ShowFeedbackContentDialog(string title, string message)
        {
            ContentDialogBlank.Title = title;
            ContentDialogBlank.Content = message;

            ContentDialogBlank.CloseButtonText = LocalizedString("CloseButtonText");
            ContentDialogBlank.PrimaryButtonText = LocalizedString("FeedbackContentDialogButtonSendFeedback");
            TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs> typedEventHandler 
                = new TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs>((a, b) => LaunchFeedbackHub(null, null));
            ContentDialogBlank.PrimaryButtonClick += typedEventHandler;
            ContentDialogBlank.SecondaryButtonText = "";
            ContentDialogBlank.DefaultButton = ContentDialogButton.Close;

            try { await ContentDialogBlank.ShowAsync(); }
            catch (Exception) { }
            ContentDialogBlank.PrimaryButtonClick -= typedEventHandler;
        }


        /// <summary>
        ///     The event listener for when the <see cref="AppBarButtonOpenWith"/> is clicked.
        /// </summary>
        private async void AppBarButtonOpenWith_Click(object sender, RoutedEventArgs e)
        {
            LauncherOptions options = new LauncherOptions();
            options.DisplayApplicationPicker = true;

            try
            {
                await Launcher.LaunchFileAsync(scannedFile, options);
            }
            catch (Exception) { };
        }


        /// <summary>
        ///     The event listener for when the user starts dragging a scan.
        /// </summary>
        private void ImageScanViewer_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            try
            {
                args.AllowedOperations = DataPackageOperation.Copy;

                List<StorageFile> list = new List<StorageFile>();
                list.Add(scannedFile);
                args.Data.SetStorageItems(list);

                args.DragUI.SetContentFromDataPackage();
            }
            catch (Exception) { }
        }


        /// <summary>
        ///     The event listener for when <see cref="ButtonDevices"/> is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonDevices_Click(object sender, RoutedEventArgs e)
        {
            if (TeachingTipDevices.IsOpen == true) TeachingTipDevices.IsOpen = false;
            TeachingTipDevices.IsOpen = true;
        }


        /// <summary>
        ///     Displays the <see cref="ContentDialogUpdate"/>.
        /// </summary>
        private async void ShowUpdateMessage()
        {
            try { await ContentDialogUpdate.ShowAsync(); }
            catch (Exception) { }
        }


        /// <summary>
        ///     The event listener for when the pointer entered the <see cref="ImageScanViewer"/>. If the pointer
        ///     belongs to a pen and drawing is available, the app will switch to drawing mode.
        /// </summary>
        private void ImageScanViewer_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Pen
                && AppBarButtonDraw.Visibility == Visibility.Visible
                && AppBarButtonDraw.IsEnabled
                && flowState == FlowState.result
                && settingDrawPenDetected)
            {
                AppBarButtonDraw.IsChecked = true;
            } 
        }

        private void AppBarButtonTouchDraw_Checked(object sender, RoutedEventArgs e)
        {
            // enable touch drawing
            InkCanvasScan.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Pen | CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Touch;
        }


        private void AppBarButtonTouchDraw_Unchecked(object sender, RoutedEventArgs e)
        {
            // disable touch drawing
            InkCanvasScan.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Pen | CoreInputDeviceTypes.Mouse;
        }


        private void CommandBarPrimary_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                ScrollViewerTextBlockCommandBarPrimaryFile.MaxWidth = FrameCommandBarPrimary.ActualWidth;
                if (FrameCommandBarPrimary.ActualWidth <= ScrollViewerTextBlockCommandBarPrimaryFile.ActualWidth)
                {
                    // remove round corner to close gap to file name
                    CornerRadius cornerRadius = FrameCommandBarPrimary.CornerRadius;
                    cornerRadius.TopLeft = 0;
                    FrameCommandBarPrimary.CornerRadius = cornerRadius;
                }
                else
                {
                    // reset to round corners
                    CornerRadius cornerRadius = FrameCommandBarPrimary.CornerRadius;
                    cornerRadius.TopLeft = cornerRadius.BottomLeft;
                    FrameCommandBarPrimary.CornerRadius = cornerRadius;
                }
                ScrollViewerTextBlockCommandBarPrimaryFile.MaxWidth = CommandBarPrimary.ActualWidth;
            }
            catch (Exception) { }
        }


        private async void ButtonCommandBarPrimaryFileName_Click(object sender, RoutedEventArgs e)
        {
            // open folder and select result in it
            FolderLauncherOptions launcherOptions = new FolderLauncherOptions();
            launcherOptions.ItemsToSelect.Add(scannedFile);

            try
            {
                string folder = scannedFile.Path.Remove(scannedFile.Path.LastIndexOf(System.IO.Path.DirectorySeparatorChar));
                await Launcher.LaunchFolderPathAsync(folder, launcherOptions);
            }
            catch (Exception) { }
        }



        private void HyperlinkRate_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            ShowRatingDialog();
            ContentDialogFeedback.Hide();
        }



        private void ContentDialogUpdate_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ShowRatingDialog();
        }
    }
}
