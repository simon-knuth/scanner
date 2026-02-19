using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Extensions;
using Scanner.Models.ItemNaming;
using Scanner.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using static Scanner.Helpers.Helpers;


namespace Scanner.Views.Settings;

[ObservableObject]
public sealed partial class SettingsViewCustomItemNaming : SettingsPage
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    [ObservableProperty]
    private string heading;

    [ObservableProperty]
    private string body;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public SettingsViewCustomItemNaming()
    {
        this.InitializeComponent();

        ViewModel.CloseRequested += ViewModel_CloseRequested;
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel.Kind = (CustomItemNamingViewModel.ItemNamingKind)e.Parameter;
        switch (ViewModel.Kind)
        {
            case CustomItemNamingViewModel.ItemNamingKind.File:
                Heading = GetLocalized(Scanner.Resources.Strings.ResourcesExtension.KeyEnum.CustomFileNamingDialogHeading);
                Body = GetLocalized(Scanner.Resources.Strings.ResourcesExtension.KeyEnum.SettingsFileNamingCustomPatternExplanation);
                break;
            case CustomItemNamingViewModel.ItemNamingKind.Folder:
                Heading = GetLocalized(Scanner.Resources.Strings.ResourcesExtension.KeyEnum.CustomFolderNamingDialogHeading);
                Body = GetLocalized(Scanner.Resources.Strings.ResourcesExtension.KeyEnum.SettingsFolderNamingCustomPatternExplanation);
                break;
        }

        base.OnNavigatedTo(e);
    }
    
    private async void ListViewPattern_ItemClick(object sender, ItemClickEventArgs e)
    {
        await this.RunOnUIThreadAndWaitAsync(DispatcherQueuePriority.Normal, () =>
        {
            ListViewItem container = (ListViewItem)((ListView)sender).ContainerFromItem(e.ClickedItem);
            FlyoutBase.ShowAttachedFlyout(container);
        });
    }

    private void ButtonAddBlock_Loaded(object sender, RoutedEventArgs e)
    {
        // get all available blocks
        List<IItemNamingBlock> availableBlocks = new List<IItemNamingBlock>();
        foreach (Type type in ItemNamingStatics.ItemNamingBlocksDictionary.Values)
        {
            Type[] parameterTypes = [];
            string[] parameters = [];

            IItemNamingBlock? block = type.GetConstructor(parameterTypes)?.Invoke(parameters) as IItemNamingBlock;
            if (block != null) availableBlocks.Add(block);
        }

        // create parent items for date & time
        MenuFlyoutSubItem dateParentItem = new MenuFlyoutSubItem
        {
            Text = GetLocalized(Scanner.Resources.Strings.ResourcesExtension.KeyEnum.FileNamingBlockDate),
            Icon = new FontIcon
            {
                Glyph = "\uE163"
            }
        };
        MenuFlyoutSubItem timeParentItem = new MenuFlyoutSubItem
        {
            Text = GetLocalized(Scanner.Resources.Strings.ResourcesExtension.KeyEnum.FileNamingBlockTime),
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

            if (block.GetType() == typeof(TextItemNamingBlock))
            {
                MenuFlyoutAddBlock.Items.Add(item);
                MenuFlyoutAddBlock.Items.Add(new MenuFlyoutSeparator());
            }
            else if (block.GetType() == typeof(HourItemNamingBlock)
                || block.GetType() == typeof(MinuteItemNamingBlock)
                || block.GetType() == typeof(SecondItemNamingBlock))
            {
                timeParentItem.Items.Add(item);
            }
            else if (block.GetType() == typeof(HourPeriodItemNamingBlock))
            {
                timeParentItem.Items.Add(new MenuFlyoutSeparator());
                timeParentItem.Items.Add(item);
            }
            else if (block.GetType() == typeof(DayItemNamingBlock)
                || block.GetType() == typeof(MonthItemNamingBlock)
                || block.GetType() == typeof(YearItemNamingBlock))
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
        await this.RunOnUIThreadAndWaitAsync(DispatcherQueuePriority.Normal, () =>
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
        await this.RunOnUIThreadAndWaitAsync(DispatcherQueuePriority.Normal, () =>
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

        await this.RunOnUIThreadAndWaitAsync(DispatcherQueuePriority.High, () =>
        {
            GridTrashDropZone.Visibility = Visibility.Collapsed;
            GridTrashDropZoneHover.Visibility = Visibility.Visible;
        });
    }

    private async void GridTrashDropZones_DragLeave(object sender, DragEventArgs e)
    {
        await this.RunOnUIThreadAndWaitAsync(DispatcherQueuePriority.High, () =>
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

    private async void TextBoxText_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await this.RunOnUIThreadAndWaitAsync(DispatcherQueuePriority.High, () =>
            {
                ListViewItem item = (ListViewItem)ListViewPattern.ContainerFromItem(((TextBox)sender).Tag);
                FlyoutBase.GetAttachedFlyout(item).Hide();
            });
        }
    }

    private async void TextBoxText_Loaded(object sender, RoutedEventArgs e)
    {
        await this.RunOnUIThreadAndWaitAsync(DispatcherQueuePriority.Normal, () =>
        {
            ((TextBox)sender).Focus(FocusState.Programmatic);
            ((TextBox)sender).SelectAll();
        });
    }

    private void ListViewItemPattern_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Delete)
        {
            ListViewItem item = (ListViewItem)sender;
            ViewModel.DeleteBlockCommand.Execute(item.DataContext);
        }
    }

    private void ViewModel_CloseRequested(object? sender, EventArgs e)
    {
        OnGoBackRequested();
    }

    private void ButtonCancel_Click(object sender, RoutedEventArgs e)
    {
        OnGoBackRequested();
    }
}
