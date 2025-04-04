using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Extensions;
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
using Windows.UI.Core;


namespace Scanner.Views
{
    public sealed partial class EditorView : Page
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private VirtualizingStackPanel? flipViewPanel;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public EditorView()
        {
            this.InitializeComponent();

            ViewModel.SettingsService.PropertyChanged += SettingsService_PropertyChanged;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void ButtonRotate_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
        }

        private void SettingsService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ISettingsService.SettingEditorOrientation):
                    _ = ApplyFlipViewOrientationAsync(SettingEditorOrientationToOrientation(ViewModel.SettingsService.SettingEditorOrientation));
                    break;
            }
        }

        private async Task ApplyFlipViewOrientationAsync(Orientation orientation)
        {
            await this.RunOnUIThreadAndWaitAsync(DispatcherQueuePriority.Normal, () =>
            {
                if (flipViewPanel != null)
                {
                    flipViewPanel.Orientation = orientation;
                }

                // fix scrolling in vertical mode
                if (orientation == Orientation.Vertical)
                {
                    ((ScrollViewer)VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(FlipViewPages, 0), 0)).HorizontalScrollMode = ScrollMode.Disabled;
                }
                else
                {
                    ((ScrollViewer)VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(FlipViewPages, 0), 0)).HorizontalScrollMode = ScrollMode.Enabled;
                }
            });
        }

        private Orientation SettingEditorOrientationToOrientation(SettingEditorOrientation setting)
        {
            return setting switch
            {
                SettingEditorOrientation.Horizontal => Orientation.Horizontal,
                SettingEditorOrientation.Vertical => Orientation.Vertical,
                _ => Orientation.Horizontal,
            };
        }

        private void VirtualizingStackPanel_Loading(FrameworkElement sender, object args)
        {
            flipViewPanel = sender as VirtualizingStackPanel;
            _ = ApplyFlipViewOrientationAsync(SettingEditorOrientationToOrientation(ViewModel.SettingsService.SettingEditorOrientation));
        }
    }
}
