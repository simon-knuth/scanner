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
    /// An <see cref="IProjectPage"/> created from a PDF page.
    /// </summary>
    public partial class PdfPage : ObservableObject, IProjectPage
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private static readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        private static string[] allowedFileExtensions = [".pdf"];

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

        [ObservableProperty]
        private Uri previewBitmapUri;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageNumber))]
        private int index;

        public int PageNumber => Index + 1;

        public bool IsReadOnly { get; } = true;

        public uint IndexInPdf { get; }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private PdfPage(StorageFile sourceFile, uint indexInPdf, int index)
        {
            SourceFile = sourceFile;
            IndexInPdf = indexInPdf;
            Index = index;
        }

        /// <summary>
        ///    Creates a new <see cref="PdfPage"/> from a file.
        /// </summary>
        public static async Task<IProjectPage> CreateAsync(StorageFile sourceFile, uint indexInPdf, int index)
        {
            PdfPage result = await CreateAsyncInternal(sourceFile, indexInPdf, index);
            return result;
        }

        private static async Task<PdfPage> CreateAsyncInternal(StorageFile sourceFile, uint indexInPdf, int index)
        {
            // check file
            if (sourceFile == null)
            {
                throw new ArgumentException("Can't create PdfPage from null file");
            }

            // check file extension
            string extension = sourceFile.FileType.ToLower();
            if (!allowedFileExtensions.Contains(extension))
            {
                // unknown format
                throw new ArgumentException("Failed to create PdfPage due to incompatible file format");
            }


            // create PdfPage
            PdfPage result = new(sourceFile, indexInPdf, index);
            
            return result;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task UpdatePreviewFileAsync(StorageFile newPreviewFile, DispatcherQueue uiDispatcherQueue)
        {
            // change file
            await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Normal, () =>
            {
                PreviewFile = newPreviewFile;
                PreviewBitmapUri = new Uri(AppDataService.GetUriForAppDataFolder(AppDataService.PreviewFolder, newPreviewFile.Name));
            });
        }
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
}
