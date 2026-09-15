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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Input.Inking;
using WinRT.Interop;
using static Scanner.Helpers.RotationHelpers;
using static Scanner.Helpers.Helpers;

namespace Scanner.Models;

public partial class SetPageInkAction : IProjectAction
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    private static readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
    private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
    #endregion

    private ImagePage page;
    private IReadOnlyList<InkStroke> strokes;

    private IReadOnlyList<InkStroke>? previousStrokes;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Sets the ink drawn on a page, replacing whatever it had before.
    /// </summary>
    /// <param name="page">
    /// The page to draw on.
    /// </param>
    /// <param name="strokes">
    /// The strokes to put on the page, in the coordinate space of its pixels.
    /// </param>
    public SetPageInkAction(ImagePage page, IReadOnlyList<InkStroke> strokes)
    {
        this.page = page;
        this.strokes = strokes;
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public async Task<bool> ExecuteAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
    {
        previousStrokes = page.InkStrokes;
        if (previousStrokes.Count == 0 && strokes.Count == 0) return false;

        await project.SetInkStrokesAsync(page, strokes, uiDispatcherQueue);

        return true;
    }

    public (AnalyticsEvent Event, Dictionary<string, string>? Properties)? GetAnalyticsEvent()
    {
        if (strokes.Count == 0)
            return null;

        return (AnalyticsEvent.DrawOnPage, null);
    }

    public async Task UndoAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
    {
        if (previousStrokes == null)
            throw new ActionFailedAndRolledBackException("Can't undo SetPageInkAction without the previous strokes");

        await project.SetInkStrokesAsync(page, previousStrokes, uiDispatcherQueue);
    }

    public string GetFriendlyName()
    {
        // erasing every stroke reads as clearing the page rather than drawing on it
        if (strokes.Count == 0)
            return GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ProjectActionEraseInkOnPage);
        else
            return GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ProjectActionDrawOnPage);
    }
}
