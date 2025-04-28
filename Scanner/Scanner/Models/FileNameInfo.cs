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
using System.IO;
using Microsoft.UI.Dispatching;
using Scanner.Extensions;

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
        private string preGenerationName;

        [ObservableProperty]
        private bool isNameGenerationInProgress;


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
        public async Task UpdateNamesAsync(string desiredName, string? actualName, DispatcherQueue uiDispatcherQueue)
        {
            await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Low, () =>
            {
                bool changed = DesiredName != desiredName || ActualName != actualName;

                DesiredName = desiredName;
                ActualName = actualName;

                if (changed) NameChanged?.Invoke(this, EventArgs.Empty);
            });
        }
    }
}
