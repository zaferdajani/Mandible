using System.Threading.Tasks;
using Godot;

namespace Mandible;

/// App root: owns audio, the main menu and the current session.
public partial class Main : Node
{
    public AudioDirector Audio;
    GameSession _session;
    MainMenu _menu;
    string _capture;

    public override void _Ready()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--capture=")) _capture = a.Substring(10);
            if (a.StartsWith("--simtest="))
            {
                var parts = a.Substring(10).Split(',');
                var da = parts.Length > 1 ? System.Enum.Parse<Difficulty>(parts[1]) : Difficulty.Normal;
                var db = parts.Length > 2 ? System.Enum.Parse<Difficulty>(parts[2]) : Difficulty.Normal;
                Capture.SimTest(this, float.Parse(parts[0]), da, db);
                return;
            }
        }
        Audio = new AudioDirector();
        AddChild(Audio);
        ShowMenu();
        if (_capture != null) _ = Capture.Run(this, _capture);
    }

    void Swap(GameSession s)
    {
        if (_session != null) { _session.QueueFree(); _session = null; }
        _session = s;
        AddChild(s);
    }

    public void ShowMenu()
    {
        Audio.FadeMusic(1);
        Swap(new GameSession { Demo = true, App = this });
        if (_menu == null)
        {
            _menu = new MainMenu { App = this };
            AddChild(_menu);
        }
        _menu.Visible = true;
    }

    public void StartBattle(Difficulty d)
    {
        Audio.FadeMusic(1);
        if (_menu != null) _menu.Visible = false;
        Swap(new GameSession { Demo = false, Diff = d, App = this });
    }

    public GameSession Session => _session;
}

public partial class MainMenu : CanvasLayer
{
    public Main App;
    Control _root, _main, _diff;

    public override void _Ready()
    {
        Layer = 10;
        _root = new Control { Theme = UiTheme.Build(), MouseFilter = Control.MouseFilterEnum.Ignore };
        UiTheme.Place(_root, 0, 0, 1, 1, 0, 0, 0, 0);
        AddChild(_root);

        // vignette
        var shade = new TextureRect { MouseFilter = Control.MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
        var grad = new Gradient();
        grad.SetColor(0, new Color(0, 0, 0, 0.0f));
        grad.SetColor(1, new Color(0.02f, 0.015f, 0.01f, 0.82f));
        shade.Texture = new GradientTexture2D { Gradient = grad, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.42f), FillTo = new Vector2(1.15f, 1.1f), Width = 256, Height = 256 };
        UiTheme.Place(shade, 0, 0, 1, 1, 0, 0, 0, 0);
        _root.AddChild(shade);

        var title = UiTheme.Text("MANDIBLE", 120, UiTheme.Title, UiTheme.Gold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeConstantOverride("shadow_offset_y", 4);
        title.AddThemeConstantOverride("shadow_outline_size", 18);
        title.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.75f));
        UiTheme.Place(title, 0, 0, 1, 0, 0, 150, 0, 300);
        _root.AddChild(title);
        var sub = UiTheme.Text("WAR  BENEATH  THE  GRASS", 26, UiTheme.Title, UiTheme.Ink);
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        UiTheme.Place(sub, 0, 0, 1, 0, 0, 300, 0, 340);
        _root.AddChild(sub);

        _main = Column(
            ("Begin Battle", () => { _main.Visible = false; _diff.Visible = true; }),
            ("Field Manual", ShowManual),
            ("Quit", () => GetTree().Quit()));
        _diff = Column(
            ("Easy  ·  a gentle colony", () => App.StartBattle(Difficulty.Easy)),
            ("Normal  ·  a fair war", () => App.StartBattle(Difficulty.Normal)),
            ("Hard  ·  a ravenous swarm", () => App.StartBattle(Difficulty.Hard)),
            ("Back", () => { _diff.Visible = false; _main.Visible = true; }));
        _diff.Visible = false;

        var credit = UiTheme.Text("Godot 4.7  ·  original art, sound and music generated for Mandible", 13, null, new Color(UiTheme.Dim, 0.7f));
        credit.HorizontalAlignment = HorizontalAlignment.Center;
        UiTheme.Place(credit, 0, 1, 1, 1, 0, -34, 0, -10);
        _root.AddChild(credit);
    }

    Control Column(params (string, System.Action)[] items)
    {
        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.AddThemeConstantOverride("separation", 12);
        UiTheme.Place(v, 0.5f, 0, 0.5f, 0, -190, 420, 190, 760);
        foreach (var (label, act) in items)
        {
            var b = new Button { Text = label, CustomMinimumSize = new Vector2(380, 58) };
            b.AddThemeFontSizeOverride("font_size", 20);
            b.AddThemeFontOverride("font", UiTheme.Title);
            b.Pressed += () => { App.Audio.Ui("ui_click"); act(); };
            b.MouseEntered += () => App.Audio.Ui("ui_hover");
            v.AddChild(b);
        }
        _root.AddChild(v);
        return v;
    }

    void ShowManual()
    {
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
        UiTheme.Place(dim, 0, 0, 1, 1, 0, 0, 0, 0);
        var p = new PanelContainer();
        UiTheme.Place(p, 0.5f, 0.5f, 0.5f, 0.5f, -420, -240, 420, 240);
        var v = new VBoxContainer();
        var t = UiTheme.Text("FIELD MANUAL", 32, UiTheme.Title, UiTheme.Gold);
        t.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(t);
        foreach (var line in new[]
        {
            "You command the black colony. The red wood-ants hold the thatch mound across the yard.",
            "Workers find food and lay trails home; every trail they reinforce brings more workers.",
            "Soldiers and big-headed majors fight. Fallen ants become food for whoever hauls them home.",
            "Feed the queen royal jelly to unlock new castes and heroes; a greater queen hatches stronger heroes.",
            "Wild beetles, spiders and wasps guard the richest food. Slay them to level your warriors.",
            "Dig chambers to raise more brood. Kill the red queen to win.",
            "",
            "Drag to select · Right-click to command · A attack-move · Z/X/C/V brood · Tab trails · F1 help in battle",
        })
        {
            var l = UiTheme.Text(line, 16);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.HorizontalAlignment = HorizontalAlignment.Center;
            v.AddChild(l);
        }
        var b = new Button { Text = "Close", CustomMinimumSize = new Vector2(160, 46), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        b.Pressed += () => { App.Audio.Ui("ui_click"); dim.QueueFree(); };
        v.AddChild(b);
        p.AddChild(v);
        dim.AddChild(p);
        _root.AddChild(dim);
    }
}
