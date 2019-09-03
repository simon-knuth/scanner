using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Scanners;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

using static Globals;
using static Utilities;

class ScannerOperation
{
    /// <summary>
    ///     Updates resolutions according to given configuration (flatbed or feeder) and protects the ComboBox while running.
    /// </summary>
    /// <param name="config">
    ///     The configuration that resolutions shall be generated for.
    /// </param>
    /// <param name="comboBox">
    ///     The ComboBox that will contain the resolutions.
    /// </param>
    /// <param name="resolutions">
    ///     The ObservableCollection that will contain the ComboBoxItems.
    /// </param>
    public static void GenerateResolutions(IImageScannerSourceConfiguration config, ComboBox comboBox, 
        ObservableCollection<ComboBoxItem> resolutions)
    {
        comboBox.IsEnabled = false;

        float minX = config.MinResolution.DpiX;
        float minY = config.MinResolution.DpiY;
        float maxX = config.MaxResolution.DpiX;
        float maxY = config.MaxResolution.DpiY;
        float actualX = config.ActualResolution.DpiX;
        float actualY = config.ActualResolution.DpiY;

        resolutions.Clear();
        int lowerBound = -1;

        for (int i = 0; actualX - (i * 100) > minX && actualY * (actualX - (i * 100)) / actualX > minY; i++)
        {
            lowerBound++;
        }

        resolutions.Add(CreateComboBoxItem(minX.ToString() + " x " + minY.ToString(), minX.ToString() + "," + minY.ToString()));
        for (int i = lowerBound; i >= 0; i--)
        {
            float x = actualX - (i * 100);
            float y = actualY * x / actualX;
            if (i == 0)
            {
                resolutions.Add(CreateComboBoxItem(x + " x " + y + " (" + ResourceLoader.GetForCurrentView().GetString("DefaultResolutionIndicator") + ")", x + "," + y));
            }
            else
            {
                resolutions.Add(CreateComboBoxItem(x + " x " + y, x + "," + y));
            }

        }

        comboBox.SelectedIndex = lowerBound + 1;

        for (int i = 1; actualX + (i * 200) < maxX && actualY * (actualX + (i * 200)) / actualX < maxY; i++)
        {
            float x = actualX + (i * 200);
            float y = actualY * x / actualX;
            resolutions.Add(CreateComboBoxItem(x + " x " + y, x + "," + y));
        }
        resolutions.Add(CreateComboBoxItem(maxX.ToString() + " x " + maxY.ToString(), maxX.ToString() + "," + maxY.ToString()));

        comboBox.IsEnabled = true;
    }


    /// <summary>
    ///     Returns the tuple fitting to the corresponding ComboBox entry selected by the user.
    /// </summary>
    /// <remarks>
    ///     Returns ImageScannerFormat.bitmap as base format if no other match was found.
    /// </remarks>
    /// <returns>
    ///     The corresponding format tuple consisting of:
    ///         (1) ImageScannerFormat  baseFormat
    ///         (2) string              formatToConvertTo
    ///     If no conversion is necessary, the string will be null.
    ///     
    ///     Returns null if no format has been selected by the user.
    /// </returns>
    public static Tuple<ImageScannerFormat, string> GetDesiredFormat(ComboBox comboBoxFormat, ObservableCollection<ComboBoxItem> formats)
    {
        if (comboBoxFormat.SelectedIndex == -1)
        {
            return null;
        }

        ComboBoxItem selectedItem = ((ComboBoxItem) comboBoxFormat.SelectedItem);
        ImageScannerFormat baseFormat = ImageScannerFormat.DeviceIndependentBitmap;          // initialize with most supported type
        string secondFormat = null;

        if (selectedItem.Tag.ToString().Split(",")[1] == "converted")
        {
            secondFormat = selectedItem.Tag.ToString().Split(",")[0];
            foreach (ComboBoxItem item in formats)
            {
                if (item.Tag.ToString().Split(",")[1] == "native")
                {
                    // found a native format, is it an image format?
                    string tmp = item.Tag.ToString().Split(",")[0];
                    if (tmp == "jpg")
                    {
                        baseFormat = ImageScannerFormat.Jpeg; break;
                    }
                    else if (tmp == "png")
                    {
                        baseFormat = ImageScannerFormat.Png; break;
                    }
                    else if (tmp == "tif")
                    {
                        baseFormat = ImageScannerFormat.Tiff; break;
                    }
                    else if (tmp == "bmp")
                    {
                        baseFormat = ImageScannerFormat.DeviceIndependentBitmap; break;
                    }
                }
            }
        } else
        {
            if (selectedItem.Tag.ToString().Split(",")[0] == "jpg") baseFormat = ImageScannerFormat.Jpeg;
            else if (selectedItem.Tag.ToString().Split(",")[0] == "png") baseFormat = ImageScannerFormat.Png;
            else if (selectedItem.Tag.ToString().Split(",")[0] == "pdf") baseFormat = ImageScannerFormat.Pdf;
            else if (selectedItem.Tag.ToString().Split(",")[0] == "xps") baseFormat = ImageScannerFormat.Xps;
            else if (selectedItem.Tag.ToString().Split(",")[0] == "openxps") baseFormat = ImageScannerFormat.OpenXps;
            else if (selectedItem.Tag.ToString().Split(",")[0] == "tif") baseFormat = ImageScannerFormat.Tiff;
        }

        return new Tuple<ImageScannerFormat, string>(baseFormat, secondFormat);
    }


