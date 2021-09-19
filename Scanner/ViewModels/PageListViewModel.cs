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
            set => SetProperty(ref _IsScanRunning, value);
        }

        private bool _IsScanResultChanging;
        public bool IsScanResultChanging
        {
            get => _IsScanResultChanging;
            set => SetProperty(ref _IsScanResultChanging, value);
        }

        private bool _IsEditorEditing;
        public bool IsEditorEditing
        {
            get => _IsEditorEditing;
            set => SetProperty(ref _IsEditorEditing, value);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public PageListViewModel()
        {
            ScanResult = ScanResultService.Result;
            ScanResultService.ScanResultCreated += ScanResultService_ScanResultCreated;
            ScanResultService.ScanResultDismissed += ScanResultService_ScanResultDismissed;
            IsScanResultChanging = ScanResultService.IsScanResultChanging;
            ScanResultService.ScanResultChanging += ScanResultService_ScanResultChanging;
            ScanResultService.ScanResultChanged += ScanResultService_ScanResultChanged;
            IsScanRunning = ScanService.IsScanInProgress;
            ScanService.ScanStarted += ScanService_ScanStarted;
            ScanService.ScanEnded += ScanService_ScanEnded;

            DisposeCommand = new RelayCommand(Dispose);

            Messenger.Register<EditorCurrentIndexChangedMessage>(this, (r, m) => SelectedPageIndex = m.Value);
            SelectedPageIndex = Messenger.Send(new EditorCurrentIndexRequestMessage());

            Messenger.Register<EditorIsEditingChangedMessage>(this, (r, m) => IsEditorEditing = m.Value);
            IsEditorEditing = Messenger.Send(new EditorIsEditingRequestMessage());
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
            ScanResultService.ScanResultChanging -= ScanResultService_ScanResultChanging;
            ScanResultService.ScanResultChanged -= ScanResultService_ScanResultChanged;
            ScanService.ScanStarted -= ScanService_ScanStarted;
            ScanService.ScanEnded -= ScanService_ScanEnded;
        }

        private void ScanResultService_ScanResultDismissed(object sender, EventArgs e)
        {
            ScanResult = null;
        }

        private void ScanResultService_ScanResultCreated(object sender, ScanResult e)
        {
            ScanResult = e;
        }

        private void ScanResultService_ScanResultChanged(object sender, EventArgs e)
        {
            IsScanResultChanging = false;
        }

        private void ScanResultService_ScanResultChanging(object sender, EventArgs e)
        {
            IsScanResultChanging = true;
        }

        private void ScanService_ScanEnded(object sender, EventArgs e)
        {
            IsScanRunning = false;
        }

        private void ScanService_ScanStarted(object sender, EventArgs e)
        {
            IsScanRunning = true;
        }
    }
}
