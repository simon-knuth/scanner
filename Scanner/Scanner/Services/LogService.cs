using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Sinks.File;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Enumeration;
using Windows.Storage;
using Windows.System;
using Windows.System.Profile;
using WinRT.Interop;

namespace Scanner.Services;

internal partial class LogService : ILogService
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Events
    public event EventHandler<string> LogFilePathChanged;
    #endregion

    private const string logFolderName = "logs";

    // A level above Fatal, which no log entry reaches.
    private const LogEventLevel levelOff = LogEventLevel.Fatal + 1;

    public CallerLogger Log
    {
        get;
        private set;
    }

    public StorageFolder LogFolder
    {
        get;
        private set;
    }

    public string LogFilePath => hook.Path;

    private Serilog.Core.Logger? logger;
    private CaptureFilePathHook hook;
    private LogTailSink? sentryLogTail;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public LogService()
    {

    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    ///     Writes all pending entries to the current log file and closes it. Entries logged afterwards are dropped until
    ///     <see cref="InitializeAsync"/> is called again.
    /// </summary>
    public void CloseAndFlush()
    {
        Interlocked.Exchange(ref logger, null)?.Dispose();
    }

    /// <summary>
    ///     Initializes <see cref="log"/> to a file sink in folder "logs" within the app's RoamingFolder. Also adds
    ///     some meta data to the log.
    /// </summary>
    public async Task InitializeAsync()
    {
        LogFolder = await ApplicationData.Current.LocalCacheFolder
            .CreateFolderAsync(logFolderName, CreationCollisionOption.OpenIfExists);
        string logPath = Path.Combine(LogFolder.Path, "log.log");

        // close a logger left over from a previous initialization, so it releases the file
        CloseAndFlush();

        // prepare hook
        hook = new CaptureFilePathHook();
        hook.FilePathChanged += Hook_FilePathChanged;

        // Sentry Logs only get the latest entries, sent along with a diagnostic event, see SendLogTailToSentry. The tail
        // is created once and outlives CloseAndFlush, as the crash handlers close the log before they report the crash.
        if (sentryLogTail == null)
        {
            ILogger sentryLogger = new LoggerConfiguration()
                    .MinimumLevel.Verbose()     // the main logger below decides the level
                    .WriteTo.Sentry(options =>
                    {
                        options.InitializeSdk = false;              // SentryService initializes the SDK
                        options.MinimumBreadcrumbLevel = levelOff;  // structured logs only
                        options.MinimumEventLevel = levelOff;
                    })
                    .CreateLogger();
            sentryLogTail = new LogTailSink(sentryLogger, AppConfig.SentryLogTailLength);
        }

        Serilog.Core.Logger log = new LoggerConfiguration()
                .MinimumLevel.Is(LogEventLevel.Information)
                .WriteTo.Async(a => a.File(
                    path: logPath,
                    outputTemplate: "{Timestamp:yy-MM-dd HH:mm:ss} [{Level:u3}] [{Caller}] {Message:lj}{NewLine}{Exception}",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 8,
                    fileSizeLimitBytes: 500000,
                    rollOnFileSizeLimit: true,
                    hooks: hook))
                .WriteTo.Sink(sentryLogTail)
                .Enrich.WithExceptionDetails()
                .ConfigureCustomDestructuring()
                .CreateLogger();

        log.Information("--- Log initialized ---");
        log.Information("App version: {AppVersion}", Helpers.Helpers.GetCurrentVersion());
        log.Information("Windows version: {WindowsVersion}", GetWindowsVersion());
        log.Information("System architecture: {SystemArchitecture}", RuntimeInformation.OSArchitecture);
        logger = log;
        Log = new(log);
    }

    private static string GetWindowsVersion()
    {
        ulong versionNumber = ulong.Parse(AnalyticsInfo.VersionInfo.DeviceFamilyVersion);
        ulong major = (versionNumber & 0xFFFF000000000000L) >> 48;
        ulong minor = (versionNumber & 0x0000FFFF00000000L) >> 32;
        ulong build = (versionNumber & 0x00000000FFFF0000L) >> 16;
        ulong revision = versionNumber & 0x000000000000FFFFL;
        return $"{major}.{minor}.{build}.{revision}";
    }

    /// <summary>
    ///     Sends the latest log entries to Sentry Logs, where they show up with the event that is being sent. Each entry
    ///     is sent once at most.
    /// </summary>
    public void SendLogTailToSentry()
    {
        sentryLogTail?.Send();
    }

    private void Hook_FilePathChanged(object? sender, string e)
    {
        if (sender == null) return;
        LogFilePathChanged?.Invoke(this, ((CaptureFilePathHook)sender).Path);
    }

    public async Task<List<LogFile>> GetLogFilesAsync()
    {
        // flush log
        CloseAndFlush();

        try
        {
            // populate file list
            var files = await LogFolder.GetFilesAsync();

            List<LogFile> sortedFiles = new List<LogFile>();
            foreach (var file in files)
            {
                var properties = await file.GetBasicPropertiesAsync();
                if (properties.Size >= 1000)
                {
                    sortedFiles.Add(await LogFile.CreateLogFile(file));
                }
            }
            sortedFiles.Sort(delegate (LogFile x, LogFile y)
            {
                return DateTimeOffset.Compare(x.LastModified, y.LastModified);
            });
            sortedFiles.Reverse();

            return sortedFiles;
        }
        finally
        {
            // reopen the log, also when listing the files failed
            await InitializeAsync();
        }
    }

    public async Task OpenLatestLogFileAsync()
    {
        IReadOnlyList<StorageFile> sortedItems = await LogFolder.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.OrderByName);
        StorageFile latestLogFile = sortedItems.Last();
        await Launcher.LaunchFileAsync(latestLogFile);
    }

    public async Task OpenLogFolderAsync()
    {
        await Launcher.LaunchFolderAsync(LogFolder);
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////// 
    internal class CaptureFilePathHook : FileLifecycleHooks
    {
        public event EventHandler<string> FilePathChanged;

        public string? Path { get; private set; }

        public override Stream OnFileOpened(string path, Stream underlyingStream, Encoding encoding)
        {
            Path = path;
            FilePathChanged?.Invoke(this, path);
            return base.OnFileOpened(path, underlyingStream, encoding);
        }
    }
}

