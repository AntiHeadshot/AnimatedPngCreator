using System.Drawing;
using NUnit.Framework;
using AntiPebbleNG;
using Image = AntiPebbleNG.Image;

namespace AntiPebbleNgTests;

[TestFixture]
class PngSuiteTests
{
    public static readonly string[] PngSuiteFiles =
    [
        "basi0g01","basi0g02","basi0g04","basi0g08","basi0g16",
    "basi2c08","basi2c16","basi3p01","basi3p02","basi3p04","basi3p08",
    "basi4a08","basi4a16","basi6a08","basi6a16",
    "basn0g01","basn0g02","basn0g04","basn0g08","basn0g16",
    "basn2c08","basn2c16","basn3p01","basn3p02","basn3p04","basn3p08",
    "basn4a08","basn4a16","basn6a08","basn6a16",
    "bgai4a08","bgai4a16","bgan6a08","bgan6a16",
    "bgbn4a08","bggn4a16","bgwn6a08","bgyn6a16",
    "ccwn2c08","ccwn3p08",
    "cdfn2c08","cdhn2c08","cdsn2c08","cdun2c08",
    "ch1n3p04","ch2n3p08",
    "cm0n0g04","cm7n0g04","cm9n0g04",
    "cs3n2c16","cs3n3p08","cs5n2c08","cs5n3p08","cs8n2c08","cs8n3p08",
    "ct0n0g04","ct1n0g04","cten0g04","ctfn0g04","ctgn0g04","cthn0g04","ctjn0g04","ctzn0g04",
    "exif2c08",
    "f00n0g08","f00n2c08","f01n0g08","f01n2c08","f02n0g08","f02n2c08",
    "f03n0g08","f03n2c08","f04n0g08","f04n2c08","f99n0g04",
    "g03n0g16","g03n2c08","g03n3p04",
    "g04n0g16","g04n2c08","g04n3p04",
    "g05n0g16","g05n2c08","g05n3p04",
    "g07n0g16","g07n2c08","g07n3p04",
    "g10n0g16","g10n2c08","g10n3p04",
    "g25n0g16","g25n2c08","g25n3p04",
    "oi1n0g16","oi1n2c16","oi2n0g16","oi2n2c16",
    "oi4n0g16","oi4n2c16","oi9n0g16","oi9n2c16",
    "pp0n2c16","pp0n6a08",
    "ps1n0g08","ps1n2c16","ps2n0g08","ps2n2c16",
    "s01i3p01","s01n3p01","s02i3p01","s02n3p01","s03i3p01","s03n3p01",
    "s04i3p01","s04n3p01","s05i3p02","s05n3p02","s06i3p02","s06n3p02",
    "s07i3p02","s07n3p02","s08i3p02","s08n3p02","s09i3p02","s09n3p02",
    "s32i3p04","s32n3p04","s33i3p04","s33n3p04","s34i3p04","s34n3p04",
    "s35i3p04","s35n3p04","s36i3p04","s36n3p04","s37i3p04","s37n3p04",
    "s38i3p04","s38n3p04","s39i3p04","s39n3p04","s40i3p04","s40n3p04",
    "tbbn0g04","tbbn2c16","tbbn3p08","tbgn2c16","tbgn3p08","tbrn2c08",
    "tbwn0g16","tbwn3p08","tbyn3p08","tm3n3p02",
    "tp0n0g08","tp0n2c08","tp0n3p08","tp1n3p08",
    "xc1n0g08","xc9n2c08","xcrn0g04","xcsn0g01",
    "xd0n2c08","xd3n2c08","xd9n2c08","xdtn0g01",
    "xhdn0g08","xlfn0g04","xs1n0g01","xs2n0g01","xs4n0g01","xs7n0g01",
    "z00n2c08","z03n2c08","z06n2c08","z09n2c08"
    ];

    private const string SuitePath = "PngSuite-2017jul19";

    [TestCaseSource(typeof(PngSuiteTests), nameof(PngSuiteFiles))]
    public void PngSuite_File_ShouldRoundtripCorrectly(string name)
    {
        string file = Path.Combine(SuitePath, name + ".png");
        int exceptionInPng = 0;
        try
        {
            var png = new Png(file);

            exceptionInPng = 1;

            using (var _ = png.Unlock(out Image image))
            {

            }

            byte[] encoded = png.Save();

            exceptionInPng = 2;

            using var ms = new MemoryStream(encoded);

            using var roundtrip = new Bitmap(ms);

            using var original = new Bitmap(file);

            exceptionInPng = 3;

            AreEqual(original, roundtrip, name);
        }
        catch (Exception exp)
        {
            if (exceptionInPng == 1)
                Assert.Fail("Exception in Png but in Unlock.");
            else if (exceptionInPng == 2)
                Assert.Fail("Exception in System.Drawing, but not in Png.");
            else if (exceptionInPng == 3)
                Assert.Fail(exp.Message);
            else
                try
                {
                    using var original = new Bitmap(file);
                    Assert.Fail("Exception in Png, but not in System.Drawing.");
                }
                catch (Exception exd)
                {
                    // ignored
                }
        }
    }

    private static void AreEqual(Bitmap expected, Bitmap actual, string name)
    {
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            throw new Exception(
                $"PNG‑Suite test failed for '{name}'\n" +
                $"{PngSuiteInfo.Describe(name)}\n\n" +
                $"Image size mismatch:\n" +
                $"  Expected: {expected.Width}×{expected.Height}\n" +
                $"  Actual:   {actual.Width}×{actual.Height}"
            );
        }

        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                var e = expected.GetPixel(x, y);
                var a = actual.GetPixel(x, y);

