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

    /// <summary>Looks next to Orange.exe first (bundled copy), then in a "tools" folder,
    /// then in the known install folders, then on PATH.</summary>
    public static string? Find(string exe, params string[] knownDirs)
    {
        var baseDir = AppContext.BaseDirectory;
        foreach (var dir in new[] { baseDir, Path.Combine(baseDir, "tools") }.Concat(knownDirs))
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
