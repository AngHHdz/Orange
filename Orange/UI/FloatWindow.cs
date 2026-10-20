using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Orange.UI;

/// <summary>"Float" action: pins an image on top of every window. Drag to move,
/// scroll to resize, double-click or Esc to close.</summary>
public sealed class FloatWindow : Window
{
    public FloatWindow(string imagePath)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Title = System.IO.Path.GetFileName(imagePath);

        var bmp = Load(imagePath);

        double scale = Math.Min(1, 420 / Math.Max(bmp.PixelWidth, bmp.PixelHeight));
        var image = new Image { Source = bmp, Width = bmp.PixelWidth * scale, Height = bmp.PixelHeight * scale, Stretch = Stretch.Uniform };

        Content = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0x8A, 0x1F)),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x1E, 0x1E, 0x22)),
            Padding = new Thickness(4),
            Child = image,
            ToolTip = $"{Title}\nDrag to move · Scroll to resize · Double-click to close"
        };

        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) Close();
            else DragMove();
        };
        MouseWheel += (_, e) =>
        {
            double f = e.Delta > 0 ? 1.1 : 0.9;
            image.Width = Math.Clamp(image.Width * f, 60, 3000);
            image.Height = Math.Clamp(image.Height * f, 60, 3000);
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private static BitmapImage Load(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        bool wpfCanRead = ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".ico";

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad; // don't lock the file
        if (wpfCanRead)
        {
            bmp.UriSource = new Uri(path);
        }
        else
        {
            // HEIC, AVIF, WebP, SVG…: let Magick.NET decode it into an in-memory PNG.
            using var img = new ImageMagick.MagickImage(path);
            img.AutoOrient();
            bmp.StreamSource = new System.IO.MemoryStream(img.ToByteArray(ImageMagick.MagickFormat.Png));
        }
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
