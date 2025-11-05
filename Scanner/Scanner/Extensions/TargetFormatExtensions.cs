using Microsoft.UI.Dispatching;
using Scanner.Models;
using System;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace Scanner.Extensions
{
    public static class TargetFormatExtensions
    {
        public static string GetFriendlyName(this TargetFormat targetFormat)
        {
            switch (targetFormat)
            {
                case TargetFormat.PDF:
                case TargetFormat.JPG:
                case TargetFormat.PNG:
                case TargetFormat.BMP:
                case TargetFormat.TIFF:
                case TargetFormat.RAW:
                    return targetFormat.ToString();
                case TargetFormat.SinglePagePDF:
                    return Resources.Strings.Resources.FileFormatSinglePagePDF;
                case TargetFormat.None:
                default:
                    return string.Empty;
            }
        }
    }
}
