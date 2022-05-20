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

        private string _DisplayName = "Day";
        public string DisplayName
        {
            get => _DisplayName;
            set => SetProperty(ref _DisplayName, value);
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
                    break;
                case DayType.DayOfYear:
                    result = CultureInfo.CurrentCulture.Calendar.GetDayOfYear(currentTime).ToString();
                    break;
                case DayType.DayOfMonth:
                default:
                    result = CultureInfo.CurrentCulture.Calendar.GetDayOfMonth(currentTime).ToString();
                    break;
            }

            if (UseMinimumDigits)
            {
                result = result.PadLeft(MinimumDigits, '0');
            }

            return result;
        }

        public string GetSerialized()
        {
            return $"*{Name}|{(int)Type}|{UseMinimumDigits}|{MinimumDigits}";
        }
    }

    public enum DayType
    {
        DayOfWeek = 0,
        DayOfMonth = 1,
        DayOfYear = 2
    }
}
