using AssetRipper.TextureDecoder.Bc;
using AssetRipper.TextureDecoder.Dxt;
using AssetRipper.TextureDecoder.Rgb.Formats;
using CUE4Parse.UE4.Assets.Exports.Texture;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace E33ModEditor.Core;

/// <summary>
/// Reads UI textures straight from the game containers via CUE4Parse
/// (no retoc, so installed ~mods do not shadow vanilla) and exports
/// them to PNG for the guidebook.
/// Supported: B8G8R8A8 / R8G8B8A8 raw, G8, DXT1/3/5 (=BC1/2/3), BC4/5/7.
/// </summary>
public static class TextureExportKit
{
    public sealed record TextureInfo(
        string Path,
        string Format,
        int SizeX,
        int SizeY,
        int MipCount,
        long FirstMipBytes);

    public sealed record ExportResult(string Source, string Png, int W, int H, string Format);

    public static IReadOnlyList<TextureInfo> ListTextures(string filter, int max = 200)
    {
        using var index = new GameIndex();
        var provider = index.Provider;
        var matches = provider.Files.Keys
            .Where(k => k.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .Where(k => k.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k)
            .Take(max * 4)
            .ToList();

        var out_ = new List<TextureInfo>();
        var errors = 0;
        foreach (var key in matches)
        {
            if (out_.Count >= max) break;
            // Strip extension for package-object load.
            var path = key.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                ? key[..^".uasset".Length] : key;
            UTexture2D? tex;
            try { tex = provider.LoadPackageObject<UTexture2D>(path); }
            catch (Exception ex)
            {
                if (errors++ < 5) Console.Error.WriteLine($"[texture-info] load threw {key}: {ex.GetType().Name} {ex.Message}");
                continue;
            }
            var mips = tex.PlatformData.Mips;
            if (mips.Length == 0) continue;
            var first = -1;
            for (var i = 0; i < mips.Length; i++)
            {
                try
                {
                    if (mips[i].EnsureValidBulkData(tex.MipDataProvider, i)) { first = i; break; }
                }
                catch { }
            }
            if (first < 0) continue;
            var mip = mips[first];
            long bytes;
            try { bytes = mip.BulkData?.GetDataSize() ?? 0; }
            catch { bytes = 0; }
            out_.Add(new TextureInfo(
                key,
                tex.Format.ToString(),
                tex.PlatformData.SizeX,
                tex.PlatformData.SizeY,
                mips.Length,
                bytes));
        }
        return out_;
    }

    public static IReadOnlyList<ExportResult> ExportTextures(
        IEnumerable<string> assetKeysOrNames, string outDir, int maxMipSize = 2048)
    {
        Directory.CreateDirectory(outDir);
        using var index = new GameIndex();
        var provider = index.Provider;
        var results = new List<ExportResult>();
        foreach (var keyIn in assetKeysOrNames)
        {
            var key = keyIn.Trim();
            if (key.Length == 0) continue;
            // Accept either a full ".../T_Foo.uasset" path or a bare "T_Foo" name.
            string fullKey = key;
            if (!provider.Files.Keys.Contains(fullKey))
            {
                var hit = provider.Files.Keys.FirstOrDefault(k =>
                    k.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) &&
                    (k.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                     Path.GetFileNameWithoutExtension(k).Equals(key, StringComparison.OrdinalIgnoreCase) ||
                     k.Contains(key, StringComparison.OrdinalIgnoreCase)));
                if (hit is null)
                {
                    Console.Error.WriteLine($"[texture-export] not found: {key}");
                    continue;
                }
                fullKey = hit;
            }
            var path = fullKey.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                ? fullKey[..^".uasset".Length] : fullKey;
            UTexture2D tex;
            try { tex = provider.LoadPackageObject<UTexture2D>(path); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[texture-export] load failed {fullKey}: {ex.Message}");
                continue;
            }
            var mipIndex = tex.GetMipIndexByMaxSize(maxMipSize);
            if (mipIndex < 0) mipIndex = tex.GetFirstMipIndex();
            if (mipIndex < 0)
            {
                Console.Error.WriteLine($"[texture-export] no valid mip {fullKey}");
                continue;
            }
            var mip = tex.PlatformData.Mips[mipIndex];
            byte[] src;
            try { src = mip.BulkData?.ReadDataOnce() ?? Array.Empty<byte>(); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[texture-export] bulk read failed {fullKey}: {ex.Message}");
                continue;
            }
            if (src.Length == 0)
            {
                Console.Error.WriteLine($"[texture-export] empty bulk {fullKey}");
                continue;
            }
            byte[]? rgba;
            try { rgba = DecodeToRgba(tex.Format, src, mip.SizeX, mip.SizeY); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[texture-export] decode failed {fullKey} ({tex.Format} {mip.SizeX}x{mip.SizeY}): {ex.Message}");
                continue;
            }
            if (rgba is null)
            {
                Console.Error.WriteLine($"[texture-export] unsupported format {tex.Format} for {fullKey}");
                continue;
            }
            var name = Path.GetFileNameWithoutExtension(fullKey) + ".png";
            var pngPath = Path.Combine(outDir, name);
            try
            {
                using var img = Image.LoadPixelData<Rgba32>(rgba, mip.SizeX, mip.SizeY);
                img.SaveAsPng(pngPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[texture-export] png write failed {pngPath}: {ex.Message}");
                continue;
            }
            results.Add(new ExportResult(fullKey, pngPath, mip.SizeX, mip.SizeY, tex.Format.ToString()));
            Console.WriteLine($"{tex.Format,-10} {mip.SizeX,5}x{mip.SizeY,-5}  {name}");
        }
        return results;
    }

    private static byte[]? DecodeToRgba(EPixelFormat format, byte[] src, int w, int h)
    {
        switch (format)
        {
            case EPixelFormat.PF_R8G8B8A8:
            {
                var out_ = new byte[w * h * 4];
                Buffer.BlockCopy(src, 0, out_, 0, Math.Min(src.Length, out_.Length));
                return out_;
            }
            case EPixelFormat.PF_B8G8R8A8:
            case EPixelFormat.PF_A8R8G8B8:
            {
                var out_ = new byte[w * h * 4];
                var n = Math.Min(src.Length / 4, w * h);
                for (var i = 0; i < n; i++)
                {
                    out_[i * 4 + 0] = src[i * 4 + 2];
                    out_[i * 4 + 1] = src[i * 4 + 1];
                    out_[i * 4 + 2] = src[i * 4 + 0];
                    out_[i * 4 + 3] = src[i * 4 + 3];
                }
                return out_;
            }
            case EPixelFormat.PF_G8:
            case EPixelFormat.PF_R8:
            case EPixelFormat.PF_L8:
            case EPixelFormat.PF_A8:
            {
                var out_ = new byte[w * h * 4];
                var n = Math.Min(src.Length, w * h);
                for (var i = 0; i < n; i++)
                {
                    out_[i * 4 + 0] = src[i];
                    out_[i * 4 + 1] = src[i];
                    out_[i * 4 + 2] = src[i];
                    out_[i * 4 + 3] = 255;
                }
                return out_;
            }
            case EPixelFormat.PF_DXT1:
            {
                var out_ = new byte[w * h * 4];
                DxtDecoder.DecompressDXT1<ColorRGBA<byte>, byte>(src, w, h, out_);
                return out_;
            }
            case EPixelFormat.PF_DXT3:
            {
                var out_ = new byte[w * h * 4];
                DxtDecoder.DecompressDXT3<ColorRGBA<byte>, byte>(src, w, h, out_);
                return out_;
            }
            case EPixelFormat.PF_DXT5:
            {
                var out_ = new byte[w * h * 4];
                DxtDecoder.DecompressDXT5<ColorRGBA<byte>, byte>(src, w, h, out_);
                return out_;
            }
            case EPixelFormat.PF_BC4:
            {
                var gray = new byte[w * h];
                Bc4.Decompress<ColorR<byte>, byte>(src, w, h, gray);
                var out_ = new byte[w * h * 4];
                for (var i = 0; i < w * h; i++)
                {
                    out_[i * 4 + 0] = gray[i];
                    out_[i * 4 + 1] = gray[i];
                    out_[i * 4 + 2] = gray[i];
                    out_[i * 4 + 3] = 255;
                }
                return out_;
            }
            case EPixelFormat.PF_BC5:
            {
                var rg = new byte[w * h * 2];
                Bc5.Decompress<ColorRG<byte>, byte>(src, w, h, rg);
                var out_ = new byte[w * h * 4];
                for (var i = 0; i < w * h; i++)
                {
                    out_[i * 4 + 0] = rg[i * 2 + 0];
                    out_[i * 4 + 1] = rg[i * 2 + 1];
                    out_[i * 4 + 2] = 0;
                    out_[i * 4 + 3] = 255;
                }
                return out_;
            }
            case EPixelFormat.PF_BC7:
            {
                var out_ = new byte[w * h * 4];
                Bc7.Decompress<ColorRGBA<byte>, byte>(src, w, h, out_);
                return out_;
            }
            default:
                return null;
        }
    }
}
