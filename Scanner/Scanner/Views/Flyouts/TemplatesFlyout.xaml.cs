using CommunityToolkit.Mvvm.ComponentModel;
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


namespace Scanner.Views.Flyouts;

[ObservableObject]
public sealed partial class TemplatesFlyout : Flyout
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Dependency Properties
    public static readonly DependencyProperty ScanOptionsProperty =
        DependencyProperty.Register(nameof(ScanOptions), typeof(ScanOptions), typeof(TemplatesFlyout), null);
    #endregion

    public double DesiredWidth;

    private ScanOptions? scanOptions;
    public ScanOptions? ScanOptions
    {
        get => scanOptions;
        set
        {
            SetValue(ScanOptionsProperty, value);
            SetProperty(ref scanOptions, value);
        }
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public TemplatesFlyout(double desiredWidth)
    {
        DesiredWidth = desiredWidth;

        this.InitializeComponent();
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void GridRoot_Loading(FrameworkElement sender, object args)
    {
        sender.MinWidth = sender.MaxWidth = DesiredWidth;
    }

    private void TemplatesView_CloseRequested(object sender, EventArgs e)
    {
        Hide();
    }
}
