using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Windows.Devices.Scanners;
using static Utilities;

namespace Scanner.Models.FileNaming
{
    public class MinuteFileNamingBlock : ObservableObject, IFileNamingBlock
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string Glyph => "\uE121";
        public string Name => "MINUTE";

        private string _DisplayName = "Minute";
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
        public MinuteFileNamingBlock()
        {
            
        }

        public static MinuteFileNamingBlock Deserialize(string serialized)
        {
            return new MinuteFileNamingBlock();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string ToString(ScanOptions scanOptions, DiscoveredScanner scanner)
        {
            if (Use2Digits)
            {
                return DateTime.Now.Minute.ToString().PadLeft(2, '0');
            }
            else
            {
                return DateTime.Now.Minute.ToString();
            }
        }

        public string GetSerialized()
        {
            return $"*{Name}|{Use2Digits}";
        }
    }
}
