using CommunityToolkit.Mvvm.Messaging.Messages;
using Scanner.Models;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Scanner.Messages;

internal class ShowSaveOptionsDialogMessage : RequestMessage<Task<SaveOptions?>>
{
    public readonly ScanOptions ScanOptions;
    public readonly ProjectBase? Project;
    public readonly string? DesiredFileDisplayName;

    /// <summary>
    /// Existing files being saved (0 for new scans), names may be kept if more than 1.
    /// </summary>
    public readonly int ExistingFileCount;

    public ShowSaveOptionsDialogMessage(ScanOptions scanOptions, ProjectBase? project, string? desiredFileDisplayName, int existingFileCount)
    {
        ScanOptions = scanOptions;
        Project = project;
        DesiredFileDisplayName = desiredFileDisplayName;
        ExistingFileCount = existingFileCount;
    }
}
