using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orange.Core;

public enum ModKey { Shift, Ctrl, Alt }

public enum OutputMode { NextToOriginal, Folder }

/// <summary>User preferences, stored as JSON in %LOCALAPPDATA%\Orange\settings.json.</summary>
public sealed class Settings
{
    public string Language { get; set; } = "en";
    public double WheelScale { get; set; } = 0.8;
    public ModKey ConvertKey { get; set; } = ModKey.Shift;
    public ModKey ToolsKey { get; set; } = ModKey.Ctrl;
    public ModKey ActionsKey { get; set; } = ModKey.Alt;
    public OutputMode Output { get; set; } = OutputMode.NextToOriginal;
    public string OutputFolder { get; set; } = "";
    public int ImageQuality { get; set; } = 90;
    public bool Notifications { get; set; } = true;

    private static readonly string FilePath = Path.Combine(Paths.AppData, "settings.json");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static Settings Current { get; private set; } = Load();

    /// <summary>Raised after <see cref="Save"/> so open UI can refresh (e.g. a new language).</summary>
    public static event Action? Changed;

    public WheelMode? ModeFor(ModKey key) =>
        key == ConvertKey ? WheelMode.Convert :
        key == ToolsKey ? WheelMode.Tools :
        key == ActionsKey ? WheelMode.Actions : null;

    public ModKey KeyFor(WheelMode mode) => mode switch
    {
        WheelMode.Convert => ConvertKey,
        WheelMode.Tools => ToolsKey,
        _ => ActionsKey,
    };

    public static string KeyName(ModKey key) => key == ModKey.Ctrl ? "Ctrl" : key.ToString();

    private static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return Sanitize(JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new Settings());
        }
        catch (Exception ex)
        {
            JobRunner.Log($"Could not read settings, using defaults: {ex.Message}");
        }
        return new Settings();
    }

    public static void Save(Settings updated)
    {
        Current = Sanitize(updated);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, Json));
        Changed?.Invoke();
    }

    public Settings Clone() => (Settings)MemberwiseClone();

    private static Settings Sanitize(Settings s)
    {
        s.WheelScale = Math.Clamp(s.WheelScale, 0.5, 1.2);
        s.ImageQuality = Math.Clamp(s.ImageQuality, 40, 100);
        if (!Loc.Languages.ContainsKey(s.Language)) s.Language = "en";
        // Every mode needs its own key; fall back to the defaults if two collide.
        if (s.ConvertKey == s.ToolsKey || s.ToolsKey == s.ActionsKey || s.ConvertKey == s.ActionsKey)
            (s.ConvertKey, s.ToolsKey, s.ActionsKey) = (ModKey.Shift, ModKey.Ctrl, ModKey.Alt);
        return s;
    }
}
