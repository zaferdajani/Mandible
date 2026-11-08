using Godot;

namespace Mandible;

/// Player settings kept in user://settings.cfg. Phones, tablets, Macs and laptops on integrated graphics start on
/// Low; a computer with its own graphics card starts on High. Until the player picks a tier themselves, a battle
/// that runs slowly steps down on its own (GameSession.WatchFrameRate).
public static class Settings
{
    public enum Quality { Low, Medium, High, Ultra }
    public static Quality Gfx = Default();
    public static bool Mobile => ForceMobile || OS.HasFeature("mobile");
    public static bool ForceMobile; // --mobile: a desktop test run lays out and scales the UI as a phone does
    public static bool Portrait; // a desktop window taller than wide (phones and tablets are locked to landscape)
    public static bool Touch => Mobile || DisplayServer.IsTouchscreenAvailable();
    const string Path = "user://settings.cfg";

    /// The tier a fresh install starts on. Phones and Macs (Apple's chips share memory with the screen, and a Retina
    /// display has three times the pixels of a 1080p one) start on Low, like a laptop's integrated graphics; only a
    /// separate graphics card starts on High.
    static Quality Default()
    {
        if (Mobile || OS.GetName() == "macOS") return Quality.Low;
        var kind = RenderingServer.GetVideoAdapterType();
        return kind == RenderingDevice.DeviceType.DiscreteGpu ? Quality.High : Quality.Low;
    }

    /// True once the player has chosen a tier in the settings: the game never second-guesses that choice.
    public static bool QualityChosen;

    /// Share of the screen's pixels the 3D yard is drawn at before it is upscaled (the menus stay sharp). A phone or a
    /// Retina laptop draws the yard at no more than a laptop screen's resolution: most of a mobile game's speed is
    /// not drawing pixels no one can tell apart.
    public static float RenderScale(int screenHeight)
    {
        float tier = Gfx switch { Quality.Low => 0.8f, Quality.Medium => 0.9f, _ => 1f };
        int most = Gfx switch { Quality.Low => 720, Quality.Medium => 900, Quality.High => 1440, _ => 2160 };
        return Mathf.Clamp(Mathf.Min(tier, (float)most / Mathf.Max(screenHeight, 1)), 0.5f, 1f);
    }

    /// How much of a kill the player sees: Full sprays hemolymph, severs legs and leaves bodies in pieces; Reduced keeps
    /// the fluids light and the bodies whole. Rendering only.
    public enum Detail { Full, Reduced }
    public static Detail CombatDetail = Detail.Full;
    public static bool Severing => CombatDetail == Detail.Full;
    /// Share of the droplets and stains drawn: fewer on Reduced, and fewer on the Low tier phones run.
    public static float GoreBudget => (CombatDetail == Detail.Full ? 1f : 0.4f) * (Gfx == Quality.Low ? 0.45f : 1f);

    /// One-time lessons already shown (bit flags), kept between sessions.
    public static int Tips;
    public const int TipEatRemains = 1, TipEvolved = 2;
    /// True the first time a lesson comes up (and remembers it); false ever after.
    public static bool FirstTime(int tip)
    {
        if ((Tips & tip) != 0) return false;
        Tips |= tip;
        Save();
        return true;
    }

    /// Computers start fullscreen: in a window the mouse slides off the edge instead of scrolling the map.
    public static bool Fullscreen = true;

    /// Master volume, 0 (muted) to 1: the menu's Sound button, the - and = keys and the keyboard's media volume keys.
    public static float Volume = 0.8f;
    public const float VolumeStep = 0.1f;

    /// Sets the master bus to the chosen volume; zero mutes outright.
    public static void ApplyVolume(bool save = false)
    {
        Volume = Mathf.Clamp(Mathf.Snapped(Volume, 0.05f), 0f, 1f);
        AudioServer.SetBusMute(0, Volume <= 0.001f);
        AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(Mathf.Max(Volume, 0.001f)));
        if (save) Save();
    }

    public static string VolumeLabel() => Volume <= 0.001f ? "Sound: off" : $"Sound: {Mathf.RoundToInt(Volume * 100)}%";

    /// Short descriptions of each tier, for the settings panel.
    public static string Describe(Quality q) => q switch
    {
        Quality.Low => "Phones, Macs and laptops (the default there). The yard is drawn at a lower resolution and sharpened. Hard sun shadows; workers and soldiers cast none. No ambient occlusion, plain soil, a third of the pebbles and grass.",
        Quality.Medium => "Integrated graphics and newer Macs. Shadows on every ant, bloom, two thirds of the ground litter. No ambient occlusion.",
        Quality.High => "Dedicated graphics (the default on computers with one). Crisp four-split shadows, ambient occlusion and bounced light, 2x MSAA, detailed soil.",
        Quality.Ultra => "A strong graphics card. Soft sun shadows out to the horizon, richer soil grain, dust haze in the air, 4x MSAA, every pebble and blade, and the straw casts shadows too.",
        _ => "",
    };

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok) return;
        Gfx = (Quality)Mathf.Clamp((int)cfg.GetValue("graphics", "quality", (int)Default()), 0, (int)Quality.Ultra);
        Fullscreen = (bool)cfg.GetValue("display", "fullscreen", true);
        CombatDetail = (Detail)Mathf.Clamp((int)cfg.GetValue("graphics", "combat_detail", 0), 0, 1);
        Tips = (int)cfg.GetValue("tips", "shown", 0);
        Volume = (float)(double)cfg.GetValue("audio", "volume", 0.8);
        QualityChosen = (bool)cfg.GetValue("graphics", "chosen", false);
    }

    public static void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("graphics", "quality", (int)Gfx);
        cfg.SetValue("display", "fullscreen", Fullscreen);
        cfg.SetValue("graphics", "combat_detail", (int)CombatDetail);
        cfg.SetValue("tips", "shown", Tips);
        cfg.SetValue("audio", "volume", Volume);
        cfg.SetValue("graphics", "chosen", QualityChosen);
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
