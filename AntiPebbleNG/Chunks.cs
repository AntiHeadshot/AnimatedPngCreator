using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace AntiPebbleNG;

[Chunk("IHDR")]
public class IhdrChunk : AbstractChunk, IImageMetadataCunk
{
    public uint Width { get; set; }
    public uint Height { get; set; }
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

public struct PalletColor
{
    public byte R;
    public byte G;
    public byte B;
}

[Chunk("IDAT")]
public class IdatChunk : AbstractChunk, IImageDataCunk
{
    public byte[] ImageData { get; set; } = null!;
}

[Chunk("IEND")]
public class IendChunk : AbstractChunk
{ }

[Chunk("acTL")]
public class ActlChunk : AbstractChunk
{
    public uint NumFrames;
    public uint NumPlays;
}

[Chunk("fcTL")]
public class FctlChunk : AbstractChunk, IImageMetadataCunk
{
    public uint SequenceNumber;
    public uint Width { get; set; }
    public uint Height { get; set; }
    public uint XOffset;
    public uint YOffset;
    public ushort DelayNum;
    public ushort DelayDen;
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
public class FdatChunk : AbstractChunk, IImageDataCunk
{
    public uint SequenceNumber;

    public FdatChunk()
    {
    }

    public FdatChunk(byte[] imageData)
    {
        ImageData = imageData;
    }

    public byte[] ImageData { get; set; } = null!;
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

[Chunk("gAMA")]
public class GamaChunk : AbstractChunk
{
    public uint Gamma;
}

public class UnknownChunk : AbstractChunk
{
    internal UnknownChunk() { }

    public byte[] Data = null!;
}

public interface IImageDataCunk
{
    byte[] ImageData { get; set; }
}

public interface IImageMetadataCunk
{
    uint Width { get; set; }
    uint Height { get; set; }
}

public abstract class AbstractChunk
{
    public string ChunkName { get; internal set; }

    protected AbstractChunk()
    {
        ChunkName = GetType().GetCustomAttribute<ChunkAttribute>()?.Name ?? "";
    }

    public void Save(Stream stream)
    {
        Span<byte> name = ChunkName.Write();
        Span<byte> data = this.Write();
        stream.Write(((uint)data.Length).Write());
        stream.Write(name);
        stream.Write(data);
        stream.Write(Crc.Get([.. name, .. data]).Write());
    }
}