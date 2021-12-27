using System;
using System.Collections;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class ListNotNullOrEmptyConverter : IValueConverter
    {
        /// <summary>
        ///     Checks whether the given <see cref="IList"/> is not null or empty
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return false;
            
            IEnumerable list = (IEnumerable)value;
            foreach (var item in list)
            {
                return true;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
