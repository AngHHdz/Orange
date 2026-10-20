using System.Runtime.InteropServices;
using System.Text;
using ImageMagick;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;
using PigDocument = UglyToad.PdfPig.PdfDocument;
using WinPdfDocument = Windows.Data.Pdf.PdfDocument;
using WinPdfPage = Windows.Data.Pdf.PdfPage;
using WinPdfRenderOptions = Windows.Data.Pdf.PdfPageRenderOptions;

namespace Orange.Core;

/// <summary>
/// PDF tools. Page editing (merge, split, rotate, stamp, passwords) uses PDFsharp;
/// rendering and OCR use the PDF renderer and OCR engine built into Windows;
/// text extraction uses PdfPig.
/// </summary>
public static class Pdf
{
    // ── Merge & split ─────────────────────────────────────────────────────

    /// <summary>Merges PDFs (and images, as pages) in file-name order into one PDF.</summary>
    public static Task<string> MergeAsync(IReadOnlyList<string> inputs) => Task.Run(() =>
    {
        var ordered = inputs.OrderBy(p => Path.GetFileName(p), NaturalComparer.Instance).ToList();
        var output = Paths.Output(ordered[0], "pdf", "merged");
        var temps = new List<string>();
        try
        {
            using var merged = new PdfDocument();
            foreach (var path in ordered)
            {
                var source = path;
                if (FileKinds.Of(path) == FileKind.Image)
                {
                    source = Images.ToTempPdf(path);
                    temps.Add(source);
                }
                using var doc = PdfReader.Open(source, PdfDocumentOpenMode.Import);
                foreach (var page in doc.Pages) merged.AddPage(page);
            }
            merged.Save(output);
            return output;
        }
        finally
        {
            foreach (var t in temps) TryDelete(t);
        }
    });

