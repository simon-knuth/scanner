using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.UI.Xaml.Controls;
using Scanner.Models.FileNaming;
using System;
using System.Collections.Generic;
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

        private async void ListViewPattern_ItemClick(object sender, ItemClickEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                ListViewItem container = ((ListView)sender).ContainerFromItem(e.ClickedItem) as ListViewItem;
                FlyoutBase.ShowAttachedFlyout(container);
            });
        }

        private void ButtonAddBlock_Loaded(object sender, RoutedEventArgs e)
        {
            // get all available blocks
            List<IFileNamingBlock> availableBlocks = new List<IFileNamingBlock>();
            foreach (Type type in FileNamingStatics.FileNamingBlocksDictionary.Values)
            {
                Type[] parameterTypes = new Type[0];
                string[] parameters = new string[0];

                IFileNamingBlock block = type.GetConstructor(parameterTypes).Invoke(parameters) as IFileNamingBlock;
                availableBlocks.Add(block);
            }

            // create parent items for date & time
            MenuFlyoutSubItem dateParentItem = new MenuFlyoutSubItem
            {
                Text = LocalizedString("HeadingFileNamingBlockDate/Text"),
                Icon = new FontIcon
                {
                    Glyph = "\uE163"
                }
            };
            MenuFlyoutSubItem timeParentItem = new MenuFlyoutSubItem
            {
                Text = LocalizedString("HeadingFileNamingBlockTime/Text"),
                Icon = new FontIcon
                {
                    Glyph = "\uE121"
                }
            };

            // create remaining items
            foreach (var block in availableBlocks)
            {
                MenuFlyoutItem item = new MenuFlyoutItem
                {
                    Text = block.DisplayName,
                };

                if (block.Glyph != null)
                {
                    item.Icon = new FontIcon
                    {
                        Glyph = block.Glyph
                    };
                }

                item.Command = ViewModel.AddBlockCommand;
                item.CommandParameter = block.Name;

                if (block.GetType() == typeof(TextFileNamingBlock))
                {
                    MenuFlyoutAddBlock.Items.Add(item);
                    MenuFlyoutAddBlock.Items.Add(new MenuFlyoutSeparator());
                }
                else if (block.GetType() == typeof(HourFileNamingBlock)
                    || block.GetType() == typeof(MinuteFileNamingBlock)
                    || block.GetType() == typeof(SecondFileNamingBlock))
                {
                    timeParentItem.Items.Add(item);
                }
                else if (block.GetType() == typeof(HourPeriodFileNamingBlock))
                {
                    timeParentItem.Items.Add(new MenuFlyoutSeparator());
                    timeParentItem.Items.Add(item);
                }
                else if (block.GetType() == typeof(DayFileNamingBlock)
                    || block.GetType() == typeof(MonthFileNamingBlock)
                    || block.GetType() == typeof(YearFileNamingBlock))
                {
                    dateParentItem.Items.Add(item);
                }
                else
                {
                    MenuFlyoutAddBlock.Items.Add(item);
                }
            }

            MenuFlyoutAddBlock.Items.Insert(2, timeParentItem);
            MenuFlyoutAddBlock.Items.Insert(3, dateParentItem);
        }

        private async void ListViewPattern_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                ButtonAddBlock.Visibility = Visibility.Collapsed;
                ButtonClearPattern.Visibility = Visibility.Collapsed;
                GridTrashDropZones.Visibility = Visibility.Visible;
                GridTrashDropZone.Visibility = Visibility.Visible;
                GridTrashDropZoneHover.Visibility = Visibility.Collapsed;

                e.Data.SetText(ListViewPattern.Items.IndexOf(e.Items[0]).ToString());
                e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            });
        }

        private async void ListViewPattern_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                ButtonAddBlock.Visibility = Visibility.Visible;
                ButtonClearPattern.Visibility = Visibility.Visible;
                GridTrashDropZones.Visibility = Visibility.Collapsed;
                GridTrashDropZone.Visibility = Visibility.Collapsed;
                GridTrashDropZoneHover.Visibility = Visibility.Collapsed;
            });
        }

        private async void GridTrashDropZones_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            e.DragUIOverride.IsCaptionVisible = false;
            e.DragUIOverride.IsGlyphVisible = false;
            e.Handled = true;

            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                GridTrashDropZone.Visibility = Visibility.Collapsed;
                GridTrashDropZoneHover.Visibility = Visibility.Visible;
            });
        }

        private async void GridTrashDropZones_DragLeave(object sender, DragEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                GridTrashDropZone.Visibility = Visibility.Visible;
                GridTrashDropZoneHover.Visibility = Visibility.Collapsed;
            });
        }

        private async void GridTrashDropZones_Drop(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;

            int blockIndex = int.Parse(await e.DataView.GetTextAsync());
            ViewModel.DeleteBlockCommand.Execute(ViewModel.SelectedBlocks[blockIndex]);
        }

        private async void TextBoxText_KeyUp(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
                {
                    ListViewItem item = ListViewPattern.ContainerFromItem(((TextBox)sender).Tag) as ListViewItem;
                    FlyoutBase.GetAttachedFlyout(item).Hide();
                });
            }
        }

        private async void TextBoxText_Loaded(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                ((TextBox)sender).Focus(FocusState.Programmatic);
                ((TextBox)sender).SelectAll();
            });
        }

        private void ListViewItemPattern_KeyUp(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Delete)
            {
                ListViewItem item = sender as ListViewItem;
                ViewModel.DeleteBlockCommand.Execute(item.DataContext);
            }
        }
    }
}
