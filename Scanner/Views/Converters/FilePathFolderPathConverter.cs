using System;
using System.IO;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Scanner.Views.Converters
{
    public class FilePathFolderPathConverter : IValueConverter
    {
        /// <summary>
        ///     Converts a file path string to the path of the containing folder.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string path = (string)value;
            int lastDirectorySeparatorIndex = path.LastIndexOf(Path.DirectorySeparatorChar);

            string result = path.Substring(0, lastDirectorySeparatorIndex);
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
