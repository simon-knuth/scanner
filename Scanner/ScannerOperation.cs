using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

using static Globals;
using static Utilities;


class ScannerOperation
{
    /// <summary>
    ///     Updates resolutions according to given <paramref name="config"/> (flatbed or feeder).
    ///     Resolutions added are:
    ///         MinResolution -> ActualResolution -> MaxResolution
    ///     The ActualResolution is marked with the DefaultResolutionIndicator resource and automatically selected by the <paramref name="comboBox"/>.
    /// </summary>
    /// <param name="config">The configuration that resolutions shall be generated for.</param>
    /// <param name="comboBox">The <see cref="ComboBox"/> that will contain the resolutions.</param>
    /// <param name="resolutions">The <see cref="ObservableCollection{ComboBoxItem}"/> that will contain the <see cref="ComboBoxItem"/>s.</param>
    public static void GenerateResolutions(IImageScannerSourceConfiguration config, ComboBox comboBox, 
        ObservableCollection<ComboBoxItem> resolutions)
    {
        float minX = config.MinResolution.DpiX;
        float maxX = config.MaxResolution.DpiX;
        float actualX = config.ActualResolution.DpiX;

        resolutions.Clear();

        if (minX != actualX) resolutions.Add(CreateComboBoxItem(minX + " DPI", config.MinResolution));

        resolutions.Add(CreateComboBoxItem(actualX + " DPI" + " (" + LocalizedString("DefaultResolutionIndicator") + ")",
            config.ActualResolution));
        comboBox.SelectedIndex = resolutions.Count - 1;

        if (maxX != actualX) resolutions.Add(CreateComboBoxItem(maxX + " DPI", config.MaxResolution));
    }


    /// <summary>
    ///     Returns the tuple fitting to the corresponding <paramref name="comboBoxFormat"/> entry selected by the user.
    /// </summary>
    /// <returns>
    ///     The corresponding format tuple consisting of:
    ///         (1) <see cref="ImageScannerFormat"/>    baseFormat
    ///         (2) <see cref="string"/>                formatToConvertTo
    ///     If no conversion is necessary, the string will be null.
    ///     
    ///     Returns <see cref="ImageScannerFormat.bitmap"/> as base format if no other match was found.
    /// 
    ///     Returns null if no format has been selected by the user.
    /// </returns>
    /// <param name="comboBoxFormat">The <see cref="ComboBox"/> that contains the format list.</param>
    /// <param name="formats">The <see cref="ObservableCollection{ComboBoxItem}"/> that contains the selected <see cref="ComboBoxItem"/>.</param>
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
    ///     Extracts the selected <see cref="ImageScannerResolution?"/> from the <paramref name="comboBoxResolution"/>.
    /// </summary>
    /// <param name="comboBoxResolution">The <see cref="ComboBox"/> that holds the resolutions.</param>
    /// <returns>null if no resolution has been selected.</returns>
    public static ImageScannerResolution? GetDesiredResolution(ComboBox comboBoxResolution)
    {
        if (comboBoxResolution.SelectedIndex == -1) return null;
        else return (ImageScannerResolution) ((ComboBoxItem)comboBoxResolution.SelectedItem).Tag;
    }


    /// <summary>
    ///     Gets the formats supported by <paramref name="config"/>. If <see cref="settingUnsupportedFileFormat"/> is true and a base image
    ///     format is supported, also add unsupported formats which can be reached through conversion. The formats are added in this order:
    ///         JPG -> PNG -> PDF -> XPS -> OpenXPS -> TIF -> BMP
    ///     All available formats are added to <paramref name="formats"/>.
    ///     If possible, the previously selected format will be reselected. Otherwise the first one in the list is selected.
    /// </summary>
    /// <param name="config">The configuration that determines whether to check the auto, flatbed or feeder mode.</param>
    /// <param name="formats">The <see cref="ObservableCollection{ComboBoxItem}"/> that will contain the <see cref="ComboBoxItem"/>s for the formats.</param>
    /// <param name="selectedScanner">The <see cref="ImageScanner"/> that the <paramref name="config"/> belongs to.</param>
    /// <param name="comboBoxFormats">The <see cref="ComboBox"/> that contains the formats.</param>
    public static void GetSupportedFormats(IImageScannerFormatConfiguration config, ObservableCollection<ComboBoxItem> formats,
        ImageScanner selectedScanner, ComboBox comboBoxFormats)
    {
        string currentlySelected = "";
        LinkedList<string> newNativeFormats = new LinkedList<string>();
        bool canConvert = false;

        // save currently selected format to hopefully reselect it after the update
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
            if (((ComboBoxItem)formats[i]).Tag.ToString().Split(",")[0] == currentlySelected) comboBoxFormats.SelectedIndex = i;
        }
        if (comboBoxFormats.SelectedIndex == -1 && comboBoxFormats.Items.Count > 0) comboBoxFormats.SelectedIndex = 0;
    }


    /// <summary>
    ///     Scans in correct mode by looking at the three <see cref="RadioButton"/>s that represent each mode.
    ///     The file is saved to the <paramref name="folder"/>.
    /// </summary>
    /// <exception cref="Exception()">No <see cref="RadioButton"/> is checked.</exception>
    /// <param name="radioButtonAuto">The <see cref="RadioButton"/> representing the auto mode.</param>
    /// <param name="radioButtonFlatbed">The <see cref="RadioButton"/> representing the flatbed mode.</param>
    /// <param name="radioButtonFeeder">The <see cref="RadioButton"/> representing the feeder mode.</param>
    /// <param name="folder">The <see cref="StorageFolder"/> that the scan is saved to.</param>
    /// <param name="cancellationToken">The token that can be used to cancel the scan.</param>
    /// <param name="progress">The progress of the scan.</param>
    /// <param name="scanner">The <see cref="ImageScanner"/> that will perform the scan.</param>
    /// <returns></returns>
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


    /// <summary>
    ///     Checks whether the scan result is valid.
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public static bool ScanResultValid(ImageScannerScanResult result)
    {
        try {
            return (result != null 
                    && result.ScannedFiles != null 
                    && result.ScannedFiles[0] != null 
                    && result.ScannedFiles.Count != 0);
        }
        catch (Exception)
        {
            return false;
        }
    }
}