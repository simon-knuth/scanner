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
using Windows.UI.Input.Inking;
using System.ComponentModel;
using Microsoft.UI.Dispatching;

namespace Scanner.Models.Interfaces;

/// <summary>
/// A snapshot of a project page with the basic data required to save or create it.
/// </summary>
public interface IProjectSnapshotPage
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    StorageFile SourceFile { get; }
    ImageFilter Filter { get; }
    int Brightness { get; }
    int Contrast { get; }

    /// <summary>
    /// The page's ink, in the coordinate space of <see cref="SourceFile"/>'s pixels.
    /// </summary>
    IReadOnlyList<InkStroke> InkStrokes { get; }

    /// <summary>
    /// Whether the page has to be rendered rather than written out as-is. Skipping the render pass when this is
    /// <see langword="true"/> silently drops whatever it reports.
    /// </summary>
    bool RequiresRasterPass => Filter != ImageFilter.None || Brightness != 0 || Contrast != 0 || InkStrokes.Count > 0;
}
