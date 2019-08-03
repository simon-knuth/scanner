using System;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.UI.ViewManagement;

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

    // TODO add documentation
    public static void UpdateTheme(UISettings uISettings, object theObject, DropShadowPanel dropShadowPanel)
    {
        var titleBar = ApplicationView.GetForCurrentView().TitleBar;

        titleBar.ButtonBackgroundColor = Windows.UI.Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Windows.UI.Colors.Transparent;

        if ((new UISettings()).GetColorValue(Windows.UI.ViewManagement.UIColorType.Background).ToString() == "#FF000000")
        {
            // Dark mode is active
            titleBar.ButtonForegroundColor = Windows.UI.Colors.LightGray;
            if (dropShadowPanel != null) dropShadowPanel.ShadowOpacity = 0.6;
        }
        else
        {
            // Light mode is active
            titleBar.ButtonForegroundColor = Windows.UI.Colors.Black;
            if (dropShadowPanel != null) dropShadowPanel.ShadowOpacity = 0.3;
        }
    }
}