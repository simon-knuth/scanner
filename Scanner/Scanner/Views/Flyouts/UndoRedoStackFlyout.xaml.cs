using ABI.System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;


namespace Scanner.Views.Flyouts;

public sealed partial class UndoRedoStackFlyout : MenuFlyout
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Events
    public event EventHandler<IProjectAction> ActionSelected;
    #endregion

    private Stack<IProjectAction> stack;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public UndoRedoStackFlyout(Stack<IProjectAction> stack)
    {
        this.stack = stack;
        GenerateItems();

        this.InitializeComponent();
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void GenerateItems()
    {
        foreach (IProjectAction action in stack)
        {
            MenuFlyoutItem item = new MenuFlyoutItem
            {
                Text = action.GetFriendlyName(),
                Tag = action
            };
            item.Click += Item_Click;
            Items.Add(item);
        }
    }

    private void Item_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item)
        {
            ActionSelected?.Invoke(this, (IProjectAction)item.Tag);
        }
    }
}
