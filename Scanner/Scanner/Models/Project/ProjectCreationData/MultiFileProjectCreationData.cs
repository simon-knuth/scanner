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

namespace Scanner.Models;

public partial class MultiFileProjectCreationData : IProjectCreationData
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

    public bool IsAlreadySaved { get; }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public MultiFileProjectCreationData(List<(StorageFile SourceFile, string? TargetFileName, StorageFile? TargetFile)> pages, TargetFormat format, StorageFolder? targetFolder, ScanOptions initialScanOptions, bool isAlreadySaved)
    {
        Format = format;
        InitialScanOptions = initialScanOptions;
        IsAlreadySaved = isAlreadySaved;

        foreach ((StorageFile SourceFile, string? TargetFileName, StorageFile? TargetFile) page in pages)
        {
            Pages.Add(new PageCreationData(page.SourceFile, page.TargetFileName, page.TargetFile, targetFolder, initialScanOptions.GetBaseFilter(), initialScanOptions.GetFilter(), initialScanOptions.Brightness, initialScanOptions.Contrast));
        }
    }

    public MultiFileProjectCreationData(Collection<IProjectPage> pages, TargetFormat format, string? targetFileName, ScanOptions initialScanOptions, bool isAlreadySaved)
    {
        Format = format;
        InitialScanOptions = initialScanOptions;
        IsAlreadySaved = isAlreadySaved;

        foreach (IProjectPage page in pages)
        {
            if (page is ImagePage imagePage)
            {
                Pages.Add(new PageCreationData(imagePage.SourceFile, targetFileName ?? imagePage.FileNameInfo?.DesiredName, null, imagePage.TargetFolder,
                    imagePage.BaseFilter, imagePage.Filter, imagePage.Brightness, imagePage.Contrast));
            }
        }
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public async Task<ProjectBase> CreateProjectAsync(bool keepSourceFiles, DispatcherQueue uiDispatcherQueue)
    {
        return await MultiFileProject.CreateAsync(this, keepSourceFiles, IsAlreadySaved, uiDispatcherQueue);
    }
}
