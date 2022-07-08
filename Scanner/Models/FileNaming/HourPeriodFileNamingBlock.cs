using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Windows.Devices.Scanners;
using static Utilities;

namespace Scanner.Models.FileNaming
{
    public class HourPeriodFileNamingBlock : ObservableObject, IFileNamingBlock
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string Glyph => "\uE121";
        public string Name => "HOURPERIOD";

        public string DisplayName
        {
            get => LocalizedString("HeadingFileNamingBlockHourPeriod/Text");
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
        public HourPeriodFileNamingBlock()
        {
            
        }

        public HourPeriodFileNamingBlock(string serialized)
        {
            string[] parts = serialized.TrimStart('*').Split('|', StringSplitOptions.RemoveEmptyEntries);
            AllCaps = bool.Parse(parts[1]);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string ToString(ScanOptions scanOptions, DiscoveredScanner scanner)
        {
            string result;
            
            DateTime currentTime = DateTime.Now;
            result = currentTime.ToString("tt").ToUpper();

            if (string.IsNullOrWhiteSpace(result))
            {
                // fallback to American English for languages that don't have a 24-hour system
                result = currentTime.ToString("tt", CultureInfo.GetCultureInfoByIetfLanguageTag("en-us").DateTimeFormat);
            }
            
            if (AllCaps)
            {
                result = result.ToUpper();
            }
            else
            {
                result = result.ToLower();
            }

            return result;
        }

        public string GetSerialized(bool obfuscated)
        {
            return $"*{Name}|{AllCaps}";
        }
    }
}
