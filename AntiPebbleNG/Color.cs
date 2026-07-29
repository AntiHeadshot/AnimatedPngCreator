using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace AntiPebbleNG;

public interface IColor
{
    IColor ApplyGamma(Func<ushort, ushort> gammaFunc);
}

[DebuggerDisplay("Gray1 {Value,nq}")]
[Color(ColorType.Greyscale, 1)]
public readonly struct ColorGray1(byte value) : IColor
{
    public readonly byte Value = value;

    public IColor ApplyGamma(Func<ushort, ushort> gammaFunc) => new ColorGray1((byte)gammaFunc(Value));
}

[DebuggerDisplay("Gray2 {Value,nq}")]
[Color(ColorType.Greyscale, 2)]
public readonly struct ColorGray2(byte value) : IColor
{
    public readonly byte Value = value;

    public IColor ApplyGamma(Func<ushort, ushort> gammaFunc) => new ColorGray2((byte)gammaFunc(Value));
}

[DebuggerDisplay("Gray4 {Value,nq}")]
[Color(ColorType.Greyscale, 4)]
public readonly struct ColorGray4(byte value) : IColor
{
    public readonly byte Value = value;

    public IColor ApplyGamma(Func<ushort, ushort> gammaFunc) => new ColorGray4((byte)gammaFunc(Value));
}

[DebuggerDisplay("Gray8 {Value,nq}")]
[Color(ColorType.Greyscale, 8)]
public readonly struct ColorGray8(byte value) : IColor
{
    public readonly byte Value = value;

    public IColor ApplyGamma(Func<ushort, ushort> gammaFunc) => new ColorGray8((byte)gammaFunc(Value));
}

[DebuggerDisplay("Gray16 {Value,nq}")]
[Color(ColorType.Greyscale, 16)]
public readonly struct ColorGray16(ushort value) : IColor
{
    public readonly ushort Value = value;

    public IColor ApplyGamma(Func<ushort, ushort> gammaFunc) => new ColorGray16(gammaFunc(Value));
}

[DebuggerDisplay("Index {Value,nq}")]
[Color(ColorType.IndexedColor, 1)]
[Color(ColorType.IndexedColor, 2)]
[Color(ColorType.IndexedColor, 4)]
[Color(ColorType.IndexedColor, 8)]
public readonly struct ColorIndexByte(byte value) : IColor
{
    public readonly byte Value = value;

    public IColor ApplyGamma(Func<ushort, ushort> gammaFunc) => throw new NotSupportedException();
}

[DebuggerDisplay("Index {Value,nq}")]
[Color(ColorType.IndexedColor, 16)]
public readonly struct ColorIndexUshort(ushort value) : IColor
{
    public readonly ushort Value = value;

    public IColor ApplyGamma(Func<ushort, ushort> gammaFunc) => throw new NotSupportedException();
}

[Color(ColorType.Truecolor, 8)]
public readonly struct ColorRgb8(byte r, byte g, byte b) : IColor
{
    public readonly byte R = r;
    public readonly byte G = g;
    public readonly byte B = b;

    public IColor ApplyGamma(Func<ushort, ushort> gammaFunc) => new ColorRgb8((byte)gammaFunc(R), (byte)gammaFunc(G), (byte)gammaFunc(B));
}

[Color(ColorType.Truecolor, 16)]
public readonly struct ColorRgb16(ushort r, ushort g, ushort b) : IColor
{
    public readonly ushort R = r;
    public readonly ushort G = g;
    public readonly ushort B = b;

    public IColor ApplyGamma(Func<ushort, ushort> gammaFunc) => new ColorRgb16(gammaFunc(R), gammaFunc(G), gammaFunc(B));
}

[Color(ColorType.GreyscaleWithAlpha, 8)]
public readonly struct ColorGrayAlpha8(byte value, byte alpha) : IColor
{
    public readonly byte Value = value;
    public readonly byte Alpha = alpha;

    public IColor ApplyGamma(Func<ushort, ushort> gammaFunc) => new ColorGrayAlpha8((byte)gammaFunc(Value), Alpha);
}

[Color(ColorType.GreyscaleWithAlpha, 16)]
public readonly struct ColorGrayAlpha16(ushort value, ushort alpha) : IColor
{
    public readonly ushort Value = value;
    public readonly ushort Alpha = alpha;

    public IColor ApplyGamma(Func<ushort, ushort> gammaFunc) => new ColorGrayAlpha16(gammaFunc(Value), Alpha);
}

