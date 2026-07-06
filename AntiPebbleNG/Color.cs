using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AntiPebbleNG;

public interface IColor;

[DebuggerDisplay("{Value,nq}")]
[Color(ColorType.Greyscale, 1)]
public readonly struct ColorGray1(byte value) : IColor
{
    public readonly byte Value = value;
}

[DebuggerDisplay("{Value,nq}")]
[Color(ColorType.Greyscale, 2)]
public readonly struct ColorGray2(byte value) : IColor
{
    public readonly byte Value = value;
}

[DebuggerDisplay("{Value,nq}")]
[Color(ColorType.Greyscale, 4)]
public readonly struct ColorGray4(byte value) : IColor
{
    public readonly byte Value = value;
}

[DebuggerDisplay("{Value,nq}")]
[Color(ColorType.Greyscale, 8)]
public readonly struct ColorGray8(byte value) : IColor
{
    public readonly byte Value = value;
}

[DebuggerDisplay("{Value,nq}")]
[Color(ColorType.Greyscale, 16)]
public readonly struct ColorGray16(ushort value) : IColor
{
    public readonly ushort Value = value;
}

[Color(ColorType.Truecolor, 8)]
public readonly struct ColorRgb8(byte r, byte g, byte b) : IColor
{
    public readonly byte R = r;
    public readonly byte G = g;
    public readonly byte B = b;
}

[Color(ColorType.Truecolor, 16)]
public readonly struct ColorRgb16(ushort r, ushort g, ushort b) : IColor
{
    public readonly ushort R = r;
    public readonly ushort G = g;
    public readonly ushort B = b;
}

[Color(ColorType.IndexedColor, 1)]
public readonly struct ColorIndexed1(byte value) : IColor
{
    public readonly byte Value = value;
}

[Color(ColorType.IndexedColor, 2)]
public readonly struct ColorIndexed2(byte value) : IColor
{
    public readonly byte Value = value;
}

[Color(ColorType.IndexedColor, 4)]
public readonly struct ColorIndexed4(byte value) : IColor
{
    public readonly byte Value = value;
}

[Color(ColorType.IndexedColor, 8)]
public readonly struct ColorIndexed8(byte value) : IColor
{
    public readonly byte Value = value;
}

[Color(ColorType.GreyscaleWithAlpha, 8)]
public readonly struct ColorGrayAlpha8(byte value, byte alpha) : IColor
{
    public readonly byte Value = value;
    public readonly byte Alpha = alpha;
}

[Color(ColorType.GreyscaleWithAlpha, 16)]
public readonly struct ColorGrayAlpha16(ushort value, ushort alpha) : IColor
{
    public readonly ushort Value = value;
    public readonly ushort Alpha = alpha;
}

[Color(ColorType.TruecolorWithAlpha, 8)]
public readonly struct ColorRgba8(byte r, byte g, byte b, byte a) : IColor
{
    public readonly byte R = r;
    public readonly byte G = g;
    public readonly byte B = b;
    public readonly byte A = a;
}

[Color(ColorType.TruecolorWithAlpha, 16)]
public readonly struct ColorRgba16(ushort r, ushort g, ushort b, ushort a) : IColor
{
    public readonly ushort R = r;
    public readonly ushort G = g;
    public readonly ushort B = b;
    public readonly ushort A = a;
}

public static class ColorExtension
{
    public static IColor ConvertTo(this IColor color, Image image) => color.ConvertTo(image.ColorType, image.BitDepth, image.Palette);

