using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Windows.Devices.Scanners;
using static Utilities;

namespace Scanner.Models.FileNaming
{
    public class SecondFileNamingBlock : ObservableObject, IFileNamingBlock
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string Glyph => "\uE121";
        public string Name => "SECOND";

        private string _DisplayName = "Second";
        public string DisplayName
        {
            get => _DisplayName;
            set => SetProperty(ref _DisplayName, value);
        }

        private bool _Use2Digits;
        public bool Use2Digits
        {
            get => _Use2Digits;
            set => SetProperty(ref _Use2Digits, value);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SecondFileNamingBlock()
        {
            
        }

        public static SecondFileNamingBlock Deserialize(string serialized)
        {
            return new SecondFileNamingBlock();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string ToString(ScanOptions scanOptions, DiscoveredScanner scanner)
        {
            if (Use2Digits)
            {
                return DateTime.Now.Second.ToString().PadLeft(2, '0');
            }
            else
            {
                return DateTime.Now.Second.ToString();
            }
        }

        public string GetSerialized()
        {
            return $"*{Name}|{Use2Digits}";
        }
    }
}
