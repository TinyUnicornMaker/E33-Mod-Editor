using System.Buffers.Binary;
using System.Text;

namespace E33ModEditor.Core.Localization;

/// <summary>
/// Reader/writer for Unreal Engine .locres files (versions 0-3). The game ships
/// version 3 (CityHash) resources; writing always emits version 3, which UE5.4
/// reads regardless of the source version.
/// </summary>
public sealed class LocresFile
{
    private static readonly byte[] Magic = [0x0e, 0x14, 0x74, 0x75, 0x67, 0x4a, 0x03, 0xfc, 0x4a, 0x15, 0x90, 0x9d, 0xc3, 0x37, 0x7f, 0x1b];

    public sealed class Entry
    {
        public required string Key { get; init; }
        public required string Translation { get; set; }
        public uint SourceHash { get; set; }
    }

    public sealed class Namespace
    {
        public required string Name { get; init; }
        public Dictionary<string, Entry> Entries { get; } = new(StringComparer.Ordinal);
    }

    public int Version { get; private set; } = 3;
    public Dictionary<string, Namespace> Namespaces { get; } = new(StringComparer.Ordinal);

    public Namespace GetOrAddNamespace(string name)
    {
        if (!Namespaces.TryGetValue(name, out var ns))
        {
            ns = new Namespace { Name = name };
            Namespaces[name] = ns;
        }
        return ns;
    }

    public string? Get(string ns, string key) =>
        Namespaces.TryGetValue(ns, out var n) && n.Entries.TryGetValue(key, out var e) ? e.Translation : null;

    public void Set(string ns, string key, string translation, uint sourceHash)
    {
        var n = GetOrAddNamespace(ns);
        if (n.Entries.TryGetValue(key, out var existing))
        {
            existing.Translation = translation;
            existing.SourceHash = sourceHash;
        }
        else
        {
            n.Entries[key] = new Entry { Key = key, Translation = translation, SourceHash = sourceHash };
        }
    }

    public static LocresFile Read(string path) => Read(File.ReadAllBytes(path));

    public static LocresFile Read(byte[] data)
    {
        var file = new LocresFile();
        using var ms = new MemoryStream(data, writable: false);
        using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        var first16 = br.ReadBytes(16);
        ulong stringTableOffset = 0;
        if (first16.Length == 16 && first16.AsSpan().SequenceEqual(Magic))
        {
            file.Version = br.ReadByte();
            stringTableOffset = br.ReadUInt64();
        }
        else
        {
            file.Version = 0;
            ms.Position = 0;
        }

        string[] strings = [];
        if (file.Version >= 1)
        {
            ms.Position = checked((long)stringTableOffset);
            var stringCount = br.ReadUInt32();
            strings = new string[stringCount];
            for (var i = 0; i < stringCount; i++)
            {
                strings[i] = ReadFString(br);
                if (file.Version >= 2) br.ReadUInt32(); // reference count
            }
        }

        if (file.Version == 0) ms.Position = 0;
        else ms.Position = 25;

        if (file.Version >= 2) br.ReadUInt32(); // total entry count
        var namespaceCount = br.ReadUInt32();
        for (var i = 0; i < namespaceCount; i++)
        {
            if (file.Version >= 2) br.ReadUInt32(); // namespace hash
            var ns = file.GetOrAddNamespace(ReadFString(br));
            var keyCount = br.ReadUInt32();
            for (var j = 0; j < keyCount; j++)
            {
                if (file.Version >= 2) br.ReadUInt32(); // key hash
                var key = ReadFString(br);
                var sourceHash = br.ReadUInt32();
                string translation;
                if (file.Version >= 1)
                {
                    var index = br.ReadUInt32();
                    translation = index < strings.Length ? strings[index] : "";
                }
                else
                {
                    translation = ReadFString(br);
                }
                ns.Entries[key] = new Entry { Key = key, Translation = translation, SourceHash = sourceHash };
            }
        }

        return file;
    }

    public byte[] Write()
    {
        // Build the unique string table in insertion order and assign indices.
        var stringIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var stringRefs = new List<(string Text, uint Refs)>();
        var ordered = new List<(Namespace Ns, Entry Entry, int Index)>();
        foreach (var ns in Namespaces.Values)
        {
            foreach (var entry in ns.Entries.Values)
            {
                if (!stringIndex.TryGetValue(entry.Translation, out var idx))
                {
                    idx = stringRefs.Count;
                    stringIndex[entry.Translation] = idx;
                    stringRefs.Add((entry.Translation, 0));
                }
                var (text, refs) = stringRefs[idx];
                stringRefs[idx] = (text, refs + 1);
                ordered.Add((ns, entry, idx));
            }
        }

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
        bw.Write(Magic);
        bw.Write((byte)3);
        bw.Write((ulong)0); // patched below

        bw.Write((uint)ordered.Count);
        bw.Write((uint)Namespaces.Count);
        foreach (var ns in Namespaces.Values)
        {
            bw.Write(CityHash64.HashString32(ns.Name));
            WriteFString(bw, ns.Name);
            bw.Write((uint)ns.Entries.Count);
            foreach (var entry in ns.Entries.Values)
            {
                bw.Write(CityHash64.HashString32(entry.Key));
                WriteFString(bw, entry.Key);
                bw.Write(entry.SourceHash);
                bw.Write((uint)stringIndex[entry.Translation]);
            }
        }

        var offset = (ulong)ms.Position;
        ms.Position = 17;
        bw.Write(offset);
        ms.Position = (long)offset;

        bw.Write((uint)stringRefs.Count);
        foreach (var (text, refs) in stringRefs)
        {
            WriteFString(bw, text);
            bw.Write(refs);
        }

        return ms.ToArray();
    }

    public void Write(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, Write());
    }

    private static string ReadFString(BinaryReader br)
    {
        var length = br.ReadInt32();
        if (length == 0) return "";
        if (length > 0)
        {
            var bytes = br.ReadBytes(length);
            var end = Array.IndexOf(bytes, (byte)0);
            if (end >= 0) bytes = bytes[..end];
            return Encoding.UTF8.GetString(bytes);
        }

        var units = -length;
        var data = br.ReadBytes(units * 2);
        var chars = Encoding.Unicode.GetString(data);
        var nul = chars.IndexOf('\0');
        return nul >= 0 ? chars[..nul] : chars;
    }

    private static void WriteFString(BinaryWriter bw, string value)
    {
        if (value.Length == 0)
        {
            bw.Write(0);
            return;
        }

        var ascii = value.All(c => c is >= (char)0x20 and <= (char)0x7E or '\n' or '\r' or '\t');
        if (ascii)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            bw.Write(bytes.Length + 1);
            bw.Write(bytes);
            bw.Write((byte)0);
        }
        else
        {
            bw.Write(-(value.Length + 1));
            bw.Write(Encoding.Unicode.GetBytes(value));
            bw.Write((ushort)0);
        }
    }
}
