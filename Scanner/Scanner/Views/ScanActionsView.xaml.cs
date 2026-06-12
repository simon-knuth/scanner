using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Extensions;
using Scanner.Models;
using Scanner.Services;
using Scanner.Services.Interfaces;
using Scanner.Views.Flyouts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Principal;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Text;


namespace Scanner.Views;

[ObservableObjectAttribute]
public sealed partial class ScanActionsView : Page
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Events
    public event EventHandler ExpandScanOptionsRequested;
    public event EventHandler AreScanOptionsVisibleChanged;
    #endregion

    #region Dependency Properties
    public static readonly DependencyProperty AreScanOptionsVisibleProperty =
        DependencyProperty.Register(nameof(AreScanOptionsVisible), typeof(bool), typeof(ScanActionsView),
            new PropertyMetadata(false, OnAreScanOptionsVisibleChanged));

    public static readonly DependencyProperty ScanOptionsProperty =
        DependencyProperty.Register(nameof(ScanOptions), typeof(ScanOptions), typeof(ScanActionsView), null);
    #endregion

    public bool AreScanOptionsVisible
    {
        get => (bool)GetValue(AreScanOptionsVisibleProperty);
        set => SetValue(AreScanOptionsVisibleProperty, value);
    }

    public ScanOptions? ScanOptions
    {
        get => ViewModel.ScanOptions;
        set
        {
            SetValue(ScanOptionsProperty, value);
            ViewModel.ScanOptions = value;
        }
    }

    [ObservableProperty]
    private bool isHoveringCancelButton;

    public bool IsTemplatesButtonVisible => !AreScanOptionsVisible || GridRoot.ActualWidth > 400;
    public bool IsPreviewButtonVisible => (!ViewModel.IsScanning && !AreScanOptionsVisible) || GridRoot.ActualWidth > 500;

    public TimeSpan AnimationDuration => ViewModel.SettingsService.SettingAnimations ?
        defaultAnimationDuration : TimeSpan.FromMilliseconds(1);

    private bool showEntranceAnimations;

    private readonly TimeSpan defaultAnimationDuration = TimeSpan.FromMilliseconds(250);


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public ScanActionsView()
    {
        this.InitializeComponent();

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.SettingsService.PropertyChanged += SettingsService_PropertyChanged;
        ActualThemeChanged += ScanActionsView_ActualThemeChanged;
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewModel.IsScanning))
            return;

        OnPropertyChanged(nameof(IsPreviewButtonVisible));
    }

    private void SettingsService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsService.SettingAnimations):
                OnPropertyChanged(nameof(AnimationDuration));
                showEntranceAnimations = ViewModel.SettingsService.SettingAnimations;
                break;
        }
    }

    private void ButtonScanMode_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddToProject = !ViewModel.AddToProject;
    }

    private void ScanActionsView_ActualThemeChanged(FrameworkElement sender, object args)
    {
        // Fix scan mode icons on theme change
        IValueConverter converter = (IValueConverter)Resources["BoolOnAccentForegroundConverter"];
        Brush brush = (Brush)converter.Convert(ViewModel.CanScan, typeof(Brush), null, null);

        if (FontIconScanModeAdd is not null)
            FontIconScanModeAdd.Foreground = brush;
        if (FontIconScanModeNew is not null)
            FontIconScanModeNew.Foreground = brush;
    }

    private void ButtonScanOptions_Click(object sender, RoutedEventArgs e)
    {
        TeachingTipScanOptions.IsOpen = false;
        ExpandScanOptionsRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.SettingsService.TutorialScanOptionsButtonShown = true;
    }

    private void GridRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsTemplatesButtonVisible));
        OnPropertyChanged(nameof(IsPreviewButtonVisible));

        if (double.IsFinite(e.NewSize.Width))
        {
            DoubleAnimationScanAnimation.From = -e.NewSize.Width;
            DoubleAnimationScanAnimation.To = e.NewSize.Width;
        }
    }

    private void ButtonAnimated_Loading(FrameworkElement sender, object args)
    {
        // prevent animations during application startup
        if (!showEntranceAnimations)
        {
            Implicit.SetShowAnimations(sender, new ImplicitAnimationSet());
        }
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await Task.Delay(500);
        showEntranceAnimations = ViewModel.SettingsService.SettingAnimations;
    }

    private void ShowTemplates()
    {
        TemplatesFlyout flyout = new TemplatesFlyout(GridContent.ActualWidth);
        flyout.ShowAt(GridContent);
    }

    private void ButtonTemplates_Click(object sender, RoutedEventArgs e)
    {
        ShowTemplates();
    }

    private void MenuFlyoutItemTemplates_Click(object sender, RoutedEventArgs e)
    {
        ShowTemplates();
    }

    private static void OnAreScanOptionsVisibleChanged(DependencyObject source, DependencyPropertyChangedEventArgs args)
    {
        if (source is ScanActionsView view)
        {
            view.OnPropertyChanged(nameof(IsTemplatesButtonVisible));
            view.OnPropertyChanged(nameof(IsPreviewButtonVisible));

            view.AreScanOptionsVisibleChanged?.Invoke(view, EventArgs.Empty);
        }
    }

    private void BorderScanAnimation_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel.SettingsService.SettingAnimations)
                StoryboardScanAnimation.Begin();
        }
        catch (Exception)
        {

        }
    }

    private void ButtonCancel_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        IsHoveringCancelButton = true;
    }

    private void ButtonCancel_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        IsHoveringCancelButton = false;
    }

    private void MenuFlyoutItemAddToProject_Loading(FrameworkElement sender, object args)
    {
        if (ViewModel.AddToProject)
        {
            ((MenuFlyoutItem)sender).FontWeight = FontWeights.SemiBold;
        }
        else
        {
            ((MenuFlyoutItem)sender).FontWeight = FontWeights.Normal;
        }
    }

    private void MenuFlyoutItemNewProject_Loading(FrameworkElement sender, object args)
    {
        if (!ViewModel.AddToProject)
        {
            ((MenuFlyoutItem)sender).FontWeight = FontWeights.SemiBold;
        }
        else
        {
            ((MenuFlyoutItem)sender).FontWeight = FontWeights.Normal;
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        
    }

    private void ButtonScanOptions_Loaded(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.SettingsService.TutorialScanOptionsButtonShown)
        {
            TeachingTipScanOptions.Target = BorderTeachingTipTarget;
            TeachingTipScanOptions.IsOpen = true;
        }
    }

    private void TeachingTipScanOptions_Closing(TeachingTip sender, TeachingTipClosingEventArgs args)
    {
        if (args.Reason is TeachingTipCloseReason.CloseButton)
            ViewModel.SettingsService.TutorialScanOptionsButtonShown = true;
    }

    private void ButtonScanOptions_Unloaded(object sender, RoutedEventArgs e)
    {
        TeachingTipScanOptions.IsOpen = false;
    }
}
