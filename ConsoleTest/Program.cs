using System.Dynamic;
using Anti;
using Microsoft.VisualBasic;

Console.WriteLine("Hello, World!");

string[] files = [
    @"H:\Projects\pebble-rorschach-v3\resources\images\H1.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\H1M10.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\H10.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\H10H1_1.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\H10H1_2.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\H10H1M10M1.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\H10M1.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\icon.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\IH1.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\IH1M10.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\IH10.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\IH10H1_1.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\IH10H1_2.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\IH10H1M10M1.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\IH10M1.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\IM1.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\IM10.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\IM10M1_1.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\IM10M1_2.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\M1.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\M10.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\M10M1_1.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\M10M1_2.png",
    @"H:\Projects\pebble-rorschach-v3\resources\images\icon.png"
    ];


foreach (string s in files)
{
    new Png(s) { StripDecoration = true }.Save(s);
}