using Microsoft.UI.Xaml.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Scanner.Models;
using Scanner.Views;
using static Scanner.Helpers.Helpers;
using Windows.Foundation;
using System;
using Microsoft.UI.Xaml.Controls.Primitives;
using Scanner.Services.Interfaces;
using CommunityToolkit.Mvvm.DependencyInjection;
using Scanner.Helpers;
using Scanner.Extensions;

namespace Scanner.Controls;

[ObservableObject]
public sealed partial class ScanAreaAlignmentControl : UserControl
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
    #endregion

    #region Dependency Properties
    public static readonly DependencyProperty LongestSideProperty =
        DependencyProperty.Register(nameof(LongestSide), typeof(int), typeof(ScanAreaAlignmentControl), new PropertyMetadata(0, OnLongestSideChanged));

    public static readonly DependencyProperty MinScanAreaProperty =
        DependencyProperty.Register(nameof(MinScanArea), typeof(Size), typeof(ScanAreaAlignmentControl), new PropertyMetadata(Size.Empty, OnMinScanAreaChanged));

    public static readonly DependencyProperty MaxScanAreaProperty =
        DependencyProperty.Register(nameof(MaxScanArea), typeof(Size), typeof(ScanAreaAlignmentControl), new PropertyMetadata(Size.Empty, OnMaxScanAreaChanged));

    public static readonly DependencyProperty PaperSizeProperty =
        DependencyProperty.Register(nameof(PaperSize), typeof(PaperSize), typeof(ScanAreaAlignmentControl), new PropertyMetadata(PaperSize.DinA4, OnPaperSizeChanged));

    public static readonly DependencyProperty SelectedOrientationProperty =
        DependencyProperty.Register(nameof(SelectedOrientation), typeof(ScanOrientation), typeof(ScanAreaAlignmentControl), new PropertyMetadata(ScanOrientation.Portrait, OnSelectedOrientationChanged));

    public static readonly DependencyProperty SelectedCornerProperty =
        DependencyProperty.Register(nameof(SelectedCorner), typeof(ScanCorner), typeof(ScanAreaAlignmentControl), new PropertyMetadata(ScanCorner.TopLeft, OnSelectedCornerChanged));

    public static readonly DependencyProperty PreviewBitmapUriProperty =
        DependencyProperty.Register(nameof(PreviewBitmapUri), typeof(Uri), typeof(ScanAreaAlignmentControl), new PropertyMetadata(null, OnPreviewBitmapUriChanged));
    #endregion

    public int LongestSide
    {
        get => (int)GetValue(LongestSideProperty);
        set => SetValue(LongestSideProperty, value);
    }

    public Size MinScanArea
    {
        get => (Size)GetValue(MinScanAreaProperty);
        set => SetValue(MinScanAreaProperty, value);
    }

    public Size MaxScanArea
    {
        get => (Size)GetValue(MaxScanAreaProperty);
        set => SetValue(MaxScanAreaProperty, value);
    }

    public PaperSize PaperSize
    {
        get => (PaperSize)GetValue(PaperSizeProperty);
        set => SetValue(PaperSizeProperty, value);
    }

    public ScanOrientation SelectedOrientation
    {
        get => (ScanOrientation)GetValue(SelectedOrientationProperty);
        set => SetValue(SelectedOrientationProperty, value);
    }

    public ScanCorner SelectedCorner
    {
        get => (ScanCorner)GetValue(SelectedCornerProperty);
        set => SetValue(SelectedCornerProperty, value);
    }

    public bool IsTopLeftScanCornerSelected
    {
        get => SelectedCorner == ScanCorner.TopLeft;
        set
        {
            if (value)
                SelectedCorner = ScanCorner.TopLeft;
        }
    }

    public bool IsTopRightScanCornerSelected
    {
        get => SelectedCorner == ScanCorner.TopRight;
        set
        {
            if (value)
                SelectedCorner = ScanCorner.TopRight;
        }
    }

    public bool IsBottomRightScanCornerSelected
    {
        get => SelectedCorner == ScanCorner.BottomRight;
        set
        {
            if (value)
                SelectedCorner = ScanCorner.BottomRight;
        }
    }

    public bool IsBottomLeftScanCornerSelected
    {
        get => SelectedCorner == ScanCorner.BottomLeft;
        set
        {
            if (value)
                SelectedCorner = ScanCorner.BottomLeft;
        }
    }

    public Uri? PreviewBitmapUri
    {
        get => (Uri?)GetValue(PreviewBitmapUriProperty);
        set => SetValue(PreviewBitmapUriProperty, value);
    }

    [ObservableProperty]
    private bool isSelectionTooBig;

    [ObservableProperty]
    private bool isSelectionTooSmall;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public ScanAreaAlignmentControl()
    {
        this.InitializeComponent();
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private static void OnLongestSideChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScanAreaAlignmentControl control)
        {
            control.UpdateVisualization();
        }
    }

    private static void OnMinScanAreaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScanAreaAlignmentControl control)
        {
            control.UpdateVisualization();
        }
    }

    private static void OnMaxScanAreaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScanAreaAlignmentControl control)
        {
            control.UpdateVisualization();
        }
    }

    private static void OnPaperSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScanAreaAlignmentControl control)
        {
            control.UpdateVisualization();
        }
    }

    private static void OnSelectedOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScanAreaAlignmentControl control)
        {
            control.UpdateVisualization();
        }
    }

    private static void OnPreviewBitmapUriChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScanAreaAlignmentControl control)
        {
            control.BitmapImagePreview.UriSource = e.NewValue as Uri;
        }
    }

    private static void OnSelectedCornerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScanAreaAlignmentControl control)
        {
            control.OnPropertyChanged(nameof(IsTopLeftScanCornerSelected));
            control.OnPropertyChanged(nameof(IsTopRightScanCornerSelected));
            control.OnPropertyChanged(nameof(IsBottomLeftScanCornerSelected));
            control.OnPropertyChanged(nameof(IsBottomRightScanCornerSelected));
            control.UpdateVisualization();
        }
    }

    private void UpdateVisualization()
    {
        // max scan area
        if (MaxScanArea.Width > MaxScanArea.Height)
        {
            GridContent.Width = LongestSide;
            GridContent.Height = MaxScanArea.Height / MaxScanArea.Width * LongestSide;
        }
        else
        {
            GridContent.Height = LongestSide;
            GridContent.Width = MaxScanArea.Width / MaxScanArea.Height * LongestSide;
        }

        // selected scan area
        Rect paperSizeRect = PaperSize.ToRect();
        double widthIn = Measurement.FromCentimeters(paperSizeRect.Width / 10).GetInches();
        double heightIn = Measurement.FromCentimeters(paperSizeRect.Height / 10).GetInches();
        double borderWidth = BorderThickness.Left + BorderThickness.Right;
        double borderHeight = BorderThickness.Top + BorderThickness.Bottom;
        switch (SelectedOrientation)
        {
            case ScanOrientation.Portrait:
                BorderSelectedArea.Width = (widthIn - borderWidth) / MaxScanArea.Width * GridContent.Width;
                BorderSelectedArea.Height = (heightIn - borderHeight) / MaxScanArea.Height * GridContent.Height;
                break;
            case ScanOrientation.Landscape:
                BorderSelectedArea.Width = (heightIn - borderHeight) / MaxScanArea.Width * GridContent.Width;
                BorderSelectedArea.Height = (widthIn - borderWidth) / MaxScanArea.Height * GridContent.Height;
                break;
            default:
                LogService?.Log.Error("Can't size scan region alignment Border for orientation " + SelectedOrientation);
                throw new ApplicationException("Failed to size scan region alignment Border for orientation " + SelectedOrientation);
        }

        switch (SelectedCorner)
        {
            case ScanCorner.TopLeft:
                BorderSelectedArea.HorizontalAlignment = HorizontalAlignment.Left;
                BorderSelectedArea.VerticalAlignment = VerticalAlignment.Top;
                break;
            case ScanCorner.TopRight:
                BorderSelectedArea.HorizontalAlignment = HorizontalAlignment.Right;
                BorderSelectedArea.VerticalAlignment = VerticalAlignment.Top;
                break;
            case ScanCorner.BottomRight:
                BorderSelectedArea.HorizontalAlignment = HorizontalAlignment.Right;
                BorderSelectedArea.VerticalAlignment = VerticalAlignment.Bottom;
                break;
            case ScanCorner.BottomLeft:
                BorderSelectedArea.HorizontalAlignment = HorizontalAlignment.Left;
                BorderSelectedArea.VerticalAlignment = VerticalAlignment.Bottom;
                break;
            default:
                LogService?.Log.Error("Can't position scan region alignment Border for corner " + SelectedCorner);
                throw new ApplicationException("Failed to position scan region alignment Border for corner " + SelectedCorner);
        }

        // warnings
        double effectiveWidthIn = SelectedOrientation == ScanOrientation.Portrait ? widthIn : heightIn;
        double effectiveHeightIn = SelectedOrientation == ScanOrientation.Portrait ? heightIn : widthIn;
        IsSelectionTooBig = effectiveWidthIn > MaxScanArea.Width || effectiveHeightIn > MaxScanArea.Height;
        IsSelectionTooSmall = effectiveWidthIn < MinScanArea.Width || effectiveHeightIn < MinScanArea.Height;
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        // prevent unchecking by clicking
        if (((ToggleButton)sender).IsChecked == false)
            ((ToggleButton)sender).IsChecked = true;
    }

    private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateVisualization();
    }
}
