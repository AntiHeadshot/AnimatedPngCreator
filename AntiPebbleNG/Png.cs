using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace AntiPebbleNG;

public class Png
{
    public bool CheckCrc = false;

    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public List<AbstractChunk> _chunks = [];

    public float DefaultDelayInSeconds
    {
        get => (float)_defaultNum / _defaultDen;
        set => Maths.ExpandFraction(value, out _defaultNum, out _defaultDen);
    }

    public uint Width { get; private set; }
    public uint Height { get; private set; }
    public bool Interlaced { get; private set; }
    public byte BitDepth { get; private set; }
    public ColorType ColorType { get; private set; }
    public ColorRgba8[] Palette { get; private set; } = null!;//Is set from Init()

    private ushort _defaultNum = 1;
    private ushort _defaultDen = 100;

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
        using MemoryStream ms = new(data);
        Load(ms);
        Init();
    }

    private Png() { Palette = []; }

    public static Png Create(uint width, uint height, ColorType colorType = ColorType.TruecolorWithAlpha, byte bitDepth = 8, ColorRgba8[]? palette = null, IColor? fillColor = null)
    {
        if (palette == null && colorType == ColorType.IndexedColor)
            throw new ArgumentException("IndexedColor set, but no palette provided", nameof(palette));
        if (1 << (bitDepth) < (palette?.Length ?? 0))
            throw new ArgumentException($"palette length {palette!.Length} is too big for bitDepth {bitDepth} ({1 << (bitDepth - 1)} possible values)", nameof(bitDepth));

        Png png = new();
        fillColor ??= Colors.Black;

        png._chunks.Add(new IhdrChunk
        {
            BitDepth = bitDepth,
            ColorType = colorType,
            Height = height,
            Width = width,
            CompressionMethod = 0,
        });

        if (palette?.Length > 0)
        {
            png._chunks.Add(new PlteChunk
            {
                Colors = [.. palette.Select(c => new PalletColor { R = c.R, G = c.G, B = c.B })]
            });

            if (palette.Any(c => c.A != byte.MaxValue))
            {
                png._chunks.Add(new TrnsChunk
                {
                    //Remaining A=255s do not have to be present.
                    ColorData = [.. palette.Select(c => c.A).Reverse().SkipWhile(x => x == 255).Reverse()]
                });
            }
        }

        IColor iColor = fillColor.ConvertTo(colorType, bitDepth, palette!);

        int size = Marshal.SizeOf(iColor);

        Image image;

        if (colorType != ColorType.IndexedColor)
        {
            image = Image.Create(width, height, new byte[size * width * height], bitDepth, colorType, palette!);
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    image.SetPixel(x, y, iColor);
        }
        else
        {
            Image<ColorIndexByte> indexImage = new(width, height, new byte[width * height], [], ColorType.IndexedColor, bitDepth);

            Dictionary<ColorRgba8, int> reversePallet = palette!.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i);

            ColorRgba8 color = (ColorRgba8)fillColor.ConvertTo(ColorType.TruecolorWithAlpha, 8, []);

            var index = new ColorIndexByte((byte)reversePallet[color]);

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    indexImage.SetPixel(x, y, index);

            image = indexImage;
        }

        byte[] byteData = image.GetBytes();
        png._chunks.Add(new IdatChunk { ImageData = PngIdatCodec.EncodeIdat(byteData, width, height, bitDepth, colorType, false) });
        png._chunks.Add(new IendChunk());

        png.Init();
        return png;
    }

    private void Init()
    {
        if (_chunks.FirstOrDefault<IdatChunk>() == null)
            throw new FormatException("PNG without image data. Seems to be corrupted.");

        IhdrChunk ihdr = _chunks.First<IhdrChunk>();
        Width = ihdr.Width;
        Height = ihdr.Height;
        BitDepth = ihdr.BitDepth;
        ColorType = ihdr.ColorType;
        Interlaced = ihdr.InterlaceMethod == 1;

        if (!Enum.IsDefined(typeof(ColorType), ColorType))
            throw new FormatException("PNG with unknown ColorType.");

        if (BitDepth == 0)
            throw new FormatException("PNG with BitDepth of 0.");
        //Is not power of 2
        else if ((BitDepth & (BitDepth - 1)) != 0)
            throw new FormatException("PNG with BitDepth other than a power of 2.");

        PlteChunk? plte = _chunks.FirstOrDefault<PlteChunk>();
        TrnsChunk? trns = _chunks.FirstOrDefault<TrnsChunk>();

        Palette = plte?.Colors.Select((p, i) =>
            new ColorRgba8(p.R, p.G, p.B, trns?.ColorData.Length > i ? trns.ColorData[i] : byte.MaxValue)).ToArray() ?? [];

        SquishFrames();
    }

    private void SquishFrames()
    {
        List<IdatChunk> idats = [.. _chunks.OfType<IdatChunk>()];
        idats[0].ImageData = [.. idats.SelectMany(x => x.ImageData)];
        foreach (IdatChunk idat in idats.Skip(1))
            _chunks.Remove(idat);

        //TODO: Squish fdAt Frames
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

    private void LoadChunk(Stream stream)
    {
        long streamStart = stream.Position;

        Span<byte> chunkstart = new byte[sizeof(uint) + 4];
        int read = stream.Read(chunkstart);
        if (read < 8)
            throw new FormatException("Corrupt PNG file loaded. Malformed chunk start");
        chunkstart[..4].Read(out uint size);
        chunkstart[^4..].Read(out string name);

        byte[] chunk = new byte[size + 4];
        read = stream.Read(chunk, 0, chunk.Length);
        if (read < chunk.Length)
            throw new FormatException($"Corrupt PNG file loaded. Malformed chunk ending at {stream.Position}.");

        uint crc = LoadChunk(name, chunk);

        if (CheckCrc)
        {
            stream.Seek(streamStart + sizeof(uint), SeekOrigin.Begin);
            // ReSharper disable once MustUseReturnValue : Can not happen here, because then the previous read would have failed.
            stream.Read(chunk, 0, chunk.Length);
            stream.Position += sizeof(uint);
            long crcCalc = Crc.Get(chunk);
            if (crc != crcCalc)
                throw new Exception($"Corrupt PNG file loaded. Wrong CRC in chunk {_chunks.Count - 1}. Expected {crcCalc:x8}, found {crc:x8}");
        }
    }

    private uint LoadChunk(string chunkName, Span<byte> chunk)
    {
        Type t = AntiPebbleNgData.ChunkTypesByName.TryGetValue(chunkName, out Type? type) ? type : typeof(UnknownChunk);

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

        //TODO separate big IDAT and fDAT into multiples
        foreach (AbstractChunk abstractChunk in _chunks)
            abstractChunk.Save(stream);
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
            _chunks.SpliceOrDefault<ActlChunk>() ?? new ActlChunk(),
            _chunks.SpliceOrDefault("eXIF"),
            _chunks.SpliceOrDefault<FctlChunk>(),
            _chunks.SpliceOrDefault("pHYs"),
            _chunks.SpliceOrDefault("sPLT"),
            _chunks.SpliceOrDefault<IdatChunk>(),
            //Insert rest here
            _chunks.SpliceOrDefault<IendChunk>(),
        ];
        List<AbstractChunk> orderedChunks = [.. chunks.OfType<AbstractChunk>()];

        // foreach (IdatChunk idat in _chunks.OfType<IdatChunk>().ToList())
        //     _chunks[_chunks.IndexOf(idat)] = new FdatChunk(idat.ImageData);

        orderedChunks.InsertRange(orderedChunks.Count - 1, _chunks);

        uint id = 0;
        foreach ((FdatChunk? fdat, FctlChunk? fctl) in orderedChunks.OfType<FdatChunk, FctlChunk>())
        {
            if (fdat != null) fdat.SequenceNumber = id++;
            if (fctl != null) fctl.SequenceNumber = id++;
        }

        ActlChunk actl = orderedChunks.First<ActlChunk>();
        actl.NumFrames = (uint)orderedChunks.OfType<FctlChunk>().Count();
        if (actl.NumFrames < 2)
            orderedChunks.Remove(actl);

        //Safe to copy-flag = true, so only metadata.
        if (StripDecoration)
            foreach (AbstractChunk chunk in _chunks.Where(c => char.IsLower(c.ChunkName[^1])))
                orderedChunks.Remove(chunk);

        _chunks = orderedChunks;
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
        FdatChunk fdAt;

        _chunks.Add(new FctlChunk
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

        //TODO: convert Image to same Format as main Image
        _chunks.Add(fdAt = new FdatChunk { ImageData = idat.ImageData });
        _frames.Add(new Frame(fdAt, idat));
    }

    private void UpdateFrames()
    {
        //TODO: convert Image to same Format as main Image
        foreach (Frame frame in _frames)
            frame.FdAt.ImageData = frame.Idat.ImageData;
    }

    public void AddFrame(Image image)
    {
        _chunks.Add(new FctlChunk
        {
            Width = image.Width,
            Height = image.Height,
            XOffset = 0,
            YOffset = 0,
            DelayNum = _defaultNum,
            DelayDen = _defaultDen,
            DisposeOp = DisposeOp.ApngDisposeOpNone,
            BlendOp = BlendOp.ApngBlendOpOver
        });

        //TODO: convert Image to same Format as main Image
        _chunks.Add(new FdatChunk { ImageData = PngIdatCodec.EncodeIdat(image.GetBytes(), image.Width, image.Height, image.BitDepth, image.ColorType, Interlaced) });
    }

    private readonly HashSet<int> _unlockedImages = [];

    public IDisposable Unlock(out Image image, int frameNr = 0)
    {
        if (!_unlockedImages.Add(frameNr))
            throw new InvalidOperationException("Already unlocked");
        IImageDataCunk idat = _chunks.OfType<IImageDataCunk>().Skip((int)frameNr).First();
        IImageMetadataCunk ihdr = _chunks.TakeWhile(x => x != idat).OfType<IImageMetadataCunk>().Last();
        byte[] pixelData = PngIdatCodec.DecodeIdat(idat.ImageData, ihdr.Width, ihdr.Height, BitDepth, ColorType, Interlaced);
        Image cachedImage = image = Image.Create(ihdr.Width, ihdr.Height, pixelData, BitDepth, ColorType, Palette);
        GamaChunk? gama = _chunks.FirstOrDefault<GamaChunk>();
        if (gama != null && gama.Gamma != 100000 && gama.Gamma != 0)
            PngGamma.ApplyGamma(image, BitDepth, gama.Gamma);
        return new DisposableAction(() => Lock(cachedImage, frameNr));
    }

    private void Lock(Image image, int frameNr)
    {
        IImageDataCunk idat = _chunks.OfType<IImageDataCunk>().Skip(frameNr).First();

        if (ColorType == ColorType.IndexedColor)
        {
            Image<ColorIndexByte> indexImage = new(Width, Height, new byte[Width * Height], [], ColorType.IndexedColor, BitDepth);

            Dictionary<ColorRgba8, int> reversePallet = Palette.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i);

            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    byte value = (byte)reversePallet[Palette.GetClosestColor((ColorRgba8)image.GetPixel(x, y))];
                    indexImage.SetPixel(x, y, new ColorIndexByte(value));
                }

            image = indexImage;
        }

        idat.ImageData = PngIdatCodec.EncodeIdat(image.GetBytes(), image.Width, image.Height, image.BitDepth, image.ColorType, Interlaced);
        _unlockedImages.Remove(frameNr);
    }

}

public class Frame(FdatChunk fdAt, IdatChunk idat)
{
    internal FdatChunk FdAt = fdAt;
    internal IdatChunk Idat = idat;
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

    private void Load(Span<byte> chunk)
    {
        chunk = chunk.Read(out Size);
        chunk.Read(out Name);
    }
}

public enum ColorType : byte
{
    /// <summary>
    /// Allows <see cref="IhdrChunk.BitDepth"/> of 1, 2, 4, 8 and 16
    /// </summary>
    Greyscale = 0,
    /// <summary>
    /// Allows <see cref="IhdrChunk.BitDepth"/> of 8 and 16
    /// </summary>
    Truecolor = 2,
    /// <summary>
    /// Allows <see cref="IhdrChunk.BitDepth"/> of 1, 2, 4 and 8
    /// </summary>
    IndexedColor = 3,
    /// <summary>
    /// Allows <see cref="IhdrChunk.BitDepth"/> of 8 and 16
    /// </summary>
    GreyscaleWithAlpha = 4,
    /// <summary>
    /// Allows <see cref="IhdrChunk.BitDepth"/> of 8 and 16
    /// </summary>
    TruecolorWithAlpha = 6
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