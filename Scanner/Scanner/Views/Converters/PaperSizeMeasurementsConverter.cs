using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Scanner.Extensions;
using Scanner.Helpers;
using Scanner.Models;
using Scanner.Models.FileNaming;
using System;

namespace Scanner.Views.Converters
{
    public partial class PaperSizeMeasurementsConverter : IValueConverter
    {
        /// <summary>
        ///     Converts a <see cref="PaperSize"/> to its measurements as a string.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            PaperSize paperSize = (PaperSize)value;
            return $"{Measurement.FromCentimeters(paperSize.ToRect().Width / 10)} × {Measurement.FromCentimeters(paperSize.ToRect().Height / 10)}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}