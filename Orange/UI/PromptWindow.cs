using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Orange.Core;

namespace Orange.UI;

/// <summary>Small dark dialog that asks for one value (page ranges, a password, watermark text…).</summary>
public sealed class PromptWindow : Window
{
    private readonly TextBox? _text;
    private readonly PasswordBox? _password;

    private PromptWindow(string title, string message, string initial, bool password)
    {
        Title = title;
        Width = 380;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22));
        Foreground = Brushes.White;

        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10), FontSize = 13 });

        Control input;
        if (password)
            input = _password = new PasswordBox { Password = initial };
        else
            input = _text = new TextBox { Text = initial };
        input.Padding = new Thickness(6, 4, 6, 4);
        input.FontSize = 14;
        panel.Children.Add(input);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var ok = new Button { Content = Loc.T("OK"), Width = 90, IsDefault = true, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(0, 4, 0, 4) };
        var cancel = new Button { Content = Loc.T("Cancel"), Width = 90, IsCancel = true, Padding = new Thickness(0, 4, 0, 4) };
        ok.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;

        Loaded += (_, _) =>
        {
            Activate();
            input.Focus();
            _text?.SelectAll();
            Keyboard.Focus(input);
        };
    }

    /// <summary>Returns the typed value, or null if the user cancelled or left it empty.</summary>
    public static string? Ask(string title, string message, string initial = "", bool password = false)
    {
        var w = new PromptWindow(Loc.T(title), Loc.T(message), Loc.T(initial), password);
        if (w.ShowDialog() != true) return null;
        var value = password ? w._password!.Password : w._text!.Text.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
