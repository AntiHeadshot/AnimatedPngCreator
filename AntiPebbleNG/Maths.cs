using System;

namespace AntiPebbleNG;

internal static class Maths
{
    internal static void ExpandFraction(float value, out ushort defaultNum, out ushort defaultDen)
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

        defaultNum = (ushort)n1;
        defaultDen = (ushort)d1;
    }
}