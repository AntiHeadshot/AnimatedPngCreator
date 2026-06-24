using Anti;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Anti
{
    internal static class Crc
    {
        private static readonly uint[] PTable = new uint[256];

        static Crc()
        {
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                {
                    if ((c & 1) == 1)
                        c = 0xedb88320 ^ ((c >> 1) & 0x7FFFFFFF);
                    else
                        c = (c >> 1) & 0x7FFFFFFF;
                }
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

    public static class ChunkListExtension
    {
        public static IEnumerable<AbstractChunk> OfType(this IEnumerable<AbstractChunk> chunks, string type)
            => chunks.Where(c => c.Start.Name == type);

        public static IEnumerable<Chunk<T>> OfType<T>(this IEnumerable<AbstractChunk> chunks) where T : struct, IChunkData
            => chunks.Where(c => c.OData is T).Select(c => (c as Chunk<T>)!);

        public static IEnumerable<(Chunk<T1>?, Chunk<T2>?)> OfType<T1, T2>(this IEnumerable<AbstractChunk> chunks) where T1 : struct, IChunkData where T2 : struct, IChunkData
            => chunks.Where(c => c.OData is T1 or T2).Select(c => (c as Chunk<T1>, c as Chunk<T2>));

        public static Chunk<T> First<T>(this IEnumerable<AbstractChunk> chunks) where T : struct, IChunkData
            => (chunks.First(c => c.OData is T) as Chunk<T>)!;

        public static Chunk<T>? FirstOrDefault<T>(this IEnumerable<AbstractChunk> chunks) where T : struct, IChunkData
            => chunks.FirstOrDefault(c => c.OData is T) as Chunk<T>;

        public static AbstractChunk? FirstOrDefault(this IEnumerable<AbstractChunk> chunks, string type)
            => chunks.FirstOrDefault(c => c.Start.Name == type);

        public static Chunk<T>? SpliceOrDefault<T>(this IList<AbstractChunk> chunks) where T : struct, IChunkData
            => chunks.SpliceOrDefault(typeof(T).Name) as Chunk<T>;

        public static AbstractChunk? SpliceOrDefault(this IList<AbstractChunk> chunks, string type)
        {
            AbstractChunk? value = chunks.FirstOrDefault(c => c.Start.Name == type);

            if (value != null)
                chunks.Remove(value);

            return value;
        }
    }

    public class Png
    {
        public static bool CheckCrc = false;

        private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        public List<AbstractChunk> Chunks = [];

        public float DefaultDelayInSeconds
        {
            get => (float)defaultNum / defaultDen;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentException("Value must be a finite number.");

                if (value <= 0)
                {
                    defaultNum = 0;
                    defaultDen = 1;
                    return;
                }

                // Continued fraction expansion
                const int max = ushort.MaxValue;

                int a0 = (int)Math.Floor(value);
                if (a0 > max)
                {
                    defaultNum = max;
                    defaultDen = 1;
                    return;
                }

                int n0 = 1, d0 = 0;     // previous convergent
                int n1 = a0, d1 = 1;    // current convergent

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

                defaultNum = (ushort)n1;
                defaultDen = (ushort)d1;
            }
        }

        private UInt16 defaultNum = 1;
        private UInt16 defaultDen = 100;

        public bool StripDecoration = false;

        public Png(string filename)
        {
            using var file = File.OpenRead(filename);
            Load(file);
        }

        public Png(Stream stream)
        {
            Load(stream);
        }

        public Png(byte[] data)
        {
            Load(data);
        }

        private void Load(Stream stream)
        {
            foreach (byte b in Signature)
            {
                int r = stream.ReadByte();
                if (b != r)
                    throw new FormatException("No PNG file loaded. Signature missmatch.");
            }

            long readEnd = stream.Length - Marshal.SizeOf(typeof(ChunkStart)) + sizeof(UInt32);

            do LoadChunk(stream);
            while (readEnd > stream.Position);

            if (stream.Length > stream.Position)
                throw new FormatException("No PNG file loaded. Unexpected data at end.");
        }

        private void Load(Span<byte> data)
        {
            for (int i = 0; i < Signature.Length; i++)
                if (Signature[i] != data[i])
                    throw new FormatException("No PNG data loaded. Signature missmatch.");

            long minSize = Marshal.SizeOf(typeof(ChunkStart)) + sizeof(UInt32);

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

            AbstractChunk chunkData = LoadChunk(start, chunk);

            if (CheckCrc)
            {
                stream.Seek(streamStart + sizeof(UInt32), SeekOrigin.Begin);
                stream.ReadExactly(chunk, 0, chunk.Length);
                stream.Position += sizeof(UInt32);
                long crc = Crc.Get(chunk);
                if (chunkData.CRC != crc)
                    throw new Exception($"Corrupt PNG file loaded. Wrong CRC in chunk {Chunks.Count - 1}. Expected {crc:x8}, found {chunkData.CRC:x8}");
            }
        }

        private Span<byte> LoadChunk(Span<byte> data)
        {
            ChunkStart start = new(data);
            Span<byte> chunk = data[(int)(start.Size + 4)..];
            if (chunk.Length < start.Size)
                throw new FormatException($"Corrupt PNG file loaded. Malformed chunk Nr. {Chunks.Count}");

            AbstractChunk chunkData = LoadChunk(start, chunk);
            if (CheckCrc)
            {
                long crc = Crc.Get(data[4..^4]);
                if (chunkData.CRC != crc)
                    throw new Exception($"Corrupt PNG file loaded. Wrong CRC in chunk {Chunks.Count - 1}. Expected {crc:x8}, found {chunkData.CRC:x8}");
            }

            return data[chunk.Length..];
        }

        private AbstractChunk LoadChunk(ChunkStart chunkStart, Span<byte> chunk)
        {
            AbstractChunk chunkData = chunkStart.Name switch
            {
                "IHDR" => LoadChunk<IHDR>(chunk),
                "PLTE" => LoadChunk<PLTE>(chunk),
                "IDAT" => LoadChunk<IDAT>(chunk),
                "IEND" => LoadChunk<IEND>(chunk),
                "acTL" => LoadChunk<acTL>(chunk),
                "fcTL" => LoadChunk<fcTL>(chunk),
                "fdAT" => LoadChunk<fdAT>(chunk),
                "tEXt" => LoadChunk<tEXt>(chunk),
                _ => LoadChunk<Unknown>(chunk),
            };
            chunkData.Start = chunkStart;
            Chunks.Add(chunkData);
            return chunkData;
        }

        private AbstractChunk LoadChunk<T>(Span<byte> data) where T : struct, IChunkData
        {
            Chunk<T> chunk = (Chunk<T>)Activator.CreateInstance(typeof(Chunk<T>), true)!;

            data[..^4].Read(out chunk.Data);
            data[^4..].Read(out chunk.CRC);

            return chunk;
        }

        public void Save(string path) => Save(new FileInfo(path));

        public void Save(FileInfo fileInfo)
        {
            string? dirname = Path.GetDirectoryName(fileInfo.DirectoryName);
            if (dirname != null)
                Directory.CreateDirectory(dirname);

            using var f = File.Open(fileInfo.FullName, FileMode.Create);
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
            foreach (AbstractChunk abstractChunk in Chunks)
            {
                abstractChunk.Save(stream);
            }
        }

        private void PrepareForSave()
        {
            List<AbstractChunk> orderedChunks =
            [
                Chunks.SpliceOrDefault<IHDR>(),
                Chunks.SpliceOrDefault("cHRM"),
                Chunks.SpliceOrDefault("cICP"),
                Chunks.SpliceOrDefault("gAMA"),
                Chunks.SpliceOrDefault("iCCP"),
                Chunks.SpliceOrDefault("mDCV"),
                Chunks.SpliceOrDefault("cLLI"),
                Chunks.SpliceOrDefault("sBIT"),
                Chunks.SpliceOrDefault("sRGB"),
                Chunks.SpliceOrDefault<PLTE>(),
                Chunks.SpliceOrDefault<tRNS>(),
                Chunks.SpliceOrDefault<bKGD>(),
                Chunks.SpliceOrDefault("hIST"),
                Chunks.SpliceOrDefault<acTL>() ?? new Chunk<acTL>( new acTL {NumFrames = 1} ),
                Chunks.SpliceOrDefault("eXIF"),
                Chunks.SpliceOrDefault<fcTL>(),
                Chunks.SpliceOrDefault("pHYs"),
                Chunks.SpliceOrDefault("sPLT"),
                Chunks.SpliceOrDefault<IDAT>(),

                //all other fcTL and IDAT, but IDAT as fDAT.

                Chunks.SpliceOrDefault<IEND>(),
            ];
            orderedChunks = orderedChunks.Where(x => x != null).ToList();

            foreach (Chunk<IDAT> idat in Chunks.OfType<IDAT>())
            {
                Chunks[Chunks.IndexOf(idat)] = new Chunk<fdAT>(new fdAT
                {
                    FrameData = idat.Data.Data
                });
            }

            orderedChunks.InsertRange(orderedChunks.Count - 1, Chunks);

            UInt32 id = 0;
            foreach ((Chunk<fdAT>? fdat, Chunk<fcTL>? fctl) in orderedChunks.OfType<fdAT, fcTL>())
            {
                fdat?.Data.SequenceNumber = id++;
                fctl?.Data.SequenceNumber = id++;
            }

            Chunk<acTL> actl = orderedChunks.First<acTL>();
            actl.Data.NumFrames = (UInt32)orderedChunks.OfType<fcTL>().Count();
            if (actl.Data.NumFrames < 2)
                orderedChunks.Remove(actl);

            if (StripDecoration)
            {
                //Safe to copy-flag = true, so only meta data.
                foreach (AbstractChunk chunk in Chunks.Where(c => char.IsLower(c.Start.Name[^1])))
                    orderedChunks.Remove(chunk);
            }

            Chunks = orderedChunks;
        }

        protected virtual void InsertMissingFctl()
        {
            Chunk<IHDR> ihdr = Chunks.First<IHDR>();
            AbstractChunk? fctl = null;
            foreach (AbstractChunk chunk in Chunks.ToList())
            {
                switch (chunk.OData)
                {
                    case fcTL:
                        fctl = chunk;
                        break;
                    case IDAT or fdAT when fctl != null:
                        fctl = null;
                        break;
                    case IDAT or fdAT:
                        Chunks.Insert(Chunks.IndexOf(chunk), new Chunk<fcTL>(new fcTL
                        {
                            Width = ihdr.Data.Width,//should be from IHDR of Image
                            Height = ihdr.Data.Height,//should be from IHDR of Image
                            DelayNum = defaultNum,
                            DelayDen = defaultDen,
                            DisposeOp = DisposeOp.ApngDisposeOpBackground,
                            BlendOp = BlendOp.ApngBlendOpSource
                        }));
                        break;
                }
            }
        }
    }

    public abstract class AbstractChunk
    {
        public ChunkStart Start;
        public UInt32 CRC;
        public abstract object OData { get; }

        public abstract void Save(Stream stream);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ChunkStart
    {
        public UInt32 Size;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
        public string Name;

        internal ChunkStart(Stream stream)
        {
            Span<byte> chunkstart = new byte[Marshal.SizeOf(typeof(ChunkStart))];
            int read = stream.Read(chunkstart);
            if (read < chunkstart.Length)
                throw new FormatException($"Corrupt PNG file loaded. Malformed chunk start at {stream.Position}.");

            Load(chunkstart);
        }

        internal ChunkStart(Span<byte> data) => Load(data);

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

                value = Activator.CreateInstance(type);

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

        public static byte[] Write(this object obj)
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
                            data.AddRange(from object? o in (fieldInfo.GetValue(obj) as IEnumerable)! select o!.Write());
                    }
                    else
                        data.Add(fieldInfo.GetValue(obj)!.Write());
                }

                return data.SelectMany(x => x).ToArray();
            }

            byte[] valueBytes = Type.GetTypeCode(type) switch
            {
                TypeCode.UInt16 => BitConverter.GetBytes((UInt16)obj),
                TypeCode.UInt32 => BitConverter.GetBytes((UInt32)obj),
                TypeCode.UInt64 => BitConverter.GetBytes((UInt64)obj),
                TypeCode.Int16 => BitConverter.GetBytes((Int16)obj),
                TypeCode.Int32 => BitConverter.GetBytes((Int32)obj),
                TypeCode.Int64 => BitConverter.GetBytes((Int64)obj),
                TypeCode.Byte => [(byte)obj],
                _ => throw new ArgumentException($"Cant handle {type.Name}.")
            };
            valueBytes.Reverse();
            return valueBytes;
        }
    }

    public class Chunk<T> : AbstractChunk where T : struct, IChunkData
    {
        public override object OData => Data;
        public T Data;

        private Chunk() { }

        public Chunk(T data)
        {
            Data = data;
            Start = new ChunkStart(typeof(T).Name);
        }

        public override void Save(Stream stream)
        {
            byte[] name = Start.Name.Write();
            byte[] data = Data.Write();
            stream.Write((Start.Size = (UInt32)data.Length).Write());
            stream.Write(name);
            stream.Write(data);
            stream.Write((CRC = Crc.Get([.. name, .. data])).Write());
        }
    }

    public interface IChunkData
    {

    }

    public enum ColorType : byte
    {
        /// <summary>
        /// Allows <see cref="Config.BitDepth"/> of 1, 2, 4, 8 and 16
        /// </summary>
        Greyscale = 0,
        /// <summary>
        /// Allows <see cref="Config.BitDepth"/> of 8 and 16
        /// </summary>
        Truecolor = 2,
        /// <summary>
        /// Allows <see cref="Config.BitDepth"/> of 1, 2, 4 and 8
        /// </summary>
        IndexedColor = 3,
        /// <summary>
        /// Allows <see cref="Config.BitDepth"/> of 8 and 16
        /// </summary>
        GreyscaleWithAlpha = 4,
        /// <summary>
        /// Allows <see cref="Config.BitDepth"/> of 8 and 16
        /// </summary>
        TruecolorWithAlpha = 6
    }

    public struct IHDR : IChunkData
    {
        public uint Width;
        public uint Height;
        public byte BitDepth;
        public ColorType ColorType;
        public byte CompressionMethod;
        public byte FilterMethod;
        public byte InterlaceMethod;
    }

    public struct Color
    {
        public byte R;
        public byte G;
        public byte B;
    }

    public struct PLTE : IChunkData
    {
        public Color[] Colors;
    }

    public struct IDAT : IChunkData
    {
        public byte[] Data;
    }

    public struct IEND : IChunkData
    {
    }

    public struct acTL : IChunkData
    {
        public UInt32 NumFrames;
        public UInt32 NumPlays;
    }

    public struct fcTL : IChunkData
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

    public struct fdAT : IChunkData
    {
        public UInt32 SequenceNumber;
        public byte[] FrameData;
    }


    public struct tEXt : IChunkData
    {
        private string text;

        public string Keyword => text.Split((char)0)[0];
        public string Text => text.Split((char)0)[1];
    }

    public struct tRNS : IChunkData
    {
        public byte[] ColorData;
    }

    public struct bKGD : IChunkData
    {
        public byte[] ColorData;
    }

    public struct Unknown : IChunkData
    {
        public byte[] Data;
    }
}