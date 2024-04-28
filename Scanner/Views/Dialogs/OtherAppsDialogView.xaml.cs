using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Scanner.Services;
using System;
using Windows.System;
using Windows.UI.Xaml.Controls;


namespace Scanner.Views.Dialogs
{
    public sealed partial class OtherAppsDialogView : ContentDialog
    {
        public OtherAppsDialogView()
        {
            this.InitializeComponent();
        }

        private async void ButtonGetClipShelf_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?productid=9NV7F7JGLRPL"));
        }
    }
}
