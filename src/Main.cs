using System.Threading.Tasks;
using Godot;

namespace Mandible;

/// App root: owns audio, the main menu and the current session.
public partial class Main : Node
{
    public AudioDirector Audio;
    GameSession _session;
    MainMenu _menu;
    public MainMenu Menu => _menu;
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
            if (a.StartsWith("--nettest="))
            {
                Audio = new AudioDirector();
                AddChild(Audio);
                _ = Capture.NetTest(this, a.Substring(10));
                return;
            }
        }
        Settings.Load();
        if (Settings.Mobile) GetTree().Root.ContentScaleFactor = 1.3f; // bigger buttons for fingers
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
        Local.Me = Config.Player;
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
        Local.Me = Config.Player;
        Audio.FadeMusic(1);
        if (_menu != null) _menu.Visible = false;
        Swap(new GameSession { Demo = false, Diff = d, App = this });
    }

    /// Starts a lockstep match: the host plays the black colony, the guest the red.
    public void StartMultiplayer(Lockstep net, bool autoPlay = false, int fastForward = 0)
    {
        Local.Me = net.Me;
        Audio.FadeMusic(1);
        if (_menu != null) _menu.Visible = false;
        Swap(new GameSession { Demo = false, App = this, Net = net, Seed = net.Seed, AutoPlayLocal = autoPlay, FastForward = fastForward });
    }

    public GameSession Session => _session;
}

public partial class MainMenu : CanvasLayer
{
    public Main App;
    Control _root, _main, _diff;
    Lobby _lobby;

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
            ("Multiplayer", () => { _main.Visible = false; OpenLobby(); }),
            ("Field Manual", ShowManual),
            ($"Graphics: {Settings.Gfx}", CycleGraphics),
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

    void CycleGraphics()
    {
        Settings.Gfx = (Settings.Quality)(((int)Settings.Gfx + 2) % 3); // High -> Medium -> Low -> High
        Settings.Save();
        foreach (var n in _main.GetChildren())
            if (n is Button b && b.Text.StartsWith("Graphics")) b.Text = $"Graphics: {Settings.Gfx}";
        App.ShowMenu(); // rebuild the backdrop at the new quality
    }

