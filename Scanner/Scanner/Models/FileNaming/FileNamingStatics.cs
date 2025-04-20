using Scanner.Models.Interfaces;
using Scanner.Models.ScanningDevices;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Windows.Devices.Scanners;

namespace Scanner.Models.FileNaming
{
    public static class FileNamingStatics
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public static Dictionary<string, Type> FileNamingBlocksDictionary = new Dictionary<string, Type>
        {
            { "TEXT",  typeof(TextFileNamingBlock)},
            { "HOUR",  typeof(HourFileNamingBlock)},
            { "MINUTE",  typeof(MinuteFileNamingBlock)},
            { "SECOND",  typeof(SecondFileNamingBlock)},
            { "HOURPERIOD",  typeof(HourPeriodFileNamingBlock)},
            { "DAY",  typeof(DayFileNamingBlock)},
            { "MONTH",  typeof(MonthFileNamingBlock)},
            { "YEAR",  typeof(YearFileNamingBlock)},
            { "RESOLUTION",  typeof(ResolutionFileNamingBlock)},
            { "FILETYPE",  typeof(FileTypeFileNamingBlock)},
            { "BRIGHTNESS",  typeof(BrightnessFileNamingBlock)},
            { "CONTRAST",  typeof(ContrastFileNamingBlock)},            
            { "SCANNERNAME",  typeof(ScannerNameFileNamingBlock)},
        };

        public static FileNamingPattern DateTimePattern = new FileNamingPattern(new List<IFileNamingBlock>
        {
            new TextFileNamingBlock
            {
                Text = "SCN_"
            },
            new YearFileNamingBlock(),
            new MonthFileNamingBlock
            {
                Type = MonthType.Number
            },
            new DayFileNamingBlock
            {
                Type = DayType.DayOfMonth
            },
            new TextFileNamingBlock
            {
                Text = "_"
            },
            new HourFileNamingBlock
            {
                Use2Digits = true
            },
            new MinuteFileNamingBlock
            {
                Use2Digits = true
            },
            new SecondFileNamingBlock
            {
                Use2Digits = true
            }
        });

        public static FileNamingPattern DatePattern = new FileNamingPattern(new List<IFileNamingBlock>
        {
            new TextFileNamingBlock
            {
                Text = "SCN_"
            },
            new YearFileNamingBlock(),
            new MonthFileNamingBlock
            {
                Type = MonthType.Number
            },
            new DayFileNamingBlock
            {
                Type = DayType.DayOfMonth
            },
        });

        public static FileNamingPattern DefaultCustomPattern = new FileNamingPattern(new List<IFileNamingBlock>
        {
            new TextFileNamingBlock
            {
                Text = "SCN - "
            },
            new YearFileNamingBlock(),
            new TextFileNamingBlock
            {
                Text = " "
            },
            new MonthFileNamingBlock
            {
                Type = MonthType.Number
            },
            new TextFileNamingBlock
            {
                Text = " "
            },
            new DayFileNamingBlock
            {
                Type = DayType.DayOfMonth
            },
        });


        private static DebugScanner PreviewScanner => new DebugScanner(new DebugScannerSetupProperties
        {
            Name = "IntelliQ TX3000-S",
            IsFlatbedAllowed = true,
        });


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
        public static ScanOptions GetPreviewScanOptions(IScanningDevice? scanner)
        {
            return new ScanOptions(scanner ?? PreviewScanner)
            {
                Brightness = -20,
                Contrast = 5,
                TargetFormat = TargetFormat.PDF,
                Resolution = new ScanResolution(300f, ResolutionAnnotation.Default),
                SourceMode = ScannerSource.Flatbed
            };
        }
    }
}
