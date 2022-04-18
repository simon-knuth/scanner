using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Windows.Devices.Scanners;
using static Utilities;

namespace Scanner.Models.FileNaming
{
    public class BrightnessFileNamingBlock : ObservableObject, IFileNamingBlock
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string Glyph => "\uF08C";
        public string Name => "BRIGHTNESS";

        public string DisplayName
        {
            get => "Brightness";
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
        public BrightnessFileNamingBlock()
        {

        }

        public static BrightnessFileNamingBlock Deserialize(string serialized)
        {
            return new BrightnessFileNamingBlock();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string ToString(ScanOptions scanOptions, DiscoveredScanner scanner)
        {
            if (scanOptions.Brightness != null)
            {
                if (SkipIfDefault && scanOptions.Brightness != null)
                {
                    switch (scanOptions.Source)
                    {
                        case Enums.ScannerSource.Flatbed:
                            if (scanOptions.Brightness != scanner.FlatbedBrightnessConfig.DefaultBrightness)
                            {
                                return scanOptions.Brightness.Value.ToString();
                            }
                            else
                            {
                                return "";
                            }
                        case Enums.ScannerSource.Feeder:
                            if (scanOptions.Brightness != scanner.FeederBrightnessConfig.DefaultBrightness)
                            {
                                return scanOptions.Brightness.Value.ToString();
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
                    return scanOptions.Brightness.Value.ToString();
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
