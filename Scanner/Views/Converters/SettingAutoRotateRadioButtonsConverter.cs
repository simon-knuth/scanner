using System;
using Windows.UI.Xaml.Data;
using static Scanner.Services.SettingsEnums;

namespace Scanner.Views.Converters
{
    public class SettingAutoRotateRadioButtonsConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given <see cref="SettingAutoRotate"/> into an
        ///     int for a RadioButton group.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            SettingAutoRotate setting = (SettingAutoRotate)value;

            switch (setting)
            {
                case SettingAutoRotate.Off:
                    return -1;
                case SettingAutoRotate.AutoRotate:
                    return 0;
                case SettingAutoRotate.AskEveryTime:
                default:
                    return 1;
            }
        }

        /// <summary>
        ///     Relays the selected value directly.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            switch ((int)value)
            {
                case 1:
                    return SettingAutoRotate.AskEveryTime;
                case 0:
                    return SettingAutoRotate.AutoRotate;
                case -1:
                default:
                    return SettingAutoRotate.Off;
            }
        }
    }
}
