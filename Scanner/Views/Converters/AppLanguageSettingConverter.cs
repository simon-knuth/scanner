using System;
using Windows.Globalization;
using Windows.UI.Xaml.Data;
using static Utilities;

namespace Scanner.Views.Converters
{
    public class AppLanguageSettingConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given <see cref="Language"/> into a display string.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string languageString = value as string;

            if (languageString == "SYSTEM")
            {
                return LocalizedString("OptionSettingsAppLanguageSystem");
            }
            else
            {
                return new Language(languageString).DisplayName;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
