using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Scanner.Messages;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Services;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scanner.ViewModels;

partial class ScanMergeDialogViewModel : ObservableRecipient, IDisposable
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    public readonly IAccessibilityService? AccessibilityService = Ioc.Default.GetService<IAccessibilityService>();
    public readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
    public readonly IProjectService ProjectService = Ioc.Default.GetRequiredService<IProjectService>();
    public readonly ISentryService? SentryService = Ioc.Default.GetService<ISentryService>();
    public readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
    #endregion

    #region Commands
    public RelayCommand AcceptCommand => new RelayCommand(AcceptConfig);
    public RelayCommand CancelCommand => new RelayCommand(Cancel);
    public RelayCommand DisposeCommand => new RelayCommand(Dispose);
    #endregion

    #region Events
    public event EventHandler CloseRequested;
    #endregion

    [ObservableProperty]
    private List<ScanMergeElement> mergePreview;

    private int _StartPageNumber;
    public int StartPageNumber
    {
        get => _StartPageNumber;
        set
        {
            SetProperty(ref _StartPageNumber, value);
            RefreshMergeResult();
            MaxSkippablePages = TotalNumberOfPages - StartPageNumber + 1;
        }
    }

    private int _SkipPages = 1;
    public int SkipPages
    {
        get => _SkipPages;
        set
        {
            SetProperty(ref _SkipPages, value);
            RefreshMergeResult();
        }
    }

    private bool _ReversePages;
    public bool ReversePages
    {
        get => _ReversePages;
        set
        {
            SetProperty(ref _ReversePages, value);
            RefreshMergeResult();
        }
    }

    [ObservableProperty]
    private int totalNumberOfPages;

    [ObservableProperty]
    private int maxSkippablePages;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public ScanMergeDialogViewModel()
    {
        LogService?.Log.Information("Opening scan and merge dialog");
        SentryService?.TrackEvent(AnalyticsEvent.ScanMergeDialogOpened);
        Messenger.Register<SelectedScannerChangedMessage>(this, (r, m) =>
        {
            if (m.SelectedScanner == null)
            {
                // scanner lost
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        });

        ReversePages = SettingsService.LastScanMergeReversed;

        if (ProjectService.CurrentProject == null)
        {
            // invalid project
            ApplicationException exc = new("No project for scan and merge");
            LogService?.Log.Error(exc, "No project for scan and merge");
            SentryService?.TrackError(exc);
            return;
        }

        List<ScanMergeElement> newList = [];
        foreach (IProjectPage page in ProjectService.CurrentProject.Pages)
        {
            newList.Add(new ScanMergeElement
            {
                IsPotentialPage = false,
                PreviewBitmapUri = page.PreviewBitmapUri,
                ItemDescriptor = GetItemDescriptor(page.PageNumber),
            });
        }
        MergePreview = newList;
        TotalNumberOfPages = ProjectService.CurrentProject.Pages.Count;

        if (TotalNumberOfPages > 1)
        {
            StartPageNumber = 2;
        }
        else
        {
            StartPageNumber = 1;
        }
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Dispose()
    {
        Messenger.UnregisterAll(this);
    }

    private void RefreshMergeResult()
    {
        try
        {
            LogService?.Log.Information("Creating 'Scan and merge' preview for {StartPage} and {SkipPages}",
                StartPageNumber, SkipPages);
            List<ScanMergeElement> cleanList = [.. MergePreview];
            cleanList.RemoveAll((x) => x.IsPotentialPage == true);

            // generate new final pages
            List<ScanMergeElement> newList = new List<ScanMergeElement>();
            for (int i = 0; i < cleanList.Count; i++)
            {
                if (newList.Count == StartPageNumber - 1)
                {
                    // start of potential pages
                    if (!ReversePages)
                    {
                        newList.Add(new ScanMergeElement
                        {
                            IsPotentialPage = true,
                            ItemDescriptor = Resources.Strings.Resources.ScanMergeElementStartPage,
                            IsStartPage = true,
                            IsOrderReversed = ReversePages
                        });
                    }
                    else
                    {
                        newList.Add(new ScanMergeElement
                        {
                            IsPotentialPage = true,
                            ItemDescriptor = Resources.Strings.Resources.TextScanMergeElementLastPage,
                            IsStartPage = true,
                            IsOrderReversed = ReversePages
                        });
                    }

                    if (SkipPages == 0)
                    {
                        newList.Add(new ScanMergeElement
                        {
                            IsPotentialPage = true,
                            IsPlaceholderForMultiplePages = true,
                            ItemDescriptor = Resources.Strings.Resources.ScanMergeElementSurplusPages
                        });

                        for (int j = i; j < cleanList.Count; j++)
                        {
                            newList.Add(cleanList[j]);
                            newList[newList.Count - 1].ItemDescriptor = "";
                        }

                        break;
                    }
                }
                else if ((newList.Count - (StartPageNumber - 1)) % (SkipPages + 1) == 0
                    && newList.Count > StartPageNumber - 1)
                {
                    // add normal potential page
                    newList.Add(new ScanMergeElement
                    {
                        IsPotentialPage = true,
                        ItemDescriptor = Resources.Strings.Resources.ScanMergeElementSinglePage
                    });
                }

                newList.Add(cleanList[i]);
                newList[newList.Count - 1].ItemDescriptor = GetItemDescriptor(newList.Count);
            }

            if (!newList.Exists((x) => x.IsPlaceholderForMultiplePages))
            {
                newList.Add(new ScanMergeElement
                {
                    IsPotentialPage = true,
                    IsPlaceholderForMultiplePages = true,
                    ItemDescriptor = Resources.Strings.Resources.ScanMergeElementSurplusPages
                });
            }

            MergePreview = newList;
        }
        catch (Exception exc)
        {
            LogService?.Log.Error(exc, "Failed to create 'Scan and merge' preview");
            SentryService?.TrackError(exc);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private string GetItemDescriptor(int number)
    {
        return String.Format(Resources.Strings.Resources.PageDescriptor, number);
    }

    private void AcceptConfig()
    {
        ScanMergeConfig config = CreateMergeConfig();
        if (config != null)
        {
            SettingsService.LastScanMergeReversed = config.InsertReversed;
            SentryService?.TrackEvent(AnalyticsEvent.ScanMergeConfirmed, new Dictionary<string, string>
            {
                { "reversed", config.InsertReversed.ToString() }
            });
            Messenger.Send(new InvokeScanMergeMessage(config));
        }
    }

    private void Cancel()
    {
        LogService?.Log.Information("Cancel");
    }

    private ScanMergeConfig CreateMergeConfig()
    {
        try
        {
            LogService?.Log.Information("Creating 'Scan and merge' preview for {StartPage} and {SkipPages}",
                StartPageNumber, SkipPages);

            if (MergePreview != null && MergePreview.Count >= 1)
            {
                // create config from preview
                ScanMergeConfig config = new ScanMergeConfig
                {
                    InsertReversed = ReversePages
                };

                int i = 0;
                foreach (ScanMergeElement element in MergePreview)
                {
                    if (element.IsPlaceholderForMultiplePages)
                    {
                        // surplus pages
                        config.SurplusPagesIndex = i;
                        break;
                    }
                    else if (element.IsPotentialPage)
                    {
                        // single new page
                        config.InsertIndices.Add(i);
                        i++;
                    }
                    else
                    {
                        // single existing page
                        i++;
                    }
                }

                LogService?.Log.Information("Returning 'Scan and merge' {@Config}", config);
                return config;
            }
            else
            {
                LogService?.Log.Information("Returning no 'Scan and merge' config.");
                return null;
            }
        }
        catch (Exception exc)
        {
            LogService?.Log.Error(exc, "Failed to create 'Scan and merge' config");
            SentryService?.TrackError(exc);
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return null;
        }
    }
}
