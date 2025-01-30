using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Scanner.Models;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scanner.ViewModels
{
    partial class ScanActionsViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private readonly IProjectService ProjectService = Ioc.Default.GetRequiredService<IProjectService>();
        #endregion

        #region Commands
        public AsyncRelayCommand ScanCommand => new AsyncRelayCommand(ScanAsync);

        public RelayCommand DisposeCommand => new RelayCommand(Dispose);
        #endregion

        private ScanOptions? scanOptions;
        public ScanOptions? ScanOptions
        {
            get => scanOptions;
            set
            {
                if (scanOptions != null)
                {
                    scanOptions.PropertyChanged -= ScanOptions_PropertyChanged;
                }

                SetProperty(ref scanOptions, value);
                OnPropertyChanged(nameof(CanScan));

                if (value != null)
                {
                    value.PropertyChanged += ScanOptions_PropertyChanged;
                }
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanScan))]
        private bool isScanning;

        public bool CanScan => ScanOptions?.Scanner != null && !IsScanning;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanActionsViewModel()
        {
            ProjectService.IsScanProcessRunningChanged += ProjectService_IsScanProcessRunningChanged;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void Dispose()
        {
            Messenger.UnregisterAll(this);
        }

        private async Task ScanAsync()
        {
            await ProjectService.TryCreateProjectAsync(ScanOptions.Scanner);
        }

        private void ScanOptions_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ScanOptions.Scanner):
                    OnPropertyChanged(nameof(CanScan));
                    break;
            }
        }

        private void ProjectService_IsScanProcessRunningChanged(object? sender, bool e)
        {
            IsScanning = e;
        }
    }
}
