using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        public List<IFileNamingBlock> Blocks = new List<IFileNamingBlock>();


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public FileNamingPattern()
        {
            
        }

        public FileNamingPattern(string serialized)
        {           
            if (!string.IsNullOrEmpty(serialized))
            {
                string[] parts = serialized.Split('*', StringSplitOptions.RemoveEmptyEntries);
                Type[] types = new Type[]
                {
                    typeof(string),
                };

                foreach (string part in parts)
                {
                    Type blockType = FileNamingStatics.FileNamingBlocksDictionary[part.Split("|", StringSplitOptions.RemoveEmptyEntries)[0]];
                    string[] partArray = new string[1] { part };
                    Blocks.Add(blockType.GetConstructor(types).Invoke(partArray) as IFileNamingBlock);
                }
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
        public string GenerateResult(ScanOptions scanOptions, DiscoveredScanner scanner)
        {
            string result = "";

            foreach (IFileNamingBlock block in Blocks)
            {
                result += block.ToString(scanOptions, scanner);
            }

            return result;
        }

        public string GetSerialized()
        {
            string serialized = "";

            foreach (IFileNamingBlock block in Blocks)
            {
                serialized += GetSerialized();
            }

            return serialized;
        }
    }
}
