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

        public StorageFile File
        {
            get;
            private set;
        }

        public Uri BitmapUri
        {
            get;
            private set;
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageNumber))]
        private int index;

        public int PageNumber => Index + 1;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private ImagePage(StorageFile file, Uri uri, int index)
        {
            File = file;
            BitmapUri = uri;
            Index = index;
        }

        public static async Task<IProjectPage> CreateAsync(StorageFile file, int index)
        {
            // check file
            if (file == null)
            {
                throw new ArgumentException("Can't create ImagePage from null file");
            }

            // check file extension
            string extension = file.FileType.ToLower();
            if (!allowedFileExtensions.Contains(extension))
            {
                // unknown format
                throw new ArgumentException("Failed to create ImagePage due to incompatible file format");
            }

            // move file to project folder
            await file.MoveAsync(AppDataService.ProjectFolder, index.ToString() + file.FileType, NameCollisionOption.FailIfExists);

            // create ImagePage
            return new ImagePage(file, new Uri(AppDataService.GetUriForAppDataFolder(AppDataService.ProjectFolder, file.Name)), index);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    }
}
