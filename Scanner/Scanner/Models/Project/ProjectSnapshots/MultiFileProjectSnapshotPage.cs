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

namespace Scanner.Models;

public partial class MultiFileProjectSnapshotPage : IProjectSnapshotPage
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public StorageFile SourceFile { get; }

    public FileHandle? TargetFile { get; }
    public StorageFolder? TargetFolder { get; }

    public string? DesiredFileName { get; }

    public ImageFilter Filter { get; }

    public int Brightness { get; }
    public int Contrast { get; }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public MultiFileProjectSnapshotPage(StorageFile sourceFile, FileHandle? targetFile, StorageFolder targetFolder,
        string? desiredFileName, ImageFilter filter, int brightness, int contrast)
    {
        SourceFile = sourceFile;
        TargetFile = targetFile;
        TargetFolder = targetFolder;
        DesiredFileName = desiredFileName;
        Filter = filter;
        Brightness = brightness;
        Contrast = contrast;
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

}
