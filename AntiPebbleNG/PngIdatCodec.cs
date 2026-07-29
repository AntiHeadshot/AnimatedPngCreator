using System;
using System.Collections.Generic;
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

    public static byte[] DecodeIdat(byte[] idatData, uint width, uint height,
                                    byte bitDepth, ColorType colorType,
                                    bool interlaced)
    {
        int bytesPerPixel = ComputeBytesPerPixel(colorType, bitDepth);
        int strideBytes = ComputeStride(width, bitDepth, colorType);

        byte[] decompressed = ZlibDecompress(idatData);

        if (!interlaced)
        {
            return DecodeNonInterlaced(decompressed, width, height,
                                       bitDepth, colorType,
                                       bytesPerPixel, strideBytes);
        }

        return DecodeAdam7Interlaced(decompressed, width, height,
                                     bitDepth, colorType,
                                     bytesPerPixel);
    }

    private static byte[] DecodeNonInterlaced(byte[] filtered,
                                              uint width, uint height,
                                              byte bitDepth, ColorType colorType,
                                              int bytesPerPixel, int strideBytes)
    {
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

    private static byte[] DecodeAdam7Interlaced(byte[] filtered,
                                            uint width, uint height,
                                            byte bitDepth, ColorType colorType,
                                            int bytesPerPixel)
    {
        int[] XStart = { 0, 4, 0, 2, 0, 1, 0 };
        int[] YStart = { 0, 0, 4, 0, 2, 0, 1 };
        int[] XStep = { 8, 8, 4, 4, 2, 2, 1 };
        int[] YStep = { 8, 8, 8, 4, 4, 2, 2 };

        static int PassSize(uint size, int start, int step)
            => start >= size ? 0 : (int)((size - start + step - 1) / step);

        byte[] full = new byte[width * height * bytesPerPixel];

        int src = 0;

        for (int p = 0; p < 7; p++)
        {
            int xs = XStart[p], ys = YStart[p];
            int xd = XStep[p], yd = YStep[p];

            int pw = PassSize(width, xs, xd);
            int ph = PassSize(height, ys, yd);

            if (pw == 0 || ph == 0)
                continue;

            int packedStride = bitDepth >= 8
                ? pw * bytesPerPixel
                : (int)((pw * bitDepth + 7) >> 3);

            Span<byte> prev = new byte[packedStride];

            for (int py = 0; py < ph; py++)
            {
                int dy = ys + py * yd;

                Span<byte> recon = new byte[packedStride];

                FilterDelegate filter = GetDecodeFilter((PngFilterType)filtered[src++]);

                for (int x = 0; x < packedStride; x++)
                {
                    byte raw = filtered[src + x];
                    byte a = x > 0 ? recon[x - 1] : (byte)0;
                    byte b = prev[x];
                    byte c = x > 0 ? prev[x - 1] : (byte)0;

                    recon[x] = filter(raw, a, b, c);
                }

                prev = recon;
                src += packedStride;

                byte[] unpacked = ImagePacking.UnpackScanline(recon.ToArray(), pw, bitDepth, colorType);

                int di = (dy * (int)width) * bytesPerPixel;

                for (int px = 0; px < pw; px++)
                {
                    int dx = xs + px * xd;
                    int dstIndex = di + dx * bytesPerPixel;
                    int srcIndex = px * bytesPerPixel;

                    Buffer.BlockCopy(unpacked, srcIndex,
                                     full, dstIndex,
                                     bytesPerPixel);
                }
            }
        }

        return full;
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

    public static byte[] EncodeIdat(byte[] raw, uint width, uint height,
                                byte bitDepth, ColorType colorType,
                                bool interlaced,
                                PngFilterType filterType = PngFilterType.None)
    {
        if (interlaced)
            return EncodeAdam7Interlaced(raw, width, height, bitDepth, colorType, filterType);

        raw = ImagePacking.Pack(raw, bitDepth, colorType, width, height);
        return EncodeNonInterlaced(raw, width, height, bitDepth, colorType, filterType);
    }

    private static byte[] EncodeNonInterlaced(byte[] raw,
                                              uint width, uint height,
                                              byte bitDepth, ColorType colorType,
                                              PngFilterType filterType)
    {
        int bytesPerPixel = ComputeBytesPerPixel(colorType, bitDepth);
        int strideBytes = ComputeStride(width, bitDepth, colorType);

        byte[] filtered = new byte[height * (strideBytes + 1)];
        Span<byte> prev = new byte[strideBytes];

        int src = 0;
        int dst = 0;

        FilterDelegate filter = GetEncodeFilter(filterType);

        for (int y = 0; y < height; y++)
        {
            filtered[dst++] = (byte)filterType;

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

    private static byte[] EncodeAdam7Interlaced(byte[] raw,
                                            uint width, uint height,
                                            byte bitDepth, ColorType colorType,
                                            PngFilterType filterType)
    {
        int channels = colorType switch
        {
            ColorType.Greyscale => 1,
            ColorType.IndexedColor => 1,
            ColorType.Truecolor => 3,
            ColorType.GreyscaleWithAlpha => 2,
            ColorType.TruecolorWithAlpha => 4,
            _ => 1
        };

        // Raw is expanded: <8-bit = 1 byte per sample, >=8-bit = bitDepth/8 bytes per sample
        int bytesPerSample = bitDepth >= 8 ? bitDepth / 8 : 1;
        int bytesPerPixelRaw = channels * bytesPerSample;

        int bitsPerPixel = channels * bitDepth;
        int filterBpp = Math.Max(1, (bitsPerPixel + 7) / 8);

        int[] XStart = { 0, 4, 0, 2, 0, 1, 0 };
        int[] YStart = { 0, 0, 4, 0, 2, 0, 1 };
        int[] XStep = { 8, 8, 4, 4, 2, 2, 1 };
        int[] YStep = { 8, 8, 8, 4, 4, 2, 2 };

        static int PassSize(uint size, int start, int step)
            => start >= size ? 0 : (int)((size - start + step - 1) / step);

        FilterDelegate filter = GetEncodeFilter(filterType);

        List<byte> output = new();

        for (int p = 0; p < 7; p++)
        {
            int xs = XStart[p], ys = YStart[p];
            int xd = XStep[p], yd = YStep[p];

            int pw = PassSize(width, xs, xd);
            int ph = PassSize(height, ys, yd);

            if (pw == 0 || ph == 0)
                continue;

            int stride = bitDepth >= 8
                ? pw * bytesPerPixelRaw
                : (int)((pw * bitsPerPixel + 7) >> 3);

            byte[] prev = new byte[stride];

            for (int py = 0; py < ph; py++)
            {
                int dy = ys + py * yd;

                byte[] scanline = new byte[stride];

                if (bitDepth >= 8)
                {
                    int dst = 0;
                    for (int px = 0; px < pw; px++)
                    {
                        int dx = xs + px * xd;
                        int srcIndex = (dy * (int)width + dx) * bytesPerPixelRaw;

                        Buffer.BlockCopy(raw, srcIndex, scanline, dst, bytesPerPixelRaw);
                        dst += bytesPerPixelRaw;
                    }
                }
                else
                {
                    int bitOut = 0;
                    int mask = (1 << bitDepth) - 1;

                    for (int px = 0; px < pw; px++)
                    {
                        int dx = xs + px * xd;
                        int srcIndex = (dy * (int)width + dx) * bytesPerPixelRaw;

                        int value = raw[srcIndex] & mask;

                        int dstByte = bitOut >> 3;
                        int dstShift = 7 - (bitOut & 7) - (bitDepth - 1);

                        scanline[dstByte] |= (byte)(value << dstShift);

                        bitOut += bitDepth;
                    }
                }

                output.Add((byte)filterType);

                for (int x = 0; x < stride; x++)
                {
                    byte orig = scanline[x];
                    byte a = x >= filterBpp ? scanline[x - filterBpp] : (byte)0;
                    byte b = prev[x];
                    byte c = x >= filterBpp ? prev[x - filterBpp] : (byte)0;

                    output.Add(filter(orig, a, b, c));
                }

                prev = scanline;
            }
        }

        return ZlibCompress(output.ToArray());
    }

    private static byte[] ExtractAdam7Scanline(byte[] packed,
        int pw,
        int xs,
        int xd,
        byte bitDepth,
        int bytesPerPixel)
    {
        // 8-bit and 16-bit: direct byte slicing
        if (bitDepth >= 8)
        {
            int stride = pw * bytesPerPixel;
            byte[] result2 = new byte[stride];

            int dst = 0;
            for (int i = 0; i < pw; i++)
            {
                int srcPixel = xs + i * xd;
                int srcIndex = srcPixel * bytesPerPixel;

                Buffer.BlockCopy(packed, srcIndex, result2, dst, bytesPerPixel);
                dst += bytesPerPixel;
            }

            return result2;
        }

        int packedStride = (pw * bitDepth + 7) >> 3;
        byte[] result = new byte[packedStride];

        int bitOut = 0;

        for (int i = 0; i < pw; i++)
        {
            int srcPixel = xs + i * xd;

            int srcBit = srcPixel * bitDepth;
            int srcByte = srcBit >> 3;
            int srcShift = 7 - (srcBit & 7);

            int value;

            if (bitDepth == 1)
            {
                value = (packed[srcByte] >> srcShift) & 1;
            }
            else if (bitDepth == 2)
            {
                int shift = srcShift - 1;
                value = (packed[srcByte] >> shift) & 3;
            }
            else if (bitDepth == 4)
            {
                int shift = srcShift - 3;
                value = (packed[srcByte] >> shift) & 0xF;
            }
            else
            {
                Buffer.BlockCopy(packed, srcPixel * bytesPerPixel,
                                 result, i * bytesPerPixel,
                                 bytesPerPixel);
                continue;
            }

            int dstByte = bitOut >> 3;
            int dstShift = 7 - (bitOut & 7) - (bitDepth - 1);

            result[dstByte] |= (byte)(value << dstShift);

            bitOut += bitDepth;
        }

        return result;
    }

    private static int ComputeTotalAdam7FilteredSize(uint width, uint height, byte bitDepth)
    {
        int[] XStart = { 0, 4, 0, 2, 0, 1, 0 };
        int[] YStart = { 0, 0, 4, 0, 2, 0, 1 };
        int[] XStep = { 8, 8, 4, 4, 2, 2, 1 };
        int[] YStep = { 8, 8, 8, 4, 4, 2, 2 };

        static int PassSize(uint size, int start, int step)
            => start >= size ? 0 : (int)((size - start + step - 1) / step);

        static int PackedStride(int pixels, int bits)
            => (pixels * bits + 7) >> 3;

        int total = 0;

        for (int p = 0; p < 7; p++)
        {
            int pw = PassSize(width, XStart[p], XStep[p]);
            int ph = PassSize(height, YStart[p], YStep[p]);

            if (pw == 0 || ph == 0)
                continue;

            total += ph * (PackedStride(pw, bitDepth) + 1);
        }

        return total;
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