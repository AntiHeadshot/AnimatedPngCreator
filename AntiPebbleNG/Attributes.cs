using System;

namespace AntiPebbleNG;

[AttributeUsage(AttributeTargets.Class)]
public class ChunkAttribute(string name) : Attribute
{
    public readonly string Name = name;
}

[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public class ColorAttribute(ColorType colorType, byte bitDepth) : Attribute
{
    public readonly ColorType ColorType = colorType;
    public readonly byte BitDepth = bitDepth;
}