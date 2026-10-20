using System.Diagnostics;
using System.Windows;
using ImageMagick;
using Orange.UI;

namespace Orange.Core;

/// <summary>Decides which slices the wheel shows for a mode and a set of dropped paths.</summary>
public static class Catalog
{
    public static IReadOnlyList<WheelItem> For(WheelMode mode, IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return [];
        var kind = FileKinds.Common(paths);
        var items = mode switch
        {
            WheelMode.Convert => ConvertItems(kind, paths),
            WheelMode.Tools => ToolItems(kind, paths),
            _ => ActionItems(kind, paths),
        };
        return items.Take(12).ToList();
    }

    // ── Convert (Shift) ───────────────────────────────────────────────────

    private static IEnumerable<WheelItem> ConvertItems(FileKind kind, IReadOnlyList<string> paths)
    {
        var src = Path.GetExtension(paths[0]).TrimStart('.').ToLowerInvariant();
        bool Same(string ext) => paths.Count == 1 && (ext == src || (ext == "jpg" && src == "jpeg") || (ext == "tiff" && src == "tif"));

        switch (kind)
        {
            case FileKind.Image:
                foreach (var ext in new[] { "jpg", "png", "webp", "avif", "gif", "tiff", "bmp", "ico" }.Where(e => !Same(e)))
                    yield return new WheelItem(ext.ToUpperInvariant(), ImageHint(ext),
                        JobRunner.PerFile($"Convert to {ext.ToUpperInvariant()}", f => Images.ConvertAsync(f, ext)));
                yield return paths.Count > 1
                    ? new WheelItem("PDF", "one file", JobRunner.AllFiles("Images → PDF", Images.ToPdfAsync))
                    : new WheelItem("PDF", "document", JobRunner.PerFile("Convert to PDF", f => Images.ConvertAsync(f, "pdf")));
                break;

            case FileKind.Video:
                foreach (var ext in new[] { "mp4", "mov", "mkv", "webm", "avi", "gif" }.Where(e => !Same(e)))
                    yield return new WheelItem(ext.ToUpperInvariant(), ext == "gif" ? "animated" : "video",
                        JobRunner.PerFile($"Convert to {ext.ToUpperInvariant()}", f => FfmpegTo(f, ext, Ffmpeg.VideoArgs(ext))));
                foreach (var ext in new[] { "mp3", "m4a", "wav" })
                    yield return new WheelItem(ext.ToUpperInvariant(), "audio only",
                        JobRunner.PerFile($"Extract {ext.ToUpperInvariant()}", f => FfmpegTo(f, ext, Ffmpeg.AudioArgs(ext))));
                break;

            case FileKind.Audio:
                foreach (var ext in new[] { "mp3", "m4a", "wav", "flac", "ogg", "opus", "aiff", "wma" }.Where(e => !Same(e)))
                    yield return new WheelItem(ext.ToUpperInvariant(), AudioHint(ext),
                        JobRunner.PerFile($"Convert to {ext.ToUpperInvariant()}", f => FfmpegTo(f, ext, Ffmpeg.AudioArgs(ext))));
                break;

            case FileKind.Document:
            case FileKind.Ebook:
                foreach (var ext in Docs.TargetsFor(paths[0]).Where(e => !Same(e)))
                    yield return new WheelItem(ext.ToUpperInvariant(), DocHint(ext),
                        JobRunner.PerFile($"Convert to {ext.ToUpperInvariant()}", f => Docs.ConvertAsync(f, ext)));
                break;

            case FileKind.Spreadsheet:
                foreach (var ext in Office.ExcelTargets.Where(e => !Same(e)))
                    yield return new WheelItem(ext.ToUpperInvariant(), SheetHint(ext),
                        JobRunner.PerFile($"Convert to {ext.ToUpperInvariant()}", f => Office.ExcelConvertAsync(f, ext)));
                break;

            case FileKind.Presentation:
                foreach (var ext in Office.PowerPointTargets.Where(e => !Same(e)))
                    yield return new WheelItem(ext.ToUpperInvariant(), ext == "pdf" ? "handout" : "slides",
                        JobRunner.PerFile($"Convert to {ext.ToUpperInvariant()}", f => Office.PowerPointConvertAsync(f, ext)));
                yield return new WheelItem("PNG", "image per slide",
                    JobRunner.PerFile("Slides → PNG", f => Office.PowerPointConvertAsync(f, "png")));
                yield return new WheelItem("JPG", "image per slide",
                    JobRunner.PerFile("Slides → JPG", f => Office.PowerPointConvertAsync(f, "jpg")));
                break;

            case FileKind.Archive:
                yield return new WheelItem("Extract", "to folder", JobRunner.PerFile("Extract", Archives.ExtractAsync));
                foreach (var fmt in Archives.PackFormats.Where(e => !Same(e)))
                    yield return new WheelItem(fmt.ToUpperInvariant(), "repack",
                        JobRunner.PerFile($"Repack as {fmt.ToUpperInvariant()}", f => Archives.RepackAsync(f, fmt)));
                break;

            case FileKind.Subtitle:
                foreach (var ext in new[] { "srt", "vtt", "txt" }.Where(e => !Same(e)))
                    yield return new WheelItem(ext.ToUpperInvariant(), ext == "txt" ? "text only" : "subtitles",
                        JobRunner.PerFile($"Convert to {ext.ToUpperInvariant()}", f => Subtitles.ConvertAsync(f, ext)));
                break;

            // Folders and mixed selections can always be packed.
            case FileKind.Folder:
            case FileKind.Other:
                foreach (var item in PackItems()) yield return item;
                break;
        }
    }

