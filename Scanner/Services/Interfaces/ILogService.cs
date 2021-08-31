using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scanner.Services
{
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

        Task<List<Models.LogFile>> GetLogFiles();
    }
}
