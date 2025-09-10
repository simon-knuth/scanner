using CommunityToolkit.Mvvm.ComponentModel;
using Scanner.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Windows.Devices.Scanners;
using static Scanner.Helpers.Helpers;

namespace Scanner.Models.FileNaming
{
    public class SecondFileNamingBlock : ObservableObject, IFileNamingBlock
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string Glyph => "\uE121";
        public string Name => "SECOND";

        public string DisplayName
        {
            get => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.FileNamingBlockSecond);
        }

        private bool _Use2Digits = true;
        public bool Use2Digits
        {
            get => _Use2Digits;
            set => SetProperty(ref _Use2Digits, value);
        }

        public bool IsValid
        {
            get => true;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SecondFileNamingBlock()
        {
            
        }

        public SecondFileNamingBlock(string serialized)
        {
            string[] parts = serialized.TrimStart('*').Split('|', StringSplitOptions.RemoveEmptyEntries);
            Use2Digits = bool.Parse(parts[1]);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string ToString(ScanOptions scanOptions)
        {
            if (Use2Digits)
            {
                return scanOptions.ScanTime.Second.ToString().PadLeft(2, '0');
            }
            else
            {
                return scanOptions.ScanTime.Second.ToString();
            }
        }

        public string GetSerialized(bool obfuscated)
        {
            return $"*{Name}|{Use2Digits}";
        }
    }
}
