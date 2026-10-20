using System.Drawing;
using System.Drawing.Drawing2D;

namespace Orange.UI;

/// <summary>Draws the tray icon at runtime (an orange with a leaf), so no .ico file is needed yet.</summary>
public static class IconFactory
{
    public static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var fruit = new LinearGradientBrush(new Rectangle(3, 6, 26, 26), Color.FromArgb(255, 176, 64), Color.FromArgb(240, 110, 10), 60f);
            g.FillEllipse(fruit, 3, 6, 26, 24);
            using var leaf = new SolidBrush(Color.FromArgb(76, 175, 80));
            g.FillEllipse(leaf, 16, 1, 11, 7);
            using var shine = new SolidBrush(Color.FromArgb(90, 255, 255, 255));
            g.FillEllipse(shine, 9, 11, 7, 5);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
