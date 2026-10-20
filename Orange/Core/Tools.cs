namespace Orange.Core;

/// <summary>Locates the external programs Orange relies on.</summary>
public static class Tools
{
    public static string? Ffmpeg { get; private set; }

    public static void Initialize()
    {
        Ffmpeg = Find("ffmpeg.exe");
    }

    /// <summary>Looks next to Orange.exe first (bundled copy), then in a "tools" folder, then on PATH.</summary>
    public static string? Find(string exe)
    {
        var baseDir = AppContext.BaseDirectory;
        foreach (var candidate in new[] { Path.Combine(baseDir, exe), Path.Combine(baseDir, "tools", exe) })
            if (File.Exists(candidate)) return candidate;

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
