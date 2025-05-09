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
    internal class ShowProjectDeletionDialogMessage : RequestMessage<Task<bool>>
    {
        public ProjectBase Project;

        public ShowProjectDeletionDialogMessage(ProjectBase project)
        {
            this.Project = project;
        }
    }
}
