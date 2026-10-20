using System.Windows;
using Orange.Core;
using Orange.Input;
using Orange.UI;
using Forms = System.Windows.Forms;

namespace Orange;

public partial class App : Application
{
    private Forms.NotifyIcon? _tray;
    private DragWatcher? _watcher;
    private WheelWindow? _wheel;
    private Mutex? _singleInstance;

    public static App Instance => (App)Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Windows blocks drag & drop from Explorer into elevated apps (UIPI),
        // so if we were started as admin, restart as a normal user via Explorer.
        if (IsElevated() && !e.Args.Contains("--allow-admin"))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"\"{Environment.ProcessPath}\"");
            Shutdown();
            return;
        }

        _singleInstance = new Mutex(true, "Orange.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        Tools.Initialize();
        PdfSharp.Fonts.GlobalFontSettings.UseWindowsFontsUnderWindows = true; // Arial etc. for page numbers and watermarks

        _wheel = new WheelWindow();
        _watcher = new DragWatcher();
        _watcher.WheelRequested += (mode, x, y) => _wheel.ShowAt(mode, x, y);
        _watcher.ModeChanged += mode => { if (_wheel.IsVisible) _wheel.SetMode(mode); };
        _watcher.DragEnded += () => _wheel.HideIfIdle();
        _watcher.CancelRequested += () => _wheel.HideWheel();
        _watcher.MouseDown += (x, y) => _wheel.OnGlobalMouseDown(x, y);
        _watcher.Start();

        CreateTray();
        Settings.Changed += BuildTrayMenu; // new language → new menu texts

        var s = Settings.Current;
        Notify(Loc.T("Orange is running"), Loc.F("Drag a file and press {0} (convert), {1} (tools) or {2} (actions).",
            Settings.KeyName(s.ConvertKey), Settings.KeyName(s.ToolsKey), Settings.KeyName(s.ActionsKey)), force: true);
    }

    private void CreateTray()
    {
        _tray = new Forms.NotifyIcon
        {
            Icon = IconFactory.CreateTrayIcon(),
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => PickAndShowMenu();
        _tray.BalloonTipClicked += (_, _) => JobRunner.RevealLastOutput();
        BuildTrayMenu();
    }

    private void BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(Loc.T("Convert files…"), null, (_, _) => PickAndShowMenu());
        menu.Items.Add(Loc.T("Settings…"), null, (_, _) => SettingsWindow.ShowSingle());
        menu.Items.Add(Loc.T("Open output log"), null, (_, _) => JobRunner.OpenLog());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Loc.T("Quit Orange"), null, (_, _) => Shutdown());

        var old = _tray!.ContextMenuStrip;
        _tray.ContextMenuStrip = menu;
        _tray.Text = Loc.T("Orange — file converter");
        old?.Dispose();
    }

    // Fallback flow without dragging: pick files, then open the wheel in the middle of the screen.
    private void PickAndShowMenu()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = Loc.T("Choose files for Orange") };
        if (dlg.ShowDialog() != true) return;
        var pos = Forms.Cursor.Position;
        _wheel!.ShowForFiles(dlg.FileNames, WheelMode.Convert, pos.X, pos.Y);
    }

    private static bool IsElevated()
    {
        using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    /// <summary>Tray notification. Errors always show; other messages respect the Notifications setting.</summary>
    public void Notify(string title, string text, bool error = false, bool force = false)
    {
        if (!error && !force && !Settings.Current.Notifications) return;
        _tray?.ShowBalloonTip(3000, title, text, error ? Forms.ToolTipIcon.Error : Forms.ToolTipIcon.Info);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _watcher?.Dispose();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
