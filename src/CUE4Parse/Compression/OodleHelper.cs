using System.Diagnostics.CodeAnalysis;

using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Readers;

using OodleDotNet;

namespace CUE4Parse.Compression;

public class OodleException : ParserException
{
    public OodleException(string? message = null, Exception? innerException = null) : base(message, innerException) { }
    public OodleException(FArchive reader, string? message = null, Exception? innerException = null) : base(reader, message, innerException) { }
}

public static class OodleHelper
{
    public const string OODLE_NAME_OLD = "oo2core_9_win64.dll";
    public const string OODLE_NAME_CURRENT = "oodle-data-shared.dll";
    public const string OODLE_NAME_LINUX = "liboodle-data-shared.so";

    public static Oodle? Instance { get; private set; }

    public static void Initialize(Oodle instance)
    {
        Instance?.Dispose();
        Instance = instance;
        Compression.UseNativeOodle(instance);
    }

    public static void Decompress(
        byte[] compressed, int compressedOffset, int compressedSize,
        byte[] uncompressed, int uncompressedOffset, int uncompressedSize,
        FArchive? reader = null)
    {
        var instance = Instance;
        if (instance is null)
            ThrowDecompressionException(reader, "Oodle decompression failed: not initialized");

        var decodedSize = instance.Decompress(
            compressed.AsSpan(compressedOffset, compressedSize),
            uncompressed.AsSpan(uncompressedOffset, uncompressedSize));

        if (decodedSize <= 0)
            ThrowDecompressionException(reader, $"Oodle decompression failed with result {decodedSize}");
    }

    [DoesNotReturn]
    private static void ThrowDecompressionException(FArchive? reader, string message)
    {
        if (reader is not null) throw new OodleException(reader, message);
        throw new OodleException(message);
    }
}