    public void OpenLobby()
    {
        _main.Visible = false;
        _diff.Visible = false;
        _lobby?.QueueFree();
        _lobby = new Lobby { App = App, OnBack = () => { _lobby.QueueFree(); _lobby = null; _main.Visible = true; } };
        _root.AddChild(_lobby);
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

/// Multiplayer lobby: host on the LAN or over the internet, browse LAN games, or join by address.
public partial class Lobby : Control
{
    public Main App;
    public System.Action OnBack;
    Lockstep _net;
    LanBrowser _browser;
    LineEdit _name, _address;
    ItemList _games;
    Label _status;
    Button _start, _hostLan, _hostNet, _join;
    readonly System.Collections.Generic.List<string> _gameAddrs = new();
    float _refresh;

    public override void _Ready()
    {
        UiTheme.Place(this, 0.5f, 0, 0.5f, 0, -360, 360, 360, 900);
        var p = new PanelContainer();
        UiTheme.Place(p, 0, 0, 1, 1, 0, 0, 0, 0);
        AddChild(p);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        p.AddChild(v);
        var title = UiTheme.Text("MULTIPLAYER", 28, UiTheme.Title, UiTheme.Gold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(title);

        var nameRow = new HBoxContainer();
        nameRow.AddChild(UiTheme.Text("Your name", 16, UiTheme.Bold, UiTheme.Dim));
        _name = new LineEdit { Text = DefaultName(), SizeFlagsHorizontal = SizeFlags.ExpandFill, MaxLength = 20 };
        nameRow.AddChild(_name);
        v.AddChild(nameRow);

        var hostRow = new HBoxContainer();
        _hostLan = Btn("Host on this network", () => Host(false));
        _hostNet = Btn("Host over the internet", () => Host(true));
        hostRow.AddChild(_hostLan);
        hostRow.AddChild(_hostNet);
        v.AddChild(hostRow);

        v.AddChild(UiTheme.Text("Games on your network", 15, UiTheme.Bold, UiTheme.Dim));
        _games = new ItemList { CustomMinimumSize = new Vector2(0, 130), SizeFlagsVertical = SizeFlags.ExpandFill };
        _games.ItemActivated += i => Join(_gameAddrs[(int)i]);
        v.AddChild(_games);

        var joinRow = new HBoxContainer();
        _address = new LineEdit { PlaceholderText = "IP address, e.g. 192.168.1.20", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        joinRow.AddChild(_address);
        _join = Btn("Join", () =>
        {
            var sel = _games.GetSelectedItems();
            Join(_address.Text.Trim() != "" ? _address.Text : sel.Length > 0 ? _gameAddrs[sel[0]] : "");
        });
        joinRow.AddChild(_join);
        v.AddChild(joinRow);

        _status = UiTheme.Text("Host a game, or pick one from the list. Friends on the same Wi-Fi appear automatically.", 15, null, UiTheme.Ink);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        v.AddChild(_status);

        var bottom = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _start = Btn("Start Battle", () => _net?.StartMatch());
        _start.Visible = false;
        bottom.AddChild(_start);
        bottom.AddChild(Btn("Back", Back));
        v.AddChild(bottom);

        _browser = new LanBrowser();
        AddChild(_browser);
    }

    static string DefaultName()
    {
        var n = OS.GetEnvironment("USERNAME");
        if (string.IsNullOrEmpty(n)) n = OS.GetEnvironment("USER");
        return string.IsNullOrEmpty(n) ? "Commander" : n;
    }

    Button Btn(string text, System.Action act)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(200, 50), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        b.AddThemeFontOverride("font", UiTheme.Title);
        b.AddThemeFontSizeOverride("font_size", 16);
        b.Pressed += () => { App.Audio.Ui("ui_click"); act(); };
        b.MouseEntered += () => App.Audio.Ui("ui_hover");
        return b;
    }

    Lockstep NewNet()
    {
        _net?.Leave();
        _net?.QueueFree();
        _net = new Lockstep();
        App.AddChild(_net);
        _net.OnStart += () => App.StartMultiplayer(_net);
        _net.OnConnected += () => App.Audio.Ui("ui_ready");
        return _net;
    }

    void Host(bool internet)
    {
        var net = NewNet();
        if (net.Host(Name(), internet) != Error.Ok) App.Audio.Ui("ui_error");
        _hostLan.Disabled = _hostNet.Disabled = _join.Disabled = true;
    }

    void Join(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) { _status.Text = "Type an address or pick a game from the list."; App.Audio.Ui("ui_error"); return; }
        var net = NewNet();
        if (net.Join(address, Name()) != Error.Ok) App.Audio.Ui("ui_error");
        _hostLan.Disabled = _hostNet.Disabled = _join.Disabled = true;
    }

    string Name() => _name.Text.Trim() == "" ? "Commander" : _name.Text.Trim();

    void Back()
    {
        _net?.Leave();
        _net?.QueueFree();
        _net = null;
        OnBack?.Invoke();
    }

    public override void _Process(double delta)
    {
        _refresh -= (float)delta;
        if (_refresh > 0) return;
        _refresh = 0.25f;
        if (_net != null)
        {
            _status.Text = _net.Status;
            _start.Visible = _net.IsHost && _net.State == Lockstep.Phase.Connected;
            if (_net.State == Lockstep.Phase.Closed) { _hostLan.Disabled = _hostNet.Disabled = _join.Disabled = false; }
        }
        // refresh the LAN list without disturbing the selection
        var keys = new System.Collections.Generic.List<string>(_browser.Hosts.Keys);
        keys.Sort();
        if (string.Join(",", keys) != string.Join(",", _gameAddrs))
        {
            _games.Clear();
            _gameAddrs.Clear();
            foreach (var k in keys)
            {
                _games.AddItem($"{_browser.Hosts[k].name}  \u00b7  {k}");
                _gameAddrs.Add(k);
            }
        }
    }
}
