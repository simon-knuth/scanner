using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using static Enums;
using static Globals;
using static Utilities;


namespace Scanner
{
    public sealed partial class SettingsPage : Page
    {
        private SettingsPageIntent intent;
        private long _allSettingsLoaded = 0;
        private bool allSettingsLoaded
        {
            get { return Interlocked.Read(ref _allSettingsLoaded) == 1; }
            set { Interlocked.Exchange(ref _allSettingsLoaded, Convert.ToInt64(value)); }
        }


        public SettingsPage()
        {
            this.InitializeComponent();

            // register event listeners
            CoreApplication.GetCurrentView().TitleBar.LayoutMetricsChanged += (titleBar, y) =>
            {
                GridSettingsHeader.Padding = new Thickness(0, titleBar.Height, 0, 0);
            };
            Window.Current.Activated += Window_Activated;
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            intent = (SettingsPageIntent)e.Parameter;
        }


        private async void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == CoreWindowActivationState.Deactivated)
            {
                // window deactivated
                await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
                {
                    TextBlockSettingsHeader.Opacity = 0.5;
                });
            }
            else
            {
                // window activated
                await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
                {
                    TextBlockSettingsHeader.Opacity = 1;
                });
            }
        }


        /// <summary>
        ///     The event listener for when a button is pressed. Allows to discard changes using the escape key.
        /// </summary>
        private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.GoBack || e.Key == Windows.System.VirtualKey.Escape)
            {
                GoBack();
            }
        }


        /// <summary>
        ///     The event listener for when the page is loading. Loads all current settings and updates
        ///     the version indicator at the bottom.
        /// </summary>
        private async void Page_Loading(FrameworkElement sender, object args)
        {
            if (scanFolder != null)
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => TextBlockSaveLocation.Text = scanFolder.Path);
            }
            else
            {
                await ResetSaveLocationAsync();
            }

            await RunOnUIThreadAsync(CoreDispatcherPriority.High, async () =>
            {
                var flowDirectionSetting = Windows.ApplicationModel.Resources.Core.ResourceContext.GetForCurrentView().QualifierValues["LayoutDirection"];
                if (flowDirectionSetting == "LTR")
                {
                    GridSettingsContent.FlowDirection = FlowDirection.LeftToRight;
                    StackPanelDialogLicensesHeading.FlowDirection = FlowDirection.LeftToRight;
                    ItemsRepeaterExportLog.FlowDirection = FlowDirection.LeftToRight;
                    RelativePanelSettingsSaveLocation.FlowDirection = FlowDirection.LeftToRight;
                    StackPanelTextBlockRestart.FlowDirection = FlowDirection.LeftToRight;
                    StackPanelSettingsHeadingHelpSetup.FlowDirection = FlowDirection.LeftToRight;
                    StackPanelSettingsHeadingDonate.FlowDirection = FlowDirection.LeftToRight;
                    StackPanelSettingsHeadingFeedback.FlowDirection = FlowDirection.LeftToRight;
                    StackPanelSettingsHeadingTranslations.FlowDirection = FlowDirection.LeftToRight;
                    StackPanelSettingsHeadingAbout.FlowDirection = FlowDirection.LeftToRight;
                    ButtonSettingsHeaderBackScaleTransform.ScaleX = 1;
                    ButtonDialogLicensesHeadingBackScaleTransform.ScaleX = 1;
                }
                else
                {
                    GridSettingsContent.FlowDirection = FlowDirection.RightToLeft;
                    StackPanelDialogLicensesHeading.FlowDirection = FlowDirection.RightToLeft;
                    ItemsRepeaterExportLog.FlowDirection = FlowDirection.RightToLeft;
                    RelativePanelSettingsSaveLocation.FlowDirection = FlowDirection.RightToLeft;
                    StackPanelTextBlockRestart.FlowDirection = FlowDirection.RightToLeft;
                    StackPanelSettingsHeadingHelpSetup.FlowDirection = FlowDirection.RightToLeft;
                    StackPanelSettingsHeadingDonate.FlowDirection = FlowDirection.RightToLeft;
                    StackPanelSettingsHeadingFeedback.FlowDirection = FlowDirection.RightToLeft;
                    StackPanelSettingsHeadingTranslations.FlowDirection = FlowDirection.RightToLeft;
                    StackPanelSettingsHeadingAbout.FlowDirection = FlowDirection.RightToLeft;
                    ButtonSettingsHeaderBackScaleTransform.ScaleX = -1;
                    ButtonDialogLicensesHeadingBackScaleTransform.ScaleX = -1;
                }

                if (settingSaveLocationAsk)
                {
                    RadioButtonSaveLocationAsk.IsChecked = true;
                    StackPanelSettingsSaveLocationSet.Visibility = Visibility.Collapsed;
                }
                else
                {
                    RadioButtonSaveLocationSet.IsChecked = true;
                    StackPanelSettingsSaveLocationSet.Visibility = Visibility.Visible;
                }

                switch (settingAppTheme)
                {
                    case Theme.light:
                        ComboBoxTheme.SelectedIndex = 1;
                        break;
                    case Theme.dark:
                        ComboBoxTheme.SelectedIndex = 2;
                        break;
                    default:
                        ComboBoxTheme.SelectedIndex = 0;
                        break;
                }
                StackPanelTextBlockRestart.Visibility = Visibility.Collapsed;
                CheckBoxAppendTime.IsChecked = settingAppendTime;
                CheckBoxNotificationScanComplete.IsChecked = settingNotificationScanComplete;
                CheckBoxSettingsErrorStatistics.IsChecked = settingErrorStatistics;

                if (await IsDefaultScanFolderSetAsync() != true) ButtonResetLocation.IsEnabled = true;

                allSettingsLoaded = true;

                PackageVersion version = Package.Current.Id.Version;
                RunSettingsVersion.Text = String.Format("Version {0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);

                if (localSettingsContainer.Values["awaitingRestartAfterThemeChange"] != null
                    && (bool) localSettingsContainer.Values["awaitingRestartAfterThemeChange"] == true)
                {
                    // theme change pending
                    StackPanelTextBlockRestart.Visibility = Visibility.Visible;
                }
            });

            await InitializeAutomationPropertiesAsync();
        }



        /// <summary>
        ///     The event listener for when another <see cref="Theme"/> is selected from <see cref="ComboBoxTheme"/>.
        /// </summary>
        private async void ComboBoxTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (allSettingsLoaded)
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => StackPanelTextBlockRestart.Visibility = Visibility.Visible);

                settingAppTheme = (Theme)int.Parse(((ComboBoxItem)ComboBoxTheme.SelectedItem).Tag.ToString());
                localSettingsContainer.Values["awaitingRestartAfterThemeChange"] = true;
                SaveSettings();
            }
        }


        /// <summary>
        ///     The event listener for when the <see cref="HyperlinkRestart"/>, which saves the settings and restarts
        ///     the app after a theme change, is clicked.
        /// </summary>
        private async void HyperlinkRestart_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            log.Information("Requesting app restart.");
            await CoreApplication.RequestRestartAsync("");
        }


        /// <summary>
        ///     The event listener for when the <see cref="ButtonBrowse"/>, which allows the user to select a new
        ///     <see cref="scanFolder"/>, is clicked.
        /// </summary>
        private async void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            folderPicker.FileTypeFilter.Add("*");

            StorageFolder folder;
            try
            {
                folder = await folderPicker.PickSingleFolderAsync();
            }
            catch (Exception exc)
            {
                log.Warning(exc, "Picking a new save location failed.");
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessagePickFolderHeading"),
                    LocalizedString("ErrorMessagePickFolderBody") + "\n" + exc.Message));
                return;
            }

            if (folder != null &&
                (( scanFolder != null && folder.Path != scanFolder.Path ) || scanFolder == null))
            {
                Windows.Storage.AccessCache.StorageApplicationPermissions.
                    FutureAccessList.AddOrReplace("scanFolder", folder);
                scanFolder = folder;
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => TextBlockSaveLocation.Text = scanFolder.Path);
            }

            if (await IsDefaultScanFolderSetAsync() != true) await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => ButtonResetLocation.IsEnabled = true);
            log.Information("Successfully selected new save location.");
        }


        /// <summary>
        ///     The event listener for when the button that resets the current <see cref="scanFolder"/> to ..\Pictures\Scans
        ///     is clicked.
        /// </summary>
        private async void ButtonResetLocation_Click(object sender, RoutedEventArgs e)
        {
            await ResetSaveLocationAsync();
        }



        private async Task ResetSaveLocationAsync()
        {
            try
            {
                await ResetScanFolderAsync();
            }
            catch (UnauthorizedAccessException)
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageResetFolderUnauthorizedHeading"),
                    LocalizedString("ErrorMessageResetFolderUnauthorizedBody")));
                return;
            }
            catch (Exception exc)
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => ErrorMessage.ShowErrorMessage(TeachingTipEmpty, LocalizedString("ErrorMessageResetFolderHeading"),
                    LocalizedString("ErrorMessageResetFolderBody") + "\n" + exc.Message));
                return;
            }

            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                TextBlockSaveLocation.Text = scanFolder.Path;
                ButtonResetLocation.IsEnabled = false;
            });
        }



        /// <summary>
        ///     Page was loaded (possibly through navigation).
        /// </summary>
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            log.Information("Navigated to SettingsPage.");
            await RunOnUIThreadAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
            {
                StoryboardEnter.Begin();
                ButtonBrowse.Focus(FocusState.Programmatic);

                ScrollViewerSettings.Margin = new Thickness(0, GridSettingsHeader.ActualHeight, 0, 0);
                ScrollViewerSettings.Padding = new Thickness(0, -GridSettingsHeader.ActualHeight, 0, 0);

                if (intent != null && intent.scrollToDonateSection)
                {
                    GridSettingsDonate.StartBringIntoView();
                    StoryboardScrollingToDonate.Begin();
                }

                TeachingTipEmpty.CloseButtonContent = LocalizedString("ButtonCloseText");
            });
        }



        private async void HyperlinkRate_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            await ShowRatingDialogAsync();
        }



        private void ButtonBack_Click(object sender, RoutedEventArgs e)
        {
            GoBack();
        }



        private void GoBack()
        {
            Frame.GoBack();
        }



        private async void SettingCheckboxChanged(object sender, RoutedEventArgs e)
        {
            if (allSettingsLoaded)
            {
                settingSaveLocationAsk = (bool)RadioButtonSaveLocationAsk.IsChecked;
                settingAppendTime = (bool)CheckBoxAppendTime.IsChecked;
                settingNotificationScanComplete = (bool)CheckBoxNotificationScanComplete.IsChecked;
                settingErrorStatistics = (bool)CheckBoxSettingsErrorStatistics.IsChecked;

                if (sender != null && (sender == RadioButtonSaveLocationAsk || sender == RadioButtonSaveLocationSet))
                {
                    await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
                    {
                        if (RadioButtonSaveLocationAsk.IsChecked == true) StackPanelSettingsSaveLocationSet.Visibility = Visibility.Collapsed;
                        else StoryboardSaveLocationSet.Begin();
                    });
                }

                SaveSettings();
            }
        }



        /// <summary>
        ///     The event listener for when <see cref="HyperlinkButtonSettingsAboutLicenses"/>, which displays <see cref="ContentDialogLicenses"/>, is clicked.
        /// </summary>
        private async void HyperlinkButtonSettingsAboutLicenses_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, async () =>
            {
                ContentDialogLicenses.PrimaryButtonText = "";
                ContentDialogLicenses.SecondaryButtonText = "";
                ButtonDialogLicensesHeadingBack.IsEnabled = false;

                if (FrameDialogLicenses.Content == null)
                {
                    FrameDialogLicenses.Navigate(typeof(LicensePage), new SuppressNavigationTransitionInfo());
                }
                if (FrameDialogLicenses.Content.GetType() == typeof(LicenseDetailPage))
                {
                    FrameDialogLicenses.GoBack(new SuppressNavigationTransitionInfo());
                }

                await ContentDialogLicenses.ShowAsync();
            });
        }



        private async void HyperlinkButtonSettingsHelpScannerSettings_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:printers"));
        }



        private async void HyperlinkButtonSettingsTranslationsContributors_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, async () => await ContentDialogTranslationsContributors.ShowAsync());
        }



        private async void HyperlinkButtonSettingsAboutCredits_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, async () => await ContentDialogAboutCredits.ShowAsync());
        }


        private async void HyperlinkSettingsExportLog_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, async () =>
            {
                ProgressBarExportLog.Visibility = Visibility.Visible;
                ItemsRepeaterExportLog.Visibility = Visibility.Collapsed;
                await ContentDialogExportLog.ShowAsync();
            });

            // flush log
            Log.CloseAndFlush();

            // populate file list
            StorageFolder logFolder = await ApplicationData.Current.RoamingFolder.GetFolderAsync("logs");
            var files = await logFolder.GetFilesAsync();

            List<LogFile> sortedFiles = new List<LogFile>();
            foreach (var file in files)
            {
                sortedFiles.Add(await LogFile.CreateLogFile(file));
            }
            sortedFiles.Sort(delegate (LogFile x, LogFile y)
            {
                return DateTimeOffset.Compare(x.LastModified, y.LastModified);
            });
            sortedFiles.Reverse();

            ObservableCollection<LogFile> logFilesExport = new ObservableCollection<LogFile>(sortedFiles);

            await InitializeSerilogAsync();

            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                ItemsRepeaterExportLog.ItemsSource = logFilesExport;
                ProgressBarExportLog.Visibility = Visibility.Collapsed;
                ItemsRepeaterExportLog.Visibility = Visibility.Visible;
            });
        }


        private async void ButtonExportLog_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;

            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            savePicker.FileTypeChoices.Add("TXT", new List<string>() { ".txt" });
            savePicker.SuggestedFileName = ((string)button.Tag).Split(".")[0];

            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                CachedFileManager.DeferUpdates(file);

                // write to file
                StorageFolder logFolder = await ApplicationData.Current.RoamingFolder.GetFolderAsync("logs");
                StorageFile sourceFile = await logFolder.GetFileAsync((string)button.Tag);
                await sourceFile.CopyAndReplaceAsync(file);
                await CachedFileManager.CompleteUpdatesAsync(file);
            }
        }


        private void ButtonDialogLicensesHeadingBack_Click(object sender, RoutedEventArgs e)
        {
            if (FrameDialogLicenses.Content.GetType() == typeof(LicenseDetailPage))
            {
                FrameDialogLicenses.GoBack(new SlideNavigationTransitionInfo()
                { Effect = SlideNavigationTransitionEffect.FromRight });
            }
        }


        private void FrameDialogLicenses_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            if (e.SourcePageType == typeof(LicenseDetailPage))
            {
                ButtonDialogLicensesHeadingBack.IsEnabled = true;
            }
            else
            {
                ButtonDialogLicensesHeadingBack.IsEnabled = false;
            }
        }


        private async Task InitializeAutomationPropertiesAsync()
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                CopyToolTipToAutomationPropertiesName(ButtonSettingsHeaderBack);
                CopyToolTipToAutomationPropertiesName(ButtonDialogLicensesHeadingBack);
            });
        }
    }
}
