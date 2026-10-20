using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Orange.UI;

/// <summary>Draws the Orange logo (an orange with a leaf) at runtime, so no image files are needed yet.</summary>
public static class IconFactory
{
    public static Icon CreateTrayIcon()
    {
        using var bmp = Draw(32, transparent: true);
        return Icon.FromHandle(bmp.GetHicon());
    }

    /// <summary>Square PNG on a dark background (iPhone home-screen icon).</summary>
    public static byte[] CreatePng(int size)
    {
        using var bmp = Draw(size, transparent: false);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private static Bitmap Draw(int size, bool transparent)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(transparent ? Color.Transparent : Color.FromArgb(22, 22, 24));

        // Layout designed on a 32×32 grid, scaled to the requested size (with padding when opaque).
        float pad = transparent ? 0 : size * 0.12f;
        float s = (size - 2 * pad) / 32f;
        RectangleF R(float x, float y, float w, float h) => new(pad + x * s, pad + y * s, w * s, h * s);

        var body = R(3, 6, 26, 24);
        using var fruit = new LinearGradientBrush(body, Color.FromArgb(255, 176, 64), Color.FromArgb(240, 110, 10), 60f);
        g.FillEllipse(fruit, body);
        using var leaf = new SolidBrush(Color.FromArgb(76, 175, 80));
        g.FillEllipse(leaf, R(16, 1, 11, 7));
        using var shine = new SolidBrush(Color.FromArgb(90, 255, 255, 255));
        g.FillEllipse(shine, R(9, 11, 7, 5));
        return bmp;
    }
}
