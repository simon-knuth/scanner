using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Windows.Devices.Scanners;
using static Utilities;

namespace Scanner.Models.FileNaming
{
    public class DayFileNamingBlock : ObservableObject, IFileNamingBlock
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string Glyph => "\uE163";
        public string Name => "DAY";

        public string DisplayName
        {
            get => LocalizedString("HeadingFileNamingBlockDay/Text");
        }

        private DayType _Type = DayType.DayOfMonth;
        public DayType Type
        {
            get => _Type;
            set => SetProperty(ref _Type, value);
        }

        private bool _UseMinimumDigits = true;
        public bool UseMinimumDigits
        {
            get => _UseMinimumDigits;
            set => SetProperty(ref _UseMinimumDigits, value);
        }

        private int _MinimumDigits = 2;
        public int MinimumDigits
        {
            get => _MinimumDigits;
            set => SetProperty(ref _MinimumDigits, value);
        }

        private bool _LimitMaxChars = false;
        public bool LimitMaxChars
        {
            get => _LimitMaxChars;
            set => SetProperty(ref _LimitMaxChars, value);
        }

        private int _MaxChars = 3;
        public int MaxChars
        {
            get => _MaxChars;
            set => SetProperty(ref _MaxChars, value);
        }

        public bool IsValid
        {
            get => true;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public DayFileNamingBlock()
        {
            
        }

        public DayFileNamingBlock(string serialized)
        {
            string[] parts = serialized.TrimStart('*').Split('|', StringSplitOptions.RemoveEmptyEntries);
            Type = (DayType)int.Parse(parts[1]);
            UseMinimumDigits = bool.Parse(parts[2]);
            MinimumDigits = int.Parse(parts[3]);
            LimitMaxChars = bool.Parse(parts[4]);
            MaxChars = int.Parse(parts[5]);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string ToString(ScanOptions scanOptions, DiscoveredScanner scanner)
        {
            string result;
            DateTime currentTime = DateTime.Now;

            switch (Type)
            {
                case DayType.DayOfWeek:
                    result = CultureInfo.CurrentCulture.Calendar.GetDayOfWeek(currentTime).ToString();

                    if (LimitMaxChars)
                    {
                        result = result.Substring(0, MaxChars);
                    }
                    break;
                case DayType.DayOfYear:
                    result = CultureInfo.CurrentCulture.Calendar.GetDayOfYear(currentTime).ToString();

                    if (UseMinimumDigits)
                    {
                        result = result.PadLeft(MinimumDigits, '0');
                    }
                    break;
                case DayType.DayOfMonth:
                default:
                    result = CultureInfo.CurrentCulture.Calendar.GetDayOfMonth(currentTime).ToString();

                    if (UseMinimumDigits)
                    {
                        result = result.PadLeft(MinimumDigits, '0');
                    }
                    break;
            }

            return result;
        }

        public string GetSerialized(bool obfuscated)
        {
            return $"*{Name}|{(int)Type}|{UseMinimumDigits}|{MinimumDigits}|{LimitMaxChars}|{MaxChars}";
        }
    }

    public enum DayType
    {
        DayOfWeek = 0,
        DayOfMonth = 1,
        DayOfYear = 2
    }
}
