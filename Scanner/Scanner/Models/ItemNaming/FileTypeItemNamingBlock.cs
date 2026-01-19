using CommunityToolkit.Mvvm.ComponentModel;
using Scanner.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Windows.Devices.Scanners;
using static Scanner.Helpers.Helpers;

namespace Scanner.Models.ItemNaming
{
    public class FileTypeItemNamingBlock : ObservableObject, IItemNamingBlock
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string Glyph => "\uE8A5";
        public string Name => "FILETYPE";

        public string DisplayName
        {
            get => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.FileNamingBlockFileType);
        }

        private bool _AllCaps = true;
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
        public FileTypeItemNamingBlock()
        {

        }

        public FileTypeItemNamingBlock(string serialized)
        {
            string[] parts = serialized.TrimStart('*').Split('|', StringSplitOptions.RemoveEmptyEntries);
            AllCaps = bool.Parse(parts[1]);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string ToString(ScanOptions scanOptions)
        {
            if (AllCaps)
            {
                return TargetFormatToFileExtension(scanOptions.TargetFormat).ToUpper().Split(".")[1];
            }
            else
            {
                return TargetFormatToFileExtension(scanOptions.TargetFormat).Split(".")[1];
            }
        }

        public string GetSerialized(bool obfuscated)
        {
            return $"*{Name}|{AllCaps}";
        }
    }
}
