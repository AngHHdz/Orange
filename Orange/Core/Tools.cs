namespace Orange.Core;

/// <summary>Locates the external programs Orange relies on.</summary>
public static class Tools
{
    public static string? Ffmpeg { get; private set; }
    public static string? Pandoc { get; private set; }

    public static void Initialize()
    {
        Ffmpeg = Find("ffmpeg.exe");
        // winget installs Pandoc per user; Orange may have started before PATH was refreshed.
        Pandoc = Find("pandoc.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pandoc"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Pandoc"));
    }

    /// <summary>Looks in Orange's updated-tools folder first (Settings → Update tools), then next to
    /// Orange.exe and its "tools" folder (bundled copies), then the known install folders, then PATH.</summary>
    public static string? Find(string exe, params string[] knownDirs)
    {
        var baseDir = AppContext.BaseDirectory;
        var updated = Path.Combine(Paths.AppData, "tools");
        foreach (var dir in new[] { updated, baseDir, Path.Combine(baseDir, "tools") }.Concat(knownDirs))
        {
            var candidate = Path.Combine(dir, exe);
            if (File.Exists(candidate)) return candidate;
        }

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), exe);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { /* malformed PATH entry */ }
        }
        return null;
    }
}
