using Scanner.Models.Interfaces;
using System.ComponentModel;

namespace Scanner.Models.FileNaming
{
    public interface IFileNamingBlock : INotifyPropertyChanged
    {       
        string Glyph
        {
            get;
        }

        string Name
        {
            get;
        }

        string DisplayName
        {
            get;
        }

        bool IsValid
        {
            get;
        }


        string ToString(ScanOptions scanOptions);
        string GetSerialized(bool obfuscated);
    }
}
