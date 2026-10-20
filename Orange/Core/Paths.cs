namespace Orange.Core;

public static class Paths
{
    public static string AppData
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orange");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// Output path next to the source: "photo.heic" + ".png" → "photo.png",
    /// or "photo (1).png" if that name is taken. A suffix gives "clip (compressed).mp4".
    /// </summary>
    public static string Output(string source, string newExt, string? suffix = null)
    {
        var dir = Path.GetDirectoryName(source) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var name = Path.GetFileNameWithoutExtension(source);
        if (!string.IsNullOrEmpty(suffix)) name += $" ({suffix})";
        if (!newExt.StartsWith('.')) newExt = "." + newExt;
        return Unique(Path.Combine(dir, name + newExt));
    }

    public static string Unique(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 1; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }
}
