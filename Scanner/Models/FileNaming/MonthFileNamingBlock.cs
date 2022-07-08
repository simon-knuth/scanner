using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Windows.Devices.Scanners;
using static Utilities;

namespace Scanner.Models.FileNaming
{
    public class MonthFileNamingBlock : ObservableObject, IFileNamingBlock
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string Glyph => "\uE163";
        public string Name => "MONTH";

        public string DisplayName
        {
            get => LocalizedString("HeadingFileNamingBlockMonth/Text");
        }

        private MonthType _Type = MonthType.Number;
        public MonthType Type
        {
            get => _Type;
            set => SetProperty(ref _Type, value);
        }

        private bool _AllCaps;
        public bool AllCaps
        {
            get => _AllCaps;
            set => SetProperty(ref _AllCaps, value);
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
        public MonthFileNamingBlock()
        {
            
        }

        public MonthFileNamingBlock(string serialized)
        {
            string[] parts = serialized.TrimStart('*').Split('|', StringSplitOptions.RemoveEmptyEntries);
            Type = (MonthType)int.Parse(parts[1]);
            AllCaps = bool.Parse(parts[2]);
            UseMinimumDigits = bool.Parse(parts[3]);
            MinimumDigits = int.Parse(parts[4]);
            LimitMaxChars = bool.Parse(parts[5]);
            MaxChars = int.Parse(parts[6]);
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
                case MonthType.Number:
                    result = CultureInfo.CurrentUICulture.Calendar.GetMonth(currentTime).ToString();

                    if (UseMinimumDigits)
                    {
                        result = result.PadLeft(MinimumDigits, '0');
                    }
                    break;
                case MonthType.Name:
                    result = CultureInfo.CurrentUICulture.DateTimeFormat.GetMonthName(currentTime.Month);

                    if (LimitMaxChars)
                    {
                        result = result.Substring(0, MaxChars);
                    }
                    break;
                case MonthType.ShortName:
                default:
                    result = CultureInfo.CurrentUICulture.DateTimeFormat.GetAbbreviatedMonthName(currentTime.Month);

                    if (LimitMaxChars)
                    {
                        result = result.Substring(0, MaxChars);
                    }
                    break;
            }

            if (AllCaps)
            {
                result = result.ToUpper();
            }

            return result;
        }

        public string GetSerialized(bool obfuscated)
        {
            return $"*{Name}|{(int)Type}|{AllCaps}|{UseMinimumDigits}|{MinimumDigits}|{LimitMaxChars}|{MaxChars}";
        }
    }

    public enum MonthType
    {
        Number = 0,
        Name = 1,
        ShortName = 2
    }
}
