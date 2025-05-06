using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Scanner.Messages;
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
        public readonly IProjectService ProjectService = Ioc.Default.GetRequiredService<IProjectService>();
        private readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        #endregion

        #region Commands
        public AsyncRelayCommand<bool> ScanCommand => new AsyncRelayCommand<bool>(ScanAsync);
        public RelayCommand ShowSettingsCommand => new RelayCommand(ShowSettings);

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
                OnPropertyChanged(nameof(CanPreviewScan));

                if (value != null)
                {
                    value.PropertyChanged += ScanOptions_PropertyChanged;
                }
            }
        }

        [ObservableProperty]
        private bool isScanning;

        public bool CanScan => ScanOptions?.Scanner != null && !ProjectService.IsProcessRunning;
        public bool CanPreviewScan => ScanOptions?.Scanner != null && !ProjectService.IsProcessRunning && ScanOptions.Scanner.IsPreviewSupported(ScanOptions.SourceMode);
        public bool CanScanModeBeSwitched => CurrentProject != null && !ProjectService.IsProcessRunning && CanAddToProject;
        public bool CanScanAndMerge => CurrentProject != null && CurrentProject.Format == ScanOptions?.TargetFormat && CurrentProject.Format == TargetFormat.PDF 
            && ScanOptions?.SourceMode == ScannerSource.Feeder;

        private bool canAddToProject;
        public bool CanAddToProject
        {
            get => canAddToProject;
            set
            {
                if (SetProperty(ref canAddToProject, value))
                {
                    if (value == true)
                    {
                        // can add to project
                        if (SettingsService.SettingScanAction == SettingScanAction.AddToExisting)
                        {
                            AddToProject = true;
                        }
                    }
                    else
                    {
                        // can't add to project anymore
                        AddToProject = false;
                    }
                }
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanScanModeBeSwitched))]
        [NotifyPropertyChangedFor(nameof(CanScanAndMerge))]
        private ProjectBase? currentProject;

        [ObservableProperty]
        private bool addToProject;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanActionsViewModel()
        {
            PropertyChanged += ScanActionsViewModel_PropertyChanged;
            ProjectService.PropertyChanged += ProjectService_PropertyChanged;
            CurrentProject = ProjectService.CurrentProject;
            UpdateCanAddToProject();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void Dispose()
        {
            Messenger.UnregisterAll(this);
        }

        private async Task ScanAsync(bool addToProject)
        {
            if (ScanOptions == null) return;

            if (addToProject)
            {
                await ProjectService.TryScanToProjectAsync(ScanOptions);
            }
            else
            {
                await ProjectService.TryCreateProjectAsync(ScanOptions);
            }
        }

        private void ScanActionsViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(CurrentProject):
                case nameof(CanScanModeBeSwitched):
                    UpdateCanAddToProject();
                    break;
            }
        }

        private void ScanOptions_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ScanOptions.Scanner):
                    OnPropertyChanged(nameof(CanScan));
                    OnPropertyChanged(nameof(CanPreviewScan));
                    break;
                case nameof(ScanOptions.SourceMode):
                    OnPropertyChanged(nameof(CanPreviewScan));
                    OnPropertyChanged(nameof(CanScanAndMerge));
                    break;
                case nameof(ScanOptions.TargetFormat):
                    OnPropertyChanged(nameof(CanScanModeBeSwitched));
                    UpdateCanAddToProject();
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
                case nameof(IProjectService.IsProcessRunning):
                    OnPropertyChanged(nameof(CanScan));
                    OnPropertyChanged(nameof(CanPreviewScan));
                    OnPropertyChanged(nameof(CanScanModeBeSwitched));
                    break;
            }
        }

        private void UpdateCanAddToProject()
        {
            CanAddToProject = CurrentProject != null && CurrentProject.Format == ScanOptions?.TargetFormat;
        }

        private void ShowSettings()
        {
            Messenger.Send(new ShowSettingsMessage());
        }
    }
}
