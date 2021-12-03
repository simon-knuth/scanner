using Scanner.ViewModels;
using System;
using Windows.UI.Xaml.Data;


namespace Scanner.Views.Converters
{
    public class AspectRatioOptionBoolConverter : IValueConverter
    {
        /// <summary>
        ///     Checks whether the given <see cref="AspectRatioOption"/> equals the parameter.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            AspectRatioOption aspectRatioOptionSelected = (AspectRatioOption)value;
            AspectRatioOption aspectRatioOptionComparison = (AspectRatioOption)int.Parse((string)parameter);

            return aspectRatioOptionSelected == aspectRatioOptionComparison;
        }


        /// <summary>
        ///     Converts the given parameter to an <see cref="AspectRatioOption"/>.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            AspectRatioOption aspectRatioOptionSelected = (AspectRatioOption)int.Parse((string)parameter);
            return aspectRatioOptionSelected;
        }
    }
}
