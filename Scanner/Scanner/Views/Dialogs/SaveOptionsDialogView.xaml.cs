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
using Scanner.ViewModels;
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
        [ObservableProperty]
        private double folderFlyoutWidth;

        public SaveOptions? SaveOptions => ViewModel.SaveOptions;

        private SaveOptionsDialogViewModel ViewModel;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SaveOptionsDialogView(ScanOptions scanOptions, Project? project)
        {
            ViewModel = new SaveOptionsDialogViewModel(scanOptions, project);

            this.InitializeComponent();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void TextBoxFileName_GotFocus(object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        private void DropDownButtonFolder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            FolderFlyoutWidth = e.NewSize.Width;
        }

        private void MenuFlyoutFolder_Opening(object sender, object e)
        {
            MenuFlyout menuFlyout = (MenuFlyout)sender;

            // clear entries
            while (menuFlyout.Items.Count > 2)
            {
                menuFlyout.Items.RemoveAt(0);
            }

            // generate entries
            if (ViewModel.RecentFolders != null)
            {
                for (int i = 0; i < ViewModel.RecentFolders.Count; i++)
                {
                    MenuFlyoutItemBase item = (MenuFlyoutItemBase)DataTemplateRecentFolder.LoadContent();
                    item.DataContext = ViewModel.RecentFolders[i];
                    menuFlyout.Items.Insert(i, item);
                }
            }

            // remove separator if list is empty
            if (menuFlyout.Items.Count == 2)
            {
                menuFlyout.Items.RemoveAt(0);
            }
        }

        private void MenuFlyoutItemRecentFolder_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectedFolder = (StorageFolder)((MenuFlyoutItem)sender).CommandParameter;
        }
    }
}
