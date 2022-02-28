using Microsoft.Graphics.Canvas;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Scanner.Services;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Media.Imaging;
using static Enums;
using static Globals;

static class Utilities
{
    /// <summary>
    ///     Converts the <paramref name="formatString"/> into the corresponding <see cref="BitmapEncoder"/>ID.
    /// </summary>
    /// <param name="formatString">"jpg", "jpeg", "png", "bmp" or "tiff"/"tif" (with or without dot).</param>
    /// <returns>The corresponding BitmapEncoderId.</returns>
    public static Guid GetBitmapEncoderId(string formatString)
    {
        switch (formatString.ToLower())
        {
            case "jpg":
            case ".jpg":
            case "jpeg":
            case ".jpeg":
                return BitmapEncoder.JpegEncoderId;
            case "png":
            case ".png":
                return BitmapEncoder.PngEncoderId;
            case "bmp":
            case ".bmp":
                return BitmapEncoder.BmpEncoderId;
            case "tif":
            case ".tif":
            case "tiff":
            case ".tiff":
                return BitmapEncoder.TiffEncoderId;
            default:
                throw new ApplicationException("GetBitmapEncoderId received invalid format string '" + formatString.ToLower() + "'.");
        }
    }

    /// <summary>
    ///     Converts a <see cref="ImageScannerFormat"/> into the corresponding format string.
    /// </summary>
    /// <param name="format">An image format.</param>
    /// <returns>The corresponding string.</returns>
    public static string ConvertImageScannerFormatToString(ImageScannerFormat format)
    {
        switch (format)
        {
            case ImageScannerFormat.Jpeg:
                return ".jpg";
            case ImageScannerFormat.Png:
                return ".png";
            case ImageScannerFormat.DeviceIndependentBitmap:
                return ".bmp";
            case ImageScannerFormat.Tiff:
                return ".tif";
            case ImageScannerFormat.Xps:
                return ".xps";
            case ImageScannerFormat.OpenXps:
                return ".oxps";
            case ImageScannerFormat.Pdf:
                return ".pdf";
            default:
                throw new ApplicationException("ConvertImageScannerFormatToImageScannerFormat received invalid ImageScannerFormat '" + format + "' for conversion.");
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
        string formatString = file.FileType;

        switch (formatString)
        {
            case ".jpg":
            case ".jpeg":
                return BitmapFileFormat.Jpeg;
            case ".png":
                return BitmapFileFormat.Png;
            case ".bmp":
                return BitmapFileFormat.Bmp;
            case ".tif":
            case ".tiff":
                return BitmapFileFormat.Tiff;
            default:
                throw new ApplicationException("GetBitmapFileFormat received invalid file format '" + formatString + "' for conversion.");
        }
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
        string formatString = file.FileType;

        switch (formatString)
        {
            case ".jpg":
            case ".jpeg":
                return CanvasBitmapFileFormat.Jpeg;
            case ".png":
                return CanvasBitmapFileFormat.Png;
            case ".bmp":
                return CanvasBitmapFileFormat.Bmp;
            case ".tif":
            case ".tiff":
                return CanvasBitmapFileFormat.Tiff;
            default:
                throw new ApplicationException("GetCanvasBitmapFileFormat received invalid file format '" + formatString + "' for conversion.");
        }
    }

    /// <summary>
    ///     Searches the app resources for a localized version of a string.
    /// </summary>
    /// <param name="resource">The resource name.</param>
    /// <returns>The localized string.</returns>
    public static string LocalizedString(string resource)
    {
        if (CoreWindow.GetForCurrentThread() != null)
        {
            return ResourceLoader.GetForCurrentView().GetString(resource);
        }
        else
        {
            return ResourceLoader.GetForViewIndependentUse().GetString(resource);
        }
    }

    /// <summary>
    ///     Searches the app secrets for a localized version of a string.
    /// </summary>
    /// <param name="resource">The resource name.</param>
    /// <returns>The secret.</returns>
    public static string GetSecret(string secret)
    {
        var resources = new ResourceLoader("Secrets");
        return resources.GetString(secret);
    }

    /// <summary>
    ///     Removes brackets, their content and the leading whitespace from <paramref name="input"/>.
    /// </summary>
    /// <param name="input">A string like "abc (def).xyz", of which " (def)" shall be removed.</param>
    public static string RemoveNumbering(string input)
    {
        // expect string like "abc (def).xyz" and deliver "abc.xyz"
        string name = input.Substring(0, input.LastIndexOf("."));           // get name without file extension
        string extension = input.Substring(input.LastIndexOf("."));         // get file extension (with ".")

        if (name[name.Length - 1] == ')' && name.Contains(" ("))
        {
            name = name.Substring(0, name.LastIndexOf(" ("));
            return name + extension;
        }
        else
        {
            return input;
        }
    }

    /// <summary>
    ///     Adapts the titlebar buttons to the current theme.
    /// </summary>
    public static void UpdateTheme(UISettings uISettings, object theObject)
    {
        ISettingsService settingsService = Ioc.Default.GetService<ISettingsService>();
        SettingAppTheme theme = (SettingAppTheme)settingsService?.GetSetting(AppSetting.SettingAppTheme);

        if (theme == SettingAppTheme.System)
        {
            if ((new UISettings()).GetColorValue(UIColorType.Background).ToString() == "#FF000000")
            {
                // dark mode is active
                applicationViewTitlebar.ButtonForegroundColor = Windows.UI.Colors.White;
                applicationViewTitlebar.ButtonHoverForegroundColor = Windows.UI.Colors.White;
                applicationViewTitlebar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(32, 169, 169, 211);
                applicationViewTitlebar.ButtonPressedForegroundColor = Windows.UI.Colors.White;
                applicationViewTitlebar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(96, 169, 169, 211);
            }
            else
            {
                // light mode is active
                applicationViewTitlebar.ButtonForegroundColor = Windows.UI.Colors.Black;
                applicationViewTitlebar.ButtonHoverForegroundColor = Windows.UI.Colors.Black;
                applicationViewTitlebar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(64, 169, 169, 169);
                applicationViewTitlebar.ButtonPressedForegroundColor = Windows.UI.Colors.Black;
                applicationViewTitlebar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(156, 169, 169, 169);
            }
        }
        else
        {
            if (theme == SettingAppTheme.Light)
            {
                // light mode is forced
                applicationViewTitlebar.ButtonForegroundColor = Windows.UI.Colors.Black;
                applicationViewTitlebar.ButtonHoverForegroundColor = Windows.UI.Colors.Black;
                applicationViewTitlebar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(64, 169, 169, 169);
                applicationViewTitlebar.ButtonPressedForegroundColor = Windows.UI.Colors.Black;
                applicationViewTitlebar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(156, 169, 169, 169);
            }
            else
            {
                // dark mode is forced
                applicationViewTitlebar.ButtonForegroundColor = Windows.UI.Colors.White;
                applicationViewTitlebar.ButtonHoverForegroundColor = Windows.UI.Colors.White;
                applicationViewTitlebar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(32, 169, 169, 211);
                applicationViewTitlebar.ButtonPressedForegroundColor = Windows.UI.Colors.White;
                applicationViewTitlebar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(96, 169, 169, 211);
            }
        }
    }

    /// <summary>
    ///     Gets the image measurements from a <see cref="BitmapImage"/>.
    /// </summary>
    /// <returns>
    ///     A tuple of width and height.
    /// </returns>
    public static Tuple<double, double> GetImageMeasurements(BitmapImage image)
    {
        return new Tuple<double, double>(image.PixelWidth, image.PixelHeight);
    }

    /// <summary>
    ///     Converts a string to to a <see cref="ImageScannerFormat"/>.
    /// </summary>
    /// <param name="formatString">
    ///     The source format string, case-insensitive and with or without dot.
    ///     E.g. "png" / "PNG" / ".png" / ".PNG"
    /// </param>
    /// <returns>The corresponding <see cref="ImageScannerFormat"/>.</returns>
    public static ImageScannerFormat? ConvertFormatStringToImageScannerFormat(string formatString)
    {
        switch (formatString.ToLower())
        {
            case "jpg":
            case "jpeg":
            case ".jpg":
            case ".jpeg":
                return ImageScannerFormat.Jpeg;

            case "png":
            case ".png":
                return ImageScannerFormat.Png;

            case "tif":
            case "tiff":
            case ".tif":
            case ".tiff":
                return ImageScannerFormat.Tiff;

            case "bmp":
            case ".bmp":
                return ImageScannerFormat.DeviceIndependentBitmap;

            case "pdf":
            case ".pdf":
                return ImageScannerFormat.Pdf;

            case "xps":
            case ".xps":
                return ImageScannerFormat.Xps;

            case "oxps":
            case ".oxps":
                return ImageScannerFormat.OpenXps;

            default:
                return null;
        }
    }

    /// <summary>
    ///     Opens a teaching tip and takes care of usual pitfalls.
    /// </summary>
    /// <param name="teachingTip"></param>
    public static void ReliablyOpenTeachingTip(Microsoft.UI.Xaml.Controls.TeachingTip teachingTip)
    {
        if (teachingTip.IsOpen == false) teachingTip.IsOpen = true;
        else
        {
            teachingTip.IsOpen = false;
            teachingTip.IsOpen = true;
        }
    }

    /// <summary>
    ///     Wrapper for <see cref="CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync()"/>.
    /// </summary>
    public static IAsyncAction RunOnUIThreadAsync(CoreDispatcherPriority priority, DispatchedHandler agileCallback)
    {
        return Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(priority, agileCallback);
    }

    /// <summary>
    ///     Wrapper for <see cref="CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync()"/>
    ///     which waits for the code to finish running.
    /// </summary>
    public static async Task RunOnUIThreadAndWaitAsync(CoreDispatcherPriority priority, Action action)
    {
        TaskCompletionSource<bool> taskCompleted = new TaskCompletionSource<bool>();
        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(priority, () =>
        {
            try
            {
                action();
                taskCompleted.SetResult(true);
            }
            catch (Exception exc)
            {
                taskCompleted.SetException(exc);
                throw;
            }
        });
        await taskCompleted.Task;
    }

    /// <summary>
    ///     Wrapper for <see cref="CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync()"/>
    ///     which waits for the code to finish running.
    /// </summary>
    public static async Task RunOnUIThreadAndWaitAsync(CoreDispatcherPriority priority, Func<Task> func)
    {
        TaskCompletionSource<bool> taskCompleted = new TaskCompletionSource<bool>();
        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(priority, async () =>
        {
            try
            {
                await func();
                taskCompleted.SetResult(true);
            }
            catch (Exception exc)
            {
                taskCompleted.SetException(exc);
                throw;
            }
        });
        await taskCompleted.Task;
    }

    /// <summary>
    ///     Wrapper for <see cref="CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync()"/>
    ///     which waits for the code to finish running and returns its result.
    /// </summary>
    public static async Task<T> RunOnUIThreadAndWaitAsync<T>(CoreDispatcherPriority priority, Func<Task<T>> func)
    {
        TaskCompletionSource<T> taskCompleted = new TaskCompletionSource<T>();
        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(priority, async () =>
        {
            try
            {
                taskCompleted.SetResult(await func());
            }
            catch (Exception exc)
            {
                taskCompleted.SetException(exc);
                throw;
            }
        });
        return await taskCompleted.Task;
    }

    /// <summary>
    ///     Checks whether the given format is an image format.
    /// </summary>
    public static bool IsImageFormat(ImageScannerFormat format)
    {
        switch (format)
        {
            case ImageScannerFormat.Jpeg:
            case ImageScannerFormat.Png:
            case ImageScannerFormat.DeviceIndependentBitmap:
            case ImageScannerFormat.Tiff:
                return true;
            case ImageScannerFormat.Xps:
            case ImageScannerFormat.OpenXps:
            case ImageScannerFormat.Pdf:
            default:
                return false;
        }
    }

    /// <summary>
    ///    Returns the current package version as a friendly string. 
    /// </summary>
    public static string GetCurrentVersion()
    {
        PackageVersion version = Package.Current.Id.Version;
        return String.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
    }

    /// <summary>
    ///     Converts the <paramref name="value"/> from its <paramref name="sourceUnit"/> to the given
    ///     <paramref name="targetUnit"/>.
    /// </summary>
    public static double ConvertMeasurement(double value, SettingMeasurementUnit sourceUnit, SettingMeasurementUnit targetUnit)
    {
        if (sourceUnit != targetUnit)
        {
            // convert value
            if (targetUnit == SettingMeasurementUnit.Metric)
            {
                // inches to cm
                return value * 2.54;
            }
            else
            {
                // cm to inches
                return value / 2.54;
            }
        }
        else
        {
            return value;
        }
    }

    public static double? ConvertAspectRatioOptionToValue(AspectRatioOption option)
    {
        switch (option)
        {
            case AspectRatioOption.Custom:
                return null;
            case AspectRatioOption.Square:
                return 1;
            case AspectRatioOption.ThreeByTwo:
                return 1.5;
            case AspectRatioOption.FourByThree:
                return 1.3333;
            case AspectRatioOption.DinA:
                return 0.7070;
            case AspectRatioOption.AnsiA:
                return 0.7741;
            case AspectRatioOption.AnsiB:
                return 0.6458;
            case AspectRatioOption.AnsiC:
                return 0.7728;
            case AspectRatioOption.Kai4:
                return 0.7216;
            case AspectRatioOption.Kai8:
                return 0.6929;
            case AspectRatioOption.Kai16:
                return 0.7216;
            case AspectRatioOption.Kai32:
                return 0.6954;
            case AspectRatioOption.Legal:
                return 0.6067;
            default:
                throw new ArgumentException($"Can't convert AspectRatioOption {option} to value.");
        }
    }
}