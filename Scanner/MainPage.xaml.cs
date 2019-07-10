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

        private Windows.UI.Xaml.Controls.StackPanel[] radioButtonStackPanels;
        private DeviceWatcher scannerWatcher;
        private double ColumnLeftDefaultMaxWidth;
        private ObservableCollection<string> scannerList = new ObservableCollection<string>();

        public MainPage()
        {
            this.InitializeComponent();
            ColumnLeftDefaultMaxWidth = ColumnLeft.MaxWidth;
            this.radioButtonStackPanels = new Windows.UI.Xaml.Controls.StackPanel[] { StackPanelSource, StackPanelColor };
            refreshScannerList();
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
                    scannerList.Add(deviceInfo.Name);
                }
            );
        }

        private async void OnScannerRemoved(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            Debug.WriteLine("OnScannerRemoved() with " + deviceInfoUpdate.ToString());
        }

        private async void OnScannerEnumerationComplete(DeviceWatcher sender, Object theObject)
        {
            Debug.WriteLine("OnScannerEnumerationComplete()");

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
                {
                    ProgressBarRefresh.Visibility = Visibility.Collapsed;
                }
            );
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
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
                StackPanelTextRight.Opacity = 1.0;
            }
        }

        private void Hyperlink_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            refreshScannerList();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder folder = await KnownFolders.PicturesLibrary.GetFolderAsync("Scans");
            await Launcher.LaunchFolderAsync(folder);
        }
    }
}
