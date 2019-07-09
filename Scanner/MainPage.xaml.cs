using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Scanner
{
    public sealed partial class MainPage : Page
    {

        private Windows.UI.Xaml.Controls.StackPanel[] radioButtonStackPanels;
        private DeviceWatcher scannerWatcher;

        public MainPage()
        {
            this.InitializeComponent();
            this.radioButtonStackPanels = new Windows.UI.Xaml.Controls.StackPanel[] { StackPanelSource, StackPanelColor };
            refreshScannerList();
        }

        public void refreshScannerList()
        {
            // Create a Device Watcher class for type Image Scanner for enumerating scanners
            scannerWatcher = DeviceInformation.CreateWatcher(DeviceClass.ImageScanner);

            scannerWatcher.Added += OnScannerAdded;
            scannerWatcher.Removed += OnScannerRemoved;
            scannerWatcher.EnumerationCompleted += OnScannerEnumerationComplete;
        }

        private async void OnScannerAdded(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            await
            this.Dispatcher.RunAsync(
                  Windows.UI.Core.CoreDispatcherPriority.Normal,
                  async () => 
                  {
                      ContentDialog dialog = new ContentDialog
                      {
                          Title = "New device added",
                          Content = "Id=" + deviceInfo.Id + " | Name=" + deviceInfo.Name + " | Kind=" + deviceInfo.Kind,
                          CloseButtonText = "Ok"
                      };


                      ContentDialogResult result = await dialog.ShowAsync();

                    // search the device list for a device with a matching device id
                    /*ScannerDataItem match = FindInList(deviceInfo.Id);

                    // If we found a match then mark it as verified and return
                    if (match != null)
                        {
                            match.Matched = true;
                            return;
                        }

                    // Add the new element to the end of the list of devices
                    AppendToList(deviceInfo);*/
                  }
            );
        }

        private async void OnScannerRemoved(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {

        }

        private async void OnScannerEnumerationComplete(DeviceWatcher sender, Object theObject)
        {

        }
    }
}
