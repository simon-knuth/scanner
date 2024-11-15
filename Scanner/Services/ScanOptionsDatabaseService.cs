using Microsoft.Data.Sqlite;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Scanner.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Storage;
using static Enums;

namespace Scanner.Services
{
    /// <summary>
    ///     Manages remembered <see cref="ScanOptions"/> on a per-scanner basis.
    /// </summary>
    internal class ScanOptionsDatabaseService : IScanOptionsDatabaseService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        public readonly ILogService LogService = Ioc.Default.GetService<ILogService>();
        private readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();

        private const string DbFolderName = "db";
        private const string DbFileName = "scanoptions.db";
        private const string TableName = "SCAN_OPTIONS";

        private SqliteConnection Connection;

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanOptionsDatabaseService()
        {
            SettingsService.SettingChanged += SettingsService_SettingChanged;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Initializes the database.
        /// </summary>
        public async Task InitializeAsync()
        {
            // get database folder
            StorageFolder folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(DbFolderName,
                    CreationCollisionOption.OpenIfExists);

            // create database file, if necessary
            await folder.CreateFileAsync(DbFileName, CreationCollisionOption.OpenIfExists);

            // configure database
            string path = Path.Combine(folder.Path, DbFileName);
            SqliteConnection db = new SqliteConnection(String.Format("Filename={0}", path));
            db.Open();

            string tableCommand = "CREATE TABLE IF NOT EXISTS " + TableName.ToUpper() + " ("
                + "ID STRING PRIMARY KEY,"      // 0
                + "SOURCE_MODE INTEGER,"        // 1
                + "COLOR_MODE INTEGER,"         // 2
                + "RESOLUTION INTEGER,"         // 3
                + "MULTIPLE_PAGES BOOLEAN,"     // 4
                + "DUPLEX BOOLEAN,"             // 5
                + "FILE_FORMAT INTEGER,"        // 6
                + "AUTO_CROP_MODE INTEGER"      // 7
                + ")";

            SqliteCommand createTable = new SqliteCommand(tableCommand, db);
            createTable.ExecuteReader();

            db.Close();

            Connection = db;

            LogService?.Log.Information("ScanOptionsDatabaseService initialized");
        }


        private void SettingsService_SettingChanged(object sender, AppSetting e)
        {
            if (e == AppSetting.SettingRememberScanOptions
                && !(bool)SettingsService.GetSetting(AppSetting.SettingRememberScanOptions))
            {
                // database disabled, clear all values
                try
                {
                    // prepare command
                    Connection.Open();
                    SqliteCommand clearCommand = new SqliteCommand
                        ($"DELETE FROM {TableName.ToUpper()}", Connection);

                    // execute
                    clearCommand.ExecuteReader();

                    Connection.Close();
                }
                catch (Exception exc)
                {
                    AppCenterService?.TrackError(exc);
                    LogService.Log.Error(exc, "Clearing database failed.");
                }
            }
        }

        /// <summary>
        ///     Gets <see cref="ScanOptions"/> for the given <paramref name="scanner"/>.
        /// </summary>
        public ScanOptions GetScanOptionsForScanner(DiscoveredScanner scanner)
        {
            try
            {
                string id;
                if (scanner.Debug) id = "DEBUG";
                else id = scanner.Id;

                Connection.Open();
                SqliteCommand selectCommand = new SqliteCommand
                    ($"SELECT * FROM {TableName.ToUpper()} WHERE ID = \"{id}\"", Connection);
                SqliteDataReader query = selectCommand.ExecuteReader();

                // check if data for this scanner even exists
                if (!query.HasRows) return null;

                // load data
                ScanOptions result = new ScanOptions();
                while (query.Read())
                {
                    // common properties
                    result.Source = (ScannerSource)int.Parse(query.GetString(1));
                    result.ColorMode = (ScannerColorMode)int.Parse(query.GetString(2));
                    result.Format = new ScannerFileFormat((ImageScannerFormat)int.Parse(query.GetString(6)));

                    // source mode-dependent properties
                    switch (result.Source)
                    {
                        case ScannerSource.Flatbed:
                            result.Resolution = float.Parse(query.GetString(3));
                            result.AutoCropMode = (ScannerAutoCropMode)int.Parse(query.GetString(7));
                            break;
                        case ScannerSource.Feeder:
                            result.Resolution = float.Parse(query.GetString(3));
                            result.FeederMultiplePages = Convert.ToBoolean(int.Parse(query.GetString(4)));
                            result.FeederDuplex = Convert.ToBoolean(int.Parse(query.GetString(5)));
                            result.AutoCropMode = (ScannerAutoCropMode)int.Parse(query.GetString(7));
                            break;
                        case ScannerSource.None:
                        case ScannerSource.Auto:
                        default:
                            break;
                    }
                }

                Connection.Close();

                LogService?.Log.Information("GetScanOptionsForScanner: Returning {@Result} for {Id}", result, scanner.Id);
                return result;
            }
            catch (Exception exc)
            {
                AppCenterService?.TrackError(exc);
                LogService.Log.Error(exc, "Getting scan options for {Id} failed", scanner.Id);
                return null;
            }
        }

        /// <summary>
        ///     Saves the <paramref name="scanOptions"/> for the given <paramref name="scanner"/>.
        /// </summary>
        public void SaveScanOptionsForScanner(DiscoveredScanner scanner, ScanOptions scanOptions)
        {
            try
            {
                string id;
                if (scanner.Debug) id = "DEBUG";
                else id = scanner.Id;

                // prepare command
                Connection.Open();
                SqliteCommand upsertCommand = new SqliteCommand
                    ($"INSERT INTO {TableName.ToUpper()} VALUES" +
                    $"(\"{id}\", {(int)scanOptions.Source}, {(int)scanOptions.ColorMode}, {(int)scanOptions.Resolution}, " +
                    $"{scanOptions.FeederMultiplePages}, {scanOptions.FeederDuplex}, {(int)scanOptions.Format.TargetFormat}, " +
                    $"{(int)scanOptions.AutoCropMode}) " +
                    $"ON CONFLICT(id) DO UPDATE SET source_mode={(int)scanOptions.Source}, color_mode={(int)scanOptions.ColorMode}, " +
                    $"resolution={(int)scanOptions.Resolution}, multiple_pages={scanOptions.FeederMultiplePages}, " +
                    $"duplex={scanOptions.FeederDuplex}, file_format={(int)scanOptions.Format.TargetFormat}, " +
                    $"auto_crop_mode={(int)scanOptions.AutoCropMode}", Connection);

                // execute
                upsertCommand.ExecuteReader();

                Connection.Close();

                LogService?.Log.Information("SaveScanOptionsForScanner: Saved scan options");
            }
            catch (Exception exc)
            {
                AppCenterService?.TrackError(exc);
                LogService.Log.Error(exc, "Saving scan options for scanner failed.");
            }
        }

        /// <summary>
        ///     Deletes the saved <see cref="ScanOptions"/> for the given <paramref name="scanner"/>, if any
        ///     exist.
        /// </summary>
        public void DeleteScanOptionsForScanner(DiscoveredScanner scanner)
        {
            try
            {
                string id;
                if (scanner.Debug) id = "DEBUG";
                else id = scanner.Id;

                // prepare command
                Connection.Open();
                SqliteCommand deleteCommand = new SqliteCommand
                    ($"DELETE FROM {TableName.ToUpper()} WHERE id = \"{id}\"", Connection);

                // execute
                deleteCommand.ExecuteReader();

                Connection.Close();
                LogService?.Log.Information("DeleteScanOptionsForScanner: Deleted scan options");
            }
            catch (Exception exc)
            {
                LogService.Log.Warning(exc, "Deleting scan options for scanner failed.");
                return;
            }
        }
    }
}
