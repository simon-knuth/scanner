using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
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
        public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetService<IAccessibilityService>(); 
        public readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>(); 
        public readonly ILogService LogService = Ioc.Default.GetService<ILogService>(); 
        public readonly IScanResultService ScanResultService = Ioc.Default.GetRequiredService<IScanResultService>();

        public event EventHandler CloseRequested;

        private List<ScanMergeElement> _MergeResult;
        public List<ScanMergeElement> MergeResult
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
            MergeResult = newList;
            TotalNumberOfPages = ScanResultService.Result.NumberOfPages;
            StartPageNumber = 2;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void RefreshMergeResult()
        {
            try
            {
                List<ScanMergeElement> cleanList = new List<ScanMergeElement>(MergeResult);
                cleanList.RemoveAll((x) => x.IsPotentialPage == true);

                // generate new final pages
                List<ScanMergeElement> newList = new List<ScanMergeElement>();
                for (int i = 0; i < cleanList.Count; i++)
                {
                    if (newList.Count == StartPageNumber - 1)
                    {
                        // start of potential pages
                        newList.Add(new ScanMergeElement
                        {
                            IsPotentialPage = true,
                            ItemDescriptor = String.Format(LocalizedString("TextPageListDescriptor"), newList.Count + 1),
                            IsStartPage = true
                        });

                        // if 0 pages are skipped, add a total of 3 potential pages and then go on,
                        // otherwise would end up adding an infinite amount of potential pages
                        if (SkipPages == 0)
                        {
                            newList.Add(new ScanMergeElement
                            {
                                IsPotentialPage = true,
                                IsPlaceholderForMultiplePages = true,
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
                            ItemDescriptor = String.Format(LocalizedString("TextPageListDescriptor"), newList.Count + 1),
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
                    });
                }

                MergeResult = newList;
            }
            catch (Exception exc)
            {

                throw;
            }
        }
    }
}
