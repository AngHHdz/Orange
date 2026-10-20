using System.Diagnostics;

namespace Orange.Core;

/// <summary>Runs per-file work in the background, logs it and reports the result in the tray.</summary>
public static class JobRunner
{
    private static readonly string LogPath = Path.Combine(Paths.AppData, "orange.log");
    private static readonly object LogLock = new();

    public static string? LastOutput { get; private set; }

    /// <summary>Makes a click on the next notification open this file's folder.</summary>
    public static void Remember(string path) => LastOutput = path;

    /// <summary>Runs <paramref name="work"/> for every file (one at a time) and notifies when done.</summary>
    public static Func<IReadOnlyList<string>, Task> PerFile(string verb, Func<string, Task<string>> work) => async files =>
    {
        int ok = 0;
        var errors = new List<string>();
        foreach (var file in files)
        {
            try
            {
                var output = await work(file);
                LastOutput = output;
                ok++;
                Log($"OK   {verb}: {file} → {output}");
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(file)}: {FirstLine(ex.Message)}");
                Log($"FAIL {verb}: {file}\n     {ex.Message}");
            }
        }
        Report(verb, ok, errors);
    };

    /// <summary>Runs one job over the whole selection (e.g. merge into one PDF).</summary>
    public static Func<IReadOnlyList<string>, Task> AllFiles(string verb, Func<IReadOnlyList<string>, Task<string>> work) => async files =>
    {
        try
        {
            LastOutput = await work(files);
            Log($"OK   {verb}: {files.Count} file(s) → {LastOutput}");
            Report(verb, 1, []);
        }
        catch (Exception ex)
        {
            Log($"FAIL {verb}: {string.Join(", ", files)}\n     {ex.Message}");
            Report(verb, 0, [FirstLine(ex.Message)]);
        }
    };

    private static void Report(string verb, int ok, List<string> errors)
    {
        App.Instance.Dispatcher.BeginInvoke(() =>
        {
            var title = Loc.T(verb);
            if (errors.Count == 0)
                App.Instance.Notify($"{title} ✓", ok == 1 ? Path.GetFileName(LastOutput) ?? Loc.T("Done") : Loc.F("{0} files done", ok));
            else
                App.Instance.Notify(Loc.F("{0}: {1} failed", title, errors.Count), string.Join("\n", errors.Take(3)), error: true);
        });
    }

    public static void Log(string line)
    {
        lock (LogLock)
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}{Environment.NewLine}");
    }

    public static void OpenLog()
    {
        if (!File.Exists(LogPath)) File.WriteAllText(LogPath, "");
        Process.Start(new ProcessStartInfo(LogPath) { UseShellExecute = true });
    }

    public static void RevealLastOutput()
    {
        if (LastOutput != null && File.Exists(LastOutput))
            Process.Start("explorer.exe", $"/select,\"{LastOutput}\"");
    }

    private static string FirstLine(string s) => s.Split('\n', 2)[0].Trim();
}