    /// <summary>Every page as its own PDF, in a new folder.</summary>
    public static Task<string> SplitAllAsync(string input) => Task.Run(() =>
    {
        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Import);
        var folder = Paths.Output(input, "", "pages");
        Directory.CreateDirectory(folder);
        var name = Path.GetFileNameWithoutExtension(input);
        int digits = doc.PageCount.ToString().Length;
        for (int i = 0; i < doc.PageCount; i++)
            SavePages(doc, [i], Path.Combine(folder, $"{name} - {(i + 1).ToString().PadLeft(digits, '0')}.pdf"));
        return folder;
    });

    /// <summary>"1-3, 4-6" → two PDFs: "file (1-3).pdf" and "file (4-6).pdf".</summary>
    public static Task<string> SplitRangesAsync(string input, string ranges) => Task.Run(() =>
    {
        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Import);
        string? last = null;
        foreach (var (label, pages) in PageRanges.Parse(ranges, doc.PageCount))
            SavePages(doc, pages, last = Paths.Output(input, "pdf", label));
        return last!;
    });

    /// <summary>"2, 5-7" → one PDF with just those pages.</summary>
    public static Task<string> ExtractAsync(string input, string ranges) => Task.Run(() =>
    {
        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Import);
        var pages = PageRanges.Parse(ranges, doc.PageCount).SelectMany(r => r.Pages).ToList();
        var output = Paths.Output(input, "pdf", "pages " + ranges.Replace(" ", ""));
        SavePages(doc, pages, output);
        return output;
    });

    private static void SavePages(PdfDocument source, IEnumerable<int> pages, string output)
    {
        using var part = new PdfDocument();
        foreach (var i in pages) part.AddPage(source.Pages[i]);
        part.Save(output);
    }

    // ── Page edits ────────────────────────────────────────────────────────

    public static Task<string> RotateAsync(string input) => Modify(input, "rotated", doc =>
    {
        foreach (var page in doc.Pages) page.Rotate = (page.Rotate + 90) % 360;
    });

    public static Task<string> NumberPagesAsync(string input) => Modify(input, "numbered", doc =>
    {
        var font = new XFont("Arial", 10);
        for (int i = 0; i < doc.PageCount; i++)
        {
            var page = doc.Pages[i];
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            gfx.DrawString($"{i + 1} / {doc.PageCount}", font, XBrushes.DimGray,
                new XRect(0, page.Height.Point - 30, page.Width.Point, 20), XStringFormats.Center);
        }
    });

    public static Task<string> WatermarkAsync(string input, string text) => Modify(input, "watermark", doc =>
    {
        var brush = new XSolidBrush(XColor.FromArgb(60, 220, 40, 40));
        foreach (var page in doc.Pages)
        {
            double w = page.Width.Point, h = page.Height.Point;
            var font = new XFont("Arial", Math.Min(w, h) / Math.Max(4, text.Length * 0.55), XFontStyleEx.Bold);
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            gfx.TranslateTransform(w / 2, h / 2);
            gfx.RotateTransform(-Math.Atan2(h, w) * 180 / Math.PI); // along the diagonal
            gfx.DrawString(text, font, brush, 0, 0, XStringFormats.Center);
        }
    });

    public static Task<string> ProtectAsync(string input, string password) => Modify(input, "protected", doc =>
    {
        doc.SecuritySettings.UserPassword = password;
        doc.SecuritySettings.OwnerPassword = password;
    });

    public static Task<string> UnlockAsync(string input, string password) => Task.Run(() =>
    {
        using var doc = PdfReader.Open(input, password, PdfDocumentOpenMode.Modify);
        doc.SecurityHandler.SetEncryptionToNoneAndResetPasswords();
        var output = Paths.Output(input, "pdf", "unlocked");
        doc.Save(output);
        return output;
    });

    /// <summary>Re-encodes the photos inside the PDF (the usual reason a PDF is big)
    /// and recompresses everything else. Text stays text.</summary>
    public static Task<string> CompressAsync(string input) => Task.Run(() =>
    {
        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        var seen = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        foreach (var page in doc.Pages)
            CompressImages(page.Resources, seen);

        doc.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
        doc.Options.CompressContentStreams = true;
        doc.Options.NoCompression = false;

        var output = Paths.Output(input, "pdf", "compressed");
        doc.Save(output);

        long before = new FileInfo(input).Length, after = new FileInfo(output).Length;
        if (after >= before)
        {
            File.Delete(output);
            throw new InvalidOperationException("This PDF is already as small as Orange can make it.");
        }
        JobRunner.Log($"     compressed {before / 1024} KB → {after / 1024} KB ({100 - after * 100 / before}% smaller)");
        return output;
    });

    private static void CompressImages(PdfDictionary? resources, HashSet<PdfDictionary> seen)
    {
        var xobjects = resources?.Elements.GetDictionary("/XObject");
        if (xobjects == null) return;

        foreach (var item in xobjects.Elements.Values)
        {
            if ((item as PdfSharp.Pdf.Advanced.PdfReference)?.Value is not PdfDictionary obj || !seen.Add(obj)) continue;

            var subtype = Name(obj, "/Subtype");
            if (subtype == "/Form")
            {
                CompressImages(obj.Elements.GetDictionary("/Resources"), seen); // images nested in forms
                continue;
            }
            // Only plain JPEG photos: their bytes are a complete .jpg we can safely re-encode.
            if (subtype != "/Image" || Name(obj, "/Filter") != "/DCTDecode" || obj.Stream == null) continue;
            if (Name(obj, "/ColorSpace") == "/DeviceCMYK" || obj.Elements.ContainsKey("/Decode")) continue;

            try
            {
                using var img = new MagickImage(obj.Stream.Value);
                if (img.ColorSpace == ColorSpace.CMYK) continue;
                bool gray = img.ChannelCount == 1;
                if (img.Width > 2000 || img.Height > 2000) img.Resize(new MagickGeometry("2000x2000>"));
                img.Strip();
                img.Quality = 60;
                img.ColorType = gray ? ColorType.Grayscale : ColorType.TrueColor; // must match the PDF's color space
                var smaller = img.ToByteArray(MagickFormat.Jpeg);
                if (smaller.Length >= obj.Stream.Value.Length) continue;

                obj.Stream.Value = smaller;
                obj.Elements.SetInteger("/Width", (int)img.Width);
                obj.Elements.SetInteger("/Height", (int)img.Height);
            }
            catch (MagickException) { /* leave odd images untouched */ }
        }
    }

    /// <summary>A name value such as "/DCTDecode". Also accepts a one-item array ([/DCTDecode]);
    /// anything else (references, longer arrays) gives null.</summary>
    private static string? Name(PdfDictionary dict, string key) => dict.Elements[key] switch
    {
        PdfName n => n.Value,
        PdfArray { Elements.Count: 1 } a => (a.Elements[0] as PdfName)?.Value,
        _ => null,
    };

    private static Task<string> Modify(string input, string suffix, Action<PdfDocument> edit) => Task.Run(() =>
    {
        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        edit(doc);
        var output = Paths.Output(input, "pdf", suffix);
        doc.Save(output);
        return output;
    });

    // ── Rendering (Windows built-in) ──────────────────────────────────────

    /// <summary>One image per page. A one-page PDF gives a single file; more pages give a folder.</summary>
    public static async Task<string> ToImagesAsync(string input, string ext)
    {
        var doc = await LoadWin(input);
        bool single = doc.PageCount == 1;
        var folder = single ? null : Paths.Output(input, "", $"{ext} pages");
        if (folder != null) Directory.CreateDirectory(folder);
        int digits = doc.PageCount.ToString().Length;

        for (uint i = 0; i < doc.PageCount; i++)
        {
            var target = single ? Paths.Output(input, ext)
                                : Path.Combine(folder!, $"Page {(i + 1).ToString().PadLeft(digits, '0')}.{ext}");
            using var page = doc.GetPage(i);
            using var stream = await Render(page, 2.0, jpeg: ext == "jpg");
            await using var file = File.Create(target);
            await stream.AsStreamForRead().CopyToAsync(file);
            if (single) return target;
        }
        return folder!;
    }

    /// <summary>Renders pages as PNGs into a temp folder (for PDF → PPTX). Returns the files and page size in points.</summary>
    public static async Task<(List<string> Files, double WidthPt, double HeightPt)> RenderToTempAsync(string input, string tempDir)
    {
        var doc = await LoadWin(input);
        var files = new List<string>();
        double w = 0, h = 0;
        for (uint i = 0; i < doc.PageCount; i++)
        {
            using var page = doc.GetPage(i);
            if (i == 0) { w = page.Size.Width * 72 / 96; h = page.Size.Height * 72 / 96; }
            var target = Path.Combine(tempDir, $"page{i:0000}.png");
            using var stream = await Render(page, 2.0, jpeg: false);
            await using var file = File.Create(target);
            await stream.AsStreamForRead().CopyToAsync(file);
            files.Add(target);
        }
        return (files, w, h);
    }

    private static async Task<WinPdfDocument> LoadWin(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        return await WinPdfDocument.LoadFromFileAsync(file);
    }

    private static async Task<InMemoryRandomAccessStream> Render(WinPdfPage page, double scale, bool jpeg)
    {
        var options = new WinPdfRenderOptions { DestinationWidth = (uint)(page.Size.Width * scale) };
        if (jpeg) options.BitmapEncoderId = BitmapEncoder.JpegEncoderId;
        var stream = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(stream, options);
        stream.Seek(0);
        return stream;
    }

    // ── Text ──────────────────────────────────────────────────────────────

    /// <summary>Text layer via PdfPig; scanned PDFs (no text layer) fall back to OCR.</summary>
    public static async Task<string> ToTextAsync(string input)
    {
        var text = await Task.Run(() =>
        {
            using var doc = PigDocument.Open(input);
            return string.Join("\n\n", doc.GetPages().Select(p => ContentOrderTextExtractor.GetText(p)));
        });

        if (text.Count(char.IsLetterOrDigit) < 20)
            return await OcrAsync(input); // probably a scan

        var output = Paths.Output(input, "txt");
        await File.WriteAllTextAsync(output, text, new UTF8Encoding(false));
        return output;
    }

    /// <summary>Reads the text in a scanned PDF or a photo with Windows OCR (in your Windows display languages).</summary>
    public static async Task<string> OcrAsync(string input)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? throw new InvalidOperationException("No OCR language is installed. Add one in Settings → Time & language → Language.");

        var sb = new StringBuilder();
        if (FileKinds.Of(input) == FileKind.Pdf)
        {
            var doc = await LoadWin(input);
            for (uint i = 0; i < doc.PageCount; i++)
            {
                using var page = doc.GetPage(i);
                double scale = Math.Min(3.0, OcrEngine.MaxImageDimension / Math.Max(page.Size.Width, page.Size.Height));
                using var stream = await Render(page, scale, jpeg: false);
                sb.AppendLine(await Recognize(engine, stream)).AppendLine();
            }
        }
        else
        {
            // Any image Magick can read (HEIC, WebP…) → PNG in memory → OCR.
            using var img = new MagickImage(input);
            img.AutoOrient();
            img.Resize(new MagickGeometry($"{OcrEngine.MaxImageDimension}x{OcrEngine.MaxImageDimension}>"));
            using var stream = new InMemoryRandomAccessStream();
            var bytes = img.ToByteArray(MagickFormat.Png);
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
            }
            stream.Seek(0);
            sb.AppendLine(await Recognize(engine, stream));
        }

        var text = sb.ToString().Trim();
        if (text.Length == 0) throw new InvalidOperationException("No text was found.");
        var output = Paths.Output(input, "txt", "ocr");
        await File.WriteAllTextAsync(output, text, new UTF8Encoding(false));
        return output;
    }

    private static async Task<string> Recognize(OcrEngine engine, IRandomAccessStream stream)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var result = await engine.RecognizeAsync(bitmap);
        return string.Join(Environment.NewLine, result.Lines.Select(l => l.Text));
    }

    // ── Office formats ────────────────────────────────────────────────────

    /// <summary>PDF → DOCX/RTF/ODT/HTML through Word's PDF import; MD/EPUB continue through Pandoc.</summary>
    public static async Task<string> ToDocumentAsync(string input, string ext)
    {
        if (ext is "docx" or "rtf" or "odt" or "html")
            return await Office.WordConvertAsync(input, ext);

        var docx = await Office.WordConvertAsync(input, "docx");
        try
        {
            var converted = await Docs.ConvertAsync(docx, ext);
            var output = Paths.Output(input, ext);
            if (!string.Equals(converted, output, StringComparison.OrdinalIgnoreCase)) File.Move(converted, output);
            return output;
        }
        finally
        {
            TryDelete(docx);
        }
    }

    /// <summary>PDF → PowerPoint: each page becomes a full-slide picture.</summary>
    public static async Task<string> ToPptxAsync(string input)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"orange-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            var (files, w, h) = await RenderToTempAsync(input, temp);
            return await Office.ImagesToPptxAsync(files, w, h, Paths.Output(input, "pptx"));
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}

