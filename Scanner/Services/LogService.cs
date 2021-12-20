using Microsoft.Toolkit.Uwp.Helpers;
using Scanner.Models;
using Serilog;
using Serilog.Exceptions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Storage;
using static Utilities;

namespace Scanner.Services
{
    internal class LogService : ILogService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private ILogger _Log;
        public ILogger Log
        {
            get => _Log;
        }

        public string LogFolder => "logs";


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
        ///     Gets all existing log files.
        /// </summary>
        /// <remarks>
        ///     This causes the current log file to be closed and a new one to be created.
        /// </remarks>
        public async Task<List<Models.LogFile>> GetLogFiles()
        {
            // flush log
            Serilog.Log.CloseAndFlush();

            // populate file list
            StorageFolder logFolder = await ApplicationData.Current.RoamingFolder.GetFolderAsync(LogFolder);
            var files = await logFolder.GetFilesAsync();

            List<Models.LogFile> sortedFiles = new List<Models.LogFile>();
            foreach (var file in files)
            {
                sortedFiles.Add(await Models.LogFile.CreateLogFile(file));
            }
            sortedFiles.Sort(delegate (Models.LogFile x, Models.LogFile y)
            {
                return DateTimeOffset.Compare(x.LastModified, y.LastModified);
            });
            sortedFiles.Reverse();

            await InitializeAsync();

            return sortedFiles;
        }

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
            Serilog.Debugging.SelfLog.Enable(msg => System.Diagnostics.Debug.WriteLine(msg));

            StorageFolder folder = await ApplicationData.Current.RoamingFolder
                .CreateFolderAsync("logs", CreationCollisionOption.OpenIfExists);
            string logPath = Path.Combine(folder.Path, "log.txt");

            ILogger log;
            log = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Async(a => a.File(
                        path: logPath,
                        formatter: new Serilog.Formatting.Json.JsonFormatter(),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 8,
                        fileSizeLimitBytes: 6900000))       // Microsoft App Center supports attachments up to 7 MB
                    .Enrich.WithExceptionDetails()
                    .Destructure.ByTransforming<ScanOptions>(
                        o => new
                        {
                            Source = o.Source,
                            ColorMode = o.ColorMode,
                            Resolution = o.Resolution,
                            AutoCropMode = o.AutoCropMode,
                            FeederMultiplePages = o.FeederMultiplePages,
                            FeederDuplex = o.FeederDuplex,
                            Format = o.Format,
                            Brightness = o.Brightness,
                            Contrast = o.Brightness
                        })
                    .Destructure.ByTransforming<DeviceInformation>(
                        i => new { Name = i.Name, IsEnabled = i.IsEnabled, IsDefault = i.IsDefault, Kind = i.Kind, Id = i.Id })
                    .Destructure.ByTransforming<ScannerFileFormat>(
                        f => new
                        {
                            TargetFormat = f.TargetFormat,
                            OriginalFormat = f.OriginalFormat,
                            RequiresConversion = f.RequiresConversion
                        })
                    .Destructure.ByTransforming<DiscoveredScanner>(
                        s => new
                        {
                            Id = s.Id,
                            Name = s.Name,
                            IsAutoAllowed = s.IsAutoAllowed,
                            IsAutoPreviewAllowed = s.IsAutoPreviewAllowed,
                            AutoFormats = s.AutoFormats,
                            IsFlatbedAllowed = s.IsFlatbedAllowed,
                            IsFlatbedColorAllowed = s.IsFlatbedColorAllowed,
                            IsFlatbedGrayscaleAllowed = s.IsFlatbedGrayscaleAllowed,
                            IsFlatbedMonochromeAllowed = s.IsFlatbedMonochromeAllowed,
                            IsFlatbedAutoColorAllowed = s.IsFlatbedAutoColorAllowed,
                            IsFlatbedPreviewAllowed = s.IsFlatbedPreviewAllowed,
                            IsFlatbedAutoCropSingleRegionAllowed = s.IsFlatbedAutoCropSingleRegionAllowed,
                            IsFlatbedAutoCropMultiRegionAllowed = s.IsFlatbedAutoCropMultiRegionAllowed,
                            IsFlatbedAutoCropPossible = s.IsFlatbedAutoCropPossible,
                            FlatbedResolutions = s.FlatbedResolutions,
                            FlatbedFormats = s.FlatbedFormats,
                            FlatbedBrightnessConfig = s.FlatbedBrightnessConfig,
                            FlatbedContrastConfig = s.FlatbedContrastConfig,
                            IsFeederAllowed = s.IsFeederAllowed,
                            IsFeederColorAllowed = s.IsFeederColorAllowed,
                            IsFeederGrayscaleAllowed = s.IsFeederGrayscaleAllowed,
                            IsFeederMonochromeAllowed = s.IsFeederMonochromeAllowed,
                            IsFeederAutoColorAllowed = s.IsFeederAutoColorAllowed,
                            IsFeederDuplexAllowed = s.IsFeederDuplexAllowed,
                            IsFeederPreviewAllowed = s.IsFeederPreviewAllowed,
                            IsFeederAutoCropSingleRegionAllowed = s.IsFeederAutoCropSingleRegionAllowed,
                            IsFeederAutoCropMultiRegionAllowed = s.IsFeederAutoCropMultiRegionAllowed,
                            IsFeederAutoCropPossible = s.IsFeederAutoCropPossible,
                            FeederResolutions = s.FeederResolutions,
                            FeederFormats = s.FeederFormats,
                            FeederBrightnessConfig = s.FeederBrightnessConfig,
                            FeederContrastConfig = s.FeederContrastConfig
                        })
                    .Destructure.ByTransforming<ScanResolution>(
                        r => new
                        {
                            Resolution = r.Resolution.DpiX,
                            Annotation = r.Annotation
                        })
                    .CreateLogger();

            log.Information("--- Log initialized ---");

            // add meta data
            log.Information("App version: {0}", GetCurrentVersion());
            log.Information("OS: {OS} | OS version: {Version} | OS architecture: {Architecture} | OS language: {Language}",
                SystemInformation.Instance.OperatingSystem, SystemInformation.Instance.OperatingSystemVersion,
                SystemInformation.Instance.OperatingSystemArchitecture, CultureInfo.InstalledUICulture.Name);
            log.Information("Device family: {Family} | Device model: {Model} | Device manufacturer: {Manufacturer}",
                SystemInformation.Instance.DeviceFamily, SystemInformation.Instance.DeviceModel,
                SystemInformation.Instance.DeviceManufacturer);

            _Log = log;
        }
    }
}
