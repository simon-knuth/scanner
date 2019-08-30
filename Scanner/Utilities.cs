using System;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;

using static Globals;
using System.IO;
using Windows.ApplicationModel;
using Windows.UI.Core;
using Windows.System;
using Windows.ApplicationModel.Resources;
using Windows.Storage.FileProperties;
using Microsoft.Graphics.Canvas;

static class Utilities
{
    /// <summary>
    ///     Accomodates the responsiveness of the UI.
    /// </summary>
    public enum UIstate
    {
        unset = -1,
        full = 0,                   // the whole UI is visible
        small_initial = 1,          // only the options pane is visible
        small_result = 2            // only the result of a scan is visible
    }


    public enum FlowState
    {
        initial = 0,                // there is no result visible
        result = 1,                 // there is a result visible but no crop in progress
        crop = 2,                   // there is a result visible and a crop in progress
        draw = 3                    // there is a result visible and drawing in progress
    }


    public enum SecondaryMenuConfig
    {
        hidden = 0,                 // the secondary CommandBar is hidden
        done = 1,                   // the secondary CommandBar shows the "done" button
        crop = 2,                   // the secondary CommandBar shows the crop commands
        draw = 3                    // the secondary CommandBar shows the draw commands
    }


    /// <summary>
    ///     Creates a ComboBoxItem with the specified content string and tag string.
    /// </summary>
    /// <param name="content">
    ///     The ComboBoxItem's content.
    /// </param>
    /// <param name="tag">
    ///     The ComboBoxItem's tag.
    /// </param>
    /// <returns>
    ///     The ComboBoxItem.
    /// </returns>
    public static ComboBoxItem CreateComboBoxItem(string content, string tag)
    {
        ComboBoxItem item = new ComboBoxItem();

        item.Content = content;
        item.Tag = tag;

        return item;
    }


    /// <summary>
    ///     Display an image file inside an Image object.
    /// </summary>
    /// <param name="file">
    ///     The image file.
    /// </param>
    /// <param name="image">
    ///     The Image object.
    /// </param>
    public static async void DisplayImageAsync(StorageFile file, Image image)
    {
        IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
        BitmapImage bmp = new BitmapImage();
        await bmp.SetSourceAsync(stream);
        image.Source = bmp;
        image.Visibility = Visibility.Visible;
    }


    public static Guid GetBitmapEncoderId(string formatString)
    {
        switch (formatString)
        {
            case "jpg":
                return BitmapEncoder.JpegEncoderId;
            case "jpeg":
                return BitmapEncoder.JpegEncoderId;
            case "png":
                return BitmapEncoder.PngEncoderId;
            case "bmp":
                return BitmapEncoder.BmpEncoderId;
            case "tif":
                return BitmapEncoder.TiffEncoderId;
            default:
                throw new Exception();          // TODO throw meaningful exception
        }
    }


    /// <summary>
    ///     Gets the BitmapFileFormat corresponding to a file's format.
    /// </summary>
    /// <param name="file">
    ///     The file of which to determine the BitmapFileFormat.
    /// </param>
    /// <returns>
    ///     The corresponding BitmapFileFormat.
    /// </returns>
    public static BitmapFileFormat GetBitmapFileFormat(StorageFile file)
    {
        string formatString = file.Name.Split(".")[1];

        switch (formatString)
        {
            case "jpg":
                return Microsoft.Toolkit.Uwp.UI.Controls.BitmapFileFormat.Jpeg;
            case "jpeg":
                return Microsoft.Toolkit.Uwp.UI.Controls.BitmapFileFormat.Jpeg;
            case "png":
                return Microsoft.Toolkit.Uwp.UI.Controls.BitmapFileFormat.Png;
            case "bmp":
                return Microsoft.Toolkit.Uwp.UI.Controls.BitmapFileFormat.Bmp;
            case "tif":
                return Microsoft.Toolkit.Uwp.UI.Controls.BitmapFileFormat.Tiff;
        }

        throw new Exception();      // TODO add meaningful exception
    }


    /// <summary>
    ///     Gets the CanvasBitmapFileFormat corresponding to a file's format.
    /// </summary>
    /// <param name="file">
    ///     The file of which to determine the CanvasBitmapFileFormat.
    /// </param>
    /// <returns>
    ///     The corresponding CanvasBitmapFileFormat.
    /// </returns>
    public static CanvasBitmapFileFormat GetCanvasBitmapFileFormat(StorageFile file)
    {
        string formatString = file.Name.Split(".")[1];

        switch (formatString)
        {
            case "jpg":
                return CanvasBitmapFileFormat.Jpeg;
            case "jpeg":
                return CanvasBitmapFileFormat.Jpeg;
            case "png":
                return CanvasBitmapFileFormat.Png;
            case "bmp":
                return CanvasBitmapFileFormat.Bmp;
            case "tif":
                return CanvasBitmapFileFormat.Tiff;
        }

        throw new Exception();      // TODO add meaningful exception
    }



