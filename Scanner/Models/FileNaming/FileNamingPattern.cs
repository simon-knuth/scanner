using Microsoft.Toolkit.Diagnostics;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Windows.Devices.Scanners;
using static Utilities;

namespace Scanner.Models.FileNaming
{
    public class FileNamingPattern
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        [Required(ErrorMessage = "Blocks is required")]
        public IReadOnlyList<IFileNamingBlock> Blocks;

        public bool IsValid;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public FileNamingPattern(List<IFileNamingBlock> blocks)
        {
            Blocks = blocks;
            IsValid = CheckValidity();
        }

        public FileNamingPattern(string serialized)
        {           
            Guard.IsNotNullOrEmpty(serialized, nameof(serialized));

            string[] parts = serialized.Split('*', StringSplitOptions.RemoveEmptyEntries);
            Type[] types = new Type[]
            {
                typeof(string),
            };

            // iterate through blocks
            List<IFileNamingBlock> newList = new List<IFileNamingBlock>();
            foreach (string part in parts)
            {
                Type blockType = FileNamingStatics.FileNamingBlocksDictionary[part.Split("|", StringSplitOptions.RemoveEmptyEntries)[0]];
                string[] partArray = new string[1] { part };
                newList.Add(blockType.GetConstructor(types).Invoke(partArray) as IFileNamingBlock);
            }

            Blocks = newList;
            IsValid = CheckValidity();
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

            foreach (IFileNamingBlock block in Blocks)
            {
                if (!block.IsValid)
                {
                    return false;
                }
            }

            return true;
        }
        
        public string GenerateResult(ScanOptions scanOptions, DiscoveredScanner scanner)
        {
            string result = "";

            foreach (IFileNamingBlock block in Blocks)
            {
                result += block.ToString(scanOptions, scanner);
            }

            return result + ConvertImageScannerFormatToString(scanOptions.Format.TargetFormat);
        }

        public string GetSerialized()
        {
            string serialized = "";

            foreach (IFileNamingBlock block in Blocks)
            {
                serialized += block.GetSerialized();
            }

            return serialized;
        }
    }
}
