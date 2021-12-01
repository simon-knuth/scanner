using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Scanner.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using Windows.UI.Core;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class WindowStateOpacityConverter : IValueConverter
    {
        private readonly ILogService LogService = Ioc.Default.GetRequiredService<ILogService>();
        private readonly IAppCenterService AppCenterService = Ioc.Default.GetRequiredService<IAppCenterService>();


        /// <summary>
        ///     Converts the given <see cref="CoreWindowActivationState"/> into an
        ///     opacity value to expose the current window state to the user as per
        ///     guidelines.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var windowActivationState = (CoreWindowActivationState) value;

            if (windowActivationState == CoreWindowActivationState.Deactivated)
            {
                // window deactivated
                return 0.4;
            }
            else
            {
                // window activated
                if (parameter != null)
                {
                    // allow override of default value
                    try
                    {
                        CultureInfo cultureInfo = CultureInfo.GetCultureInfoByIetfLanguageTag("en-us");
                        return Double.Parse((string)parameter, cultureInfo);
                    }
                    catch (Exception exc)
                    {
                        LogService.Log.Error(exc, "Error while parsing value in WindowStateOpacityConverter");
                        AppCenterService.TrackError(exc, new Dictionary<string, string> {
                            { "Parameter", (string)parameter },
                        });
                        return 1.0;
                    }
                }
                else
                {
                    return 1.0;
                }
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
