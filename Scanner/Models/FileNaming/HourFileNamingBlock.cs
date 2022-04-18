using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Windows.Devices.Scanners;
using static Utilities;

namespace Scanner.Models.FileNaming
{
    public class HourFileNamingBlock : ObservableObject, IFileNamingBlock
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string Glyph => "\uE121";
        public string Name => "HOUR";

        private string _DisplayName = "Hour";
        public string DisplayName
        {
            get => _DisplayName;
            set => SetProperty(ref _DisplayName, value);
        }

        private bool _Use24Hours;
        public bool Use24Hours
        {
            get => _Use24Hours;
            set => SetProperty(ref _Use24Hours, value);
        }

        private bool _Use2Digits = true;
        public bool Use2Digits
        {
            get => _Use2Digits;
            set => SetProperty(ref _Use2Digits, value);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public HourFileNamingBlock()
        {
            
        }

        public static HourFileNamingBlock Deserialize(string serialized)
        {
            return new HourFileNamingBlock();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string ToString(ScanOptions scanOptions, DiscoveredScanner scanner)
        {
            DateTime currentTime = DateTime.Now;
            string result = "";
            if (Use24Hours)
            {
                result = currentTime.Hour.ToString(new CultureInfo("de-DE"));
            }
            else
            {
                result = currentTime.Hour.ToString(new CultureInfo("en-US"));
            }

            if (Use2Digits)
            {
                return result.PadLeft(2, '0');
            }
            else
            {
                return result;
            }
        }

        public string GetSerialized()
        {
            return $"*{Name}|{Use24Hours}|{Use2Digits}";
        }
    }
}
