using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Graphics.Canvas;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Scanner.Services;
using Serilog;
using Serilog.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using static Globals;

static class Utilities
{
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
    public static ComboBoxItem CreateComboBoxItem(string content, object tag)
    {
        ComboBoxItem item = new ComboBoxItem
        {
            Content = content,
            Tag = tag
        };

        return item;
    }


    /// <summary>
    ///     Creates a ComboBoxItem with the specified glyph, content string and tag string.
    /// </summary>
    /// <param name="glyph">
    ///     The glyph that shall be displayed to the left of the content.
    /// </param>
    /// <param name="content">
    ///     The ComboBoxItem's content.
    /// </param>
    /// <param name="tag">
    ///     The ComboBoxItem's tag.
    /// </param>
    /// <returns>
    ///     The ComboBoxItem.
    /// </returns>
    public static ComboBoxItem CreateComboBoxItem(string glyph, string content, object tag)
    {
        StackPanel stackPanel = new StackPanel();
        stackPanel.Orientation = Orientation.Horizontal;

        stackPanel.Children.Add(new FontIcon
        {
            Glyph = glyph,
            Margin = new Thickness(0, 0, 8, 0),
            FontSize = 16,
            Opacity = 0.9
        });

        stackPanel.Children.Add(new TextBlock
        {
            Text = content
        });

        ComboBoxItem item = new ComboBoxItem
        {
            Content = stackPanel,
            Tag = tag,
        };

        Windows.UI.Xaml.Automation.AutomationProperties.SetName(item, content);

        return item;
    }