[Color(ColorType.TruecolorWithAlpha, 8)]
public readonly record struct ColorRgba8(byte R, byte G, byte B, byte A) : IColor
{
    public readonly byte R = R;
    public readonly byte G = G;
    public readonly byte B = B;
    public readonly byte A = A;

    public int ToArgb() => (A << 24) | (R << 16) | (G << 8) | B;

    public IColor ApplyGamma(Func<ushort, ushort> gammaFunc) => new ColorRgba8((byte)gammaFunc(R), (byte)gammaFunc(G), (byte)gammaFunc(B), A);
}

[Color(ColorType.TruecolorWithAlpha, 16)]
public readonly struct ColorRgba16(ushort r, ushort g, ushort b, ushort a) : IColor
{
    public readonly ushort R = r;
    public readonly ushort G = g;
    public readonly ushort B = b;
    public readonly ushort A = a;

    public IColor ApplyGamma(Func<ushort, ushort> gammaFunc) => new ColorRgba16(gammaFunc(R), gammaFunc(G), gammaFunc(B), A);
}

public static class ColorExtension
{
    public static IColor ConvertTo(this IColor color, Image image) => color.ConvertTo(image.ColorType, image.BitDepth, image.Palette);

    public static IColor ConvertTo(this IColor color, ColorType colorType, byte bitDepth, ColorRgba8[] toPalette)//Todo React to from Pallet
    {
        if (color is ColorRgba16 rgba)
            return colorType switch
            {
                ColorType.IndexedColor =>
                    bitDepth switch
                    {
                        1 => toPalette.GetClosestColor(rgba),
                        2 => toPalette.GetClosestColor(rgba),
                        4 => toPalette.GetClosestColor(rgba),
                        8 => toPalette.GetClosestColor(rgba),
                        16 => toPalette.GetClosestColor(rgba),
                        _ => throw new InvalidOperationException("This is not a valid ColorType, bitDepth combination.")
                    },
                ColorType.Greyscale =>
                    bitDepth switch
                    {
                        1 => new ColorGray1((byte)((rgba.R + rgba.G + rgba.B) / (3 * 0xFFFF))),
                        2 => new ColorGray2((byte)((rgba.R + rgba.G + rgba.B) / (3 * 0x5555))),
                        4 => new ColorGray4((byte)((rgba.R + rgba.G + rgba.B) / (3 * 0x1111))),
                        8 => new ColorGray8((byte)((rgba.R + rgba.G + rgba.B) / (3 * 0x0101))),
                        16 => new ColorGray16((ushort)((rgba.R + rgba.G + rgba.B) / 3)),
                        _ => throw new InvalidOperationException("This is not a valid ColorType, bitDepth combination.")
                    },
                ColorType.Truecolor =>
                    bitDepth switch
                    {
                        8 => new ColorRgb8((byte)(rgba.R / 0x0101), (byte)(rgba.G / 0x0101), (byte)(rgba.B / 0x0101)),
                        16 => new ColorRgb16(rgba.R, rgba.G, rgba.B),
                        _ => throw new InvalidOperationException("This is not a valid ColorType, bitDepth combination.")
                    },
                ColorType.GreyscaleWithAlpha =>
                    bitDepth switch
                    {
                        8 => new ColorGrayAlpha8((byte)((rgba.R + rgba.G + rgba.B) / (3 * 0x0101)), (byte)(rgba.A / 0x0101)),
                        16 => new ColorGrayAlpha16((ushort)((rgba.R + rgba.G + rgba.B) / 3), rgba.A),
                        _ => throw new InvalidOperationException("This is not a valid ColorType, bitDepth combination.")
                    },
                ColorType.TruecolorWithAlpha =>
                    bitDepth switch
                    {
                        8 => new ColorRgba8((byte)(rgba.R / 0x0101), (byte)(rgba.G / 0x0101), (byte)(rgba.B / 0x0101), (byte)(rgba.A / 0x0101)),
                        16 => rgba,
                        _ => throw new InvalidOperationException("This is not a valid ColorType, bitDepth combination.")
                    },
                _ => throw new InvalidOperationException("This is not a valid ColorType")
            };

        //Convert all to RGBA because it is most expressive.
        return color switch
        {
            ColorGray1 c => colorType == ColorType.Greyscale && bitDepth == 1 ? c :
                new ColorRgba16((ushort)(c.Value * 0xFFFF), (ushort)(c.Value * 0xFFFF), (ushort)(c.Value * 0xFFFF), ushort.MaxValue).ConvertTo(colorType, bitDepth, toPalette),
            ColorGray2 c => colorType == ColorType.Greyscale && bitDepth == 2 ? c :
                new ColorRgba16((ushort)(c.Value * 0x5555), (ushort)(c.Value * 0x5555), (ushort)(c.Value * 0x5555), ushort.MaxValue).ConvertTo(colorType, bitDepth, toPalette),
            ColorGray4 c => colorType == ColorType.Greyscale && bitDepth == 4 ? c :
                new ColorRgba16((ushort)(c.Value * 0x1111), (ushort)(c.Value * 0x1111), (ushort)(c.Value * 0x1111), ushort.MaxValue).ConvertTo(colorType, bitDepth, toPalette),
            ColorGray8 c => colorType == ColorType.Greyscale && bitDepth == 8 ? c :
                new ColorRgba16((ushort)(c.Value * 0x0101), (ushort)(c.Value * 0x0101), (ushort)(c.Value * 0x0101), ushort.MaxValue).ConvertTo(colorType, bitDepth, toPalette),
            ColorGray16 c => colorType == ColorType.Greyscale && bitDepth == 16 ? c :
                new ColorRgba16(c.Value, c.Value, c.Value, ushort.MaxValue).ConvertTo(colorType, bitDepth, toPalette),
            ColorRgb8 c => colorType == ColorType.Truecolor && bitDepth == 8 ? c :
                new ColorRgba16((ushort)(c.R * 0x0101), (ushort)(c.G * 0x0101), (ushort)(c.B * 0x0101), ushort.MaxValue).ConvertTo(colorType, bitDepth, toPalette),
            ColorRgb16 c => colorType == ColorType.Truecolor && bitDepth == 16 ? c :
                new ColorRgba16(c.R, c.G, c.B, ushort.MaxValue).ConvertTo(colorType, bitDepth, toPalette),
            ColorGrayAlpha8 c => colorType == ColorType.GreyscaleWithAlpha && bitDepth == 8 ? c :
                new ColorRgba16((ushort)(c.Value * 0x0101), (ushort)(c.Value * 0x0101), (ushort)(c.Value * 0x0101), (ushort)(c.Alpha * 0x0101)).ConvertTo(colorType, bitDepth, toPalette),
            ColorGrayAlpha16 c => colorType == ColorType.GreyscaleWithAlpha && bitDepth == 16 ? c :
                new ColorRgba16(c.Value, c.Value, c.Value, c.Alpha).ConvertTo(colorType, bitDepth, toPalette),
            ColorRgba8 c => colorType == ColorType.TruecolorWithAlpha && bitDepth == 8 ? c :
                new ColorRgba16((ushort)(c.R * 0x0101), (ushort)(c.G * 0x0101), (ushort)(c.B * 0x0101), (ushort)(c.A * 0x0101)).ConvertTo(colorType, bitDepth, toPalette),
            ColorIndexByte c => colorType == ColorType.IndexedColor && bitDepth < 16 ? c : throw new InvalidOperationException("Conversion not supported."),
            ColorIndexUshort c => colorType == ColorType.IndexedColor && bitDepth == 16 ? c : throw new InvalidOperationException("Conversion not supported."),
            _ => throw new InvalidOperationException("Conversion not supported.")
        };
    }

