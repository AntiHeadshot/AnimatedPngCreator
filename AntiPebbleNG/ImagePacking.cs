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

    public static byte[] PackScanline(byte[] raw, int row,
        uint width, byte bitDepth,
        ColorType colorType,
        int bytesPerPixel)
    {
        // raw is already "expanded" (1 byte per channel for <8-bit, 1 or 2 bytes per channel for 8/16-bit)
        int rawOffset = row * (int)width * bytesPerPixel;

        // 8/16-bit: direct copy
        if (bitDepth == 8 || bitDepth == 16)
        {
            int stride = (int)width * bytesPerPixel;
            byte[] result = new byte[stride];
            Buffer.BlockCopy(raw, rawOffset, result, 0, stride);
            return result;
        }

        // 1/2/4-bit packing (one sample per pixel, first channel only)
        int packedStride = (int)((width * bitDepth + 7) >> 3);
        byte[] packed = new byte[packedStride];

        int bitOut = 0;

        for (int i = 0; i < width; i++)
        {
            int srcIndex = rawOffset + i * bytesPerPixel;
            if (srcIndex >= raw.Length)
                break; // safety guard

            int value = raw[srcIndex] & ((1 << bitDepth) - 1);

            int dstByte = bitOut >> 3;
            int dstShift = 7 - (bitOut & 7) - (bitDepth - 1);

            packed[dstByte] |= (byte)(value << dstShift);

            bitOut += bitDepth;
        }

        return packed;
    }


    internal static byte[] Unpack(byte[] packed, byte bitDepth, ColorType colorType, uint width, uint height)
    {
        byte bitsPerPixel = GetBitsPerPixel(bitDepth, colorType);

        byte[] pixels;

        if (bitsPerPixel >= 8)
            pixels = packed;
        else
        {
            pixels = new byte[width * height];
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
        }

        return pixels;
    }

    public static byte[] UnpackScanline(byte[] packedScanline, int pixels,
        byte bitDepth, ColorType colorType)
    {
        int channels = colorType switch
        {
            ColorType.Greyscale => 1,
            ColorType.Truecolor => 3,
            ColorType.IndexedColor => 1,
            ColorType.GreyscaleWithAlpha => 2,
            ColorType.TruecolorWithAlpha => 4,
            _ => 1
        };

        int bytesPerPixel = ((bitDepth + 7) / 8) * channels;
        byte[] result = new byte[pixels * bytesPerPixel];

        if (bitDepth == 8 || bitDepth == 16)
        {
            Buffer.BlockCopy(packedScanline, 0, result, 0, result.Length);
            return result;
        }

        int bitIndex = 0;

        for (int i = 0; i < pixels; i++)
        {
            int byteIndex = bitIndex >> 3;
            int bitOffset = 7 - (bitIndex & 7);

            int value;

            switch (bitDepth)
            {
                case 1:
                    value = (packedScanline[byteIndex] >> bitOffset) & 0x01;
                    bitIndex += 1;
                    break;
                case 2:
                    {
                        int shift = bitOffset - 1;
                        value = (packedScanline[byteIndex] >> shift) & 0x03;
                        bitIndex += 2;
                        break;
                    }
                // 4
                default:
                    {
                        int shift = bitOffset - 3;
                        value = (packedScanline[byteIndex] >> shift) & 0x0F;
                        bitIndex += 4;
                        break;
                    }
            }

            for (int c = 0; c < channels; c++)
            {
                result[i * bytesPerPixel + c] = (byte)value;
            }
        }

        return result;
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