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

public partial class PdfProjectCreationData : IProjectCreationData
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
    #endregion

    public Guid? Id { get; }

    public List<PageCreationData> Pages { get; } = [];

    public TargetFormat Format { get; private set; } = TargetFormat.PDF;

    public ScanOptions InitialScanOptions { get; }

    public bool IsAlreadySaved { get; }

    public string TargetFileName { get; }

    public StorageFolder? TargetFolder { get; }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public PdfProjectCreationData(Guid? id, IReadOnlyList<StorageFile> files, string targetFileName, StorageFolder? targetFolder, ScanOptions initialScanOptions, bool isAlreadySaved)
    {
        Id = id;
        TargetFileName = targetFileName;
        TargetFolder = targetFolder;
        InitialScanOptions = initialScanOptions;
        IsAlreadySaved = isAlreadySaved;

        foreach (StorageFile file in files)
        {
            Pages.Add(new PageCreationData(file, targetFileName, null, targetFolder, initialScanOptions.GetBaseFilter(), initialScanOptions.GetFilter(), initialScanOptions.Brightness, initialScanOptions.Contrast));
        }
    }

    public PdfProjectCreationData(Guid? id, StorageFile pdfFile, uint pagesInPdf, StorageFolder? targetFolder, ScanOptions initialScanOptions, bool isAlreadySaved)
    {
        Id = id;
        TargetFileName = pdfFile.Name;
        TargetFolder = targetFolder;
        InitialScanOptions = initialScanOptions;
        IsAlreadySaved = isAlreadySaved;

        for (int i = 0; i < pagesInPdf; i++)
        {
            Pages.Add(new PageCreationData(pdfFile, TargetFileName, null, targetFolder, initialScanOptions.GetBaseFilter(), initialScanOptions.GetFilter(), initialScanOptions.Brightness, initialScanOptions.Contrast));
        }
    }

    public PdfProjectCreationData(Guid? id, Collection<IProjectPage> pages, string targetFileName, StorageFolder? targetFolder, ScanOptions initialScanOptions, bool isAlreadySaved)
    {
        Id = id;
        TargetFileName = targetFileName;
        TargetFolder = targetFolder;
        InitialScanOptions = initialScanOptions;
        IsAlreadySaved = isAlreadySaved;

        foreach (IProjectPage page in pages)
        {
            if (page is ImagePage imagePage)
            {
                Pages.Add(new PageCreationData(imagePage.SourceFile, imagePage.TargetFile?.File.Name, null, imagePage.TargetFolder,
                    imagePage.BaseFilter, imagePage.Filter, imagePage.Brightness, imagePage.Contrast));
            }
        }
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public async Task<ProjectBase> CreateProjectAsync(bool keepSourceFiles, DispatcherQueue uiDispatcherQueue)
    {
        return await PdfProject.CreateAsync(this, keepSourceFiles, IsAlreadySaved, uiDispatcherQueue);
    }
}
