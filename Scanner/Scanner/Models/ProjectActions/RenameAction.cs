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
using Windows.Storage;
using CommunityToolkit.Mvvm.DependencyInjection;
using Scanner.Services.Interfaces;
using Scanner.Models.Interfaces;
using System.ComponentModel;
using Windows.Graphics.Imaging;
using Microsoft.UI.Dispatching;
using static Scanner.Helpers.RotationHelpers;
using static Scanner.Helpers.Helpers;

namespace Scanner.Models;

public partial class RenameAction : IProjectAction
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
    #region Services
    private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
    #endregion

    private IProjectPage? page;
    private string newName;
    private bool isAIGenerated;

    private string? oldName;
    private AnalyticsEvent? analyticsEvent;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Renames a page.
    /// </summary>
    public RenameAction(IProjectPage? page, string newName, bool isAIGenerated = false)
    {
        this.page = page;
        this.newName = newName;
        this.isAIGenerated = isAIGenerated;
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public async Task<bool> ExecuteAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
    {
        if (project is PdfProject pdfProject)
        {
            if (!isAIGenerated)
                pdfProject.FileNameInfo!.NameGenerationCts?.Cancel();
            oldName = pdfProject.FileNameInfo!.DesiredName;
            await pdfProject.FileNameInfo!.UpdateNamesAsync(newName, pdfProject.FileNameInfo.ActualName, isAIGenerated, uiDispatcherQueue);
            if (!isAIGenerated)
                analyticsEvent = AnalyticsEvent.RenamePDF;
        }
        else if (page is ImagePage imagePage)
        {
            if (!isAIGenerated)
                imagePage.FileNameInfo.NameGenerationCts?.Cancel();
            oldName = imagePage.FileNameInfo.DesiredName;
            await imagePage.FileNameInfo.UpdateNamesAsync(newName, imagePage.FileNameInfo.ActualName, isAIGenerated, uiDispatcherQueue);
            if (!isAIGenerated)
                analyticsEvent = AnalyticsEvent.RenamePage;
        }

        return true;
    }

    public (AnalyticsEvent Event, Dictionary<string, string>? Properties)? GetAnalyticsEvent()
    {
        return analyticsEvent != null ? (analyticsEvent.Value, null) : null;
    }

    public async Task UndoAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
    {
        if (oldName == null)
        {
            throw new ActionFailedAndRolledBackException("Can't undo RenameAction without old name");
        }

        if (project is PdfProject pdfProject)
        {
            await pdfProject.FileNameInfo.UpdateNamesAsync(oldName, pdfProject.FileNameInfo.ActualName, false, uiDispatcherQueue);
        }
        else if (page is ImagePage imagePage)
        {
            await imagePage.FileNameInfo.UpdateNamesAsync(oldName, imagePage.FileNameInfo.ActualName, false, uiDispatcherQueue);
        }
    }

    public string GetFriendlyName()
    {
        return GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ProjectActionRename);
    }
}
