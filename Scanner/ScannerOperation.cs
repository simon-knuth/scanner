using Scanner;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.Devices.Scanners;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml.Controls;

using static Enums_old;
using static Globals;
using static Utilities;


class ScannerOperation
{
    /// <summary>
    ///     Converts the resolutions previously generated in <see cref="RecognizedScanner"/> to their corresponding
    ///     items in the given <see cref="ComboBox"/>. Their item's tags contain the <see cref="ImageScannerResolution"/>.
    ///     This also marks the items according to their <see cref="ResolutionProperty"/>.
    /// </summary>
    /// <param name="scanner">The scanner that supplies the available resolutions.</param>
    /// <param name="mode">The source mode that resolutions shall be generated for.</param>
    /// <param name="comboBox">The <see cref="ComboBox"/> that will contain the resolutions.</param>
    /// <param name="resolutions">The <see cref="ObservableCollection{ComboBoxItem}"/> that will contain the <see cref="ComboBoxItem"/>s.</param>
    public static void GenerateResolutionItems(RecognizedScanner scanner, SourceMode mode, ComboBox comboBox,
        ObservableCollection<ComboBoxItem> resolutions)
    {
        log.Information("Generating resolution items for ComboBox.");
        resolutions.Clear();

        List<ValueTuple<float, ResolutionProperty>> scannerResolutions = null;
        if (mode == SourceMode.Flatbed)
        {
            scannerResolutions = scanner.flatbedResolutions;
        }
        else if (mode == SourceMode.Feeder)
        {
            scannerResolutions = scanner.feederResolutions;
        }

        int currentIndex = 0, newIndex = -1;
        foreach (var resolution in scannerResolutions)
        {
            switch (resolution.Item2)
            {
                case ResolutionProperty.None:
                    resolutions.Add(CreateComboBoxItem(String.Format(LocalizedString("OptionScanOptionsResolution"), resolution.Item1),
                        new ImageScannerResolution { DpiX = resolution.Item1, DpiY = resolution.Item1 }));
                    break;
                case ResolutionProperty.Default:
                    resolutions.Add(CreateComboBoxItem(String.Format(LocalizedString("OptionScanOptionsResolutionDefault"), resolution.Item1),
                        new ImageScannerResolution { DpiX = resolution.Item1, DpiY = resolution.Item1 }));
                    newIndex = currentIndex;
                    break;
                case ResolutionProperty.Documents:
                    resolutions.Add(CreateComboBoxItem(String.Format(LocalizedString("OptionScanOptionsResolutionDocuments"), resolution.Item1),
                        new ImageScannerResolution { DpiX = resolution.Item1, DpiY = resolution.Item1 }));
                    newIndex = currentIndex;
                    break;
                case ResolutionProperty.Photos:
                    resolutions.Add(CreateComboBoxItem(String.Format(LocalizedString("OptionScanOptionsResolutionPhotos"), resolution.Item1),
                        new ImageScannerResolution { DpiX = resolution.Item1, DpiY = resolution.Item1 }));
                    break;
                default:
                    break;
            }
            currentIndex += 1;
        }

        comboBox.SelectedIndex = newIndex;
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
    public static Tuple<ImageScannerFormat, SupportedFormat?> GetDesiredFormat(ComboBox comboBoxFormat, ObservableCollection<ComboBoxItem> formats)
    {
        if (comboBoxFormat.SelectedIndex == -1)
        {
            return null;
        }

        ComboBoxItem selectedItem = ((ComboBoxItem)comboBoxFormat.SelectedItem);
        ImageScannerFormat baseFormat = ImageScannerFormat.DeviceIndependentBitmap;          // initialize with most supported type
        SupportedFormat? secondFormat = null;

        if (selectedItem.Tag.ToString().Split(",")[1] == "converted")
        {
            secondFormat = ConvertFormatStringToSupportedFormat(selectedItem.Tag.ToString().Split(",")[0]);
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
        }
        else
        {
            if (selectedItem.Tag.ToString().Split(",")[0] == "jpg") baseFormat = ImageScannerFormat.Jpeg;
            else if (selectedItem.Tag.ToString().Split(",")[0] == "png") baseFormat = ImageScannerFormat.Png;
            else if (selectedItem.Tag.ToString().Split(",")[0] == "pdf") baseFormat = ImageScannerFormat.Pdf;
            else if (selectedItem.Tag.ToString().Split(",")[0] == "xps") baseFormat = ImageScannerFormat.Xps;
            else if (selectedItem.Tag.ToString().Split(",")[0] == "openxps") baseFormat = ImageScannerFormat.OpenXps;
            else if (selectedItem.Tag.ToString().Split(",")[0] == "tif") baseFormat = ImageScannerFormat.Tiff;
        }

        log.Information("Got desired format: [baseFormat={Base}|secondFormat={Second}]", baseFormat, secondFormat);
        return new Tuple<ImageScannerFormat, SupportedFormat?>(baseFormat, secondFormat);
    }


    /// <summary>
    ///     Extracts the selected <see cref="ImageScannerResolution?"/> from the <paramref name="comboBoxResolution"/>.
    /// </summary>
    /// <param name="comboBoxResolution">The <see cref="ComboBox"/> that holds the resolutions.</param>
    /// <returns>null if no resolution has been selected.</returns>
    public static ImageScannerResolution? GetDesiredResolution(ComboBox comboBoxResolution)
    {
        if (comboBoxResolution.SelectedIndex == -1) return null;
        else return (ImageScannerResolution)((ComboBoxItem)comboBoxResolution.SelectedItem).Tag;
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
    /// <param name="comboBoxFormats">The <see cref="ComboBox"/> that contains the formats.</param>
    public static void GetSupportedFormats(IImageScannerFormatConfiguration config, ObservableCollection<ComboBoxItem> formats,
        ComboBox comboBoxFormats)
    {
        string currentlySelected = "";
        LinkedList<string> newNativeFormats = new LinkedList<string>();
        bool canConvert = false;

        // save currently selected format to hopefully reselect it after the update
        if ((ComboBoxItem)comboBoxFormats.SelectedItem != null) currentlySelected = ((ComboBoxItem)comboBoxFormats.SelectedItem).Tag.ToString().Split(",")[0];
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
        //if (config.IsFormatSupported(ImageScannerFormat.Pdf)) newNativeFormats.AddLast("pdf");            // always use own PDF conversion
        //if (config.IsFormatSupported(ImageScannerFormat.Xps)) newNativeFormats.AddLast("xps");            // no support for XPS
        //if (config.IsFormatSupported(ImageScannerFormat.OpenXps)) newNativeFormats.AddLast("openxps");    // no support for OXPS
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
        if (newNativeFormats.Contains("jpg")) formats.Add(CreateComboBoxItem(glyphFormatImage, "JPG", "jpg,native"));
        else if (canConvert) formats.Add(CreateComboBoxItem(glyphFormatImage, "JPG", "jpg,converted"));
        if (newNativeFormats.Contains("png")) formats.Add(CreateComboBoxItem(glyphFormatImage, "PNG", "png,native"));
        else if (canConvert) formats.Add(CreateComboBoxItem(glyphFormatImage, "PNG", "png,converted"));
        if (newNativeFormats.Contains("pdf")) formats.Add(CreateComboBoxItem(glyphFormatPdf, "PDF", "pdf,native"));
        else if (canConvert
            && ApiInformation.IsApiContractPresent("Windows.ApplicationModel.FullTrustAppContract", 1, 0))
            formats.Add(CreateComboBoxItem(glyphFormatPdf, "PDF", "PDF,converted"));
        if (newNativeFormats.Contains("xps")) formats.Add(CreateComboBoxItem("XPS", "xps,native"));
        if (newNativeFormats.Contains("openxps")) formats.Add(CreateComboBoxItem("OpenXPS", "oxps,native"));
        if (newNativeFormats.Contains("tif")) formats.Add(CreateComboBoxItem(glyphFormatImage, "TIF", "tif,native"));
        else if (canConvert) formats.Add(CreateComboBoxItem(glyphFormatImage, "TIF", "tif,converted"));
        if (newNativeFormats.Contains("bmp")) formats.Add(CreateComboBoxItem(glyphFormatImage, "BMP", "bmp,native"));
        else if (canConvert) formats.Add(CreateComboBoxItem(glyphFormatImage, "BMP", "bmp,converted"));

        log.Information("Got natively supported formats: {@Formats}. [canConvert={Convert}]", newNativeFormats, canConvert);

        // select last selected format again (if possible)
        for (int i = 0; i < formats.Count; i++)
        {
            if (formats[i].Tag.ToString().Split(",")[0] == currentlySelected) comboBoxFormats.SelectedIndex = i;
        }
        if (comboBoxFormats.SelectedIndex == -1 && comboBoxFormats.Items.Count > 0) comboBoxFormats.SelectedIndex = 0;
    }


    /// <summary>
    ///     Checks whether the scan result is valid.
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public static bool ScanResultValid(ImageScannerScanResult result)
    {
        try
        {
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