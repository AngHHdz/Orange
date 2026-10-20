using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Orange.Core;
using Orange.Input;
using Path = System.Windows.Shapes.Path;

namespace Orange.UI;

/// <summary>
/// The radial menu. Two ways to use it:
///  • Drag mode: opened by <see cref="DragWatcher"/> while dragging files; the user drops on a slice.
///  • Click mode: opened from the tray with files already chosen; the user clicks a slice.
/// </summary>
public partial class WheelWindow : Window
{
    private const double Cx = 200, Cy = 200, OuterR = 190, InnerR = 64;

    /// <summary>Visual size of the wheel (1 = 400 px), from Settings. Hit-testing works in unscaled coordinates, so only this changes.</summary>
    private static double WheelScale => Settings.Current.WheelScale;

    private static readonly Brush SliceBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xEB, 0x22, 0x22, 0x26)));
    private static readonly Brush SliceHotBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x1F)));
    private static readonly Brush TabOn = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x1F)));
    private static readonly Brush TabOff = Freeze(new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF)));

    private WheelMode _mode;
    private IReadOnlyList<string> _files = [];
    private IReadOnlyList<WheelItem> _items = [];
    private readonly List<Path> _slices = [];
    private int _hot = -1;
    private bool _clickMode;

    public WheelWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            // Never steal focus from Explorer while the user is dragging.
            var hwnd = new WindowInteropHelper(this).Handle;
            int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
            Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW);
        };

        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        DragLeave += (_, _) => SetHot(-1);
        Drop += OnDrop;
        MouseMove += (_, e) => { if (_clickMode) SetHot(HitTest(e.GetPosition(Ring))); };
        MouseLeftButtonUp += OnClick;
    }

    // ── Showing / hiding ──────────────────────────────────────────────────

    public void ShowAt(WheelMode mode, int screenX, int screenY)
    {
        _clickMode = false;
        _files = [];
        Open(mode, screenX, screenY);
    }

    public void ShowForFiles(IReadOnlyList<string> files, WheelMode mode, int screenX, int screenY)
    {
        _clickMode = true;
        _files = files;
        Open(mode, screenX, screenY);
    }

    private void Open(WheelMode mode, int x, int y)
    {
        SetMode(mode);
        if (!IsVisible) Show();

        // Hook coordinates are physical pixels; place the wheel's center under the cursor.
        var dpi = VisualTreeHelper.GetDpi(this);
        var hwnd = new WindowInteropHelper(this).Handle;
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST,
            (int)(x - Cx * dpi.DpiScaleX), (int)(y - Cy * dpi.DpiScaleY), 0, 0,
            Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);

        Animate(1, WheelScale);
    }

    public void HideWheel()
    {
        if (!IsVisible) return;
        _files = [];
        _clickMode = false;
        Root.Opacity = 0;
        Hide();
    }

    /// <summary>Called when the mouse button is released. A drop on the wheel arrives right after,
    /// so wait a moment before hiding.</summary>
    public async void HideIfIdle()
    {
        if (_clickMode) return;
        await Task.Delay(300);
        if (!_clickMode) HideWheel();
    }

    /// <summary>In click mode, a click anywhere outside the wheel closes it.</summary>
    public void OnGlobalMouseDown(int x, int y)
    {
        if (!_clickMode || !IsVisible) return;
        Native.GetWindowRect(new WindowInteropHelper(this).Handle, out var r);
        if (x < r.Left || x > r.Right || y < r.Top || y > r.Bottom) HideWheel();
    }

    public void SetMode(WheelMode mode)
    {
        _mode = mode;
        var s = Settings.Current;
        TabConvert.Text = $"{Settings.KeyName(s.ConvertKey)} · {Loc.T("Convert")}";
        TabTools.Text = $"{Settings.KeyName(s.ToolsKey)} · {Loc.T("Tools")}";
        TabActions.Text = $"{Settings.KeyName(s.ActionsKey)} · {Loc.T("Actions")}";
        TabConvert.Foreground = mode == WheelMode.Convert ? TabOn : TabOff;
        TabTools.Foreground = mode == WheelMode.Tools ? TabOn : TabOff;
        TabActions.Foreground = mode == WheelMode.Actions ? TabOn : TabOff;
        Rebuild();
    }

    // ── Drag & drop ───────────────────────────────────────────────────────

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            e.Effects = DragDropEffects.None;
            CenterTitle.Text = Loc.T("Files only");
            CenterSub.Text = Loc.T("drag files or folders");
            e.Handled = true;
            return;
        }

        if (!_files.SequenceEqual(files))
        {
            _files = files;
            Rebuild();
        }

        int hit = HitTest(e.GetPosition(Ring));
        SetHot(hit);
        // Always Copy: Orange never takes the file away from where it came from.
        e.Effects = hit >= 0 && e.AllowedEffects.HasFlag(DragDropEffects.Copy) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        int hit = HitTest(e.GetPosition(Ring));
        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
        Execute(hit, files);
    }

    private void OnClick(object sender, MouseButtonEventArgs e)
    {
        if (!_clickMode) return;
        Execute(HitTest(e.GetPosition(Ring)), _files.ToArray());
    }

    private void Execute(int index, string[]? files)
    {
        var item = index >= 0 && index < _items.Count ? _items[index] : null;
        HideWheel();
        if (item == null || files == null || files.Length == 0) return;

        // Let the drag/drop call return to Explorer first, then do the work.
        Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                if (item.OnUiThread) await item.Run(files);
                else await Task.Run(() => item.Run(files));
            }
            catch (Exception ex)
            {
                JobRunner.Log($"FAIL {item.Label}: {ex}");
                App.Instance.Notify(item.Label, ex.Message, error: true);
            }
        });
    }

    // ── Drawing ───────────────────────────────────────────────────────────

    private void Rebuild()
    {
        _items = Catalog.For(_mode, _files);
        _slices.Clear();
        _hot = -1;
        Ring.Children.Clear();

        // Invisible disc so drops in the center land on Orange (= cancel) instead of the window below.
        var catcher = new Ellipse { Width = OuterR * 2, Height = OuterR * 2, Fill = Freeze(new SolidColorBrush(Color.FromArgb(1, 0, 0, 0))) };
        Canvas.SetLeft(catcher, Cx - OuterR);
        Canvas.SetTop(catcher, Cy - OuterR);
        Ring.Children.Add(catcher);

        int n = _items.Count;
        if (n == 0)
        {
            var ring = new Path { Data = Donut(), Fill = SliceBrush };
            Ring.Children.Add(ring);
            ShowCenter();
            return;
        }

        double step = 2 * Math.PI / n;
        double gap = n > 1 ? 0.012 : 0;
        for (int i = 0; i < n; i++)
        {
            double a0 = i * step - step / 2 + gap, a1 = a0 + step - 2 * gap;
            var slice = new Path { Data = n == 1 ? Donut() : Slice(a0, a1), Fill = SliceBrush };
            _slices.Add(slice);
            Ring.Children.Add(slice);

            // Crowded wheels: labels sit further out, where the slices are wider.
            double mid = i * step, r = (OuterR + InnerR) / 2 + (n >= 10 ? 10 : 4);
            AddLabel(_items[i], Cx + r * Math.Sin(mid), Cy - r * Math.Cos(mid), n);
        }
        ShowCenter();
    }

    private void AddLabel(WheelItem item, double x, double y, int count)
    {
        // With 10+ slices neighbours are only ~70 px apart, so labels get narrower, smaller and may wrap
        // ("Marca de agua…" becomes two lines) instead of overlapping the next slice.
        bool crowded = count >= 10;
        double width = crowded ? 74 : 90;
        var label = Loc.T(item.Label);
        // A single long word ("Desbloquear…") can't wrap nicely, so it gets a smaller font instead.
        double labelSize = !crowded ? 15 : !label.Contains(' ') && label.Length > 10 ? 11 : 12.5;
        var panel = new StackPanel { IsHitTestVisible = false, Width = width };
        panel.Children.Add(new TextBlock
        {
            Text = label, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold,
            FontSize = labelSize, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
            LineHeight = crowded ? 14 : double.NaN,
        });
        panel.Children.Add(new TextBlock
        {
            Text = Loc.T(item.Hint), Foreground = TabOff, FontSize = crowded ? 9.5 : 10,
            TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
        });
        panel.Measure(new Size(width, double.PositiveInfinity));
        Canvas.SetLeft(panel, x - width / 2);
        Canvas.SetTop(panel, y - panel.DesiredSize.Height / 2);
        Ring.Children.Add(panel);
    }

    private void SetHot(int index)
    {
        if (index == _hot) return;
        if (_hot >= 0 && _hot < _slices.Count) _slices[_hot].Fill = SliceBrush;
        _hot = index;
        if (_hot >= 0 && _hot < _slices.Count) _slices[_hot].Fill = SliceHotBrush;
        ShowCenter();
    }

    private void ShowCenter()
    {
        if (_files.Count == 0 && !_clickMode)
        {
            CenterTitle.Text = Loc.T(_mode.ToString());
            CenterSub.Text = Loc.T("move over the wheel");
        }
        else if (_items.Count == 0)
        {
            CenterTitle.Text = Loc.T(_mode.ToString());
            CenterSub.Text = Loc.T("nothing for this file yet");
        }
        else if (_hot >= 0)
        {
            CenterTitle.Text = Loc.T(_items[_hot].Label);
            CenterSub.Text = _files.Count == 1 ? System.IO.Path.GetFileName(_files[0]) : Loc.F("{0} files", _files.Count);
        }
        else
        {
            CenterTitle.Text = Loc.T("Cancel");
            CenterSub.Text = _files.Count == 1 ? System.IO.Path.GetFileName(_files[0]) : Loc.F("{0} files", _files.Count);
        }
    }

    /// <summary>Which slice is under a point (in Ring coordinates), or -1 for the center / outside.</summary>
    private int HitTest(Point p)
    {
        int n = _items.Count;
        if (n == 0) return -1;
        double dx = p.X - Cx, dy = p.Y - Cy, dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < InnerR || dist > OuterR + 30) return -1;

        double angle = Math.Atan2(dx, -dy);            // 0 = up, clockwise
        if (angle < 0) angle += 2 * Math.PI;
        double step = 2 * Math.PI / n;
        return (int)((angle + step / 2) % (2 * Math.PI) / step);
    }

    private static Geometry Slice(double a0, double a1)
    {
        Point P(double a, double r) => new(Cx + r * Math.Sin(a), Cy - r * Math.Cos(a));
        bool large = a1 - a0 > Math.PI;
        var fig = new PathFigure { StartPoint = P(a0, OuterR), IsClosed = true };
        fig.Segments.Add(new ArcSegment(P(a1, OuterR), new Size(OuterR, OuterR), 0, large, SweepDirection.Clockwise, true));
        fig.Segments.Add(new LineSegment(P(a1, InnerR), true));
        fig.Segments.Add(new ArcSegment(P(a0, InnerR), new Size(InnerR, InnerR), 0, large, SweepDirection.Counterclockwise, true));
        var g = new PathGeometry([fig]);
        g.Freeze();
        return g;
    }

    private static Geometry Donut()
    {
        var g = new CombinedGeometry(GeometryCombineMode.Exclude,
            new EllipseGeometry(new Point(Cx, Cy), OuterR, OuterR),
            new EllipseGeometry(new Point(Cx, Cy), InnerR, InnerR));
        g.Freeze();
        return g;
    }

    private void Animate(double opacity, double scale)
    {
        var d = new Duration(TimeSpan.FromMilliseconds(140));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, opacity, d) { EasingFunction = ease });
        Zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale * 0.85, scale, d) { EasingFunction = ease });
        Zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale * 0.85, scale, d) { EasingFunction = ease });
    }

    private static T Freeze<T>(T f) where T : Freezable { f.Freeze(); return f; }
}
