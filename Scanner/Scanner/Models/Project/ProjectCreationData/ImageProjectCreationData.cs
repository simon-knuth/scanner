using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Scanner.Extensions;
using Scanner.Models.Interfaces;
using Scanner.Models.Project;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using WinRT.Interop;
using static Scanner.Helpers.Helpers;

namespace Scanner.Models
{
    public partial class ImageProjectCreationData : IProjectCreationData
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        public List<PageCreationData> Pages { get; } = [];

        public TargetFormat Format { get; private set; }

        public ScanOptions InitialScanOptions { get; }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ImageProjectCreationData(IReadOnlyList<StorageFile> files, TargetFormat format, string? targetFileName, StorageFolder? targetFolder, ScanOptions initialScanOptions)
        {
            Format = format;
            InitialScanOptions = initialScanOptions;

            foreach (StorageFile file in files)
            {
                Pages.Add(new PageCreationData(file, targetFileName, targetFolder, initialScanOptions.GetBaseFilter(), initialScanOptions.GetFilter(), initialScanOptions.Brightness, initialScanOptions.Contrast));
            }
        }

        public ImageProjectCreationData(Collection<IProjectPage> pages, TargetFormat format, string? targetFileName, ScanOptions initialScanOptions)
        {
            Format = format;
            InitialScanOptions = initialScanOptions;

            foreach (IProjectPage page in pages)
            {
                if (page is ImagePage imagePage)
                {
                    Pages.Add(new PageCreationData(imagePage.SourceFile, imagePage.TargetFile?.Name, imagePage.TargetFolder,
                        imagePage.BaseFilter, imagePage.Filter, imagePage.Brightness, imagePage.Contrast));
                }
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        
    }
}
