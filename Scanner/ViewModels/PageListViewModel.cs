using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Scanner.Services;
using Scanner.Services.Messenger;
using System;
using System.Collections.Generic;

namespace Scanner.ViewModels
{
    public class PageListViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public readonly IScanResultService ScanResultService = Ioc.Default.GetRequiredService<IScanResultService>();
        public readonly IScanService ScanService = Ioc.Default.GetRequiredService<IScanService>();

        public RelayCommand DisposeCommand;

        public event EventHandler ScanStarted;
        public event EventHandler ScanEnded;


        private ScanResult _ScanResult;
        public ScanResult ScanResult
        {
            get => _ScanResult;
            set => SetProperty(ref _ScanResult, value);
        }

        private int _SelectedPageIndex;
        public int SelectedPageIndex
        {
            get => _SelectedPageIndex;
            set
            {
                int old = _SelectedPageIndex;
                SetProperty(ref _SelectedPageIndex, value);
                if (old != value && value != -1) Messenger.Send(new PageListCurrentIndexChangedMessage(value));
            }
        }

        private IList<ScanResultElement> _SelectedPages;
        public IList<ScanResultElement> SelectedPages
        {
            get => _SelectedPages;
            set => SetProperty(ref _SelectedPages, value);
        }

        private bool _IsScanRunning;
        public bool IsScanRunning
        {
            get => _IsScanRunning;
            set
            {
                bool old = _IsScanRunning;
                
                SetProperty(ref _IsScanRunning, value);

                if (old != value && value == true) ScanStarted?.Invoke(this, EventArgs.Empty);
                else if (old != value && value == false) ScanEnded?.Invoke(this, EventArgs.Empty);
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public PageListViewModel()
        {
            ScanResult = ScanResultService.Result;
            ScanResultService.ScanResultCreated += ScanResultService_ScanResultCreated;
            ScanResultService.ScanResultDismissed += ScanResultService_ScanResultDismissed;
            IsScanRunning = ScanService.IsScanInProgress;
            ScanService.ScanStarted += (x, y) => IsScanRunning = true;
            ScanService.ScanEnded += (x, y) => IsScanRunning = false;

            DisposeCommand = new RelayCommand(Dispose);

            Messenger.Register<EditorCurrentIndexChangedMessage>(this, (r, m) => SelectedPageIndex = m.Value);
            SelectedPageIndex = Messenger.Send(new EditorCurrentIndexRequestMessage());
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void Dispose()
        {
            // clean up messenger
            Messenger.UnregisterAll(this);

            // clean up event handlers
            ScanResultService.ScanResultCreated -= ScanResultService_ScanResultCreated;
            ScanResultService.ScanResultDismissed -= ScanResultService_ScanResultDismissed;
        }

        private void ScanResultService_ScanResultDismissed(object sender, EventArgs e)
        {
            ScanResult = null;
        }

        private void ScanResultService_ScanResultCreated(object sender, ScanResult e)
        {
            ScanResult = e;
        }
    }
}
