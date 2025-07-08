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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using WinRT.Interop;

namespace Scanner.Models
{
    public partial class FileNameInfo : ObservableObject
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        #region Events
        public event EventHandler NameChanged;
        #endregion

        private string desiredName;
        public string DesiredName
        {
            get => desiredName;
            private set
            {
                // remove invalid chars
                string result = value;
                foreach (char invalidChar in Path.GetInvalidFileNameChars())
                {
                    result = result.Replace(invalidChar, ' ');
                }
                desiredName = result;
            }
        }

        public string? ActualName { get; private set; }

        [ObservableProperty]
        private string? preGenerationName;

        [ObservableProperty]
        private bool isNameGenerationInProgress;

        public CancellationTokenSource? NameGenerationCts;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public FileNameInfo(string desiredName)
        {
            DesiredName = desiredName;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task UpdateNamesAsync(string desiredName, string? actualName, bool isAIGenerated, DispatcherQueue uiDispatcherQueue)
        {
            await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Low, () =>
            {
                bool changed = DesiredName != desiredName || ActualName != actualName;

                if (isAIGenerated && DesiredName != desiredName)
                    PreGenerationName = DesiredName;

                if (!isAIGenerated && DesiredName != desiredName)
                    PreGenerationName = null;

                DesiredName = desiredName;
                ActualName = actualName;

                if (changed)
                    NameChanged?.Invoke(this, EventArgs.Empty);
            });
        }
    }
}
