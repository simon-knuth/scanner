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
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Devices.Scanners;
using Windows.Storage;
using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Scanner.Models.Project;

namespace Scanner.Models.Interfaces
{
    /// <summary>
    /// All data that's necessary to create a <see cref="ProjectBase"/>.
    /// </summary>
    public interface IProjectCreationData
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        List<PageCreationData> Pages { get; }
        
        TargetFormat Format { get; }

        ScanOptions InitialScanOptions { get; }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        Task<ProjectBase> CreateProjectAsync(bool keepSourceFiles, DispatcherQueue uiDispatcherQueue);
    }
}