/// <summary>Parses page ranges typed by the user: "1-3, 5, 8-" (1-based, inclusive).</summary>
public static class PageRanges
{
    public static List<(string Label, List<int> Pages)> Parse(string text, int pageCount)
    {
        var result = new List<(string, List<int>)>();
        foreach (var raw in text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = raw.Split('-', StringSplitOptions.TrimEntries);
            int from, to;
            if (parts.Length == 1 && int.TryParse(parts[0], out from)) to = from;
            else if (parts.Length == 2)
            {
                from = parts[0].Length == 0 ? 1 : int.TryParse(parts[0], out var f) ? f : -1;
                to = parts[1].Length == 0 ? pageCount : int.TryParse(parts[1], out var t) ? t : -1;
            }
            else throw new FormatException($"I don't understand \"{raw}\". Use something like 1-3, 5, 8-");

            if (from < 1 || to < from || to > pageCount)
                throw new FormatException($"\"{raw}\" is outside this PDF (it has {pageCount} pages).");

            result.Add((from == to ? $"p{from}" : $"p{from}-{to}", Enumerable.Range(from - 1, to - from + 1).ToList()));
        }
        if (result.Count == 0) throw new FormatException("No pages were given.");
        return result;
    }
}

/// <summary>Sorts like Explorer: "page 2" before "page 10".</summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string a, string b);

    public int Compare(string? x, string? y) => StrCmpLogicalW(x ?? "", y ?? "");
}