                if (e.ToArgb() != a.ToArgb())
                {
                    throw new Exception(
                        $"PNG‑Suite test failed for '{name}'\n" +
                        $"{PngSuiteInfo.Describe(name)}\n\n" +
                        $"Pixel mismatch at ({x},{y}):\n" +
                        $"  Expected: ARGB {e.ToArgb():X8} ({e})\n" +
                        $"  Actual:   ARGB {a.ToArgb():X8} ({a})\n\n" +
                        $"This test checks: {PngSuiteInfo.Describe(name)}"
                    );
                }
            }
        }
    }
}

public static class PngSuiteInfo
{
    public static string Describe(string name)
    {
        string prefix = name.Substring(0, 3);

        return prefix switch
        {
            "bas" => "Basic format test — verifies core PNG decoding.",
            "bg" => "Background color test — checks bKGD handling.",
            "ccw" => "Chromaticity test — validates cHRM chunk.",
            "cdf" => "Physical pixel dimensions — pHYs chunk (flat pixels).",
            "cdh" => "Physical pixel dimensions — pHYs chunk (tall pixels).",
            "cds" => "Physical pixel dimensions — pHYs chunk (square pixels).",
            "cdu" => "Physical pixel dimensions — pHYs with unit specifier.",
            "ch1" => "Histogram test — 15‑color histogram.",
            "ch2" => "Histogram test — 256‑color histogram.",
            "cm0" => "Time chunk — modified 01‑Jan‑2000.",
            "cm7" => "Time chunk — modified 01‑Jan‑1970.",
            "cm9" => "Time chunk — modified 31‑Dec‑1999.",
            "cs3" => "Significant bits — 3 significant bits.",
            "cs5" => "Significant bits — 5 significant bits.",
            "cs8" => "Significant bits — 8 significant bits.",
            "ct0" => "Text chunk — no textual data.",
            "ct1" => "Text chunk — with textual data.",
            "cte" => "UTF‑8 text — English.",
            "ctf" => "UTF‑8 text — Finnish.",
            "ctg" => "UTF‑8 text — Greek.",
            "cth" => "UTF‑8 text — Hindi.",
            "ctj" => "UTF‑8 text — Japanese.",
            "ctz" => "Compressed text.",
            "exi" => "EXIF chunk test.",
            "f00" => "Filtering test — no filtering.",
            "f01" => "Filtering test — sub filter.",
            "f02" => "Filtering test — up filter.",
            "f03" => "Filtering test — average filter.",
            "f04" => "Filtering test — Paeth filter.",
            "f99" => "Filtering test — invalid filter type.",
            "g03" => "Gamma test — file gamma 0.35.",
            "g04" => "Gamma test — file gamma 0.45.",
            "g05" => "Gamma test — file gamma 0.55.",
            "g07" => "Gamma test — file gamma 0.70.",
            "g10" => "Gamma test — file gamma 1.00.",
            "g25" => "Gamma test — file gamma 2.50.",
            "oi1" => "Chunk ordering — 1 IDAT chunk.",
            "oi2" => "Chunk ordering — 2 IDAT chunks.",
            "oi4" => "Chunk ordering — 4 IDAT chunks.",
            "oi9" => "Chunk ordering — many tiny IDAT chunks.",
            "pp0" => "Palette chunk — normal palette.",
            "ps1" => "Suggested palette — type 1.",
            "ps2" => "Suggested palette — type 2.",
            "s01" => "Odd size test — 1×1 image.",
            "s02" => "Odd size test — 2×2 image.",
            "s03" => "Odd size test — 3×3 image.",
            "s32" => "Odd size test — 32×32 image.",
            "s33" => "Odd size test — 33×33 image.",
            "s34" => "Odd size test — 34×34 image.",
            "s35" => "Odd size test — 35×35 image.",
            "s36" => "Odd size test — 36×36 image.",
            "s37" => "Odd size test — 37×37 image.",
            "s38" => "Odd size test — 38×38 image.",
            "s39" => "Odd size test — 39×39 image.",
            "s40" => "Odd size test — 40×40 image.",
            "tbb" => "Transparency — transparent + black background.",
            "tbg" => "Transparency — transparent + gray background.",
            "tbw" => "Transparency — transparent + white background.",
            "tby" => "Transparency — transparent + yellow background.",
            "tm3" => "Transparency — multiple transparency levels.",
            "tp0" => "Transparency — not transparent (reference).",
            "tp1" => "Transparency — transparent, no background chunk.",
            "xc1" => "Corrupted — invalid color type 1.",
            "xc9" => "Corrupted — invalid color type 9.",
            "xcr" => "Corrupted — extra CR bytes.",
            "xcs" => "Corrupted — invalid IDAT checksum.",
            "xd0" => "Corrupted — bit depth 0.",
            "xd3" => "Corrupted — bit depth 3.",
            "xd9" => "Corrupted — bit depth 99.",
            "xdt" => "Corrupted — missing IDAT chunk.",
            "xhd" => "Corrupted — invalid IHDR checksum.",
            "xlf" => "Corrupted — extra LF bytes.",
            "xs1" => "Corrupted — signature byte 1 MSB reset.",
            "xs2" => "Corrupted — signature byte 2 is 'Q'.",
            "xs4" => "Corrupted — signature byte 4 lowercase.",
            "xs7" => "Corrupted — signature byte 7 is space.",
            "z00" => "Zlib compression level 0.",
            "z03" => "Zlib compression level 3.",
            "z06" => "Zlib compression level 6 (default).",
            "z09" => "Zlib compression level 9.",
            _ => "Unknown PNG‑Suite test category."
        };
    }
}