    public static IColor ConvertTo(this IColor color, ColorType colorType, byte bitDepth, ColorRgba8[] palette)
    {
        if (color is ColorRgba16 rgba)
            return colorType switch
            {
                ColorType.Greyscale =>
                    bitDepth switch
                    {
                        1 => new ColorGray1((byte)((rgba.R + rgba.G + rgba.B) / (3 * 0x8000))),
                        2 => new ColorGray2((byte)((rgba.R + rgba.G + rgba.B) / (3 * 0x4000))),
                        4 => new ColorGray4((byte)((rgba.R + rgba.G + rgba.B) / (3 * 0x1000))),
                        8 => new ColorGray8((byte)((rgba.R + rgba.G + rgba.B) / (3 * 256))),
                        16 => new ColorGray16((ushort)((rgba.R + rgba.G + rgba.B) / 3)),
                        _ => throw new InvalidOperationException("This is not a valid ColorType, bitDepth combination.")
                    },
                ColorType.Truecolor =>
                    bitDepth switch
                    {
                        8 => new ColorRgb8((byte)(rgba.R / 256), (byte)(rgba.G / 256), (byte)(rgba.B / 256)),
                        16 => new ColorRgb16(rgba.R, rgba.G, rgba.B),
                        _ => throw new InvalidOperationException("This is not a valid ColorType, bitDepth combination.")
                    },
                ColorType.IndexedColor =>
                    bitDepth switch
                    {
                        1 => new ColorIndexed1(palette.GetClosestIndex(rgba)),
                        2 => new ColorIndexed2(palette.GetClosestIndex(rgba)),
                        4 => new ColorIndexed4(palette.GetClosestIndex(rgba)),
                        8 => new ColorIndexed8(palette.GetClosestIndex(rgba)),
                        _ => throw new InvalidOperationException("This is not a valid ColorType, bitDepth combination.")
                    },
                ColorType.GreyscaleWithAlpha =>
                    bitDepth switch
                    {
                        8 => new ColorGrayAlpha8((byte)((rgba.R + rgba.G + rgba.B) / (3 * 256)), (byte)(rgba.A / 256)),
                        16 => new ColorGrayAlpha16((ushort)((rgba.R + rgba.G + rgba.B) / 3), rgba.A),
                        _ => throw new InvalidOperationException("This is not a valid ColorType, bitDepth combination.")
                    },
                ColorType.TruecolorWithAlpha =>
                    bitDepth switch
                    {
                        8 => new ColorRgba8((byte)(rgba.R / 256), (byte)(rgba.G / 256), (byte)(rgba.B / 256), (byte)(rgba.A / 256)),
                        16 => rgba,
                        _ => throw new InvalidOperationException("This is not a valid ColorType, bitDepth combination.")
                    },
                _ => throw new InvalidOperationException("This is not a valid ColorType")
            };

        //Convert all to RGBA because it is most expressive.
        return (color switch
        {
            ColorGray1 c => new ColorRgba16((ushort)(c.Value * 0x8000), (ushort)(c.Value * 0x8000), (ushort)(c.Value * 0x8000), ushort.MaxValue),
            ColorGray2 c => new ColorRgba16((ushort)(c.Value * 0x4000), (ushort)(c.Value * 0x4000), (ushort)(c.Value * 0x4000), ushort.MaxValue),
            ColorGray4 c => new ColorRgba16((ushort)(c.Value * 0x1000), (ushort)(c.Value * 0x1000), (ushort)(c.Value * 0x1000), ushort.MaxValue),
            ColorGray8 c => new ColorRgba16((ushort)(c.Value * 256), (ushort)(c.Value * 256), (ushort)(c.Value * 256), ushort.MaxValue),
            ColorGray16 c => new ColorRgba16(c.Value, c.Value, c.Value, ushort.MaxValue),
            ColorRgb8 c => new ColorRgba16((ushort)(c.R * 256), (ushort)(c.G * 256), (ushort)(c.B * 256), ushort.MaxValue),
            ColorRgb16 c => new ColorRgba16(c.R, c.G, c.B, ushort.MaxValue),
            ColorIndexed1 c => palette[c.Value].ConvertTo(ColorType.TruecolorWithAlpha, 16, palette),
            ColorIndexed2 c => palette[c.Value].ConvertTo(ColorType.TruecolorWithAlpha, 16, palette),
            ColorIndexed4 c => palette[c.Value].ConvertTo(ColorType.TruecolorWithAlpha, 16, palette),
            ColorIndexed8 c => palette[c.Value].ConvertTo(ColorType.TruecolorWithAlpha, 16, palette),
            ColorGrayAlpha8 c => new ColorRgba16((ushort)(c.Value * 256), (ushort)(c.Value * 256), (ushort)(c.Value * 256), (ushort)(c.Alpha * 256)),
            ColorGrayAlpha16 c => new ColorRgba16(c.Value, c.Value, c.Value, c.Alpha),
            ColorRgba8 c => new ColorRgba16((ushort)(c.R * 256), (ushort)(c.G * 256), (ushort)(c.B * 256), (ushort)(c.A * 256)),
            _ => throw new InvalidOperationException("Conversion not supported.")
        }).ConvertTo(colorType, bitDepth, palette);
    }

    private static byte GetClosestIndex(this ColorRgba8[] palette, ColorRgba16 color)
    {
        // Convert 16-bit (0–65535) to 8-bit (0–255)
        static byte ToByte(ushort v) => (byte)((v * 255 + 32767) / 65535);

        byte r = ToByte(color.R);
        byte g = ToByte(color.G);
        byte b = ToByte(color.B);
        byte a = ToByte(color.A);

        byte bestIndex = 0;
        int bestScore = int.MaxValue;

        // Special case: fully transparent target → prioritize transparent palette entries
        bool targetTransparent = (a == 0);

        for (byte i = 0; i < palette.Length; i++)
        {
            var p = palette[i];

            int score;

            if (targetTransparent)
            {
                // Strongly prefer transparent colors
                if (p.A == 0)
                {
                    // Only compare RGB lightly (transparent colors often share RGB)
                    int dr = p.R - r;
                    int dg = p.G - g;
                    int db = p.B - b;
                    score = dr * dr + dg * dg + db * db;
                }
                // Penalize non-transparent colors heavily
                else
                    score = 1_000_000 + p.A * p.A;
            }
            else
            {
                // Normal weighted RGBA distance
                int dr = p.R - r;
                int dg = p.G - g;
                int db = p.B - b;
                int da = p.A - a;

                // Alpha is more important than RGB
                score = dr * dr + dg * dg + db * db + (da * da * 4);
            }

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }
}

public static class Colors
{
    public static readonly ColorRgba8 Black = new(0, 0, 0, 255);
    public static readonly ColorRgba8 White = new(255, 255, 255, 255);
    public static readonly ColorRgba8 Red = new(255, 0, 0, 255);
    public static readonly ColorRgba8 Green = new(0, 255, 0, 255);
    public static readonly ColorRgba8 Blue = new(0, 0, 255, 255);
}