using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Windows.Devices.Scanners;
using static Utilities;

namespace Scanner.Models.FileNaming
{
    public class FileTypeFileNamingBlock : ObservableObject, IFileNamingBlock
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string Glyph => "\uE8A5";
        public string Name => "FILETYPE";

        public string DisplayName
        {
            get => "File type";
        }

        private bool _AllCaps = true;
        public bool AllCaps
        {
            get => _AllCaps;
            set => SetProperty(ref _AllCaps, value);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public FileTypeFileNamingBlock()
        {

        }

        public static FileTypeFileNamingBlock Deserialize(string serialized)
        {
            return new FileTypeFileNamingBlock
            {
                AllCaps = bool.Parse(serialized.Split('|', StringSplitOptions.RemoveEmptyEntries)[1]),
            };
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string ToString(ScanOptions scanOptions, DiscoveredScanner scanner)
        {
            if (AllCaps)
            {
                return ConvertImageScannerFormatToString(scanOptions.Format.TargetFormat).ToUpper().Split(".")[1];
            }
            else
            {
                return ConvertImageScannerFormatToString(scanOptions.Format.TargetFormat).Split(".")[1];
            }
        }

        public string GetSerialized()
        {
            return $"*{Name}|{AllCaps}";
        }
    }
}
