using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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
    public static ImageScannerFormat GetDesiredImageScannerFormat(ComboBox comboBox)
    {
        ComboBoxItem selectedFormat = ((ComboBoxItem) comboBox.SelectedItem);

        if (selectedFormat.Tag.ToString() == "jpeg") return ImageScannerFormat.Jpeg;
        if (selectedFormat.Tag.ToString() == "png") return ImageScannerFormat.Png;
        if (selectedFormat.Tag.ToString() == "pdf") return ImageScannerFormat.Pdf;
        if (selectedFormat.Tag.ToString() == "xps") return ImageScannerFormat.Xps;
        if (selectedFormat.Tag.ToString() == "openxps") return ImageScannerFormat.OpenXps;
        if (selectedFormat.Tag.ToString() == "tiff") return ImageScannerFormat.Tiff;
        return ImageScannerFormat.DeviceIndependentBitmap;
    }


    /// <summary>
    ///     Gets the formats supported by the given scanner's configuration.
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
        ImageScanner selectedScanner)
    {
        formats.Clear();
        if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Jpeg)) formats.Add(CreateComboBoxItem("JPG (Recommended)", "jpeg"));
        if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Png)) formats.Add(CreateComboBoxItem("PNG", "png"));
        if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Pdf)) formats.Add(CreateComboBoxItem("PDF", "pdf"));
        if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Xps)) formats.Add(CreateComboBoxItem("XPS", "xps"));
        if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.OpenXps)) formats.Add(CreateComboBoxItem("OpenXPS", "openxps"));
        if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Tiff)) formats.Add(CreateComboBoxItem("TIFF", "tiff"));
        if (selectedScanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.DeviceIndependentBitmap)) formats.Add(CreateComboBoxItem("Bitmap", "bitmap"));
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