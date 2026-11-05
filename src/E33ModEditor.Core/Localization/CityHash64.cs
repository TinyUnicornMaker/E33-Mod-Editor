using System.Buffers.Binary;
using System.Text;

namespace E33ModEditor.Core.Localization;

/// <summary>
/// CityHash64 (UTF-16LE) as used by UE5 locres version 3 for namespace/key
/// hashes, plus the 32-bit fold UE uses for source-string hashes.
/// Ported from the reference implementation (cityhash.py in pylocres).
/// </summary>
public static class CityHash64
{
    private const ulong K0 = 0xc3a5c85c97cb3127;
    private const ulong K1 = 0xb492b66fbe98f273;
    private const ulong K2 = 0x9ae16a3b2f90404f;
    private const ulong Mul = 0x9ddfea08eb382d69;

    public static uint HashString32(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        var bytes = Encoding.Unicode.GetBytes(value);
        var h = Hash64(bytes);
        unchecked
        {
            return (uint)((h & 0xFFFFFFFF) + ((h >> 32) * 23));
        }
    }

    private static ulong Hash64(byte[] data)
    {
        var length = data.Length;
        unchecked
        {
            if (length <= 32)
                return length <= 16 ? HashLen0To16(data, 0, length) : HashLen17To32(data, length);
            if (length <= 64) return HashLen33To64(data, length);

            var x = Fetch64(data, length - 40);
            var y = Fetch64(data, length - 16) + Fetch64(data, length - 56);
            var z = HashLen16(Fetch64(data, length - 48) + (ulong)length, Fetch64(data, length - 24));

            var v = WeakHashLen32WithSeeds(data, length - 64, (ulong)length, z);
            var w = WeakHashLen32WithSeeds(data, length - 32, y + K1, x);
            x = x * K1 + Fetch64(data, 0);

            var chunkLength = (length - 1) & ~63;
            var offset = 0;
            while (chunkLength != 0)
            {
                x = Rotate(x + y + v.Item1 + Fetch64(data, offset + 8), 37) * K1;
                y = Rotate(y + v.Item2 + Fetch64(data, offset + 48), 42) * K1;
                x ^= w.Item2;
                y += v.Item1 + Fetch64(data, offset + 40);
                z = Rotate(z + w.Item1, 33) * K1;
                v = WeakHashLen32WithSeeds(data, offset, v.Item2 * K1, x + w.Item1);
                w = WeakHashLen32WithSeeds(data, offset + 32, z + w.Item2, y + Fetch64(data, offset + 16));
                (z, x) = (x, z);
                offset += 64;
                chunkLength -= 64;
            }

            return HashLen16(
                HashLen16(v.Item1, w.Item1) + ShiftMix(y) * K1 + z,
                HashLen16(v.Item2, w.Item2) + x);
        }
    }

    private static ulong HashLen0To16(byte[] data, int offset, int length)
    {
        unchecked
        {
            if (length >= 8)
            {
                var mul = K2 + (ulong)length * 2;
                var a = Fetch64(data, offset) + K2;
                var b = Fetch64(data, offset + length - 8);
                var c = Rotate(b, 37) * mul + a;
                var d = (Rotate(a, 25) + b) * mul;
                return HashLen16(c, d, mul);
            }

            if (length >= 4)
            {
                var mul = K2 + (ulong)length * 2;
                var a = Fetch32(data, offset);
                return HashLen16((ulong)length + ((ulong)a << 3), Fetch32(data, offset + length - 4), mul);
            }

            if (length > 0)
            {
                var a = data[offset];
                var b = data[offset + (length >> 1)];
                var c = data[offset + length - 1];
                var y = (ulong)a + ((ulong)b << 8);
                var z = (ulong)length + ((ulong)c << 2);
                return ShiftMix((y * K2) ^ (z * K0)) * K2;
            }

            return K2;
        }
    }

    private static ulong HashLen17To32(byte[] data, int length)
    {
        unchecked
        {
            var mul = K2 + (ulong)length * 2;
            var a = Fetch64(data, 0) * K1;
            var b = Fetch64(data, 8);
            var c = Fetch64(data, length - 8) * mul;
            var d = Fetch64(data, length - 16) * K2;
            return HashLen16(
                Rotate(a + b, 43) + Rotate(c, 30) + d,
                a + Rotate(b + K2, 18) + c,
                mul);
        }
    }

    private static ulong HashLen33To64(byte[] data, int length)
    {
        unchecked
        {
            var mul = K2 + (ulong)length * 2;
            var a = Fetch64(data, 0) * K2;
            var b = Fetch64(data, 8);
            var c = Fetch64(data, length - 24);
            var d = Fetch64(data, length - 32);
            var e = Fetch64(data, 16) * K2;
            var f = Fetch64(data, 24) * 9;
            var g = Fetch64(data, length - 8);
            var h = Fetch64(data, length - 16) * mul;

            var u = Rotate(a + g, 43) + (Rotate(b, 30) + c) * 9;
            var v = ((a + g) ^ d) + f + 1;
            var w = BinaryPrimitives.ReverseEndianness((u + v) * mul) + h;
            var x = Rotate(e + f, 42) + c;
            var y = (BinaryPrimitives.ReverseEndianness((v + w) * mul) + g) * mul;
            var z = e + f + c;

            a = BinaryPrimitives.ReverseEndianness((x + z) * mul + y) + b;
            b = ShiftMix((z + a) * mul + d + h) * mul;
            return b + x;
        }
    }

    private static (ulong, ulong) WeakHashLen32WithSeeds(byte[] data, int offset, ulong a, ulong b)
    {
        unchecked
        {
            var w = Fetch64(data, offset);
            var x = Fetch64(data, offset + 8);
            var y = Fetch64(data, offset + 16);
            var z = Fetch64(data, offset + 24);

            a += w;
            b = Rotate(b + a + z, 21);
            var c = a;
            a += x;
            a += y;
            b += Rotate(a, 44);
            return (a + z, b + c);
        }
    }

    private static ulong HashLen16(ulong u, ulong v)
    {
        unchecked
        {
            var a = (u ^ v) * Mul;
            a ^= a >> 47;
            var b = (v ^ a) * Mul;
            b ^= b >> 47;
            b *= Mul;
            return b;
        }
    }

    private static ulong HashLen16(ulong u, ulong v, ulong mul)
    {
        unchecked
        {
            var a = (u ^ v) * mul;
            a ^= a >> 47;
            var b = (v ^ a) * mul;
            b ^= b >> 47;
            b *= mul;
            return b;
        }
    }

    private static ulong Rotate(ulong value, int shift) =>
        shift == 0 ? value : (value >> shift) | (value << (64 - shift));

    private static ulong ShiftMix(ulong value) => value ^ (value >> 47);

    private static ulong Fetch64(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset, 8));

    private static ulong Fetch32(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
}
