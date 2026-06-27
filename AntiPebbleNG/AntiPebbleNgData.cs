using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AntiPebbleNG;

internal static class AntiPebbleNgData
{
    internal static readonly Dictionary<string, Type> ChunkTypesByName = typeof(AbstractChunk).Assembly.GetTypes()
        .Select(x => (x, CustomAttributeExtensions.GetCustomAttribute<ChunkAttribute>((MemberInfo)x))).Where(x => x.Item2 != null)
        .ToDictionary(x => x.Item2.Name, x => x.x);

    internal static readonly Dictionary<ColorType, Dictionary<byte, Type>> ColorByConfiguration = typeof(IColor).Assembly.GetTypes()
        .Select(x => (x, x.GetCustomAttribute<ColorAttribute>())).Where(x => x.Item2 != null)
        .GroupBy(x => x.Item2.ColorType).ToDictionary(x => x.Key, x => x.ToDictionary(y => y.Item2.BitDepth, y => y.x));
}