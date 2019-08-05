using System;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.UI.ViewManagement;

using static Globals;
using Windows.ApplicationModel.Core;

static class Utilities
{
    /// <summary>
    ///     Accomodates the responsiveness of the UI.
    /// </summary>
    public enum UIstate
    {
        full = 0,                   // the whole UI is visible
        small_initial = 1,          // only the options pane is visible
        small_result = 2            // only the result of a scan is visible
    }


    public enum FlowState
    {
        initial = 0,                // there is no result visible
        result = 1,                 // there is a result visible but no crop in progress
        crop = 2                    // there is a result visible and a crop in progress
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


    public static void LoadSettings()
    {
        localSettingsContainer = ApplicationData.Current.LocalSettings;

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


    public static void UnlockCommandBar(CommandBar commandBar, Control except)
    {

        if (except == null)
        {
            foreach (Control item in commandBar.PrimaryCommands)
            {
                item.IsEnabled = true;
            }
            foreach (Control item in commandBar.PrimaryCommands)
            {
                item.IsEnabled = true;
            }
        }
        else
        {
            foreach (Control item in commandBar.PrimaryCommands)
            {
                if (item != except) item.IsEnabled = true;
            }
            foreach (Control item in commandBar.PrimaryCommands)
            {
                if (item != except) item.IsEnabled = true;
            }
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
                //if (dropShadowPanel != null) dropShadowPanel.ShadowOpacity = 0.6;
            }
            else
            {
                // Light mode is active
                applicationViewTitlebar.ButtonForegroundColor = Windows.UI.Colors.Black;
                //if (dropShadowPanel != null) dropShadowPanel.ShadowOpacity = 0.3;
            }
        } else
        {
            if (settingAppTheme == Theme.light)
            {
                applicationViewTitlebar.ButtonForegroundColor = Windows.UI.Colors.Black;
                //if (dropShadowPanel != null) dropShadowPanel.ShadowOpacity = 0.3;
            } else
            {
                applicationViewTitlebar.ButtonForegroundColor = Windows.UI.Colors.LightGray;
                //if (dropShadowPanel != null) dropShadowPanel.ShadowOpacity = 0.6;
            }
        }
    }


    public static void SaveSettings()
    {
        localSettingsContainer.Values["settingAppTheme"] = (int) settingAppTheme;
        localSettingsContainer.Values["settingSearchIndicator"] = settingSearchIndicator;
        localSettingsContainer.Values["settingNotificationScanComplete"] = settingNotificationScanComplete;
        localSettingsContainer.Values["settingUnsupportedFileFormat"] = settingUnsupportedFileFormat;
    }
}