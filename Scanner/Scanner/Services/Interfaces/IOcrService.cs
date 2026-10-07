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
using Scanner.Models.Interfaces;
using Scanner.Models;
using Scanner.Models.ScanningDevices;
using Windows.Graphics.Imaging;
using Windows.Storage;
using static Scanner.Models.PdfProjectSnapshot;
using Microsoft.UI.Dispatching;

namespace Scanner.Services.Interfaces;

/// <summary>
/// Enables OCR operations, such as generating searchable PDFs from images or detecting document orientation.
/// </summary>
public interface IOcrService
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Whether the OCR engine could be initialized.
    /// </summary>
    bool IsAvailable { get; }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    BitmapRotation? GetRecommendedRotation(StorageFile file);
    Task GenerateOcrPdfAsync(List<IProjectSnapshotPage> Pages, string targetFilePath, DispatcherQueue uiDispatcherQueue);
}
