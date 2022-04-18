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

        private void AppBarButton_Loaded(object sender, RoutedEventArgs e)
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
                Text = "Date",
                Icon = new FontIcon
                {
                    Glyph = "\uE163"
                }
            };
            MenuFlyoutSubItem timeParentItem = new MenuFlyoutSubItem
            {
                Text = "Time",
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
                else
                {
                    MenuFlyoutAddBlock.Items.Add(item);
                }
            }

            MenuFlyoutAddBlock.Items.Insert(2, timeParentItem);
            MenuFlyoutAddBlock.Items.Insert(3, dateParentItem);
        }

        private async void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                FlyoutBase.ShowAttachedFlyout((AppBarButton)sender);
            });
        }

        private void ButtonDeleteBlock_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.DeleteBlockCommand.Execute(((Button)sender).CommandParameter);
        }

        private async void ButtonDoneEditingBlock_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                ListViewItem item = ListViewPattern.ContainerFromItem(((Button)sender).Tag) as ListViewItem;
                FlyoutBase.GetAttachedFlyout(item).Hide();
            });
        }
    }
}
