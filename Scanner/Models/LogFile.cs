using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Storage;

namespace Scanner.Models
{
    public class LogFile : INotifyPropertyChanged
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public event PropertyChangedEventHandler PropertyChanged;

        private StorageFile _File;
        public StorageFile File
        {
            get => _File;
            set
            {
                _File = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(_File)));
            }
        }

        private string _FileSize;
        public string FileSize
        {
            get => _FileSize;
            set
            {
                _FileSize = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(_FileSize)));
            }
        }

        private DateTimeOffset _LastModified;
        public DateTimeOffset LastModified
        {
            get => _LastModified;
            set
            {
                _LastModified = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(_LastModified)));
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public LogFile(StorageFile file)
        {
            File = file;
        }

        public static async Task<LogFile> CreateLogFile(StorageFile file)
        {
            LogFile logFile = new LogFile(file);
            var properties = await file.GetBasicPropertiesAsync();
            logFile.FileSize = Math.Ceiling((double)properties.Size / 1000).ToString() + " KB";
            logFile.LastModified = properties.DateModified;

            return logFile;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    }
}
