using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

using static Enums;
using static Globals;
using static Utilities;


namespace Scanner
{
    class ScanResultElement : INotifyPropertyChanged
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private StorageFile scanFile;
        public StorageFile ScanFile
        {
            get
            {
                return scanFile;
            }
            set
            {
                scanFile = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScanFile)));
            }
        }

        private BitmapImage cachedImage;
        public BitmapImage CachedImage
        {
            get
            {
                return cachedImage;
            }
            set
            {
                cachedImage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CachedImage)));
            }
        }

        private BitmapImage thumbnail;
        public BitmapImage Thumbnail
        {
            get
            {
                return thumbnail;
            }
            set
            {
                thumbnail = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
            }
        }

        private StorageFile imageWithoutRotation;
        public StorageFile ImageWithoutRotation
        {
            get
            {
                return imageWithoutRotation;
            }
            set
            {
                imageWithoutRotation = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageWithoutRotation)));
            }
        }

        public BitmapRotation CurrentRotation;

        private string itemDescriptor;
        public string ItemDescriptor
        {
            get
            {
                return itemDescriptor;
            }
            set
            {
                itemDescriptor = value;
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
        public async Task RenameFileAsync(string newName)
        {
            await ScanFile.RenameAsync(newName, NameCollisionOption.FailIfExists);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScanFile)));
        }
    }
}
