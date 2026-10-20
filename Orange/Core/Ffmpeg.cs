using System.Diagnostics;
using System.Text;

namespace Orange.Core;

/// <summary>Thin wrapper that runs ffmpeg and turns failures into readable exceptions.</summary>
public static class Ffmpeg
{
    public static async Task RunAsync(string input, string output, string args, CancellationToken ct = default)
    {
        var exe = Tools.Ffmpeg ?? throw new InvalidOperationException(
            "ffmpeg was not found. Install it (winget install Gyan.FFmpeg) or put ffmpeg.exe next to Orange.exe.");

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"-hide_banner -nostdin -loglevel error -y -i \"{input}\" {args} \"{output}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var proc = new Process { StartInfo = psi };
        var err = new StringBuilder();
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) err.AppendLine(e.Data); };
        proc.Start();
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();

        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(true); } catch { }
            TryDelete(output);
            throw;
        }

        if (proc.ExitCode != 0)
        {
            TryDelete(output);
            var msg = err.ToString().Trim();
            throw new InvalidOperationException(string.IsNullOrEmpty(msg) ? $"ffmpeg exited with code {proc.ExitCode}" : msg);
        }
    }

    // ── Conversion presets ────────────────────────────────────────────────

    public static string VideoArgs(string ext) => ext switch
    {
        "mp4" or "mov" or "m4v" => "-c:v libx264 -preset medium -crf 23 -pix_fmt yuv420p -c:a aac -b:a 160k -movflags +faststart",
        "mkv" => "-c:v libx264 -preset medium -crf 23 -c:a aac -b:a 160k",
        "webm" => "-c:v libvpx-vp9 -crf 32 -b:v 0 -row-mt 1 -c:a libopus -b:a 128k",
        "avi" => "-c:v mpeg4 -q:v 3 -c:a libmp3lame -q:a 3",
        "wmv" => "-c:v wmv2 -q:v 3 -c:a wmav2 -b:a 160k",
        "gif" => "-vf \"fps=12,scale='min(480,iw)':-2:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse\" -loop 0",
        _ => throw new NotSupportedException($"No video preset for .{ext}")
    };

    public static string AudioArgs(string ext) => "-vn -map_metadata 0 " + ext switch
    {
        "mp3" => "-c:a libmp3lame -q:a 2",
        "m4a" => "-c:a aac -b:a 192k",
        "wav" => "-c:a pcm_s16le",
        "flac" => "-c:a flac",
        "ogg" => "-c:a libvorbis -q:a 5",
        "opus" => "-c:a libopus -b:a 128k",
        "aiff" => "-c:a pcm_s16be",
        "wma" => "-c:a wmav2 -b:a 192k",
        _ => throw new NotSupportedException($"No audio preset for .{ext}")
    };

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
