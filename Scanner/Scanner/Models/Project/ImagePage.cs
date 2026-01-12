using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Scanner.Extensions;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using WinRT.Interop;

namespace Scanner.Models
{
    /// <summary>
    /// An <see cref="IProjectPage"/> created from an image file.
    /// </summary>
    public partial class ImagePage : ObservableObject, IProjectPage
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private static readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        private static string[] allowedFileExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff"];

        private StorageFile sourceFile;
        public StorageFile SourceFile
        {
            get => sourceFile;
            private set
            {
                SetProperty(ref sourceFile, value);
            }
        }

        private StorageFile previewFile;
        public StorageFile PreviewFile
        {
            get => previewFile;
            private set
            {
                SetProperty(ref previewFile, value);
            }
        }

        public StorageFile? OutOfDateSourceFile {  get; private set; }
        public bool CommitNeeded => Path.GetDirectoryName(SourceFile.Path) == AppDataService.ChangesFolder.Path;

        public TargetFile? TargetFile { get; set; }

        public StorageFolder? TargetFolder { get; set; }

        [ObservableProperty]
        private Uri sourceBitmapUri;

        [ObservableProperty]
        private Uri previewBitmapUri;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageNumber))]
        private int index;

        public FileNameInfo? FileNameInfo { get; set; }

        public int PageNumber => Index + 1;

        public BitmapRotation Rotation { get; set; } = BitmapRotation.None;
        public BitmapRotation? RecommendedRotation { get; set; } = null;

        public bool IsUsingDestructiveEffects => Filter != ImageFilter.None || Brightness != 0 || Contrast != 0;

        /// <summary>
        /// The <see cref="ImageFilter"/> used by the source file, usually <see cref="ImageFilter.None"/>.
        /// For example, if a scanner only supports grayscale, this will be set to <see cref="ImageFilter.Grayscale"/> and
        /// indicate that <see cref="ImageFilter.None"/> is unavailable due to the lack of color information.
        /// </summary>
        public ImageFilter BaseFilter { get; private set; }

        /// <summary>
        /// The currently applied <see cref="ImageFilter"/>. Has to be one of the elements in <see cref="AvailableFilters"/>.
        /// </summary>
        [ObservableProperty]
        private ImageFilter filter = ImageFilter.None;

        public ImageFilter[] AvailableFilters { get; private set; }

        [ObservableProperty]
        private uint width;

        [ObservableProperty]
        private uint height;

        [ObservableProperty]
        private int brightness = AppConfig.DefaultBrightness;

        [ObservableProperty]
        private int contrast = AppConfig.DefaultContrast;

        /// <summary>
        /// Used for two-way binding in the UI. Value is copied to <see cref="Brightness"/> after a short delay.
        /// </summary>
        [ObservableProperty]
        private int displayedBrightness = AppConfig.DefaultBrightness;

        /// <summary>
        /// Used for two-way binding in the UI. Value is copied to <see cref="Contrast"/> after a short delay.
        /// </summary>
        [ObservableProperty]
        private int displayedContrast = AppConfig.DefaultContrast;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private ImagePage(StorageFile sourceFile, TargetFile? targetFile, Uri sourceBitmapUri, int index, string? targetFileName, StorageFolder? targetFolder, ImageFilter baseFilter, ImageFilter filter, int brightness, int contrast, uint width, uint height)
        {
            SourceFile = PreviewFile = sourceFile;
            TargetFile = targetFile;
            SourceBitmapUri = PreviewBitmapUri = sourceBitmapUri;
            Index = index;
            if (targetFileName != null) FileNameInfo = new FileNameInfo(targetFileName);
            TargetFolder = targetFolder;
            BaseFilter = baseFilter;
            Filter = filter;
            AvailableFilters = GetAvailableFilters();
            Brightness = brightness;
            Contrast = contrast;
            DisplayedBrightness = brightness;
            DisplayedContrast = contrast;
            Width = width;
            Height = height;
        }

        /// <summary>
        ///    Creates a new ImagePage from a file.
        /// </summary>
        public static async Task<IProjectPage> CreateAsync(StorageFile sourceFile, StorageFile? targetFile, StorageFolder? targetFolder, int index, string? targetFileName, bool keepSourceFile, StorageFolder pagesFolder, ImageFilter baseFilter, ImageFilter filter, int brightness, int contrast)
        {
            ImagePage result = await CreateAsyncInternal(sourceFile, targetFile, targetFolder, index, targetFileName, keepSourceFile, pagesFolder, baseFilter, filter, brightness, contrast);
            return result;
        }

        private static async Task<ImagePage> CreateAsyncInternal(StorageFile sourceFile, StorageFile? targetFile, StorageFolder? targetFolder, int index, string? targetFileName, bool keepSourceFile, StorageFolder pagesFolder, ImageFilter baseFilter, ImageFilter filter, int brightness, int contrast)
        {
            // check file
            if (sourceFile == null)
            {
                throw new ArgumentException("Can't create ImagePage from null file");
            }

            // check file extension
            string extension = sourceFile.FileType.ToLower();
            if (!allowedFileExtensions.Contains(extension))
            {
                // unknown format
                throw new ArgumentException("Failed to create ImagePage due to incompatible file format");
            }

            // copy file to pages folder
            if (keepSourceFile)
            {
                sourceFile = await sourceFile.CopyAsync(pagesFolder, index.ToString() + sourceFile.FileType, NameCollisionOption.GenerateUniqueName);
            }
            else
            {
                await sourceFile.MoveAsync(pagesFolder, index.ToString() + sourceFile.FileType, NameCollisionOption.GenerateUniqueName);
            }

            // get image attributes
            ImageProperties imageProperties = await sourceFile.Properties.GetImagePropertiesAsync();

            // get target file
            TargetFile? target = null;
            if (targetFile != null)
                target = new(targetFile, await targetFile.OpenAsync(FileAccessMode.ReadWrite, StorageOpenOptions.AllowOnlyReaders));

            // create ImagePage
            ImagePage result = new(sourceFile, target, new Uri(AppDataService.GetUriForAppDataFolder(pagesFolder, sourceFile.Name)), index, targetFileName, targetFolder, baseFilter, filter, brightness, contrast, imageProperties.Width, imageProperties.Height);
            
            return result;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task ChangeSourceFileAsync(StorageFolder parentFolder, StorageFile file, DispatcherQueue uiDispatcherQueue)
        {
            if (OutOfDateSourceFile == null && parentFolder == AppDataService.ChangesFolder)
            {
                // keep track of file that needs to be replaced once changes are committed
                OutOfDateSourceFile = SourceFile;
            }

            Uri previewBitmapUri = new Uri(AppDataService.GetUriForAppDataFolder(parentFolder, file.Name));
            if (PreviewFile == SourceFile)
            {
                // no separate preview ~> preview bitmap URI changes
                PreviewFile = file;
                await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Normal, () =>
                {
                    PreviewBitmapUri = previewBitmapUri;
                });
            }

            SourceFile = file;
            await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Normal, () =>
            {
                SourceBitmapUri = previewBitmapUri;
            });
        }

        public async Task UpdatePreviewFileAsync(StorageFile? newPreviewFile, DispatcherQueue uiDispatcherQueue)
        {
            StorageFile? previousFile = PreviewFile != SourceFile ? PreviewFile : null;

            // change file
            await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Normal, () =>
            {
                if (newPreviewFile != null)
                {
                    PreviewFile = newPreviewFile;
                    PreviewBitmapUri = new Uri(AppDataService.GetUriForAppDataFolder(AppDataService.PreviewFolder, newPreviewFile.Name));
                }
                else
                {
                    PreviewFile = SourceFile;
                    if (CommitNeeded)
                    {
                        PreviewBitmapUri = new Uri(AppDataService.GetUriForAppDataFolder(AppDataService.ChangesFolder, SourceFile.Name));
                    }
                    else
                    {
                        PreviewBitmapUri = new Uri(AppDataService.GetUriForAppDataFolder(AppDataService.ProjectFolder, SourceFile.Name));
                    }
                }
            });

            // remove previous one
            if (previousFile != null)
            {
                await previousFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
        }

        public void ClearOutOfDateSourceFile()
        {
            OutOfDateSourceFile = null;
        }

        private ImageFilter[] GetAvailableFilters()
        {
            switch (BaseFilter)
            {
                case ImageFilter.None:
                    return [ImageFilter.None, ImageFilter.Grayscale, ImageFilter.Monochrome];
                case ImageFilter.Grayscale:
                    return [ImageFilter.Grayscale, ImageFilter.Monochrome];
                case ImageFilter.Monochrome:
                    return [ImageFilter.Monochrome];
                default:
                    throw new ArgumentException($"Failed to get available ImageFilters for BaseFilter {BaseFilter}");
            }
        }
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public enum ImageFilter
    {
        None,
        Grayscale,
        Monochrome
    }
}
