using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using static Scanner.Services.SettingsEnums;
using static Utilities;

namespace Scanner.Services
{
    class AppCenterService : IAppCenterService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        private ILogService LogService = Ioc.Default.GetService<ILogService>();

        private bool _IsAppCenterAllowed;
        private bool IsAppCenterAllowed
        {
            get => _IsAppCenterAllowed;
            set
            {
                if (value == true && _IsAppCenterAllowed == false)
                {
                    // AppCenter needs to be activated
                    AppCenter.SetEnabledAsync(true);
                }
                else if (value == false && _IsAppCenterAllowed == true)
                {
                    // AppCenter needs to be deactivated
                    AppCenter.SetEnabledAsync(false);
                }
                _IsAppCenterAllowed = value;
            }
        }

        private readonly Dictionary<AppCenterEvent, string> EventStrings = new Dictionary<AppCenterEvent, string>
        {
            { AppCenterEvent.ScannerAdded, "Scanner added" },
            { AppCenterEvent.ScanCompleted, "Scan completed" },
            { AppCenterEvent.Share, "Share" },
            { AppCenterEvent.Preview, "Preview" },
            { AppCenterEvent.RotatePages, "Rotate pages" },
            { AppCenterEvent.RenamePage, "Rename page" },
            { AppCenterEvent.RenamePDF, "Rename PDF" },
            { AppCenterEvent.Crop, "Crop" },
            { AppCenterEvent.CropAsCopy, "Crop as copy" },
            { AppCenterEvent.DeletePages, "Delete pages" },
            { AppCenterEvent.DeletePage, "Delete page" },
            { AppCenterEvent.DrawOnPage, "Draw on page" },
            { AppCenterEvent.DrawOnPageAsCopy, "Draw on page as copy" },
            { AppCenterEvent.CopyPages, "Copy pages" },
            { AppCenterEvent.CopyPage, "Copy page" },
            { AppCenterEvent.CopyDocument, "Copy document" },
            { AppCenterEvent.OpenWith, "Open with" },
        };

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public AppCenterService()
        {
            // prepare service
            Task.Run(async () => await AppCenter.SetEnabledAsync(false));
            Crashes.GetErrorAttachments = (report) => CreateErrorAttachmentAsync(report).Result;
            AppCenter.Start(GetSecret("SecretAppCenter"), typeof(Analytics), typeof(Crashes));

            // get whether AppCenter is even allowed
            SettingsService.SettingChanged += SettingsService_SettingChanged;
            IsAppCenterAllowed = (bool)SettingsService.GetSetting(AppSetting.SettingErrorStatistics);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Refreshes the AppCenter status when the user toggles AppCenter on or off.
        /// </summary>
        private void SettingsService_SettingChanged(object sender, AppSetting e)
        {
            if (e == AppSetting.SettingErrorStatistics)
            {
                IsAppCenterAllowed = (bool)SettingsService.GetSetting(AppSetting.SettingErrorStatistics);
            }
        }

        /// <summary>
        ///     Returns an <see cref="ErrorAttachmentLog"/> that includes the relevant log file
        ///     for the given <paramref name="report"/>.
        /// </summary>
        private async Task<ErrorAttachmentLog[]> CreateErrorAttachmentAsync(ErrorReport report)
        {
            // check whether LogService is available
            if (LogService == null)
            {
                return new ErrorAttachmentLog[]
                {
                    ErrorAttachmentLog.AttachmentWithText("LogService unavailable.", "nolog.txt")
                };
            }

            // attempt to append log to the ErrorReport
            try
            {
                // close log file
                Log.CloseAndFlush();
                await InitializeSerilogAsync();

                // get all logs
                StorageFolder logFolder = await ApplicationData.Current.RoamingFolder
                    .GetFolderAsync(LogService.LogFolder);
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
                    if (log.DateCreated <= report.AppErrorTime)
                    {
                        IBuffer buffer = await FileIO.ReadBufferAsync(log);
                        return new ErrorAttachmentLog[]
                        {
                        ErrorAttachmentLog.AttachmentWithBinary(buffer.ToArray(), "log.json",
                            "application/json")
                        };
                    }
                }
            }
            catch (Exception exc)
            {
                return new ErrorAttachmentLog[]
                {
                    ErrorAttachmentLog.AttachmentWithText("Failed to append log. (" + exc.Message + ")",
                        "nolog.txt")
                };
            }

            return new ErrorAttachmentLog[]
            {
                ErrorAttachmentLog.AttachmentWithText("Failed to append log.", "nolog.txt")
            };
        }

        /// <summary>
        ///     Track an <see cref="AppCenterEvent"/> in AppCenter.
        /// </summary>
        public void TrackEvent(AppCenterEvent appCenterEvent, IDictionary<string, string> properties = null)
        {
            Analytics.TrackEvent(EventStrings[appCenterEvent], properties);
        }

        /// <summary>
        ///     Track an Error in AppCenter.
        /// </summary>
        public void TrackError(Exception exception, IDictionary<string, string> properties = null, params ErrorAttachmentLog[] attachments)
        {
            Crashes.TrackError(exception, properties, attachments);
        }
    }

    public enum AppCenterEvent
    {
        ScannerAdded,
        ScanCompleted,
        Share,
        Preview,
        RotatePages,
        RenamePage,
        RenamePDF,
        Crop,
        CropAsCopy,
        DeletePages,
        DeletePage,
        DrawOnPage,
        DrawOnPageAsCopy,
        CopyPages,
        CopyPage,
        CopyDocument,
        OpenWith,
    }
}
