﻿using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Messaging;
using Scanner.Models;
using Scanner.Services.Messenger;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Scanners;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;
using static Scanner.Services.Messenger.MessengerEnums;
using static Utilities;

namespace Scanner.Services
{
    /// <summary>
    ///     Holds the current <see cref="ScanResult"/> and exposes it to other code.
    /// </summary>
    internal class ScanResultService : ObservableRecipient, IScanResultService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();

        public event EventHandler<ScanResult> ScanResultCreated;
        public event EventHandler ScanResultChanging;
        public event EventHandler ScanResultChanged;
        public event EventHandler ScanResultDismissed;

        private ScanResult _Result;
        public ScanResult Result
        {
            get => _Result;
        }

        private bool _IsScanResultChanging;
        public bool IsScanResultChanging
        {
            get => _IsScanResultChanging;
            private set
            {
                bool old = _IsScanResultChanging;
                SetProperty(ref _IsScanResultChanging, value);

                if (old != value)
                {
                    if (value == true) ScanResultChanging?.Invoke(this, EventArgs.Empty);
                    else ScanResultChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private int FutureAccessListIndex;

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanResultService()
        {
            
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task CreateResultFromFilesAsync(IReadOnlyList<StorageFile> files, StorageFolder targetFolder,
            bool fixedFolder)
        {
            if (Result != null)
            {
                _Result = null;
                ScanResultDismissed?.Invoke(this, EventArgs.Empty);
            }

            FutureAccessListIndex = 0;
            _Result = await ScanResult.CreateAsync(files, targetFolder, FutureAccessListIndex, fixedFolder);

            ScanResultCreated?.Invoke(this, Result);
        }

        public async Task AddToResultFromFilesAsync(IReadOnlyList<StorageFile> files, ImageScannerFormat targetFormat,
            StorageFolder targetFolder)
        {
            IsScanResultChanging = true;
            await Result.AddFiles(files, targetFormat, targetFolder, FutureAccessListIndex);
            IsScanResultChanging = false;
        }

        public async Task<bool> RotatePagesAsync(IList<Tuple<int, BitmapRotation>> instructions)
        {
            IsScanResultChanging = true;

            try
            {
                await Result.RotateScansAsync(instructions);
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageRotateHeading"),
                    MessageText = LocalizedString("ErrorMessageRotateBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });
                LogService?.Log.Error(exc, "Rotating pages failed.");

                IsScanResultChanging = false;
                return false;
            }

            IsScanResultChanging = false;
            return true;
        }

        public async Task<bool> RenameAsync(int index, string newDisplayName)
        {
            try
            {
                await Result.RenameScanAsync(index, newDisplayName);
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageRenameHeading"),
                    MessageText = LocalizedString("ErrorMessageRenameBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });
                LogService?.Log.Error(exc, "Renaming failed.");

                return false;
            }

            return true;
        }

        public async Task<bool> RenameAsync(string newDisplayName)
        {
            try
            {
                await Result.RenameScanAsync(newDisplayName);
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageRenameHeading"),
                    MessageText = LocalizedString("ErrorMessageRenameBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });
                LogService?.Log.Error(exc, "Renaming failed.");

                return false;
            }

            return true;
        }

        public async Task<bool> DeleteScanAsync(int index)
        {
            try
            {
                await Result.DeleteScanAsync(index);
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageDeleteHeading"),
                    MessageText = LocalizedString("ErrorMessageDeleteBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });
                LogService?.Log.Error(exc, "Deleting failed.");

                IsScanResultChanging = false;
                return false;
            }

            return true;
        }

        public async Task<bool> DeleteScanAsync(int index, StorageDeleteOption deleteOption)
        {
            try
            {
                await Result.DeleteScanAsync(index);
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageDeleteHeading"),
                    MessageText = LocalizedString("ErrorMessageDeleteBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });
                LogService?.Log.Error(exc, "Deleting failed.");

                IsScanResultChanging = false;
                return false;
            }

            return true;
        }

        public async Task<bool> DeleteScansAsync(List<int> indices)
        {
            try
            {
                await Result.DeleteScansAsync(indices);
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageDeleteHeading"),
                    MessageText = LocalizedString("ErrorMessageDeleteBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });
                LogService?.Log.Error(exc, "Deleting failed.");

                IsScanResultChanging = false;
                return false;
            }

            return true;
        }

        public async Task<bool> DeleteScansAsync(List<int> indices, StorageDeleteOption deleteOption)
        {
            try
            {
                await Result.DeleteScansAsync(indices, deleteOption);
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageDeleteHeading"),
                    MessageText = LocalizedString("ErrorMessageDeleteBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });
                LogService?.Log.Error(exc, "Deleting failed.");

                IsScanResultChanging = false;
                return false;
            }

            return true;
        }

        public async Task<bool> CopyAsync()
        {
            try
            {
                await Result.CopyAsync();
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageCopyHeading"),
                    MessageText = LocalizedString("ErrorMessageCopyBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });

                return false;
            }

            return true;
        }

        public async Task<bool> CopyImageAsync(int index)
        {
            try
            {
                await Result.CopyImageAsync(index);
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageCopyHeading"),
                    MessageText = LocalizedString("ErrorMessageCopyBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });

                return false;
            }

            return true;
        }

        public async Task<bool> CopyImagesAsync()
        {
            try
            {
                await Result.CopyImagesAsync();
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageCopyHeading"),
                    MessageText = LocalizedString("ErrorMessageCopyBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });

                return false;
            }

            return true;
        }

        public async Task<bool> CopyImagesAsync(IList<int> indices)
        {
            try
            {
                await Result.CopyImagesAsync(indices);
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageCopyHeading"),
                    MessageText = LocalizedString("ErrorMessageCopyBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });

                return false;
            }

            return true;
        }

        public async Task OpenWithAsync()
        {
            try
            {
                await Result.OpenWithAsync();
            }
            catch (Exception exc)
            {
                return;
            }

            return;
        }

        public async Task OpenWithAsync(AppInfo appInfo)
        {
            try
            {
                await Result.OpenWithAsync(appInfo);
            }
            catch (Exception exc)
            {
                return;
            }

            return;
        }

        public async Task OpenImageWithAsync(int index)
        {
            try
            {
                await Result.OpenImageWithAsync(index);
            }
            catch (Exception exc)
            {
                return;
            }

            return;
        }

        public async Task OpenImageWithAsync(int index, AppInfo appInfo)
        {
            try
            {
                await Result.OpenImageWithAsync(index, appInfo);
            }
            catch (Exception exc)
            {
                return;
            }

            return;
        }
    }
}
