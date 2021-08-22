public static class Enums
{
    /// <summary>
    ///     The possible scanner sources.
    /// </summary>
    public enum ScannerSource
    {
        None = 0,
        Auto = 1,
        Flatbed = 2,
        Feeder = 3
    }


    /// <summary>
    ///     The possible scanner color modes.
    /// </summary>
    public enum ScannerColorMode
    {
        None = 0,
        Color = 1,
        Grayscale = 2,
        Monochrome = 3
    }


    /// <summary>
    ///     The possible file formats.
    /// </summary>
    public enum FileFormat
    {
        JPG = 0,
        PNG = 1,
        TIF = 2,
        BMP = 3,
        PDF = 4,
        XPS = 5,
        OpenXPS = 6,
    }
}