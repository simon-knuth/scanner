using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;


namespace Scanner.Views;

public sealed partial class TemplatesView : Page
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Events
    public event EventHandler CloseRequested;
    #endregion


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public TemplatesView()
    {
        this.InitializeComponent();
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void ListViewTemplates_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TemplateEntry template)
        {
            ViewModel.TryApplyTemplate(template);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void TextBoxTemplateName_LostFocus(object sender, RoutedEventArgs e)
    {
        TextBox textBox = (TextBox)sender;
        TemplateEntry template = (TemplateEntry)textBox.Tag;

        // The TwoWay binding uses UpdateSourceTrigger=LostFocus, but focus here is lost because the
        // TextBox was just disabled (Enter) — a disabled control does not flush its source update, so
        // template.Name would still hold the old value. Capture the edited text directly instead.
        ViewModel.StopRenamingAsync(template, textBox.Text);
    }

    private void TextBoxTemplateName_GotFocus(object sender, RoutedEventArgs e)
    {
        TextBox textBox = (TextBox)sender;
        textBox.SelectAll();
    }

    private void TextBoxTemplateName_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            TextBox textBox = (TextBox)sender;
            textBox.StartBringIntoView();
            textBox.Focus(FocusState.Programmatic);
        }
    }

    private void TextBoxTemplateName_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is Windows.System.VirtualKey.Enter or Windows.System.VirtualKey.Accept)
        {
            e.Handled = true;
            ((TextBox)sender).IsEnabled = false;
        }
        else if (e.Key is Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            TextBox textBox = (TextBox)sender;
            textBox.Text = ((TemplateEntry)textBox.Tag).Name;
            textBox.IsEnabled = false;
        }
    }
}
