using Godot;

namespace Mandible;

/// Player settings kept in user://settings.cfg. Phones and tablets start on Low.
public static class Settings
{
    public enum Quality { Low, Medium, High }
    public static Quality Gfx = Default();
    public static bool Mobile => OS.HasFeature("mobile");
    public static bool Portrait; // the screen is taller than wide (a phone held upright)
    public static bool Touch => Mobile || DisplayServer.IsTouchscreenAvailable();
    const string Path = "user://settings.cfg";

    static Quality Default() => OS.HasFeature("mobile") ? Quality.Low : Quality.High;

    /// Computers start fullscreen: in a window the mouse slides off the edge instead of scrolling the map.
    public static bool Fullscreen = true;

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok) return;
        Gfx = (Quality)(int)cfg.GetValue("graphics", "quality", (int)Default());
        Fullscreen = (bool)cfg.GetValue("display", "fullscreen", true);
    }

    public static void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("graphics", "quality", (int)Gfx);
        cfg.SetValue("display", "fullscreen", Fullscreen);
        cfg.Save(Path);
    }

    /// Applies (and remembers) fullscreen or windowed on computers.
    public static void ApplyDisplay(bool save = false)
    {
        if (Mobile) return;
        DisplayServer.WindowSetMode(Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        if (save) Save();
    }

    /// Fraction of grass, pebbles and straw to scatter (purely visual).
    public static float Density => Gfx switch { Quality.Low => 0.35f, Quality.Medium => 0.7f, _ => 1f };
}
