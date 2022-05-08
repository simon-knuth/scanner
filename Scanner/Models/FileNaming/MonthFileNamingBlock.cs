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

        private string _DisplayName = "Month";
        public string DisplayName
        {
            get => _DisplayName;
            set => SetProperty(ref _DisplayName, value);
        }

        private MonthType _Type = MonthType.Number;
        public MonthType Type
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
        public MonthFileNamingBlock()
        {
            
        }

        public MonthFileNamingBlock(string serialized)
        {
            string[] parts = serialized.TrimStart('*').Split('|', StringSplitOptions.RemoveEmptyEntries);
            Type = (MonthType)int.Parse(parts[1]);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string ToString(ScanOptions scanOptions, DiscoveredScanner scanner)
        {
            DateTime currentTime = DateTime.Now;

            switch (Type)
            {
                case MonthType.Number:
                    return CultureInfo.CurrentCulture.Calendar.GetMonth(currentTime).ToString();
                case MonthType.Name:
                    return CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(currentTime.Month);
                case MonthType.ShortName:
                default:
                    return CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(currentTime.Month);
            }
        }

        public string GetSerialized()
        {
            return $"*{Name}|{(int)Type}";
        }
    }

    public enum MonthType
    {
        Number = 0,
        Name = 1,
        ShortName = 2
    }
}
