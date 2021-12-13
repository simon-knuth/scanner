using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace Scanner.Services
{
    /// <summary>
    ///     Offers helper methods.
    /// </summary>
    public interface IHelperService
    {
        /// <summary>
        ///     Shows the dialog for rating the app. Opens the Microsoft Store, if something goes wrong.
        /// </summary>
        Task ShowRatingDialogAsync();

        /// <summary>
        ///     Moves the <paramref name="file"/> to the <paramref name="targetFolder"/>. Attempts to name
        ///     it <paramref name="desiredName"/>.
        /// </summary>
        /// <param name="file">The file that's to be moved.</param>
        /// <param name="targetFolder">The folder that the file shall be moved to.</param>
        /// <param name="desiredName">The name that the file should ideally have when finished.</param>
        /// <param name="replaceExisting">Replaces file if true, otherwise asks the OS to generate a unique name.</param>
        /// <returns>The final name of the file.</returns>
        Task<string> MoveFileToFolderAsync(StorageFile file, StorageFolder targetFolder, string desiredName, bool replaceExisting);

        /// <summary>
        ///     Converts the <paramref name="file"/> to a <see cref="BitmapImage"/>.
        /// </summary>
        /// <remarks>
        ///     Partially runs on the UI thread.
        /// </remarks>
        Task<BitmapImage> GenerateBitmapFromFileAsync(StorageFile file);

        /// <summary>
        ///     Creates a <see cref="BitmapEncoder"/> for the given <paramref name="encoderFormat"/> that's optimized
        ///     to prevent unneccessarily big results.
        /// </summary>
        Task<BitmapEncoder> CreateOptimizedBitmapEncoderAsync(ImageScannerFormat? encoderFormat, IRandomAccessStream stream);
    }
}
