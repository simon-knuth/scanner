using Microsoft.UI.Xaml.Controls;
using System;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

using static Utilities;

namespace Scanner.Views.Dialogs
{
    public sealed partial class ScanMergeDialogView : ContentDialog
    {
        public ScanMergeDialogView()
        {
            this.InitializeComponent();
        }

        private async void ScrollViewer_SizeChanged(object sender, Windows.UI.Xaml.SizeChangedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                ScrollViewer scrollViewer = sender as ScrollViewer;
                
                // apply correct sizing to items
                if (scrollViewer.ActualWidth == 0) return;
                double size = (scrollViewer.ActualWidth - 48) / 4;

                UniformGridLayoutScanMergeElements.MinItemWidth = UniformGridLayoutScanMergeElements.MinItemHeight = size;
            });
        }
    }
}