    /// <summary>
    ///     Display a <see cref="BitmapImage"/> inside an <see cref="Image"/> control. If the <paramref name="imageControl"/>
    ///     is already visible and source != null, <see cref="FinishDisplayImage(object, SizeChangedEventArgs)"/>
    ///     is used to make the control visible once the image has been loaded.
    /// </summary>
    /// <remarks>
    ///     If <see cref="FinishDisplayImage(object, SizeChangedEventArgs)"/> is used, the
    ///     <paramref name="imageControl"/> will be empty for a brief moment in order to detect when the
    ///     new image has been loaded.
    /// </remarks>
    /// <param name="bitmapImage">The <see cref="BitmapImage"/> that shall be displayed.</param>
    /// <param name="imageControl">The <see cref="Image"/> control.</param>
    public static void DisplayImage(BitmapImage bitmapImage, Image imageControl)
    {
        if (imageControl.Visibility == Visibility.Collapsed || imageControl.Source == null)
        {
            imageControl.Source = bitmapImage;
            imageControl.Visibility = Visibility.Visible;
        }
        else
        {
            imageControl.Source = null;
            imageControl.Tag = bitmapImage;
        }
    }


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
    ///     Converts a <see cref="ImageScannerFormat"/> into the corresponding <see cref="BitmapEncoder"/>ID.
    /// </summary>
    /// <param name="format">An image format.</param>
    /// <returns>The corresponding <see cref="BitmapEncoder"/>ID.</returns>
    public static Guid GetBitmapEncoderId(ImageScannerFormat? format)
    {
        switch (format)
        {
            case ImageScannerFormat.Jpeg:
                return BitmapEncoder.JpegEncoderId;
            case ImageScannerFormat.Png:
                return BitmapEncoder.PngEncoderId;
            case ImageScannerFormat.Tiff:
                return BitmapEncoder.TiffEncoderId;
            case ImageScannerFormat.DeviceIndependentBitmap:
                return BitmapEncoder.BmpEncoderId;
            default:
                throw new ApplicationException("GetBitmapEncoderId received invalid ImageScannerFormat '" + format + "'.");
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
    ///     Checks whether the ctrl key is currently pressed.
    /// </summary>
    /// <returns>True if the ctrl key is currently pressed.</returns>
    public static bool IsCtrlKeyPressed()
    {
        var ctrlState = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Control);
        return (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }


    /// <summary>
    ///     Empty the <paramref name="canvas"/> and make sure that it has the correct dimensions to cover the image.
    /// </summary>
    /// <param name="canvas">The <see cref="InkCanvas"/> that is to be cleared and resized.</param>
    /// <param name="properties">The <see cref="ImageProperties"/> that include the dimensions.</param>
    public static void InitializeInkCanvas(InkCanvas canvas, double width, double height)
    {
        canvas.InkPresenter.StrokeContainer.Clear();
        canvas.Width = width;
        canvas.Height = height;
    }


    /// <summary>
    ///     Searches the app resources for a localized version of a string.
    /// </summary>
    /// <param name="resource">The resource name.</param>
    /// <returns>The localized string.</returns>
    public static string LocalizedString(string resource)
    {
        if (CoreWindow.GetForCurrentThread() != null) return ResourceLoader.GetForCurrentView().GetString(resource);
        else return "{~STRING~}";
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
    ///     Sends a <see cref="ToastNotification"/> consisting of a <paramref name="title"/>, <paramref name="content"/> and
    ///     an <paramref name="expirationTime"/> (in seconds).
    /// </summary>
    /// <param name="title">The title of the <see cref="ToastNotification"/>.</param>
    /// <param name="content">The content of the <see cref="ToastNotification"/>.</param>
    /// <param name="expirationTime">The time (in seconds) after which the <see cref="ToastNotification"/> is removed from the Action Center.</param>
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

        var toast = new ToastNotification(toastContent.GetXml())
        {
            ExpirationTime = DateTime.Now.AddMinutes(expirationTime)
        };

        ToastNotificationManager.CreateToastNotifier().Show(toast);
    }


    /// <summary>
    ///     Displays a MessageDialog consisting of a title and message.
    /// </summary>
    /// <param name="title">The title of the <see cref="MessageDialog"/>.</param>
    /// <param name="message">The body of the <see cref="MessageDialog"/>.</param>
    public async static Task ShowMessageDialogAsync(string title, string message)
    {
        await RunOnUIThreadAsync(CoreDispatcherPriority.High, async () =>
        {
            MessageDialog messageDialog = new MessageDialog(message, title);
            await messageDialog.ShowAsync();
        });
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
                // Dark mode is active
                applicationViewTitlebar.ButtonForegroundColor = Windows.UI.Colors.LightGray;
            }
            else
            {
                // Light mode is active
                applicationViewTitlebar.ButtonForegroundColor = Windows.UI.Colors.Black;
            }
        }
        else
        {
            if (theme == SettingAppTheme.Light) applicationViewTitlebar.ButtonForegroundColor = Windows.UI.Colors.Black;
            else applicationViewTitlebar.ButtonForegroundColor = Windows.UI.Colors.LightGray;
        }
    }


    /// <summary>
    ///     Gets the image measurements from <see cref="ImageProperties"/>.
    /// </summary>
    /// <returns>
    ///     A tuple of width and height.
    /// </returns>
    public static Tuple<double, double> GetImageMeasurements(ImageProperties properties)
    {
        return new Tuple<double, double>(properties.Width, properties.Height);
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
    ///     Ask the narrator to announce something.
    /// </summary>
    /// <param name="announcement">The announcement.</param>
    /// <param name="liveRegion">A live region on the current page.</param>
    public static async Task NarratorAnnounceAsync(string announcement, TextBlock liveRegion)
    {
        await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
        {
            liveRegion.Text = announcement;
            TextBlockAutomationPeer peer = new TextBlockAutomationPeer(liveRegion);
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        });
    }


    /// <summary>
    ///     Copies the tooltip string of a <see cref="UIElement"/> to its AutomationProperties.Name.
    /// </summary>
    public static void CopyToolTipToAutomationPropertiesName(UIElement element)
    {
        string toolTip = (string)ToolTipService.GetToolTip(element);
        if (toolTip != null)
        {
            Windows.UI.Xaml.Automation.AutomationProperties.SetName(element, toolTip);
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
}