using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Scanner.Extensions;
using Scanner.Messages;
using Scanner.Models;
using Scanner.Models.ItemNaming;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using Serilog;
using Serilog.Exceptions;
using Serilog.Formatting.Compact;
using Serilog.Sinks.File;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUIEx;
using static Scanner.Helpers.Helpers;

namespace Scanner.Services
{
    internal class SaveLocationService : ObservableRecipient, ISaveLocationService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        private readonly ISentryService? SentryService = Ioc.Default.GetService<ISentryService>();
        private readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        #endregion

        #region Constants
        private const string futureAccessListFixedLocationToken = "scanFolder";
        private const string futureAccessListRecentFoldersToken = "recentFolder";
        private const int recentFoldersLimit = 5;
        #endregion

        private TaskCompletionSource initializationCompleted = new();

        private StorageFolder? fixedSaveLocation;

        private StorageItemAccessList futureAccessList = StorageApplicationPermissions.FutureAccessList;

        private bool isFixedSaveLocationSupported = true;

        private List<StorageFolder> recentFolders = new();


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SaveLocationService()
        {
            Task.Run(InitializeAsync);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private async Task InitializeAsync()
        {
            if (futureAccessList.Entries.Count != 0)
            {
                // save location may be cached
                try
                {
                    fixedSaveLocation = await futureAccessList.GetFolderAsync(futureAccessListFixedLocationToken);
                }
                catch (Exception exc)
                {
                    LogService?.Log.Error(exc, "SaveLocationService - Failed to load fixed save location from FutureAccessList");
                    try
                    {
                        fixedSaveLocation = await KnownFolders.PicturesLibrary.CreateFolderAsync
                            (GetDefaultScanFolderName(), CreationCollisionOption.OpenIfExists);
                    }
                    catch (Exception exc2)
                    {
                        isFixedSaveLocationSupported = false;
                        SettingsService.SettingSaveLocationType = SettingSaveLocationType.AskBeforeNewProject;
                        LogService?.Log.Error(exc2, "SaveLocationService - Fixed save location is not supported");
                        SentryService?.TrackError(exc2);
                    }
                }

                // recent folders may be cached
                for (int i = 0; i < recentFoldersLimit; i++)
                {
                    string token = $"{futureAccessListRecentFoldersToken}{i}";
                    try
                    {
                        if (futureAccessList.ContainsItem(token))
                        {
                            recentFolders.Add(await futureAccessList.GetFolderAsync(token));
                        }
                    }
                    catch (Exception)
                    {
                        // folder probably deleted
                        futureAccessList.Remove(token);
                    }
                }
            }
            else
            {
                // no folders saved ~> save location needs to be acquired
                await TryResetSaveLocationInternalAsync(false);
            }

            initializationCompleted.TrySetResult();
        }

        public async Task<SaveOptions?> GetSaveOptionsAsync(Window window, ScanOptions scanOptions, ProjectBase? existingProject,
            bool forceTargetFolder, DispatcherQueue uiDispatcherQueue, bool forceDialog = false, string? desiredFileDisplayName = null)
        {
            // generate default file name
            string fileName;
            switch (SettingsService.SettingFileNamingPattern)
            {
                case SettingFileNamingPattern.DateTime:
                default:
                    fileName = ItemNamingStatics.FileDateTimePattern.GenerateResult(scanOptions, true);
                    break;
                case SettingFileNamingPattern.Date:
                    fileName = ItemNamingStatics.FileDatePattern.GenerateResult(scanOptions, true);
                    break;
                case SettingFileNamingPattern.Custom:
                    fileName = SettingsService.CustomFileNamingPattern.GenerateResult(scanOptions, true);
                    break;
            }

            // show save dialog if forced
            if (forceDialog)
            {
                SaveOptions? result = await Messenger.Send(new ShowSaveOptionsDialogMessage(scanOptions, existingProject, desiredFileDisplayName)).Response;
                if (result?.TargetFolder != null)
                    TrackRecentlyUsedFolder(result.TargetFolder);
                return result;
            }

            // get sub folder name
            string? subFolderName = null;
            switch (SettingsService.SettingSubFolderNamingPattern)
            {
                case SettingSubFolderNamingPattern.Date:
                    subFolderName = ItemNamingStatics.FolderDatePattern.GenerateResult(scanOptions, false);
                    break;
                case SettingSubFolderNamingPattern.FileType:
                    subFolderName = ItemNamingStatics.FolderFileTypePattern.GenerateResult(scanOptions, false);
                    break;
                case SettingSubFolderNamingPattern.Custom:
                    subFolderName = SettingsService.CustomSubFolderNamingPattern.GenerateResult(scanOptions, false);
                    break;
            }

            // determine final file name
            switch (SettingsService.SettingSaveLocationType)
            {
                case SettingSaveLocationType.FixedLocation:
                    await initializationCompleted.Task;
                    TrackRecentlyUsedFolder(fixedSaveLocation!);

                    if (scanOptions.TargetFormat == TargetFormat.PDF)
                        return new SaveOptions(fixedSaveLocation!, subFolderName, fileName, SettingsService.SettingGenerateFileNameWithAI);
                    else
                        return new SaveOptions(fixedSaveLocation!, subFolderName, fileName, false);

                case SettingSaveLocationType.AskBeforeNewProject:
                    if (existingProject != null)
                    {
                        if (existingProject is PdfProject pdfProject)
                            return new SaveOptions(pdfProject.TargetFolder!, subFolderName, fileName, false);
                        else if (existingProject.Pages[0] is ImagePage imagePage)
                            return new SaveOptions(imagePage.TargetFolder, subFolderName, fileName, false);
                    }

                    // ask user for location
                    SaveOptions? result = await Messenger.Send(new ShowSaveOptionsDialogMessage(scanOptions, existingProject, null)).Response;
                    if (result?.TargetFolder != null)
                        TrackRecentlyUsedFolder(result.TargetFolder);
                    return result;

                case SettingSaveLocationType.AskAfterNewProject:
                    if (existingProject != null)
                    {
                        if (existingProject is PdfProject pdfProject)
                        {
                            if (!forceTargetFolder || pdfProject.TargetFolder != null)
                                return new SaveOptions(pdfProject.TargetFolder, subFolderName, fileName, false);
                        }
                        else if (existingProject.Pages[0] is ImagePage imagePage)
                        {
                            if (!forceTargetFolder || imagePage.TargetFolder != null)
                                return new SaveOptions(imagePage.TargetFolder, subFolderName, fileName, false);
                        }
                    }

                    if (forceTargetFolder)
                    {
                        // get base file display name
                        string? baseFileDisplayName = null;
                        if (existingProject is PdfProject pdfProject)
                            baseFileDisplayName = pdfProject.FileNameInfo.DesiredDisplayName;
                        else if (existingProject is MultiFileProject imageProject && imageProject.Pages[0] is ImagePage imagePage)
                            baseFileDisplayName = imagePage.FileNameInfo?.DesiredDisplayName;                            

                        // ask user for location
                        result = await Messenger.Send(new ShowSaveOptionsDialogMessage(scanOptions, existingProject, baseFileDisplayName)).Response;
                        if (result?.TargetFolder != null)
                            TrackRecentlyUsedFolder(result.TargetFolder);
                        return result;
                    }
                    else
                    {
                        if (scanOptions.TargetFormat == TargetFormat.PDF)
                        {
                            return new SaveOptions(null, subFolderName, fileName, SettingsService.SettingGenerateFileNameWithAI);
                        }
                        else
                        {
                            return new SaveOptions(null, subFolderName, fileName, false);
                        }
                    }

                case SettingSaveLocationType.AskEveryTime:
                    // ask user for location
                    result = await Messenger.Send(new ShowSaveOptionsDialogMessage(scanOptions, existingProject, null)).Response;
                    if (result?.TargetFolder != null)
                        TrackRecentlyUsedFolder(result.TargetFolder);
                    return result;

                default:
                    throw new ArgumentException("Invalid save location type");
            }
        }

        public async Task<bool> GetIsFixedSaveLocationSupportedAsync()
        {
            await initializationCompleted.Task;
            return isFixedSaveLocationSupported;
        }

        public async Task<StorageFolder?> TryResetSaveLocationAsync()
        {
            return await TryResetSaveLocationInternalAsync();
        }

        private async Task<StorageFolder?> TryResetSaveLocationInternalAsync(bool awaitInitialization = true)
        {
            if (!isFixedSaveLocationSupported) return null;
            if (awaitInitialization) await initializationCompleted.Task;

            try
            {
                fixedSaveLocation = await KnownFolders.PicturesLibrary.CreateFolderAsync
                    (GetDefaultScanFolderName(), CreationCollisionOption.OpenIfExists);
            }
            catch (UnauthorizedAccessException exc)
            {
                isFixedSaveLocationSupported = false;
                SettingsService.SettingSaveLocationType = SettingSaveLocationType.AskBeforeNewProject;
                LogService?.Log.Error(exc, "SaveLocationService - Resetting fixed save location failed (Unauthorized)");
                SentryService?.TrackError(exc);
                Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                {
                    Title = GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ErrorMessageResetFolderUnauthorizedHeading),
                    Message = GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ErrorMessageResetFolderUnauthorizedBody),
                    Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error
                }));
            }
            catch (Exception exc)
            {
                isFixedSaveLocationSupported = false;
                SettingsService.SettingSaveLocationType = SettingSaveLocationType.AskBeforeNewProject;
                LogService?.Log.Error(exc, "SaveLocationService - Resetting fixed save location failed");
                SentryService?.TrackError(exc);
                Messenger.Send(new ShowNotificationMessage(new CommunityToolkit.WinUI.Behaviors.Notification
                {
                    Title = GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ErrorMessageHeading),
                    Message = GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ErrorMessageResetFolderBody),
                    Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error
                }));
            }

            if (fixedSaveLocation != null)
            {
                futureAccessList.AddOrReplace(futureAccessListFixedLocationToken, fixedSaveLocation);
            }
            return fixedSaveLocation;
        }

        public async Task<StorageFolder?> SelectFixedSaveLocationAsync(DispatcherQueue uiDispatcherQueue, Window window)
        {
            await initializationCompleted.Task;

            // construct folder picker
            FolderPicker picker = new()
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, window.GetWindowHandle());

            // pick folder
            StorageFolder? folder = null;
            await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Normal, async () =>
            {
                folder = await picker.PickSingleFolderAsync();
            });

            // process selection
            if (folder != null)
            {
                fixedSaveLocation = folder;
                futureAccessList.AddOrReplace(futureAccessListFixedLocationToken, folder);
            }

            return fixedSaveLocation;
        }

        private string GetDefaultScanFolderName()
        {
            string defaultScanFolderName = GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.DefaultScanFolderName);
            bool validName = true;

            // safeguard against localization mistakes
            foreach (char character in defaultScanFolderName.ToCharArray())
            {
                if (!char.IsLetter(character))
                {
                    validName = false;
                    break;
                }
            }

            if (string.IsNullOrEmpty(defaultScanFolderName) || validName == false)
            {
                // use fallback name if there is an issue with the localization
                SentryService?.TrackError(new ApplicationException($"The localized scan folder " +
                    $"name '{defaultScanFolderName}' is invalid, using 'Scans' instead."));
                defaultScanFolderName = "Scans";
            }

            return defaultScanFolderName;
        }

        public async Task<StorageFolder?> GetFixedSaveLocationAsync()
        {
            await initializationCompleted.Task;
            return fixedSaveLocation;
        }

        public async Task<List<StorageFolder>> GetRecentFoldersAsync()
        {
            await initializationCompleted.Task;
            return new List<StorageFolder>(recentFolders);
        }

        private void TrackRecentlyUsedFolder(StorageFolder folder)
        {
            // remove existing entry
            recentFolders.Remove(folder);

            // add entry to front
            recentFolders.Insert(0, folder);

            // update FutureAccessList
            for (int i = 0; i < recentFoldersLimit; i++)
            {
                if (i < recentFolders.Count)
                {
                    // write entry to FutureAccessList
                    futureAccessList.AddOrReplace($"{futureAccessListRecentFoldersToken}{i}", recentFolders[i]);
                }
                else if (futureAccessList.ContainsItem($"{futureAccessListRecentFoldersToken}{i}"))
                {
                    // no more actual entries ~> clear in FutureAccessList
                    futureAccessList.Remove($"{futureAccessListRecentFoldersToken}{i}");
                }
            };
        }
    }
}
