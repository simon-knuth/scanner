using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Graphics.Canvas;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Scanner;
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
using Windows.Services.Store;
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
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using static Enums;
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

        if (Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
        {
            item.CornerRadius = new CornerRadius(2);
        }

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
            CornerRadius = new CornerRadius(2)
        };

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
    ///     Converts a <see cref="SupportedFormat"/> into the corresponding <see cref="BitmapEncoder"/>ID.
    /// </summary>
    /// <param name="format">An image format.</param>
    /// <returns>The corresponding <see cref="BitmapEncoder"/>ID.</returns>
    public static Guid GetBitmapEncoderId(SupportedFormat? format)
    {
        switch (format)
        {
            case SupportedFormat.JPG:
                return BitmapEncoder.JpegEncoderId;
            case SupportedFormat.PNG:
                return BitmapEncoder.PngEncoderId;
            case SupportedFormat.TIF:
                return BitmapEncoder.TiffEncoderId;
            case SupportedFormat.BMP:
                return BitmapEncoder.BmpEncoderId;
            default:
                throw new ApplicationException("GetBitmapEncoderId received invalid SupportedFormat '" + format + "'.");
        }
    }


    /// <summary>
    ///     Converts a <see cref="SupportedFormat"/> into the corresponding <see cref="BitmapEncoder"/>ID.
    /// </summary>
    /// <param name="format">An image format.</param>
    /// <returns>The corresponding <see cref="BitmapEncoder"/>ID.</returns>
    public static SupportedFormat ConvertImageScannerFormatToSupportedFormat(ImageScannerFormat format)
    {
        switch (format)
        {
            case ImageScannerFormat.Jpeg:
                return SupportedFormat.JPG;
            case ImageScannerFormat.Png:
                return SupportedFormat.PNG;
            case ImageScannerFormat.DeviceIndependentBitmap:
                return SupportedFormat.BMP;
            case ImageScannerFormat.Tiff:
                return SupportedFormat.TIF;
            case ImageScannerFormat.Xps:
                return SupportedFormat.XPS;
            case ImageScannerFormat.OpenXps:
                return SupportedFormat.OpenXPS;
            case ImageScannerFormat.Pdf:
                return SupportedFormat.PDF;
            default:
                throw new ApplicationException("ConvertImageScannerFormatToSupportedFormat received invalid ImageScannerFormat '" + format + "' for conversion.");
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
    ///     Loads the <see cref="scanFolder"/> from the <see cref="futureAccessList"/>.
    /// </summary>
    public static async Task LoadScanFolderAsync()
    {
        StorageItemAccessList futureAccessList = StorageApplicationPermissions.FutureAccessList;
        string defaultScanFolderName = GetDefaultScanFolderName();

        if (futureAccessList.Entries.Count != 0)
        {
            try { scanFolder = await futureAccessList.GetFolderAsync("scanFolder"); }
            catch (Exception exc)
            {
                log.Error(exc, "Loading scanFolder from futureAccessList failed.");
                try { scanFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync(defaultScanFolderName, CreationCollisionOption.OpenIfExists); }
                catch (Exception exc2)
                {
                    log.Error(exc2, "Creating a new scanFolder in PicturesLibrary failed as well.");
                    ShowMessageDialogAsync(LocalizedString("ErrorMessageLoadScanFolderHeader"), LocalizedString("ErrorMessageLoadScanFolderBody"));
                }
                futureAccessList.AddOrReplace("scanFolder", scanFolder);
            }
        }
        else
        {
            // Either first app launch ever or the futureAccessList is unavailable ~> Reset it
            try
            {
                scanFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync(defaultScanFolderName, CreationCollisionOption.OpenIfExists);
            }
            catch (UnauthorizedAccessException exc)
            {
                log.Error(exc, "Creating a new scanFolder in PicturesLibrary failed. (Unauthorized)");
                ShowMessageDialogAsync(LocalizedString("ErrorMessageResetFolderUnauthorizedHeading"), LocalizedString("ErrorMessageResetFolderUnauthorizedBody"));
                return;
            }
            catch (Exception exc)
            {
                log.Error(exc, "Creating a new scanFolder in PicturesLibrary failed.");
                ShowMessageDialogAsync(LocalizedString("ErrorMessageResetFolderHeading"), LocalizedString("ErrorMessageResetFolderBody") + "\n" + exc.Message);
                return;
            }
            futureAccessList.AddOrReplace("scanFolder", scanFolder);
        }
    }


    /// <summary>
    ///     Loads all settings/data from the app's data container and initializes default data if necessary.
    /// </summary>
    public static void LoadSettings()
    {
        localSettingsContainer = ApplicationData.Current.LocalSettings;

        if (localSettingsContainer.Values["settingSaveLocationAsk"] != null)
        {
            settingSaveLocationAsk = (bool)localSettingsContainer.Values["settingSaveLocationAsk"];
        }
        else
        {
            settingSaveLocationAsk = false;
            localSettingsContainer.Values["settingSaveLocationAsk"] = settingSaveLocationAsk;
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
        localSettingsContainer.Values["awaitingRestartAfterThemeChange"] = false;

        if (localSettingsContainer.Values["settingAppendTime"] != null)
        {
            settingAppendTime = (bool)localSettingsContainer.Values["settingAppendTime"];
        }
        else
        {
            settingAppendTime = true;
            localSettingsContainer.Values["settingAppendTime"] = settingAppendTime;
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

        if (localSettingsContainer.Values["settingErrorStatistics"] != null)
        {
            settingErrorStatistics = (bool)localSettingsContainer.Values["settingErrorStatistics"];
        }
        else
        {
            settingErrorStatistics = false;
            localSettingsContainer.Values["settingErrorStatistics"] = settingErrorStatistics;
        }
        if (settingErrorStatistics == true)
        {
            AppCenter.SetEnabledAsync(true);
        }
        else AppCenter.SetEnabledAsync(false);
        RegisterWithMicrosoftAppCenter();

        PackageVersion version = Package.Current.Id.Version;
        string currentVersionNumber = String.Format("Version {0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
        if (localSettingsContainer.Values["lastKnownVersion"] != null)
        {
            string lastKnownVersionNumber = (string)localSettingsContainer.Values["lastKnownVersion"];

            if (currentVersionNumber != lastKnownVersionNumber) isFirstAppLaunchWithThisVersion = true;
            else isFirstAppLaunchWithThisVersion = false;

            localSettingsContainer.Values["lastKnownVersion"] = currentVersionNumber;
        }
        else
        {
            // first launch of the app ever
            localSettingsContainer.Values["lastKnownVersion"] = currentVersionNumber;
        }

        if (localSettingsContainer.Values["scanNumber"] != null)
        {
            scanNumber = (int)localSettingsContainer.Values["scanNumber"];
        }
        else
        {
            // first launch of the app ever
            localSettingsContainer.Values["scanNumber"] = 1;
            scanNumber = 1;
        }

        if (localSettingsContainer.Values["lastTouchDrawState"] != null)
        {
            lastTouchDrawState = (bool)localSettingsContainer.Values["lastTouchDrawState"];
        }
        else
        {
            lastTouchDrawState = false;
        }

        if (localSettingsContainer.Values["manageTutorialAlreadyShown"] != null)
        {
            manageTutorialAlreadyShown = (bool)localSettingsContainer.Values["manageTutorialAlreadyShown"];
        }
        else
        {
            manageTutorialAlreadyShown = false;
        }

        log.Information("Settings loaded: [settingSaveLocationAsk={SettingSaveLocationAsk}|settingAppTheme={SettingAppTheme}|settingAppendTime={SettingAppendTime}|settingNotificationScanComplete={SettingNotificationScanComplete}|settingErrorStatistics={SettingErrorStatistics}" +
            "|isFirstAppLaunchWithThisVersion={IsFirstAppLaunchWithThisVersion}|scanNumber={ScanNumber}|lastTouchDrawState={LastTouchDrawState}|manageTutorialAlreadyShown={ManageTutorialAlreadyShown}]",
            settingSaveLocationAsk, settingAppTheme, settingAppendTime, settingNotificationScanComplete, settingErrorStatistics, isFirstAppLaunchWithThisVersion, scanNumber, lastTouchDrawState, manageTutorialAlreadyShown);
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
    ///     Resets the <see cref="scanFolder"/> to "Scans" in the Pictures Library.
    /// </summary>
    /// <remarks>
    ///     The folder is created if necessary.
    /// </remarks>
    /// <exception cref="UnauthorizedAccessException">Access to the Pictures Library has been denied.</exception>
    /// <exception cref="Exception">Remaining errors</exception>
    public static async Task ResetScanFolderAsync()
    {
        StorageFolder folder;
        string defaultScanFolderName = GetDefaultScanFolderName();

        try
        {
            folder = await KnownFolders.PicturesLibrary.CreateFolderAsync(defaultScanFolderName, CreationCollisionOption.OpenIfExists);
        }
        catch (UnauthorizedAccessException exc)
        {
            log.Error(exc, "Resetting the scan folder failed. (Unauthorized)");
            Crashes.TrackError(exc);
            throw;
        }
        catch (Exception exc)
        {
            log.Error(exc, "Resetting the scan folder failed.");
            Crashes.TrackError(exc);
            throw;
        }

        scanFolder = folder;
        StorageApplicationPermissions.FutureAccessList.AddOrReplace("scanFolder", scanFolder);
        log.Information("Resetting scan folder successful.");
    }


    /// <summary>
    ///     Sends a <see cref="ToastNotification"/> consisting of a <paramref name="title"/>, <paramref name="content"/>,
    ///     an <paramref name="expirationTime"/> (in seconds) and an image.
    /// </summary>
    /// <param name="title">The title of the <see cref="ToastNotification"/>.</param>
    /// <param name="content">The content of the <see cref="ToastNotification"/>.</param>
    /// <param name="expirationTime">The time (in seconds) after which the <see cref="ToastNotification"/> is removed from the Action Center.</param>
    /// <param name="imageURI">The URI pointing to the image that is displayed as part of the <see cref="ToastNotification"/>.</param>
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

        var toast = new ToastNotification(toastContent.GetXml())
        {
            ExpirationTime = DateTime.Now.AddMinutes(expirationTime)
        };

        ToastNotificationManager.CreateToastNotifier().Show(toast);
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
    public async static void ShowMessageDialogAsync(string title, string message)
    {
        MessageDialog messageDialog = new MessageDialog(message, title);
        await messageDialog.ShowAsync();
    }


    /// <summary>
    ///     Adapts the titlebar buttons to the current theme.
    /// </summary>
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
        }
        else
        {
            if (settingAppTheme == Theme.light) applicationViewTitlebar.ButtonForegroundColor = Windows.UI.Colors.Black;
            else applicationViewTitlebar.ButtonForegroundColor = Windows.UI.Colors.LightGray;
        }
    }


    /// <summary>
    ///     Saves all settings to the app's data container.
    /// </summary>
    public static void SaveSettings()
    {
        localSettingsContainer.Values["settingSaveLocationAsk"] = settingSaveLocationAsk;
        localSettingsContainer.Values["settingAppTheme"] = (int)settingAppTheme;
        localSettingsContainer.Values["settingAppendTime"] = settingAppendTime;
        localSettingsContainer.Values["settingNotificationScanComplete"] = settingNotificationScanComplete;
        localSettingsContainer.Values["settingErrorStatistics"] = settingErrorStatistics;

        if (settingErrorStatistics == true) AppCenter.SetEnabledAsync(true);
        else AppCenter.SetEnabledAsync(false);
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
    ///     Returns the default name of the folder that scans are saved to. This varies depending on the system language.
    ///     The fallback name is "Scans".
    /// </summary>
    private static string GetDefaultScanFolderName()
    {
        string defaultScanFolderName = LocalizedString("DefaultScanFolderName");
        bool validName = true;

        foreach (char character in defaultScanFolderName.ToCharArray())
        {
            if (!Char.IsLetter(character))
            {
                validName = false;
                break;
            }
        }

        if (defaultScanFolderName == "" || validName == false)
        {
            defaultScanFolderName = "Scans";        // fallback name if there is an issue with the localization
            Crashes.TrackError(new ApplicationException("The localized scan folder name is invalid, using 'Scans' instead."));
        }

        return defaultScanFolderName;
    }


    /// <summary>
    ///     Checks whether <see cref="scanFolder"/> is set to its default value (..\Pictures\Scans).
    /// </summary>
    /// <returns>
    ///     null  - <see cref="scanFolder"/> is null
    ///     true  - <see cref="scanFolder"/> is set to its default value
    ///     false - <see cref="scanFolder"/> is not set to its default value or null
    /// </returns>
    public static async Task<bool?> IsDefaultScanFolderSetAsync()
    {
        if (scanFolder == null) return null;

        StorageFolder folder;
        try
        {
            folder = await KnownFolders.PicturesLibrary.GetFolderAsync(GetDefaultScanFolderName());
        }
        catch (Exception)
        {
            return false;
        }

        if (folder.Path == scanFolder.Path) return true;
        else return false;
    }


    /// <summary>
    ///     Converts a string to to a <see cref="SupportedFormat"/>.
    /// </summary>
    /// <param name="formatString">
    ///     The source format string, case-insensitive and with or without dot.
    ///     E.g. "png" / "PNG" / ".png" / ".PNG"
    /// </param>
    /// <returns>The corresponding <see cref="SupportedFormat"/>.</returns>
    public static SupportedFormat? ConvertFormatStringToSupportedFormat(string formatString)
    {
        switch (formatString.ToLower())
        {
            case "jpg":
            case "jpeg":
            case ".jpg":
                return SupportedFormat.JPG;

            case "png":
            case ".png":
                return SupportedFormat.PNG;

            case "tif":
            case "tiff":
            case ".tif":
            case ".tiff":
                return SupportedFormat.TIF;

            case "bmp":
            case ".bmp":
                return SupportedFormat.BMP;

            case "pdf":
            case ".pdf":
                return SupportedFormat.PDF;

            case "xps":
            case ".xps":
                return SupportedFormat.XPS;

            case "oxps":
            case ".oxps":
                return SupportedFormat.OpenXPS;

            default:
                return null;
        }
    }


    public async static Task ShowRatingDialogAsync()
    {
        try
        {
            log.Information("Displaying rating dialog.");
            StoreContext storeContext = StoreContext.GetDefault();
            await storeContext.RequestRateAndReviewAppAsync();
        }
        catch (Exception exc)
        {
            log.Warning(exc, "Displaying the rating dialog failed.");
            try { await Launcher.LaunchUriAsync(new Uri(storeRateUri)); } catch (Exception) { }
        }
    }


    public async static Task LaunchFeedbackHubAsync()
    {
        try
        {
            log.Information("Launching the Feedback Hub.");
            var launcher = Microsoft.Services.Store.Engagement.StoreServicesFeedbackLauncher.GetDefault();
            await launcher.LaunchAsync();
        }
        catch (Exception) { }
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
    ///     Deletes all files from temporary folder. Creates the following subfolders, if necessary:
    ///         - conversion
    /// </summary>
    public static async Task InitializeTempFolderAsync()
    {
        folderTemp = ApplicationData.Current.TemporaryFolder;

        IReadOnlyList<StorageFile> files = await folderTemp.GetFilesAsync();
        foreach (StorageFile file in files)
        {
            await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }

        // attempt to actively delete folders first, replacing is not reliable
        try
        {
            StorageFolder folder = await folderTemp.GetFolderAsync("conversion");
            await folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
        } catch (Exception exc) { log.Error(exc, "Actively deleting folder 'conversion' in temp folder failed."); }

        try
        {
            StorageFolder folder = await folderTemp.GetFolderAsync("withoutRotation");
            await folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }
        catch (Exception exc) { log.Error(exc, "Actively deleting folder 'withoutRotation' in temp folder failed."); }

        // replace/create folders
        try
        {
            folderConversion = await folderTemp.CreateFolderAsync("conversion", CreationCollisionOption.ReplaceExisting);
        }
        catch (Exception exc)
        {
            log.Error(exc, "Couldn't create/replace folder 'conversion' in temp folder.");
            throw;
        }

        try
        {
            folderWithoutRotation = await folderTemp.CreateFolderAsync("withoutRotation", CreationCollisionOption.ReplaceExisting);
        }
        catch (Exception exc)
        {
            log.Error(exc, "Couldn't create/replace folder 'withoutRotation' in temp folder.");
            throw;
        }

        log.Information("Initialized temp folder");
    }


    /// <summary>
    ///     Wrapper for <see cref="CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync()"/> .
    /// </summary>
    public static IAsyncAction RunOnUIThreadAsync(CoreDispatcherPriority priority, DispatchedHandler agileCallback)
    {
        return Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(priority, agileCallback);
    }


    /// <summary>
    ///     Initializes <see cref="log"/> to a file sink in folder "logs" within the app's RoamingFolder. Also adds some meta
    ///     data to the log.
    /// </summary>
    public static async Task InitializeSerilogAsync()
    {
        StorageFolder folder = await ApplicationData.Current.RoamingFolder
            .CreateFolderAsync("logs", CreationCollisionOption.OpenIfExists);
        string logPath = Path.Combine(folder.Path, "log.txt");

        log = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Async(a => a.File(new Serilog.Formatting.Json.JsonFormatter(),
            logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 8,
            fileSizeLimitBytes: 6900000))       // Microsoft App Center supports attachments up to 7 MB
            .Enrich.WithExceptionDetails()
            .CreateLogger();
        log.Information("--- Log initialized ---");

        // add meta data
        PackageVersion version = Package.Current.Id.Version;
        log.Information("App version: {0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
        log.Information("OS: {OS} | OS version: {Version} | OS architecture: {Architecture}",
            SystemInformation.Instance.OperatingSystem, SystemInformation.Instance.OperatingSystemVersion, SystemInformation.Instance.OperatingSystemArchitecture);
        log.Information("Device family: {Family} | Device model: {Model} | Device manufacturer: {Manufacturer}",
            SystemInformation.Instance.DeviceFamily, SystemInformation.Instance.DeviceModel, SystemInformation.Instance.DeviceManufacturer);
    }


    /// <summary>
    ///     Attaches the relevant log to the Microsoft App Center error report.
    /// </summary>
    public async static Task<ErrorAttachmentLog[]> SendRelevantLogWithErrorReportAsync(ErrorReport report)
    {
        try
        {
            // close log file
            Log.CloseAndFlush();
            await InitializeSerilogAsync();

            // get all logs
            StorageFolder logFolder = await ApplicationData.Current.RoamingFolder.GetFolderAsync("logs");
            IReadOnlyList<StorageFile> files = await logFolder.GetFilesAsync();

            // find relevant log
            List<StorageFile> sortedLogs = new List<StorageFile>(files);
            sortedLogs.Sort(delegate (StorageFile x, StorageFile y)
            {
                return DateTimeOffset.Compare(x.DateCreated, y.DateCreated);
            });
            sortedLogs.Reverse();
            foreach (StorageFile log in sortedLogs)
            {
                if (log.DateCreated <= report.AppErrorTime) {
                    IBuffer buffer = await FileIO.ReadBufferAsync(log);
                    return new ErrorAttachmentLog[]
                    {
                        ErrorAttachmentLog.AttachmentWithBinary(buffer.ToArray(), "log.json", "application/json")
                    };
                }
            }
        }
        catch (Exception exc)
        {
            return new ErrorAttachmentLog[]
            {
                ErrorAttachmentLog.AttachmentWithText("Failed to append log. (" + exc.Message + ")", "nolog.txt")
            };
        }

        return new ErrorAttachmentLog[]
        {
            ErrorAttachmentLog.AttachmentWithText("Failed to append log.", "nolog.txt")
        };
    }


    /// <summary>
    ///     Registers the app with Microsoft's App Center service. It can still be enabled/disabled
    ///     separately from this.
    /// </summary>
    public static void RegisterWithMicrosoftAppCenter()
    {
        Crashes.GetErrorAttachments = (report) => SendRelevantLogWithErrorReportAsync(report).Result;
        AppCenter.Start(GetSecret("SecretAppCenter"), typeof(Analytics), typeof(Crashes));
    }


    /// <summary>
    ///     Send information on a new scanner to App Center
    /// </summary>
    public static void SendScannerAnalytics(RecognizedScanner scanner)
    {
        string formatCombination = "";
        bool jpgSupported, pngSupported, pdfSupported, xpsSupported, oxpsSupported, tifSupported, bmpSupported;
        jpgSupported = pngSupported = pdfSupported = xpsSupported = oxpsSupported = tifSupported = bmpSupported = false;

        try
        {
            if (scanner.scanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Jpeg))
            {
                formatCombination = formatCombination.Insert(formatCombination.Length, "|JPG");
                jpgSupported = true;
            }
            if (scanner.scanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Png))
            {
                formatCombination = formatCombination.Insert(formatCombination.Length, "|PNG");
                pngSupported = true;
            }
            if (scanner.scanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Pdf))
            {
                formatCombination = formatCombination.Insert(formatCombination.Length, "|PDF");
                pdfSupported = true;
            }
            if (scanner.scanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Xps))
            {
                formatCombination = formatCombination.Insert(formatCombination.Length, "|XPS");
                xpsSupported = true;
            }
            if (scanner.scanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.OpenXps))
            {
                formatCombination = formatCombination.Insert(formatCombination.Length, "|OXPS");
                oxpsSupported = true;
            }
            if (scanner.scanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.Tiff))
            {
                formatCombination = formatCombination.Insert(formatCombination.Length, "|TIF");
                tifSupported = true;
            }
            if (scanner.scanner.AutoConfiguration.IsFormatSupported(ImageScannerFormat.DeviceIndependentBitmap))
            {
                formatCombination = formatCombination.Insert(formatCombination.Length, "|BMP");
                bmpSupported = true;
            }

            formatCombination = formatCombination.Insert(formatCombination.Length, "|");


            Analytics.TrackEvent("Scanner added", new Dictionary<string, string> {
                            { "formatCombination", formatCombination },
                            { "jpgSupported", jpgSupported.ToString() },
                            { "pngSupported", pngSupported.ToString() },
                            { "pdfSupported", pdfSupported.ToString() },
                            { "xpsSupported", xpsSupported.ToString() },
                            { "oxpsSupported", oxpsSupported.ToString() },
                            { "tifSupported", tifSupported.ToString() },
                            { "bmpSupported", bmpSupported.ToString() },
                            { "hasAuto", scanner.isAutoAllowed.ToString() },
                            { "hasFlatbed", scanner.isFlatbedAllowed.ToString() },
                            { "hasFeeder", scanner.isFeederAllowed.ToString() },
                            { "autoPreviewSupported", scanner.isAutoPreviewAllowed.ToString() },
                            { "flatbedPreviewSupported", scanner.isFlatbedPreviewAllowed.ToString() },
                            { "feederPreviewSupported", scanner.isFeederPreviewAllowed.ToString() },
                        });
        }
        catch (Exception) { }
    }
}