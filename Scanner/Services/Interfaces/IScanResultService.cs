using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

namespace Scanner.Services
{
    /// <summary>
    ///     Holds the current <see cref="ScanResult"/>.
    /// </summary>
    public interface IScanResultService
    {
        event EventHandler<ScanResult> ScanResultCreated;
        event EventHandler ScanResultDismissed;

        ScanResult Result
        {
            get;
        }

        Task CreateResultFromFilesAsync(IReadOnlyList<StorageFile> files, StorageFolder targetFolder,
            bool fixedFolder);
        Task AddToResultFromFilesAsync(IReadOnlyList<StorageFile> files);
    }
}
