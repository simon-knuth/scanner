using System;
using System.Collections;
using System.Collections.Generic;
using Windows.UI.Xaml;
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
            
            IList list = (IList)value;
            return list.Count > 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
