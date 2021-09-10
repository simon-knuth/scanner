using System;
using Windows.UI.Core;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class WindowStateOpacityConverter : IValueConverter
    {
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
                    return Double.Parse((string)parameter);
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
