using System;
using Windows.UI.Xaml.Data;
using static Scanner.Services.SettingsEnums;

namespace Scanner.Views.Converters
{
    public class SettingAutoRotateSwitchConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given <see cref="SettingAutoRotate"/> into a
        ///     bool for a master switch.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            SettingAutoRotate setting = (SettingAutoRotate)value;

            switch (setting)
            {
                case SettingAutoRotate.Off:
                    return false;
                case SettingAutoRotate.AutoRotate:
                case SettingAutoRotate.AskEveryTime:
                default:
                    return true;
            }
        }

        /// <summary>
        ///     Relays the selected value directly.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return (bool)value;
        }
    }
}
