using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Models;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUIEx;
using static Scanner.Helpers.Helpers;


namespace Scanner.Views.Dialogs
{
    [ObservableObjectAttribute]
    public partial class SaveOptionsDialogView : ContentDialog
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private ISaveLocationService SaveLocationService = Ioc.Default.GetRequiredService<ISaveLocationService>();
        #endregion

        public SaveOptions? SaveOptions { get; private set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedFolderPathWithoutFolder))]
        [NotifyPropertyChangedFor(nameof(AreValidOptionsSelected))]
        private StorageFolder? selectedFolder;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AreValidOptionsSelected))]
        private string fileDisplayName;

        public string? SelectedFolderPathWithoutFolder => SelectedFolder?.Path.Substring(0, SelectedFolder.Path.LastIndexOf(Path.DirectorySeparatorChar)) ?? string.Empty;

        public string FileExtension => TargetFormatToFileExtension(scanOptions.TargetFormat);

        public bool IsPdf => scanOptions.TargetFormat == TargetFormat.PDF;

        public bool AreValidOptionsSelected => SelectedFolder != null && IsValidFileName(FileDisplayName);

        private ScanOptions scanOptions;

        private Project? project;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SaveOptionsDialogView(ScanOptions scanOptions, Project? project)
        {
            this.scanOptions = scanOptions;
            this.project = project;

            this.InitializeComponent();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (AreValidOptionsSelected)
            {
                SaveOptions = new SaveOptions(SelectedFolder!, FileDisplayName + FileExtension);
            }
        }

        private async void ButtonSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            // create picker
            FolderPicker picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, ((App)Application.Current).MainWindow.GetWindowHandle());

            // pick folder
            StorageFolder? folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                SelectedFolder = folder;
            }
        }

        private async void ContentDialog_Loading(FrameworkElement sender, object args)
        {
            if (project != null && project.TargetFolder != null)
            {
                SelectedFolder = project.TargetFolder;
            }
            else
            {
                SelectedFolder = await SaveLocationService.GetFixedSaveLocationAsync();
            }
        }
    }
}
