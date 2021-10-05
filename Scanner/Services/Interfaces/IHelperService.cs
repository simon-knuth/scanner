using System;
using System.Threading.Tasks;
using Windows.Storage;
using static Scanner.Services.SettingsEnums;

namespace Scanner.Services
{
    /// <summary>
    ///     Offers helper methods.
    /// </summary>
    public interface IHelperService
    {
        Task ShowRatingDialogAsync();
    }
}
