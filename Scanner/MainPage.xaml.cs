using Microsoft.Toolkit.Extensions;
using Microsoft.Toolkit.Uwp.UI.Extensions;
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
using Windows.Storage.AccessCache;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Popups;
using Windows.UI.Text;
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
        private double ColumnSidePaneMinWidth = 300;
        private double ColumnSidePaneMaxWidth = 350;
        private bool firstLoaded = true;
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
        }

        private async void OnScannerRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
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
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
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
                        scannerList.Insert(0, CreateComboBoxItem(newScanner.scannerName, newScanner));

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
            else Frame.Navigate(typeof(SettingsPage), null, new DrillInNavigationTransitionInfo());     // navigate to settings
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
                    selectedScanner = (RecognizedScanner)scannerList[ComboBoxScanners.SelectedIndex].Tag;
                    await CleanMenuForNewScanner((RecognizedScanner)scannerList[ComboBoxScanners.SelectedIndex].Tag);
                    RadioButtonSourceMode_Checked(null, null);
                    StackPanelContentPaneText.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async Task RefreshPreviewIndicators(bool auto, bool? flatbed, bool? feeder)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                if (auto) FontIconAutoPreviewSupported.Visibility = Visibility.Visible;
                else FontIconAutoPreviewSupported.Visibility = Visibility.Collapsed;

                if (flatbed == true) FontIconFlatbedPreviewSupported.Visibility = Visibility.Visible;
                else FontIconFlatbedPreviewSupported.Visibility = Visibility.Collapsed;

                if (feeder == true) FontIconFeederPreviewSupported.Visibility = Visibility.Visible;
                else FontIconFeederPreviewSupported.Visibility = Visibility.Collapsed;
            });
        }

        private async Task CleanMenuForNewScanner(RecognizedScanner scanner)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            async () =>
            {
                ComboBoxScanners.IsEnabled = false;
                
                // select source mode and enable/disable scan button
                if (scanner != null)
                {
                    bool modeSelected = false;

                    if (scanner.autoAllowed)        // auto config
                    {
                        RadioButtonSourceAutomatic.IsChecked = true;
                        modeSelected = true;
                        RadioButtonSourceAutomatic.IsEnabled = true;
                    } 
                    else
                    {
                        RadioButtonSourceAutomatic.IsEnabled = false;
                        RadioButtonSourceAutomatic.IsChecked = false;
                    }

                    if (scanner.flatbedAllowed)     // flatbed
                    {
                        if (!modeSelected)
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
                        if (!modeSelected)
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
                    await RefreshPreviewIndicators(scanner.autoPreviewAllowed, scanner.flatbedPreviewAllowed, scanner.feederPreviewAllowed);
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

                    await RefreshPreviewIndicators(false, false, false);
                }

                ComboBoxScanners.IsEnabled = true;
            });
        }

        private async void RadioButtonSourceMode_Checked(object sender, RoutedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
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
                    MenuFlyoutItemButtonScanPreview.IsEnabled = (bool)selectedScanner.autoPreviewAllowed;
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
            }
        }

        private async void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High,
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
                    LeftPaneListViewOrganize.ItemContainerTransitions.Remove(LeftPaneListViewOrganizeRepositionThemeTransition);
                    try { SplitViewContentPaneContent.Children.Add(LeftPaneOrganize); } catch (Exception) { }
                    LeftPaneListViewOrganize.ItemContainerTransitions.Add(LeftPaneListViewOrganizeRepositionThemeTransition);
                    GridLeftPaneFooterContent.Background = new SolidColorBrush(Colors.Transparent);
                    RectangleGridLeftPaneScanOptions.Fill = (Brush)Resources["SystemControlAcrylicElementBrush"];
                    RectangleGridLeftPaneFooter.Fill = (Brush)Resources["SystemControlAcrylicElementBrush"];
                    RectangleGridLeftPaneOrganize.Fill = (Brush)Resources["SystemControlAcrylicElementBrush"];
                    FrameLeftPaneScanHeader.Background = new SolidColorBrush(Colors.Transparent);
                    DropShadowPanelGridLeftPaneScanHeader.Margin = new Thickness(0);
                    DropShadowPanelGridLeftPaneFooter.Margin = new Thickness(0);
                    DropShadowPanelGridLeftPaneOrganize.Margin = new Thickness(0);
                    ButtonOrganize.IsChecked = false;
                    ButtonOrganize.Visibility = Visibility.Visible;
                    ButtonScanOptions.Visibility = Visibility.Visible;
                    StackPanelContentPaneTopToolbarText.HorizontalAlignment = HorizontalAlignment.Left;
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
                    LeftPaneListViewOrganize.ItemContainerTransitions.Remove(LeftPaneListViewOrganizeRepositionThemeTransition);
                    try { FlipViewLeftPane.Items.Add(LeftPaneOrganize); } catch (Exception) { }
                    LeftPaneListViewOrganize.ItemContainerTransitions.Add(LeftPaneListViewOrganizeRepositionThemeTransition);
                    GridLeftPaneFooterContent.Background = (Brush)Resources["SystemControlAcrylicWindowBrush"];
                    RectangleGridLeftPaneScanOptions.Fill = (Brush)Resources["ApplicationPageBackgroundThemeBrush"];
                    RectangleGridLeftPaneFooter.Fill = (Brush)Resources["ApplicationPageBackgroundThemeBrush"];
                    RectangleGridLeftPaneOrganize.Fill = (Brush)Resources["ApplicationPageBackgroundThemeBrush"];
                    FrameLeftPaneScanHeader.Background = (Brush)Resources["SystemControlAcrylicWindowBrush"];
                    DropShadowPanelGridLeftPaneScanHeader.Margin = new Thickness(16, 0, 16, 0);
                    DropShadowPanelGridLeftPaneFooter.Margin = new Thickness(16,0,16,0);
                    DropShadowPanelGridLeftPaneOrganize.Margin = new Thickness(16,0,16,0);
                    ButtonOrganize.Visibility = Visibility.Visible;
                    ButtonScanOptions.Visibility = Visibility.Collapsed;
                    if ((GridContentPaneTopToolbar.ActualWidth - StackPanelContentPaneTopToolbarText.ActualWidth) / 2 <= currentTitleBarButtonWidth)
                    {
                        StackPanelContentPaneTopToolbarText.HorizontalAlignment = HorizontalAlignment.Left;
                    }
                    else
                    {
                        StackPanelContentPaneTopToolbarText.HorizontalAlignment = HorizontalAlignment.Center;
                    }
                } else if (width >= 1750 && uiState != UIstate.wide)    // wide window
                {
                    uiState = UIstate.wide;
                    ColumnLeftPane.MinWidth = ColumnSidePaneMinWidth;
                    ColumnLeftPane.MaxWidth = ColumnSidePaneMaxWidth;
                    ColumnRightPane.MaxWidth = ColumnSidePaneMaxWidth;
                    ColumnRightPane.MinWidth = ColumnSidePaneMaxWidth;
                    SplitViewLeftPaneContent.Children.Clear();
                    SplitViewContentPaneContent.Children.Clear();
                    try { FlipViewLeftPane.Items.Remove(LeftPaneOrganize); } catch (Exception) { }
                    try { FlipViewLeftPane.Items.Insert(0, LeftPaneScanOptions); } catch (Exception) { }
                    LeftPaneListViewOrganize.ItemContainerTransitions.Remove(LeftPaneListViewOrganizeRepositionThemeTransition);
                    try { RightPane.Children.Add(LeftPaneOrganize); } catch (Exception) { }
                    LeftPaneListViewOrganize.ItemContainerTransitions.Add(LeftPaneListViewOrganizeRepositionThemeTransition);
                    GridLeftPaneFooterContent.Background = (Brush)Resources["SystemControlAcrylicWindowBrush"];
                    RectangleGridLeftPaneScanOptions.Fill = (Brush)Resources["ApplicationPageBackgroundThemeBrush"];
                    RectangleGridLeftPaneFooter.Fill = (Brush)Resources["ApplicationPageBackgroundThemeBrush"];
                    RectangleGridLeftPaneOrganize.Fill = (Brush)Resources["ApplicationPageBackgroundThemeBrush"];
                    FrameLeftPaneScanHeader.Background = (Brush)Resources["SystemControlAcrylicWindowBrush"];
                    DropShadowPanelGridLeftPaneScanHeader.Margin = new Thickness(16, 0, 16, 0);
                    DropShadowPanelGridLeftPaneFooter.Margin = new Thickness(16, 0, 16, 0);
                    DropShadowPanelGridLeftPaneOrganize.Margin = new Thickness(16, 0, 16, 0);
                    ButtonOrganize.Visibility = Visibility.Collapsed;
                    ButtonScanOptions.Visibility = Visibility.Collapsed;
                    if ((GridContentPaneTopToolbar.ActualWidth - StackPanelContentPaneTopToolbarText.ActualWidth) / 2 <= currentTitleBarButtonWidth)
                    {
                        StackPanelContentPaneTopToolbarText.HorizontalAlignment = HorizontalAlignment.Left;
                    }
                    else
                    {
                        StackPanelContentPaneTopToolbarText.HorizontalAlignment = HorizontalAlignment.Center;
                    }
                }
            });
        }

        private async void FlipViewLeftPane_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                if (FlipViewLeftPane.SelectedIndex == 0)
                {
                    ButtonOrganize.IsChecked = false;
                    ButtonScanOptions.IsChecked = true;
                }
                else if (FlipViewLeftPane.SelectedIndex == 1)
                {
                    ButtonOrganize.IsChecked = true;
                    ButtonScanOptions.IsChecked = false;
                } 
                else
                {
                    ButtonOrganize.IsChecked = false;
                    ButtonScanOptions.IsChecked = false;
                }
            });
        }

        private async void ButtonOrganize_Checked(object sender, RoutedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                if (uiState == UIstate.small) SplitViewContentPane.IsPaneOpen = true;
                else FlipViewLeftPane.SelectedIndex = 1;
            });
        }

        private async void ButtonScanOptions_Checked(object sender, RoutedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                if (uiState == UIstate.small) SplitViewLeftPane.IsPaneOpen = true;
                else FlipViewLeftPane.SelectedIndex = 0;
            });
        }

        private async void SplitViewLeftPane_PaneClosed(SplitView sender, object args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                if (uiState == UIstate.small) ButtonScanOptions.IsChecked = false;
            });
        }

        private async void SplitViewContentPane_PaneClosed(SplitView sender, object args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                if (uiState == UIstate.small) ButtonOrganize.IsChecked = false;
            });
        }

        private async void ButtonOrganize_Click(object sender, RoutedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
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
            });
        }


        private void MenuFlyoutItemButtonScanPreview_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButtonSourceAutomatic.IsChecked == true) Frame.Navigate(typeof(PreviewPage), new PreviewPageIntent(selectedScanner.scanner, ImageScannerScanSource.AutoConfigured, RadioButtonSourceAutomatic.Content.ToString()), new DrillInNavigationTransitionInfo());
            else if (RadioButtonSourceFlatbed.IsChecked == true) Frame.Navigate(typeof(PreviewPage), new PreviewPageIntent(selectedScanner.scanner, ImageScannerScanSource.Flatbed, RadioButtonSourceFlatbed.Content.ToString()), new DrillInNavigationTransitionInfo());
            else if (RadioButtonSourceFeeder.IsChecked == true) Frame.Navigate(typeof(PreviewPage), new PreviewPageIntent(selectedScanner.scanner, ImageScannerScanSource.Feeder, RadioButtonSourceFeeder.Content.ToString()), new DrillInNavigationTransitionInfo());
        }


        private async Task<bool> Scan(bool startFresh)
        {
            try
            {
                flowState = FlowState.scanning;
                bool scanSuccessful = false;
                if (startFresh) scanResult = null;

                // disable controls and show progress bar
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High,
                async () =>
                {
                    await TransitionLeftPaneButtonsForScan(true);
                    ButtonLeftPaneScan.IsEnabled = false;
                    FrameLeftPaneScanSource.IsEnabled = false;
                    FrameLeftPaneScanSourceMode.IsEnabled = false;
                    FrameLeftPaneScanColor.IsEnabled = false;
                    FrameLeftPaneResolution.IsEnabled = false;
                    FrameLeftPaneScanFeeder.IsEnabled = false;
                    FrameLeftPaneScanFormat.IsEnabled = false;
                    ScrollViewerLeftPaneOrganize.IsEnabled = false;
                    ContentPaneFrameProgressBarScan.Visibility = Visibility.Visible;
                });

                // get selected format
                Tuple<ImageScannerFormat, SupportedFormat?> selectedFormat = await PrepareScanConfig();
                if (selectedFormat == null) return false;

                // get selected color mode
                ImageScannerColorMode? selectedColorMode = GetDesiredColorMode();
                if (selectedColorMode == null)
                {
                    ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageScanErrorHeader"), LocalizedString("ErrorMessageScanErrorBody"));
                    await CancelScan();
                    return false;
                }

                // get selected resolution
                ImageScannerResolution? selectedResolution = GetDesiredResolution(ComboBoxResolution);
                if (selectedResolution == null)
                {
                    ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageScanErrorHeader"), LocalizedString("ErrorMessageScanErrorBody"));
                    await CancelScan();
                    return false;
                }

                // prepare scan config
                if (RadioButtonSourceAutomatic.IsChecked == true)
                {
                    selectedScanner.scanner.AutoConfiguration.Format = selectedFormat.Item1;
                }
                else if (RadioButtonSourceFlatbed.IsChecked == true)
                {
                    selectedScanner.scanner.FlatbedConfiguration.Format = selectedFormat.Item1;
                    selectedScanner.scanner.FlatbedConfiguration.ColorMode = (ImageScannerColorMode) selectedColorMode;
                    selectedScanner.scanner.FlatbedConfiguration.DesiredResolution = (ImageScannerResolution) selectedResolution;
                }
                else if (RadioButtonSourceFeeder.IsChecked == true)
                {
                    selectedScanner.scanner.FeederConfiguration.Format = selectedFormat.Item1;
                    selectedScanner.scanner.FeederConfiguration.ColorMode = (ImageScannerColorMode)selectedColorMode;
                    selectedScanner.scanner.FeederConfiguration.DesiredResolution = (ImageScannerResolution)selectedResolution;
                    
                    if (CheckBoxAllPages.IsChecked == true) selectedScanner.scanner.FeederConfiguration.MaxNumberOfPages = 0;
                    else selectedScanner.scanner.FeederConfiguration.MaxNumberOfPages = 1;

                    if (CheckBoxDuplex.IsChecked == true) selectedScanner.scanner.FeederConfiguration.Duplex = true;
                    else selectedScanner.scanner.FeederConfiguration.Duplex = false;
                }
                else
                {
                    ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageScanErrorHeader"), LocalizedString("ErrorMessageScanErrorBody"));
                    await CancelScan();
                    return false;
                }

                // get scan
                scanProgress = new Progress<uint>();
                scanCancellationToken = new CancellationTokenSource();
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => ButtonLeftPaneCancel.IsEnabled = true);
                ImageScannerScanResult scannerScanResult;
                try
                {
                    scannerScanResult = await ScanInCorrectMode(RadioButtonSourceAutomatic, RadioButtonSourceFlatbed, RadioButtonSourceFeeder, scanFolder,
                    scanCancellationToken, scanProgress, selectedScanner.scanner);
                }
                catch (Exception exc)
                {
                    ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageScanScannerErrorHeader"), LocalizedString("ErrorMessageScanScannerErrorBody") + "\n" + exc.HResult);
                    await CancelScan();
                    return false;
                }
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => ButtonLeftPaneCancel.IsEnabled = false);

                int priorNumberOfScans = scanResult != null ? scanResult.GetTotalNumberOfScans() : 0;

                if (ScanResultValid(scannerScanResult))
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => LeftPaneOrganizeInitialText.Visibility = Visibility.Collapsed);
                    if (scanResult == null)
                    {
                        scanResult = await ScanResult.Create(scannerScanResult.ScannedFiles);
                        FlipViewScan.ItemsSource = scanResult.elements;
                        LeftPaneListViewOrganize.ItemsSource = scanResult.elements;
                    }
                    else
                    {
                        await scanResult.AddFiles(scannerScanResult.ScannedFiles);
                        FlipViewScan.SelectedIndex = scanResult.GetTotalNumberOfScans() - 1;
                    }
                }

                flowState = FlowState.initial;
                scanSuccessful = true;

                // reenable controls and hide progress bar
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High,
                async () =>
                {
                    await TransitionLeftPaneButtonsForScan(false);
                    ButtonLeftPaneScan.IsEnabled = true;
                    FrameLeftPaneScanSource.IsEnabled = true;
                    FrameLeftPaneScanSourceMode.IsEnabled = true;
                    FrameLeftPaneScanColor.IsEnabled = true;
                    FrameLeftPaneResolution.IsEnabled = true;
                    FrameLeftPaneScanFeeder.IsEnabled = true;
                    FrameLeftPaneScanFormat.IsEnabled = true;
                    ScrollViewerLeftPaneOrganize.IsEnabled = true;
                    ContentPaneFrameProgressBarScan.Visibility = Visibility.Collapsed;
                });

                await RefreshScanButton();

                return scanSuccessful;
            }
            catch (Exception)
            {
                ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageScanErrorHeader"), LocalizedString("ErrorMessageScanErrorBody"));
                await CancelScan();
                return false;
            }            
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
                if (CheckBoxAllPages.IsChecked == true) selectedScanner.scanner.FeederConfiguration.MaxNumberOfPages = 0;
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

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                ContentPaneFrameProgressBarScan.Visibility = Visibility.Collapsed;
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
            await Scan(false);
            FlipViewScan.Visibility = Visibility.Visible;
        }

        private async void ButtonScanFresh_Click(object sender, RoutedEventArgs e)
        {
            await Scan(true);
            FlipViewScan.Visibility = Visibility.Visible;
        }

        private void StackPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ScrollViewerLeftPaneScan.ScrollableHeight >= 0) RectangleGridLeftPaneScanOptions.Visibility = Visibility.Visible;
            else RectangleGridLeftPaneScanOptions.Visibility = Visibility.Visible;
        }

        private async Task RefreshFileName()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                if (FlipViewScan.SelectedIndex < 0) return;

                StorageFile selectedFile = ((ScanResultElement) FlipViewScan.SelectedItem).ScanFile;
                TextBlockContentPaneTopToolbarFileName.Text = selectedFile.DisplayName;
                TextBlockContentPaneTopToolbarFileExtension.Text = selectedFile.FileType;
            });
        }

        private async void FlipViewScan_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await RefreshFileName();
            LeftPaneListViewOrganize.SelectedIndex = FlipViewScan.SelectedIndex;
        }

        private async void ButtonLeftPaneScanFolder_Click(object sender, RoutedEventArgs e)
        {
            try { await Launcher.LaunchFolderAsync(scanFolder); }
            catch (Exception) { }
        }

        private async void LockToolbar()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High,
            () =>
            {
                ButtonCrop.IsEnabled = false;
                ButtonRotate.IsEnabled = false;
                ButtonDraw.IsEnabled = false;
                ButtonRename.IsEnabled = false;
                ButtonDelete.IsEnabled = false;
                ButtonCopy.IsEnabled = false;
                ButtonOpenWith.IsEnabled = false;
                ButtonShare.IsEnabled = false;
            });
        }

        private async void UnlockToolbar()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High,
            () =>
            {
                ButtonCrop.IsEnabled = true;
                ButtonRotate.IsEnabled = true;
                ButtonDraw.IsEnabled = true;
                ButtonRename.IsEnabled = true;
                ButtonDelete.IsEnabled = true;
                ButtonCopy.IsEnabled = true;
                ButtonOpenWith.IsEnabled = true;
                ButtonShare.IsEnabled = true;
            });
        }

        private async Task TransitionLeftPaneButtonsForScan(bool beginScan)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High,
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

        private void LeftPaneListViewOrganize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LeftPaneListViewOrganize.SelectionMode == ListViewSelectionMode.Single) FlipViewScan.SelectedIndex = LeftPaneListViewOrganize.SelectedIndex;
        }

        private async void ButtonDebugAddScanner_Click(object sender, RoutedEventArgs e)
        {
            RecognizedScanner fakeScanner = new RecognizedScanner(TextBoxDebugFakeScannerName.Text, ToggleSwitchDebugFakeScannerAuto.IsOn, (bool)ToggleSwitchDebugFakeScannerAutoPreview.IsOn,
                ToggleSwitchDebugFakeScannerFlatbed.IsOn, ToggleSwitchDebugFakeScannerFlatbedPreview.IsOn, (bool)CheckBoxDebugFakeScannerFlatbedColor.IsChecked,
                (bool)CheckBoxDebugFakeScannerFlatbedGrayscale.IsChecked, (bool)CheckBoxDebugFakeScannerFlatbedMonochrome.IsChecked, ToggleSwitchDebugFakeScannerFeeder.IsOn,
                ToggleSwitchDebugFakeScannerFeederPreview.IsOn, (bool)CheckBoxDebugFakeScannerFeederColor.IsChecked, (bool)CheckBoxDebugFakeScannerFeederGrayscale.IsChecked,
                (bool)CheckBoxDebugFakeScannerFeederMonochrome.IsChecked, (bool)CheckBoxDebugFakeScannerFeederDuplex.IsChecked);
            
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High,
            () =>
            {
                scannerList.Insert(0, CreateComboBoxItem(fakeScanner.scannerName, fakeScanner));

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
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High,
            () =>
            {
                if (scanResult == null || scanResult.GetTotalNumberOfScans() == 0) 
                {
                    FontIconButtonScanAdd.Visibility = Visibility.Collapsed;
                    FontIconButtonScanStartFresh.Visibility = Visibility.Collapsed;
                    MenuFlyoutItemButtonScan.IsEnabled = true;
                    MenuFlyoutItemButtonScan.FontWeight = FontWeights.Bold;
                    MenuFlyoutItemButtonScan.Icon = null;
                    MenuFlyoutItemButtonScanFresh.IsEnabled = false;
                    MenuFlyoutItemButtonScanFresh.FontWeight = FontWeights.Normal;
                    return;
                }

                // get currently selected format
                var selectedFormatTuple = GetDesiredFormat(ComboBoxFormat, formats);
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

        }
    }
}
