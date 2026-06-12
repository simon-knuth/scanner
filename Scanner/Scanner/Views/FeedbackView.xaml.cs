using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Services.Interfaces;
using Scanner.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;


namespace Scanner.Views;

[ObservableObjectAttribute]
public sealed partial class FeedbackView : Page
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetRequiredService<IAccessibilityService>();
    private readonly ISentryService? SentryService = Ioc.Default.GetService<ISentryService>();
    #endregion

    #region Events
    public event EventHandler FeedbackSent;
    #endregion

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private string email;

    [ObservableProperty]
    private int selectedFeedbackTypeIndex = 0;

    public FeedbackType SelectedFeedbackType => (FeedbackType)SelectedFeedbackTypeIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    private string message;

    public bool CanSend => !string.IsNullOrEmpty(Message) && Message.Length > 24;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public FeedbackView()
    {
        this.InitializeComponent();
        Ioc.Default.GetService<ILogService>()?.Log.Information("View loaded");
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void Page_Loading(FrameworkElement sender, object args)
    {
        // update titlebar spacing
        AppWindowTitleBar? titlebar = ((App)Application.Current).FeedbackWindow?.AppWindow.TitleBar;

        if (titlebar != null)
        {
            double scaleAdjustment = this.XamlRoot.RasterizationScale;
            double headerInset = AccessibilityService.DefaultFlowDirection == FlowDirection.LeftToRight ? titlebar.LeftInset : titlebar.RightInset;
            double footerInset = AccessibilityService.DefaultFlowDirection == FlowDirection.LeftToRight ? titlebar.RightInset : titlebar.LeftInset;
            ColumnDefinitionTitlebarInsetHeader.Width = new GridLength(headerInset / scaleAdjustment);
            ColumnDefinitionTitlebarInsetFooter.Width = new GridLength(footerInset / scaleAdjustment);
        }
    }

    private void ButtonSend_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFeedbackType == FeedbackType.Error)
            SentryService?.SendErrorFeedback(Message, Email, Name);
        else
            SentryService?.SendSuggestionFeedback(Message, Email, Name);

        FeedbackSent?.Invoke(this, EventArgs.Empty);
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public enum FeedbackType
    {
        Error = 0,
        Suggestion = 1
    }
}
