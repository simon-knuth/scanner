using System;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using static Utilities;


namespace Scanner.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class EditorView : Page
    {
        public EditorView()
        {
            this.InitializeComponent();

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private async Task ApplyFlipViewOrientation(Orientation orientation)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                VirtualizingStackPanel panel = (VirtualizingStackPanel)FlipViewPages.ItemsPanelRoot;
                panel.Orientation = orientation;
            });
        }

        private async void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Orientation))
            {
                await ApplyFlipViewOrientation(ViewModel.Orientation);
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await ApplyFlipViewOrientation(ViewModel.Orientation);
        }
    }
}
