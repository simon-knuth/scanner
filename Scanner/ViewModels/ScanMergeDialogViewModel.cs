using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.Helpers;
using Scanner.Services;
using Scanner.Services.Messenger;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using static Utilities;

namespace Scanner.ViewModels
{
    public class ScanMergeDialogViewModel : ObservableRecipient
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetService<IAccessibilityService>();
        public readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        public readonly ILogService LogService = Ioc.Default.GetService<ILogService>();
        public readonly IScanResultService ScanResultService = Ioc.Default.GetRequiredService<IScanResultService>();
        #endregion

        #region Commands
        public RelayCommand AcceptCommand => new RelayCommand(AcceptConfig);
        public RelayCommand CancelCommand => new RelayCommand(Cancel);
        #endregion

        #region Events
        public event EventHandler CloseRequested;
        #endregion

        private List<ScanMergeElement> _MergeResult;
        public List<ScanMergeElement> MergePreview
        {
            get => _MergeResult;
            set => SetProperty(ref _MergeResult, value);
        }

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

        private int _TotalNumberOfPages;
        public int TotalNumberOfPages
        {
            get => _TotalNumberOfPages;
            set => SetProperty(ref _TotalNumberOfPages, value);
        }

        private int _MaxSkippablePages;
        public int MaxSkippablePages
        {
            get => _MaxSkippablePages;
            set => SetProperty(ref _MaxSkippablePages, value);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanMergeDialogViewModel()
        {
            Messenger.Register<SelectedScannerChangedMessage>(this, (r, m) =>
            {
                if (m.Value == null)
                {
                    // scanner lost
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                }
            });
            
            if (ScanResultService.Result == null || ScanResultService.Result.NumberOfPages == 0)
            {
                // invalid result
                ApplicationException exc = new ApplicationException("Invalid result for scan and merge");
                LogService.Log.Error(exc, "Invalid result for scan and merge");
                AppCenterService.TrackError(exc);
                return;
            }

            List<ScanMergeElement> newList = new List<ScanMergeElement>();
            foreach (ScanResultElement element in ScanResultService.Result.Elements)
            {
                newList.Add(new ScanMergeElement
                {
                    IsPotentialPage = false,
                    Thumbnail = element.Thumbnail,
                    ItemDescriptor = element.ItemDescriptor,
                });
            }
            MergePreview = newList;
            TotalNumberOfPages = ScanResultService.Result.NumberOfPages;

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
        private void RefreshMergeResult()
        {
            try
            {
                LogService?.Log.Information("Creating 'Scan and merge' preview for {StartPage} and {SkipPages}",
                    StartPageNumber, SkipPages);
                List<ScanMergeElement> cleanList = new List<ScanMergeElement>(MergePreview);
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
                                ItemDescriptor = LocalizedString("TextScanMergeElementStartPage"),
                                IsStartPage = true,
                                IsOrderReversed = ReversePages
                            });
                        }
                        else
                        {
                            newList.Add(new ScanMergeElement
                            {
                                IsPotentialPage = true,
                                ItemDescriptor = LocalizedString("TextScanMergeElementLastPage"),
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
                                ItemDescriptor = LocalizedString("TextScanMergeElementSurplusPages")
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
                            ItemDescriptor = LocalizedString("TextScanMergeElementSinglePage")
                        });
                    }

                    newList.Add(cleanList[i]);
                    newList[newList.Count - 1].ItemDescriptor = String.Format(LocalizedString("TextPageListDescriptor"), newList.Count);
                }

                if (!newList.Exists((x) => x.IsPlaceholderForMultiplePages))
                {
                    newList.Add(new ScanMergeElement
                    {
                        IsPotentialPage = true,
                        IsPlaceholderForMultiplePages = true,
                        ItemDescriptor = LocalizedString("TextScanMergeElementSurplusPages")
                    });
                }

                MergePreview = newList;
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "Failed to create 'Scan and merge' preview");
                AppCenterService?.TrackError(exc);
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void AcceptConfig()
        {
            ScanMergeConfig config = CreateMergeConfig();
            if (config != null)
            {
                Messenger.Send(new ScanMergeRequestMessage(config));
            }
        }

        private void Cancel()
        {
            LogService?.Log.Information("Scan and merge: Cancel");
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
                AppCenterService?.TrackError(exc);
                CloseRequested?.Invoke(this, EventArgs.Empty);
                return null;
            }
        }
    }
}
