using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


namespace Scanner.Controls
{
    public sealed partial class ScanMergePreviewBlock : UserControl
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanMergeElement ScanMergeElement
        {
            get => (ScanMergeElement)GetValue(ScanMergeElementProperty);
            set
            {
                SetValue(ScanMergeElementProperty, value);
            }
        }

        #region Dependency Properties
        public static readonly DependencyProperty ScanMergeElementProperty =
            DependencyProperty.Register(nameof(ScanMergeElement), typeof(ScanMergeElement), typeof(ScanMergePreviewBlock), null);
        #endregion


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanMergePreviewBlock()
        {
            this.InitializeComponent();
            (this.Content as FrameworkElement).DataContext = this;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        protected override AutomationPeer OnCreateAutomationPeer() => new CardControlAutomationPeer(this);
    }


    sealed class CardControlAutomationPeer : FrameworkElementAutomationPeer
    {
        private readonly ScanMergePreviewBlock owner;

        public CardControlAutomationPeer(ScanMergePreviewBlock owner) : base(owner) => this.owner = owner;

        protected override int GetPositionInSetCore()
          => ((ItemsRepeater)owner.Parent)?.GetElementIndex(this.owner) + 1 ?? base.GetPositionInSetCore();

        protected override int GetSizeOfSetCore()
          => ((ItemsRepeater)owner.Parent)?.ItemsSourceView?.Count ?? base.GetSizeOfSetCore();
    }
}
