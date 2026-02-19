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
using Scanner.Models.Interfaces;

namespace Scanner.Models.Project;

/// <summary>
/// All data that's necessary to create an <see cref="IProjectPage"/>.
/// </summary>
public class PageCreationData
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public StorageFile File { get; }

    public string? TargetFileName { get; }
    public StorageFile? TargetFile { get; }
    public StorageFolder? TargetFolder { get; }

    public ImageFilter BaseFilter { get; }
    public ImageFilter Filter { get; }

    public int Brightness { get; }
    public int Contrast { get; }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public PageCreationData(StorageFile file, string? targetFileName, StorageFile? targetFile, StorageFolder? targetFolder,
        ImageFilter baseFilter, ImageFilter filter, int brightness, int contrast)
    {
        File = file;
        TargetFileName = targetFileName;
        TargetFile = targetFile;
        TargetFolder = targetFolder;
        BaseFilter = baseFilter;
        Filter = filter;
        Brightness = brightness;
        Contrast = contrast;
    }
}
