using Serilog;

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
    }
}
