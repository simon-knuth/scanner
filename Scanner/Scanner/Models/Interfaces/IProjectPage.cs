using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinRT.Interop;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Devices.Scanners;
using Windows.Storage;
using System.ComponentModel;
using Windows.Graphics.Imaging;
using Microsoft.UI.Dispatching;

namespace Scanner.Models.Interfaces
{
    public interface IProjectPage : INotifyPropertyChanged
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        StorageFile SourceFile { get; }
        StorageFile? TargetFile { get; set; }

        /// <summary>
        /// The file used for preview generation. Usually the same as <see cref="SourceFile"/>, unless a destructive effect is applied.
        /// </summary>
        StorageFile? PreviewFile { get; }

        /// <summary>
        /// Whether the current <see cref="SourceFile"/> needs to be committed to the <see cref="IAppDataService.ProjectFolder"/>.
        /// </summary>
        bool CommitNeeded { get; }

        /// <summary>
        /// If the current <see cref="SourceFile"/> needs to be committed to the <see cref="IAppDataService.ProjectFolder"/>
        /// and a file already exists, this is the file it will replace there.
        /// </summary>
        StorageFile? OutOfDateSourceFile { get; }

        Uri PreviewBitmapUri { get; }

        int Index { get; set; }
        int PageNumber { get; }

        uint Width { get; set; }
        uint Height { get; set; }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        void ChangeSourceFile(StorageFolder parentFolder, StorageFile file, DispatcherQueue uiDispatcherQueue);
        void ClearOutOfDateSourceFile();
    }
}
