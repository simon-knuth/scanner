using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Messaging;
using Scanner.Models;
using Scanner.Services.Messenger;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;
using static Scanner.Services.Messenger.MessengerEnums;
using static Utilities;

namespace Scanner.Services
{
    /// <summary>
    ///     Holds the current <see cref="ScanResult"/> and exposes it to other code.
    /// </summary>
    internal class ScanResultService : ObservableRecipient, IScanResultService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();

        public event EventHandler<ScanResult> ScanResultCreated;
        public event EventHandler ScanResultChanging;
        public event EventHandler ScanResultChanged;
        public event EventHandler ScanResultDismissed;

        private ScanResult _Result;
        public ScanResult Result
        {
            get => _Result;
        }

        private bool _IsScanResultChanging;
        public bool IsScanResultChanging
        {
            get => _IsScanResultChanging;
            private set
            {
                bool old = _IsScanResultChanging;
                SetProperty(ref _IsScanResultChanging, value);

                if (old != value)
                {
                    if (value == true) ScanResultChanging?.Invoke(this, EventArgs.Empty);
                    else ScanResultChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private int futureAccessListIndex;

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanResultService()
        {
            
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task CreateResultFromFilesAsync(IReadOnlyList<StorageFile> files, StorageFolder targetFolder,
            bool fixedFolder)
        {
            if (Result != null)
            {
                _Result = null;
                ScanResultDismissed?.Invoke(this, EventArgs.Empty);
            }

            futureAccessListIndex = 0;
            _Result = await ScanResult.CreateAsync(files, targetFolder, futureAccessListIndex, fixedFolder);

            ScanResultCreated?.Invoke(this, Result);
        }

        public async Task AddToResultFromFilesAsync(IReadOnlyList<StorageFile> files)
        {
            throw new NotImplementedException();
        }

        public async Task RotatePagesAsync(IList<Tuple<int, BitmapRotation>> instructions)
        {
            IsScanResultChanging = true;

            try
            {
                await Result.RotateScansAsync(instructions);
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageRotateHeading"),
                    MessageText = LocalizedString("ErrorMessageRotateBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });
            }

            IsScanResultChanging = false;
        }
    }
}
