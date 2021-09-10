using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Scanner.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace Scanner.Services
{
    /// <summary>
    ///     Holds the current <see cref="ScanResult"/>.
    /// </summary>
    internal class ScanResultService : ObservableObject, IScanResultService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();

        public event EventHandler<ScanResult> ScanResultCreated;
        public event EventHandler ScanResultDismissed;

        private ScanResult _Result;
        public ScanResult Result
        {
            get => _Result;
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
            _Result = null;
            if (Result != null) ScanResultDismissed?.Invoke(this, EventArgs.Empty);

            futureAccessListIndex = 0;
            _Result = await ScanResult.CreateAsync(files, targetFolder, futureAccessListIndex, fixedFolder);

            ScanResultCreated?.Invoke(this, Result);
        }

        public async Task AddToResultFromFilesAsync(IReadOnlyList<StorageFile> files)
        {
            throw new NotImplementedException();
        }
    }
}
