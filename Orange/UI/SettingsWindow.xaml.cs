using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Orange.Core;

namespace Orange.UI;

public partial class SettingsWindow : Window
{
    private static SettingsWindow? _open;

    private static readonly ModKey[] Keys = [ModKey.Shift, ModKey.Ctrl, ModKey.Alt];

    /// <summary>Shows the window, or brings the already-open one to the front.</summary>
    public static void ShowSingle()
    {
        if (_open != null)
        {
            _open.Activate();
            return;
        }
        _open = new SettingsWindow();
        _open.Closed += (_, _) => _open = null;
        _open.Show();
        _open.Activate();
    }

    private SettingsWindow()
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height - 40; // scroll instead of running off small screens
        var s = Settings.Current;

        LanguageBox.ItemsSource = Loc.Languages;
        LanguageBox.SelectedValue = s.Language;
        LanguageBox.SelectionChanged += (_, _) => ApplyTexts();

        AutostartBox.IsChecked = Autostart.IsEnabled;
        NotifyBox.IsChecked = s.Notifications;

        SizeSlider.ValueChanged += (_, e) => SizeValue.Text = $"{e.NewValue:0} %";
        SizeSlider.Value = Math.Round(s.WheelScale * 100);
        QualitySlider.ValueChanged += (_, e) => QualityValue.Text = $"{e.NewValue:0}";
        QualitySlider.Value = s.ImageQuality;

        foreach (var (box, key) in new[] { (ConvertKeyBox, s.ConvertKey), (ToolsKeyBox, s.ToolsKey), (ActionsKeyBox, s.ActionsKey) })
        {
            box.ItemsSource = Keys.Select(Settings.KeyName).ToList();
            box.SelectedIndex = Array.IndexOf(Keys, key);
            box.SelectionChanged += (_, _) => CheckKeys();
        }

        NextToOriginal.IsChecked = s.Output == OutputMode.NextToOriginal;
        ToFolder.IsChecked = s.Output == OutputMode.Folder;
        FolderBox.Text = s.OutputFolder;
        BrowseButton.Click += (_, _) => Browse();

        PhoneBox.IsChecked = s.PhoneSharing;
        InboxBox.Text = s.PhoneInboxOrDefault;
        InboxBrowse.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = Loc.In(Lang, "Files from the phone go to:") };
            if (dlg.ShowDialog(this) == true) InboxBox.Text = dlg.FolderName;
        };
        PairButton.Click += (_, _) => PairWindow.ShowSingle();

        SaveButton.Click += (_, _) => Save();
        ApplyTexts();
        ShowEngines();
    }

    private string Lang => LanguageBox.SelectedValue as string ?? "en";

    private void ApplyTexts()
    {
        string T(string s) => Loc.In(Lang, s);
        Title = TitleText.Text = T("Orange Settings");
        GeneralHeader.Text = T("General");
        LanguageLabel.Text = T("Language");
        AutostartBox.Content = T("Start with Windows");
        NotifyBox.Content = T("Show notifications when a job finishes");
        WheelHeader.Text = T("Wheel");
        SizeLabel.Text = T("Size");
        ConvertKeyLabel.Text = T("Key for Convert");
        ToolsKeyLabel.Text = T("Key for Tools");
        ActionsKeyLabel.Text = T("Key for Actions");
        KeyWarning.Text = T("Each mode needs a different key.");
        OutputHeader.Text = T("Output");
        NextToOriginal.Content = T("Save next to the original file");
        ToFolder.Content = T("Save everything in this folder:");
        BrowseButton.Content = T("Browse…");
        QualityLabel.Text = T("Image quality (JPG, WebP, AVIF)");
        PhoneHeader.Text = T("Phone (Wi-Fi sharing)");
        PhoneBox.Content = T("Share files with my phone over Wi-Fi");
        InboxLabel.Text = T("Files from the phone go to:");
        InboxBrowse.Content = T("Browse…");
        PairButton.Content = T("Connect phone…");
        EnginesHeader.Text = T("Engines");
        SaveButton.Content = T("Save");
        CancelButton.Content = T("Cancel");
        ShowEngines();
    }

    private void ShowEngines()
    {
        string T(string s) => Loc.In(Lang, s);
        var engines = new (string Name, bool Ok, string Detail)[]
        {
            ("ffmpeg", Tools.Ffmpeg != null, Tools.Ffmpeg ?? "winget install Gyan.FFmpeg"),
            ("Pandoc", Tools.Pandoc != null, Tools.Pandoc ?? "winget install JohnMacFarlane.Pandoc"),
            ("Microsoft Word", Office.HasWord, ""),
            ("Microsoft Excel", Office.HasExcel, ""),
            ("Microsoft PowerPoint", Office.HasPowerPoint, ""),
        };

        EngineList.Items.Clear();
        foreach (var (name, ok, detail) in engines)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock
            {
                Text = ok ? "✓" : "✗", Width = 20, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(ok ? Color.FromRgb(0x2E, 0x7D, 0x32) : Color.FromRgb(0xC6, 0x28, 0x28)),
            });
            row.Children.Add(new TextBlock { Text = name, Width = 160 });
            row.Children.Add(new TextBlock
            {
                Text = ok ? T("found") : $"{T("not found")} — {detail}",
                Foreground = Brushes.Gray, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 290,
                ToolTip = detail.Length > 0 ? detail : null,
            });
            EngineList.Items.Add(row);
        }
    }

    private bool CheckKeys()
    {
        var picked = new[] { ConvertKeyBox.SelectedIndex, ToolsKeyBox.SelectedIndex, ActionsKeyBox.SelectedIndex };
        bool ok = picked.Distinct().Count() == 3;
        KeyWarning.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
        SaveButton.IsEnabled = ok;
        return ok;
    }

    private void Browse()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = Loc.In(Lang, "Choose a folder for the output files.") };
        if (Directory.Exists(FolderBox.Text)) dlg.InitialDirectory = FolderBox.Text;
        if (dlg.ShowDialog(this) != true) return;
        FolderBox.Text = dlg.FolderName;
        ToFolder.IsChecked = true;
    }

    private void Save()
    {
        if (!CheckKeys()) return;
        // Choosing "this folder" without a folder makes no sense — ask for one.
        if (ToFolder.IsChecked == true && !Directory.Exists(FolderBox.Text))
        {
            Browse();
            if (!Directory.Exists(FolderBox.Text)) return;
        }

        var s = Settings.Current.Clone();
        s.Language = Lang;
        s.Notifications = NotifyBox.IsChecked == true;
        s.WheelScale = SizeSlider.Value / 100;
        s.ConvertKey = Keys[ConvertKeyBox.SelectedIndex];
        s.ToolsKey = Keys[ToolsKeyBox.SelectedIndex];
        s.ActionsKey = Keys[ActionsKeyBox.SelectedIndex];
        s.Output = ToFolder.IsChecked == true ? OutputMode.Folder : OutputMode.NextToOriginal;
        s.OutputFolder = FolderBox.Text;
        s.ImageQuality = (int)QualitySlider.Value;
        s.PhoneSharing = PhoneBox.IsChecked == true;
        s.PhoneInbox = InboxBox.Text;
        bool phoneChanged = s.PhoneSharing != Settings.Current.PhoneSharing;
        Settings.Save(s);
        if (phoneChanged) _ = Orange.Core.Share.PhoneBridge.RestartAsync(); // starts or stops the server

        Autostart.Set(AutostartBox.IsChecked == true);
        Close();
    }
}
