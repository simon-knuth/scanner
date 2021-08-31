using Microsoft.UI.Xaml.Controls;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using static Scanner.Services.Messenger.MessengerEnums;

namespace Scanner.Views.Converters
{
    public class AppWideMessageSeverityInfoBarConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given <see cref="AppWideMessageSeverity"/> into the corresponding
        ///     <see cref="InfoBarSeverity"/>.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (InfoBarSeverity)((AppWideMessageSeverity)((int)value));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
