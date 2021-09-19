using Scanner.ViewModels;
using System;
using Windows.UI.Core;
using Windows.UI.Xaml.Data;

using static Enums;

namespace Scanner.Views.Converters
{
    public class EditorModeIntConverter : IValueConverter
    {
        /// <summary>
        ///     Converts the given enum element into an integer string.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return ((int)value).ToString();
        }


        /// <summary>
        ///     Converts the given integer string into a <see cref="EditorMode"/>.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            switch ((string)value)
            {
                case "0":
                    return EditorMode.Initial;
                case "1":
                    return EditorMode.Crop;
                case "2":
                    return EditorMode.Draw;
                default:
                    throw new ApplicationException("Can't convert " + (int)value + " to EditorMode.");
            }
        }
    }
}
