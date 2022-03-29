using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.UI.Xaml.Controls;
using Scanner.Models.FileNaming;
using System;
using Windows.Globalization.NumberFormatting;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using static Utilities;

namespace Scanner.Views.Dialogs
{
    public sealed partial class CustomFileNamingDialogView : ContentDialog
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public CustomFileNamingDialogView()
        {
            this.InitializeComponent();

            ViewModel.CloseRequested += ViewModel_CloseRequested;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private async void ViewModel_CloseRequested(object sender, EventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                this.Hide();
            });
        }

        private void ContentDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            if (args.Result == ContentDialogResult.Primary)
            {
                ViewModel.AcceptCommand.Execute(null);
            }
            else
            {
                ViewModel.CancelCommand.Execute(null);
            }
        }

        private async void AdaptiveGridViewPattern_ItemClick(object sender, ItemClickEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                GridViewItem container = ((AdaptiveGridView)sender).ContainerFromItem(e.ClickedItem) as GridViewItem;
                FlyoutBase.ShowAttachedFlyout(container.ContentTemplateRoot as FrameworkElement);
            });
        }

        private async void AdaptiveGridViewPattern_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetView().AvailableFormats.Count > 0)
            {
                // new block
                string block = await e.Data.GetView().GetTextAsync();

                Type type = FileNamingStatics.FileNamingBlocksDictionary[block];
                Type[] parameterTypes = new Type[0];
                string[] parameters = new string[0];

                ViewModel.SelectedBlocks.Add(type.GetConstructor(parameterTypes).Invoke(parameters) as IFileNamingBlock);
                e.Handled = true;
            }
            else
            {
                // existing block
                e.Handled = false;
            }            
        }

        private void AdaptiveGridViewPattern_DragEnter(object sender, DragEventArgs e)
        {
            
        }

        private void AdaptiveGridViewPattern_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            e.DragUIOverride.IsCaptionVisible = false;
            e.DragUIOverride.IsGlyphVisible = false;
            e.Handled = true;
        }

        private void AdaptiveGridViewBlocks_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            e.Data.SetText(e.Items[0] as string);
        }
    }
}
