using CommunityToolkit.Mvvm.ComponentModel;
using Scanner.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Windows.Devices.Scanners;
using static Scanner.Helpers.Helpers;

namespace Scanner.Models.ItemNaming;

public class TextItemNamingBlock : ObservableObject, IItemNamingBlock
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public string Glyph => null;
    public string Name => "TEXT";

    public string DisplayName
    {
        get => GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.FileNamingBlockText);
    }

    private string _Text = "";
    public string Text
    {
        get => _Text;
        set
        {
            if (SetProperty(ref _Text, value))
            {
                IsValid = CheckValidity();
            }
        }
    }

    private bool _IsValid;
    public bool IsValid
    {
        get => _IsValid;
        set => SetProperty(ref _IsValid, value);
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public TextItemNamingBlock()
    {

    }

    public TextItemNamingBlock(string serialized)
    {
        string[] parts = serialized.TrimStart('*').Split('|', StringSplitOptions.RemoveEmptyEntries);
        Text = parts[1];
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public string ToString(ScanOptions scanOptions)
    {
        return Text;
    }

    public string GetSerialized(bool obfuscated)
    {
        string resultText = Text;
        if (obfuscated)
        {
            // obfuscate all alphabetic characters
            Regex regex = new Regex(@"\w", RegexOptions.IgnoreCase);
            resultText = regex.Replace(resultText, "?");
        }
        
        return $"*{Name}|{resultText}";
    }

    public bool CheckValidity()
    {
        // not empty?
        if (string.IsNullOrEmpty(Text))
        {
            return false;
        }
        
        // forbidden chars?
        char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
        foreach (char invalidChar in invalidChars)
        {
            if (Text.Contains(invalidChar.ToString()))
            {
                return false;
            }
        }

        return true;
    }
}
