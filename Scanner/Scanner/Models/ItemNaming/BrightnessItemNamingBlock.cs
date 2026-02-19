using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Animation;
using Scanner.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Windows.Devices.Scanners;
using static Scanner.Helpers.Helpers;

namespace Scanner.Models.ItemNaming;

public class BrightnessItemNamingBlock : ObservableObject, IItemNamingBlock
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public string Glyph => "\uE706";
    public string Name => "BRIGHTNESS";

    public string DisplayName
    {
        get => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.Brightness);
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
    public BrightnessItemNamingBlock()
    {

    }

    public BrightnessItemNamingBlock(string serialized)
    {
        string[] parts = serialized.TrimStart('*').Split('|', StringSplitOptions.RemoveEmptyEntries);
        SkipIfDefault = bool.Parse(parts[1]);
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public string ToString(ScanOptions scanOptions)
    {
        if (SkipIfDefault && scanOptions.Brightness == 0)
        {
            return "";
        }
        else
        {
            return scanOptions.Brightness.ToString();
        }
    }

    public string GetSerialized(bool obfuscated)
    {
        return $"*{Name}|{SkipIfDefault}";
    }
}
