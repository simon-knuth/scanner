using Microsoft.Data.Sqlite;
using Scanner.Models;
using System;
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
        private const string dbFolderName = "db";
        private const string dbFileName = "scanoptions.db";
        private const string tableName = "SCAN_OPTIONS";

        private SqliteConnection Connection;

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanOptionsDatabaseService()
        {
            // get database folder
            StorageFolder folder = Task.Run(async () =>
                await ApplicationData.Current.LocalFolder.CreateFolderAsync(dbFolderName,
                    CreationCollisionOption.OpenIfExists)).Result;

            // create database file, if necessary
            StorageFile file = Task.Run(async () =>
                await folder.CreateFileAsync(dbFileName, CreationCollisionOption.OpenIfExists)).Result;

            // configure database
            SqliteConnection db = new SqliteConnection(String.Format("Filename={0}", file.Path));
            db.Open();

            String tableCommand = "CREATE TABLE IF NOT EXISTS " + tableName.ToUpper() + " ("
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
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanOptions GetScanOptionsForScanner(DiscoveredScanner scanner)
        {
            // check if just debug scanner
            if (scanner.Debug) return null;

            // read database
            string id = scanner.Id;

            Connection.Open();
            SqliteCommand selectCommand = new SqliteCommand
                ("SELECT * FROM " + tableName.ToUpper() + " WHERE ID = \"" + id + "\"", Connection);
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
                        result.FeederMultiplePages = bool.Parse(query.GetString(4));
                        result.FeederDuplex = bool.Parse(query.GetString(5));
                        break;
                    case ScannerSource.None:
                    case ScannerSource.Auto:
                    default:
                        break;
                }
            }

            Connection.Close();

            return result;
        }
    }
}
