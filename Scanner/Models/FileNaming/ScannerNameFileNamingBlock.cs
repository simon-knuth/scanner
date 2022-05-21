using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Windows.Devices.Scanners;
using static Utilities;

namespace Scanner.Models.FileNaming
{
    public class ScannerNameFileNamingBlock : ObservableObject, IFileNamingBlock
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string Glyph => "\uE294";
        public string Name => "SCANNERNAME";

        public string DisplayName
        {
            get => LocalizedString("HeadingFileNamingBlockScannerName/Text");
        }

        private bool _AllCaps;
        public bool AllCaps
        {
            get => _AllCaps;
            set => SetProperty(ref _AllCaps, value);
        }

        public bool IsValid
        {
            get => true;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScannerNameFileNamingBlock()
        {

        }

        public ScannerNameFileNamingBlock(string serialized)
        {
            string[] parts = serialized.TrimStart('*').Split('|', StringSplitOptions.RemoveEmptyEntries);
            AllCaps = bool.Parse(parts[1]);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string ToString(ScanOptions scanOptions, DiscoveredScanner scanner)
        {
            if (AllCaps)
            {
                return scanner.Name.ToUpper();
            }
            else
            {
                return scanner.Name;
            }
        }

        public string GetSerialized()
        {
            return $"*{Name}|{AllCaps}";
        }
    }
}