    public static ColorRgba8 GetClosestColor(this ColorRgba8[] palette, ColorRgba16 color) => palette[palette.GetClosestIndex(color)];
    private static byte GetClosestIndex(this ColorRgba8[] palette, ColorRgba16 color)
    {
        // Convert 16-bit (0–65535) to 8-bit (0–255)
        static byte ToByte(ushort v) => (byte)((v * 255 + 32767) / 65535);

        byte r = ToByte(color.R);
        byte g = ToByte(color.G);
        byte b = ToByte(color.B);
        byte a = ToByte(color.A);

        return palette.GetClosestIndex(new ColorRgba8(r, g, b, a));
    }

    public static ColorRgba8 GetClosestColor(this ColorRgba8[] palette, ColorRgba8 color) => palette[palette.GetClosestIndex(color)];
    private static byte GetClosestIndex(this ColorRgba8[] palette, ColorRgba8 color)
    {
        byte r = color.R;
        byte g = color.G;
        byte b = color.B;
        byte a = color.A;

        int bestIndex = 0;
        int bestScore = int.MaxValue;

        // Special case: fully transparent target → prioritize transparent palette entries
        bool targetTransparent = (a == 0);

        for (int i = 0; i < palette.Length; i++)
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

        return (byte)bestIndex;
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