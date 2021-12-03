using Serilog;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scanner.Services
{
    /// <summary>
    ///     Manages and exposes the application event log.
    /// </summary>
    public interface ILogService
    {
        ILogger Log
        {
            get;
        }

        string LogFolder
        {
            get;
        }

        Task InitializeAsync();
        Task<List<Models.LogFile>> GetLogFiles();
        void CloseAndFlush();
    }
}
