using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AntiPebbleNG;

public abstract class Image(in uint width, in uint height, ColorType colorType, byte bitDepth, ColorRgba8[] palette)
{
    public readonly uint Width = width;
    public readonly uint Height = height;

    public readonly ColorType ColorType = colorType;
    public readonly byte BitDepth = bitDepth;
    public readonly ColorRgba8[] Palette = palette;

    public IColor this[int x, int y]
    {
        get => GetPixel(x, y);
        set => SetPixel(x, y, value);
    }

    public abstract IColor GetPixel(int x, int y);
    public abstract void SetPixel(int x, int y, IColor color);
    internal abstract byte[] GetBytes();

    public void FlipVertical()
    {
        for (int y = 0; y < Height; y++)
            for (int left = 0, right = (int)(Width - 1); left < right; left++, right--)
            {
                IColor cLeft = this[left, y];
                IColor cRight = this[right, y];

                this[left, y] = cRight;
                this[right, y] = cLeft;
            }
    }

    public void FlipHorizontal()
    {
        for (int x = 0; x < Width; x++)
            for (int top = 0, bottom = (int)(Height - 1); top < bottom; top++, bottom--)
            {
                IColor cTop = this[x, top];
                IColor cBottom = this[x, bottom];

                this[x, top] = cBottom;
                this[x, bottom] = cTop;
            }
    }

    internal static Image Create(uint width, uint height, byte[] data, byte bitDepth, ColorType colorType, ColorRgba8[] palette = null!)
    {
        if (!AntiPebbleNgData.ColorByConfiguration.TryGetValue(colorType, out Dictionary<byte, Type> byBitDepth)
            || !byBitDepth.TryGetValue(bitDepth, out Type type))
            throw new ArgumentOutOfRangeException(nameof(bitDepth), bitDepth, null);
        Type imageType = typeof(Image<>).MakeGenericType(type);
        return (Image)Activator.CreateInstance(imageType, width, height, data, palette);
    }
}

public class Image<TColor>(uint width, uint height, byte[] data, ColorRgba8[] palette) : Image(width, height, typeof(TColor).GetCustomAttribute<ColorAttribute>().ColorType, typeof(TColor).GetCustomAttribute<ColorAttribute>().BitDepth, palette)
    where TColor : struct, IColor
{
    private readonly TColor[] _data = MemoryMarshal.Cast<byte, TColor>(data).ToArray();

    internal override byte[] GetBytes()
    {
        return MemoryMarshal.Cast<TColor, byte>(_data).ToArray();
    }

    public override IColor GetPixel(int x, int y) => _data[x + y * Width];

    public override void SetPixel(int x, int y, IColor color) => _data[x + y * Width] = (TColor)color.ConvertTo(this);
}
