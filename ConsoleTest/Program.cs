using AntiPng;
using System.Text.RegularExpressions;

(string name, Regex regex)[] patterns = [
    ("H10H1M10M1", new Regex(@"\d+_\d+_\d+_\d+")),
    ("H10H1_1", new Regex(@"\d+h10_\d+h1_1")),
    ("H10H1_2", new Regex(@"\d+h10_\d+h1_2")),
    ("H10M1", new Regex(@"\d+h10_\d+m1")),
    ("H1M10", new Regex(@"\d+h1_\d+m10")),
    ("M10M1_1", new Regex(@"\d+m10_\d+m1_1")),
    ("M10M1_2", new Regex(@"\d+m10_\d+m1_2")),
    ("H10", new Regex(@"\d+h10")),
    ("H1", new Regex(@"\d+h1")),
    ("M10", new Regex(@"\d+m10")),
    ("M1", new Regex(@"\d+m1"))
];

List<string> defines = [];

foreach (IGrouping<string, string> group in Directory.GetFiles(@"H:\Projects\pebble-rorschach-v3\resources\images\numbers").GroupBy(x => patterns.First(p => p.regex.IsMatch(x)).name))
{
    int id = 0;

    defines.Add($"#define FRAME_{group.Key}_EMPTY 0");

    List<Png> images = [];

    foreach (string s in group)
    {
        id++;
        defines.Add($"#define FRAME_{Path.GetFileNameWithoutExtension(s).ToUpper()} {id}");
        images.Add(new Png(s));
    }


    Png emptyImg = new(images.First().Width, images.First().Height, ColorType.Greyscale, 2, Colors.Black)
    {
        CheckCrc = false,
        DefaultDelayInSeconds = 1,
        StripDecoration = true
    };

    foreach (Png image in images)
        emptyImg.AddFrame(image);

    emptyImg.Save(group.Key + ".png");

    foreach (Png image in images)
        using (var _ = image.Unlock(out Image pixels))
        {
            pixels.FlipVertical();
        }

    emptyImg.Save("I" + group.Key + ".png");
}

File.WriteAllLines("defines.h", defines);