internal static class LogDestructuringExtensions
{
    public static LoggerConfiguration ConfigureCustomDestructuring(this LoggerConfiguration loggerConfiguration)
    {
        return loggerConfiguration
            // A scanner: its model name and capabilities, but not its raw device id.
            .Destructure.ByTransforming<IScanningDevice>(s => new
            {
                s.Name,
                IsAutoAllowed = s.IsAutoAllowed,
                IsFlatbedAllowed = s.IsFlatbedAllowed,
                IsFeederAllowed = s.IsFeederAllowed,
                IsFeederDuplexSupported = s.IsFeederDuplexSupported,
                FlatbedResolutionCount = s.FlatbedResolutions?.Count,
                FeederResolutionCount = s.FeederResolutions?.Count,
                Color = s.IsColorAllowedInAnyMode,
                Grayscale = s.IsGrayscaleAllowedInAnyMode,
                Monochrome = s.IsMonochromeAllowedInAnyMode,
                AutoColor = s.IsAutoColorAllowedInAnyMode,
            })
            // The options chosen for a scan.
            .Destructure.ByTransforming<ScanOptions>(o => new
            {
                o.SourceMode,
                o.TargetFormat,
                o.ColorMode,
                ResolutionDpi = o.Resolution?.Resolution.DpiX,
                o.Duplex,
                o.ScanMultiplePages,
                o.Brightness,
                o.Contrast,
                ScanArea = o.ScanArea,
                HasScanMergeConfig = o.ScanMergeConfig != null,
                HasScanner = o.Scanner != null,
            })
            // A single resolution value.
            .Destructure.ByTransforming<ScanResolution>(r => new
            {
                Dpi = r.Resolution.DpiX,
                r.Annotation,
            })
            // The configuration for merging a scan into an existing project.
            .Destructure.ByTransforming<ScanMergeConfig>(c => new
            {
                c.InsertIndices,
                c.SurplusPagesIndex,
                c.InsertReversed,
            })
            // The selected scan area (one of several concrete shapes).
            .Destructure.ByTransforming<ScanArea>(DescribeScanArea);
    }

    private static object DescribeScanArea(ScanArea area)
    {
        return area switch
        {
            AutoCropArea auto => (object)new { Type = "AutoCrop", auto.AutoCropMode },
            PaperSizeArea paper => new { Type = "PaperSize", paper.PaperSize, paper.Corner, paper.Orientation },
            PreviewSelectionArea preview => new { Type = "PreviewSelection", Region = preview.SelectedRegion.ToString() },
            _ => new { Type = area.GetType().Name },
        };
    }
}
