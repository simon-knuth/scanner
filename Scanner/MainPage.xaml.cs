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
        private ObservableCollection<ComboBoxItem> formats = new ObservableCollection<ComboBoxItem>();
        private ObservableCollection<ComboBoxItem> resolutions = new ObservableCollection<ComboBoxItem>();


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


        private ComboBoxItem CreateComboBoxItem(string content, string tag)
        {
            ComboBoxItem item = new ComboBoxItem();

            item.Content = content;
            item.Tag = tag;

            return item;
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


        /// <summary>
        ///     Makes sure that the right panel is aligned to the bottom of the title bar buttons.
        /// </summary>
        /// <param name="coreApplicationViewTitleBar"></param>
        /// <param name="theObject"></param>
        private void correct_titlebar(CoreApplicationViewTitleBar coreApplicationViewTitleBar, object theObject)
        {
            GridPanelRight.Margin = new Thickness(32, coreApplicationViewTitleBar.Height, 0, 0);
        }


        private async void ButtonScan_Click(object sender, RoutedEventArgs e)
        {
            // lock (almost) entire left panel
            UI_enabled(false, false, false, false, false, false, false, false, false, false, false, true);

            // gather options
            if (RadioButtonSourceAutomatic.IsChecked.Value)
            {
                // user asked for automatic configuration

                if (ComboBoxFormat.SelectedIndex == -1)
                {
                    // TODO no format selected
                }
                selectedScanner.AutoConfiguration.Format = GetDesiredFormat();

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
                selectedScanner.FlatbedConfiguration.Format = GetDesiredFormat();

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
                selectedScanner.FeederConfiguration.Format = GetDesiredFormat();

            } else
            {
                // TODO no configuration selected
            }

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

            // unlock UI
            RadioButtonSourceChanged(null, null);
            UI_enabled(true, true, true, true, true, true, true, true, true, true, true, true);
        }


        /// <summary>
        ///     Returns the ImageScannerFormat to the corresponding ComboBox entry selected by the user.
        /// </summary>
        /// <remarks>
        ///     Returns ImageScannerFormat.bitmap if no other option could be matched.
        /// </remarks>
        /// <returns>
        ///     The corresponding ImageScannerFormat.
        /// </returns>
        private ImageScannerFormat GetDesiredFormat()
        {
            ComboBoxItem selectedFormat = ((ComboBoxItem) ComboBoxFormat.SelectedItem);

            if (selectedFormat.Tag.ToString() == "jpeg") return ImageScannerFormat.Jpeg;
            if (selectedFormat.Tag.ToString() == "png") return ImageScannerFormat.Png;
            if (selectedFormat.Tag.ToString() == "pdf") return ImageScannerFormat.Pdf;
            if (selectedFormat.Tag.ToString() == "xps") return ImageScannerFormat.Xps;
            if (selectedFormat.Tag.ToString() == "openxps") return ImageScannerFormat.OpenXps;
            if (selectedFormat.Tag.ToString() == "tiff") return ImageScannerFormat.Tiff;
            return ImageScannerFormat.DeviceIndependentBitmap;
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
        ///     Updates resolutions according to given configuration (flatbed or feeder) and protects ComboBoxResolutions while running.
        /// </summary>
        /// <param name="config">
        ///     The configuration that resolutions shall be generated for.
        /// </param>
        private void GenerateResolutions(IImageScannerSourceConfiguration config)
        {
            ComboBoxResolution.IsEnabled = false;

            float minX = config.MinResolution.DpiX;
            float minY = config.MinResolution.DpiY;
            float maxX = config.MaxResolution.DpiX;
            float maxY = config.MaxResolution.DpiY;
            float actualX = config.ActualResolution.DpiX;
            float actualY = config.ActualResolution.DpiY;

            Debug.WriteLine("minX: " + minX + " | minY: " + minY);

            resolutions.Clear();
            int lowerBound = -1;
            for (int i = 0; actualX - (i * 100) >= minX && actualY * (actualX - (i * 100)) / actualX >= minY; i++)
            {
                lowerBound++;   
            }

            for (int i = lowerBound; i >= 0; i--)
            {
                float x = actualX - (i * 100);
                float y = actualY * x / actualX;
                if (i == 0)
                {
                    resolutions.Add(CreateComboBoxItem(x + " x " + y + " (Default)", x + "," + y));
                } else
                {
                    resolutions.Add(CreateComboBoxItem(x + " x " + y, x + "," + y));
                }
                
            }

            ComboBoxResolution.SelectedIndex = lowerBound;

            for (int i = 1; actualX + (i * 100) <= maxX && actualY * (actualX + (i * 100)) / actualX <= maxY; i++)
            {
                float x = actualX + (i * 100);
                float y = actualY * x / actualX;
                resolutions.Add(CreateComboBoxItem(x + " x " + y, x + "," + y));
            }

            ComboBoxResolution.IsEnabled = true;
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
                formats.Clear();
                if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Jpeg)) formats.Add(CreateComboBoxItem("JPG (Recommended)", "jpeg"));
                if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Png)) formats.Add(CreateComboBoxItem("PNG", "png"));
                if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Pdf)) formats.Add(CreateComboBoxItem("PDF", "pdf"));
                if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Xps)) formats.Add(CreateComboBoxItem("XPS", "xps"));
                if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.OpenXps)) formats.Add(CreateComboBoxItem("OpenXPS", "openxps"));
                if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Tiff)) formats.Add(CreateComboBoxItem("TIFF", "tiff"));
                if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.DeviceIndependentBitmap)) formats.Add(CreateComboBoxItem("Bitmap", "bitmap"));
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
                    formats.Clear();
                    if (selectedScanner.FlatbedConfiguration.IsFormatSupported(ImageScannerFormat.Jpeg)) formats.Add(CreateComboBoxItem("JPG (Recommended)", "jpeg"));
                    if (selectedScanner.FlatbedConfiguration.IsFormatSupported(ImageScannerFormat.Png)) formats.Add(CreateComboBoxItem("PNG", "png"));
                    if (selectedScanner.FlatbedConfiguration.IsFormatSupported(ImageScannerFormat.Pdf)) formats.Add(CreateComboBoxItem("PDF", "pdf"));
                    if (selectedScanner.FlatbedConfiguration.IsFormatSupported(ImageScannerFormat.Xps)) formats.Add(CreateComboBoxItem("XPS", "xps"));
                    if (selectedScanner.FlatbedConfiguration.IsFormatSupported(ImageScannerFormat.OpenXps)) formats.Add(CreateComboBoxItem("OpenXPS", "xps"));
                    if (selectedScanner.FlatbedConfiguration.IsFormatSupported(ImageScannerFormat.Tiff)) formats.Add(CreateComboBoxItem("TIFF", "tiff"));
                    if (selectedScanner.FlatbedConfiguration.IsFormatSupported(ImageScannerFormat.DeviceIndependentBitmap)) formats.Add(CreateComboBoxItem("Bitmap", "bitmap"));
                    ComboBoxFormat.SelectedIndex = 0;

                    // detect available resolutions and update UI accordingly
                    GenerateResolutions(selectedScanner.FlatbedConfiguration);
                }
                else if (sender == RadioButtonSourceFeeder)
                {
                    StackPanelColor.Visibility = Visibility.Visible;
                    StackPanelResolution.Visibility = Visibility.Visible;
                    RadioButtonColorModeColor.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Color);
                    RadioButtonColorModeGrayscale.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Grayscale);
                    RadioButtonColorModeMonochrome.IsEnabled = selectedScanner.FeederConfiguration.IsColorModeSupported(ImageScannerColorMode.Monochrome);

                    // detect available file formats and update UI accordingly
                    formats.Clear();
                    if (selectedScanner.FeederConfiguration.IsFormatSupported(ImageScannerFormat.Jpeg)) formats.Add(CreateComboBoxItem("JPG (Recommended)", "jpeg"));
                    if (selectedScanner.FeederConfiguration.IsFormatSupported(ImageScannerFormat.Png)) formats.Add(CreateComboBoxItem("PNG", "png"));
                    if (selectedScanner.FeederConfiguration.IsFormatSupported(ImageScannerFormat.Pdf)) formats.Add(CreateComboBoxItem("PDF", "pdf"));
                    if (selectedScanner.FeederConfiguration.IsFormatSupported(ImageScannerFormat.Xps)) formats.Add(CreateComboBoxItem("XPS", "xps"));
                    if (selectedScanner.FeederConfiguration.IsFormatSupported(ImageScannerFormat.OpenXps)) formats.Add(CreateComboBoxItem("OpenXPS", "xps"));
                    if (selectedScanner.FeederConfiguration.IsFormatSupported(ImageScannerFormat.Tiff)) formats.Add(CreateComboBoxItem("TIFF", "tiff"));
                    if (selectedScanner.FeederConfiguration.IsFormatSupported(ImageScannerFormat.DeviceIndependentBitmap)) formats.Add(CreateComboBoxItem("Bitmap", "bitmap"));
                    ComboBoxFormat.SelectedIndex = 0;

                    // detect available resolutions and update UI accordingly
                    GenerateResolutions(selectedScanner.FlatbedConfiguration);
                }

                // select first available color mode
                if (RadioButtonColorModeColor.IsEnabled) RadioButtonColorModeColor.IsChecked = true;
                else if (RadioButtonColorModeGrayscale.IsEnabled) RadioButtonColorModeGrayscale.IsChecked = true;
                else if (RadioButtonColorModeMonochrome.IsEnabled) RadioButtonColorModeMonochrome.IsChecked = true;
            }
        }

        private void ButtonSettings_Click(object sender, RoutedEventArgs e)
        {
            ScrollViewerScan.ChangeView(0, 0, 0);
        }
    }
}
