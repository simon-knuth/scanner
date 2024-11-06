using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
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
        public event EventHandler ExpandPageListRequested;
        #endregion

        #region Dependency Properties
        public static readonly DependencyProperty CanExpandPageListProperty =
            DependencyProperty.Register(nameof(CanExpandPageList), typeof(bool), typeof(ScanOptionsView), null);
        #endregion

        public bool CanExpandPageList
        {
            get => (bool)GetValue(CanExpandPageListProperty);
            set
            {
                SetValue(CanExpandPageListProperty, value);
            }
        }


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
