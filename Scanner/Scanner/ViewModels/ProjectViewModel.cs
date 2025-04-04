using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Scanner.Messages;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scanner.ViewModels
{
    partial class ProjectViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        public readonly IProjectService ProjectService = Ioc.Default.GetRequiredService<IProjectService>();
        public readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        #endregion

        #region Commands
        public AsyncRelayCommand TryCloseProjectAsyncCommand => new AsyncRelayCommand(TryCloseProjectAsync);
        public RelayCommand SelectPreviousPageCommand => new RelayCommand(SelectPreviousPage);
        public RelayCommand SelectNextPageCommand => new RelayCommand(SelectNextPage);
        public RelayCommand ShowSettingsCommand => new RelayCommand(ShowSettings);
        public RelayCommand DisposeCommand => new RelayCommand(Dispose);
        #endregion

        [ObservableProperty]
        private Project? currentProject;

        public string FileName
        {
            get
            {
                if (CurrentProject == null) return string.Empty;

                if (CurrentProject.IsPdf)
                {
                    return Path.GetFileNameWithoutExtension(CurrentProject.TargetFileName);
                }
                else if (ProjectService.SelectedPage is ImagePage imagePage)
                {
                    return Path.GetFileNameWithoutExtension(imagePage.TargetFileName);
                }
                return string.Empty;
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ProjectViewModel()
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

        private void ProjectService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IProjectService.CurrentProject):
                    CurrentProject = ProjectService.CurrentProject;
                    OnPropertyChanged(nameof(FileName));
                    break;
                case nameof(IProjectService.SelectedPage):
                    OnPropertyChanged(nameof(FileName));
                    break;
            }
        }

        private void SelectPreviousPage()
        {
            ProjectService.SelectPreviousPage();
        }

        private void SelectNextPage()
        {
            ProjectService.SelectNextPage();
        }

        private async Task TryCloseProjectAsync()
        {
            await ProjectService.TryCloseProjectAsync();
        }

        private void ShowSettings()
        {
            Messenger.Send(new ShowSettingsMessage());
        }
    }
}
