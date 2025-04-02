using CommunityToolkit.Mvvm.Messaging.Messages;
using Scanner.Models;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Scanner.Messages
{
    internal class ShowSaveFileDialogMessage : RequestMessage<Task<SaveOptions?>>
    {
        public ScanOptions ScanOptions;
        public Project? Project;

        public ShowSaveFileDialogMessage(ScanOptions scanOptions, Project? project)
        {
            this.ScanOptions = scanOptions;
            this.Project = project;
        }
    }
}