    public static bool IsCtrlKeyPressed()
    {
        var ctrlState = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Control);
        return (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }


    public static void InitializeInkCanvas(InkCanvas canvas, ImageProperties properties)
    {
        canvas.Width = properties.Width;
        canvas.Height = properties.Height;
        canvas.InkPresenter.StrokeContainer.Clear();
    }


    public async static void LoadSettings()
    {
        localSettingsContainer = ApplicationData.Current.LocalSettings;
        
        if (futureAccessList.Entries.Count != 0)
        {
            try { scanFolder = await futureAccessList.GetFolderAsync("scanFolder"); }
            catch (Exception)
            {
                try { scanFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync("Scans", CreationCollisionOption.OpenIfExists); }
                catch (Exception)
                {
                    // tell the user that something went wrong and they should try setting a folder manually
                    ShowMessageDialog("Something went wrong", "Unable to access the last selected scan folder or the default scan folder. Please try to select a new folder in the settings menu.");
                }
                futureAccessList.AddOrReplace("scanFolder", scanFolder);
            }
        } else
        {
            ShowMessageDialog("Something went wrong", "Unable to access the default scan folder. Please try to select a new folder in the settings menu.");
        }

        if (localSettingsContainer.Values["settingAppTheme"] != null)
        {
            switch ((int)localSettingsContainer.Values["settingAppTheme"])
            {
                case 0:
                    settingAppTheme = Theme.system;
                    break;
                case 1:
                    settingAppTheme = Theme.light;
                    break;
                case 2:
                    settingAppTheme = Theme.dark;
                    break;
                default:
                    settingAppTheme = Theme.system;
                    break;
            }
        }
        else
        {
            settingAppTheme = Theme.system;
            localSettingsContainer.Values["settingAppTheme"] = (int)settingAppTheme;
        }

        if (localSettingsContainer.Values["settingSearchIndicator"] != null)
        {
            settingSearchIndicator = (bool)localSettingsContainer.Values["settingSearchIndicator"];
        }
        else
        {
            settingSearchIndicator = true;
            localSettingsContainer.Values["settingSearchIndicator"] = settingSearchIndicator;
        }

        if (localSettingsContainer.Values["settingAutomaticScannerSelection"] != null)
        {
            settingAutomaticScannerSelection = (bool)localSettingsContainer.Values["settingAutomaticScannerSelection"];
        }
        else
        {
            settingAutomaticScannerSelection = true;
            localSettingsContainer.Values["settingAutomaticScannerSelection"] = settingAutomaticScannerSelection;
        }

        if (localSettingsContainer.Values["settingNotificationScanComplete"] != null)
        {
            settingNotificationScanComplete = (bool)localSettingsContainer.Values["settingNotificationScanComplete"];
        }
        else
        {
            settingNotificationScanComplete = true;
            localSettingsContainer.Values["settingNotificationScanComplete"] = settingNotificationScanComplete;
        }

        if (localSettingsContainer.Values["settingUnsupportedFileFormat"] != null)
        {
            settingUnsupportedFileFormat = (bool)localSettingsContainer.Values["settingUnsupportedFileFormat"];
        }
        else
        {
            settingUnsupportedFileFormat = true;
            localSettingsContainer.Values["settingUnsupportedFileFormat"] = settingUnsupportedFileFormat;
        }

        PackageVersion version = Package.Current.Id.Version;
        string currentVersionNumber = String.Format("Version {0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
        if (localSettingsContainer.Values["lastKnownVersion"] != null)
        {
            string lastKnownVersionNumber = (string) localSettingsContainer.Values["lastKnownVersion"];

            if (currentVersionNumber != lastKnownVersionNumber) firstAppLaunchWithThisVersion = true;
            else firstAppLaunchWithThisVersion = false;

            localSettingsContainer.Values["lastKnownVersion"] = currentVersionNumber;
        }
        else
        {
            // first launch of the app ever
            localSettingsContainer.Values["lastKnownVersion"] = currentVersionNumber;
        }

        if (localSettingsContainer.Values["scanNumber"] != null)
        {
            scanNumber = (int) localSettingsContainer.Values["scanNumber"];
        }
        else
        {
            // first launch of the app ever
            localSettingsContainer.Values["scanNumber"] = 1;
            scanNumber = 1;
        }
    }


    public static string LocalizedString(string resource)
    {
        return ResourceLoader.GetForCurrentView().GetString(resource);
    }


    public static void LockCommandBar(CommandBar commandBar, Control except)
    {
        if (except == null)
        {
            foreach (Control item in commandBar.PrimaryCommands)
            {
                item.IsEnabled = false;
            }
            foreach (Control item in commandBar.PrimaryCommands)
            {
                item.IsEnabled = false;
            }
        } else
        {
            foreach (Control item in commandBar.PrimaryCommands)
            {
                if (item != except) item.IsEnabled = false;
            }
            foreach (Control item in commandBar.PrimaryCommands)
            {
                if (item != except) item.IsEnabled = false;
            }
        }
    }


    public static string RemoveNumbering(string input)
    {
        // expect string like "abc (def).xyz" and deliver "abc.xyz"
        string name = input.Substring(0, input.LastIndexOf("."));       // get name without file extension
        string extension = input.Substring(input.LastIndexOf("."));     // get file extension (with ".")

        if (name[name.Length - 1] == ')' && name.Contains(" ("))
        {
            name = name.Substring(0, name.LastIndexOf(" ("));
            return name + extension;
        } else
        {
            return input;
        }
    }


    public static void SendToastNotification(string title, string content, int expirationTime, string imageURI)
    {
        // Construct the visuals of the toast
        ToastVisual visual = new ToastVisual()
        {
            BindingGeneric = new ToastBindingGeneric()
            {
                Children =
                {
                    new AdaptiveText()
                    {
                        Text = title
                    },

                    new AdaptiveText()
                    {
                        Text = content
                    },

                    new AdaptiveImage()
                    {
                        Source = imageURI
                    }
                },
            }
        };

        // Construct final toast
        ToastContent toastContent = new ToastContent()
        {
            Visual = visual,
        };


        var toast = new ToastNotification(toastContent.GetXml());
        toast.ExpirationTime = DateTime.Now.AddMinutes(expirationTime);

        ToastNotificationManager.CreateToastNotifier().Show(toast);
    }

    public static void SendToastNotification(string title, string content, int expirationTime)
    {
        // Construct the visuals of the toast
        ToastVisual visual = new ToastVisual()
        {
            BindingGeneric = new ToastBindingGeneric()
            {
                Children =
                {
                    new AdaptiveText()
                    {
                        Text = title
                    },

                    new AdaptiveText()
                    {
                        Text = content
                    },
                },
            }
        };

        // Construct final toast
        ToastContent toastContent = new ToastContent()
        {
            Visual = visual,
        };


        var toast = new ToastNotification(toastContent.GetXml());
        toast.ExpirationTime = DateTime.Now.AddMinutes(expirationTime);

        ToastNotificationManager.CreateToastNotifier().Show(toast);
    }



    public async static void ShowMessageDialog(string title, string message)
    {
        MessageDialog messageDialog = new MessageDialog(message, title);
        await messageDialog.ShowAsync();
    }


    public static void UnlockCommandBar(CommandBar commandBar, Control except)
    {
        if (except == null)
        {
            foreach (Control item in commandBar.PrimaryCommands) item.IsEnabled = true;
            foreach (Control item in commandBar.PrimaryCommands) item.IsEnabled = true;
        }
        else
        {
            foreach (Control item in commandBar.PrimaryCommands) if (item != except) item.IsEnabled = true;
            foreach (Control item in commandBar.PrimaryCommands) if (item != except) item.IsEnabled = true;
        }
    }


    // TODO add documentation
    public static void UpdateTheme(UISettings uISettings, object theObject)
    {
        if (settingAppTheme == Theme.system)
        {
            if ((new UISettings()).GetColorValue(UIColorType.Background).ToString() == "#FF000000")
            {
                // Dark mode is active
                applicationViewTitlebar.ButtonForegroundColor = Windows.UI.Colors.LightGray;
            }
            else
            {
                // Light mode is active
                applicationViewTitlebar.ButtonForegroundColor = Windows.UI.Colors.Black;
            }
        } else
        {
            if (settingAppTheme == Theme.light) applicationViewTitlebar.ButtonForegroundColor = Windows.UI.Colors.Black;
            else applicationViewTitlebar.ButtonForegroundColor = Windows.UI.Colors.LightGray;
        }
    }


    public static void SaveSettings()
    {
        localSettingsContainer.Values["settingAppTheme"] = (int) settingAppTheme;
        localSettingsContainer.Values["settingSearchIndicator"] = settingSearchIndicator;
        localSettingsContainer.Values["settingAutomaticScannerSelection"] = settingAutomaticScannerSelection;
        localSettingsContainer.Values["settingNotificationScanComplete"] = settingNotificationScanComplete;
        localSettingsContainer.Values["settingUnsupportedFileFormat"] = settingUnsupportedFileFormat;
    }
}