using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Graphics.Imaging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.ContentSafety;
using Microsoft.Windows.AI.Imaging;
using Microsoft.Windows.AI.Text;
using Scanner.Extensions;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using Serilog;
using Serilog.Exceptions;
using Serilog.Formatting.Compact;
using Serilog.Sinks.File;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace Scanner.Services
{
    internal partial class CopilotRuntimeService : ObservableObject, ICopilotRuntimeService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        #region Constants
        private const int minImageDescriptionLengthAcceptanceThreshold = 48;
        private const int minNameLengthAcceptanceThreshold = 8;
        private const int maxNameLengthAcceptanceThreshold = 40;

        private const string nameGenerationPrompt = "Reply with a short heading for the following scanned document consisting of 4 words or less: ";
        #endregion

        public bool IsSupported { get; private set; }

        [ObservableProperty]
        private bool areModelsInstalled;

        [ObservableProperty]
        private bool areModelsInstalling;

        private ImageDescriptionGenerator? imageDescriptionGenerator;
        private SemaphoreSlim fileNameModelsSemaphore = new(1, 1);


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public CopilotRuntimeService()
        {
            UpdateStatus();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<string?> TryGenerateFileNameForImageAsync(ImageBuffer imageBuffer, CancellationTokenSource cts)
        {
            // image description
            ImageDescriptionResult descriptionResult;
            try
            {
                if (!IsSupported || !AreModelsInstalled)
                    return null;

                await fileNameModelsSemaphore.WaitAsync();
                await LoadImageDescriptionModelsAsync(cts);

                if (imageDescriptionGenerator == null)
                    return null;

                descriptionResult = await imageDescriptionGenerator.DescribeAsync(imageBuffer, ImageDescriptionKind.BriefDescription, new ContentFilterOptions()).AsTask(cts.Token);
                imageDescriptionGenerator.Dispose();
            }
            catch (Exception exc)
            {
                LogService?.Log.Warning(exc, "CopilotRuntimeService - Failed to generate file name");
                return null;
            }
            finally
            {
                fileNameModelsSemaphore.Release();
            }

            // name generation
            try
            {                
                string description = descriptionResult.Description.Trim();

                // validate description (ignore ImageHasTooMuchText status)
                if (descriptionResult.Status != ImageDescriptionResultStatus.Complete && descriptionResult.Status != ImageDescriptionResultStatus.ImageHasTooMuchText)
                {
                    LogService?.Log.Warning("CopilotRuntimeService - Image description failed with {Status}", descriptionResult.Status);
                    return null;
                }
                if (description.Length < minImageDescriptionLengthAcceptanceThreshold)
                {
                    LogService?.Log.Warning("CopilotRuntimeService - Image description {Length} is below threshold", descriptionResult.Description.Length);
                    return null;
                }

                // generate short name
                using LanguageModel languageModel = await LanguageModel.CreateAsync().AsTask(cts.Token);
                LanguageModelResponseResult languageModelResult = await languageModel.GenerateResponseAsync(nameGenerationPrompt + description, new LanguageModelOptions()).AsTask(cts.Token);

                // clean up name
                string generatedName = languageModelResult.Text.Trim();
                foreach (char invalidChar in Path.GetInvalidFileNameChars())
                {
                    generatedName = generatedName.Replace(invalidChar, ' ');
                }
                generatedName = generatedName.Trim([',', '.', '?', '!', '&', '%', '"']);
                generatedName = generatedName.Trim();

                // validate name
                if (languageModelResult.Status != LanguageModelResponseStatus.Complete)
                {
                    LogService?.Log.Warning("CopilotRuntimeService - Name generation failed with {Status} and {Error}", languageModelResult.Status, languageModelResult.ExtendedError);
                    return null;
                }
                if (languageModelResult.Text.Length < minNameLengthAcceptanceThreshold)
                {
                    LogService?.Log.Warning("CopilotRuntimeService - Name generation {Length} is below threshold", languageModelResult.Text.Length);
                    return null;
                }
                if (languageModelResult.Text.Length > maxNameLengthAcceptanceThreshold)
                {
                    LogService?.Log.Warning("CopilotRuntimeService - Name generation {Length} is above threshold", languageModelResult.Text.Length);
                    return null;
                }

                return generatedName;
            }
            catch (Exception exc)
            {
                LogService?.Log.Warning(exc, "CopilotRuntimeService - Failed to generate file name");
            }
            finally
            {
                fileNameModelsSemaphore.Release();
            }

            return null;
        }

        public async Task TryShowModelsInstallProgressAsync(DispatcherQueue uiDispatcherQueue)
        {
            await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Normal, async () =>
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:windowsupdate"));
            });
        }

        public async Task TryInstallModelsAsync()
        {
            try
            {
                Task[] tasks =
                [
                    ImageDescriptionGenerator.EnsureReadyAsync().AsTask(),
                    LanguageModel.EnsureReadyAsync().AsTask(),
                ];
                await Task.WhenAll(tasks);

                UpdateStatus();
            }
            catch (Exception exc)
            {
                LogService?.Log.Warning(exc, "CopilotRuntimeService - Failed to install models");
            }
        }

        private void UpdateStatus()
        {
            AIFeatureReadyState imageDescriptionGeneratorState = ImageDescriptionGenerator.GetReadyState();
            AIFeatureReadyState languageModelState = LanguageModel.GetReadyState();

            // check support
            IsSupported = imageDescriptionGeneratorState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady;
            if (IsSupported)
                IsSupported = languageModelState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady;

            // check install status
            AreModelsInstalled = imageDescriptionGeneratorState is AIFeatureReadyState.Ready;
            if (AreModelsInstalled)
                AreModelsInstalled = languageModelState is AIFeatureReadyState.Ready;
        }

        private async Task LoadImageDescriptionModelsAsync(CancellationTokenSource? cts)
        {
            if (imageDescriptionGenerator != null)
                return;

            cts ??= new();

            imageDescriptionGenerator = await ImageDescriptionGenerator.CreateAsync().AsTask(cts.Token);
        }

        public async Task PreheatFileNameGenerationModelsAsync()
        {
            await fileNameModelsSemaphore.WaitAsync();
            try
            {
                await LoadImageDescriptionModelsAsync(null);
            }
            finally
            {
                fileNameModelsSemaphore.Release();
            }
        }

        public async Task StopPreheatingFileNameGenerationModelsAsync()
        {
            if (imageDescriptionGenerator == null)
                return;

            await fileNameModelsSemaphore.WaitAsync();
            imageDescriptionGenerator?.Dispose();
            fileNameModelsSemaphore.Release();
        }
    }
}
