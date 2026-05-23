using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using Serilog;
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

    private CaptureFilePathHook hook;


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
    ///     Close the current log file.
    /// </summary>
    public void CloseAndFlush()
    {
        Serilog.Log.CloseAndFlush();
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

        // prepare hook
        hook = new CaptureFilePathHook();
        hook.FilePathChanged += Hook_FilePathChanged;

        ILogger log;
        log = new LoggerConfiguration()
                .MinimumLevel.Is(Serilog.Events.LogEventLevel.Information)
                .WriteTo.Async(a => a.File(
                    path: logPath,
                    outputTemplate: "{Timestamp:yy-MM-dd HH:mm:ss} [{Level:u3}] [{Caller}] {Message:lj}{NewLine}{Exception}",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 8,
                    fileSizeLimitBytes: 500000,
                    rollOnFileSizeLimit: true,
                    hooks: hook))
                .Enrich.WithExceptionDetails()
                .CreateLogger();

        log.Information("--- Log initialized ---");
        log.Information("App version: {AppVersion}", Helpers.Helpers.GetCurrentVersion());
        log.Information("Windows version: {WindowsVersion}", GetWindowsVersion());
        log.Information("System architecture: {SystemArchitecture}", RuntimeInformation.OSArchitecture);
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

    private void Hook_FilePathChanged(object? sender, string e)
    {
        if (sender == null) return;
        LogFilePathChanged?.Invoke(this, ((CaptureFilePathHook)sender).Path);
    }

    public async Task<List<LogFile>> GetLogFilesAsync()
    {
        // flush log
        Serilog.Log.CloseAndFlush();

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

        await InitializeAsync();

        return sortedFiles;
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
            return base.OnFileOpened(path, underlyingStream, encoding);
        }
    }
}
