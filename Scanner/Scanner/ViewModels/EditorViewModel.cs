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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using static Scanner.Helpers.RotationHelpers;

namespace Scanner.ViewModels
{
    partial class EditorViewModel : ObservableRecipient, IDisposable
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
        public AsyncRelayCommand RotateCurrentPage90DegreesAsyncCommand => new AsyncRelayCommand(async (x) => await RotateCurrentPageAsync(RotationIntent.Degrees90));
        public AsyncRelayCommand RotateCurrentPage180DegreesAsyncCommand => new AsyncRelayCommand(async (x) => await RotateCurrentPageAsync(RotationIntent.Degrees180));
        public AsyncRelayCommand RotateCurrentPage270DegreesAsyncCommand => new AsyncRelayCommand(async (x) => await RotateCurrentPageAsync(RotationIntent.Degrees270));
        public AsyncRelayCommand RotateCurrentPageAutomaticallyAsyncCommand => new AsyncRelayCommand(async (x) => await RotateCurrentPageAsync(RotationIntent.Automatic));
        public AsyncRelayCommand RemoveCurrentPageAsyncCommand => new AsyncRelayCommand(RemoveCurrentPageAsync);
        public RelayCommand ShowSettingsCommand => new RelayCommand(ShowSettings);
        public RelayCommand DisposeCommand => new RelayCommand(Dispose);
        #endregion

        [ObservableProperty]
        private Project? currentProject;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public EditorViewModel()
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
                    break;
            }
        }

        private void ShowSettings()
        {
            Messenger.Send(new ShowSettingsMessage());
        }

        private async Task RotateCurrentPageAsync(RotationIntent rotationIntent)
        {
            if (CurrentProject == null) return;
            if (ProjectService.SelectedPage == null) return;

            await ProjectService.ApplyActionAsync(new RotatePagesAction(new()
            {
                { ProjectService.SelectedPage, rotationIntent }
            }));
        }

        private async Task RemoveCurrentPageAsync()
        {
            if (CurrentProject == null) return;
            if (ProjectService.SelectedPage == null) return;
            
            await ProjectService.ApplyActionAsync(new RemovePagesAction(new()
            {
                ProjectService.SelectedPage
            }));
        }
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    ///     Available aspect ratio options for cropping or selecting a region.
    /// </summary>
    public enum AspectRatioOption
    {
        Custom = 0,
        Square = 1,
        ThreeByTwo = 2,
        FourByThree = 3,
        DinA = 4,
        AnsiA = 5,
        AnsiB = 6,
        AnsiC = 7,
        Kai4 = 8,
        Kai8 = 9,
        Kai16 = 10,
        Kai32 = 11,
        Legal = 12
    }
}
