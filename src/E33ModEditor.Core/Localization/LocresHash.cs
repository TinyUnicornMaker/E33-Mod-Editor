using System.Text;

namespace E33ModEditor.Core.Localization;

/// <summary>
/// CRC32 (standard zlib table) of the UTF-32LE representation of a string.
/// This is the SourceStringHash Unreal writes into .locres entries; it must
/// hash the ENGLISH source string, not the translation, or the engine treats
/// the translation as stale.
/// </summary>
public static class LocresHash
{
    private static readonly uint[] Table = BuildTable();

    public static uint SourceHash(string english)
    {
        if (string.IsNullOrEmpty(english)) return 0;
        var crc = 0xFFFFFFFFu;
        foreach (var b in System.Text.Encoding.UTF32.GetBytes(english))
            crc = Step(crc, b);
        return ~crc;
    }

    private static uint Step(uint crc, byte value) =>
        (crc >> 8) ^ Table[(crc ^ value) & 0xFF];

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var bit = 0; bit < 8; bit++)
                c = (c & 1) != 0 ? (c >> 1) ^ 0xEDB88320u : c >> 1;
            table[i] = c;
        }
        return table;
    }
}
