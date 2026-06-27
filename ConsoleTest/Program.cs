using AntiPebbleNG;
using System.Text.RegularExpressions;

(string name, Regex regex, bool withBlank)[] patterns = [
    ("H10H1M10M1", new Regex(@"\d+_\d+_\d+_\d+"), false),
    ("H10H1_1", new Regex(@"\d+h10_\d+h1_1"), true),
    ("H10H1_2", new Regex(@"\d+h10_\d+h1_2"), true),
    ("H10M1", new Regex(@"\d+h10_\d+m1"), true),
    ("H1M10", new Regex(@"\d+h1_\d+m10"), false),
    ("M10M1_1", new Regex(@"\d+m10_\d+m1_1"), false),
    ("M10M1_2", new Regex(@"\d+m10_\d+m1_2"), false),
    ("H10", new Regex(@"\d+h10"), false),
    ("H1", new Regex(@"\d+h1"), false),
    ("M10", new Regex(@"\d+m10"), false),
    ("M1", new Regex(@"\d+m1"), false)
];

List<string> defines = [];

foreach (IGrouping<string, string> group in Directory.GetFiles(@"H:\Projects\pebble-rorschach-v3\resources\images\numbers").GroupBy(x => patterns.First(p => p.regex.IsMatch(x)).name))
{
    int id = 0;
    bool withBlank = patterns.First(p => p.name == group.Key).withBlank;

    if (withBlank)
        defines.Add($"#define FRAME_{group.Key}_EMPTY {id++}");

    List<Png> images = [];

    foreach (string s in group)
    {
        defines.Add($"#define FRAME_{Path.GetFileNameWithoutExtension(s).ToUpper()} {id++}");
        images.Add(new Png(s));
    }

    Png firstImg;

    if (withBlank)
        firstImg = Png.Create(images[0].Width, images[0].Height, ColorType.Greyscale, 2, Colors.Black);
    else
    {
        firstImg = images[0];
        images = images[1..];
    }

    firstImg.DefaultDelayInSeconds = 1;
    firstImg.StripDecoration = true;

    foreach (Png image in images)
        firstImg.AddFrame(image);

    firstImg.Save(group.Key + ".png");

    foreach (Png image in images)
    {
        using IDisposable _ = image.Unlock(out Image pixels);
        pixels.FlipVertical();
    }

    firstImg.Save("I" + group.Key + ".png");
}

File.WriteAllLines("defines.h", defines);