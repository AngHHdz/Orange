using System.Net;
using System.Text.Json;

namespace Orange.Core.Share;

/// <summary>The single web page the phone sees. Self-contained (no external scripts), dark, thumb-friendly.</summary>
public static class PhonePage
{
    /// <param name="host">"192.168.0.181:8765" as the phone reached us — used to build the Shortcut URL.</param>
    public static string Render(string token, string host)
    {
        string T(string s) => WebUtility.HtmlEncode(Loc.T(s));
        // Strings the script needs, JSON-encoded so quotes and accents are safe.
        var js = JsonSerializer.Serialize(new
        {
            key = token,
            open = Loc.T("Open"), download = Loc.T("Download"), remove = Loc.T("Remove"),
            sending = Loc.T("Sending"), sent = Loc.T("Sent"), failed = Loc.T("Failed"),
            empty = Loc.F("Nothing here yet. On the PC, drag a file, press {0} and drop it on “iPhone”.",
                Settings.KeyName(Settings.Current.ActionsKey)),
        });

        return Template
            .Replace("{{title}}", T("Orange"))
            .Replace("{{subtitle}}", WebUtility.HtmlEncode(Loc.F("Wi-Fi sharing with {0}", Environment.MachineName)))
            .Replace("{{send}}", T("Send to PC"))
            .Replace("{{sendHint}}", T("Photos, videos or any file"))
            .Replace("{{fromPc}}", T("From your PC"))
            .Replace("{{homeTip}}", T("Tip: tap Share, then “Add to Home Screen” to keep Orange one tap away."))
            .Replace("{{shortcutTitle}}", T("Share-menu shortcut"))
            .Replace("{{shortcutHint}}", T("Paste this address in the “Get Contents of URL” action:"))
            .Replace("{{copy}}", T("Copy"))
            .Replace("{{copied}}", T("Copied ✓"))
            .Replace("{{uploadUrl}}", WebUtility.HtmlEncode($"http://{host}/api/upload?k={Uri.EscapeDataString(token)}"))
            .Replace("{{data}}", js);
    }

