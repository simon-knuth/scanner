using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Enumeration;
using Windows.Devices.Input;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using static Enums;
using static Globals;
using static ScannerOperation;
using static Utilities;


namespace Scanner
{
    public sealed partial class MainPage : Page
    {
        private double ColumnSidePaneMinWidth = 300;
        private double ColumnSidePaneMaxWidth = 350;
        private bool makeNextScanAFreshOne = false;
        private bool firstLoaded = true;
        private bool inForeground = true;
        private ObservableCollection<ComboBoxItem> scannerList = new ObservableCollection<ComboBoxItem>();
        private DeviceWatcher scannerWatcher;
        private RecognizedScanner selectedScanner;
        private ObservableCollection<ComboBoxItem> formats = new ObservableCollection<ComboBoxItem>();
        private ObservableCollection<ComboBoxItem> resolutions = new ObservableCollection<ComboBoxItem>();
        private FlowState flowState = FlowState.initial;
        private UIstate uiState = UIstate.unset;
        private CancellationTokenSource scanCancellationToken;
        private Progress<uint> scanProgress;
        private ScanResult scanResult = null;
        private double currentTitleBarButtonWidth = 0;
        private DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
        private int[] shareIndexes;
        private ScopeActions scopeAction;


        public MainPage()
        {
            InitializeComponent();

            CoreApplication.GetCurrentView().TitleBar.LayoutMetricsChanged += (titleBar, y) =>
            {
                FrameLeftPaneScanHeader.Padding = new Thickness(0, titleBar.Height, 0, 0);
                StackPanelContentPaneTopToolbarText.Height = titleBar.Height;
                currentTitleBarButtonWidth = titleBar.SystemOverlayRightInset;
            };

            // add scanner search indicator
            scannerList.Add(ComboBoxItemScannerIndicator);
            ComboBoxItemScannerIndicator.Visibility = Visibility.Visible;

            // initialize scanner watcher
            scannerWatcher = DeviceInformation.CreateWatcher(DeviceClass.ImageScanner);
            scannerWatcher.Added += OnScannerAdded;
            scannerWatcher.Removed += OnScannerRemoved;
            scannerWatcher.Start();

            // register event listeners
            dataTransferManager.DataRequested += DataTransferManager_DataRequested;
            CoreApplication.EnteredBackground += (x, y) => { inForeground = false; };
            CoreApplication.LeavingBackground += (x, y) => { inForeground = true; };
        }

        private void DataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            if (shareIndexes == null && scanResult.GetFileFormat() == SupportedFormat.PDF)
            {
                List<StorageFile> files = new List<StorageFile>();
                files.Add(scanResult.pdf);
                args.Request.Data.SetStorageItems(files);
            }
            else if (shareIndexes.Length == 1)
            {
                StorageFile file = scanResult.GetImageFile(shareIndexes[0]);
                args.Request.Data.SetBitmap(RandomAccessStreamReference.CreateFromFile(file));
                args.Request.Data.Properties.Title = file.Name;
            } 
            else
            {
                List<StorageFile> files = new List<StorageFile>();
                foreach (int index in shareIndexes)
                {
                    files.Add(scanResult.GetImageFile(index));
                }
                args.Request.Data.SetStorageItems(files);
            }
        }

