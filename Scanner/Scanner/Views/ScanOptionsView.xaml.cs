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


namespace Scanner.Views
{
    [ObservableObjectAttribute]
    public sealed partial class ScanOptionsView : Page
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
        #region Events
        public event EventHandler? ExpandPageListRequested;
        #endregion

        #region Dependency Properties
        public static readonly DependencyProperty CanExpandPageListProperty =
            DependencyProperty.Register(nameof(CanExpandPageList), typeof(bool), typeof(ScanOptionsView), null);

        public static readonly DependencyProperty ScanOptionsProperty =
            DependencyProperty.Register(nameof(ViewModel.ScanOptions), typeof(ScanOptions), typeof(ScanOptionsView), null);
        #endregion

        public bool CanExpandPageList
        {
            get => (bool)GetValue(CanExpandPageListProperty);
            set
            {
                SetValue(CanExpandPageListProperty, value);
            }
        }

        public ScanOptions ScanOptions
        {
            get => ViewModel.ScanOptions;
            set
            {
                SetValue(ScanOptionsProperty, value);
                ViewModel.ScanOptions = value;
            }
        }

        #region Source mode
        public bool IsSourceModeAutomatic
        {
            get => ViewModel.ScanOptions.SourceMode == ScannerSource.Auto;
            set
            {
                if (value)
                {
                    ViewModel.ScanOptions.SourceMode = ScannerSource.Auto;
                }
            }
        }

        public bool IsSourceModeFlatbed
        {
            get => ViewModel.ScanOptions.SourceMode == ScannerSource.Flatbed;
            set
            {
                if (value)
                {
                    ViewModel.ScanOptions.SourceMode = ScannerSource.Flatbed;
                }
            }
        }

        public bool IsSourceModeFeeder
        {
            get => ViewModel.ScanOptions.SourceMode == ScannerSource.Feeder;
            set
            {
                if (value)
                {
                    ViewModel.ScanOptions.SourceMode = ScannerSource.Feeder;
                }
            }
        }
        #endregion

        public int TargetFormat
        {
            // work around additional ComboBoxItems
            get
            {
                if ((int)ViewModel.ScanOptions.TargetFormat > 0)
                {
                    return (int)ViewModel.ScanOptions.TargetFormat + 2;
                }
                else
                {
                    return (int)ViewModel.ScanOptions.TargetFormat + 1;
                }
            }
            set
            {
                if (value > 1)
                {
                    ViewModel.ScanOptions.TargetFormat = (TargetFormat)value - 2;
                }
                else
                {
                    ViewModel.ScanOptions.TargetFormat = (TargetFormat)value - 1;
                }
            }
        }

        #region Color mode
        public bool IsColorModeColor
        {
            get => ViewModel.ScanOptions.ColorMode == ScannerColorMode.Color;
            set
            {
                if (value)
                {
                    ViewModel.ScanOptions.ColorMode = ScannerColorMode.Color;
                }
            }
        }

        public bool IsColorModeGrayscale
        {
            get => ViewModel.ScanOptions.ColorMode == ScannerColorMode.Grayscale;
            set
            {
                if (value)
                {
                    ViewModel.ScanOptions.ColorMode = ScannerColorMode.Grayscale;
                }
            }
        }

        public bool IsColorModeMonochrome
        {
            get => ViewModel.ScanOptions.ColorMode == ScannerColorMode.Monochrome;
            set
            {
                if (value)
                {
                    ViewModel.ScanOptions.ColorMode = ScannerColorMode.Monochrome;
                }
            }
        }
        #endregion


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanOptionsView()
        {
            this.InitializeComponent();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void ComboBoxFileFormats_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            foreach (ComboBoxItem item in ComboBoxFileFormats.Items)
            {
                item.MaxWidth = e.NewSize.Width;
            }
        }

        private void GridContent_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ComboBoxFileFormats.MaxWidth = Math.Max(0, e.NewSize.Width - 40);
        }

        private void ButtonPageList_Click(object sender, RoutedEventArgs e)
        {
            ExpandPageListRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
