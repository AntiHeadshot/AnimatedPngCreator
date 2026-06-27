using System;
using System.IO;
using System.IO.Compression;

namespace AntiPebbleNG;

public static class PngIdatCodec
{
    public enum PngFilterType : byte
    {
        None = 0,
        Sub = 1,
        Up = 2,
        Average = 3,
        Paeth = 4
    }

    public static byte[] DecodeIdat(byte[] idatData, uint width, uint height, byte bitDepth, ColorType colorType)
    {
        int bytesPerPixel = ComputeBytesPerPixel(colorType, bitDepth);
        int strideBytes = ComputeStride(width, bitDepth, colorType);

        byte[] filtered = ZlibDecompress(idatData);
        byte[] result = new byte[height * strideBytes];

        Span<byte> prev = new byte[strideBytes];

        int src = 0;
        int dst = 0;

        for (int y = 0; y < height; y++)
        {
            Span<byte> recon = result.AsSpan(dst, strideBytes);

            FilterDelegate filter = GetDecodeFilter((PngFilterType)filtered[src++]);

            for (int x = 0; x < strideBytes; x++)
            {
                byte raw = filtered[src + x];
                byte a = x >= bytesPerPixel ? recon[x - bytesPerPixel] : (byte)0;
                byte b = prev[x];
                byte c = x >= bytesPerPixel ? prev[x - bytesPerPixel] : (byte)0;

                recon[x] = filter(raw, a, b, c);
            }

            prev = recon;

            src += strideBytes;
            dst += strideBytes;
        }

        return ImagePacking.Unpack(result, bitDepth, colorType, width, height);
    }

    private static FilterDelegate GetDecodeFilter(PngFilterType filterType)
    {
        return filterType switch
        {
            PngFilterType.None => (x, _, _, _) => x,

            PngFilterType.Sub => (x, a, _, _) => (byte)(x + a),

            PngFilterType.Up => (x, _, b, _) => (byte)(x + b),

            PngFilterType.Average => (x, a, b, _) => (byte)(x + (byte)((a + b) / 2)),

            PngFilterType.Paeth => (x, a, b, c) => (byte)(x + PaethPredictor(a, b, c)),

            _ => throw new InvalidOperationException($"Unsupported filter type {filterType}")
        };
    }

    public static byte[] EncodeIdat(byte[] raw, uint width, uint height, byte bitDepth, ColorType colorType, PngFilterType filterType = PngFilterType.None)
    {
        raw = ImagePacking.Pack(raw, bitDepth, colorType, width, height);

        int bytesPerPixel = ComputeBytesPerPixel(colorType, bitDepth);
        int strideBytes = ComputeStride(width, bitDepth, colorType);

        byte[] filtered = new byte[height * (strideBytes + 1)];
        Span<byte> prev = new byte[strideBytes];

        int src = 0;
        int dst = 0;

        for (int y = 0; y < height; y++)
        {
            filtered[dst++] = (byte)filterType;

            FilterDelegate filter = GetEncodeFilter(filterType);

            for (int x = 0; x < strideBytes; x++)
            {
                byte orig = raw[src + x];
                byte a = x >= bytesPerPixel ? raw[src + x - bytesPerPixel] : (byte)0;
                byte b = prev[x];
                byte c = x >= bytesPerPixel ? prev[x - bytesPerPixel] : (byte)0;
                filtered[dst + x] = filter(orig, a, b, c);
            }

            prev = raw.AsSpan(src, strideBytes);

            dst += strideBytes;
            src += strideBytes;
        }

        return ZlibCompress(filtered);
    }

    delegate byte FilterDelegate(byte x, byte a, byte b, byte c);
    private static FilterDelegate GetEncodeFilter(PngFilterType filterType)
    {
        return filterType switch
        {
            PngFilterType.None => (x, _, _, _) => x,

            PngFilterType.Sub => (x, a, _, _) => (byte)(x - a),

            PngFilterType.Up => (x, _, b, _) => (byte)(x - b),

            PngFilterType.Average => (x, a, b, _) => (byte)(x - (byte)((a + b) / 2)),

            PngFilterType.Paeth => (x, a, b, c) => (byte)(x - PaethPredictor(a, b, c)),

            _ => throw new InvalidOperationException($"Unsupported filter type {filterType}")
        };
    }

    private static int ComputeBytesPerPixel(ColorType colorType, byte bitDepth)
    {
        if (bitDepth < 8)
            return 1; // packed formats: filters operate on bytes, not pixels

        int bytesPerSample = bitDepth / 8;

        return colorType switch
        {
            ColorType.Greyscale => bytesPerSample,
            ColorType.Truecolor => 3 * bytesPerSample,
            ColorType.IndexedColor => 1,
            ColorType.GreyscaleWithAlpha => 2 * bytesPerSample,
            ColorType.TruecolorWithAlpha => 4 * bytesPerSample,
            _ => throw new ArgumentOutOfRangeException(nameof(colorType))
        };
    }

    private static int ComputeStride(uint width, byte bitDepth, ColorType colorType)
    {
        if (bitDepth < 8)
        {
            int bitsPerPixel = bitDepth;
            int bits = (int)(width * bitsPerPixel);
            return (bits + 7) / 8; // packed
        }

        int bytesPerPixel = ComputeBytesPerPixel(colorType, bitDepth);
        return (int)(width * bytesPerPixel);
    }

    private static byte PaethPredictor(byte a, byte b, byte c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);

        if (pa <= pb && pa <= pc) return a;
        if (pb <= pc) return b;
        return c;
    }

    private static byte[] ZlibCompress(byte[] data)
    {
        using MemoryStream ms = new();

        // zlib header: 0x78 0x9C (DEFLATE, 32K window, default compression)
        ms.WriteByte(0x78);
        ms.WriteByte(0x9C);

        using (DeflateStream def = new(ms, CompressionLevel.Optimal, true))
            def.Write(data, 0, data.Length);

        uint adler = Adler32(data);
        ms.Write(adler.Write());

        return ms.ToArray();
    }

    private static byte[] ZlibDecompress(byte[] zlib)
    {
        using MemoryStream ms = new(zlib[2..^4]);

        zlib.AsSpan(zlib.Length - 4).Read(out uint adler);

        using DeflateStream def = new(ms, CompressionMode.Decompress);
        using MemoryStream outMs = new();
        def.CopyTo(outMs);
        byte[] data = outMs.ToArray();

        if (Adler32(data) != adler)
            throw new Exception("Adler32 mismatch");
        return data;
    }

    private static uint Adler32(byte[] data)
    {
        const uint mod = 65521;
        uint a = 1, b = 0;

        foreach (byte t in data)
        {
            a = (a + t) % mod;
            b = (b + a) % mod;
        }

        return (b << 16) | a;
    }
}