    private static IEnumerable<WheelItem> PackItems() => Archives.PackFormats.Select(fmt =>
        new WheelItem(fmt.ToUpperInvariant(), "compress", JobRunner.AllFiles($"Pack as {fmt.ToUpperInvariant()}", items => Archives.PackAsync(items, fmt))));

    // ── Tools (Ctrl) ──────────────────────────────────────────────────────

    private static IEnumerable<WheelItem> ToolItems(FileKind kind, IReadOnlyList<string> paths)
    {
        switch (kind)
        {
            case FileKind.Image:
                yield return new WheelItem("Compress", "smaller file", JobRunner.PerFile("Compress",
                    f => Images.EditAsync(f, "compressed", Images.Compress)));
                yield return new WheelItem("Resize", "50 %", JobRunner.PerFile("Resize",
                    f => Images.EditAsync(f, "50%", img => img.Resize(new Percentage(50)))));
                yield return new WheelItem("Rotate", "90° right", JobRunner.PerFile("Rotate",
                    f => Images.EditAsync(f, "rotated", img => img.Rotate(90))));
                yield return new WheelItem("Flip", "mirror", JobRunner.PerFile("Flip",
                    f => Images.EditAsync(f, "flipped", img => img.Flop())));
                yield return new WheelItem("B & W", "grayscale", JobRunner.PerFile("Grayscale",
                    f => Images.EditAsync(f, "bw", img => img.Grayscale())));
                yield return new WheelItem("Clean", "remove metadata", JobRunner.PerFile("Remove metadata",
                    f => Images.EditAsync(f, "clean", img => img.Strip())));
                if (paths.Count > 1)
                    yield return new WheelItem("Merge", "into PDF", JobRunner.AllFiles("Images → PDF", Images.ToPdfAsync));
                break;

            case FileKind.Video:
                yield return new WheelItem("Compress", "smaller MP4", JobRunner.PerFile("Compress",
                    f => FfmpegTo(f, "mp4", "-c:v libx264 -preset slow -crf 28 -pix_fmt yuv420p -c:a aac -b:a 128k -movflags +faststart", "compressed")));
                yield return new WheelItem("720p", "downscale", JobRunner.PerFile("Downscale to 720p",
                    f => FfmpegTo(f, "mp4", "-vf \"scale=-2:'min(720,ih)'\" -c:v libx264 -crf 23 -pix_fmt yuv420p -c:a aac -b:a 160k -movflags +faststart", "720p")));
                yield return new WheelItem("Mute", "remove audio", JobRunner.PerFile("Mute",
                    f => FfmpegTo(f, Ext(f), "-c copy -an", "muted")));
                yield return new WheelItem("Rotate", "90° right", JobRunner.PerFile("Rotate",
                    f => FfmpegTo(f, "mp4", "-vf transpose=1 -c:v libx264 -crf 20 -pix_fmt yuv420p -c:a copy", "rotated")));
                yield return new WheelItem("Speed", "2×", JobRunner.PerFile("Speed up",
                    f => FfmpegTo(f, "mp4", "-filter_complex \"[0:v]setpts=0.5*PTS[v];[0:a]atempo=2.0[a]\" -map \"[v]\" -map \"[a]\" -c:v libx264 -crf 23 -pix_fmt yuv420p -c:a aac", "2x")));
                yield return new WheelItem("Audio", "extract MP3", JobRunner.PerFile("Extract audio",
                    f => FfmpegTo(f, "mp3", Ffmpeg.AudioArgs("mp3"))));
                break;

            case FileKind.Audio:
                yield return new WheelItem("Compress", "MP3 128k", JobRunner.PerFile("Compress",
                    f => FfmpegTo(f, "mp3", "-vn -c:a libmp3lame -b:a 128k", "compressed")));
                yield return new WheelItem("Normalize", "loudness", JobRunner.PerFile("Normalize",
                    f => FfmpegTo(f, Ext(f), "-af loudnorm", "normalized")));
                yield return new WheelItem("Mono", "1 channel", JobRunner.PerFile("Mono",
                    f => FfmpegTo(f, Ext(f), "-ac 1", "mono")));
                yield return new WheelItem("Speed", "1.5×", JobRunner.PerFile("Speed up",
                    f => FfmpegTo(f, Ext(f), "-af atempo=1.5", "1.5x")));
                break;
        }

        // Anything can be zipped.
        if (kind != FileKind.Archive)
            yield return new WheelItem("ZIP", "compress", JobRunner.AllFiles("Pack as ZIP", items => Archives.PackAsync(items, "zip")));
    }

