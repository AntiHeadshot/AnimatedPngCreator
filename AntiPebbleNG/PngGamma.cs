using System;

namespace AntiPebbleNG;

public static class PngGamma
{
    public static double DecodeGamma(uint gAMA)
    {
        if (gAMA == 0) return 1.0;
        return gAMA / 100000.0;
    }

    public static Func<ushort, ushort> GetGammaFunction(int bitDepth, double gamma, double displayExponent = 1)
    {
        int maxSample = (1 << bitDepth) - 1;
        double decodingExponent = 1.0 / (gamma * displayExponent);

        return value =>
        {
            double sample = value / (double)maxSample;
            double corrected = Math.Pow(sample, decodingExponent);
            int outVal = (int)(corrected * maxSample + 0.5);

            if (outVal < 0) outVal = 0;
            if (outVal > maxSample) outVal = maxSample;

            return (ushort)outVal;
        };
    }

    public static void ApplyGamma(Image image, int bitDepth, uint gAMA)
    {
        double gamma = DecodeGamma(gAMA);

        Func<ushort, ushort> gammaFunc = GetGammaFunction(bitDepth, gamma);

        for (int x = 0; x < image.Width; x++)
        for (int y = 0; y < image.Height; y++)
        {
            image.SetPixel(x,y, image[x,y].ApplyGamma(gammaFunc));
        }
    }
}