        private async void OnScannerRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                // find lost scanner in scannerList to remove the corresponding scanner and its list entry
                for (int i = 0; i < scannerList.Count - 1; i++)
                {
                    if (((RecognizedScanner)scannerList[i].Tag).scanner.DeviceId.ToLower() == args.Id.ToLower())
                    {
                        ComboBoxScanners.IsDropDownOpen = false;
                        ComboBoxScanners.SelectedIndex = -1;
                        scannerList.RemoveAt(i);
                        return;
                    }
                }
            });
        }

        private async void OnScannerAdded(DeviceWatcher sender, DeviceInformation args)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            async () =>
            {
                bool duplicate = false;

                for(int i = 0; i < scannerList.Count - 1; i++)
                {
                    if (!((RecognizedScanner)scannerList[i].Tag).fake && ((RecognizedScanner)scannerList[i].Tag).scanner.DeviceId.ToLower() == args.Id.ToLower())
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    try 
                    {
                        RecognizedScanner newScanner = await RecognizedScanner.CreateFromDeviceInformationAsync(args);
                        ComboBoxScanners.IsDropDownOpen = false;
                        scannerList.Insert(ComboBoxScanners.Items.Count - 1, CreateComboBoxItem(newScanner.scannerName, newScanner));

                        if (settingAutomaticScannerSelection == true && ComboBoxScanners.SelectedIndex == -1 && flowState != FlowState.scanning)
                            ComboBoxScanners.SelectedIndex = 0;
                    }
                    catch (Exception) { }
                }
                else return;
            });
        }

        private async void ButtonLeftPaneSettings_Click(object sender, RoutedEventArgs e)
        {
            var ctrlKey = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);

            if (ctrlKey.HasFlag(CoreVirtualKeyStates.Down)) await ContentDialogDebug.ShowAsync();       // show debug menu when CTRL key is pressed
            else
            {
                Frame.Navigate(typeof(SettingsPage), null, new DrillInNavigationTransitionInfo());     // navigate to settings
                await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
                {
                    SplitViewLeftPane.IsPaneOpen = false;
                    ButtonScanOptions.IsChecked = false;
                });
            }
        }

        private async void HyperlinkSettings_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            await Launcher.LaunchUriAsync(new Uri("ms-settings:printers"));
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (firstLoaded)
            {
                firstLoaded = false;

                await LoadScanFolder();
                await InitializeTempFolder();

                await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
                {
                    SplitViewLeftPane.Margin = new Thickness(0, GridContentPaneTopToolbar.ActualHeight, 0, 0);
                    SplitViewContentPane.Margin = SplitViewLeftPane.Margin;
                    ScrollViewerContentPaneContentDummy.Margin = SplitViewLeftPane.Margin;
                    PrepareTeachingTips();
                    InkCanvasEditDraw.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Pen;
                    IReadOnlyList<PointerDevice> pointerDevices = PointerDevice.GetPointerDevices();
                    foreach (var device in pointerDevices)
                    {
                        if (device.PointerDeviceType == PointerDeviceType.Touch)
                        {
                            ButtonDrawTouch.Visibility = Visibility.Visible;
                            break;
                        }
                    }
                });

                ComboBoxScanners.Focus(FocusState.Programmatic);

                // initialize debug menu
                ComboBoxDebugFormat.Items.Add(SupportedFormat.JPG);
                ComboBoxDebugFormat.Items.Add(SupportedFormat.PNG);
                ComboBoxDebugFormat.Items.Add(SupportedFormat.TIF);
                ComboBoxDebugFormat.Items.Add(SupportedFormat.BMP);
                ComboBoxDebugFormat.Items.Add(SupportedFormat.PDF);
                ComboBoxDebugFormat.SelectedIndex = 0;
            }

            // refresh scan folder icon
            bool? defaultFolder = await IsDefaultScanFolderSet();
            if (defaultFolder == true || defaultFolder == null) FontIconButtonScanFolder.Glyph = glyphButtonRecentsDefault;
            else FontIconButtonScanFolder.Glyph = glyphButtonRecentsCustom;
        }

        private async void ComboBoxScanners_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxScanners.SelectedIndex < ComboBoxScanners.Items.Count)
            {
                if (ComboBoxScanners.SelectedIndex == -1)
                {
                    // no scanner selected
                    selectedScanner = null;
                    await CleanMenuForNewScanner(null);
                    formats.Clear();
                    resolutions.Clear();
                    StackPanelContentPaneText.Visibility = Visibility.Visible;
                } 
                else
                {
                    // scanner selected
                    await CleanMenuForNewScanner((RecognizedScanner)scannerList[ComboBoxScanners.SelectedIndex].Tag);
                    selectedScanner = (RecognizedScanner)scannerList[ComboBoxScanners.SelectedIndex].Tag;
                    RadioButtonSourceMode_Checked(null, null);
                    StackPanelContentPaneText.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async Task RefreshPreviewIndicators(bool autoPreview, bool? flatbedPreview, bool? feederPreview,
            bool autoAllowed, bool flatbedAllowed, bool feederAllowed)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                if (autoPreview) FontIconAutoPreviewSupported.Visibility = Visibility.Visible;
                else FontIconAutoPreviewSupported.Visibility = Visibility.Collapsed;

                if (flatbedPreview == true) FontIconFlatbedPreviewSupported.Visibility = Visibility.Visible;
                else FontIconFlatbedPreviewSupported.Visibility = Visibility.Collapsed;

                if (feederPreview == true) FontIconFeederPreviewSupported.Visibility = Visibility.Visible;
                else FontIconFeederPreviewSupported.Visibility = Visibility.Collapsed;

                // allow preview of auto config when only one other mode is available and supports previewing
                if (autoAllowed == true && (flatbedAllowed == true && feederAllowed == false && flatbedPreview == true
                                            || flatbedAllowed == false && feederAllowed == true && feederPreview == true))
                {
                    FontIconAutoPreviewSupported.Visibility = Visibility.Visible;
                }
            });
        }

        private async Task CleanMenuForNewScanner(RecognizedScanner scanner)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            async () =>
            {
                ComboBoxScanners.IsEnabled = false;
                
                // select source mode and enable/disable scan button
                if (scanner != null)
                {
                    bool modeSelected = false;
                    bool scannerAlreadySelected = false;
                    if (selectedScanner != null && selectedScanner == scanner)
                    {
                        scannerAlreadySelected = true;
                    }

                    if (scanner.autoAllowed)        // auto config
                    {
                        if (!scannerAlreadySelected)
                        {
                            RadioButtonSourceAutomatic.IsChecked = true;
                            modeSelected = true;
                        }
                        RadioButtonSourceAutomatic.IsEnabled = true;
                    } 
                    else
                    {
                        RadioButtonSourceAutomatic.IsEnabled = false;
                        RadioButtonSourceAutomatic.IsChecked = false;
                    }

                    if (scanner.flatbedAllowed)     // flatbed
                    {
                        if (!modeSelected && !scannerAlreadySelected)
                        {
                            RadioButtonSourceFlatbed.IsChecked = true;
                            modeSelected = true;
                        }
                        RadioButtonSourceFlatbed.IsEnabled = true;
                    }
                    else
                    {
                        RadioButtonSourceFlatbed.IsEnabled = false;
                        RadioButtonSourceFlatbed.IsChecked = false;
                    }

                    if (scanner.feederAllowed)      // feeder
                    {
                        if (!modeSelected && !scannerAlreadySelected)
                        {
                            RadioButtonSourceFeeder.IsChecked = true;
                            modeSelected = true;
                        }
                        RadioButtonSourceFeeder.IsEnabled = true;
                    }
                    else
                    {
                        RadioButtonSourceFeeder.IsEnabled = false;
                        RadioButtonSourceFeeder.IsChecked = false;
                    }
                    ButtonLeftPaneScan.IsEnabled = true;
                    await RefreshPreviewIndicators(scanner.autoPreviewAllowed, scanner.flatbedPreviewAllowed, scanner.feederPreviewAllowed,
                        scanner.autoAllowed, scanner.flatbedAllowed, scanner.feederAllowed);
                }
                else
                {                    
                    ButtonLeftPaneScan.IsEnabled = false;

                    RadioButtonSourceAutomatic.IsEnabled = false;
                    RadioButtonSourceAutomatic.IsChecked = false;
                    RadioButtonSourceFlatbed.IsEnabled = false;
                    RadioButtonSourceFlatbed.IsChecked = false;
                    RadioButtonSourceFeeder.IsEnabled = false;
                    RadioButtonSourceFeeder.IsChecked = false;

                    FrameLeftPaneScanColor.Visibility = Visibility.Collapsed;
                    FrameLeftPaneScanColor.Visibility = Visibility.Collapsed;
                    FrameLeftPaneResolution.Visibility = Visibility.Collapsed;

                    await RefreshPreviewIndicators(false, false, false, false, false, false);
                }

                ComboBoxScanners.IsEnabled = true;
            });
        }

        private async void RadioButtonSourceMode_Checked(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                // color mode
                bool colorSelected = false;
                bool colorAllowed, grayscaleAllowed, monochromeAllowed;

                if (RadioButtonSourceFlatbed.IsChecked == true)         // flatbed
                {
                    colorAllowed = (bool) selectedScanner.flatbedColorAllowed;
                    grayscaleAllowed = (bool)selectedScanner.flatbedGrayscaleAllowed;
                    monochromeAllowed = (bool)selectedScanner.flatbedGrayscaleAllowed;
                } else if (RadioButtonSourceFeeder.IsChecked == true)   // feeder
                {
                    colorAllowed = (bool)selectedScanner.feederColorAllowed;
                    grayscaleAllowed = (bool)selectedScanner.feederGrayscaleAllowed;
                    monochromeAllowed = (bool)selectedScanner.feederGrayscaleAllowed;
                }
                else FrameLeftPaneScanColor.Visibility = Visibility.Collapsed;

                if (RadioButtonSourceFlatbed.IsChecked == true || RadioButtonSourceFeeder.IsChecked == true)
                {
                    if (selectedScanner.flatbedColorAllowed == true)
                    {
                        RadioButtonColorModeColor.IsEnabled = true;
                        RadioButtonColorModeColor.IsChecked = true;
                        colorSelected = true;
                    }
                    else
                    {
                        RadioButtonColorModeColor.IsEnabled = false;
                        RadioButtonColorModeColor.IsChecked = false;
                    }

                    if (selectedScanner.flatbedGrayscaleAllowed == true)
                    {
                        RadioButtonColorModeGrayscale.IsEnabled = true;
                        if (!colorSelected)
                        {
                            RadioButtonColorModeGrayscale.IsChecked = true;
                            colorSelected = true;
                        }
                    }
                    else
                    {
                        RadioButtonColorModeGrayscale.IsEnabled = false;
                        RadioButtonColorModeGrayscale.IsChecked = false;
                    }

                    if (selectedScanner.flatbedMonochromeAllowed == true)
                    {
                        RadioButtonColorModeMonochrome.IsEnabled = true;
                        if (!colorSelected)
                        {
                            RadioButtonColorModeMonochrome.IsChecked = true;
                            colorSelected = true;
                        }
                    }
                    else
                    {
                        RadioButtonColorModeMonochrome.IsEnabled = false;
                        RadioButtonColorModeMonochrome.IsChecked = false;
                    }

                    FrameLeftPaneScanColor.Visibility = Visibility.Visible;
                }

                // resolution
                if (selectedScanner.fake)
                {

                }
                else if (RadioButtonSourceFlatbed.IsChecked == true)
                {
                    GenerateResolutions(selectedScanner.scanner.FlatbedConfiguration, ComboBoxResolution, resolutions);
                    FrameLeftPaneResolution.Visibility = Visibility.Visible;
                }
                else if (RadioButtonSourceFeeder.IsChecked == true)
                {
                    GenerateResolutions(selectedScanner.scanner.FeederConfiguration, ComboBoxResolution, resolutions);
                    FrameLeftPaneResolution.Visibility = Visibility.Visible;
                } else FrameLeftPaneResolution.Visibility = Visibility.Collapsed;

                // file formats
                if (selectedScanner.fake)
                {
                    
                }
                else if (RadioButtonSourceAutomatic.IsChecked == true)
                {
                    GetSupportedFormats(selectedScanner.scanner.AutoConfiguration, formats, selectedScanner.scanner, ComboBoxFormat);
                }
                else if (RadioButtonSourceFlatbed.IsChecked == true)
                {
                    GetSupportedFormats(selectedScanner.scanner.FlatbedConfiguration, formats, selectedScanner.scanner, ComboBoxFormat);
                }
                else if (RadioButtonSourceFeeder.IsChecked == true)
                {
                    GetSupportedFormats(selectedScanner.scanner.FeederConfiguration, formats, selectedScanner.scanner, ComboBoxFormat);
                }

                // duplex
                if (selectedScanner.feederDuplexAllowed == true)
                {
                    CheckBoxDuplex.IsEnabled = true;
                    CheckBoxDuplex.IsChecked = false;
                }

                // preview
                if (RadioButtonSourceAutomatic.IsChecked == true)
                {
                    MenuFlyoutItemButtonScanPreview.IsEnabled = FontIconAutoPreviewSupported.Visibility == Visibility.Visible;
                }
                else if (RadioButtonSourceFlatbed.IsChecked == true)
                {
                    MenuFlyoutItemButtonScanPreview.IsEnabled = (bool)selectedScanner.flatbedPreviewAllowed;
                }
                else if (RadioButtonSourceFeeder.IsChecked == true)
                {
                    MenuFlyoutItemButtonScanPreview.IsEnabled = (bool)selectedScanner.feederPreviewAllowed;
                }
                else MenuFlyoutItemButtonScanPreview.IsEnabled = false;
            });
        }

        private void IgnorePointerWheel(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            e.Handled = true;
        }

        private async void TeachingTipDevices_ActionButtonClick(Microsoft.UI.Xaml.Controls.TeachingTip sender, object args)
        {
            await Launcher.LaunchUriAsync(new Uri("ms-settings:printers"));
        }

        private void ButtonDevices_Click(object sender, RoutedEventArgs e)
        {
            ReliablyOpenTeachingTip(TeachingTipDevices);
        }

        private void PrepareTeachingTips()
        {
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
            {
                // v1809+
                TeachingTipDevices.ActionButtonStyle = RoundedButtonAccentStyle;
                TeachingTipEmpty.ActionButtonStyle = RoundedButtonAccentStyle;
                TeachingTipScope.ActionButtonStyle = RoundedButtonAccentStyle;
                TeachingTipRename.ActionButtonStyle = RoundedButtonAccentStyle;
            }
            TeachingTipEmpty.CloseButtonContent = LocalizedString("CloseButtonText");
        }

        private async void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High,
            () =>
            {
                double width = ((Frame)Window.Current.Content).ActualWidth;

                if (width < 750 && uiState != UIstate.small)            // narrow window
                {
                    uiState = UIstate.small;
                    ColumnLeftPane.MaxWidth = 0;
                    ColumnLeftPane.MinWidth = 0;
                    ColumnRightPane.MaxWidth = 0;
                    ColumnRightPane.MinWidth = 0;
                    FlipViewLeftPane.Items.Clear();
                    RightPane.Children.Clear();
                    try { SplitViewLeftPaneContent.Children.Add(LeftPaneScanOptions); } catch (Exception) { }
                    try { SplitViewContentPaneContent.Children.Add(LeftPaneManage); } catch (Exception) { }
                    GridLeftPaneFooterContent.Background = new SolidColorBrush(Colors.Transparent);
                    RectangleGridLeftPaneScanOptions.Fill = (Brush)Resources["SystemControlAcrylicElementBrush"];
                    RectangleGridLeftPaneFooter.Fill = (Brush)Resources["SystemControlAcrylicElementBrush"];
                    RectangleGridLeftPaneManage.Fill = (Brush)Resources["SystemControlAcrylicElementBrush"];
                    GridLeftPaneManageHeaderControls.Background = new SolidColorBrush(Colors.Transparent);
                    FrameLeftPaneScanHeader.Background = new SolidColorBrush(Colors.Transparent);
                    DropShadowPanelGridLeftPaneScanHeader.Margin = new Thickness(0);
                    DropShadowPanelGridLeftPaneFooter.Margin = new Thickness(0);
                    DropShadowPanelGridLeftPaneManage.Margin = new Thickness(0);
                    ButtonManage.IsChecked = false;
                    ButtonManage.Visibility = Visibility.Visible;
                    ButtonScanOptions.Visibility = Visibility.Visible;
                }
                else if (width >= 750 && width < 1750 && uiState != UIstate.full)       // normal window
                {
                    uiState = UIstate.full;
                    ColumnLeftPane.MinWidth = ColumnSidePaneMinWidth;
                    ColumnLeftPane.MaxWidth = ColumnSidePaneMaxWidth;
                    ColumnRightPane.MaxWidth = 0;
                    ColumnRightPane.MinWidth = 0;
                    SplitViewLeftPaneContent.Children.Clear();
                    SplitViewContentPaneContent.Children.Clear();
                    RightPane.Children.Clear();
                    try { FlipViewLeftPane.Items.Insert(0, LeftPaneScanOptions); } catch (Exception) { }
                    try { FlipViewLeftPane.Items.Add(LeftPaneManage); } catch (Exception) { }
                    GridLeftPaneFooterContent.Background = (Brush)Resources["SystemControlAcrylicWindowBrush"];
                    RectangleGridLeftPaneScanOptions.Fill = (Brush)Resources["ApplicationPageBackgroundThemeBrush"];
                    RectangleGridLeftPaneFooter.Fill = (Brush)Resources["ApplicationPageBackgroundThemeBrush"];
                    RectangleGridLeftPaneManage.Fill = (Brush)Resources["ApplicationPageBackgroundThemeBrush"];
                    GridLeftPaneManageHeaderControls.Background = (Brush)Resources["SystemControlAcrylicWindowBrush"];
                    FrameLeftPaneScanHeader.Background = (Brush)Resources["SystemControlAcrylicWindowBrush"];
                    DropShadowPanelGridLeftPaneScanHeader.Margin = new Thickness(16, 0, 16, 0);
                    DropShadowPanelGridLeftPaneFooter.Margin = new Thickness(16,0,16,0);
                    DropShadowPanelGridLeftPaneManage.Margin = new Thickness(16,0,16,0);
                    ButtonManage.Visibility = Visibility.Visible;
                    ButtonScanOptions.Visibility = Visibility.Collapsed;
                } else if (width >= 1750 && uiState != UIstate.wide)    // wide window
                {
                    uiState = UIstate.wide;
                    ColumnLeftPane.MinWidth = ColumnSidePaneMinWidth;
                    ColumnLeftPane.MaxWidth = ColumnSidePaneMaxWidth;
                    ColumnRightPane.MaxWidth = ColumnSidePaneMaxWidth;
                    ColumnRightPane.MinWidth = ColumnSidePaneMaxWidth;
                    SplitViewLeftPaneContent.Children.Clear();
                    SplitViewContentPaneContent.Children.Clear();
                    try { FlipViewLeftPane.Items.Remove(LeftPaneManage); } catch (Exception) { }
                    try { FlipViewLeftPane.Items.Insert(0, LeftPaneScanOptions); } catch (Exception) { }
                    try { RightPane.Children.Add(LeftPaneManage); } catch (Exception) { }
                    GridLeftPaneFooterContent.Background = (Brush)Resources["SystemControlAcrylicWindowBrush"];
                    RectangleGridLeftPaneScanOptions.Fill = (Brush)Resources["ApplicationPageBackgroundThemeBrush"];
                    RectangleGridLeftPaneFooter.Fill = (Brush)Resources["ApplicationPageBackgroundThemeBrush"];
                    RectangleGridLeftPaneManage.Fill = (Brush)Resources["ApplicationPageBackgroundThemeBrush"];
                    FrameLeftPaneScanHeader.Background = (Brush)Resources["SystemControlAcrylicWindowBrush"];
                    DropShadowPanelGridLeftPaneScanHeader.Margin = new Thickness(16, 0, 16, 0);
                    DropShadowPanelGridLeftPaneFooter.Margin = new Thickness(16, 0, 16, 0);
                    DropShadowPanelGridLeftPaneManage.Margin = new Thickness(16, 0, 16, 0);
                    ButtonManage.Visibility = Visibility.Collapsed;
                    ButtonScanOptions.Visibility = Visibility.Collapsed;
                }
                
                if ((GridContentPaneTopToolbar.ActualWidth - StackPanelContentPaneTopToolbarText.ActualWidth) / 2 <= currentTitleBarButtonWidth
                || uiState == UIstate.small)
                {
                    StackPanelContentPaneTopToolbarText.HorizontalAlignment = HorizontalAlignment.Left;
                }
                else
                {
                    StackPanelContentPaneTopToolbarText.HorizontalAlignment = HorizontalAlignment.Center;
                }

                RectangleGeometry rectangleClip = new RectangleGeometry();
                rectangleClip.Rect = new Rect(0, 0, Double.PositiveInfinity,
                    GridContentPaneTopToolbar.ActualHeight);
                GridContentPaneTopToolbar.Clip = rectangleClip;
            });
        }

        private async void FlipViewLeftPane_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                if (FlipViewLeftPane.SelectedIndex == 0)
                {
                    ButtonManage.IsChecked = false;
                    ButtonScanOptions.IsChecked = true;
                }
                else if (FlipViewLeftPane.SelectedIndex == 1)
                {
                    ButtonManage.IsChecked = true;
                    ButtonScanOptions.IsChecked = false;
                } 
                else
                {
                    ButtonManage.IsChecked = false;
                    ButtonScanOptions.IsChecked = false;
                }
            });
        }

        private async void ButtonScanOptions_Checked(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                if (uiState == UIstate.small) SplitViewLeftPane.IsPaneOpen = true;
                else FlipViewLeftPane.SelectedIndex = 0;
            });
        }

        private async void ButtonManage_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High,
            () =>
            {
                if (ButtonManage.IsChecked == true)
                {
                    if (uiState == UIstate.small)
                    {
                        SplitViewContentPane.IsPaneOpen = true;
                    }
                    else if (uiState == UIstate.full)
                    {
                        if (FlipViewLeftPane.SelectedIndex == 1) FlipViewLeftPane.SelectedIndex = 0;
                        else FlipViewLeftPane.SelectedIndex = 1;
                    }
                }
                else
                {
                    if (uiState == UIstate.small)
                    {
                        SplitViewContentPane.IsPaneOpen = false;
                    }
                    ButtonManage.IsChecked = false;
                }
            });
        }


        private void MenuFlyoutItemButtonScanPreview_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButtonSourceAutomatic.IsChecked == true)
            {
                if (selectedScanner.autoPreviewAllowed == false)
                {
                    // auto preview only simulated
                    if (selectedScanner.flatbedPreviewAllowed == true)
                    {
                        Frame.Navigate(typeof(PreviewPage), new PreviewPageIntent(selectedScanner.scanner, ImageScannerScanSource.Flatbed, RadioButtonSourceAutomatic.Content.ToString()), new DrillInNavigationTransitionInfo());
                    }
                    else if (selectedScanner.feederPreviewAllowed == true)
                    {
                        Frame.Navigate(typeof(PreviewPage), new PreviewPageIntent(selectedScanner.scanner, ImageScannerScanSource.Feeder, RadioButtonSourceAutomatic.Content.ToString()), new DrillInNavigationTransitionInfo());
                    }
                }
                else
                {
                    // auto preview natively supported
                    Frame.Navigate(typeof(PreviewPage), new PreviewPageIntent(selectedScanner.scanner, ImageScannerScanSource.AutoConfigured, RadioButtonSourceAutomatic.Content.ToString()), new DrillInNavigationTransitionInfo());
                }
            }
            else if (RadioButtonSourceFlatbed.IsChecked == true) Frame.Navigate(typeof(PreviewPage), new PreviewPageIntent(selectedScanner.scanner, ImageScannerScanSource.Flatbed, RadioButtonSourceFlatbed.Content.ToString()), new DrillInNavigationTransitionInfo());
            else if (RadioButtonSourceFeeder.IsChecked == true) Frame.Navigate(typeof(PreviewPage), new PreviewPageIntent(selectedScanner.scanner, ImageScannerScanSource.Feeder, RadioButtonSourceFeeder.Content.ToString()), new DrillInNavigationTransitionInfo());
        }


        private async Task<bool> Scan(bool startFresh, IReadOnlyList<StorageFile> debugFiles)
        {
            try
            {
                // disable controls and show progress bar
                await RunOnUIThreadAsync(CoreDispatcherPriority.High,
                async () =>
                {
                    ButtonLeftPaneScan.IsEnabled = false;
                    await TransitionLeftPaneButtonsForScan(true);
                    FrameLeftPaneScanSource.IsEnabled = false;
                    FrameLeftPaneScanSourceMode.IsEnabled = false;
                    FrameLeftPaneScanColor.IsEnabled = false;
                    FrameLeftPaneResolution.IsEnabled = false;
                    FrameLeftPaneScanFeeder.IsEnabled = false;
                    FrameLeftPaneScanFormat.IsEnabled = false;
                    LockPaneManage(true);
                    LockToolbar();
                    StoryboardProgressBarScanBegin.Begin();
                });
                
                flowState = FlowState.scanning;
                bool scanSuccessful = false;
                if (startFresh)
                {
                    scanResult = null;
                    await InitializeTempFolder();
                }
                Tuple<ImageScannerFormat, SupportedFormat?> selectedFormat;                

                ImageScannerScanSource scanSource;

                if (debugFiles == null)
                {
                    // no debug files provided, commence actual scan
                    // get selected format
                    selectedFormat = await PrepareScanConfig();
                    if (selectedFormat == null) return false;

                    // get selected color mode
                    ImageScannerColorMode? selectedColorMode = GetDesiredColorMode();
                    if (selectedColorMode == null && (RadioButtonSourceFlatbed.IsChecked == true || RadioButtonSourceFeeder.IsChecked == true))
                    {
                        ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageScanErrorHeader"), LocalizedString("ErrorMessageScanErrorBody"));
                        await CancelScan();
                        return false;
                    }

                    // get selected resolution
                    ImageScannerResolution? selectedResolution = GetDesiredResolution(ComboBoxResolution);
                    if (selectedResolution == null && (RadioButtonSourceFlatbed.IsChecked == true || RadioButtonSourceFeeder.IsChecked == true))
                    {
                        ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageScanErrorHeader"), LocalizedString("ErrorMessageScanErrorBody"));
                        await CancelScan();
                        return false;
                    }

                    // prepare scan config
                    if (RadioButtonSourceAutomatic.IsChecked == true)
                    {
                        selectedScanner.scanner.AutoConfiguration.Format = selectedFormat.Item1;

                        scanSource = ImageScannerScanSource.AutoConfigured;
                    }
                    else if (RadioButtonSourceFlatbed.IsChecked == true)
                    {
                        selectedScanner.scanner.FlatbedConfiguration.Format = selectedFormat.Item1;
                        selectedScanner.scanner.FlatbedConfiguration.ColorMode = (ImageScannerColorMode)selectedColorMode;
                        selectedScanner.scanner.FlatbedConfiguration.DesiredResolution = (ImageScannerResolution)selectedResolution;

                        scanSource = ImageScannerScanSource.Flatbed;
                    }
                    else if (RadioButtonSourceFeeder.IsChecked == true)
                    {
                        selectedScanner.scanner.FeederConfiguration.Format = selectedFormat.Item1;
                        selectedScanner.scanner.FeederConfiguration.ColorMode = (ImageScannerColorMode)selectedColorMode;
                        selectedScanner.scanner.FeederConfiguration.DesiredResolution = (ImageScannerResolution)selectedResolution;

                        if (CheckBoxAllPages.IsChecked == true) selectedScanner.scanner.FeederConfiguration.MaxNumberOfPages = 10;
                        else selectedScanner.scanner.FeederConfiguration.MaxNumberOfPages = 1;

                        if (CheckBoxDuplex.IsChecked == true) selectedScanner.scanner.FeederConfiguration.Duplex = true;
                        else selectedScanner.scanner.FeederConfiguration.Duplex = false;

                        scanSource = ImageScannerScanSource.Feeder;
                    }
                    else
                    {
                        ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageScanErrorHeader"), LocalizedString("ErrorMessageScanErrorBody"));
                        await CancelScan();
                        return false;
                    }

                    await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => ButtonLeftPaneCancel.IsEnabled = true);

                    // determine target folder
                    StorageFolder folderToScanTo;
                    if (selectedFormat.Item2 == null)
                    {
                        folderToScanTo = scanFolder;
                    }
                    else if (selectedFormat.Item2 == SupportedFormat.PDF)
                    {
                        folderToScanTo = await folderTemp.GetFolderAsync("conversion");
                    }
                    else
                    {
                        folderToScanTo = folderTemp;
                    }

                    // get scan
                    ImageScannerScanResult scannerScanResult = null;
                    scanProgress = new Progress<uint>();
                    scanProgress.ProgressChanged += ScanProgress_ProgressChanged;
                    scanCancellationToken = new CancellationTokenSource();

                    try
                    {
                        scannerScanResult = await selectedScanner.scanner.ScanFilesToFolderAsync(scanSource, folderToScanTo).AsTask(scanCancellationToken.Token, scanProgress);
                    }
                    catch (Exception exc)
                    {
                        ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageScanScannerErrorHeader"), LocalizedString("ErrorMessageScanScannerErrorBody") + "\n" + exc.HResult);
                        await CancelScan();
                        return false;
                    }

                    await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => ButtonLeftPaneCancel.IsEnabled = false);

                    if (settingAppendTime) try { await SetInitialNames(scannerScanResult.ScannedFiles); } catch (Exception) { }

                    if (ScanResultValid(scannerScanResult))
                    {
                        await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => LeftPaneManageInitialText.Visibility = Visibility.Collapsed);
                        if (scanResult == null)
                        {
                            if (selectedFormat.Item2 == null)
                            {
                                // no conversion
                                scanResult = await ScanResult.Create(scannerScanResult.ScannedFiles, scanFolder);
                            }
                            else
                            {
                                // conversion necessary
                                scanResult = await ScanResult.Create(scannerScanResult.ScannedFiles, scanFolder, (SupportedFormat)selectedFormat.Item2);
                            }

                            FlipViewScan.ItemsSource = scanResult.elements;
                            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => InitializePaneManage());
                        }
                        else
                        {
                            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, async () => {
                                if (selectedFormat.Item2 != null) await scanResult.AddFiles(scannerScanResult.ScannedFiles, (SupportedFormat)selectedFormat.Item2);
                                else await scanResult.AddFiles(scannerScanResult.ScannedFiles, null);
                                FlipViewScan.SelectedIndex = scanResult.GetTotalNumberOfScans() - 1;
                            });
                        }
                    }
                }
                else
                {
                    SupportedFormat selectedDebugFormat = (SupportedFormat)ComboBoxDebugFormat.SelectedItem;
                        
                    // debug files provided, create scanResult from these
                    List<StorageFile> copiedDebugFiles = new List<StorageFile>();
                    foreach (StorageFile file in debugFiles)
                    {
                        if (selectedDebugFormat == SupportedFormat.PDF) copiedDebugFiles.Add(await file.CopyAsync(folderConversion));
                        else copiedDebugFiles.Add(await file.CopyAsync(folderTemp));
                    }

                    if (settingAppendTime) try { await SetInitialNames(copiedDebugFiles); } catch (Exception) { }

                    if (scanResult == null || startFresh)
                    {
                        if (ConvertFormatStringToSupportedFormat(copiedDebugFiles[0].FileType) != selectedDebugFormat)
                        {
                            scanResult = await ScanResult.Create(copiedDebugFiles, scanFolder, selectedDebugFormat);
                        }
                        else
                        {
                            scanResult = await ScanResult.Create(copiedDebugFiles, scanFolder);
                        }
                        await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => {
                            FlipViewScan.ItemsSource = scanResult.elements;
                            InitializePaneManage();
                        });
                    }
                    else
                    {
                        await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, async () => {
                            foreach (StorageFile file in copiedDebugFiles)
                            {
                                if (selectedDebugFormat == SupportedFormat.PDF) await file.MoveAsync(folderConversion, RemoveNumbering(file.Name), NameCollisionOption.GenerateUniqueName);
                                else await file.MoveAsync(scanFolder, RemoveNumbering(file.Name), NameCollisionOption.GenerateUniqueName);
                            } 
                            await scanResult.AddFiles(copiedDebugFiles, selectedDebugFormat);
                            FlipViewScan.SelectedIndex = scanResult.GetTotalNumberOfScans() - 1;
                        });
                    }
                }

                flowState = FlowState.initial;
                scanSuccessful = true;

                // reenable controls and change UI
                await RunOnUIThreadAsync(CoreDispatcherPriority.High,
                async () =>
                {
                    await TransitionLeftPaneButtonsForScan(false);
                    FrameLeftPaneScanSource.IsEnabled = true;
                    FrameLeftPaneScanSourceMode.IsEnabled = true;
                    FrameLeftPaneScanColor.IsEnabled = true;
                    FrameLeftPaneResolution.IsEnabled = true;
                    FrameLeftPaneScanFeeder.IsEnabled = true;
                    FrameLeftPaneScanFormat.IsEnabled = true;
                    UnlockPaneManage(false);
                    UnlockToolbar();
                    StoryboardProgressBarScanEnd.Begin();
                    FlipViewScan.Visibility = Visibility.Visible;
                    LeftPaneManageInitialText.Visibility = Visibility.Collapsed;
                    if (startFresh) FlipViewScan.SelectedIndex = 0;
                    FlipViewScan_SelectionChanged(null, null);
                    TextBlockContentPaneGridProgressRingScan.Visibility = Visibility.Collapsed;
                    TextBlockContentPaneGridProgressRingScan.Text = "";
                });

                await CleanMenuForNewScanner(selectedScanner);
                await RefreshScanButton();

                // send toast if the app is minimized
                if (settingNotificationScanComplete && !inForeground) SendToastNotification(LocalizedString("NotificationScanCompleteHeader"), LocalizedString("NotificationScanCompleteBody"), 5);

                // ask for feedback
                scanNumber++;
                if (scanNumber == 10) ReliablyOpenTeachingTip(TeachingTipFeedback);
                localSettingsContainer.Values["scanNumber"] = ((int)localSettingsContainer.Values["scanNumber"]) + 1;

                return scanSuccessful;
            }
            catch (Exception)
            {
                ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageScanErrorHeader"), LocalizedString("ErrorMessageScanErrorBody"));
                await CancelScan();
                return false;
            }            
        }


        private void InitializePaneManage()
        {
            if (scanResult == null) throw new ApplicationException("Couldn't initialize PaneManage. (scanResult null)");

            if (scanResult.GetFileFormat() == SupportedFormat.PDF)
            {
                LeftPaneListViewManage.ItemsSource = null;
                LeftPaneListViewManage.Visibility = Visibility.Collapsed;
                LeftPaneGridViewManage.ItemsSource = scanResult.elements;
                LeftPaneGridViewManage.Visibility = Visibility.Visible;
            }
            else
            {
                LeftPaneGridViewManage.ItemsSource = null;
                LeftPaneGridViewManage.Visibility = Visibility.Collapsed;
                LeftPaneListViewManage.ItemsSource = scanResult.elements;
                LeftPaneListViewManage.Visibility = Visibility.Visible;
            }
        }


        private async void ScanProgress_ProgressChanged(object sender, uint e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High,
            () =>
            {
                if (e <= 1)
                {
                    TextBlockContentPaneGridProgressRingScan.Visibility = Visibility.Collapsed;
                    TextBlockContentPaneGridProgressRingScan.Text = "";
                }
                else
                {
                    TextBlockContentPaneGridProgressRingScan.Text = e.ToString();
                    TextBlockContentPaneGridProgressRingScan.Visibility = Visibility.Visible;
                }
            });
        }

        private async Task<Tuple<ImageScannerFormat, SupportedFormat?>> PrepareScanConfig()
        {
            var selectedFormat = GetDesiredFormat(ComboBoxFormat, formats);
            if (selectedFormat == null)
            {
                ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageNoFormatHeader"), LocalizedString("ErrorMessageNoFormatBody"));
            }
            
            if (RadioButtonSourceAutomatic.IsChecked == true)
            {
                selectedScanner.scanner.AutoConfiguration.Format = selectedFormat.Item1;
            } 
            else if (RadioButtonSourceFlatbed.IsChecked == true)
            {
                // color mode
                ImageScannerColorMode? selectedColorMode = GetDesiredColorMode();
                if (selectedColorMode == null)
                {
                    ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageHeader"), LocalizedString("ErrorMessageBody"));
                    await CancelScan();
                    return null;
                }
                else selectedScanner.scanner.FlatbedConfiguration.ColorMode = (ImageScannerColorMode)selectedColorMode;

                // resolution
                ImageScannerResolution? selectedResolution = GetDesiredResolution(ComboBoxResolution);
                if (selectedResolution == null)
                {
                    ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageHeader"), LocalizedString("ErrorMessageBody"));
                    await CancelScan();
                    return null;
                }
                selectedScanner.scanner.FlatbedConfiguration.DesiredResolution = (ImageScannerResolution)selectedResolution;

                // format
                selectedScanner.scanner.FlatbedConfiguration.Format = selectedFormat.Item1;
            } 
            else if (RadioButtonSourceFeeder.IsChecked == true)
            {
                // color mode
                ImageScannerColorMode? selectedColorMode = GetDesiredColorMode();
                if (selectedColorMode == null)
                {
                    ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageHeader"), LocalizedString("ErrorMessageBody"));
                    await CancelScan();
                    return null;
                }
                else selectedScanner.scanner.FeederConfiguration.ColorMode = (ImageScannerColorMode)selectedColorMode;

                // resolution
                ImageScannerResolution? selectedResolution = GetDesiredResolution(ComboBoxResolution);
                if (selectedResolution == null)
                {
                    ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageHeader"), LocalizedString("ErrorMessageBody"));
                    await CancelScan();
                    return null;
                }
                selectedScanner.scanner.FeederConfiguration.DesiredResolution = (ImageScannerResolution)selectedResolution;

                // format
                selectedScanner.scanner.FeederConfiguration.Format = selectedFormat.Item1;

                // additional options
                if (CheckBoxDuplex.IsChecked == true) selectedScanner.scanner.FeederConfiguration.Duplex = true;
                else selectedScanner.scanner.FeederConfiguration.Duplex = false;
                if (CheckBoxAllPages.IsChecked == true) selectedScanner.scanner.FeederConfiguration.MaxNumberOfPages = 10;
                else selectedScanner.scanner.FeederConfiguration.MaxNumberOfPages = 1;
            } 
            else
            {
                ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageHeader"), LocalizedString("ErrorMessageBody"));
            }

            return selectedFormat;
        }


        private async Task CancelScan()
        {
            throw new NotImplementedException();

            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                
            });
        }


        private ImageScannerColorMode? GetDesiredColorMode()
        {
            if (RadioButtonColorModeColor.IsChecked.Value) return ImageScannerColorMode.Color;
            if (RadioButtonColorModeGrayscale.IsChecked.Value) return ImageScannerColorMode.Grayscale;
            if (RadioButtonColorModeMonochrome.IsChecked.Value) return ImageScannerColorMode.Monochrome;
            return null;
        }


        private async void ButtonLeftPaneScan_Click(Microsoft.UI.Xaml.Controls.SplitButton sender, Microsoft.UI.Xaml.Controls.SplitButtonClickEventArgs args)
        {
            await Scan(makeNextScanAFreshOne, null);
        }

        private async void ButtonScanFresh_Click(object sender, RoutedEventArgs e)
        {
            await Scan(true, null);
        }

        private void StackPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ScrollViewerLeftPaneScan.ScrollableHeight >= 0) RectangleGridLeftPaneScanOptions.Visibility = Visibility.Visible;
            else RectangleGridLeftPaneScanOptions.Visibility = Visibility.Visible;
        }

        private async Task RefreshFileName()
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                if (scanResult != null) 
                {
                    if (scanResult.GetFileFormat() == SupportedFormat.PDF)
                    {
                        if (scanResult.pdf == null) return;
                        
                        TextBlockContentPaneTopToolbarFileName.Text = scanResult.pdf.DisplayName.Replace(scanResult.pdf.FileType, "");
                        TextBlockContentPaneTopToolbarFileExtension.Text = scanResult.pdf.FileType;
                    }
                    else
                    {
                        if (FlipViewScan.SelectedIndex < 0) return;

                        StorageFile selectedFile = ((ScanResultElement)FlipViewScan.SelectedItem).ScanFile;
                        TextBlockContentPaneTopToolbarFileName.Text = selectedFile.DisplayName;
                        TextBlockContentPaneTopToolbarFileExtension.Text = selectedFile.FileType;
                    }
                } else
                {
                    TextBlockContentPaneTopToolbarFileName.Text = "";
                    TextBlockContentPaneTopToolbarFileExtension.Text = "";
                }
            });
        }

        private async void FlipViewScan_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await RefreshFileName();
            await RunOnUIThreadAsync(CoreDispatcherPriority.High,
            () =>
            {
                if (flowState != FlowState.select)
                {
                    PaneManageSelectIndex(FlipViewScan.SelectedIndex);
                }
            });
        }

        private void PaneManageSelectIndex(int index)
        {
            if (scanResult == null) throw new ApplicationException("Unable to select index in PaneManage. (scanResult null)");

            if (scanResult.GetFileFormat() == SupportedFormat.PDF) LeftPaneGridViewManage.SelectedIndex = index;
            else LeftPaneListViewManage.SelectedIndex = index;
        }

        private int PaneManageGetFirstSelectedIndex()
        {
            if (scanResult == null) throw new ApplicationException("Unable to get first selected index in PaneManage. (scanResult null)");

            if (scanResult.GetFileFormat() == SupportedFormat.PDF) return LeftPaneGridViewManage.SelectedIndex;
            return LeftPaneListViewManage.SelectedIndex;
        }

        private IReadOnlyList<ItemIndexRange> PaneManageGetSelectedRanges()
        {
            if (scanResult == null) throw new ApplicationException("Unable to get selected ranges in PaneManage. (scanResult null)");

            if (scanResult.GetFileFormat() == SupportedFormat.PDF) return LeftPaneGridViewManage.SelectedRanges;
            return LeftPaneListViewManage.SelectedRanges;
        }

        private async void ButtonLeftPaneScanFolder_Click(object sender, RoutedEventArgs e)
        {
            try { await Launcher.LaunchFolderAsync(scanFolder); }
            catch (Exception) { }
        }

        private void LockToolbar()
        {
            ButtonCrop.IsEnabled = false;
            ButtonRotate.IsEnabled = false;
            ButtonDraw.IsEnabled = false;
            ButtonRename.IsEnabled = false;
            ButtonDelete.IsEnabled = false;
            ButtonCopy.IsEnabled = false;
            ButtonOpenWith.IsEnabled = false;
            ButtonShare.IsEnabled = false;
        }

        private void UnlockToolbar()
        {
            ButtonCrop.IsEnabled = true;
            ButtonRotate.IsEnabled = true;
            ButtonDraw.IsEnabled = true;
            ButtonRename.IsEnabled = true;
            ButtonDelete.IsEnabled = true;
            ButtonCopy.IsEnabled = true;
            ButtonOpenWith.IsEnabled = true;
            ButtonShare.IsEnabled = true;
        }

        private async Task TransitionLeftPaneButtonsForScan(bool beginScan)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High,
            () =>
            {
                if (beginScan)
                {
                    ButtonLeftPaneSettings.IsEnabled = false;
                    StoryboardChangeButtonsBeginScan.Begin();
                }
                else
                {
                    ButtonLeftPaneSettings.IsEnabled = true;
                    StoryboardChangeButtonsEndScan.Begin();
                }
            });
        }

        private async void LeftPaneManage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (flowState)
            {
                case FlowState.initial:
                    FlipViewScan.SelectedIndex = PaneManageGetFirstSelectedIndex();
                    await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => UnlockToolbar() );
                    break;
                case FlowState.scanning:
                    FlipViewScan.SelectedIndex = PaneManageGetFirstSelectedIndex();
                    break;
                case FlowState.select:
                case FlowState.crop:
                case FlowState.draw:
                    break;
            }
        }

        private async void ButtonDebugAddScanner_Click(object sender, RoutedEventArgs e)
        {
            RecognizedScanner fakeScanner = new RecognizedScanner(TextBoxDebugFakeScannerName.Text, ToggleSwitchDebugFakeScannerAuto.IsOn, (bool)ToggleSwitchDebugFakeScannerAutoPreview.IsOn,
                ToggleSwitchDebugFakeScannerFlatbed.IsOn, ToggleSwitchDebugFakeScannerFlatbedPreview.IsOn, (bool)CheckBoxDebugFakeScannerFlatbedColor.IsChecked,
                (bool)CheckBoxDebugFakeScannerFlatbedGrayscale.IsChecked, (bool)CheckBoxDebugFakeScannerFlatbedMonochrome.IsChecked, ToggleSwitchDebugFakeScannerFeeder.IsOn,
                ToggleSwitchDebugFakeScannerFeederPreview.IsOn, (bool)CheckBoxDebugFakeScannerFeederColor.IsChecked, (bool)CheckBoxDebugFakeScannerFeederGrayscale.IsChecked,
                (bool)CheckBoxDebugFakeScannerFeederMonochrome.IsChecked, (bool)CheckBoxDebugFakeScannerFeederDuplex.IsChecked);
            
            await RunOnUIThreadAsync(CoreDispatcherPriority.High,
            () =>
            {
                scannerList.Insert(ComboBoxScanners.Items.Count - 1, CreateComboBoxItem(fakeScanner.scannerName, fakeScanner));

                if (settingAutomaticScannerSelection == true && ComboBoxScanners.SelectedIndex == -1 && flowState != FlowState.scanning)
                    ComboBoxScanners.SelectedIndex = 0;
            });
        }

        private void ButtonDebugShowError_Click(object sender, RoutedEventArgs e)
        {
            ErrorMessage.ShowErrorMessage(TeachingTipEmpty, TextBoxDebugShowErrorTitle.Text, TextBoxDebugShowErrorSubtitle.Text);
        }

        private async Task RefreshScanButton()
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High,
            () =>
            {
                if (scanResult == null || scanResult.GetTotalNumberOfScans() == 0) 
                {
                    FontIconButtonScanAdd.Visibility = Visibility.Collapsed;
                    FontIconButtonScanStartFresh.Visibility = Visibility.Collapsed;
                    makeNextScanAFreshOne = false;
                    MenuFlyoutItemButtonScan.IsEnabled = true;
                    MenuFlyoutItemButtonScan.FontWeight = FontWeights.Bold;
                    MenuFlyoutItemButtonScan.Icon = null;
                    MenuFlyoutItemButtonScanFresh.IsEnabled = false;
                    MenuFlyoutItemButtonScanFresh.FontWeight = FontWeights.Normal;
                    return;
                }

                // get currently selected format
                var selectedFormatTuple = GetDesiredFormat(ComboBoxFormat, formats);
                if (selectedFormatTuple == null) return;
                SupportedFormat selectedFormat;
                if (selectedFormatTuple.Item2 != null)
                {
                    selectedFormat = (SupportedFormat) selectedFormatTuple.Item2;
                }
                else
                {
                    selectedFormat = ConvertImageScannerFormatToSupportedFormat(selectedFormatTuple.Item1);
                }

                var currentFormat = scanResult.GetFileFormat();
                if (selectedFormat == currentFormat)
                {
                    FontIconButtonScanAdd.Visibility = Visibility.Visible;
                    FontIconButtonScanStartFresh.Visibility = Visibility.Collapsed;
                    makeNextScanAFreshOne = false;
                    MenuFlyoutItemButtonScan.IsEnabled = true;
                    MenuFlyoutItemButtonScan.FontWeight = FontWeights.Bold;
                    MenuFlyoutItemButtonScan.Icon = new SymbolIcon(Symbol.Add);
                    MenuFlyoutItemButtonScanFresh.IsEnabled = true;
                    MenuFlyoutItemButtonScanFresh.FontWeight = FontWeights.Normal;
                }
                else
                {
                    FontIconButtonScanAdd.Visibility = Visibility.Collapsed;
                    FontIconButtonScanStartFresh.Visibility = Visibility.Visible;
                    makeNextScanAFreshOne = true;
                    MenuFlyoutItemButtonScan.IsEnabled = false;
                    MenuFlyoutItemButtonScan.FontWeight = FontWeights.Normal;
                    MenuFlyoutItemButtonScan.Icon = null;
                    MenuFlyoutItemButtonScanFresh.IsEnabled = true;
                    MenuFlyoutItemButtonScanFresh.FontWeight = FontWeights.Bold;
                }
            });
        }

        private async void ComboBoxFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await RefreshScanButton();
        }

        private void MenuFlyoutItemButtonScan_Click(object sender, RoutedEventArgs e)
        {
            ButtonLeftPaneScan_Click(null, null);
        }

        private async void ButtonDebugSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".tif");
            picker.FileTypeFilter.Add(".tiff");
            picker.FileTypeFilter.Add(".bmp");

            IReadOnlyList<StorageFile> files = await picker.PickMultipleFilesAsync();
            if (files.Count == 0) return;

            ContentDialogDebug.Hide();

            await Scan((bool) CheckBoxDebugStartFresh.IsChecked, files);
        }

        private async void TransitionToEditingMode(SummonToolbar summonToolbar)
        {
            FlipViewLeftPane.IsEnabled = false;
            LockPaneManage(true);

            switch (summonToolbar)
            {
                case SummonToolbar.Hidden:
                    throw new NotImplementedException();
                case SummonToolbar.Crop:
                    flowState = FlowState.crop;
                    StoryboardToolbarTransitionToCrop.Begin();
                    Thickness padding = ImageCropperScan.Padding;
                    padding.Top = 32 + GridContentPaneTopToolbar.ActualHeight;
                    ImageCropperScan.Padding = padding;
                    try
                    {
                        await ImageCropperScan.LoadImageFromFile(scanResult.GetImageFile(FlipViewScan.SelectedIndex));
                    }
                    catch (Exception)
                    {
                        ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageCropHeader"), 
                            LocalizedString("ErrorMessageCropBody"));
                        TransitionFromEditingMode();
                        return;
                    }
                    break;
                case SummonToolbar.Draw:
                    flowState = FlowState.draw;
                    StoryboardToolbarTransitionToDraw.Begin();
                    ImageEditDraw.Source = ((ScanResultElement)FlipViewScan.SelectedItem).CachedImage;
                    Tuple<double, double> imageMeasurements = GetImageMeasurements(((ScanResultElement)FlipViewScan.SelectedItem).CachedImage);
                    InitializeInkCanvas(InkCanvasEditDraw, imageMeasurements.Item1, imageMeasurements.Item2);
                    if (lastTouchDrawState == true)
                    {
                        try
                        {
                            IReadOnlyList<PointerDevice> pointerDevices = PointerDevice.GetPointerDevices();
                            foreach (var device in pointerDevices)
                            {
                                if (device.PointerDeviceType == PointerDeviceType.Touch)
                                {
                                    ButtonDrawTouch.IsChecked = true;
                                    break;
                                }
                            }
                        }
                        catch (Exception) { }
                    }
                    break;
                default:
                    break;
            }
        }

        private void TransitionFromEditingMode()
        {
            flowState = FlowState.initial;
            ScrollViewerEditDraw.ChangeView(0, 0, 0);
            FlipViewLeftPane.IsEnabled = true;
            UnlockPaneManage(false);
            StoryboardToolbarTransitionFromSpecial.Begin();
            try { ImageCropperScan.Source = null; } catch (Exception) { }
        }

        private void LockPaneManage(bool complete)
        {
            if (complete)
            {
                ButtonLeftPaneManageSelect.IsEnabled = false;
                ScrollViewerLeftPaneManage.IsEnabled = false;
            }
            ButtonLeftPaneManageRotate.IsEnabled = false;
            ButtonLeftPaneManageDelete.IsEnabled = false;
            ButtonLeftPaneManageCopy.IsEnabled = false;
            ButtonLeftPaneManageShare.IsEnabled = false;
        }

        private void UnlockPaneManage(bool complete)
        {
            ButtonLeftPaneManageSelect.IsEnabled = true;
            ScrollViewerLeftPaneManage.IsEnabled = true;
            if (complete)
            {
                ButtonLeftPaneManageRotate.IsEnabled = true;
                ButtonLeftPaneManageDelete.IsEnabled = true;
                ButtonLeftPaneManageCopy.IsEnabled = true;
                ButtonLeftPaneManageShare.IsEnabled = true;
            }
        }

        private void TransitionToSelectMode()
        {
            flowState = FlowState.select;
            LockToolbar();
            UnlockPaneManage(true);
            LeftPaneListViewManage.SelectionMode = ListViewSelectionMode.Multiple;
            LeftPaneListViewManage.CanDragItems = false;
            LeftPaneListViewManage.CanReorderItems = false;
            LeftPaneGridViewManage.SelectionMode = ListViewSelectionMode.Multiple;
            LeftPaneGridViewManage.CanDragItems = false;
            LeftPaneGridViewManage.CanReorderItems = false;
            ButtonLeftPaneManageSelect.IsChecked = true;
        }

        private void TransitionFromSelectMode()
        {
            flowState = FlowState.initial;
            LockToolbar();
            LockPaneManage(false);
            LeftPaneListViewManage.SelectionMode = ListViewSelectionMode.Single;
            LeftPaneListViewManage.CanDragItems = true;
            LeftPaneListViewManage.CanReorderItems = true;
            LeftPaneGridViewManage.SelectionMode = ListViewSelectionMode.Single;
            LeftPaneGridViewManage.CanDragItems = true;
            LeftPaneGridViewManage.CanReorderItems = true;
            ButtonLeftPaneManageSelect.IsChecked = false;
            if (scanResult != null && scanResult.GetTotalNumberOfScans() > 0)
            {
                FlipViewScan.SelectedIndex = 0;
                PaneManageSelectIndex(0);
            }
        }

        private void Share(Control targetControl)
        {
            Rect rectangle;
            ShareUIOptions shareUIOptions = new ShareUIOptions();

            if (targetControl != null)
            {
                GeneralTransform transform;
                transform = targetControl.TransformToVisual(null);
                rectangle = transform.TransformBounds(new Rect(0, 0, targetControl.ActualWidth, targetControl.ActualHeight));
                shareUIOptions.SelectionRect = rectangle;
            }

            DataTransferManager.ShowShareUI(shareUIOptions);
        }

        private void Share()
        {
            Share(null);
        }

        private void ButtonShare_Click(object sender, RoutedEventArgs e)
        {
            if (scanResult == null || scanResult.GetTotalNumberOfScans() == 0) return;

            if (scanResult.GetFileFormat() == SupportedFormat.PDF)
            {
                if (scanResult.GetTotalNumberOfScans() > 1)
                {
                    scopeAction = ScopeActions.Share;
                    TeachingTipScope.Target = ButtonShare;
                    TeachingTipScope.Title = LocalizedString("ScopeQuestionShare");
                    ReliablyOpenTeachingTip(TeachingTipScope);
                    return;
                }
                else
                {
                    shareIndexes = null;
                    Share(ButtonShare);
                    return;
                }
            }

            shareIndexes = new int[1];
            shareIndexes[0] = FlipViewScan.SelectedIndex;

            Share(ButtonShare);
        }

        private async void TeachingTipScope_ActionButtonClick(Microsoft.UI.Xaml.Controls.TeachingTip sender, object args)
        {
            // user wants to apply action for entire document
            switch (scopeAction)
            {
                case ScopeActions.Copy:
                    try
                    {
                        await scanResult.CopyAsync();
                        await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => {
                            TeachingTipScope.IsOpen = false;
                            StoryboardIconCopyDone1.Begin();
                        });
                    }
                    catch (Exception)
                    {
                        ErrorMessage.ShowErrorMessage(TeachingTipEmpty,
                            LocalizedString("ErrorMessageRenameHeader"), LocalizedString("ErrorMessageRenameBody"));
                    }
                    break;
                case ScopeActions.OpenWith:
                    await scanResult.OpenWithAsync();
                    await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => TeachingTipScope.IsOpen = false);
                    break;
                case ScopeActions.Share:
                    shareIndexes = null;
                    Share(ButtonShare);
                    await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => TeachingTipScope.IsOpen = false);
                    break;
                default:
                    break;
            }            
        }

        private async void SplitViewLeftPane_PaneClosed(SplitView sender, object args)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () => { ButtonScanOptions.IsChecked = false; });
        }

        private async void SplitViewContentPane_PaneClosed(SplitView sender, object args)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () => { ButtonManage.IsChecked = false; });
        }

        private async void ButtonCopy_Click(object sender, RoutedEventArgs e)
        {
            if (scanResult == null || scanResult.GetTotalNumberOfScans() == 0) return;

            try
            {
                if (scanResult.GetFileFormat() == SupportedFormat.PDF)
                {
                    if (scanResult.GetTotalNumberOfScans() > 1)
                    {
                        scopeAction = ScopeActions.Copy;
                        TeachingTipScope.Target = ButtonCopy;
                        TeachingTipScope.Title = LocalizedString("ScopeQuestionCopy");
                        ReliablyOpenTeachingTip(TeachingTipScope);
                        return;
                    }
                    else
                    {
                        await scanResult.CopyAsync();
                        await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => StoryboardIconCopyDone1.Begin());
                        return;
                    }
                }

                await scanResult.CopyImageAsync(FlipViewScan.SelectedIndex);
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => StoryboardIconCopyDone1.Begin());
            }
            catch (Exception)
            {
                ErrorMessage.ShowErrorMessage(TeachingTipEmpty,
                    LocalizedString("ErrorMessageCopyHeader"), LocalizedString("ErrorMessageCopyBody"));
            }            
        }

        private async void ButtonOpenWith_Click(object sender, RoutedEventArgs e)
        {
            if (scanResult == null || scanResult.GetTotalNumberOfScans() == 0) return;

            try
            {
                if (scanResult.GetFileFormat() == SupportedFormat.PDF)
                {
                    if (scanResult.GetTotalNumberOfScans() > 1)
                    {
                        scopeAction = ScopeActions.OpenWith;
                        TeachingTipScope.Target = ButtonOpenWith;
                        TeachingTipScope.Title = LocalizedString("ScopeQuestionOpenWith");
                        ReliablyOpenTeachingTip(TeachingTipScope);
                        return;
                    }
                    else
                    {
                        await scanResult.OpenWithAsync();
                        return;
                    }
                }

                await scanResult.OpenImageWithAsync(FlipViewScan.SelectedIndex);
            }
            catch (Exception) { }
        }

        private async void TeachingTipScope_CloseButtonClick(Microsoft.UI.Xaml.Controls.TeachingTip sender, object args)
        {
            // user wants to apply action for current image
            switch (scopeAction)
            {
                case ScopeActions.Copy:
                    await scanResult.CopyImageAsync(FlipViewScan.SelectedIndex);
                    await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => StoryboardIconCopyDone1.Begin());
                    break;
                case ScopeActions.OpenWith:
                    await scanResult.OpenImageWithAsync(FlipViewScan.SelectedIndex);
                    break;
                case ScopeActions.Share:
                    shareIndexes = new int[1];
                    shareIndexes[0] = FlipViewScan.SelectedIndex;
                    Share(ButtonShare);
                    break;
                default:
                    break;
            }
        }

        private async void LeftPaneManage_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            if (scanResult != null && scanResult.GetTotalNumberOfScans() > 1 
                && scanResult.GetFileFormat() == SupportedFormat.PDF)
            {
                // item order may have changed, generate PDF again
                await scanResult.ApplyElementOrderToFiles();
                await scanResult.GeneratePDF();
            }
        }

        private void TextBoxRename_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Accept || e.Key == VirtualKey.Enter)
            {
                TeachingTipRename_ActionButtonClick(null, null);
            }
        }

        private void TeachingTipRename_ActionButtonClick(Microsoft.UI.Xaml.Controls.TeachingTip sender, object args)
        {
            if (scanResult == null) return;

            if (scanResult.GetFileFormat() == SupportedFormat.PDF) Rename(null, TextBoxRename.Text);
            else Rename(FlipViewScan.SelectedIndex, TextBoxRename.Text);
        }

        private async void Rename(int? index, string newName)
        {
            try
            {
                if (scanResult != null)
                {
                    if (index == null && scanResult.GetFileFormat() == SupportedFormat.PDF)
                    {
                        // rename PDF
                        await scanResult.RenameScanAsync(newName + scanResult.pdf.FileType);
                        await RefreshFileName();
                    }
                    else if (scanResult.GetTotalNumberOfScans() - 1 >= index || newName.Length > 0)
                    {
                        // rename image file
                        StorageFile image = scanResult.GetImageFile((int)index);
                        await scanResult.RenameScanAsync((int)index, newName + image.FileType);
                        await RefreshFileName();
                    }
                    else return;

                    await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
                    () =>
                    {
                        TeachingTipRename.IsOpen = false;
                        StoryboardIconRenameDone1.Begin();
                    });
                }
            }
            catch (Exception)
            {
                ErrorMessage.ShowErrorMessage(TeachingTipEmpty, 
                    LocalizedString("ErrorMessageRenameHeader"), LocalizedString("ErrorMessageRenameBody"));
            }
        }

        private async void ButtonRename_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High,
            () =>
            {
                if (scanResult == null || scanResult.GetTotalNumberOfScans() == 0) return;

                if (scanResult.GetFileFormat() == SupportedFormat.PDF)
                {
                    // use PDF name
                    TextBoxRename.Text = scanResult.pdf.DisplayName;
                }
                else
                {
                    // use selected image file name
                    TextBoxRename.Text = scanResult.GetImageFile(FlipViewScan.SelectedIndex).DisplayName;
                }

                ReliablyOpenTeachingTip(TeachingTipRename);
                TextBoxRename.SelectAll();
                //TextBoxRename.Focus(FocusState.Programmatic);
            });
        }

        private async void StoryboardIconCopyDone1_Completed(object sender, object e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => StoryboardIconCopyDone2.Begin());
        }

        private async void StoryboardIconCopyDone2_Completed(object sender, object e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => FontIconCopyDone.Opacity = 1.0);
        }

        private async void StoryboardIconLeftPaneManageCopyDone1_Completed(object sender, object e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => StoryboardIconLeftPaneManageCopyDone2.Begin());
        }

        private async void StoryboardIconLeftPaneManageCopyDone2_Completed(object sender, object e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => FontIconLeftPaneManageCopyDone.Opacity = 1.0);
        }

        private async void StoryboardIconRenameDone1_Completed(object sender, object e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => StoryboardIconRenameDone2.Begin());
        }

        private async void StoryboardIconRenameDone2_Completed(object sender, object e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => FontIconRenameDone.Opacity = 1.0);
        }

        private async void Image_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            try
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
                {
                    args.AllowedOperations = DataPackageOperation.Copy;
                    args.Data.RequestedOperation = DataPackageOperation.Copy;

                    List<StorageFile> list = new List<StorageFile>();
                    list.Add(scanResult.GetImageFile(FlipViewScan.SelectedIndex));
                    args.Data.SetStorageItems(list);

                    args.DragUI.SetContentFromBitmapImage(scanResult.GetThumbnail(FlipViewScan.SelectedIndex));
                });
            }
            catch (Exception) { }
        }

        private async void ButtonRotate_Click(object sender, RoutedEventArgs e)
        {
            if (scanResult == null || scanResult.GetTotalNumberOfScans() == 0) return;

            // lock UI
            await RunOnUIThreadAsync(CoreDispatcherPriority.High,
            () =>
            {
                LockToolbar();
                LockPaneManage(true);
            });

            int index = FlipViewScan.SelectedIndex;

            try
            {
                List<Tuple<int, BitmapRotation>> instructions = new List<Tuple<int, BitmapRotation>>();
                instructions.Add(new Tuple<int, BitmapRotation>(index, BitmapRotation.Clockwise90Degrees));
                await scanResult.RotateScansAsync(instructions);
            }
            catch (Exception)
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.High,
                () =>
                {
                    ErrorMessage.ShowErrorMessage(TeachingTipEmpty,
                        LocalizedString("ErrorMessageRotateHeader"), LocalizedString("ErrorMessageRotateBody"));
                    UnlockToolbar();
                    UnlockPaneManage(false);
                });
                return;
            }

            // generate image
            await scanResult.GetImageAsync(index);

            // restore UI
            await RunOnUIThreadAsync(CoreDispatcherPriority.High,
            () =>
            {
                UnlockToolbar();
                UnlockPaneManage(false);
            });
        }

        private async void TeachingTipRename_Closed(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosedEventArgs args)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
                    () => ButtonRename.IsChecked = false);
        }

        private async void ButtonLeftPaneManageRotate_Click(object sender, RoutedEventArgs e)
        {
            if (scanResult == null || scanResult.GetTotalNumberOfScans() == 0 || 
                (LeftPaneListViewManage.SelectedItems.Count == 0 && LeftPaneGridViewManage.SelectedItems.Count == 0)) return;

            await RunOnUIThreadAsync(CoreDispatcherPriority.High,
            () =>
            {
                LockToolbar();
                LockPaneManage(true);
            });

            IReadOnlyList<ItemIndexRange> ranges = PaneManageGetSelectedRanges();
            int totalSelections = 0;
            foreach (var range in ranges)
            {
                totalSelections += Convert.ToInt32(range.Length);
            }

            Task[] tasksPreview = new Task[totalSelections];

            // generate instructions for rotation
            List<Tuple<int, BitmapRotation>> instructions = new List<Tuple<int, BitmapRotation>>();
            foreach (var range in ranges)
            {
                for (int i = range.FirstIndex; i <= range.LastIndex; i++)
                {
                    instructions.Add(new Tuple<int, BitmapRotation>(i, BitmapRotation.Clockwise90Degrees));
                }
            }
            await scanResult.RotateScansAsync(instructions);

            int arrayIndex = 0;
            foreach (var range in ranges)
            {
                for (int i = range.FirstIndex; i <= range.LastIndex; i++)
                {
                    tasksPreview[arrayIndex] = scanResult.GetImageAsync(i);
                    arrayIndex++;
                }
            }
            await Task.WhenAll(tasksPreview);

            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                UnlockPaneManage(true);
                UnlockToolbar();
            });
        }

        private async void ButtonLeftPaneManageSelect_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                if (flowState != FlowState.select)
                {
                    LockToolbar();
                    TransitionToSelectMode();
                } 
                else
                {

                    TransitionFromSelectMode();
                    UnlockToolbar();
                }
            });
        }

        private async void ButtonLeftPaneManageCopy_Click(object sender, RoutedEventArgs e)
        {
            if (scanResult == null || scanResult.GetTotalNumberOfScans() == 0 || 
                (LeftPaneListViewManage.SelectedItems.Count == 0 && LeftPaneGridViewManage.SelectedItems.Count == 0)) return;

            try
            {
                List<int> indices = new List<int>();
                foreach (var range in PaneManageGetSelectedRanges())
                {
                    for (int i = range.FirstIndex; i <= range.LastIndex; i++)
                    {
                        indices.Add(i);
                    }
                }
            
                await scanResult.CopyImagesAsync(indices);
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => StoryboardIconLeftPaneManageCopyDone1.Begin());
            }
            catch (Exception)
            {
                ErrorMessage.ShowErrorMessage(TeachingTipEmpty,
                    LocalizedString("ErrorMessageCopyHeader"), LocalizedString("ErrorMessageCopyBody"));
            }
        }

        private void ButtonLeftPaneManageShare_Click(object sender, RoutedEventArgs e)
        {
            if (scanResult == null || scanResult.GetTotalNumberOfScans() == 0 ||
                (LeftPaneListViewManage.SelectedItems.Count == 0 && LeftPaneGridViewManage.SelectedItems.Count == 0)) return;

            try
            {
                List<int> indices = new List<int>();
                foreach (var range in PaneManageGetSelectedRanges())
                {
                    for (int i = range.FirstIndex; i <= range.LastIndex; i++)
                    {
                        indices.Add(i);
                    }
                }

                shareIndexes = new int[indices.Count];

                for (int i = 0; i < indices.Count; i++)
                {
                    shareIndexes[i] = indices[i];
                }

                Share(ButtonLeftPaneManageShare);
            }
            catch (Exception) { }
        }

        private async void ComboBoxDebugFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                if (scanResult == null)
                {
                    CheckBoxDebugStartFresh.IsEnabled = true;
                }
                else
                {
                    if ((SupportedFormat) ComboBoxDebugFormat.SelectedItem != scanResult.GetFileFormat())
                    {
                        CheckBoxDebugStartFresh.IsEnabled = false;
                        CheckBoxDebugStartFresh.IsChecked = true;
                    } else
                    {
                        CheckBoxDebugStartFresh.IsEnabled = true;
                    }
                }
            });
        }

        private async void ButtonCrop_Click(object sender, RoutedEventArgs e)
        {
            if (scanResult == null || scanResult.GetTotalNumberOfScans() == 0) return;

            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                TransitionToEditingMode(SummonToolbar.Crop);
            });
        }

        private async void ButtonDiscard_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                TransitionFromEditingMode();
            });
        }

        private async void SetFixedAspectRatio(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High,
            () =>
            {
                // set aspect ratio according to tag
                ImageCropperScan.AspectRatio = 1.0 / double.Parse(((ToggleMenuFlyoutItem)sender).Tag.ToString(), new System.Globalization.CultureInfo("en-EN"));
                TextBlockCropAspectRatio.Text = ((ToggleMenuFlyoutItem)sender).Text;

                // only check selected item
                foreach (var item in MenuFlyoutAspectRatio.Items)
                {
                    try { ((ToggleMenuFlyoutItem)item).IsChecked = false; }
                    catch (InvalidCastException) { }
                }
                ((ToggleMenuFlyoutItem)sender).IsChecked = true;
            });
        }

        private async void SetCustomAspectRatio(object sender, RoutedEventArgs e)
        {
            // only check selected item
            await RunOnUIThreadAsync(CoreDispatcherPriority.High,
            () =>
            {
                // set aspect ratio to custom
                ImageCropperScan.AspectRatio = null;
                TextBlockCropAspectRatio.Text = ((ToggleMenuFlyoutItem)sender).Text;

                // only check selected item
                foreach (var item in MenuFlyoutAspectRatio.Items)
                {
                    try { ((ToggleMenuFlyoutItem)item).IsChecked = false; }
                    catch (InvalidCastException) { }
                }
                ((ToggleMenuFlyoutItem)sender).IsChecked = true;
            });
        }

        private async void FlipAspectRatio(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                ImageCropperScan.AspectRatio = ImageCropperScan.CroppedRegion.Height / ImageCropperScan.CroppedRegion.Width;
            });
        }

        private async void ButtonCropAspectRatio_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                FlyoutBase.ShowAttachedFlyout(ButtonCropAspectRatio);
            });
        }

        private async void MenuFlyoutAspectRatio_Closing(object sender, object e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                ButtonCropAspectRatio.IsChecked = false;
            });
        }

        private async void ButtonDraw_Click(object sender, RoutedEventArgs e)
        {
            if (scanResult == null || scanResult.GetTotalNumberOfScans() == 0) return;

            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                TransitionToEditingMode(SummonToolbar.Draw);
            });
        }

        private async Task SaveEdit(bool asCopy)
        {
            try
            {
                if (scanResult == null || scanResult.GetTotalNumberOfScans() == 0 ||
                    (flowState != FlowState.crop && flowState != FlowState.draw)) throw new ApplicationException("Could not save changes.");

                await RunOnUIThreadAsync(CoreDispatcherPriority.High,
                () =>
                {
                    ButtonCropAspectRatio.IsEnabled = false;
                    InkToolbarDraw.IsEnabled = false;
                    ButtonSave.IsEnabled = false;
                    ButtonSaveCopy.IsEnabled = false;
                    ButtonDiscard.IsEnabled = false;
                    ImageCropperScan.IsEnabled = false;
                });

                switch (flowState)
                {
                    case FlowState.initial:
                    case FlowState.scanning:
                    case FlowState.select:
                        throw new ApplicationException("Could not save changes.");
                    case FlowState.crop:
                        if (asCopy)
                        {
                            await scanResult.CropScanAsCopyAsync(FlipViewScan.SelectedIndex, ImageCropperScan);
                            await scanResult.GetImageAsync(FlipViewScan.SelectedIndex + 1);
                        }
                        else
                        {
                            await scanResult.CropScanAsync(FlipViewScan.SelectedIndex, ImageCropperScan);
                            await scanResult.GetImageAsync(FlipViewScan.SelectedIndex);
                        }
                        break;
                    case FlowState.draw:
                        if (asCopy)
                        {
                            await scanResult.DrawOnScanAsCopyAsync(FlipViewScan.SelectedIndex, InkCanvasEditDraw);
                            await scanResult.GetImageAsync(FlipViewScan.SelectedIndex + 1);
                        }
                        else
                        {
                            await scanResult.DrawOnScanAsync(FlipViewScan.SelectedIndex, InkCanvasEditDraw);
                            await scanResult.GetImageAsync(FlipViewScan.SelectedIndex);
                        }
                        break;
                }

                if (asCopy) await RunOnUIThreadAsync(CoreDispatcherPriority.High, () => StoryboardIconSaveCopyDone1.Begin());
            }
            catch (Exception)
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageSaveHeader"), LocalizedString("ErrorMessageSaveBody"));
                });
            } 
            finally
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.High,
                () =>
                {
                    if (!asCopy) TransitionFromEditingMode();
                    ButtonCropAspectRatio.IsEnabled = true;
                    InkToolbarDraw.IsEnabled = true;
                    ButtonSave.IsEnabled = true;
                    ButtonSaveCopy.IsEnabled = true;
                    ButtonDiscard.IsEnabled = true;
                    ImageCropperScan.IsEnabled = true;
                });
            }
        }

        private async void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            await SaveEdit(false);
        }

        private async void ButtonSaveCopy_Click(object sender, RoutedEventArgs e)
        {
            await SaveEdit(true);
        }

        private async void StoryboardIconSaveCopyDone1_Completed(object sender, object e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => StoryboardIconSaveCopyDone2.Begin());
        }

        private async void StoryboardIconSaveCopyDone2_Completed(object sender, object e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => FontIconSaveCopyDone.Opacity = 1.0);
        }

        private async void ButtonDrawTouch_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (InkCanvasEditDraw.InkPresenter.InputDeviceTypes.HasFlag(CoreInputDeviceTypes.Touch))
                {
                    InkCanvasEditDraw.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Pen
                                                                        | CoreInputDeviceTypes.Touch;
                    ButtonDrawTouch.IsChecked = true;
                    lastTouchDrawState = true;
                    localSettingsContainer.Values["lastTouchDrawState"] = lastTouchDrawState;
                }
                else
                {
                    InkCanvasEditDraw.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Pen;
                    ButtonDrawTouch.IsChecked = false;
                    lastTouchDrawState = false;
                    localSettingsContainer.Values["lastTouchDrawState"] = lastTouchDrawState;
                }
            });
        }

        private async void ImageEditDraw_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                ViewboxEditDraw.Width = ImageEditDraw.ActualWidth;
                ViewboxEditDraw.MaxWidth = ImageEditDraw.ActualWidth;
                ViewboxEditDraw.Height = ImageEditDraw.ActualHeight;
                ViewboxEditDraw.MaxHeight = ImageEditDraw.ActualHeight;
            });
        }

        private async void HyperlinkFeedbackHub_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            await LaunchFeedbackHub();
        }

        private async void HyperlinkRate_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            await ShowRatingDialog();
        }
    }
}
