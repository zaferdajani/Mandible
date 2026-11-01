using Godot;

namespace Mandible;

/// Player settings kept in user://settings.cfg. Phones and tablets start on Low.
public static class Settings
{
    public enum Quality { Low, Medium, High, Ultra }
    public static Quality Gfx = Default();
    public static bool Mobile => OS.HasFeature("mobile");
    public static bool Portrait; // the screen is taller than wide (a phone held upright)
    public static bool Touch => Mobile || DisplayServer.IsTouchscreenAvailable();
    const string Path = "user://settings.cfg";

    static Quality Default() => OS.HasFeature("mobile") ? Quality.Low : Quality.High;

    /// Computers start fullscreen: in a window the mouse slides off the edge instead of scrolling the map.
    public static bool Fullscreen = true;

    /// Short descriptions of each tier, for the settings panel.
    public static string Describe(Quality q) => q switch
    {
        Quality.Low => "Phones and old laptops. Hard sun shadows; workers and soldiers cast none. No ambient occlusion, plain soil, a third of the pebbles and grass.",
        Quality.Medium => "Integrated graphics. Shadows on every ant, bloom, two thirds of the ground litter. No ambient occlusion.",
        Quality.High => "Dedicated graphics (the default on computers). Crisp four-split shadows, ambient occlusion and bounced light, 2x MSAA, detailed soil.",
        Quality.Ultra => "A strong graphics card. Soft sun shadows out to the horizon, richer soil grain, dust haze in the air, 4x MSAA, every pebble and blade, and the straw casts shadows too.",
        _ => "",
    };

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok) return;
        Gfx = (Quality)Mathf.Clamp((int)cfg.GetValue("graphics", "quality", (int)Default()), 0, (int)Quality.Ultra);
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

    /// Fraction of grass, pebbles and straw to scatter (purely visual; takes effect when a world is built).
    public static float Density => Gfx switch { Quality.Low => 0.35f, Quality.Medium => 0.7f, Quality.High => 1f, _ => 1.35f };

    // What each tier turns on. Everything here is rendering only and can be switched while a battle runs.
    public static bool Ssao => Gfx >= Quality.High;
    public static bool Ssil => Gfx >= Quality.High;
    public static bool Glow => Gfx >= Quality.Medium;
    public static bool Haze => Gfx == Quality.Ultra;              // volumetric dust in the air
    public static bool SmallAntShadows => Gfx >= Quality.Medium;  // workers and soldiers (heroes, bugs and the queen always cast)
    public static bool PebbleShadows => Gfx >= Quality.High;
    public static bool StrawShadows => Gfx == Quality.Ultra;
    public static bool GrassShadows => Gfx >= Quality.Medium;
    /// Terrain shader variant: 0 plain soil, 1 detailed, 2 richer still (extra grain octave, anisotropic sampling).
    public static int TerrainDetail => Gfx switch { Quality.Low => 0, Quality.Ultra => 2, _ => 1 };
}
