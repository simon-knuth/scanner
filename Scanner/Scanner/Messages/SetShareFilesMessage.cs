using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Scanner.Messages;

/// <summary>
/// Sets the current list of files to share in case the Windows share sheet is displayed using <see cref="InvokeShareUIMessage"/>.
/// </summary>
internal class SetShareFilesMessage
{
    public readonly List<StorageFile> Files;

    public SetShareFilesMessage(List<StorageFile> files)
    {
        Files = files;
    }
}
