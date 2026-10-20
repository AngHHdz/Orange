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

        _singleInstance = new Mutex(true, "Orange.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        Tools.Initialize();

        _wheel = new WheelWindow();
        _watcher = new DragWatcher();
        _watcher.WheelRequested += (mode, x, y) => _wheel.ShowAt(mode, x, y);
        _watcher.ModeChanged += mode => { if (_wheel.IsVisible) _wheel.SetMode(mode); };
        _watcher.DragEnded += () => _wheel.HideIfIdle();
        _watcher.CancelRequested += () => _wheel.HideWheel();
        _watcher.MouseDown += (x, y) => _wheel.OnGlobalMouseDown(x, y);
        _watcher.Start();

        CreateTray();

        Notify("Orange is running",
            "Drag a file and press Shift (convert), Ctrl (tools) or Alt (actions).");
    }

    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Convert files…", null, (_, _) => PickAndShowMenu());
        menu.Items.Add("Open output log", null, (_, _) => JobRunner.OpenLog());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit Orange", null, (_, _) => Shutdown());

        _tray = new Forms.NotifyIcon
        {
            Icon = IconFactory.CreateTrayIcon(),
            Text = "Orange — file converter",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => PickAndShowMenu();
        _tray.BalloonTipClicked += (_, _) => JobRunner.RevealLastOutput();
    }

    // Fallback flow without dragging: pick files, then open the wheel in the middle of the screen.
    private void PickAndShowMenu()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = "Choose files for Orange" };
        if (dlg.ShowDialog() != true) return;
        var pos = Forms.Cursor.Position;
        _wheel!.ShowForFiles(dlg.FileNames, WheelMode.Convert, pos.X, pos.Y);
    }

    public void Notify(string title, string text, bool error = false)
    {
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
