using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace AntiPebbleNG;

internal static class AntiPebbleNGData
{
    internal static readonly Dictionary<string, Type> ChunkTypesByName = typeof(AbstractChunk).Assembly.GetTypes()
        .Where(x => x != typeof(AbstractChunk) && typeof(AbstractChunk).IsAssignableFrom(x))
        .ToDictionary(x => x.GetCustomAttribute<ChunkAttribute>().Name, x => x);
}

public class Png
{
    public bool CheckCrc = false;

    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private List<AbstractChunk> _chunks = [];

    public float DefaultDelayInSeconds
    {
        get => (float)_defaultNum / _defaultDen;
        set
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("Value must be a finite number.");

            if (value <= 0)
            {
                _defaultNum = 0;
                _defaultDen = 1;
                return;
            }

            // Continued fraction expansion
            const int max = ushort.MaxValue;

            int a0 = (int)Math.Floor(value);
            if (a0 > max)
            {
                _defaultNum = max;
                _defaultDen = 1;
                return;
            }

            int n0 = 1, d0 = 0; // previous convergent
            int n1 = a0, d1 = 1; // current convergent

            double frac = value - a0;

            while (frac > 0)
            {
                frac = 1.0 / frac;
                int a = (int)Math.Floor(frac);

                int n2 = a * n1 + n0;
                int d2 = a * d1 + d0;

                if (n2 > max || d2 > max)
                    break;

                n0 = n1; d0 = d1;
                n1 = n2; d1 = d2;

                frac -= a;
            }

            _defaultNum = (ushort)n1;
            _defaultDen = (ushort)d1;
        }
    }

    public UInt32 Width { get; private set; }
    public UInt32 Height { get; private set; }

    private UInt16 _defaultNum = 1;
    private UInt16 _defaultDen = 100;

    public bool StripDecoration = false;

    public Png(string filename)
    {
        using FileStream file = File.OpenRead(filename);
        Load(file);
        Init();
    }

    public Png(Stream stream)
    {
        Load(stream);
        Init();
    }

    public Png(byte[] data)
    {
        Load(data);
        Init();
    }

    public Png(UInt32 width, UInt32 height, ColorType colorType = ColorType.TruecolorWithAlpha, byte bitDepth = 8, IColor? color = null)
    {
        color ??= Colors.Black;

        _chunks.Add(new IhdrChunk
        {
            BitDepth = bitDepth,
            ColorType = colorType,
            Height = height,
            Width = width,
            CompressionMethod = 0,
        });

        IColor iColor = color.ConvertTo(colorType, bitDepth);

        Image image = GetImage((int)width, (int)height, new byte[Marshal.SizeOf(iColor) * width * height], bitDepth, colorType);
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
                image[x, y] = iColor;

        byte[] byteData = image.Free();
        _chunks.Add(new IdatChunk
        {
            Data = PngIdatCodec.EncodeIdat(byteData, width, height, bitDepth, colorType)
        });
        _chunks.Add(new IendChunk());

        Init();
    }

    private void Init()
    {
        IhdrChunk ihdr = _chunks.First<IhdrChunk>();
        Width = ihdr.Width;
        Height = ihdr.Height;
    }

    private Image? _unlockedImage;

    public IDisposable Unlock(out Image image)
    {
        if (_unlockedImage != null)
            throw new InvalidOperationException("Already unlocked");
        IhdrChunk ihdr = _chunks.First<IhdrChunk>();
        IdatChunk idat = _chunks.First<IdatChunk>();
        byte[] pixelData = Unpack(PngIdatCodec.DecodeIdat(idat.Data, ihdr.Width, ihdr.Height, ihdr.BitDepth, ihdr.ColorType), GetBitsPerPixel(ihdr.BitDepth, ihdr.ColorType), (int)ihdr.Width, (int)ihdr.Height);

        image = _unlockedImage = GetImage((int)ihdr.Width, (int)ihdr.Height, pixelData, ihdr.BitDepth, ihdr.ColorType);
        return new DisposableAction(Lock);
    }

    private static Image GetImage(int width, int height, byte[] data, byte bitDepth, ColorType colorType)
    {
        return colorType switch
        {
            ColorType.Greyscale =>
                bitDepth switch
                {
                    1 => new Image<ColorGray1>(width, height, data),
                    2 => new Image<ColorGray2>(width, height, data),
                    4 => new Image<ColorGray4>(width, height, data),
                    8 => new Image<ColorGray8>(width, height, data),
                    16 => new Image<ColorGray16>(width, height, data),
                    _ => throw new ArgumentOutOfRangeException(nameof(bitDepth), bitDepth, null)
                },
            ColorType.Truecolor =>
                bitDepth switch
                {
                    8 => new Image<ColorRgb8>(width, height, data),
                    16 => new Image<ColorRgb16>(width, height, data),
                    _ => throw new ArgumentOutOfRangeException(nameof(bitDepth), bitDepth, null)
                },
            ColorType.IndexedColor =>
                bitDepth switch
                {
                    1 => new Image<ColorIndexed1>(width, height, data),
                    2 => new Image<ColorIndexed2>(width, height, data),
                    4 => new Image<ColorIndexed4>(width, height, data),
                    8 => new Image<ColorIndexed8>(width, height, data),
                    _ => throw new ArgumentOutOfRangeException(nameof(bitDepth), bitDepth, null)
                },
            ColorType.GreyscaleWithAlpha =>
                bitDepth switch
                {
                    8 => new Image<ColorGrayAlpha8>(width, height, data),
                    16 => new Image<ColorGrayAlpha16>(width, height, data),
                    _ => throw new ArgumentOutOfRangeException(nameof(bitDepth), bitDepth, null)
                },
            ColorType.TruecolorWithAlpha =>
                bitDepth switch
                {
                    8 => new Image<ColorRgba8>(width, height, data),
                    16 => new Image<ColorRgba16>(width, height, data),
                    _ => throw new ArgumentOutOfRangeException(nameof(bitDepth), bitDepth, null)
                },
            _ => throw new ArgumentOutOfRangeException(nameof(colorType), colorType, null)
        };
    }

    private static byte[] Unpack(byte[] packed, byte bitsPerPixel, int width, int height)
    {
        if (bitsPerPixel >= 8)
            return packed;

        byte[] pixels = new byte[width * height];
        int p = 0;
        int mask = ~(~1 << (bitsPerPixel - 1));
        int stride = width - 1;
        foreach (byte b in packed)
        {
            for (int bit = 8 - bitsPerPixel; bit >= 0 && stride >= 0; bit -= bitsPerPixel, stride--)
            {
                pixels[p++] = (byte)((b >> bit) & mask);
            }

            if (stride <= 0)
                stride = width - 1;
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

    public void Lock()
    {
        if (_unlockedImage == null)
            return;

        IhdrChunk ihdr = _chunks.First<IhdrChunk>();
        IdatChunk idat = _chunks.First<IdatChunk>();
        byte[] pixelData = _unlockedImage.Free();
        _unlockedImage = null;
        byte[] packedData = Pack(pixelData, GetBitsPerPixel(ihdr.BitDepth, ihdr.ColorType), (int)ihdr.Width, (int)ihdr.Height);
        idat.Data = PngIdatCodec.EncodeIdat(packedData, ihdr.Width, ihdr.Height, ihdr.BitDepth, ihdr.ColorType);
    }

    private static byte[] Pack(byte[] pixels, byte bitsPerPixel, int width, int height)
    {
        if (bitsPerPixel >= 8)
            return pixels;

        byte[] packed = new byte[((width * bitsPerPixel + 7) / 8) * height];
        int mask = ~(~1 << (bitsPerPixel - 1));
        int p = 0;
        int stride = width - 1;
        int i = 0;
        for (; i < packed.Length; i++)
        {
            byte b = 0;
            for (int bit = 8 - bitsPerPixel; bit >= 0 && p < pixels.Length && stride >= 0; bit -= bitsPerPixel, stride--)
                b |= (byte)((pixels[p++] & mask) << bit);
            packed[i] = b;

            if (stride <= 0)
                stride = width - 1;
        }
        return packed;
    }

    private void Load(Stream stream)
    {
        if (Signature.Any(b => stream.ReadByte() != b))
        {
            throw new FormatException("No PNG file loaded. Signature mismatch.");
        }

        long readEnd = stream.Length - Marshal.SizeOf(typeof(ChunkStart)) + 4;

        do LoadChunk(stream);
        while (readEnd > stream.Position);

        if (stream.Length > stream.Position)
            throw new FormatException("No PNG file loaded. Unexpected data at end.");
    }

    private void Load(Span<byte> data)
    {
        for (int i = 0; i < Signature.Length; i++)
            if (Signature[i] != data[i])
                throw new FormatException("No PNG data loaded. Signature mismatch.");
        data = data[Signature.Length..];
        long minSize = Marshal.SizeOf(typeof(ChunkStart)) + 4;

        do data = LoadChunk(data);
        while (data.Length >= minSize);

        if (data.Length > 0)
            throw new FormatException("No PNG file loaded. Unexpected data at end.");
    }

    private void LoadChunk(Stream stream)
    {
        long streamStart = stream.Position;
        ChunkStart start = new(stream);
        byte[] chunk = new byte[start.Size + 4];
        int read = stream.Read(chunk, 0, chunk.Length);
        if (read < chunk.Length)
            throw new FormatException($"Corrupt PNG file loaded. Malformed chunk ending at {stream.Position}.");

        uint crc = LoadChunk(start.Name, chunk);

        if (CheckCrc)
        {
            stream.Seek(streamStart + sizeof(UInt32), SeekOrigin.Begin);
            // ReSharper disable once MustUseReturnValue : Can not happen here, because then the previous read would have failed.
            stream.Read(chunk, 0, chunk.Length);
            stream.Position += sizeof(UInt32);
            long crcCalc = Crc.Get(chunk);
            if (crc != crcCalc)
                throw new Exception($"Corrupt PNG file loaded. Wrong CRC in chunk {_chunks.Count - 1}. Expected {crcCalc:x8}, found {crc:x8}");
        }
    }

    private Span<byte> LoadChunk(Span<byte> data)
    {
        ChunkStart start = new(data);
        Span<byte> chunk = data[ChunkStart.ObjectSize..(int)(ChunkStart.ObjectSize + start.Size + 4)];
        data = data[4..];
        if (chunk.Length < start.Size)
            throw new FormatException($"Corrupt PNG file loaded. Malformed chunk Nr. {_chunks.Count}");

        uint crc = LoadChunk(start.Name, chunk);
        if (CheckCrc)
        {
            long crcCalc = Crc.Get(data[..(int)(start.Size + 4)]);
            if (crc != crcCalc)
                throw new Exception($"Corrupt PNG file loaded. Wrong CRC in chunk {_chunks.Count - 1}. Expected {crcCalc:x8}, found {crc:x8}");
        }

        return data[(int)(start.Size + 8)..];
    }

    private uint LoadChunk(string chunkName, Span<byte> chunk)
    {
        Type t = AntiPebbleNGData.ChunkTypesByName.TryGetValue(chunkName, out Type? type) ? type : AntiPebbleNGData.ChunkTypesByName[""];

        if (!typeof(AbstractChunk).IsAssignableFrom(t))
            throw new InvalidOperationException("Only Classes inheriting IChunkData can be loaded.");

        chunk[..^4].Read(t, out object chunkO);
        AbstractChunk chunkData = (AbstractChunk)chunkO;
        chunkData.ChunkName = chunkName;
        _chunks.Add(chunkData);
        chunk[^4..].Read(out uint crc);
        return crc;
    }

    public void Save(string path) => Save(new FileInfo(path));

    public void Save(FileInfo fileInfo)
    {
        string? dirname = Path.GetDirectoryName(fileInfo.DirectoryName);
        if (dirname != null)
            Directory.CreateDirectory(dirname);

        using FileStream f = File.Open(fileInfo.FullName, FileMode.Create);
        Save(f);
    }

    public byte[] Save()
    {
        using MemoryStream ms = new();
        Save(ms);
        return ms.ToArray();
    }

    public void Save(Stream stream)
    {
        PrepareForSave();

        stream.Write(Signature);
        foreach (AbstractChunk abstractChunk in _chunks)
        {
            abstractChunk.Save(stream);
        }
    }

    private void PrepareForSave()
    {
        UpdateFrames();
        InsertMissingFctl();

        List<AbstractChunk?> chunks =
        [ //TODO: sprinkel in Texts
            _chunks.SpliceOrDefault<IhdrChunk>(),
            _chunks.SpliceOrDefault("cHRM"),
            _chunks.SpliceOrDefault("cICP"),
            _chunks.SpliceOrDefault("gAMA"),
            _chunks.SpliceOrDefault("iCCP"),
            _chunks.SpliceOrDefault("mDCV"),
            _chunks.SpliceOrDefault("cLLI"),
            _chunks.SpliceOrDefault("sBIT"),
            _chunks.SpliceOrDefault("sRGB"),
            _chunks.SpliceOrDefault<PlteChunk>(),
            _chunks.SpliceOrDefault<TrnsChunk>(),
            _chunks.SpliceOrDefault<BkgdChunk>(),
            _chunks.SpliceOrDefault("hIST"),
            _chunks.SpliceOrDefault<ActlChunk>() ?? new ActlChunk{NumFrames = 1},
            _chunks.SpliceOrDefault("eXIF"),
            _chunks.SpliceOrDefault<FctlChunk>(),
            _chunks.SpliceOrDefault("pHYs"),
            _chunks.SpliceOrDefault("sPLT"),
            _chunks.SpliceOrDefault<IdatChunk>(),
            //Insert rest here
            _chunks.SpliceOrDefault<IendChunk>(),
        ];
        List<AbstractChunk> orderedChunks = [.. chunks.OfType<AbstractChunk>()];

        foreach (IdatChunk idat in _chunks.OfType<IdatChunk>().ToList())
        {
            _chunks[_chunks.IndexOf(idat)] = new FdatChunk
            {
                FrameData = idat.Data
            };
        }

        orderedChunks.InsertRange(orderedChunks.Count - 1, _chunks);

        UInt32 id = 0;
        foreach ((FdatChunk? fdat, FctlChunk? fctl) in orderedChunks.OfType<FdatChunk, FctlChunk>())
        {
            if (fdat != null) fdat.SequenceNumber = id++;
            if (fctl != null) fctl.SequenceNumber = id++;
        }

        ActlChunk actl = orderedChunks.First<ActlChunk>();
        actl.NumFrames = (UInt32)orderedChunks.OfType<FctlChunk>().Count();
        if (actl.NumFrames < 2)
            orderedChunks.Remove(actl);

        if (StripDecoration)
        {
            //Safe to copy-flag = true, so only metadata.
            foreach (AbstractChunk chunk in _chunks.Where(c => char.IsLower(c.ChunkName[^1])))
                orderedChunks.Remove(chunk);
        }

        _chunks = orderedChunks;
    }

    private void UpdateFrames()
    {
        foreach (Frame frame in _frames)
            frame.FdAt.FrameData = frame.Idat.Data;
    }

    protected virtual void InsertMissingFctl()
    {
        if (_chunks.FirstOrDefault<FdatChunk>() == null)
            return;

        IhdrChunk ihdr = _chunks.First<IhdrChunk>();
        AbstractChunk? fctl = null;
        foreach (AbstractChunk chunk in _chunks.ToList())
        {
            switch (chunk)
            {
                case FctlChunk:
                    fctl = chunk;
                    break;
                case IdatChunk or FdatChunk when fctl != null:
                    fctl = null;
                    break;
                case IdatChunk or FdatChunk:
                    _chunks.Insert(_chunks.IndexOf(chunk), new FctlChunk
                    {
                        Width = ihdr.Width,//should be from IHDR of Image
                        Height = ihdr.Height,//should be from IHDR of Image
                        DelayNum = _defaultNum,
                        DelayDen = _defaultDen,
                        DisposeOp = DisposeOp.ApngDisposeOpNone,
                        BlendOp = BlendOp.ApngBlendOpOver
                    });
                    break;
            }
        }
    }

    private readonly List<Frame> _frames = [];

    public void AddFrame(Png image)
    {
        IhdrChunk ihdr = image._chunks.First<IhdrChunk>();
        IdatChunk idat = image._chunks.First<IdatChunk>();
        FctlChunk fctl;
        FdatChunk fdAt;

        _chunks.Add(fctl = new FctlChunk
        {
            Width = ihdr.Width,
            Height = ihdr.Height,
            XOffset = 0,
            YOffset = 0,
            DelayNum = _defaultNum,
            DelayDen = _defaultDen,
            DisposeOp = DisposeOp.ApngDisposeOpNone,
            BlendOp = BlendOp.ApngBlendOpOver
        });

        _chunks.Add(fdAt = new FdatChunk { FrameData = idat.Data });

        Frame frame = new(fctl, fdAt, ihdr, idat);
        _frames.Add(frame);
    }
}

public class Frame(FctlChunk fctl, FdatChunk fdAt, IhdrChunk ihdr, IdatChunk idat)
{
    internal FctlChunk Fctl = fctl;
    internal FdatChunk FdAt = fdAt;
    internal IhdrChunk Ihdr = ihdr;
    internal IdatChunk Idat = idat;
}

public abstract class AbstractChunk
{
    public string ChunkName { get; internal set; }

    protected AbstractChunk()
    {
        ChunkName = GetType().GetCustomAttribute<ChunkAttribute>().Name;
    }

    public void Save(Stream stream)
    {
        Span<byte> name = ChunkName.Write();
        Span<byte> data = this.Write();
        stream.Write(((UInt32)data.Length).Write());
        stream.Write(name);
        stream.Write(data);
        stream.Write(Crc.Get([.. name, .. data]).Write());
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ChunkStart
{
    public static readonly int ObjectSize = Marshal.SizeOf<ChunkStart>();

    public UInt32 Size;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
    public string Name = null!;

    internal ChunkStart(Stream stream)
    {
        Span<byte> chunkstart = new byte[Marshal.SizeOf<ChunkStart>()];
        int read = stream.Read(chunkstart);
        if (read < chunkstart.Length)
            throw new FormatException($"Corrupt PNG file loaded. Malformed chunk start at {stream.Position}.");

        Load(chunkstart);
    }

    internal ChunkStart(Span<byte> data) => Load(data[..Marshal.SizeOf<ChunkStart>()]);

    internal ChunkStart(string name) => Name = name;

    private void Load(Span<byte> chunk)
    {
        chunk = chunk.Read(out Size);
        chunk.Read(out Name);
    }
}

internal static class SpanExtension
{
    public static Span<byte> Read<T>(this Span<byte> bytes, out T value)
    {
        Span<byte> newPos = bytes.Read(typeof(T), out object o);
        value = (T)o;
        return newPos;
    }

    public static Span<byte> Read(this Span<byte> bytes, Type type, out object value)
    {
        if (type.IsEnum)
            type = Enum.GetUnderlyingType(type);
        else if (!type.IsPrimitive)
        {
            if (type == typeof(string))
            {
                string s = Encoding.Default.GetString(bytes);
                value = s;
                return bytes[s.Length..];
            }

            value = Activator.CreateInstance(type, true)!;

            foreach (FieldInfo fieldInfo in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (fieldInfo.FieldType.IsArray)
                {
                    if (fieldInfo.FieldType == typeof(byte[]))
                    {
                        fieldInfo.SetValue(value, bytes.ToArray());
                        bytes = bytes[^0..];
                    }
                    else
                    {
                        Type elemType = fieldInfo.FieldType.GetElementType()!;
                        IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elemType))!;
                        while (bytes.Length > 0)
                        {
                            bytes = bytes.Read(elemType, out object o);
                            list.Add(o);
                        }

                        Array array = (Array)Activator.CreateInstance(fieldInfo.FieldType, list.Count)!;
                        list.CopyTo(array, 0);
                        fieldInfo.SetValue(value, array);
                    }
                }
                else
                {
                    bytes = bytes.Read(fieldInfo.FieldType, out object o);
                    fieldInfo.SetValue(value, o);
                }
            }

            return bytes;
        }

        int size = Marshal.SizeOf(type);

        Span<byte> valueBytes = bytes[..size];
        valueBytes.Reverse();
        value = Type.GetTypeCode(type) switch
        {
            TypeCode.UInt16 => BitConverter.ToUInt16(valueBytes),
            TypeCode.UInt32 => BitConverter.ToUInt32(valueBytes),
            TypeCode.UInt64 => BitConverter.ToUInt64(valueBytes),
            TypeCode.Int16 => BitConverter.ToInt16(valueBytes),
            TypeCode.Int32 => BitConverter.ToInt32(valueBytes),
            TypeCode.Int64 => BitConverter.ToInt64(valueBytes),
            TypeCode.Byte => valueBytes[0],
            _ => throw new ArgumentException($"Cant handle {type.Name}.")
        };
        return bytes[size..];
    }

    public static Span<byte> Write(this object obj)
    {
        Type type = obj.GetType();

        if (type.IsEnum)
            type = Enum.GetUnderlyingType(type);

        else if (!type.IsPrimitive)
        {
            if (type == typeof(string))
                return Encoding.Default.GetBytes((string)obj);

            List<byte[]> data = [];

            foreach (FieldInfo fieldInfo in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (fieldInfo.FieldType.IsArray)
                {
                    if (fieldInfo.FieldType == typeof(byte[]))
                        data.Add((byte[])fieldInfo.GetValue(obj)!);
                    else
                        data.Add([.. ((IEnumerable)fieldInfo.GetValue(obj)).Cast<object>().SelectMany(o => o!.Write().ToArray())]);
                }
                else
                    data.Add([.. fieldInfo.GetValue(obj)!.Write()]);
            }

            return data.SelectMany(x => x).ToArray();
        }

        Span<byte> valueBytes = (Type.GetTypeCode(type) switch
        {
            TypeCode.UInt16 => BitConverter.GetBytes((UInt16)obj),
            TypeCode.UInt32 => BitConverter.GetBytes((UInt32)obj),
            TypeCode.UInt64 => BitConverter.GetBytes((UInt64)obj),
            TypeCode.Int16 => BitConverter.GetBytes((Int16)obj),
            TypeCode.Int32 => BitConverter.GetBytes((Int32)obj),
            TypeCode.Int64 => BitConverter.GetBytes((Int64)obj),
            TypeCode.Byte => [(byte)obj],
            _ => throw new ArgumentException($"Cant handle {type.Name}.")
        }).AsSpan();
        valueBytes.Reverse();
        return valueBytes;
    }
}

public enum ColorType : byte
{
    /// <summary>
    /// Allows <see cref="Ihdr.BitDepth"/> of 1, 2, 4, 8 and 16
    /// </summary>
    Greyscale = 0,
    /// <summary>
    /// Allows <see cref="Ihdr.BitDepth"/> of 8 and 16
    /// </summary>
    Truecolor = 2,
    /// <summary>
    /// Allows <see cref="Ihdr.BitDepth"/> of 1, 2, 4 and 8
    /// </summary>
    IndexedColor = 3,
    /// <summary>
    /// Allows <see cref="Ihdr.BitDepth"/> of 8 and 16
    /// </summary>
    GreyscaleWithAlpha = 4,
    /// <summary>
    /// Allows <see cref="Ihdr.BitDepth"/> of 8 and 16
    /// </summary>
    TruecolorWithAlpha = 6
}

[AttributeUsage(AttributeTargets.Class)]
public class ChunkAttribute(string name) : Attribute
{
    public readonly string Name = name;
}

[Chunk("IHDR")]
public class IhdrChunk : AbstractChunk
{
    public uint Width;
    public uint Height;
    public byte BitDepth;
    public ColorType ColorType;
    public byte CompressionMethod;
    public byte FilterMethod;
    public byte InterlaceMethod;
}

[Chunk("PLTE")]
public class PlteChunk : AbstractChunk
{
    public PalletColor[] Colors = null!;
}

[Chunk("IDAT")]
public class IdatChunk : AbstractChunk
{
    public byte[] Data = null!;
}

[Chunk("IEND")]
public class IendChunk : AbstractChunk
{ }

[Chunk("acTL")]
public class ActlChunk : AbstractChunk
{
    public UInt32 NumFrames;
    public UInt32 NumPlays;
}

[Chunk("fcTL")]
public class FctlChunk : AbstractChunk
{
    public UInt32 SequenceNumber;
    public UInt32 Width;
    public UInt32 Height;
    public UInt32 XOffset;
    public UInt32 YOffset;
    public UInt16 DelayNum;
    public UInt16 DelayDen;
    public DisposeOp DisposeOp;
    public BlendOp BlendOp;
}

public enum DisposeOp : byte
{
    ApngDisposeOpNone = 0,
    ApngDisposeOpBackground = 1,
    ApngDisposeOpPrevious = 2,
}

public enum BlendOp : byte
{
    ApngBlendOpSource = 0,
    ApngBlendOpOver = 1,
}

[Chunk("fdAT")]
public class FdatChunk : AbstractChunk
{
    public UInt32 SequenceNumber;
    public byte[] FrameData = null!;
}

[Chunk("tEXt")]
public class TextChunk : AbstractChunk
{
    public string Value = null!;

    public string Keyword => Value.Split((char)0)[0];
    public string Message => Value.Split((char)0)[1];
}

[Chunk("tRNS")]
public class TrnsChunk : AbstractChunk
{
    public byte[] ColorData = null!;
}

[Chunk("bKGD")]
public class BkgdChunk : AbstractChunk
{
    public byte[] ColorData = null!;
}

[Chunk("")]
public class UnknownChunk : AbstractChunk
{
    internal UnknownChunk() { }

    public byte[] Data = null!;
}

public static class Colors
{
    public static readonly ColorRgba8 Black = new(0, 0, 0, 255);
    public static readonly ColorRgba8 White = new(255, 255, 255, 255);
    public static readonly ColorRgba8 Red = new(255, 0, 0, 255);
    public static readonly ColorRgba8 Green = new(0, 255, 0, 255);
    public static readonly ColorRgba8 Blue = new(0, 0, 255, 255);
}

public abstract class Image(in int width, in int height)
{
    public readonly int Width = width;
    public readonly int Height = height;

    public abstract IColor this[int x, int y] { get; set; }
    public abstract IColor GetPixel(int x, int y);
    public abstract void SetPixel(int x, int y, IColor color);
    internal abstract byte[] Free();

    public void FlipVertical()
    {
        for (int y = 0; y < Height; y++)
            for (int left = 0, right = Width - 1; left < right; left++, right--)
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
            for (int top = 0, bottom = Height - 1; top < bottom; top++, bottom--)
            {
                IColor cTop = this[x, top];
                IColor cBottom = this[x, bottom];

                this[x, top] = cBottom;
                this[x, bottom] = cTop;
            }
    }
}

public class Image<TColor>(int width, int height, Span<byte> data) : Image(width, height)
    where TColor : struct, IColor
{
    private readonly TColor[] _data = MemoryMarshal.Cast<byte, TColor>(data).ToArray();

    internal override byte[] Free()
    {
        return MemoryMarshal.Cast<TColor, byte>(_data).ToArray();
    }

    public override IColor this[int x, int y]
    {
        get => GetPixel(x, y);
        set => SetPixel(x, y, value);
    }

    public override IColor GetPixel(int x, int y) => _data[x + y * Width];

    public override void SetPixel(int x, int y, IColor color) => _data[x + y * Width] = (TColor)color;
}

public interface IColor
{
}

public static class ColorExtension
{
    public static IColor ConvertTo(this IColor color, ColorType colorType, byte bitDepth)
    {
        if (color is ColorRgba16 rgba)
#pragma warning disable CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
#pragma warning disable CS8509 // The switch expression does not handle all possible values of its input type (it is not exhaustive).
            return colorType switch
            {
                ColorType.Greyscale =>
                    bitDepth switch
                    {
                        1 => new ColorGray1((byte)((rgba.R + rgba.G + rgba.B) / (3 * 0x8000))),
                        2 => new ColorGray2((byte)((rgba.R + rgba.G + rgba.B) / (3 * 0x4000))),
                        4 => new ColorGray4((byte)((rgba.R + rgba.G + rgba.B) / (3 * 0x1000))),
                        8 => new ColorGray8((byte)((rgba.R + rgba.G + rgba.B) / (3 * 256))),
                        16 => new ColorGray16((UInt16)((rgba.R + rgba.G + rgba.B) / 3))
                    },
                ColorType.Truecolor =>
                    bitDepth switch
                    {
                        8 => new ColorRgb8((byte)(rgba.R / 256), (byte)(rgba.G / 256), (byte)(rgba.B / 256)),
                        16 => new ColorRgb16(rgba.R, rgba.G, rgba.B)
                    },
                ColorType.IndexedColor =>
                    //TODO ... somehow pass in the palette
                    throw new InvalidOperationException("Conversion not supported."),
                ColorType.GreyscaleWithAlpha =>
                    bitDepth switch
                    {
                        8 => new ColorGrayAlpha8((byte)((rgba.R + rgba.G + rgba.B) / (3 * 256)), (byte)(rgba.A / 256)),
                        16 => new ColorGrayAlpha16((UInt16)((rgba.R + rgba.G + rgba.B) / 3), rgba.A)
                    },
                ColorType.TruecolorWithAlpha =>
                    bitDepth switch
                    {
                        8 => new ColorRgba8((byte)(rgba.R / 256), (byte)(rgba.G / 256), (byte)(rgba.B / 256), (byte)(rgba.A / 256)),
                        16 => rgba,
                    },
            };
#pragma warning restore CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
#pragma warning restore CS8509 // The switch expression does not handle all possible values of its input type (it is not exhaustive).

        //Convert all to RGBA because it is most expressive.
        return (color switch
        {
            ColorGray1 c => new ColorRgba16((UInt16)(c.Value * 0x8000), (UInt16)(c.Value * 0x8000), (UInt16)(c.Value * 0x8000), UInt16.MaxValue),
            ColorGray2 c => new ColorRgba16((UInt16)(c.Value * 0x4000), (UInt16)(c.Value * 0x4000), (UInt16)(c.Value * 0x4000), UInt16.MaxValue),
            ColorGray4 c => new ColorRgba16((UInt16)(c.Value * 0x1000), (UInt16)(c.Value * 0x1000), (UInt16)(c.Value * 0x1000), UInt16.MaxValue),
            ColorGray8 c => new ColorRgba16((UInt16)(c.Value * 256), (UInt16)(c.Value * 256), (UInt16)(c.Value * 256), UInt16.MaxValue),
            ColorGray16 c => new ColorRgba16(c.Value, c.Value, c.Value, UInt16.MaxValue),
            ColorRgb8 c => new ColorRgba16((UInt16)(c.R * 256), (UInt16)(c.G * 256), (UInt16)(c.B * 256), UInt16.MaxValue),
            ColorRgb16 c => new ColorRgba16(c.R, c.G, c.B, UInt16.MaxValue),
            //TODO Indexed
            ColorGrayAlpha8 c => new ColorRgba16((UInt16)(c.Value * 256), (UInt16)(c.Value * 256), (UInt16)(c.Value * 256), (UInt16)(c.Alpha * 256)),
            ColorGrayAlpha16 c => new ColorRgba16(c.Value, c.Value, c.Value, c.Alpha),
            ColorRgba8 c => new ColorRgba16((UInt16)(c.R * 256), (UInt16)(c.G * 256), (UInt16)(c.B * 256), (UInt16)(c.A * 256)),
            _ => throw new InvalidOperationException("Conversion not supported.")
        }).ConvertTo(colorType, bitDepth);
    }
}

[DebuggerDisplay("{Value,nq}")]
public readonly struct ColorGray1(byte value) : IColor
{
    public readonly byte Value = value;
}

[DebuggerDisplay("{Value,nq}")]
public readonly struct ColorGray2(byte value) : IColor
{
    public readonly byte Value = value;
}

[DebuggerDisplay("{Value,nq}")]
public readonly struct ColorGray4(byte value) : IColor
{
    public readonly byte Value = value;
}

[DebuggerDisplay("{Value,nq}")]
public readonly struct ColorGray8(byte value) : IColor
{
    public readonly byte Value = value;
}

[DebuggerDisplay("{Value,nq}")]
public readonly struct ColorGray16(ushort value) : IColor
{
    public readonly UInt16 Value = value;
}

public readonly struct PalletColor : IColor
{
    public readonly byte R;
    public readonly byte G;
    public readonly byte B;
}

public readonly struct ColorRgb8(byte r, byte g, byte b) : IColor
{
    public readonly byte R = r;
    public readonly byte G = g;
    public readonly byte B = b;
}

public readonly struct ColorRgb16(ushort r, ushort g, ushort b) : IColor
{
    public readonly UInt16 R = r;
    public readonly UInt16 G = g;
    public readonly UInt16 B = b;
}

public readonly struct ColorIndexed1 : IColor
{
    public readonly byte Value;
}

public readonly struct ColorIndexed2 : IColor
{
    public readonly byte Value;
}

public readonly struct ColorIndexed4 : IColor
{
    public readonly byte Value;
}

public readonly struct ColorIndexed8 : IColor
{
    public readonly byte Value;
}

public readonly struct ColorGrayAlpha8(byte value, byte alpha) : IColor
{
    public readonly byte Value = value;
    public readonly byte Alpha = alpha;
}

public readonly struct ColorGrayAlpha16(ushort value, ushort alpha) : IColor
{
    public readonly UInt16 Value = value;
    public readonly UInt16 Alpha = alpha;
}

public readonly struct ColorRgba8(byte r, byte g, byte b, byte a) : IColor
{
    public readonly byte R = r;
    public readonly byte G = g;
    public readonly byte B = b;
    public readonly byte A = a;
}

public readonly struct ColorRgba16(ushort r, ushort g, ushort b, ushort a) : IColor
{
    public readonly UInt16 R = r;
    public readonly UInt16 G = g;
    public readonly UInt16 B = b;
    public readonly UInt16 A = a;
}

public sealed class DisposableAction(Action dispose) : IDisposable
{
    private Action? _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
    public void Dispose() => Dispose(true);
    private void Dispose(bool disposing)
    {
        if (!disposing || _dispose == null)
            return;

        try { _dispose(); }
        catch (Exception)
        {
            /* ignored */
        }

        _dispose = null;
    }
}

public static class ChunkListExtension
{
    public static IEnumerable<(T1?, T2?)> OfType<T1, T2>(this IEnumerable<AbstractChunk> chunks) where T1 : AbstractChunk where T2 : AbstractChunk
        => chunks.Where(c => c is T1 or T2).Select(c => (c as T1, c as T2));

    public static T First<T>(this IEnumerable<AbstractChunk> chunks) where T : AbstractChunk
        => chunks.OfType<T>().First();

    public static T? FirstOrDefault<T>(this IEnumerable<AbstractChunk> chunks) where T : AbstractChunk
        => chunks.OfType<T>().FirstOrDefault();

    public static AbstractChunk? FirstOrDefault(this IEnumerable<AbstractChunk> chunks, string type)
    {
        return chunks.FirstOrDefault(c => c.ChunkName == type);
    }

    public static T? SpliceOrDefault<T>(this IList<AbstractChunk> chunks) where T : AbstractChunk
    {
        T? value = chunks.FirstOrDefault<T>();
        if (value != null)
            chunks.Remove(value);
        return value;
    }

    public static AbstractChunk? SpliceOrDefault(this IList<AbstractChunk> chunks, string type)
    {
        AbstractChunk? value = chunks.FirstOrDefault(c => c.ChunkName == type);
        if (value != null)
            chunks.Remove(value);
        return value;
    }
}

internal static class Crc
{
    private static readonly uint[] PTable = new uint[256];

    static Crc()
    {
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                if ((c & 1) == 1)
                    c = 0xedb88320 ^ ((c >> 1) & 0x7FFFFFFF);
                else
                    c = (c >> 1) & 0x7FFFFFFF;
            PTable[n] = c;
        }
    }

    public static uint Get(Span<byte> input)
    {
        uint c = 0xffffffff;
        foreach (byte b in input)
            c = PTable[(c ^ b) & 0xff] ^ ((c >> 8) & 0xFFFFFF);
        return c ^ 0xffffffff;
    }
}

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

    public static byte[] DecodeIdat(byte[] idatData, UInt32 width, UInt32 height, byte bitDepth, ColorType colorType)
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

        return result;
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

    public static byte[] EncodeIdat(byte[] raw, UInt32 width, UInt32 height, byte bitDepth, ColorType colorType, PngFilterType filterType = PngFilterType.None)
    {
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

    private static int ComputeStride(UInt32 width, byte bitDepth, ColorType colorType)
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