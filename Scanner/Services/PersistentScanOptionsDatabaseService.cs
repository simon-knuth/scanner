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
    internal class PersistentScanOptionsDatabaseService : IPersistentScanOptionsDatabaseService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        public readonly ILogService LogService = Ioc.Default.GetService<ILogService>();

        private const string DbFolderName = "db";
        private const string DbFileName = "persistentscanoptions.db";
        private const string TableName = "PERSISTENT_SCAN_OPTIONS";

        private SqliteConnection Connection;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public PersistentScanOptionsDatabaseService()
        {
            
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
                + "ID STRING PRIMARY KEY,"              // 0
                + "FLATBED_BRIGHTNESS_SET BOOLEAN,"     // 1
                + "FLATBED_BRIGHTNESS INTEGER,"         // 2
                + "FLATBED_CONTRAST_SET BOOLEAN,"       // 3
                + "FLATBED_CONTRAST INTEGER,"           // 4
                + "FEEDER_BRIGHTNESS_SET BOOLEAN,"      // 5
                + "FEEDER_BRIGHTNESS INTEGER,"          // 6
                + "FEEDER_CONTRAST_SET BOOLEAN,"        // 7
                + "FEEDER_CONTRAST INTEGER"             // 8
                + ")";

            SqliteCommand createTable = new SqliteCommand(tableCommand, db);
            createTable.ExecuteReader();

            db.Close();

            Connection = db;

            LogService?.Log.Information("PersistentScanOptionsDatabaseService initialized");
        }

        /// <summary>
        ///     Gets <see cref="PersistentScanOptions"/> for the given <paramref name="scanner"/>.
        /// </summary>
        public PersistentScanOptions GetPersistentScanOptionsForScanner(DiscoveredScanner scanner)
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
                PersistentScanOptions result = new PersistentScanOptions();
                while (query.Read())
                {
                    if (Convert.ToBoolean(int.Parse(query.GetString(1))))
                    {
                        result.FlatbedBrightness = int.Parse(query.GetString(2));
                    }

                    if (Convert.ToBoolean(int.Parse(query.GetString(3))))
                    {
                        result.FlatbedContrast = int.Parse(query.GetString(4));
                    }

                    if (Convert.ToBoolean(int.Parse(query.GetString(5))))
                    {
                        result.FeederBrightness = int.Parse(query.GetString(6));
                    }

                    if (Convert.ToBoolean(int.Parse(query.GetString(7))))
                    {
                        result.FeederContrast = int.Parse(query.GetString(8));
                    }
                }

                Connection.Close();

                LogService?.Log.Information($"GetPersistentScanOptionsForScanner: Returning scan options for {scanner.Id}");
                return result;
            }
            catch (Exception exc)
            {
                AppCenterService?.TrackError(exc);
                LogService?.Log.Error(exc, $"Getting persistent scan options for {scanner.Id} failed.");
                return null;
            }
        }

        /// <summary>
        ///     Saves the <paramref name="scanOptions"/> for the given <paramref name="scanner"/>.
        /// </summary>
        public void SavePersistentScanOptionsForScanner(DiscoveredScanner scanner, PersistentScanOptions scanOptions)
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
                    $"(\"{id}\", {scanOptions.FlatbedBrightness != null}, {scanOptions.FlatbedBrightness ?? 0}, {scanOptions.FlatbedContrast != null}, " +
                    $"{scanOptions.FlatbedContrast ?? 0}, {scanOptions.FeederBrightness != null}, {scanOptions.FeederBrightness ?? 0}, " +
                    $"{scanOptions.FeederContrast != null}, {scanOptions.FeederContrast ?? 0}) " +
                    $"ON CONFLICT(id) DO UPDATE SET flatbed_brightness_set={scanOptions.FlatbedBrightness != null}, " +
                    $"flatbed_brightness={scanOptions.FlatbedBrightness ?? 0}, " +
                    $"flatbed_contrast_set={scanOptions.FlatbedContrast != null}, " +
                    $"flatbed_contrast={scanOptions.FlatbedContrast ?? 0}, " +
                    $"feeder_brightness_set={scanOptions.FeederBrightness != null}, " +
                    $"feeder_brightness={scanOptions.FeederBrightness ?? 0}, " +
                    $"feeder_contrast_set={scanOptions.FeederContrast != null}, " +
                    $"feeder_contrast={scanOptions.FeederContrast ?? 0}", Connection);

                // execute
                upsertCommand.ExecuteReader();

                Connection.Close();

                LogService?.Log.Information("SaveScanOptionsForScanner: Saved scan options");
            }
            catch (Exception exc)
            {
                AppCenterService?.TrackError(exc);
                LogService.Log.Error(exc, "Saving persistent scan options for scanner failed.");
            }
            
        }

        /// <summary>
        ///     Deletes the saved <see cref="PersistentScanOptions"/> for the given <paramref name="scanner"/>, if any
        ///     exist.
        /// </summary>
        public void DeletePersistentScanOptionsForScanner(DiscoveredScanner scanner)
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
                LogService?.Log.Information("DeletePersistentScanOptionsForScanner: Deleted scan options");
            }
            catch (Exception exc)
            {
                LogService.Log.Warning(exc, "Deleting persistent scan options for scanner failed.");
                return;
            }
        }
    }
}
