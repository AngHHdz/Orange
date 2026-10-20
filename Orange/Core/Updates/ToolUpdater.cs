using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace Orange.Core.Updates;

/// <summary>
/// Keeps the external tools (ffmpeg, Pandoc) current. Updated copies go to %LOCALAPPDATA%\Orange\tools,
/// which <see cref="Tools"/> checks first, so reinstalling Orange never brings back older versions.
/// </summary>
public static class ToolUpdater
{
    public sealed record Tool(string Name, string Exe, Func<Task<string>> LatestAsync, Func<IProgress<double>?, Task> UpdateAsync);

    public static string Folder => Path.Combine(Paths.AppData, "tools");

    public static readonly Tool Ffmpeg = new("ffmpeg", "ffmpeg.exe",
        // gyan.dev publishes the current release number as plain text.
        async () => (await Downloader.Http.GetStringAsync("https://www.gyan.dev/ffmpeg/builds/release-version")).Trim(),
        progress => InstallFromZipAsync("https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip", "ffmpeg.exe", progress));

    public static readonly Tool Pandoc = new("Pandoc", "pandoc.exe",
        async () => (await LatestPandocAsync()).Tag,
        async progress => await InstallFromZipAsync((await LatestPandocAsync()).ZipUrl, "pandoc.exe", progress));

    public static readonly Tool[] All = [Ffmpeg, Pandoc];

    /// <summary>The version of the copy Orange is using right now (asks the program itself).</summary>
    public static string? InstalledVersion(Tool tool)
    {
        var exe = tool == Ffmpeg ? Tools.Ffmpeg : Tools.Pandoc;
        if (exe == null) return null;
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, tool == Ffmpeg ? "-version" : "--version")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            })!;
            var first = p.StandardOutput.ReadLine() ?? "";
            p.WaitForExit(5000);
            // "ffmpeg version 9.0.2-essentials_build-www.gyan.dev Copyright…" / "pandoc 3.12.1"
            var parts = first.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return tool == Ffmpeg ? (parts.Length > 2 ? parts[2].Split('-')[0] : null) : parts.ElementAtOrDefault(1);
        }
        catch
        {
            return null;
        }
    }

    public static bool IsNewer(string? latest, string? installed)
    {
        var l = Downloader.ParseVersion(latest);
        var i = Downloader.ParseVersion(installed);
        return l != null && (i == null || l > i);
    }

    private static async Task<(string Tag, string ZipUrl)> LatestPandocAsync()
    {
        using var doc = JsonDocument.Parse(await Downloader.Http.GetStringAsync("https://api.github.com/repos/jgm/pandoc/releases/latest"));
        var tag = doc.RootElement.GetProperty("tag_name").GetString()!;
        foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (name.EndsWith("windows-x86_64.zip", StringComparison.OrdinalIgnoreCase))
                return (tag, asset.GetProperty("browser_download_url").GetString()!);
        }
        throw new InvalidOperationException("The latest Pandoc release has no Windows download.");
    }

    /// <summary>Downloads a zip, takes the one .exe we need out of it and swaps it in.</summary>
    private static async Task InstallFromZipAsync(string url, string exeName, IProgress<double>? progress)
    {
        var zipPath = await Downloader.ToTempFileAsync(url, ".zip", progress);
        try
        {
            Directory.CreateDirectory(Folder);
            var target = Path.Combine(Folder, exeName);
            var fresh = target + ".new";

            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var entry = zip.Entries.FirstOrDefault(e => e.Name.Equals(exeName, StringComparison.OrdinalIgnoreCase))
                            ?? throw new InvalidOperationException($"{exeName} was not found in the download.");
                entry.ExtractToFile(fresh, overwrite: true);
            }

            // Replace in one step; fails cleanly (old copy kept) if a conversion is using the file right now.
            File.Move(fresh, target, overwrite: true);
            Tools.Initialize();
        }
        finally
        {
            try { File.Delete(zipPath); } catch { }
        }
    }
}
