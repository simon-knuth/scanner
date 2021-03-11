using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;


namespace Scanner
{
    class ScanResultElement : INotifyPropertyChanged
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private StorageFile _ScanFile;
        public StorageFile ScanFile
        {
            get
            {
                return _ScanFile;
            }
            set
            {
                _ScanFile = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScanFile)));
            }
        }

        private BitmapImage _CachedImage;
        public BitmapImage CachedImage
        {
            get
            {
                return _CachedImage;
            }
            set
            {
                _CachedImage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CachedImage)));
            }
        }

        private BitmapImage _Thumbnail;
        public BitmapImage Thumbnail
        {
            get
            {
                return _Thumbnail;
            }
            set
            {
                _Thumbnail = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
            }
        }

        private StorageFile _ImageWithoutRotation;
        public StorageFile ImageWithoutRotation
        {
            get
            {
                return _ImageWithoutRotation;
            }
            set
            {
                _ImageWithoutRotation = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageWithoutRotation)));
            }
        }

        public BitmapRotation CurrentRotation;

        private string _ItemDescriptor;
        public string ItemDescriptor
        {
            get
            {
                return _ItemDescriptor;
            }
            set
            {
                _ItemDescriptor = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemDescriptor)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanResultElement(StorageFile file)
        {
            ScanFile = file;
            CachedImage = null;
            ImageWithoutRotation = null;
            CurrentRotation = BitmapRotation.None;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task RenameFileAsync(string newName, NameCollisionOption collisionOption)
        {
            await ScanFile.RenameAsync(newName, collisionOption);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScanFile)));
        }

        public Task RenameFileAsync(string newName)
        {
            return RenameFileAsync(newName, NameCollisionOption.FailIfExists);
        }
    }
}
