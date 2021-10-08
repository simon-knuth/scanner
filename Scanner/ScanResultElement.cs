using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Scanner.Services;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;
using static Utilities;


namespace Scanner
{
    public class ScanResultElement : INotifyPropertyChanged
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();

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

        public int FutureAccessListIndex;

        private string _DisplayedFolder;
        public string DisplayedFolder
        {
            get
            {
                return _DisplayedFolder;
            }
            set
            {
                _DisplayedFolder = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayedFolder)));
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanResultElement(StorageFile file, int futureAccessListIndex, string displayedFolder)
        {
            ScanFile = file;
            CachedImage = null;
            ImageWithoutRotation = null;
            CurrentRotation = BitmapRotation.None;
            FutureAccessListIndex = futureAccessListIndex;
            DisplayedFolder = displayedFolder;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // EVENTS ///////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public event PropertyChangedEventHandler PropertyChanged;


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

        /// <summary>
        ///     Generates the image preview for this scan.
        /// </summary>
        /// <remarks>
        ///     Parts of this method are required run on the UI thread.
        /// </remarks>
        /// <exception cref="ApplicationException">A file could not be accessed or a file's type could not be determined.</exception>
        /// <exception cref="NotImplementedException">Attempted to generate an image of an (O)XPS file.</exception>
        public async Task<BitmapImage> GetImageAsync()
        {
            // use cached image if possible
            if (CachedImage != null)
            {
                LogService?.Log.Information("Returning a cached image.");
                return CachedImage;
            }

            // create new bitmap
            StorageFile sourceFile = ScanFile;
            BitmapImage bmp = null;
            int attempt = 0;
            await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.High, async () =>
            {
                using (IRandomAccessStream sourceStream = await sourceFile.OpenAsync(FileAccessMode.Read))
                {
                    while (attempt != -1)
                    {
                        try
                        {
                            bmp = new BitmapImage();
                            await bmp.SetSourceAsync(sourceStream);
                            attempt = -1;
                        }
                        catch (Exception e)
                        {
                            if (attempt >= 4) throw new ApplicationException("Unable to open file stream for generating bitmap of image.", e);

                            LogService?.Log.Warning(e, "Opening the file stream of page failed, retrying in 500ms.");
                            await Task.Delay(500);
                            attempt++;
                        }
                    }

                    // save image to cache
                    CachedImage = bmp;

                    // generate thumbnail
                    BitmapImage thumbnail = new BitmapImage();
                    BitmapDecoder bitmapDecoder = null;
                    attempt = 0;
                    while (attempt != -1)
                    {
                        try
                        {
                            bitmapDecoder = await BitmapDecoder.CreateAsync(sourceStream);
                            SoftwareBitmap softwareBitmap = await bitmapDecoder.GetSoftwareBitmapAsync();
                            Guid encoderId = GetBitmapEncoderId(sourceFile.FileType);
                            var imageStream = new InMemoryRandomAccessStream();
                            BitmapEncoder bitmapEncoder = await BitmapEncoder.CreateAsync(encoderId, imageStream);
                            bitmapEncoder.SetSoftwareBitmap(softwareBitmap);

                            // reduce resolution of thumbnail
                            int resolutionScaling = 1;
                            if (softwareBitmap.PixelWidth < softwareBitmap.PixelHeight)
                            {
                                resolutionScaling = softwareBitmap.PixelWidth / 150;
                            }
                            else
                            {
                                resolutionScaling = softwareBitmap.PixelHeight / 150;
                            }
                            if (resolutionScaling < 1) resolutionScaling = 1;
                            bitmapEncoder.BitmapTransform.ScaledWidth = Convert.ToUInt32(bitmapDecoder.PixelWidth / resolutionScaling);
                            bitmapEncoder.BitmapTransform.ScaledHeight = Convert.ToUInt32(bitmapDecoder.PixelHeight / resolutionScaling);

                            await bitmapEncoder.FlushAsync();
                            await thumbnail.SetSourceAsync(imageStream);
                            Thumbnail = thumbnail;
                            break;
                        }
                        catch (Exception e)
                        {
                            if (attempt >= 4)
                            {
                                LogService?.Log.Error(e, "Couldn't generate thumbnail of page.");
                                return;
                            }

                            LogService?.Log.Warning(e, "Generating the thumbnail of page failed, retrying in 500ms.");
                            await Task.Delay(500);
                            attempt++;
                        }
                    }
                }
            });

            LogService?.Log.Information("Returning a newly generated image.");
            CachedImage = bmp;
            return CachedImage;
        }
    }
}