    private const string Template = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<meta name="apple-mobile-web-app-capable" content="yes">
<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent">
<meta name="apple-mobile-web-app-title" content="Orange">
<meta name="theme-color" content="#161618">
<link rel="apple-touch-icon" href="/icon.png">
<title>{{title}}</title>
<style>
  :root { --bg:#161618; --card:#222226; --line:#2e2e33; --text:#f2f2f2; --muted:#9a9aa2; --accent:#ff8a1f; --ok:#4caf50; --bad:#ef5350; }
  * { box-sizing:border-box; -webkit-tap-highlight-color:transparent; }
  body { margin:0; background:var(--bg); color:var(--text); font:16px/1.4 -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
         padding:calc(env(safe-area-inset-top) + 20px) 16px calc(env(safe-area-inset-bottom) + 24px); }
  header { display:flex; align-items:center; gap:12px; margin-bottom:20px; }
  .logo { width:40px; height:40px; border-radius:50%; background:linear-gradient(135deg,#ffb040,#f06e0a); flex:none; }
  h1 { font-size:24px; margin:0; }
  .sub { color:var(--muted); font-size:13px; }
  .send { display:block; width:100%; border:0; border-radius:16px; padding:20px; background:var(--accent); color:#fff;
          font-size:19px; font-weight:600; text-align:center; cursor:pointer; }
  .send small { display:block; font-weight:400; font-size:13px; opacity:.85; margin-top:2px; }
  input[type=file] { display:none; }
  h2 { font-size:15px; text-transform:uppercase; letter-spacing:.05em; color:var(--muted); margin:28px 4px 10px; }
  .list { background:var(--card); border-radius:14px; overflow:hidden; }
  .row { display:flex; align-items:center; gap:10px; padding:12px 14px; border-top:1px solid var(--line); }
  .row:first-child { border-top:0; }
  .name { flex:1; min-width:0; }
  .name b { display:block; font-weight:500; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
  .name span { color:var(--muted); font-size:12px; }
  .btn { border:0; border-radius:10px; padding:8px 11px; background:#34343a; color:var(--text); font-size:14px; text-decoration:none; white-space:nowrap; }
  .btn.primary { background:var(--accent); color:#fff; }
  .btn.x { background:transparent; color:var(--muted); padding:8px 4px; font-size:18px; }
  .empty { padding:18px 14px; color:var(--muted); font-size:14px; }
  .bar { height:4px; background:#34343a; border-radius:2px; margin-top:6px; overflow:hidden; }
  .bar i { display:block; height:100%; width:0; background:var(--accent); transition:width .2s; }
  .ok { color:var(--ok); } .bad { color:var(--bad); }
  #uploads:empty { display:none; }
  #uploads { margin-top:12px; }
  .tip { color:var(--muted); font-size:12px; margin:26px 4px 0; text-align:center; }
</style>
</head>
<body>
<header>
  <div class="logo"></div>
  <div><h1>{{title}}</h1><div class="sub">{{subtitle}}</div></div>
</header>

<label class="send">{{send}}<small>{{sendHint}}</small><input id="picker" type="file" multiple></label>
<div id="uploads" class="list"></div>

<h2>{{fromPc}}</h2>
<div id="files" class="list"><div class="empty">…</div></div>

<p class="tip" id="tip">{{homeTip}}</p>

<h2>{{shortcutTitle}}</h2>
<div class="list"><div class="row" style="flex-wrap:wrap">
  <span class="sub" style="width:100%">{{shortcutHint}}</span>
  <input id="sc" readonly value="{{uploadUrl}}" style="flex:1;min-width:0;border:0;border-radius:8px;padding:8px;background:#34343a;color:var(--text);font:12px ui-monospace,Menlo,monospace">
  <button class="btn primary" id="copyBtn" data-done="{{copied}}">{{copy}}</button>
</div></div>

<script>
const D = {{data}};
const api = p => p + (p.includes('?') ? '&' : '?') + 'k=' + encodeURIComponent(D.key);
const esc = s => s.replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const size = n => n < 1024 ? n + ' B' : n < 1048576 ? (n/1024).toFixed(0) + ' KB' : n < 1073741824 ? (n/1048576).toFixed(1) + ' MB' : (n/1073741824).toFixed(2) + ' GB';
const viewable = n => /\.(jpe?g|png|gif|webp|heic|mp4|mov|m4v|pdf|txt|mp3|m4a)$/i.test(n);

if (navigator.standalone) document.getElementById('tip').remove();

async function load() {
  try {
    const r = await fetch(api('/api/files'), { cache: 'no-store' });
    if (!r.ok) return;
    const items = await r.json();
    const box = document.getElementById('files');
    if (!items.length) { box.innerHTML = '<div class="empty">' + esc(D.empty) + '</div>'; return; }
    box.innerHTML = items.map(f =>
      '<div class="row"><div class="name"><b>' + esc(f.name) + '</b><span>' + size(f.size) + '</span></div>' +
      (viewable(f.name) ? '<a class="btn" href="' + api('/api/files/' + f.id + '?inline=true') + '" target="_blank">' + esc(D.open) + '</a>' : '') +
      '<a class="btn primary" href="' + api('/api/files/' + f.id) + '" download="' + esc(f.name) + '">' + esc(D.download) + '</a>' +
      '<button class="btn x" title="' + esc(D.remove) + '" onclick="removeFile(\'' + f.id + '\')">✕</button></div>').join('');
  } catch (e) { /* PC asleep or out of Wi-Fi; try again on the next tick */ }
}

async function removeFile(id) {
  await fetch(api('/api/files/' + id), { method: 'DELETE' });
  load();
}

function upload(file) {
  return new Promise(resolve => {
    const row = document.createElement('div');
    row.className = 'row';
    row.innerHTML = '<div class="name"><b>' + esc(file.name) + '</b><span>' + esc(D.sending) + ' · ' + size(file.size) +
                    '</span><div class="bar"><i></i></div></div>';
    document.getElementById('uploads').prepend(row);
    const bar = row.querySelector('i'), label = row.querySelector('span');

    const form = new FormData();
    form.append('file', file, file.name);
    const xhr = new XMLHttpRequest();
    xhr.open('POST', api('/api/upload'));
    xhr.upload.onprogress = e => { if (e.lengthComputable) bar.style.width = (e.loaded / e.total * 100) + '%'; };
    xhr.onload = () => {
      const ok = xhr.status >= 200 && xhr.status < 300;
      label.className = ok ? 'ok' : 'bad';
      label.textContent = (ok ? '✓ ' + D.sent : '✕ ' + D.failed) + ' · ' + size(file.size);
      bar.parentElement.remove();
      resolve();
    };
    xhr.onerror = () => { label.className = 'bad'; label.textContent = '✕ ' + D.failed; resolve(); };
    xhr.send(form);
  });
}

document.getElementById('picker').addEventListener('change', async e => {
  for (const f of e.target.files) await upload(f);   // one at a time: kinder to Wi-Fi
  e.target.value = '';
});

// The page is plain http on the LAN, where navigator.clipboard is unavailable — select + execCommand still works on iOS.
document.getElementById('copyBtn').addEventListener('click', e => {
  const sc = document.getElementById('sc');
  sc.focus(); sc.setSelectionRange(0, sc.value.length);
  let ok = false;
  try { ok = document.execCommand('copy'); } catch (err) {}
  sc.blur();
  if (ok) e.target.textContent = e.target.dataset.done;
});

load();
setInterval(() => { if (document.visibilityState === 'visible') load(); }, 3000);
document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') load(); });
</script>
</body>
</html>
""";
}
