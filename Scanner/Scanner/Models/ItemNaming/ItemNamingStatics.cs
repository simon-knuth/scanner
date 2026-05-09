using Scanner.Models.Interfaces;
using Scanner.Models.ScanningDevices;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Windows.Devices.Scanners;

namespace Scanner.Models.ItemNaming;

public static class ItemNamingStatics
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public static Dictionary<string, Type> ItemNamingBlocksDictionary = new()
    {
        { "TEXT",  typeof(TextItemNamingBlock)},
        { "HOUR",  typeof(HourItemNamingBlock)},
        { "MINUTE",  typeof(MinuteItemNamingBlock)},
        { "SECOND",  typeof(SecondItemNamingBlock)},
        { "HOURPERIOD",  typeof(HourPeriodItemNamingBlock)},
        { "DAY",  typeof(DayItemNamingBlock)},
        { "MONTH",  typeof(MonthItemNamingBlock)},
        { "YEAR",  typeof(YearItemNamingBlock)},
        { "RESOLUTION",  typeof(ResolutionItemNamingBlock)},
        { "FILETYPE",  typeof(FileTypeItemNamingBlock)},
        { "BRIGHTNESS",  typeof(BrightnessItemNamingBlock)},
        { "CONTRAST",  typeof(ContrastItemNamingBlock)},            
        { "SCANNERNAME",  typeof(ScannerNameItemNamingBlock)},
    };

    public static ItemNamingPattern FileDateTimePattern = new(
    [
        new TextItemNamingBlock
        {
            Text = "SCN_"
        },
        new YearItemNamingBlock(),
        new MonthItemNamingBlock
        {
            Type = MonthType.Number
        },
        new DayItemNamingBlock
        {
            Type = DayType.DayOfMonth
        },
        new TextItemNamingBlock
        {
            Text = "_"
        },
        new HourItemNamingBlock
        {
            Use2Digits = true
        },
        new MinuteItemNamingBlock
        {
            Use2Digits = true
        },
        new SecondItemNamingBlock
        {
            Use2Digits = true
        }
    ]);

    public static ItemNamingPattern FileDatePattern = new(
    [
        new TextItemNamingBlock
        {
            Text = "SCN_"
        },
        new YearItemNamingBlock(),
        new MonthItemNamingBlock
        {
            Type = MonthType.Number
        },
        new DayItemNamingBlock
        {
            Type = DayType.DayOfMonth
        },
    ]);

    public static ItemNamingPattern FileDefaultCustomPattern = new(
    [
        new TextItemNamingBlock
        {
            Text = "SCN - "
        },
        new YearItemNamingBlock(),
        new TextItemNamingBlock
        {
            Text = " "
        },
        new MonthItemNamingBlock
        {
            Type = MonthType.Number
        },
        new TextItemNamingBlock
        {
            Text = " "
        },
        new DayItemNamingBlock
        {
            Type = DayType.DayOfMonth
        },
    ]);

    public static ItemNamingPattern FolderDatePattern = new(
    [
        new YearItemNamingBlock(),
        new TextItemNamingBlock
        {
            Text = "-"
        },
        new MonthItemNamingBlock
        {
            Type = MonthType.Number,
            UseMinimumDigits = true,
            MinimumDigits = 2,
        },
        new TextItemNamingBlock
        {
            Text = "-"
        },
        new DayItemNamingBlock
        {
            Type = DayType.DayOfMonth,
            UseMinimumDigits = true,
            MinimumDigits = 2,
        },
    ]);

    public static ItemNamingPattern FolderFileTypePattern = new(
    [
        new FileTypeItemNamingBlock
        {
            AllCaps = true
        },
    ]);

    public static ItemNamingPattern FolderDefaultCustomPattern = new(
    [
        new YearItemNamingBlock(),
        new TextItemNamingBlock
        {
            Text = " "
        },
        new MonthItemNamingBlock
        {
            Type = MonthType.Number
        },
        new TextItemNamingBlock
        {
            Text = " "
        },
        new DayItemNamingBlock
        {
            Type = DayType.DayOfMonth
        },
    ]);

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
            SourceMode = ScannerSource.Flatbed,
            ScanTime = DateTime.Now
        };
    }
}