    // ── Actions (Alt) — work for any file, folder or mixed selection ──────

    private static IEnumerable<WheelItem> ActionItems(FileKind kind, IReadOnlyList<string> paths)
    {
        yield return new WheelItem("Copy", "to clipboard", files =>
        {
            var list = new System.Collections.Specialized.StringCollection();
            list.AddRange(files.ToArray());
            Clipboard.SetFileDropList(list);
            App.Instance.Notify("Copied", $"{files.Count} item(s) — paste anywhere with Ctrl+V");
            return Task.CompletedTask;
        }, OnUiThread: true);

        yield return new WheelItem("Path", "copy path", files =>
        {
            Clipboard.SetText(string.Join(Environment.NewLine, files));
            App.Instance.Notify("Path copied", files.Count == 1 ? files[0] : $"{files.Count} paths");
            return Task.CompletedTask;
        }, OnUiThread: true);

        yield return new WheelItem("Move", "to folder…", files => MoveOrCopy(files, move: true), OnUiThread: true);
        yield return new WheelItem("Duplicate", "to folder…", files => MoveOrCopy(files, move: false), OnUiThread: true);

        if (kind == FileKind.Image)
            yield return new WheelItem("Float", "pin on top", files =>
            {
                foreach (var f in files.Take(5)) new FloatWindow(f).Show();
                return Task.CompletedTask;
            }, OnUiThread: true);

        yield return new WheelItem("Terminal", "open here", files =>
        {
            var dir = Directory.Exists(files[0]) ? files[0] : Path.GetDirectoryName(files[0])!;
            OpenTerminal(dir);
            return Task.CompletedTask;
        }, OnUiThread: true);

        yield return new WheelItem("Reveal", "in Explorer", files =>
        {
            Process.Start("explorer.exe", $"/select,\"{files[0]}\"");
            return Task.CompletedTask;
        }, OnUiThread: true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static async Task<string> FfmpegTo(string input, string ext, string args, string? suffix = null)
    {
        var output = Paths.Output(input, ext, suffix);
        await Ffmpeg.RunAsync(input, output, args);
        return output;
    }

    private static string Ext(string path) => FileKinds.Ext(path);

    private static Task MoveOrCopy(IReadOnlyList<string> files, bool move)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = move ? "Move to…" : "Copy to…" };
        if (dlg.ShowDialog() != true) return Task.CompletedTask;
        var target = dlg.FolderName;

        // File I/O happens off the UI thread; large copies shouldn't freeze the app.
        return JobRunner.PerFile(move ? "Move" : "Copy", file => Task.Run(() =>
        {
            var dest = Paths.Unique(Path.Combine(target, Path.GetFileName(file)));
            if (Directory.Exists(file))
            {
                if (move) Directory.Move(file, dest);
                else CopyDirectory(file, dest);
            }
            else if (move) File.Move(file, dest);
            else File.Copy(file, dest);
            return dest;
        }))(files);
    }

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dest, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(src)) CopyDirectory(d, Path.Combine(dest, Path.GetFileName(d)));
    }

    private static void OpenTerminal(string dir)
    {
        var wt = Tools.Find("wt.exe");
        if (wt != null)
            Process.Start(new ProcessStartInfo(wt, $"-d \"{dir}\"") { UseShellExecute = true });
        else
            Process.Start(new ProcessStartInfo("powershell.exe") { WorkingDirectory = dir, UseShellExecute = true });
    }

    private static string ImageHint(string ext) => ext switch
    {
        "jpg" => "photo", "png" => "lossless", "webp" => "web", "avif" => "tiny",
        "gif" => "classic", "tiff" => "print", "bmp" => "raw", "ico" => "icon", _ => ""
    };

    private static string DocHint(string ext) => ext switch
    {
        "pdf" => "to share", "docx" => "Word", "doc" => "Word 97", "odt" => "OpenDocument", "rtf" => "rich text",
        "txt" => "plain text", "html" => "web page", "md" => "Markdown", "epub" => "e-book", _ => ""
    };

    private static string SheetHint(string ext) => ext switch
    {
        "pdf" => "to print", "xlsx" => "Excel", "xls" => "Excel 97", "ods" => "OpenDocument",
        "csv" => "comma", "tsv" => "tab", "html" => "web table", _ => ""
    };

    private static string AudioHint(string ext) => ext switch
    {
        "mp3" => "universal", "m4a" => "Apple", "wav" => "uncompressed", "flac" => "lossless",
        "ogg" => "open", "opus" => "voice", "aiff" => "studio", "wma" => "Windows", _ => ""
    };
}
