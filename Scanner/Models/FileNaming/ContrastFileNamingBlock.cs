using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Windows.Devices.Scanners;
using static Utilities;

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
            get => "Contrast";
        }

        private bool _SkipIfDefault = true;
        public bool SkipIfDefault
        {
            get => _SkipIfDefault;
            set => SetProperty(ref _SkipIfDefault, value);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ContrastFileNamingBlock()
        {

        }

        public static ContrastFileNamingBlock Deserialize(string serialized)
        {
            return new ContrastFileNamingBlock();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string ToString(ScanOptions scanOptions, DiscoveredScanner scanner)
        {
            if (scanOptions.Contrast != null)
            {
                if (SkipIfDefault && scanOptions.Contrast != null)
                {
                    switch (scanOptions.Source)
                    {
                        case Enums.ScannerSource.Flatbed:
                            if (scanOptions.Contrast != scanner.FlatbedContrastConfig.DefaultContrast)
                            {
                                return scanOptions.Contrast.Value.ToString();
                            }
                            else
                            {
                                return "";
                            }
                        case Enums.ScannerSource.Feeder:
                            if (scanOptions.Contrast != scanner.FeederContrastConfig.DefaultContrast)
                            {
                                return scanOptions.Contrast.Value.ToString();
                            }
                            else
                            {
                                return "";
                            }
                        default:
                        case Enums.ScannerSource.None:
                        case Enums.ScannerSource.Auto:
                            return "";
                    }
                }
                else
                {
                    return scanOptions.Contrast.Value.ToString();
                }
            }
            else
            {
                return "";
            }
        }

        public string GetSerialized()
        {
            return $"*{Name}|{SkipIfDefault}";
        }
    }
}
