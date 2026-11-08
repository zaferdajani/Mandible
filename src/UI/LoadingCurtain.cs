using Godot;

namespace Mandible;

/// The picture between screens: the launch splash's own art, so the game opens on it without a black flash, and a
/// battle is built behind it instead of freezing the menu. It fades once the new battle has drawn a few frames
/// (its shaders compiled behind the picture, where no one sees the stutter).
public partial class LoadingCurtain : CanvasLayer
{
    public static readonly string Art = "res://assets/ui/splash.png";
    Control _root;
    Label _line;
    float _fade = -1, _t;

    public override void _Ready()
    {
        Layer = 100; // above every menu and HUD
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Stop }; // swallows taps while loading
        UiTheme.Place(_root, 0, 0, 1, 1, 0, 0, 0, 0);
        AddChild(_root);
        var art = new TextureRect
        {
            Texture = GD.Load<Texture2D>(Art), MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        };
        UiTheme.Place(art, 0, 0, 1, 1, 0, 0, 0, 0);
        _root.AddChild(art);
        _line = UiTheme.Text("", 22, UiTheme.Title, UiTheme.Ink);
        _line.HorizontalAlignment = HorizontalAlignment.Center;
        _line.AddThemeConstantOverride("outline_size", 8);
        _line.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        UiTheme.Place(_line, 0, 1, 1, 1, 0, -110, 0, -60);
        _root.AddChild(_line);
        Visible = false;
    }

    public void Show(string line = "Loading")
    {
        _line.Text = line.ToUpperInvariant();
        _root.Modulate = Colors.White;
        _fade = -1;
        Visible = true;
    }

    /// Fades out over half a second.
    public void Lift() { if (Visible) _fade = 0.5f; }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _t += (float)delta;
        if (_fade < 0) { _line.Modulate = new Color(1, 1, 1, 0.65f + 0.35f * Mathf.Sin(_t * 3f)); return; }
        _fade -= (float)delta;
        _root.Modulate = new Color(1, 1, 1, Mathf.Clamp(_fade / 0.5f, 0, 1));
        if (_fade <= 0) { Visible = false; _fade = -1; }
    }
}
