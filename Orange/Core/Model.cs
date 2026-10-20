namespace Orange.Core;

public enum WheelMode { Convert, Tools, Actions }

public enum FileKind { Image, Video, Audio, Pdf, Document, Ebook, Spreadsheet, Presentation, Archive, Subtitle, Folder, Other }

/// <summary>One slice of the wheel.</summary>
/// <param name="Label">Big text, e.g. "PNG".</param>
/// <param name="Hint">Small text under the label, e.g. "lossless".</param>
/// <param name="Run">Work to do with the dropped paths.</param>
/// <param name="OnUiThread">Actions that touch the clipboard or open dialogs must stay on the UI thread.</param>
public sealed record WheelItem(string Label, string Hint, Func<IReadOnlyList<string>, Task> Run, bool OnUiThread = false);

public static class FileKinds
{
    public static readonly string[] ImageExt = [".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif", ".avif", ".tif", ".tiff", ".bmp", ".gif", ".svg", ".ico"];
    public static readonly string[] VideoExt = [".mp4", ".mov", ".mkv", ".webm", ".avi", ".wmv", ".m4v", ".flv", ".mpg", ".mpeg", ".3gp"];
    public static readonly string[] AudioExt = [".mp3", ".m4a", ".wav", ".flac", ".ogg", ".opus", ".aiff", ".aif", ".wma", ".aac"];
    public static readonly string[] DocExt = [".docx", ".doc", ".odt", ".rtf", ".txt", ".md", ".markdown", ".html", ".htm"];
    public static readonly string[] EbookExt = [".epub"];
    public static readonly string[] SheetExt = [".xlsx", ".xls", ".xlsm", ".ods", ".csv", ".tsv"];
    public static readonly string[] SlideExt = [".pptx", ".ppt", ".odp"];
    public static readonly string[] ArchiveExt = [".zip", ".rar", ".7z", ".tar", ".gz", ".tgz", ".bz2", ".xz"];
    public static readonly string[] SubtitleExt = [".srt", ".vtt"];

    public static FileKind Of(string path)
    {
        if (Directory.Exists(path)) return FileKind.Folder;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ImageExt.Contains(ext)) return FileKind.Image;
        if (VideoExt.Contains(ext)) return FileKind.Video;
        if (AudioExt.Contains(ext)) return FileKind.Audio;
        if (ext == ".pdf") return FileKind.Pdf;
        if (DocExt.Contains(ext)) return FileKind.Document;
        if (EbookExt.Contains(ext)) return FileKind.Ebook;
        if (SheetExt.Contains(ext)) return FileKind.Spreadsheet;
        if (SlideExt.Contains(ext)) return FileKind.Presentation;
        if (ArchiveExt.Contains(ext)) return FileKind.Archive;
        if (SubtitleExt.Contains(ext)) return FileKind.Subtitle;
        return FileKind.Other;
    }

    /// <summary>Lower-case extension without the dot: "Report.DOCX" → "docx".</summary>
    public static string Ext(string path) => Path.GetExtension(path).TrimStart('.').ToLowerInvariant();

    /// <summary>The kind shared by every path, or Other for a mixed selection.</summary>
    public static FileKind Common(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return FileKind.Other;
        var first = Of(paths[0]);
        return paths.All(p => Of(p) == first) ? first : FileKind.Other;
    }
}
