using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Windows.Devices.Scanners;
using static Utilities;

namespace Scanner.Models.FileNaming
{
    public class TextFileNamingBlock : ObservableObject, IFileNamingBlock
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string Glyph => null;
        public string Name => "TEXT";

        public string DisplayName
        {
            get => "Text";
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
        public TextFileNamingBlock()
        {

        }

        public TextFileNamingBlock(string serialized)
        {
            string[] parts = serialized.TrimStart('*').Split('|', StringSplitOptions.RemoveEmptyEntries);
            Text = parts[1];
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string ToString(ScanOptions scanOptions, DiscoveredScanner scanner)
        {
            return Text;
        }

        public string GetSerialized()
        {
            return $"*{Name}|{Text}";
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
}
