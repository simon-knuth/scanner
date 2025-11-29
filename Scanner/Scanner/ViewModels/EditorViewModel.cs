using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Media.Imaging;
using Scanner.Extensions;
using Scanner.Helpers;
using Scanner.Messages;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using static Scanner.Helpers.PageDimensionsHelper;
using static Scanner.Helpers.RotationHelpers;
using static Scanner.Models.ImagePage;

namespace Scanner.ViewModels
{
    partial class EditorViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        public readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        public readonly IProjectService ProjectService = Ioc.Default.GetRequiredService<IProjectService>();
        public readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        #endregion

        #region Commands
        public AsyncRelayCommand RotateCurrentPage90DegreesAsyncCommand => new AsyncRelayCommand(async (x) => await RotateCurrentPageAsync(RotationIntent.Degrees90));
        public AsyncRelayCommand RotateCurrentPage180DegreesAsyncCommand => new AsyncRelayCommand(async (x) => await RotateCurrentPageAsync(RotationIntent.Degrees180));
        public AsyncRelayCommand RotateCurrentPage270DegreesAsyncCommand => new AsyncRelayCommand(async (x) => await RotateCurrentPageAsync(RotationIntent.Degrees270));
        public AsyncRelayCommand RotateCurrentPageAutomaticallyAsyncCommand => new AsyncRelayCommand(async (x) => await RotateCurrentPageAsync(RotationIntent.Automatic));
        public AsyncRelayCommand RemoveCurrentPageAsyncCommand => new AsyncRelayCommand(RemoveCurrentPageAsync);
        public AsyncRelayCommand<ImageFilter> ApplyFilterToCurrentPageAsyncCommand => new AsyncRelayCommand<ImageFilter>(ApplyFilterToCurrentPageAsync);
        public AsyncRelayCommand<Rect> CropCurrentPageAsyncCommand => new AsyncRelayCommand<Rect>(async (x) => await CropPagesAsync([ProjectService.SelectedPage], x, false));
        public AsyncRelayCommand<Rect> CropCurrentPageAsCopyAsyncCommand => new AsyncRelayCommand<Rect>(async (x) => await CropPagesAsync([ProjectService.SelectedPage], x, true));
        public AsyncRelayCommand<(List<IProjectPage>, Rect)> CropPagesAsyncCommand => new AsyncRelayCommand<(List<IProjectPage>, Rect)>(async (x) => await CropPagesAsync(x.Item1, x.Item2, false));
        public AsyncRelayCommand<int> SetBrightnessForCurrentPageCommand => new AsyncRelayCommand<int>(SetBrightnessForCurrentPageAsync);
        public AsyncRelayCommand<int> SetContrastForCurrentPageCommand => new AsyncRelayCommand<int>(SetContrastForCurrentPageAsync);
        public AsyncRelayCommand ResetBrightnessCommand => new AsyncRelayCommand(async () => await SetBrightnessForCurrentPageAsync(AppConfig.DefaultBrightness));
        public AsyncRelayCommand ResetContrastCommand => new AsyncRelayCommand(async () => await SetContrastForCurrentPageAsync(AppConfig.DefaultContrast));
        public RelayCommand ShowSettingsCommand => new RelayCommand(ShowSettings);
        public RelayCommand DisposeCommand => new RelayCommand(Dispose);
        #endregion

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AreSimilarPagesForCropAvailable))]
        [NotifyPropertyChangedFor(nameof(SimilarPagesForCrop))]
        private ProjectBase? currentProject;

        private AspectRatio selectedAspectRatio;
        public AspectRatio SelectedAspectRatio
        {
            get => selectedAspectRatio;
            set
            {
                if (SetProperty(ref selectedAspectRatio, value))
                {
                    SelectedAspectRatioValue = value.ToValue();
                    SettingsService.LastUsedCropAspectRatio = value;
                }
            }
        }

        [ObservableProperty]
        private double? selectedAspectRatioValue;

        public bool AreSimilarPagesForCropAvailable => GetAreSimilarPagesForCropAvailable();
        public List<IProjectPage> SimilarPagesForCrop => GetSimilarPagesForCrop();

        public int PageBrightness
        {
            get
            {
                if (ProjectService.SelectedPage is not ImagePage imagePage)
                    return AppConfig.DefaultBrightness;

                return imagePage.Brightness;
            }
        }

        public int PageContrast
        {
            get
            {
                if (ProjectService.SelectedPage is not ImagePage imagePage)
                    return AppConfig.DefaultContrast;

                return imagePage.Contrast;
            }
        }

        public double PageBrightnessDouble
        {
            get => PageBrightness;
        }

        public double PageContrastDouble
        {
            get => PageContrast;
        }

        public bool CanResetBrightness => PageBrightness != 0;
        public bool CanResetContrast => PageContrast != 0;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public EditorViewModel()
        {
            ProjectService.PropertyChanging += ProjectService_PropertyChanging;
            ProjectService.PropertyChanged += ProjectService_PropertyChanged;
            CurrentProject = ProjectService.CurrentProject;

            SelectedAspectRatio = SettingsService.LastUsedCropAspectRatio;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void Dispose()
        {
            Messenger.UnregisterAll(this);
        }

        private void ProjectService_PropertyChanging(object? sender, System.ComponentModel.PropertyChangingEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IProjectService.SelectedPage):
                    if (ProjectService.SelectedPage != null)
                        ProjectService.SelectedPage.PropertyChanged -= SelectedPage_PropertyChanged;
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
                case nameof(IProjectService.SelectedPage):
                    OnPropertyChanged(nameof(AreSimilarPagesForCropAvailable));
                    OnPropertyChanged(nameof(SimilarPagesForCrop));
                    OnPropertyChanged(nameof(PageBrightness));
                    OnPropertyChanged(nameof(PageContrast));
                    OnPropertyChanged(nameof(PageBrightnessDouble));
                    OnPropertyChanged(nameof(PageContrastDouble));
                    OnPropertyChanged(nameof(CanResetBrightness));
                    OnPropertyChanged(nameof(CanResetContrast));

                    if (ProjectService.SelectedPage != null)
                        ProjectService.SelectedPage.PropertyChanged += SelectedPage_PropertyChanged;
                    break;
            }
        }

        private void SelectedPage_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ImagePage.Brightness):
                    OnPropertyChanged(nameof(PageBrightness));
                    OnPropertyChanged(nameof(PageBrightnessDouble));
                    OnPropertyChanged(nameof(CanResetBrightness));
                    break;
                case nameof(ImagePage.Contrast):
                    OnPropertyChanged(nameof(PageContrast));
                    OnPropertyChanged(nameof(PageContrastDouble));
                    OnPropertyChanged(nameof(CanResetContrast));
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

        private async Task ApplyFilterToCurrentPageAsync(ImageFilter filter)
        {
            if (CurrentProject == null) return;
            if (ProjectService.SelectedPage == null) return;

            if (ProjectService.SelectedPage is ImagePage imagePage)
            {
                await ProjectService.ApplyActionAsync(new ApplyFilterAction([imagePage], filter));
            }
        }

        private async Task CropPagesAsync(List<IProjectPage> pages, Rect cropRegion, bool asCopy)
        {
            if (CurrentProject == null) return;
            Task process;

            if (asCopy)
                process = ProjectService.ApplyActionAsync(new CropPagesAsCopyAction(pages, cropRegion));
            else
                process = ProjectService.ApplyActionAsync(new CropPagesAction(pages, cropRegion));

            if (pages.Count > 1)
                Messenger.Send(new ShowIndeterminateProgressDialogMessage(Resources.Strings.Resources.ApplyingChanges, process));

            await process;
        }

        private bool GetAreSimilarPagesForCropAvailable()
        {
            if (CurrentProject == null) return false;
            if (ProjectService.SelectedPage == null) return false;

            if (ProjectService.SelectedPage is ImagePage selectedImagePage)
            {
                // look for other page with same dimensions
                foreach (ImagePage imagePage in CurrentProject.Pages.OfType<ImagePage>())
                {
                    if (imagePage == ProjectService.SelectedPage) continue;

                    if (imagePage.Width == selectedImagePage.Width && imagePage.Height == selectedImagePage.Height)
                        return true;
                }
            }

            return false;
        }

        private List<IProjectPage> GetSimilarPagesForCrop()
        {
            if (CurrentProject == null) return [];
            if (ProjectService.SelectedPage == null) return [];

            List<IProjectPage> result = [];
            if (ProjectService.SelectedPage is ImagePage selectedImagePage)
            {
                // look for other page with same dimensions
                foreach (ImagePage imagePage in CurrentProject.Pages.OfType<ImagePage>())
                {
                    if (imagePage == ProjectService.SelectedPage) continue;

                    if (imagePage.Width == selectedImagePage.Width && imagePage.Height == selectedImagePage.Height)
                        result.Add(imagePage);
                }
            }

            return result;
        }

        private async Task SetBrightnessForCurrentPageAsync(int brightness)
        {
            if (CurrentProject == null) return;
            if (ProjectService.SelectedPage == null) return;

            if (ProjectService.SelectedPage is ImagePage imagePage && imagePage.Brightness != brightness)
            {
                await ProjectService.ApplyActionAsync(new SetBrightnessAction(imagePage, brightness));
            }
        }

        private async Task SetContrastForCurrentPageAsync(int contrast)
        {
            if (CurrentProject == null) return;
            if (ProjectService.SelectedPage == null) return;

            if (ProjectService.SelectedPage is ImagePage imagePage && imagePage.Contrast != contrast)
            {
                await ProjectService.ApplyActionAsync(new SetContrastAction(imagePage, contrast));
            }
        }
    }
}
