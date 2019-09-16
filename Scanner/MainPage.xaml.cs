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
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
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
        private List<DeviceInformation> deviceInformations = new List<DeviceInformation>();
        CancellationTokenSource cancellationToken = null;

        private ImageScanner selectedScanner = null;
        private StorageFile scannedFile;
        private Tuple<double, double> imageMeasurements;

        private double ColumnLeftDefaultMaxWidth;
        private double ColumnLeftDefaultMinWidth;
        private bool inForeground = true;

        private ObservableCollection<ComboBoxItem> formats = new ObservableCollection<ComboBoxItem>();
        private ObservableCollection<ComboBoxItem> resolutions = new ObservableCollection<ComboBoxItem>();
        
        private UIstate uiState = UIstate.unset;
        private FlowState flowState = FlowState.initial;
        private bool canceledScan = false;

        DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();


        public MainPage()
        {
            this.InitializeComponent();

            TextBlockHeader.Text = Package.Current.DisplayName.ToString();

            // localize hyperlink
            ((Windows.UI.Xaml.Documents.Run)HyperlinkSettings.Inlines[0]).Text = LocalizedString("HyperlinkScannerSelectionHintBodyLink");

            Page_ActualThemeChanged(null, null);

            // initialize veriables
            ColumnLeftDefaultMaxWidth = ColumnLeft.MaxWidth;
            ColumnLeftDefaultMinWidth = ColumnLeft.MinWidth;

            // populate the scanner list
            if (settingSearchIndicator) ProgressBarRefresh.Visibility = Visibility.Visible;
            scannerWatcher = DeviceInformation.CreateWatcher(DeviceClass.ImageScanner);
            scannerWatcher.Added += OnScannerAdded;
            scannerWatcher.Removed += OnScannerRemoved;
            scannerWatcher.EnumerationCompleted += OnScannerEnumerationComplete;
            scannerWatcher.Start();

            // register event listeners ////////////////////////////////////////////////////////////////
            InkCanvasScan.InkPresenter.UnprocessedInput.PointerEntered += InkCanvasScan_PointerEntered;
            InkCanvasScan.InkPresenter.UnprocessedInput.PointerExited += InkCanvasScan_PointerExited;
            dataTransferManager.DataRequested += DataTransferManager_DataRequested;
            CoreApplication.EnteredBackground += (x, y) => { inForeground = false; };
            CoreApplication.LeavingBackground += (x, y) => { inForeground = true; };
            CoreApplication.GetCurrentView().TitleBar.LayoutMetricsChanged += (titleBar, y) => {
                ScrollViewerLeftPanel.Margin = new Thickness(0, titleBar.Height, 0, 0);
            };
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

                // hide flatbed/feeder-specific options
                StackPanelColor.Visibility = Visibility.Collapsed;
                StackPanelResolution.Visibility = Visibility.Collapsed;
            }
            else
            {
                // scanner selected ///////////////////////////////////////////////////////////////////////
                // hide text on the right side
                StackPanelTextRight.Visibility = Visibility.Collapsed;

                if (selectedScanner == null || selectedScanner.DeviceId != ((ComboBoxItem) ComboBoxScanners.SelectedItem).Tag.ToString())
                {
                    // previously different/no scanner selected ////////////////////////////////////////////////////
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
                                ShowContentDialog(LocalizedString("ErrorMessageScannerInformationHeader"),
                                    LocalizedString("ErrorMessageScannerInformationBody") + "\n" + exc.Message);

                                // (almost) start from scratch to hopefully get rid of dead scanners
                                possiblyDeadScanner = true;
                                scannerList.Clear();
                                Page_SizeChanged(null, null);
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

                RadioButtonSourceAutomatic.IsEnabled = autoAllowed;
                RadioButtonSourceFlatbed.IsEnabled = flatbedAllowed;
                RadioButtonSourceFeeder.IsEnabled = feederAllowed;

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
                        if (check.Tag.ToString() == deviceInfo.Id)
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

                        deviceInformations.Add(deviceInfo);
                        scannerList.Add(item);
                    }
                    else return;

                    // auto select first added scanner (if some requirements are met)
                    if (!possiblyDeadScanner && !ComboBoxScanners.IsDropDownOpen && settingAutomaticScannerSelection
                        && selectedScanner == null && deviceInformations.Count == 1)
                    {
                        ComboBoxScanners.SelectedIndex = 0;
                    }

                    TextBlockFoundScannersHint.Text = " (" + LocalizedString("FoundScannersHintBeforeNumber") + scannerList.Count.ToString() + " " + LocalizedString("FoundScannersHintAfterNumber") + ")";
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


        /// <summary>
        ///     The event listener for when the <see cref="scannerWatcher"/> has finished adding all initially
        ///     available scanners.
        /// </summary>
        private async void OnScannerEnumerationComplete(DeviceWatcher sender, Object theObject)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
                {
                    TextBlockFoundScannersHint.Visibility = Visibility.Visible;
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
        ///     Only supports a single-page scan, otherwise the behavior is undefined.
        /// </remarks>
        private async void Scan(object sender, RoutedEventArgs e)
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
                dialog.Commands.Add(new UICommand(LocalizedString("ErrorMessageScanFolderSettings"), (x) => ButtonSettings_Click(null, null)));
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
                ShowContentDialog(LocalizedString("ErrorMessageNoFormatHeader"), LocalizedString("ErrorMessageNoFormatBody"));
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
                    ShowContentDialog(LocalizedString("ErrorMessageNoColorModeHeader"), LocalizedString("ErrorMessageNoColorModeBody"));
                    ScanCanceled();
                    return;
                }
                else selectedScanner.FlatbedConfiguration.ColorMode = (ImageScannerColorMode)selectedColorMode;

                // resolution
                ImageScannerResolution? selectedResolution = GetDesiredResolution(ComboBoxResolution);
                if (selectedResolution == null)
                {
                    ShowContentDialog(LocalizedString("ErrorMessageNoResolutionHeader"), LocalizedString("ErrorMessageNoResolutionBody"));
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
                    ShowContentDialog(LocalizedString("ErrorMessageNoColorModeHeader"), LocalizedString("ErrorMessageNoColorModeBody"));
                    ScanCanceled();
                    return;
                }
                else selectedScanner.FeederConfiguration.ColorMode = (ImageScannerColorMode)selectedColorMode;

                // resolution
                ImageScannerResolution? selectedResolution = GetDesiredResolution(ComboBoxResolution);
                if (selectedResolution == null)
                {
                    ShowContentDialog(LocalizedString("ErrorMessageNoResolutionHeader"), LocalizedString("ErrorMessageNoResolutionBody"));
                    ScanCanceled();
                    return;
                }
                selectedScanner.FeederConfiguration.DesiredResolution = (ImageScannerResolution)selectedResolution;

                // format
                selectedScanner.FeederConfiguration.Format = formatFlow.Item1;
            }
            else
            {
                ShowContentDialog(LocalizedString("ErrorMessageNoConfigurationHeader"), LocalizedString("ErrorMessageNoConfigurationBody"));
            }

            // start scan, send progress and show cancel button
            cancellationToken = new CancellationTokenSource();
            var progress = new Progress<UInt32>(scanProgress);

            ImageScannerScanResult result = null;
            ButtonCancel.Visibility = Visibility.Visible;

            try
            {
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
                    ShowContentDialog(LocalizedString("ErrorMessageConversionHeader"),
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

            if (scanNumber == 10) await ContentDialogFeedback.ShowAsync();
            localSettingsContainer.Values["scanNumber"] = ((int)localSettingsContainer.Values["scanNumber"]) + 1;

            // show result /////////////////////////////////
            ButtonCancel.Visibility = Visibility.Collapsed;
            TextBlockButtonScan.Visibility = Visibility.Visible;
            ProgressRingScan.Visibility = Visibility.Collapsed;

            // react differently to different formats
            switch (scannedFile.FileType)
            {
                case ".pdf":     // result is a PDF file
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

                        imageMeasurements = RefreshImageMeasurements(imageOfPdf);
                        DisplayImage(imageOfPdf, ImageScanViewer);
                    }
                    catch (Exception)
                    {
                        MessageDialog errorDialog1 = new MessageDialog(LocalizedString("ErrorMessageShowResultBody"), LocalizedString("ErrorMessageShowResultHeader"));
                        errorDialog1.Commands.Add(new UICommand(LocalizedString("ErrorMessageShowResultOpenFolder"), (x) => ButtonRecents_Click(null, null)));
                        errorDialog1.Commands.Add(new UICommand(LocalizedString("ErrorMessageShowResultClose"), (x) => { }));
                        errorDialog1.DefaultCommandIndex = 0;
                        errorDialog1.CancelCommandIndex = 1;
                        await errorDialog1.ShowAsync();
                        ScanCanceled();
                        return;
                    }
                    ShowPrimaryMenuConfig(PrimaryMenuConfig.pdf);
                    flowState = FlowState.result;
                    FixResultPositioning();
                    break;
                case ".xps":     // result is an XPS file
                case ".oxps":    // result is an OXPS file
                    MessageDialog dialog = new MessageDialog(LocalizedString("MessageFileSavedBody"), LocalizedString("MessageFileSavedHeader"));
                    dialog.Commands.Add(new UICommand(LocalizedString("MessageFileSavedOpenFolder"), (x) => ButtonRecents_Click(null, null)));
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
                    }
                    catch (Exception)
                    {
                        MessageDialog errorDialog2 = new MessageDialog(LocalizedString("ErrorMessageShowResultBody"), LocalizedString("ErrorMessageShowResultHeader"));
                        errorDialog2.Commands.Add(new UICommand(LocalizedString("ErrorMessageShowResultOpenFolder"), (x) => ButtonRecents_Click(null, null)));
                        errorDialog2.Commands.Add(new UICommand(LocalizedString("ErrorMessageShowResultClose"), (x) => { }));
                        errorDialog2.DefaultCommandIndex = 0;
                        errorDialog2.CancelCommandIndex = 1;
                        await errorDialog2.ShowAsync();
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

            // send toast if the app isn't in the foreground
            if (settingNotificationScanComplete && !inForeground) SendToastNotification(LocalizedString("NotificationScanCompleteHeader"), LocalizedString("NotificationScanCompleteBody"), 5);

            // update UI
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
        private void RadioButtonSourceChanged(object sender, RoutedEventArgs e)
        {
            if (RadioButtonSourceAutomatic.IsChecked == true)
            {
                StackPanelColor.Visibility = Visibility.Collapsed;
                StackPanelResolution.Visibility = Visibility.Collapsed;

                refreshLeftPanel();

                RadioButtonColorModeColor.IsEnabled = false;
                RadioButtonColorModeGrayscale.IsEnabled = false;
                RadioButtonColorModeMonochrome.IsEnabled = false;

                RadioButtonColorModeColor.IsChecked = false;
                RadioButtonColorModeGrayscale.IsChecked = false;
                RadioButtonColorModeMonochrome.IsChecked = false;

                // detect available file formats and update UI accordingly
                GetSupportedFormats(selectedScanner.AutoConfiguration, formats, selectedScanner, ComboBoxFormat);
                ComboBoxFormat.IsEnabled = true;
            }
            else if (RadioButtonSourceFlatbed.IsChecked == true)
            {
                refreshLeftPanel();

                // detect available color modes and update UI accordingly
                RadioButtonColorModeColor.IsEnabled = selectedScanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                RadioButtonColorModeGrayscale.IsEnabled = selectedScanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                RadioButtonColorModeMonochrome.IsEnabled = selectedScanner.FlatbedConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);

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
                refreshLeftPanel();

                // detect available color modes and update UI accordingly
                RadioButtonColorModeColor.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                RadioButtonColorModeGrayscale.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                RadioButtonColorModeMonochrome.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);

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
        private void AppBarButtonCopy_Click(object sender, RoutedEventArgs e)
        {
            // create DataPackage for clipboard
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;

            // cet contents according to file type and copy to clipboard
            string fileExtension = scannedFile.FileType;
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
        }        


        /// <summary>
        ///     The event listener for when the <see cref="AppBarButtonCrop"/> is clicked/checked. Transitions
        ///     to the crop state and disables the <see cref="CommandBarPrimary"/>.
        /// </summary>
        private async void AppBarButtonCrop_Checked(object sender, RoutedEventArgs e)
        {
            // deactivate all buttons
            LockCommandBar(CommandBarPrimary);

            await ImageCropper.LoadImageFromFile(scannedFile);
            flowState = FlowState.crop;

            // make sure that the ImageCropper won't be obstructed
            ImageCropper.Padding = new Thickness(24,
                24 + CommandBarPrimary.ActualHeight +           // use CommandBarPrimary for top padding because the other one is not rendered yet
                DropShadowPanelCommandBarSecondary.Margin.Top, 24, 24 + CommandBarPrimary.ActualHeight +
                DropShadowPanelCommandBarPrimary.Margin.Bottom);

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
            LockCommandBar(CommandBarSecondary);
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
                ShowContentDialog(LocalizedString("ErrorMessageRotateHeader"), LocalizedString("ErrorMessageRotateBody"));
                return;
            }

            DisplayImageAsync(scannedFile, ImageScanViewer);
            stream.Dispose();

            UnlockCommandBar(CommandBarPrimary, null);
            UnlockCommandBar(CommandBarSecondary, null);

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
        private void ComboBoxScanners_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxScanners.IsEnabled = false;
            refreshLeftPanel();
        }


        /// <summary>
        ///     The event listener for when a key is pressed on the MainPage. Used to process shortcuts.
        /// </summary>
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
            ImageCropper.AspectRatio = 1.0 / double.Parse(((ToggleMenuFlyoutItem)sender).Tag.ToString());
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
                        ShowContentDialog(LocalizedString("ErrorMessageSaveHeader"), LocalizedString("ErrorMessageSaveBody"));
                        try { stream.Dispose(); } catch (Exception) { }
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
                        ShowContentDialog(LocalizedString("ErrorMessageSaveHeader"), LocalizedString("ErrorMessageSaveBody"));
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
                        ShowContentDialog(LocalizedString("ErrorMessageSaveHeader"), LocalizedString("ErrorMessageSaveBody"));
                        try { stream.Dispose(); } catch (Exception) { }
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
        ///     Shows a pre-defined configuration of the <see cref="CommandBarSecondary"/>.
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
            ShowSecondaryMenuConfig(SecondaryMenuConfig.draw);
            InitializeInkCanvas(InkCanvasScan, imageMeasurements.Item1, imageMeasurements.Item2);
            FixResultPositioning();
            InkCanvasScan.Visibility = Visibility.Visible;
        }


        /// <summary>
        ///     The event listener for when the pointer entered the <see cref="InkCanvasScan"/>. If the pointer
        ///     belongs to a pen and the app is in drawing mode, the <see cref="CommandBarPrimary"/>
        ///     and <see cref="CommandBarSecondary"/> are hidden.
        /// </summary>
        private void InkCanvasScan_PointerEntered(InkUnprocessedInput input, PointerEventArgs e)
        {
            if (flowState == FlowState.draw && e.CurrentPoint.PointerDevice.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Pen)
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
            await ContentDialogRename.ShowAsync();
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
            await ContentDialogDelete.ShowAsync();
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
    }
}
