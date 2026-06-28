using System;

namespace AntiPebbleNG;

public static class ImagePacking
{
    internal static byte[] Pack(byte[] pixels, byte bitDepth, ColorType colorType, uint width, uint height)
    {
        byte bitsPerPixel = GetBitsPerPixel(bitDepth, colorType);
        if (bitsPerPixel >= 8)
            return pixels;

        byte[] packed = new byte[((width * bitsPerPixel + 7) / 8) * height];
        int mask = ~(~1 << (bitsPerPixel - 1));
        int p = 0;
        int stride = (int)(width - 1);
        int i = 0;
        for (; i < packed.Length; i++)
        {
            byte b = 0;
            for (int bit = 8 - bitsPerPixel; bit >= 0 && p < pixels.Length && stride >= 0; bit -= bitsPerPixel, stride--)
                b |= (byte)((pixels[p++] & mask) << bit);
            packed[i] = b;

            if (stride < 0)
                stride = (int)(width - 1);
        }
        return packed;
    }

    internal static byte[] Unpack(byte[] packed, byte bitDepth, ColorType colorType, uint width, uint height)
    {
        byte bitsPerPixel = GetBitsPerPixel(bitDepth, colorType);
        if (bitsPerPixel >= 8)
            return packed;

        byte[] pixels = new byte[width * height];
        int p = 0;
        int mask = ~(~1 << (bitsPerPixel - 1));
        int stride = (int)(width - 1);
        foreach (byte b in packed)
        {
            for (int bit = 8 - bitsPerPixel; bit >= 0 && stride >= 0; bit -= bitsPerPixel, stride--)
            {
                pixels[p++] = (byte)((b >> bit) & mask);
            }

            if (stride < 0)
                stride = (int)(width - 1);
        }
        return pixels;
    }

    private static byte GetBitsPerPixel(byte bitDepth, ColorType colorType)
    {
        return (byte)(colorType switch
        {
            ColorType.Greyscale => bitDepth,
            ColorType.Truecolor => 3 * bitDepth,
            ColorType.IndexedColor => bitDepth,
            ColorType.GreyscaleWithAlpha => 2 * bitDepth,
            ColorType.TruecolorWithAlpha => 4 * bitDepth,
            _ => throw new ArgumentOutOfRangeException(nameof(colorType))
        });
    }
}