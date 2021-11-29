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
using static Utilities;

namespace Scanner.Services
{
    internal class ScanResultService : ObservableRecipient, IScanResultService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();
        private readonly ISettingsService SettingsService = Ioc.Default.GetService<ISettingsService>();

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
            ScanResult.PerformedAutomaticRotation += ScanResult_PerformedAutomaticRotation;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Creates <see cref="ScanResult"/> from <paramref name="files"/> with the same file format and moves
        ///     the files to <paramref name="targetFolder"/>.
        /// </summary>
        public async Task CreateResultFromFilesAsync(IReadOnlyList<StorageFile> files, StorageFolder targetFolder)
        {
            Result = null;

            FutureAccessListIndex = 0;
            Result = await ScanResult.CreateAsync(files, targetFolder, FutureAccessListIndex);

            ScanResultCreated?.Invoke(this, Result);
        }

        /// <summary>
        ///     Creates <see cref="ScanResult"/> from <paramref name="files"/>, converting the result file(s) to the
        ///     specified <paramref name="targetFormat"/> and moving it/them to the <paramref name="targetFolder"/>.
        /// </summary>
        /// <param name="files"></param>
        /// <param name="targetFolder"></param>
        /// <param name="targetFormat"></param>
        /// <returns></returns>
        public async Task CreateResultFromFilesAsync(IReadOnlyList<StorageFile> files, StorageFolder targetFolder,
            ImageScannerFormat targetFormat)
        {
            Result = null;

            FutureAccessListIndex = 0;
            Result = await ScanResult.CreateAsync(files, targetFolder, targetFormat, FutureAccessListIndex);

            ScanResultCreated?.Invoke(this, Result);
        }

        /// <summary>
        ///     Adds <paramref name="files"/> to the existing <see cref="Result"/>, converting the file(s) to the
        ///     specified <paramref name="targetFormat"/> and moving it/them to the <paramref name="targetFolder"/>.
        /// </summary>
        public async Task AddToResultFromFilesAsync(IReadOnlyList<StorageFile> files, ImageScannerFormat? targetFormat,
            StorageFolder targetFolder)
        {
            IsScanResultChanging = true;
            await Result.AddFiles(files, targetFormat, targetFolder, FutureAccessListIndex);
            IsScanResultChanging = false;
        }

        /// <summary>
        ///     Adds <paramref name="files"/> to the existing <see cref="Result"/>, converting the file(s) to the
        ///     specified <paramref name="targetFormat"/>.
        /// </summary>
        public async Task AddToResultFromFilesAsync(IReadOnlyList<StorageFile> files, ImageScannerFormat? targetFormat)
        {
            IsScanResultChanging = true;
            await Result.AddFiles(files, targetFormat, FutureAccessListIndex);
            IsScanResultChanging = false;
        }

        /// <summary>
        ///     Rotates pages according to the <paramref name="instructions"/>.
        /// </summary>
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

        /// <summary>
        ///     Renames the page at <paramref name="index"/> to <paramref name="newDisplayName"/>.
        /// </summary>
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

        /// <summary>
        ///     Renames the result file to <paramref name="newDisplayName"/>.
        /// </summary>
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

        /// <summary>
        ///     Deletes the page at <paramref name="index"/>.
        /// </summary>
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

        /// <summary>
        ///     Deletes the page at <paramref name="index"/> using <paramref name="deleteOption"/>.
        /// </summary>
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

        /// <summary>
        ///     Deletes the page at <paramref name="indices"/>.
        /// </summary>
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

        /// <summary>
        ///     Deletes the page at <paramref name="indices"/> using <paramref name="deleteOption"/>.
        /// </summary>
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

        /// <summary>
        ///     Copies the result file of <see cref="Result"/> to the clipboard.
        /// </summary>
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

        /// <summary>
        ///     Copies the page at <paramref name="index"/> to the clipboard.
        /// </summary>
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

        /// <summary>
        ///     Copies all pages to the clipboard as image files.
        /// </summary>
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

        /// <summary>
        ///     Shows the dialog for opening the result file of <see cref="Result"/> with another app.
        /// </summary>
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

        /// <summary>
        ///     Opens the result file of <see cref="Result"/> with the app specified by <paramref name="appInfo"/>.
        /// </summary>
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

        /// <summary>
        ///     Shows the dialog for opening the page at <paramref name="index"/> with another app.
        /// </summary>
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

        /// <summary>
        ///     Opens the page at <paramref name="index"/> with the app specified by <paramref name="appInfo"/>.
        /// </summary>
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

        /// <summary>
        ///     Crops the page at <paramref name="index"/> using <paramref name="imageCropper"/>.
        /// </summary>
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

        /// <summary>
        ///     Crops the pages at <paramref name="indices"/> using <paramref name="cropRegion"/>.
        /// </summary>
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

        /// <summary>
        ///     Crops the page at <paramref name="index"/> using <paramref name="imageCropper"/> as copy.
        /// </summary>
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

        /// <summary>
        ///     Draws on the page at <paramref name="index"/> using <paramref name="inkCanvas"/>.
        /// </summary>
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

        /// <summary>
        ///     Draws on the page at <paramref name="index"/> using <paramref name="inkCanvas"/> as copy.
        /// </summary>
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

        /// <summary>
        ///     Duplicates the page at <paramref name="index"/>.
        /// </summary>
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

        /// <summary>
        ///     Clears <see cref="Result"/>.
        /// </summary>
        public void DismissScanResult()
        {
            Result = null;
        }

        private void ScanResult_PerformedAutomaticRotation(object sender, EventArgs e)
        {
            // send message, if necessary
            if ((bool)SettingsService?.GetSetting(AppSetting.ShowAutoRotationMessage))
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("PerformedAutoRotationHeading"),
                    MessageText = LocalizedString("PerformedAutoRotationBody"),
                    Severity = AppWideStatusMessageSeverity.Success
                });
                SettingsService?.SetSetting(AppSetting.ShowAutoRotationMessage, false);
            }
        }

        /// <summary>
        ///     Applies the current element order of the pages in <see cref="Result"/> to the result file.
        /// </summary>
        /// <returns></returns>
        public async Task ApplyElementOrderToFilesAsync()
        {
            IsScanResultChanging = true;

            await Result.ApplyElementOrderToFilesAsync();

            IsScanResultChanging = false;
        }
    }
}
