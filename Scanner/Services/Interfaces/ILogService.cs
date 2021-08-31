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

        void Error(Exception exception);
        void Error(string text);
        void Error(Exception exception, string text);

        void Warning(Exception exception);
        void Warning(string text);
        void Warning(Exception exception, string text);

        void Information(Exception exception);
        void Information(string text);
        void Information(Exception exception, string text);
    }
}
