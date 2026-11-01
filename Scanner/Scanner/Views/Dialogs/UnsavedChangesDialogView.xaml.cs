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
using Windows.Foundation;
using Windows.Foundation.Collections;


namespace Scanner.Views.Dialogs;

public partial class UnsavedChangesDialogView : ContentDialog
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetRequiredService<IAccessibilityService>();
    #endregion


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <remarks>
    /// Only asks the user. Saving is up to the caller once the dialog has closed, as only one dialog can be shown at a
    /// time and saving may need to ask for a save location.
    /// </remarks>
    public UnsavedChangesDialogView()
    {
        this.InitializeComponent();
        Ioc.Default.GetService<ILogService>()?.Log.Information("Dialog loaded");
    }
}
