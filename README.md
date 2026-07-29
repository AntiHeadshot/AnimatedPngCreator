Example

``` csharp
List<string> files = [
    "_1.png", 
    "_2.png", 
    "_3.png"
];

List<Png> images = [];

foreach (string s in files)
    images.Add(new Png(s));

Png firstImg;

firstImg = images[0];
images = images[1..];

firstImg.DefaultDelayInSeconds = 1;
firstImg.StripDecoration = true;

foreach (Png image in images)
    firstImg.AddFrame(image);

firstImg.Save("apng.png");
```