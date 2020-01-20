using Microsoft.Graphics.Canvas;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using Windows.ApplicationModel.Resources;
using Windows.ApplicationModel;
using Windows.Graphics.Imaging;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml;

using static Enums;
using static Globals;
using Windows.Devices.Scanners;

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
        ComboBoxItem item = new ComboBoxItem();

        item.Content = content;
        item.Tag = tag;

        return item;
    }


    /// <summary>
    ///     Display an image file inside an <see cref="Image"/> control. If the <paramref name="imageControl"/>
    ///     is already visible and source != null, <see cref="FinishDisplayImage(object, SizeChangedEventArgs)"/> 
    ///     is used to make the control visible once the image has been loaded.
    /// </summary>
    /// <remarks>
    ///     If <see cref="FinishDisplayImage(object, SizeChangedEventArgs)"/> is used, the
    ///     <paramref name="imageControl"/> will be empty for a brief moment in order to detect when the
    ///     new image has been loaded.
    /// </remarks>
    /// <param name="file">The <see cref="StorageFile"/> containing the image.</param>
    /// <param name="imageControl">The <see cref="Image"/> control.</param>
    public static async void DisplayImageAsync(StorageFile file, Image imageControl)
    {
        IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
        BitmapImage bmp = new BitmapImage();
        await bmp.SetSourceAsync(stream);

        if (imageControl.Visibility == Visibility.Collapsed || imageControl.Source == null)
        {
            imageControl.Source = bmp;
            imageControl.Visibility = Visibility.Visible;
        } else
        {
            imageControl.SizeChanged += FinishDisplayImage;
            imageLoading = true;
            imageControl.Source = null;
            imageControl.Tag = bmp;
        }
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
            imageControl.SizeChanged += FinishDisplayImage;
            imageLoading = true;
            imageControl.Source = null;
            imageControl.Tag = bitmapImage;
        }
    }


    /// <summary>
    ///     The part of <see cref="DisplayImageAsync(StorageFile, Image)"/> and 
    ///     <see cref="DisplayImage(BitmapImage, Image)"/> responsible for making the <see cref="Image"/>
    ///     visible when its source has been loaded.
    /// </summary>
    private static void FinishDisplayImage(object sender, SizeChangedEventArgs args)
    {
        if (imageLoading && ((Image)sender).Source == null)
        {
            ((Image)sender).Source = (Windows.UI.Xaml.Media.ImageSource)((Image)sender).Tag;
        }
        else
        {
            imageLoading = false;
            ((Image)sender).Visibility = Visibility.Visible;
            ((Image)sender).SizeChanged -= FinishDisplayImage;
        }
    }


    /// <summary>
    ///     Converts a <paramref name="formatString"/> into the corresponding BitmapEncoderId.
    /// </summary>
    /// <param name="formatString">"jpg", "jpeg", "png", "bmp" or "tiff"/"tif".</param>
    /// <returns>The corresponding BitmapEncoderId.</returns>
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
            default:
                return BitmapEncoder.TiffEncoderId;
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
                return BitmapFileFormat.Jpeg;
            case "jpeg":
                return BitmapFileFormat.Jpeg;
            case "png":
                return BitmapFileFormat.Png;
            case "bmp":
                return BitmapFileFormat.Bmp;
            default:
                return BitmapFileFormat.Tiff;
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
        string formatString = file.Name.Split(".")[1];

        switch (formatString)
        {
            case "jpg":
                return CanvasBitmapFileFormat.Jpeg;
            case "jpeg":
                return CanvasBitmapFileFormat.Jpeg;
            case "png":
                return CanvasBitmapFileFormat.Png;
            case "bmp":
                return CanvasBitmapFileFormat.Bmp;
            default:
                return CanvasBitmapFileFormat.Tiff;
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
    public static async void LoadScanFolder()
    {
        StorageItemAccessList futureAccessList = StorageApplicationPermissions.FutureAccessList;

        if (futureAccessList.Entries.Count != 0)
        {
            try { scanFolder = await futureAccessList.GetFolderAsync("scanFolder"); }
            catch (Exception)
            {
                try { scanFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync("Scans", CreationCollisionOption.OpenIfExists); }
                catch (Exception)
                {
                    ShowMessageDialog(LocalizedString("ErrorMessageLoadScanFolderHeader"), LocalizedString("ErrorMessageLoadScanFolderBody"));
                }
                futureAccessList.AddOrReplace("scanFolder", scanFolder);
            }
        }
        else
        {
            // Either first app launch ever or the futureAccessList is unavailable ~> Reset it
            try
            {
                scanFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync("Scans", CreationCollisionOption.OpenIfExists);
            }
            catch (UnauthorizedAccessException)
            {
                ShowMessageDialog(LocalizedString("ErrorMessageResetFolderUnauthorizedHeader"), LocalizedString("ErrorMessageResetFolderUnauthorizedBody"));
                return;
            }
            catch (Exception exc)
            {
                ShowMessageDialog(LocalizedString("ErrorMessageResetFolderHeader"), LocalizedString("ErrorMessageResetFolderBody") + "\n" + exc.Message);
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

        if (localSettingsContainer.Values["settingAutomaticScannerSelection"] != null)
        {
            settingAutomaticScannerSelection = (bool)localSettingsContainer.Values["settingAutomaticScannerSelection"];
        }
        else
        {
            settingAutomaticScannerSelection = true;
            localSettingsContainer.Values["settingAutomaticScannerSelection"] = settingAutomaticScannerSelection;
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

        if (localSettingsContainer.Values["settingDrawPenDetected"] != null)
        {
            settingDrawPenDetected = (bool)localSettingsContainer.Values["settingDrawPenDetected"];
        }
        else
        {
            settingDrawPenDetected = true;
            localSettingsContainer.Values["settingDrawPenDetected"] = settingDrawPenDetected;
        }

        PackageVersion version = Package.Current.Id.Version;
        string currentVersionNumber = String.Format("Version {0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
        if (localSettingsContainer.Values["lastKnownVersion"] != null)
        {
            string lastKnownVersionNumber = (string) localSettingsContainer.Values["lastKnownVersion"];

            if (currentVersionNumber != lastKnownVersionNumber) firstAppLaunchWithThisVersion = true;
            else firstAppLaunchWithThisVersion = false;

            localSettingsContainer.Values["lastKnownVersion"] = currentVersionNumber;
        }
        else
        {
            // first launch of the app ever
            localSettingsContainer.Values["lastKnownVersion"] = currentVersionNumber;
        }

        if (localSettingsContainer.Values["scanNumber"] != null)
        {
            scanNumber = (int) localSettingsContainer.Values["scanNumber"];
        }
        else
        {
            // first launch of the app ever
            localSettingsContainer.Values["scanNumber"] = 1;
            scanNumber = 1;
        }
    }


    /// <summary>
    ///     Searches the app resources for a localized version of a string.
    /// </summary>
    /// <param name="resource">The resource name.</param>
    /// <returns>The localized string.</returns>
    public static string LocalizedString(string resource)
    {
        return ResourceLoader.GetForCurrentView().GetString(resource);
    }


    /// <summary>
    ///     Locks/disables all items of the given CommandBar, except for one.
    /// </summary>
    /// <param name="commandBar">The CommandBar of which the items shall be disabled.</param>
    /// <param name="except">The control that shall not be disabled.</param>
    public static void LockCommandBar(CommandBar commandBar, Control except)
    {
        if (except == null)
        {
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
        }
        if (commandBar.Content != null) ((Control)commandBar.Content).IsEnabled = false;
    }


    /// <summary>
    ///     Locks/disables all items of the given <paramref name="commandBar"/>.
    /// </summary>
    /// <param name="commandBar">The <see cref="CommandBar"/> of which the items shall be disabled.</param>
    public static void LockCommandBar(CommandBar commandBar)
    {
        LockCommandBar(commandBar, null);
    }


    /// <summary>
    ///     Removes brackets, their content and the leading whitespace from <paramref name="input"/>.
    /// </summary>
    /// <param name="input">A string like "abc (def).xyz", of which " (def)" shall be removed.</param>
    public static string RemoveNumbering(string input)
    {
        // expect string like "abc (def).xyz" and deliver "abc.xyz"
        string name = input.Substring(0, input.LastIndexOf("."));       // get name without file extension
        string extension = input.Substring(input.LastIndexOf("."));     // get file extension (with ".")

        if (name[name.Length - 1] == ')' && name.Contains(" ("))
        {
            name = name.Substring(0, name.LastIndexOf(" ("));
            return name + extension;
        } else
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
    public static async void ResetScanFolder()
    {
        StorageFolder folder;
        try
        {
            folder = await KnownFolders.PicturesLibrary.CreateFolderAsync("Scans", CreationCollisionOption.OpenIfExists);
        }
        catch (UnauthorizedAccessException exc)
        {
            throw exc;
        }
        catch (Exception exc)
        {
            throw exc;
        }

        scanFolder = folder;
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


        var toast = new ToastNotification(toastContent.GetXml());
        toast.ExpirationTime = DateTime.Now.AddMinutes(expirationTime);

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


        var toast = new ToastNotification(toastContent.GetXml());
        toast.ExpirationTime = DateTime.Now.AddMinutes(expirationTime);

        ToastNotificationManager.CreateToastNotifier().Show(toast);
    }


    /// <summary>
    ///     Displays a MessageDialog consisting of a title and message.
    /// </summary>
    /// <param name="title">The title of the <see cref="MessageDialog"/>.</param>
    /// <param name="message">The body of the <see cref="MessageDialog"/>.</param>
    public async static void ShowMessageDialog(string title, string message)
    {
        MessageDialog messageDialog = new MessageDialog(message, title);
        await messageDialog.ShowAsync();
    }


    /// <summary>
    ///     Unlocks/enables all items of the given <paramref name="commandBar"/>, <paramref name="except"/> for one.
    /// </summary>
    /// <param name="commandBar">The <see cref="CommandBar"/> of which the items shall be enabled.</param>
    /// <param name="except">The control that shall not be enabled.</param>
    public static void UnlockCommandBar(CommandBar commandBar, Control except)
    {
        if (except == null)
        {
            foreach (Control item in commandBar.PrimaryCommands) item.IsEnabled = true;
        }
        else
        {
            foreach (Control item in commandBar.PrimaryCommands) if (item != except) item.IsEnabled = true;
        }
        if (commandBar.Content != null) ((Control)commandBar.Content).IsEnabled = true;
    }


    /// <summary>
    ///     Unlocks/enables all items of the given CommandBar.
    /// </summary>
    /// <param name="commandBar">The CommandBar of which the items shall be enabled.</param>
    public static void UnlockCommandBar(CommandBar commandBar)
    {
        UnlockCommandBar(commandBar, null);
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
        } else
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
        localSettingsContainer.Values["settingAppTheme"] = (int) settingAppTheme;
        localSettingsContainer.Values["settingSearchIndicator"] = settingSearchIndicator;
        localSettingsContainer.Values["settingAutomaticScannerSelection"] = settingAutomaticScannerSelection;
        localSettingsContainer.Values["settingNotificationScanComplete"] = settingNotificationScanComplete;
        localSettingsContainer.Values["settingUnsupportedFileFormat"] = settingUnsupportedFileFormat;
        localSettingsContainer.Values["settingDrawPenDetected"] = settingDrawPenDetected;
    }


    // TODO documentation
    public async static System.Threading.Tasks.Task<Tuple<double, double>> RefreshImageMeasurementsAsync(StorageFile image)
    {
        ImageProperties imageProperties = await image.Properties.GetImagePropertiesAsync();

        return new Tuple<double, double>(imageProperties.Width, imageProperties.Height);
    }


    // TODO documentation
    public static Tuple<double, double> RefreshImageMeasurements(BitmapImage image)
    {
        return new Tuple<double, double>(image.PixelWidth, image.PixelHeight);
    }


    /// <summary>
    ///     Checks whether <see cref="scanFolder"/> is set to its default value (..\Pictures\Scans).
    /// </summary>
    /// <returns>
    ///     null  - <see cref="scanFolder"/> is null
    ///     true  - <see cref="scanFolder"/> is set to its default value
    ///     false - <see cref="scanFolder"/> is not set to its default value or null
    /// </returns>
    public static async System.Threading.Tasks.Task<bool?> IsDefaultScanFolderSet()
    {
        if (scanFolder == null) return null;

        StorageFolder folder;
        try
        {
            folder = await KnownFolders.PicturesLibrary.GetFolderAsync("Scans");
        }
        catch (Exception)
        {
            return false;
        }

        if (folder.Path == scanFolder.Path) return true;
        else return false;
    }


    public static async System.Threading.Tasks.Task<string> ConvertScannedFile(StorageFile file, Tuple<ImageScannerFormat, string> formatFlow, ImageScannerScanResult result)
    {
        string newName;
        switch (formatFlow.Item2)
        {
            case "PDF":
                // convert to PDF
                try
                {
                    // save the source and target name
                    ApplicationData.Current.LocalSettings.Values["sourceFileName"] = file.Name;
                    ApplicationData.Current.LocalSettings.Values["targetFileName"] = file.DisplayName + ".pdf";

                    // save measurements, which determine PDF file size
                    var imageProperties = await file.Properties.GetImagePropertiesAsync();
                    ApplicationData.Current.LocalSettings.Values["sourceFileWidth"] = imageProperties.Width;
                    ApplicationData.Current.LocalSettings.Values["sourceFileHeight"] = imageProperties.Height;

                    // call win32 app
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();

                    // get result file and move it to its correct folder
                    StorageFile convertedFile = await ApplicationData.Current.TemporaryFolder.GetFileAsync(file.DisplayName + ".pdf");
                    await convertedFile.MoveAsync(scanFolder, convertedFile.Name, NameCollisionOption.GenerateUniqueName);

                    newName = convertedFile.Name;
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
                catch (Exception) 
                {
                    throw;
                }                
                break;

            default:
                // open image file, decode it and prepare an encoder with the target format
                IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                Guid encoderId = GetBitmapEncoderId(formatFlow.Item2);
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
                encoder.SetSoftwareBitmap(softwareBitmap);

                // save/encode the file in the target format
                try { await encoder.FlushAsync(); }
                catch (Exception)
                {
                    throw;
                }
                stream.Dispose();

                // rename file to make the extension match the new format and watch out for name collisions
                string newNameWithoutNumbering = RemoveNumbering(file.Name
                    .Replace("." + file.Name.Split(".")[1], "." + formatFlow.Item2));
                newName = newNameWithoutNumbering;

                try { await result.ScannedFiles[0].RenameAsync(newName, NameCollisionOption.FailIfExists); }
                catch (Exception)
                {
                    // cycle through file numberings until one is not occupied
                    for (int i = 1; true; i++)
                    {
                        try
                        {
                            await result.ScannedFiles[0].RenameAsync(newNameWithoutNumbering.Split(".")[0] + " (" + i.ToString()
                                + ")." + newNameWithoutNumbering.Split(".")[1], NameCollisionOption.FailIfExists);
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                        newName = newNameWithoutNumbering.Split(".")[0] + " (" + i.ToString() + ")." + newNameWithoutNumbering.Split(".")[1];
                        break;
                    }
                }
                break;
        }
        
        return newName;
    }
}