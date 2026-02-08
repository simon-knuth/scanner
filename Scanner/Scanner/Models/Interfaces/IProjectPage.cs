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
        /// <summary>
        /// The file used for preview generation. Usually the same as the source file, unless a destructive effect is
        /// applied.
        /// </summary>
        StorageFile PreviewFile { get; }

        /// <summary>
        /// The URI for the bitmap representing the <see cref="PreviewFile"/>.
        /// Usually the same as <see cref="SourceBitmapUri"/>, unless a destructive effect is applied.
        /// Will raise <see cref="INotifyPropertyChanged"/> event every
        /// time the bitmap changes, even if the file is the same.
        /// </summary>        
        Uri PreviewBitmapUri { get; }

        int Index { get; set; }
        int PageNumber { get; }

        bool IsReadOnly { get; }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        
    }
}
