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
using System.IO;

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

        public StorageFile? OutOfDateSourceFile {  get; private set; }
        public bool CommitNeeded => Path.GetDirectoryName(SourceFile.Path) == AppDataService.ChangesFolder.Path;

        public StorageFile? TargetFile { get; set; }

        public StorageFolder TargetFolder
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

        public FileNameInfo FileNameInfo { get; private set; }

        public int PageNumber => Index + 1;

        public BitmapRotation Rotation { get; set; } = BitmapRotation.None;
        public BitmapRotation? RecommendedRotation { get; set; } = null;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private ImagePage(StorageFile sourceFile, Uri uri, int index, string targetFileName, StorageFolder targetFolder)
        {
            SourceFile = sourceFile;
            BitmapUri = uri;
            Index = index;
            TargetFolder = targetFolder;
            FileNameInfo = new FileNameInfo(targetFileName);
        }

        /// <summary>
        ///    Creates a new ImagePage from a file.
        /// </summary>
        /// <param name="sourceFile">The image source file.</param>
        /// <param name="index">The index of the page in the <see cref="Project"/>.</param>
        /// <param name="targetFileName">The desired target file name.</param>
        /// <param name="targetFolder">The target folder for this specific page.</param>
        /// <param name="keepSourceFile">Whether to keep the source file or delete it after processing.</param>
        /// <param name="pagesFolder">Which internal folder to copy/move the <paramref name="sourceFile"/> to.</param>
        public static async Task<IProjectPage> CreateAsync(StorageFile sourceFile, StorageFolder targetFolder, int index, string targetFileName, bool keepSourceFile, StorageFolder pagesFolder)
        {
            ImagePage result = await CreateAsyncInternal(sourceFile, targetFolder, index, targetFileName, keepSourceFile, pagesFolder);
            if (targetFileName != null) result.FileNameInfo = new FileNameInfo(targetFileName);
            return result;
        }

        private static async Task<ImagePage> CreateAsyncInternal(StorageFile sourceFile, StorageFolder targetFolder, int index, string targetFileName, bool keepSourceFile, StorageFolder pagesFolder)
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

            // create ImagePage
            ImagePage result = new ImagePage(sourceFile, new Uri(AppDataService.GetUriForAppDataFolder(pagesFolder, sourceFile.Name)), index, targetFileName, targetFolder);
            
            return result;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void ChangeSourceFile(StorageFolder parentFolder, StorageFile file)
        {
            if (OutOfDateSourceFile == null && parentFolder == AppDataService.ChangesFolder)
            {
                // keep track of file that needs to be replaced once changes are committed
                OutOfDateSourceFile = SourceFile;
            }

            SourceFile = file;
            BitmapUri = new Uri(AppDataService.GetUriForAppDataFolder(parentFolder, file.Name));
        }

        public void ClearOutOfDateSourceFile()
        {
            OutOfDateSourceFile = null;
        }
    }
}
