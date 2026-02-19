using CommunityToolkit.Mvvm.DependencyInjection;
using Scanner.Models.Interfaces;
using Scanner.Services;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Windows.Devices.Scanners;
using static Scanner.Helpers.Helpers;

namespace Scanner.Models.ItemNaming;

public class ItemNamingPattern
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    [Required(ErrorMessage = "Blocks is required")]
    public IReadOnlyList<IItemNamingBlock> Blocks;

    public bool IsValid;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public ItemNamingPattern(List<IItemNamingBlock> blocks)
    {
        Blocks = blocks;
        IsValid = CheckValidity();
    }

    public ItemNamingPattern(string serialized)
    {
        try
        {
            string[] parts = serialized.Split('*', StringSplitOptions.RemoveEmptyEntries);
            Type[] types = new Type[]
            {
            typeof(string),
            };

            // iterate through blocks
            List<IItemNamingBlock> newList = new List<IItemNamingBlock>();
            foreach (string part in parts)
            {
                Type blockType = ItemNamingStatics.ItemNamingBlocksDictionary[part.Split("|", StringSplitOptions.RemoveEmptyEntries)[0]];
                string[] partArray = new string[1] { part };
                newList.Add(blockType.GetConstructor(types).Invoke(partArray) as IItemNamingBlock);
            }

            Blocks = newList;
            IsValid = CheckValidity();
        }
        catch (Exception exc)
        {
            Ioc.Default.GetService<ILogService>()?.Log.Error(exc, "Failed to generate file naming pattern");
            Ioc.Default.GetService<ISentryService>()?.TrackError(exc);
            throw;
        }
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
    private bool CheckValidity()
    {
        if (Blocks == null)
        {
            return false;
        }

        if (Blocks.Count == 0)
        {
            return false;
        }

        foreach (IItemNamingBlock block in Blocks)
        {
            if (!block.IsValid)
            {
                return false;
            }
        }

        return true;
    }
    
    public string GenerateResult(ScanOptions scanOptions, bool includeFileExtension)
    {
        try
        {
            string result = "";

            foreach (IItemNamingBlock block in Blocks)
            {
                result += block.ToString(scanOptions);
            }

            if (includeFileExtension)
            {
                result = result + TargetFormatToFileExtension(scanOptions.TargetFormat);
            }

            return result;
        }
        catch (Exception exc)
        {
            Ioc.Default.GetService<ILogService>()?.Log.Error(exc, "Failed to generate file name");
            Ioc.Default.GetService<ISentryService>()?.TrackError(exc);

            // fallback to rudimentary legacy file naming
            return "SCN" + DateTime.Now.Hour.ToString("00") + DateTime.Now.Minute.ToString("00") + DateTime.Now.Second.ToString("00"); ;
        }
    }

    public string GetSerialized(bool obfuscated)
    {
        string serialized = "";

        foreach (IItemNamingBlock block in Blocks)
        {
            serialized += block.GetSerialized(obfuscated);
        }

        return serialized;
    }
}
