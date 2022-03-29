using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Windows.Devices.Scanners;
using static Utilities;

namespace Scanner.Models.FileNaming
{
    public class FileTypeFileNamingBlock : ObservableObject, IFileNamingBlock
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string Name => "FILETYPE";

        public string FriendlyName
        {
            get;
            private set;
        }

        private bool _AllCaps;
        public bool AllCaps
        {
            get => _AllCaps;
            set => SetProperty(ref _AllCaps, value);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public FileTypeFileNamingBlock()
        {

        }

        public static FileTypeFileNamingBlock Deserialize(string serialized)
        {
            return new FileTypeFileNamingBlock
            {
                AllCaps = bool.Parse(serialized.Split('|', StringSplitOptions.RemoveEmptyEntries)[1]),
            };
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string ToString(ScanOptions scanOptions, DiscoveredScanner scanner)
        {
            if (AllCaps)
            {
                return scanOptions.Format.TargetFormat.ToString().ToUpper();
            }
            else
            {
                return scanOptions.Format.TargetFormat.ToString();
            }
        }

        public string GetSerialized()
        {
            return $"*{Name}|{AllCaps}";
        }
    }
}
