using System;

namespace AntiPebbleNG;

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