using System;
using System.IO;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class FilePathFolderConverter : IValueConverter
    {
        /// <summary>
        ///     Converts a file path string to a string containing just the parent folder.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string path = (string)value;
            string[] parts = path.Split(Path.DirectorySeparatorChar);

            string result = parts[parts.Length - 2];
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
