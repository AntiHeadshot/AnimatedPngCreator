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
        .SelectMany(x => x.GetCustomAttributes<ColorAttribute>().Select(a => (x, a))).Where(x => x.a != null)
        .GroupBy(x => x.a.ColorType).ToDictionary(x => x.Key, x => x.ToDictionary(y => y.a.BitDepth, y => y.x));
}