    /// <summary>
    ///     Gets the formats supported by the given scanner's configuration. If the corresponding option is enabled and a base image
    ///     format is supported, also add unsupported formats which can be reached through conversion. The formats are added in this order:
    ///         JPG -> PNG -> PDF -> XPS -> OpenXPS -> TIF -> BMP
    /// </summary>
    /// <param name="config">
    ///     The configuration that determines whether to check the auto, flatbed or feeder mode.
    /// </param>
    /// <param name="formats">
    ///     The ObservableCollection that will contain the ComboBoxItems for the formats.
    /// </param>
    /// <param name="selectedScanner">
    ///     The scanner that the configuration belongs to.
    /// </param>
    public static void GetSupportedFormats(IImageScannerFormatConfiguration config, ObservableCollection<ComboBoxItem> formats,
        ImageScanner selectedScanner, ComboBox comboBoxFormats)
    {
        comboBoxFormats.IsEnabled = false;

        string currentlySelected = "";
        LinkedList<string> newNativeFormats = new LinkedList<string>();
        bool canConvert = false;

        // save currently selected format to hoepfully reselect it after the update
        if ((ComboBoxItem) comboBoxFormats.SelectedItem != null) currentlySelected = ((ComboBoxItem) comboBoxFormats.SelectedItem).Tag.ToString().Split(",")[0];
        formats.Clear();

        // see which formats are supported and whether conversion is possible
        if (config.IsFormatSupported(ImageScannerFormat.Jpeg))
        {
            newNativeFormats.AddLast("jpg");
            canConvert = true;
        }
        if (config.IsFormatSupported(ImageScannerFormat.Png))
        {
            newNativeFormats.AddLast("png");
            canConvert = true;
        }
        if (config.IsFormatSupported(ImageScannerFormat.Pdf)) newNativeFormats.AddLast("pdf");
        if (config.IsFormatSupported(ImageScannerFormat.Xps)) newNativeFormats.AddLast("xps");
        if (config.IsFormatSupported(ImageScannerFormat.OpenXps)) newNativeFormats.AddLast("openxps");
        if (config.IsFormatSupported(ImageScannerFormat.Tiff))
        {
            newNativeFormats.AddLast("tif");
            canConvert = true;
        }
        if (config.IsFormatSupported(ImageScannerFormat.DeviceIndependentBitmap))
        {
            newNativeFormats.AddLast("bmp");
            canConvert = true;
        }

        // list available formats in correct order
        if (newNativeFormats.Contains("jpg"))                   formats.Add(CreateComboBoxItem("JPG", "jpg,native"));
        else if (canConvert && settingUnsupportedFileFormat)        formats.Add(CreateComboBoxItem("JPG", "jpg,converted"));
        if (newNativeFormats.Contains("png"))                   formats.Add(CreateComboBoxItem("PNG", "png,native"));
        else if (canConvert && settingUnsupportedFileFormat)        formats.Add(CreateComboBoxItem("PNG", "png,converted"));
        if (newNativeFormats.Contains("pdf"))                   formats.Add(CreateComboBoxItem("PDF", "PDF,native"));
        if (newNativeFormats.Contains("xps"))                   formats.Add(CreateComboBoxItem("XPS", "XPS,native"));
        if (newNativeFormats.Contains("openxps"))               formats.Add(CreateComboBoxItem("OpenXPS", "OPENXPS,native"));
        if (newNativeFormats.Contains("tif"))                   formats.Add(CreateComboBoxItem("TIF", "tif,native"));
        else if (canConvert && settingUnsupportedFileFormat)        formats.Add(CreateComboBoxItem("TIF", "tif,converted"));
        if (newNativeFormats.Contains("bmp"))                   formats.Add(CreateComboBoxItem("BMP", "bmp,native"));
        else if (canConvert && settingUnsupportedFileFormat)        formats.Add(CreateComboBoxItem("BMP", "bmp,converted"));

        // select last selected format again (if possible)
        for (int i = 0; i < formats.Count; i++)
        {
            if (((ComboBoxItem) formats[i]).Tag.ToString().Split(",")[0] == currentlySelected) comboBoxFormats.SelectedIndex = i;
        }

        comboBoxFormats.IsEnabled = true;
    }


    // TODO add documentation
    public async static Task<ImageScannerScanResult> ScanInCorrectMode(RadioButton radioButtonAuto, RadioButton radioButtonFlatbed, 
        RadioButton radioButtonFeeder, StorageFolder folder, CancellationTokenSource cancellationToken, Progress<UInt32> progress,
        ImageScanner scanner)
    {
        if (radioButtonAuto.IsChecked.Value)
        {
            return await scanner.ScanFilesToFolderAsync(
            ImageScannerScanSource.AutoConfigured, folder).AsTask(cancellationToken.Token, progress);
        }
        else if (radioButtonFlatbed.IsChecked.Value)
        {
            return await scanner.ScanFilesToFolderAsync(
            ImageScannerScanSource.Flatbed, folder).AsTask(cancellationToken.Token, progress);
        }
        else if (radioButtonFeeder.IsChecked.Value)
        {
            return await scanner.ScanFilesToFolderAsync(
            ImageScannerScanSource.Feeder, folder).AsTask(cancellationToken.Token, progress);
        }
        else throw (new Exception());
    }


    public static bool ScanResultValid(ImageScannerScanResult result)
    {
        try { return (result != null && result.ScannedFiles != null && result.ScannedFiles[0] != null && result.ScannedFiles.Count != 0); }
        catch (Exception) { return false; }
    }
}