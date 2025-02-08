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
        private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
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

        [ObservableProperty]
        private Project? currentProject;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanActionsViewModel()
        {
            ProjectService.PropertyChanged += ProjectService_PropertyChanged;
            CurrentProject = ProjectService.CurrentProject;
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
            await ProjectService.TryCreateProjectAsync(ScanOptions);
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

        private void ProjectService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IProjectService.CurrentProject):
                    CurrentProject = ProjectService.CurrentProject;
                    break;
                case nameof(IProjectService.IsScanProcessRunning):
                    IsScanning = ProjectService.IsScanProcessRunning;
                    break;
            }
        }
    }
}
