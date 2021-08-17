using Microsoft.Extensions.DependencyInjection;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Messaging;
using System;
using WinUI = Microsoft.UI.Xaml.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Animation;
using Scanner.ViewModels;
using Scanner.Services;

namespace Scanner.Views
{
    public sealed partial class ShellView : Page
    {       
        public ShellView()
        {
            this.InitializeComponent();

            Ioc.Default.ConfigureServices(new ServiceCollection()
                .AddSingleton<IMessenger>(WeakReferenceMessenger.Default)
                .AddSingleton<ISettingsService, SettingsService>()
            //    .AddSingleton<IDatabaseService, DatabaseService>()
            //    .AddSingleton<IScannerDiscoveryService, ScannerDiscoveryService>()
            //    .AddSingleton<IPdfService, PdfService>()
            //    .AddSingleton<IAutoRotatorService, AutoRotatorService>()
                .BuildServiceProvider());

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.DisplayedView):
                    ViewModel_DisplayedViewChanged(sender, e);
                    break;
                default:
                    break;
            }
        }

        private void ViewModel_DisplayedViewChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            WinUI.NavigationViewItem requestedItem = ConvertNavigationItem(ViewModel.DisplayedView);

            if (requestedItem.IsEnabled == true)
            {
                NavigationViewMain.SelectedItem = requestedItem;
            }
            else
            {
                // can not select requested item ~> resynchronize ViewModel
                var currentlySelectedItem = NavigationViewMain.SelectedItem as WinUI.NavigationViewItem;
                ViewModel.DisplayedView = ConvertNavigationItem(currentlySelectedItem);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            NavigationViewMain.SelectedItem = NavigationViewItemMainScanOptions;

            FrameMainContentSecond.Navigate(typeof(EditorView));
            FrameMainContentThird.Navigate(typeof(PageListView));
        }

        private void NavigationViewMain_SelectionChanged(WinUI.NavigationView sender, WinUI.NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem == NavigationViewItemMainScanOptions)
            {
                FrameMainContentFirst.Navigate(typeof(ScanOptionsView));
            }
            else if (args.SelectedItem == NavigationViewItemMainPageList)
            {
                FrameMainContentFirst.Navigate(typeof(PageListView));
            }
            else if (args.SelectedItem == NavigationViewItemMainEditor)
            {
                FrameMainContentSecond.Content = null;
                FrameMainContentFirst.Navigate(typeof(EditorView));
            }
            else if (args.SelectedItem == NavigationViewItemMainHelp)
            {
                FrameMainContentFirst.Navigate(typeof(HelpView));
            }
            else if (args.SelectedItem == NavigationViewItemMainSettings)
            {
                FrameMainContentFirst.Navigate(typeof(SettingsView));
            }

            ViewModel.DisplayedView = ConvertNavigationItem(args.SelectedItem as WinUI.NavigationViewItem);
        }

        private void VisualStateGroup_CurrentStateChanging(object sender, VisualStateChangedEventArgs e)
        {
            // ensure expected layout when the app is resized
            if (e.OldState == NarrowState)
            {
                FrameMainContentSecond.Navigate(typeof(EditorView), null, new SuppressNavigationTransitionInfo());
            }

            if (e.OldState == NarrowState && NavigationViewMain.SelectedItem == null || NavigationViewItemMainEditor.IsSelected)
            {
                NavigationViewItemMainScanOptions.IsSelected = true;
            }

            if (e.NewState == WideState && NavigationViewItemMainPageList.IsSelected)
            {
                FrameMainContentFirst.Content = null;
                NavigationViewItemMainScanOptions.IsSelected = true;
            }

            if (e.NewState == WideState)
            {
                FrameMainContentThird.Navigate(typeof(PageListView), null,
                    new SuppressNavigationTransitionInfo());
            }
        }

        /// <summary>
        ///     Maps a <see cref="ShellNavigationSelectableItem"/> to the corresponding
        ///     <see cref="WinUI.NavigationViewItem"/>.
        /// </summary>
        public WinUI.NavigationViewItem ConvertNavigationItem(ShellNavigationSelectableItem item)
        {
            switch (item)
            {
                case ShellNavigationSelectableItem.ScanOptions:
                    return NavigationViewItemMainScanOptions;
                case ShellNavigationSelectableItem.PageList:
                    return NavigationViewItemMainPageList;
                case ShellNavigationSelectableItem.Editor:
                    return NavigationViewItemMainEditor;
                case ShellNavigationSelectableItem.Help:
                    return NavigationViewItemMainHelp;
                case ShellNavigationSelectableItem.Donate:
                    return NavigationViewItemMainDonate;
                case ShellNavigationSelectableItem.Settings:
                    return NavigationViewItemMainSettings;
                default:
                    throw new ArgumentException(String.Format(
                        "Unable to convert ShellNavigationSelectableItem {1} to NavigationViewItem.",
                        item.ToString()));
            }
        }

        /// <summary>
        ///     Maps a <see cref="WinUI.NavigationViewItem"/> to the corresponding
        ///     <see cref="ShellNavigationSelectableItem"/>.
        /// </summary>
        public ShellNavigationSelectableItem ConvertNavigationItem(WinUI.NavigationViewItem item)
        {
            if (item == NavigationViewItemMainScanOptions) return ShellNavigationSelectableItem.ScanOptions;
            else if (item == NavigationViewItemMainPageList) return ShellNavigationSelectableItem.PageList;
            else if (item == NavigationViewItemMainEditor) return ShellNavigationSelectableItem.Editor;
            else if (item == NavigationViewItemMainHelp) return ShellNavigationSelectableItem.Help;
            else if (item == NavigationViewItemMainDonate) return ShellNavigationSelectableItem.Donate;
            else if (item == NavigationViewItemMainSettings) return ShellNavigationSelectableItem.Settings;
            else throw new ArgumentException(String.Format(
                "Unable to convert NavigationViewItem {1} to ShellNavigationSelectableItem.",
                item.Name));
        }
    }
}
