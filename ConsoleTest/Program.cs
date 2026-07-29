using AntiPebbleNG;
using System.Dynamic;
using System.Text.RegularExpressions;

List<string> _files = [
    "_1.png", 
    "_2.png", 
    "_3.png"
];

List<Png> images = [];

foreach (string s in _files)
    images.Add(new Png(s));

Png firstImg;

firstImg = images[0];
images = images[1..];

firstImg.DefaultDelayInSeconds = 1;
firstImg.StripDecoration = true;

foreach (Png image in images)
    firstImg.AddFrame(image);

firstImg.Save("apng.png");

//var a = new Png(@"H:\Projects\pebble-rorschach-v3\resources\images\M10M1_2.png");
//using var _12 = a.Unlock(out Image img, 27);

//var targetPng = Png.Create(a.Width, a.Height, a.ColorType, a.BitDepth, a.Palette, img.GetPixel(0,0));
//{
//    using var _2 = targetPng.Unlock(out Image target);

//    for (int y = 0; y < img.Height; y++)
//        for (int x = 0; x < img.Width; x++)
//            target.SetPixel(x, y, img[x, y]);
//}
//targetPng.Save("FRAME_M44.png");

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

//    firstImg.Save(@"H:\Projects\pebble-rorschach-v3\resources\images\" + group.Key + ".png");

//    foreach (Png image in images)
//    {
//        using IDisposable _ = image.Unlock(out Image pixels);
//        pixels.FlipVertical();
//    }
//    {
//        using IDisposable _ = firstImg.Unlock(out Image pixels);
//        pixels.FlipVertical();
//    }

//    firstImg.Save(@"H:\Projects\pebble-rorschach-v3\resources\images\I" + group.Key + ".png");
//}

//File.WriteAllLines(@"H:\Projects\pebble-rorschach-v3\resources\images\defines.h", defines);

string[] files = Directory.GetFiles(@"H:\Projects\pebble-tattoo\resources\images").Where(x => x.EndsWith("#emery.png") || x.EndsWith("#gabbro.png")).ToArray();

foreach (string file in files)
{
    var png = new Png(file);

    ColorRgba8[] grayPallet =
    [
        new(0, 0, 0, 0),
        new(0, 0, 0, 255),
        new(0x55, 0x55, 0x55, 255),
        new(0xAA, 0xAA, 0xAA, 255),
        new(0xFF, 0xFF, 0xFF, 255),
    ];

    var targetPng = Png.Create(png.Width, png.Height, ColorType.IndexedColor, 4, grayPallet, grayPallet[1]);
    {
        using var _1 = png.Unlock(out Image source);
        using var _2 = targetPng.Unlock(out Image target);

        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
                target.SetPixel(x, y, source[x, y]);
    }
    targetPng.Save(file.Replace("#", "~"));
    File.Delete(file);
}

files = Directory.GetFiles(@"H:\Projects\pebble-tattoo\resources\images").Where(x => x.EndsWith("#basalt.png") || x.EndsWith("#chalk.png")).ToArray();

foreach (string file in files)
{
    var png = new Png(file);

    ColorRgba8[] grayPallet =
    [
        new(0, 0, 0, 0),
        new(0, 0, 0, 255),
        new(0x55, 0x55, 0x55, 255),
        new(0xAA, 0xAA, 0xAA, 255),
        new(0xFF, 0xFF, 0xFF, 255),
    ];

    var targetPng = Png.Create(png.Width, png.Height, ColorType.IndexedColor, 4, grayPallet, grayPallet[1]);
    {
        using var _1 = png.Unlock(out Image source);
        using var _2 = targetPng.Unlock(out Image target);

        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
                target.SetPixel(x, y, source[x, y]);
    }
    targetPng.Save(file.Replace("#", "~"));
    File.Delete(file);
}

files = Directory.GetFiles(@"H:\Projects\pebble-tattoo\resources\images").Where(x => x.EndsWith("#bw.png")).ToArray();

foreach (string file in files)
{
    var png = new Png(file);

    ColorRgba8[] grayPallet =
    [
        new(0, 0, 0, 0),
        new(0, 0, 0, 255),
        new(0xFF, 0xFF, 0xFF, 255),
    ];

    var targetPng = Png.Create(png.Width, png.Height, ColorType.IndexedColor, 2, grayPallet, grayPallet[1]);
    {
        using var _1 = png.Unlock(out Image source);
        using var _2 = targetPng.Unlock(out Image target);

        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
                target.SetPixel(x, y, source[x, y]);
    }
    targetPng.Save(file.Replace("#", "~"));
    File.Delete(file);
}