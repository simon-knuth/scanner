using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
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
        

        public MainPage()
        {
            this.InitializeComponent();

            // initialize veriables
            ColumnLeftDefaultMaxWidth = ColumnLeft.MaxWidth;

            // populate the scanner list
            refreshScannerList();

            CoreApplication.MainView.TitleBar.LayoutMetricsChanged += correct_titlebar;
        }

        public void refreshScannerList()
        {
            Debug.WriteLine("refreshing scanner list");
            ProgressBarRefresh.IsEnabled = true;
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
            });
        }
            

        private async void OnScannerEnumerationComplete(DeviceWatcher sender, Object theObject)
        {
            Debug.WriteLine("OnScannerEnumerationComplete()");
            
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
                {
                    ProgressBarRefresh.Visibility = Visibility.Collapsed;
                    TextBlockRefreshHint.Visibility = Visibility.Visible;
                    ComboBoxScanners.IsEnabled = true;
                    StackPanelTextRight.Opacity = 1.0;
                }
            );
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // responsive behavior
            if (((Page) sender).ActualWidth < 900)
            {
                StackPanelTextRight.Opacity = 0.0;
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
                if (selectedScanner != null)
                {
                    StackPanelTextRight.Opacity = 1.0;
                }
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder folder = await KnownFolders.PicturesLibrary.GetFolderAsync("Scans");
            await Launcher.LaunchFolderAsync(folder);
        }

        private async void no_scanner_selected()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {

                foreach (RadioButton radioButton in StackPanelColor.Children)
                {
                    radioButton.IsEnabled = false;
                }

                foreach (RadioButton radioButton in StackPanelSource.Children)
                {
                    radioButton.IsEnabled = false;
                }

                ComboBoxResolution.IsEnabled = false;
                ComboBoxType.IsEnabled = false;
                ButtonScan.IsEnabled = false;
                StackPanelTextRight.Opacity = 1.0;
            }
            );
        }

        private async void scanner_selected()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            async () =>
            {
                // unlock basic options
                foreach (RadioButton radioButton in StackPanelColor.Children)
                {
                    radioButton.IsEnabled = true;
                }

                ComboBoxScanners.IsEnabled = true;
                ComboBoxResolution.IsEnabled = true;
                ComboBoxType.IsEnabled = true;
                ButtonScan.IsEnabled = true;
                StackPanelTextRight.Opacity = 0.0;

                // unlock supported options
                foreach (DeviceInformation check in deviceInformations)
                {
                    if (check.Id == ((ComboBoxItem) ComboBoxScanners.SelectedItem).Tag.ToString())
                    {
                        selectedScanner = await ImageScanner.FromIdAsync(check.Id);
                        break;
                    }
                }

                if (selectedScanner.IsScanSourceSupported(ImageScannerScanSource.AutoConfigured))
                {
                    RadioButtonSourceAutomatic.IsEnabled = true;
                    RadioButtonSourceAutomatic.IsChecked = true;
                } else
                {
                    RadioButtonSourceAutomatic.IsChecked = false;
                }

                if (selectedScanner.IsScanSourceSupported(ImageScannerScanSource.Flatbed))
                {
                    RadioButtonSourceFlatbed.IsEnabled = true;
                    if (RadioButtonSourceAutomatic.IsChecked == false)
                    {
                        RadioButtonSourceFlatbed.IsChecked = true;
                    }
                } else
                {
                    RadioButtonSourceFlatbed.IsChecked = false;
                }

                if (selectedScanner.IsScanSourceSupported(ImageScannerScanSource.Feeder))
                {
                    RadioButtonSourceFeeder.IsEnabled = true;
                    if (RadioButtonSourceAutomatic.IsChecked == false && RadioButtonSourceFlatbed.IsChecked == false)
                    {
                        RadioButtonSourceFeeder.IsChecked = true;
                    }
                } else
                {
                    RadioButtonSourceFeeder.IsChecked = false;
                }
            });
        }

        private async void ComboBoxScanners_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxScanners.SelectedIndex == -1)
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
    }
}
