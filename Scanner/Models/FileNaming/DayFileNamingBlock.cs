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
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string ToString(ScanOptions scanOptions, DiscoveredScanner scanner)
        {
            DateTime currentTime = DateTime.Now;

            switch (Type)
            {
                case DayType.DayOfWeek:
                    return CultureInfo.CurrentCulture.Calendar.GetDayOfWeek(currentTime).ToString();
                case DayType.DayOfYear:
                    return CultureInfo.CurrentCulture.Calendar.GetDayOfYear(currentTime).ToString();
                case DayType.DayOfMonth:
                default:
                    return CultureInfo.CurrentCulture.Calendar.GetDayOfMonth(currentTime).ToString();
            }
        }

        public string GetSerialized()
        {
            return $"*{Name}|{(int)Type}";
        }
    }

    public enum DayType
    {
        DayOfWeek = 0,
        DayOfMonth = 1,
        DayOfYear = 2
    }
}
