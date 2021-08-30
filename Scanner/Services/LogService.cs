using Microsoft.Toolkit.Uwp.Helpers;
using Serilog;
using Serilog.Exceptions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using static Utilities;

namespace Scanner.Services
{
    class LogService : ILogService
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
            Serilog.Debugging.SelfLog.Enable(msg => System.Diagnostics.Debug.WriteLine(msg));

            StorageFolder folder = Task.Run(async () => await ApplicationData.Current.RoamingFolder
                .CreateFolderAsync(LogFolder, CreationCollisionOption.OpenIfExists)).Result;
            string logPath = Path.Combine(folder.Path, "log.txt");

            ILogger log;
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
            log.Information("App version: {0}", GetCurrentVersion());
            log.Information("OS: {OS} | OS version: {Version} | OS architecture: {Architecture}",
                SystemInformation.Instance.OperatingSystem, SystemInformation.Instance.OperatingSystemVersion,
                SystemInformation.Instance.OperatingSystemArchitecture);
            log.Information("Device family: {Family} | Device model: {Model} | Device manufacturer: {Manufacturer}",
                SystemInformation.Instance.DeviceFamily, SystemInformation.Instance.DeviceModel,
                SystemInformation.Instance.DeviceManufacturer);

            _Log = log;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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

            await InitializeSerilogAsync();

            return sortedFiles;
        }
    }
}
