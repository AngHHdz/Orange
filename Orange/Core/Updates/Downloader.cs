using System.Net.Http;
using System.Reflection;

namespace Orange.Core.Updates;

/// <summary>Shared HTTP client and a download-with-progress helper for the updaters.</summary>
internal static class Downloader
{
    // Declared before Http: static fields initialize in order, and the client's User-Agent needs the version.
    public static Version AppVersion { get; } =
        typeof(Downloader).Assembly.GetName().Version is { } v ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : new Version(1, 0, 0);

    public static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        // GitHub's API rejects requests without a User-Agent.
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Orange/{AppVersion}");
        return http;
    }

    /// <summary>Downloads to a temp file and returns its path. Progress goes from 0 to 1.</summary>
    public static async Task<string> ToTempFileAsync(string url, string extension, IProgress<double>? progress, CancellationToken ct = default)
    {
        var target = Path.Combine(Path.GetTempPath(), $"orange-{Guid.NewGuid():N}{extension}");
        try
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            long? total = response.Content.Headers.ContentLength;

            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var file = File.Create(target);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                if (total > 0) progress?.Report((double)done / total.Value);
            }
            return target;
        }
        catch
        {
            try { File.Delete(target); } catch { }
            throw;
        }
    }

    /// <summary>"9.0.2-essentials_build" → 9.0.2; "v1.1.0" → 1.1.0; null if there is no version in it.</summary>
    public static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(text, @"\d+(\.\d+){1,3}");
        return m.Success && Version.TryParse(m.Value, out var v) ? v : null;
    }
}
