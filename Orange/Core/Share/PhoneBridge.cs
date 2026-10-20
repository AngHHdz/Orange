using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Orange.Core.Share;

/// <summary>
/// "AirDrop" over Wi-Fi: a small private web server on the local network.
/// The phone opens it in Safari (via the QR code) to send files to the PC and to pick up
/// files the PC queued for it. Every request needs the secret key from the QR code.
/// </summary>
public static class PhoneBridge
{
    public record OutboxItem(string Id, string Path, string Name, long Size, DateTime Added);

    private static WebApplication? _server;
    private static readonly object OutboxLock = new();
    private static readonly string OutboxFile = System.IO.Path.Combine(Paths.AppData, "phone-outbox.json");
    private static List<OutboxItem> _outbox = LoadOutbox();

    public static bool IsRunning => _server != null;
    public static string? LastError { get; private set; }

    /// <summary>The address to open on the phone, including the secret key.</summary>
    public static string? Url => LanAddress() is { } ip
        ? $"http://{ip}:{Settings.Current.PhonePort}/?k={Uri.EscapeDataString(Settings.Current.PhoneToken)}"
        : null;

    // ── Server lifecycle ──────────────────────────────────────────────────

    public static async Task StartAsync()
    {
        if (_server != null || !Settings.Current.PhoneSharing) return;
        try
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(k =>
            {
                k.ListenAnyIP(Settings.Current.PhonePort);
                k.Limits.MaxRequestBodySize = null; // phone videos can be large
            });
            builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = long.MaxValue);

            var app = builder.Build();
            app.Use(async (ctx, next) =>
            {
                if (ctx.Request.Path.StartsWithSegments("/icon.png") || Authorized(ctx)) await next();
                else ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            });

            app.MapGet("/", ctx =>
            {
                ctx.Response.ContentType = "text/html; charset=utf-8";
                ctx.Response.Headers.CacheControl = "no-store";
                return ctx.Response.WriteAsync(PhonePage.Render(Settings.Current.PhoneToken, ctx.Request.Host.Value ?? ""));
            });
            app.MapGet("/icon.png", ctx =>
            {
                ctx.Response.ContentType = "image/png";
                return ctx.Response.Body.WriteAsync(UI.IconFactory.CreatePng(180)).AsTask();
            });
            app.MapGet("/api/files", () => Results.Json(Outbox().Select(o => new { o.Id, o.Name, o.Size, added = o.Added })));
            app.MapGet("/api/files/{id}", (string id, bool? inline) =>
            {
                var item = Outbox().FirstOrDefault(o => o.Id == id);
                if (item == null) return Results.NotFound();
                // inline=true lets Safari show photos/videos so they can be long-pressed → Save to Photos.
                return inline == true
                    ? Results.File(item.Path, MimeOf(item.Name), enableRangeProcessing: true)
                    : Results.File(item.Path, MimeOf(item.Name), item.Name, enableRangeProcessing: true);
            });
            app.MapDelete("/api/files/{id}", (string id) =>
            {
                Remove(id);
                return Results.NoContent();
            });
            app.MapPost("/api/upload", ReceiveAsync);

