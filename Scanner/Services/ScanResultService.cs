using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Scanner.Services.Messenger;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
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
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();

        public event EventHandler<ScanResult> ScanResultCreated;
        public event EventHandler ScanResultChanging;
        public event EventHandler ScanResultChanged;
        public event EventHandler ScanResultDismissed;

        private ScanResult _Result;
        public ScanResult Result
        {
            get => _Result;
            private set
            {
                SetProperty(ref _Result, value);
                if (value == null) ScanResultDismissed?.Invoke(this, null);
            }
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
        public async Task CreateResultFromFilesAsync(IReadOnlyList<StorageFile> files, StorageFolder targetFolder)
        {
            Result = null;

            FutureAccessListIndex = 0;
            Result = await ScanResult.CreateAsync(files, targetFolder, FutureAccessListIndex);

            ScanResultCreated?.Invoke(this, Result);
        }

        public async Task CreateResultFromFilesAsync(IReadOnlyList<StorageFile> files, StorageFolder targetFolder, ImageScannerFormat targetFormat)
        {
            Result = null;

            FutureAccessListIndex = 0;
            Result = await ScanResult.CreateAsync(files, targetFolder, targetFormat, FutureAccessListIndex);

            ScanResultCreated?.Invoke(this, Result);
        }

        public async Task AddToResultFromFilesAsync(IReadOnlyList<StorageFile> files, ImageScannerFormat? targetFormat,
            StorageFolder targetFolder)
        {
            IsScanResultChanging = true;
            await Result.AddFiles(files, targetFormat, targetFolder, FutureAccessListIndex);
            IsScanResultChanging = false;
        }

        public async Task AddToResultFromFilesAsync(IReadOnlyList<StorageFile> files, ImageScannerFormat? targetFormat)
        {
            IsScanResultChanging = true;
            await Result.AddFiles(files, targetFormat, FutureAccessListIndex);
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
            IsScanResultChanging = true;
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

            // check if there are still pages left
            if (Result.NumberOfPages == 0)
            {
                Result = null;
            }

            IsScanResultChanging = false;
            return true;
        }

        public async Task<bool> DeleteScanAsync(int index, StorageDeleteOption deleteOption)
        {
            IsScanResultChanging = true;
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

            // check if there are still pages left
            if (Result.NumberOfPages == 0)
            {
                Result = null;
            }

            IsScanResultChanging = false;
            return true;
        }

        public async Task<bool> DeleteScansAsync(List<int> indices)
        {
            IsScanResultChanging = true;
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

            // check if there are still pages left
            if (Result.NumberOfPages == 0)
            {
                Result = null;
            }

            IsScanResultChanging = false;
            return true;
        }

        public async Task<bool> DeleteScansAsync(List<int> indices, StorageDeleteOption deleteOption)
        {
            IsScanResultChanging = true;
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

            // check if there are still pages left
            if (Result.NumberOfPages == 0)
            {
                Result = null;
            }

            IsScanResultChanging = false;
            return true;
        }

        public async Task<bool> CopyAsync()
        {
            try
            {
                await Result.CopyToClipboardAsync();
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
                await Result.CopyImageToClipboardAsync(index);
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
                await Result.CopyImagesToClipboardAsync();
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
                await Result.CopyImagesToClipboardAsync(indices);
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

        public async Task<bool> OpenWithAsync()
        {
            try
            {
                return await Result.OpenWithAsync();
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> OpenWithAsync(AppInfo appInfo)
        {
            try
            {
                return await Result.OpenWithAsync(appInfo);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> OpenImageWithAsync(int index)
        {
            try
            {
                return await Result.OpenImageWithAsync(index);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> OpenImageWithAsync(int index, AppInfo appInfo)
        {
            try
            {
                return await Result.OpenImageWithAsync(index, appInfo);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> CropScanAsync(int index, ImageCropper imageCropper)
        {
            IsScanResultChanging = true;

            try
            {
                await Result.CropScanAsync(index, imageCropper);
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageCropHeading"),
                    MessageText = LocalizedString("ErrorMessageCropBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });
                LogService?.Log.Error(exc, "Cropping page failed.");

                IsScanResultChanging = false;
                return false;
            }

            IsScanResultChanging = false;
            return true;
        }

        public async Task<bool> CropScansAsync(List<int> indices, Rect cropRegion)
        {
            IsScanResultChanging = true;

            try
            {
                await Result.CropScansAsync(indices, cropRegion);
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageCropHeading"),
                    MessageText = LocalizedString("ErrorMessageCropBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });
                LogService?.Log.Error(exc, "Cropping pages failed.");

                IsScanResultChanging = false;
                return false;
            }

            IsScanResultChanging = false;
            return true;
        }

        public async Task<bool> CropScanAsCopyAsync(int index, ImageCropper imageCropper)
        {
            IsScanResultChanging = true;

            try
            {
                await Result.CropScanAsCopyAsync(index, imageCropper);
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageCropHeading"),
                    MessageText = LocalizedString("ErrorMessageCropBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });
                LogService?.Log.Error(exc, "Cropping pages as copy failed.");

                IsScanResultChanging = false;
                return false;
            }

            IsScanResultChanging = false;
            return true;
        }

        public async Task<bool> DrawOnScanAsync(int index, InkCanvas inkCanvas)
        {
            IsScanResultChanging = true;

            try
            {
                await Result.DrawOnScanAsync(index, inkCanvas);
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageHeader"),
                    MessageText = LocalizedString("ErrorMessageBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });
                LogService?.Log.Error(exc, "Drawing on page failed.");

                IsScanResultChanging = false;
                return false;
            }

            IsScanResultChanging = false;
            return true;
        }

        public async Task<bool> DrawOnScanAsCopyAsync(int index, InkCanvas inkCanvas)
        {
            IsScanResultChanging = true;

            try
            {
                await Result.DrawOnScanAsCopyAsync(index, inkCanvas);
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageHeader"),
                    MessageText = LocalizedString("ErrorMessageBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });
                LogService?.Log.Error(exc, "Drawing on page as copy failed.");

                IsScanResultChanging = false;
                return false;
            }

            IsScanResultChanging = false;
            return true;
        }

        public async Task<bool> DuplicatePageAsync(int index)
        {
            IsScanResultChanging = true;

            try
            {
                await Result.DuplicatePageAsync(index);
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageDuplicateHeading"),
                    MessageText = LocalizedString("ErrorMessageDuplicateBody"),
                    Severity = AppWideStatusMessageSeverity.Error,
                    AdditionalText = exc.Message
                });
                LogService?.Log.Error(exc, "Duplicating page failed.");

                IsScanResultChanging = false;
                return false;
            }

            IsScanResultChanging = false;
            return true;
        }

        public void DismissScanResult()
        {
            Result = null;
        }
    }
}
