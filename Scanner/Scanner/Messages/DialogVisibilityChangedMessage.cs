using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scanner.Messages;

internal class DialogVisibilityChangedMessage
{
    public readonly bool IsDialogVisible;

    public DialogVisibilityChangedMessage(bool isDialogVisible)
    {
        IsDialogVisible = isDialogVisible;
    }
}
