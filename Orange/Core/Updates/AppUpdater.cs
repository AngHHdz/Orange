using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Orange.Core.Updates;

/// <summary>
/// Self-update from GitHub Releases: finds the newest "Orange-Setup-*.exe", downloads it, checks its
/// SHA-256 against the digest GitHub publishes, then runs it silently and lets it restart Orange.
/// </summary>
public static class AppUpdater
{
    public const string Repo = "koko00123/orangefile";

    public sealed record Release(Version Version, string Tag, string Url, long Size, string? Sha256, string Notes);

    public static Version Current => Downloader.AppVersion;

    /// <summary>Set by the last check that found something newer.</summary>
    public static Release? Available { get; private set; }

    public static event Action? AvailableChanged;

    /// <summary>Asks GitHub for the latest release. Returns it only when it is newer than this Orange.</summary>
    public static async Task<Release?> CheckAsync()
    {
        using var response = await Downloader.Http.GetAsync($"https://api.github.com/repos/{Repo}/releases/latest");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // No release published yet: nothing to update to.
            Available = null;
            AvailableChanged?.Invoke();
            return null;
        }
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var version = Downloader.ParseVersion(tag);

        Release? found = null;
        if (version != null && version > Current)
        {
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (!name.StartsWith("Orange-Setup", StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    continue;
                var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null; // "sha256:…"
                found = new Release(version, tag, asset.GetProperty("browser_download_url").GetString()!,
                    asset.GetProperty("size").GetInt64(),
                    digest?.StartsWith("sha256:") == true ? digest[7..] : null,
                    root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "");
                break;
            }
        }

        Available = found;
        AvailableChanged?.Invoke();
        return found;
    }

    /// <summary>Downloads, verifies and runs the installer, then quits so it can replace Orange.</summary>
    public static async Task InstallAsync(Release release, IProgress<double>? progress)
    {
        var setup = await Downloader.ToTempFileAsync(release.Url, ".exe", progress);

        // Never run a broken or tampered download.
        if (new FileInfo(setup).Length != release.Size)
            throw Fail(setup, "The download is incomplete. Please try again.");
        if (release.Sha256 != null)
        {
            await using var stream = File.OpenRead(setup);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream));
            if (!hash.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                throw Fail(setup, "The download does not match the published file. It was not installed.");
        }

        JobRunner.Log($"Updating Orange {Current} → {release.Version}");
        // /relaunch=1 makes the installer start Orange again when it finishes (see installer\Orange.iss).
        Process.Start(new ProcessStartInfo(setup, "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /relaunch=1")
        {
            UseShellExecute = true,
        });
        App.Instance.Shutdown();
    }

    private static Exception Fail(string file, string message)
    {
        try { File.Delete(file); } catch { }
        return new InvalidOperationException(Loc.T(message));
    }
}
