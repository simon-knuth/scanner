using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
            }
            else
            {
                resolutions.Add(CreateComboBoxItem(x + " x " + y, x + "," + y));
            }

        }

        comboBox.SelectedIndex = lowerBound;

        for (int i = 1; actualX + (i * 100) <= maxX && actualY * (actualX + (i * 100)) / actualX <= maxY; i++)
        {
            float x = actualX + (i * 100);
            float y = actualY * x / actualX;
            resolutions.Add(CreateComboBoxItem(x + " x " + y, x + "," + y));
        }

        comboBox.IsEnabled = true;
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
    public static Tuple<ImageScannerFormat, string> GetDesiredImageScannerFormat(ComboBox comboBox, ObservableCollection<ComboBoxItem> formats)
    {
        ComboBoxItem selectedItem = ((ComboBoxItem) comboBox.SelectedItem);
        ImageScannerFormat desiredFormat = ImageScannerFormat.DeviceIndependentBitmap;          // initialize with most supported type
        string secondFormat = "";

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
                        desiredFormat = ImageScannerFormat.Jpeg;
                        break;
                    }
                    else if (tmp == "png")
                    {
                        desiredFormat = ImageScannerFormat.Png;
                        break;
                    }
                    else if (tmp == "tif")
                    {
                        desiredFormat = ImageScannerFormat.Tiff;
                        break;
                    }
                    else if (tmp == "bmp")
                    {
                        desiredFormat = ImageScannerFormat.DeviceIndependentBitmap;
                        break;
                    }
                }
            }
        } else
        {
            if (selectedItem.Tag.ToString().Split(",")[0] == "jpg") desiredFormat = ImageScannerFormat.Jpeg;
            else if (selectedItem.Tag.ToString().Split(",")[0] == "png") desiredFormat = ImageScannerFormat.Png;
            else if (selectedItem.Tag.ToString().Split(",")[0] == "pdf") desiredFormat = ImageScannerFormat.Pdf;
            else if (selectedItem.Tag.ToString().Split(",")[0] == "xps") desiredFormat = ImageScannerFormat.Xps;
            else if (selectedItem.Tag.ToString().Split(",")[0] == "openxps") desiredFormat = ImageScannerFormat.OpenXps;
            else if (selectedItem.Tag.ToString().Split(",")[0] == "tif") desiredFormat = ImageScannerFormat.Tiff;
        }

        return new Tuple<ImageScannerFormat, string>(desiredFormat, secondFormat);
    }


    /// <summary>
    ///     Gets the formats supported by the given scanner's configuration. If the corresponding option is enabled and a base image
    ///     format is supported, also add unsupported formats which can be reached through conversion.
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
        string currentlySelected = "";
        if ((ComboBoxItem) comboBoxFormats.SelectedItem != null) currentlySelected = ((ComboBoxItem) comboBoxFormats.SelectedItem).Tag.ToString().Split(",")[0];
        formats.Clear();
        LinkedList<string> determinedImageFormats = new LinkedList<string>();

        if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Jpeg))
        {
            formats.Add(CreateComboBoxItem("JPG (Recommended)", "jpg,native"));
            determinedImageFormats.AddLast("jpg");
        }
        if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Png)) {
            formats.Add(CreateComboBoxItem("PNG", "png,native"));
            determinedImageFormats.AddLast("png");
        }
        if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Pdf)) formats.Add(CreateComboBoxItem("PDF", "PDF,native"));
        if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Xps)) formats.Add(CreateComboBoxItem("XPS", "XPS,native"));
        if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.OpenXps)) formats.Add(CreateComboBoxItem("OpenXPS", "OPENXPS,native"));
        if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Tiff)) {
            formats.Add(CreateComboBoxItem("TIF", "tif,native"));
            determinedImageFormats.AddLast("tif");
        }
        if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.DeviceIndependentBitmap)) {
            formats.Add(CreateComboBoxItem("BMP", "bmp,native"));
            determinedImageFormats.AddLast("bmp");
        }

        if (determinedImageFormats.Count > 0 && settingUnsupportedFileFormat)
        {
            // can convert and user wants it too ~> add missing formats
            if (!determinedImageFormats.Contains("jpg")) formats.Add(CreateComboBoxItem("JPG (Recommended)", "jpg,converted"));
            if (!determinedImageFormats.Contains("png")) formats.Add(CreateComboBoxItem("PNG", "png,converted"));
            if (!determinedImageFormats.Contains("tif")) formats.Add(CreateComboBoxItem("TIF", "tif,converted"));
            if (!determinedImageFormats.Contains("bmp")) formats.Add(CreateComboBoxItem("BMP", "bmp,converted"));
        }

        for (int i = 0; i < formats.Count; i++)
        {
            if (((ComboBoxItem) formats[i]).Tag.ToString().Split(",")[0] == currentlySelected) comboBoxFormats.SelectedIndex = i;
        } 
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
        else
        {
            throw new Exception();          // TODO throw meaningful exception
        }
    }
}