            await app.StartAsync();
            _server = app;
            LastError = null;
            JobRunner.Log($"Phone sharing on {Url}");
        }
        catch (Exception ex)
        {
            LastError = ex is IOException or SocketException
                ? $"Port {Settings.Current.PhonePort} is already in use. Choose another port in Settings."
                : ex.Message;
            JobRunner.Log($"FAIL phone sharing: {ex}");
        }
    }

    public static async Task StopAsync()
    {
        var server = _server;
        _server = null;
        if (server != null) await server.DisposeAsync();
    }

    public static async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
    }

    private static bool Authorized(HttpContext ctx)
    {
        var given = ctx.Request.Query["k"].ToString();
        if (given.Length == 0) given = ctx.Request.Headers["X-Orange-Key"].ToString();
        var expected = Encoding.UTF8.GetBytes(Settings.Current.PhoneToken);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), expected);
    }

    // ── Phone → PC ────────────────────────────────────────────────────────

    /// <summary>Accepts multipart uploads (from the web page or an iOS Shortcut "Form" body)
    /// and raw bodies with ?name=… (Shortcut "File" body).</summary>
    private static async Task<IResult> ReceiveAsync(HttpContext ctx)
    {
        var inbox = Settings.Current.PhoneInboxOrDefault;
        Directory.CreateDirectory(inbox);
        var saved = new List<string>();

        if (ctx.Request.HasFormContentType)
        {
            var form = await ctx.Request.ReadFormAsync();
            foreach (var file in form.Files)
            {
                var target = Paths.Unique(System.IO.Path.Combine(inbox, SafeName(file.FileName)));
                await using var output = File.Create(target);
                await file.CopyToAsync(output);
                saved.Add(target);
            }
        }
        else
        {
            var name = ctx.Request.Query["name"].ToString();
            if (name.Length == 0) name = $"From iPhone {DateTime.Now:yyyy-MM-dd HH.mm.ss}{ExtensionFor(ctx.Request.ContentType)}";
            var target = Paths.Unique(System.IO.Path.Combine(inbox, SafeName(name)));
            await using (var output = File.Create(target))
                await ctx.Request.Body.CopyToAsync(output);
            saved.Add(target);
        }

        if (saved.Count == 0) return Results.BadRequest("No file received.");
        foreach (var s in saved) JobRunner.Log($"OK   From phone: {s}");

        _ = App.Instance.Dispatcher.BeginInvoke(() =>
        {
            JobRunner.Remember(saved[^1]);
            App.Instance.Notify(Loc.T("Received from your phone"),
                saved.Count == 1 ? System.IO.Path.GetFileName(saved[0]) : Loc.F("{0} files", saved.Count), force: true);
        });
        return Results.Ok(new { saved = saved.Select(System.IO.Path.GetFileName) });
    }

    private static string SafeName(string name)
    {
        name = System.IO.Path.GetFileName(name.Replace('/', '\\')); // never allow folders in the name
        foreach (var c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) || name is "." or ".." ? "file" : name;
    }

    private static string ExtensionFor(string? contentType) => contentType?.Split(';')[0].Trim() switch
    {
        "image/jpeg" => ".jpg", "image/png" => ".png", "image/heic" => ".heic", "image/gif" => ".gif",
        "video/quicktime" => ".mov", "video/mp4" => ".mp4", "application/pdf" => ".pdf", "text/plain" => ".txt",
        _ => "",
    };

    // ── PC → Phone ────────────────────────────────────────────────────────

    /// <summary>Queues files for the phone. They stay listed until removed on the phone.</summary>
    public static void Send(IEnumerable<string> paths)
    {
        int added = 0;
        lock (OutboxLock)
        {
            foreach (var p in paths)
            {
                if (Directory.Exists(p) || !File.Exists(p)) continue; // folders: zip them first (Tools → ZIP)
                _outbox.RemoveAll(o => string.Equals(o.Path, p, StringComparison.OrdinalIgnoreCase));
                _outbox.Insert(0, new OutboxItem(Guid.NewGuid().ToString("N")[..12], p, System.IO.Path.GetFileName(p),
                    new FileInfo(p).Length, DateTime.Now));
                added++;
            }
            if (_outbox.Count > 100) _outbox.RemoveRange(100, _outbox.Count - 100);
            SaveOutbox();
        }
        App.Instance.Notify(Loc.T("Ready on your phone"),
            added == 0 ? Loc.T("Folders can't be sent. Zip them first.") : Loc.F("{0} file(s) — open Orange on your phone", added),
            error: added == 0, force: true);
    }

    private static List<OutboxItem> Outbox()
    {
        lock (OutboxLock)
        {
            // Forget files that were moved or deleted on the PC.
            if (_outbox.RemoveAll(o => !File.Exists(o.Path)) > 0) SaveOutbox();
            return _outbox.ToList();
        }
    }

    private static void Remove(string id)
    {
        lock (OutboxLock)
        {
            if (_outbox.RemoveAll(o => o.Id == id) > 0) SaveOutbox();
        }
    }

    private static List<OutboxItem> LoadOutbox()
    {
        try
        {
            if (File.Exists(OutboxFile))
                return JsonSerializer.Deserialize<List<OutboxItem>>(File.ReadAllText(OutboxFile)) ?? [];
        }
        catch { }
        return [];
    }

    private static void SaveOutbox() => File.WriteAllText(OutboxFile, JsonSerializer.Serialize(_outbox));

    // ── Network ───────────────────────────────────────────────────────────

    /// <summary>The PC's address on the home network: an up, non-virtual adapter with a gateway, Wi-Fi first.</summary>
    public static IPAddress? LanAddress()
    {
        string[] virtualHints = ["virtual", "vethernet", "hyper-v", "vmware", "virtualbox", "loopback", "bluetooth", "tap", "wsl", "vpn"];
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet
                        && !virtualHints.Any(h => (n.Name + " " + n.Description).Contains(h, StringComparison.OrdinalIgnoreCase)))
            .Select(n => (Nic: n, Props: n.GetIPProperties()))
            .Where(x => x.Props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
            .OrderBy(x => x.Nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 0 : 1)
            .SelectMany(x => x.Props.UnicastAddresses)
            .Select(a => a.Address)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
    }

    /// <summary>Asks for admin rights once to let the phone reach Orange through Windows Firewall.</summary>
    public static bool OpenFirewall()
    {
        var port = Settings.Current.PhonePort;
        // Clicking away Windows' "allow this app?" prompt leaves BLOCK rules for Orange.exe, and block wins
        // over allow, so remove those first. Then allow only our port.
        var args = $"advfirewall firewall delete rule name=all dir=in program=\"{Environment.ProcessPath}\" & " +
                   $"netsh advfirewall firewall delete rule name=\"Orange phone sharing\" & " +
                   $"netsh advfirewall firewall add rule name=\"Orange phone sharing\" dir=in action=allow protocol=TCP localport={port} profile=private,public";
        try
        {
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c netsh {args}")
            {
                Verb = "runas", UseShellExecute = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            });
            p?.WaitForExit(15000);
            return p?.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false; // the user said no to the admin prompt
        }
    }

    private static string MimeOf(string name) => FileKinds.Ext(name) switch
    {
        "jpg" or "jpeg" => "image/jpeg", "png" => "image/png", "gif" => "image/gif", "webp" => "image/webp",
        "heic" => "image/heic", "mp4" or "m4v" => "video/mp4", "mov" => "video/quicktime", "mp3" => "audio/mpeg",
        "m4a" => "audio/mp4", "wav" => "audio/wav", "pdf" => "application/pdf", "txt" => "text/plain; charset=utf-8",
        "zip" => "application/zip",
        "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        _ => "application/octet-stream",
    };
}
