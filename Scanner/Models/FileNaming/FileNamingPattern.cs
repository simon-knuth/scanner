using Microsoft.Toolkit.Diagnostics;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Scanner.Services;
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
            try
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
            catch (Exception exc)
            {
                ILogService logService = Ioc.Default.GetService<ILogService>();
                IAppCenterService appCenterService = Ioc.Default.GetService<IAppCenterService>();

                logService?.Log.Error(exc, "Generating file naming pattern failed");
                appCenterService.TrackError(exc);
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
            try
            {
                string result = "";

                foreach (IFileNamingBlock block in Blocks)
                {
                    result += block.ToString(scanOptions, scanner);
                }

                return result + ConvertImageScannerFormatToString(scanOptions.Format.TargetFormat);
            }
            catch (Exception exc)
            {
                ILogService logService = Ioc.Default.GetService<ILogService>();
                IAppCenterService appCenterService = Ioc.Default.GetService<IAppCenterService>();

                logService.Log.Error(exc, "Generating file name failed");
                appCenterService.TrackError(exc);

                // fallback to rudimentary legacy file naming
                return "SCN" + DateTime.Now.Hour.ToString("00") + DateTime.Now.Minute.ToString("00") + DateTime.Now.Second.ToString("00"); ;
            }
        }

        public string GetSerialized(bool obfuscated)
        {
            string serialized = "";

            foreach (IFileNamingBlock block in Blocks)
            {
                serialized += block.GetSerialized(obfuscated);
            }

            return serialized;
        }
    }
}
