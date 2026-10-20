using ImageMagick;

namespace Orange.Core;

/// <summary>Image conversions and tools on top of Magick.NET (reads HEIC, AVIF, WebP, SVG, TIFF…).</summary>
public static class Images
{
    private static readonly Dictionary<string, MagickFormat> Formats = new()
    {
        ["jpg"] = MagickFormat.Jpeg,
        ["png"] = MagickFormat.Png,
        ["webp"] = MagickFormat.WebP,
        ["avif"] = MagickFormat.Avif,
        ["tiff"] = MagickFormat.Tiff,
        ["bmp"] = MagickFormat.Bmp,
        ["gif"] = MagickFormat.Gif,
        ["ico"] = MagickFormat.Ico,
        ["pdf"] = MagickFormat.Pdf,
    };

    public static IReadOnlyCollection<string> Targets => Formats.Keys;

    public static Task<string> ConvertAsync(string input, string ext) => Task.Run(() =>
    {
        var output = Paths.Output(input, ext);
        using var img = Load(input);

        // Formats without transparency get a white background instead of black.
        if (ext is "jpg" or "bmp" or "pdf")
        {
            img.BackgroundColor = MagickColors.White;
            img.Alpha(AlphaAction.Remove);
        }
        if (ext == "ico")
            img.Resize(new MagickGeometry("256x256>"));
        if (ext is "jpg" or "webp" or "avif")
            img.Quality = 90;

        img.Write(output, Formats[ext]);
        return output;
    });

    /// <summary>Applies an edit and saves next to the source, keeping the original format.</summary>
    public static Task<string> EditAsync(string input, string suffix, Action<MagickImage> edit) => Task.Run(() =>
    {
        var ext = Path.GetExtension(input).TrimStart('.').ToLowerInvariant();
        // Formats we can't write back (HEIC, SVG) are saved as PNG.
        if (ext is "heic" or "heif" or "svg") ext = "png";
        if (ext == "jpeg") ext = "jpg";
        if (ext == "tif") ext = "tiff";

        var output = Paths.Output(input, ext, suffix);
        using var img = Load(input);
        edit(img);
        img.Write(output);
        return output;
    });

    public static void Compress(MagickImage img)
    {
        img.Strip();
        img.Quality = 70;
        img.Settings.SetDefine(MagickFormat.Png, "compression-level", "9");
    }

    public static Task<string> ToPdfAsync(IReadOnlyList<string> inputs) => Task.Run(() =>
    {
        var output = Paths.Output(inputs[0], "pdf", inputs.Count > 1 ? "merged" : null);
        using var pages = new MagickImageCollection();
        foreach (var path in inputs)
        {
            var img = Load(path);
            img.BackgroundColor = MagickColors.White;
            img.Alpha(AlphaAction.Remove);
            pages.Add(img);
        }
        pages.Write(output, MagickFormat.Pdf);
        return output;
    });

    /// <summary>One-page PDF of an image in the temp folder (used when merging images with PDFs).</summary>
    public static string ToTempPdf(string input)
    {
        var output = Path.Combine(Path.GetTempPath(), $"orange-{Guid.NewGuid():N}.pdf");
        using var img = Load(input);
        img.BackgroundColor = MagickColors.White;
        img.Alpha(AlphaAction.Remove);
        img.Write(output, MagickFormat.Pdf);
        return output;
    }

    private static MagickImage Load(string path)
    {
        var img = new MagickImage(path);
        img.AutoOrient(); // respect the camera's EXIF rotation
        return img;
    }
}
