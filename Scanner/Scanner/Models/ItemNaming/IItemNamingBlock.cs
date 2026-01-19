using Scanner.Models.Interfaces;
using System.ComponentModel;

namespace Scanner.Models.ItemNaming
{
    public interface IItemNamingBlock : INotifyPropertyChanged
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
