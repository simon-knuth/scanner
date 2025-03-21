using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinRT.Interop;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Storage;
using CommunityToolkit.Mvvm.DependencyInjection;
using Scanner.Services.Interfaces;
using Scanner.Models.Interfaces;
using System.ComponentModel;
using Windows.Graphics.Imaging;

namespace Scanner.Models
{
    public partial class ImagePage : ObservableObject, IProjectPage
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private static readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        private static string[] allowedFileExtensions = new string[] { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff" };

        private StorageFile sourceFile;
        public StorageFile SourceFile
        {
            get => sourceFile;
            private set
            {
                SetProperty(ref sourceFile, value);
            }
        }

        public StorageFile? TargetFile
        {
            get;
            private set;
        }

        private Uri bitmapUri;
        public Uri BitmapUri
        {
            get => bitmapUri;
            private set
            {
                SetProperty(ref bitmapUri, value);
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageNumber))]
        private int index;

        public int PageNumber => Index + 1;

        public BitmapRotation Rotation { get; set; } = BitmapRotation.None;
        public BitmapRotation? RecommendedRotation { get; set; } = null;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private ImagePage(StorageFile sourceFile, Uri uri, int index)
        {
            SourceFile = sourceFile;
            BitmapUri = uri;
            Index = index;
        }

        public static async Task<IProjectPage> CreateAsync(StorageFile sourceFile, int index)
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

            // move file to project folder
            await sourceFile.MoveAsync(AppDataService.ProjectFolder, index.ToString() + sourceFile.FileType, NameCollisionOption.FailIfExists);

            // create ImagePage
            return new ImagePage(sourceFile, new Uri(AppDataService.GetUriForAppDataFolder(AppDataService.ProjectFolder, sourceFile.Name)), index);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void ChangeSourceFile(StorageFile file, Uri uri)
        {
            SourceFile = file;
            BitmapUri = uri;
        }
    }
}
