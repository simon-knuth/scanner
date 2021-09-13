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

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public PageListViewModel()
        {
            ScanResult = ScanResultService.Result;
            ScanResultService.ScanResultCreated += ScanResultService_ScanResultCreated;
            ScanResultService.ScanResultDismissed += ScanResultService_ScanResultDismissed;

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
