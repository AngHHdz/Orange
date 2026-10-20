using System.Formats.Tar;
using System.IO.Compression;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Orange.Core;

/// <summary>Creating ZIP / TAR / TAR.GZ (built into .NET) and extracting anything SharpCompress reads
/// (ZIP, RAR, 7z, TAR, GZ, BZ2, XZ).</summary>
public static class Archives
{
    public static readonly string[] PackFormats = ["zip", "tar.gz", "tar"];

    // ── Extract ───────────────────────────────────────────────────────────

    /// <summary>Extracts into a new folder next to the archive, named after it.</summary>
    public static Task<string> ExtractAsync(string archive) => Task.Run(() =>
    {
        var dest = Paths.Unique(Path.Combine(Paths.OutputDir(archive), BaseName(archive)));
        Directory.CreateDirectory(dest);
        ExtractTo(archive, dest);
        return dest;
    });

    private static void ExtractTo(string archive, string dest)
    {
        var options = new ExtractionOptions { ExtractFullPath = true, Overwrite = true };
        var ext = FileKinds.Ext(archive);

        // Compressed tarballs are streams of a tar inside a gzip/bzip2/xz — the reader API unwraps both layers.
        if (ext is "gz" or "tgz" or "bz2" or "xz" or "tar")
        {
            using var reader = ReaderFactory.OpenReader(archive, new ReaderOptions());
            reader.WriteAllToDirectory(dest, options);
        }
        else
        {
            ArchiveFactory.WriteToDirectory(archive, dest, options);
        }
    }

    // ── Pack ──────────────────────────────────────────────────────────────

    /// <summary>Packs files and folders into one archive next to the first item.</summary>
    public static Task<string> PackAsync(IReadOnlyList<string> items, string format) => Task.Run(() =>
    {
        var dir = Paths.OutputDir(items[0]);
        var name = items.Count == 1 ? Path.GetFileNameWithoutExtension(items[0].TrimEnd('\\')) : "Archive";
        if (items.Count == 1 && Directory.Exists(items[0])) name = Path.GetFileName(items[0].TrimEnd('\\'));
        var output = Paths.Unique(Path.Combine(dir, $"{name}.{format}"));
        Pack(items, output, format);
        return output;
    });

    /// <summary>Re-packs an archive in another format (e.g. RAR → ZIP) via a temp folder.</summary>
    public static Task<string> RepackAsync(string archive, string format) => Task.Run(() =>
    {
        var temp = Path.Combine(Path.GetTempPath(), $"orange-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            ExtractTo(archive, temp);
            var output = Paths.Unique(Path.Combine(Paths.OutputDir(archive), $"{BaseName(archive)}.{format}"));
            Pack(Directory.GetFileSystemEntries(temp), output, format);
            return output;
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    });

    private static void Pack(IReadOnlyList<string> items, string output, string format)
    {
        try
        {
            if (format == "zip")
            {
                using var zip = ZipFile.Open(output, ZipArchiveMode.Create);
                foreach (var (path, entry) in Entries(items))
                    zip.CreateEntryFromFile(path, entry, CompressionLevel.Optimal);
                return;
            }

            using var file = File.Create(output);
            using Stream stream = format == "tar.gz" ? new GZipStream(file, CompressionLevel.Optimal) : file;
            using var tar = new TarWriter(stream, TarEntryFormat.Pax);
            foreach (var (path, entry) in Entries(items))
                tar.WriteEntry(path, entry.Replace('\\', '/'));
        }
        catch
        {
            if (File.Exists(output)) File.Delete(output);
            throw;
        }
    }

    /// <summary>Every file under the selection, with its path inside the archive
    /// (a dropped folder keeps its own name as the top-level folder).</summary>
    private static IEnumerable<(string Path, string Entry)> Entries(IEnumerable<string> items)
    {
        foreach (var item in items)
        {
            var trimmed = item.TrimEnd('\\');
            if (Directory.Exists(trimmed))
            {
                var root = Path.GetDirectoryName(trimmed)!;
                foreach (var f in Directory.EnumerateFiles(trimmed, "*", SearchOption.AllDirectories))
                    yield return (f, Path.GetRelativePath(root, f));
            }
            else
            {
                yield return (trimmed, Path.GetFileName(trimmed));
            }
        }
    }

    /// <summary>"photos.tar.gz" → "photos", "backup.zip" → "backup".</summary>
    private static string BaseName(string archive)
    {
        var name = Path.GetFileNameWithoutExtension(archive);
        return name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }
}
