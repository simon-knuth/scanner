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

public partial class DrawOnPagesAsCopyAction : IProjectAction
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    private static readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
    private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
    #endregion

    private List<ImagePage> pages;
    private IReadOnlyList<InkStroke> strokes;

    private List<ImagePage>? addedPages;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Copies a set of pages and puts ink on the copies, leaving the originals as they are.
    /// </summary>
    /// <param name="pages">
    /// A list of pages to copy and draw on.
    /// </param>
    /// <param name="strokes">
    /// The strokes to put on all copies, in the coordinate space of the pages' pixels.
    /// </param>
    public DrawOnPagesAsCopyAction(List<ImagePage> pages, IReadOnlyList<InkStroke> strokes)
    {
        this.pages = pages;
        this.strokes = strokes;
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public async Task<bool> ExecuteAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
    {
        addedPages = await project.AddInkedCopiesOfPagesAsync(pages, strokes, AppDataService.ChangesFolder, uiDispatcherQueue);

        return addedPages.Count > 0;
    }

    public (AnalyticsEvent Event, Dictionary<string, string>? Properties)? GetAnalyticsEvent()
    {
        if (addedPages == null || addedPages.Count == 0) return null;
        return (AnalyticsEvent.DrawOnPageAsCopy, null);
    }

    public async Task UndoAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
    {
        if (addedPages == null)
            throw new ActionFailedAndRolledBackException("Can't undo DrawOnPagesAsCopyAction without list of added pages");

        // remove added pages
        await project.RemovePagesAsync(addedPages, false, uiDispatcherQueue);
    }

    public string GetFriendlyName()
    {
        return GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ProjectActionDrawOnPageAsCopy);
    }
}
