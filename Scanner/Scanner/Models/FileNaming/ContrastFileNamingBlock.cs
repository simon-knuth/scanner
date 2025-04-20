using CommunityToolkit.Mvvm.ComponentModel;
using Scanner.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Windows.Devices.Scanners;
using static Scanner.Helpers.Helpers;

namespace Scanner.Models.FileNaming
{
    public class ContrastFileNamingBlock : ObservableObject, IFileNamingBlock
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string Glyph => "\uF08C";
        public string Name => "CONTRAST";

        public string DisplayName
        {
            get => GetLocalized("HeadingFileNamingBlockContrast/Text");
        }

        private bool _SkipIfDefault;
        public bool SkipIfDefault
        {
            get => _SkipIfDefault;
            set => SetProperty(ref _SkipIfDefault, value);
        }

        public bool IsValid
        {
            get => true;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ContrastFileNamingBlock()
        {

        }

        public ContrastFileNamingBlock(string serialized)
        {
            string[] parts = serialized.TrimStart('*').Split('|', StringSplitOptions.RemoveEmptyEntries);
            SkipIfDefault = bool.Parse(parts[1]);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string ToString(ScanOptions scanOptions)
        {
            if (SkipIfDefault && scanOptions.Contrast == 0)
            {
                return "";
            }
            else
            {
                return scanOptions.Contrast.ToString();
            }
        }

        public string GetSerialized(bool obfuscated)
        {
            return $"*{Name}|{SkipIfDefault}";
        }
    }
}
