using System.Dynamic;
using AntiPebbleNG;
using System.Text.RegularExpressions;

//(string name, Regex regex, bool withBlank)[] patterns = [
//    ("H10H1M10M1", new Regex(@"\d+_\d+_\d+_\d+"), false),
//    ("H10H1_1", new Regex(@"\d+h10_\d+h1_1"), true),
//    ("H10H1_2", new Regex(@"\d+h10_\d+h1_2"), true),
//    ("H10M1", new Regex(@"\d+h10_\d+m1"), true),
//    ("H1M10", new Regex(@"\d+h1_\d+m10"), false),
//    ("M10M1_1", new Regex(@"\d+m10_\d+m1_1"), false),
//    ("M10M1_2", new Regex(@"\d+m10_\d+m1_2"), false),
//    ("H10", new Regex(@"\d+h10"), false),
//    ("H1", new Regex(@"\d+h1"), false),
//    ("M10", new Regex(@"\d+m10"), false),
//    ("M1", new Regex(@"\d+m1"), false)
//];

//List<string> defines = [];

//foreach (IGrouping<string, string> group in Directory.GetFiles(@"H:\Projects\pebble-rorschach-v3\resources\images\numbers").GroupBy(x => patterns.First(p => p.regex.IsMatch(x)).name))
//{
//    int id = 0;
//    bool withBlank = patterns.First(p => p.name == group.Key).withBlank;

//    if (withBlank)
//        defines.Add($"#define FRAME_{group.Key}_EMPTY {id++}");

//    List<Png> images = [];

//    foreach (string s in group)
//    {
//        defines.Add($"#define FRAME_{Path.GetFileNameWithoutExtension(s).ToUpper()} {id++}");
//        images.Add(new Png(s));
//    }

//    Png firstImg;

//    if (withBlank)
//        firstImg = Png.Create(images[0].Width, images[0].Height, ColorType.Greyscale, 2, fillColor: Colors.Black);
//    else
//    {
//        firstImg = images[0];
//        images = images[1..];
//    }

//    firstImg.DefaultDelayInSeconds = 1;
//    firstImg.StripDecoration = true;

//    foreach (Png image in images)
//        firstImg.AddFrame(image);

//    firstImg.Save(group.Key + ".png");

//    foreach (Png image in images)
//    {
//        using IDisposable _ = image.Unlock(out Image pixels);
//        pixels.FlipVertical();
//    }

//    firstImg.Save("I" + group.Key + ".png");
//}

//File.WriteAllLines("defines.h", defines);

string[] files =
[
    @"H:\Projects\pebble-tattoo\resources\images\0h.png",
    @"H:\Projects\pebble-tattoo\resources\images\0h1.png",
    @"H:\Projects\pebble-tattoo\resources\images\0m1.png",
    @"H:\Projects\pebble-tattoo\resources\images\0m10.png",
    @"H:\Projects\pebble-tattoo\resources\images\1h.png",
    @"H:\Projects\pebble-tattoo\resources\images\1h1.png",
    @"H:\Projects\pebble-tattoo\resources\images\1h10.png",
    @"H:\Projects\pebble-tattoo\resources\images\1m1.png",
    @"H:\Projects\pebble-tattoo\resources\images\1m10.png",
    @"H:\Projects\pebble-tattoo\resources\images\2h.png",
    @"H:\Projects\pebble-tattoo\resources\images\2h1.png",
    @"H:\Projects\pebble-tattoo\resources\images\2h10.png",
    @"H:\Projects\pebble-tattoo\resources\images\2m1.png",
    @"H:\Projects\pebble-tattoo\resources\images\2m10.png",
    @"H:\Projects\pebble-tattoo\resources\images\3h.png",
    @"H:\Projects\pebble-tattoo\resources\images\3h1.png",
    @"H:\Projects\pebble-tattoo\resources\images\3m1.png",
    @"H:\Projects\pebble-tattoo\resources\images\3m10.png",
    @"H:\Projects\pebble-tattoo\resources\images\4h.png",
    @"H:\Projects\pebble-tattoo\resources\images\4h1.png",
    @"H:\Projects\pebble-tattoo\resources\images\4m1.png",
    @"H:\Projects\pebble-tattoo\resources\images\4m10.png",
    @"H:\Projects\pebble-tattoo\resources\images\5h.png",
    @"H:\Projects\pebble-tattoo\resources\images\5h1.png",
    @"H:\Projects\pebble-tattoo\resources\images\5m1.png",
    @"H:\Projects\pebble-tattoo\resources\images\5m10.png",
    @"H:\Projects\pebble-tattoo\resources\images\6h.png",
    @"H:\Projects\pebble-tattoo\resources\images\6h1.png",
    @"H:\Projects\pebble-tattoo\resources\images\6m1.png",
    @"H:\Projects\pebble-tattoo\resources\images\7h.png",
    @"H:\Projects\pebble-tattoo\resources\images\7h1.png",
    @"H:\Projects\pebble-tattoo\resources\images\7m1.png",
    @"H:\Projects\pebble-tattoo\resources\images\8h.png",
    @"H:\Projects\pebble-tattoo\resources\images\8h1.png",
    @"H:\Projects\pebble-tattoo\resources\images\8m1.png",
    @"H:\Projects\pebble-tattoo\resources\images\9h.png",
    @"H:\Projects\pebble-tattoo\resources\images\9h1.png",
    @"H:\Projects\pebble-tattoo\resources\images\9m1.png"
];

foreach (string file in files)
{
    var png = new Png(file);
    var targetPng = Png.Create(png.Width, png.Height, ColorType.IndexedColor, 4, [
        new ColorRgba8(0, 0, 0, 0),
        new ColorRgba8(0, 0, 0, 255),
        new ColorRgba8(0x55, 0x55, 0x55, 255),
        new ColorRgba8(0xAA, 0xAA, 0xAA, 255),
        new ColorRgba8(0xFF, 0xFF, 0xFF, 255),
    ], new ColorIndexed4(0));
    {
        using var _1 = png.Unlock(out Image source);
        using var _2 = targetPng.Unlock(out Image target);

        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
                target[x, y] = source[x, y];
    }
    targetPng.Save(file);
}