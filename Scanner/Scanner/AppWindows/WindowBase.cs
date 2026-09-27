using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using Windows.UI;
using WinUIEx;
using static Scanner.Helpers.Helpers;


namespace Scanner.AppWindows;

/// <summary>
///     Base class for all app windows. Sets up the backdrop, icon and a title bar with theme-aware caption buttons.
/// </summary>
public partial class WindowBase : WindowEx
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private FrameworkElement? observedContent;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public WindowBase()
    {
        Closed += WindowBase_Closed;

        // backdrop
        if (IsWindows11())
            SystemBackdrop = new MicaBackdrop();
        else
            SystemBackdrop = new DesktopAcrylicBackdrop();

        // titlebar
        AppWindowTitleBar titlebar = AppWindow.TitleBar;
        titlebar.ExtendsContentIntoTitleBar = true;
        titlebar.ButtonBackgroundColor = Colors.Transparent;
        titlebar.ButtonInactiveBackgroundColor = Colors.Transparent;

        // the content isn't available yet, so start with the app theme and follow the content's theme once it is
        UpdateCaptionButtonColors(Application.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light);
        Activated += WindowBase_Activated;

        // icon
        string? iconPath = Environment.ProcessPath;
        if (iconPath != null)
        {
            iconPath = Path.GetDirectoryName(iconPath);

            if (iconPath != null)
            {
                iconPath = Path.Combine(iconPath, "Assets/Icon.ico");
                AppWindow.SetIcon(iconPath);
            }
        }
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void WindowBase_Closed(object sender, WindowEventArgs args)
    {
        ((Window)sender).Closed -= WindowBase_Closed;
        ((Window)sender).Activated -= WindowBase_Activated;
    }

    private void WindowBase_Activated(object sender, WindowActivatedEventArgs args)
    {
        // observe the content's theme, which also changes with the system theme if the app theme is set to follow it
        if (Content is FrameworkElement content && content != observedContent)
        {
            if (observedContent != null)
                observedContent.ActualThemeChanged -= Content_ActualThemeChanged;

            observedContent = content;
            observedContent.ActualThemeChanged += Content_ActualThemeChanged;
            UpdateCaptionButtonColors(observedContent.ActualTheme);
        }
    }

    private void Content_ActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateCaptionButtonColors(sender.ActualTheme);
    }

    /// <summary>
    ///     Applies caption button colors matching <paramref name="theme"/>, as the system would otherwise always draw them
    ///     according to the system theme.
    /// </summary>
    private void UpdateCaptionButtonColors(ElementTheme theme)
    {
        bool isDark = theme == ElementTheme.Dark;
        Color foreground = isDark ? Colors.White : ColorHelper.FromArgb(0xE4, 0x00, 0x00, 0x00);

        AppWindowTitleBar titlebar = AppWindow.TitleBar;
        titlebar.ButtonForegroundColor = foreground;
        titlebar.ButtonHoverForegroundColor = foreground;
        titlebar.ButtonPressedForegroundColor = foreground;
        titlebar.ButtonInactiveForegroundColor = isDark ? ColorHelper.FromArgb(0x5D, 0xFF, 0xFF, 0xFF) : ColorHelper.FromArgb(0x5C, 0x00, 0x00, 0x00);
        titlebar.ButtonHoverBackgroundColor = isDark ? ColorHelper.FromArgb(0x0F, 0xFF, 0xFF, 0xFF) : ColorHelper.FromArgb(0x09, 0x00, 0x00, 0x00);
        titlebar.ButtonPressedBackgroundColor = isDark ? ColorHelper.FromArgb(0x0A, 0xFF, 0xFF, 0xFF) : ColorHelper.FromArgb(0x06, 0x00, 0x00, 0x00);
    }
}
