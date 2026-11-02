using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Live command interface measured against the supplied 1672 x 941 reference.
/// Only portrait artwork is sampled from the reference atlas. Text, buttons, queues,
/// minimap, selection and all world geometry remain live game systems.
/// The interface is built from blocks (top bar, chamber list, minimap, selection, cards, commands). On a
/// computer in landscape they sit exactly where the reference puts them and the whole canvas scales as one.
/// On a phone or tablet (always held sideways) each block is pinned to the screen edges at its own scale,
/// with finger-sized buttons (at least 44 px), no key hints and a long press for right-click.
public partial class ColonyCommandHud : Control
{
    public GameSession Game;
    public Minimap Map;
    /// Where the HUD sits in the touch layout, so the episode card and alerts can stay clear of it.
    public bool Flexible { get; private set; }
    public float TopEdge { get; private set; } = 44;
    public float BottomEdge { get; private set; } = 600;
    public float GuideScale { get; private set; } = 1;
    public Rect2 MapRect { get; private set; }
    Control _canvas, _chambers, _cards, _title, _resources, _mapFrame, _selectionBlock, _cardsBlock, _commandsBlock, _labelLayer;
    Panel _selectionPanel, _cardsPanel;
    readonly bool _touch = Settings.Mobile;
    Vector2 _resourcesSize, _chambersSize, _selectionSize, _cardsSize, _commandsSize;
    Label _food, _brood, _queen, _selection, _detail, _counts, _queueName, _queueNext, _queueTime, _network;
    TextureRect _selectedArt, _queueArt, _nextArt;
    public Texture2D SelectedArt => _selectedArt.Texture;
    ProgressBar _health, _queueProgress;
    readonly List<(Button button, ProdKind kind)> _units = new();
    readonly List<(Button button, ChamberKind kind, Label status)> _rooms = new();
    readonly List<(Button button, Upgrade upgrade, Label status)> _research = new();
    readonly List<Button> _tabs = new();
    readonly List<(Button button, TextureRect art)> _queueSlots = new();
    readonly Dictionary<int, PanelContainer> _labels = new();
    Texture2D _atlas;
    readonly Dictionary<Rect2, Texture2D> _regions = new();
    float _refresh;
    int _page = 1;
    static readonly Color Edge = new("a77e32"), Gold = new("f4cd70"), Background = new(0.035f, 0.031f, 0.022f, 0.96f);
    static readonly ChamberKind[] Rooms = { ChamberKind.Brood, ChamberKind.Storage, ChamberKind.Royal, ChamberKind.Wings, ChamberKind.Forge, ChamberKind.Armory, ChamberKind.Silk };
    static readonly ProdKind[] Units = { ProdKind.Worker, ProdKind.Soldier, ProdKind.Major, ProdKind.Flyer, ProdKind.Gunner };
    static readonly string[] Keys = { "Z", "X", "C", "F", "D" };

    static T At<T>(T control, float x, float y, float w, float h) where T : Control
    { control.Position = new Vector2(x, y); control.Size = new Vector2(w, h); return control; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _atlas = GD.Load<Texture2D>("res://assets/ui/colony-reference.png");
        _labelLayer = new Control { MouseFilter = MouseFilterEnum.Ignore }; AddChild(_labelLayer); // room names float over the world, under the panels
        _canvas = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(1672, 941) }; AddChild(_canvas);
        _title = Block(22, 3, 280, 55);
        Text(_title, "MANDIBLE", 0, 0, 280, 39, 27, true, Gold);
        Text(_title, Game.Campaign ? CampaignEpisodes.Name(Game.Episode).ToUpperInvariant() : Game.Skirmish ? "SKIRMISH" : "COLONY WAR", 1, 35, 260, 18, 12, false, UiTheme.Dim);
        BuildResources(); BuildRoomList(); BuildBottom();
        _network = Text(_canvas, "", 320, 52, 650, 20, 12);
        _mapFrame = Panel(_canvas, 14, 651, 254, 267);
        Map = new Minimap { Game = Game, MouseFilter = MouseFilterEnum.Stop };
        At(Map, 5, 5, 244, 257); _mapFrame.AddChild(Map);
        RebuildCards();
    }

    /// A group of controls placed together; its children use coordinates relative to it.
    Control Block(float x, float y, float w, float h)
    {
        var b = At(new Control { MouseFilter = MouseFilterEnum.Ignore }, x, y, w, h);
        _canvas.AddChild(b); return b;
    }

    static void Place(Control block, float x, float y, float scale)
    { block.Position = new Vector2(x, y); block.Scale = Vector2.One * scale; }

    /// Computer: the reference composition, scaled as one. Phones and tablets: blocks pinned to the edges, the
    /// bottom row first, then the top bar and the minimap (or the chamber list in build mode), as large as fits.
    void Arrange(Vector2 viewport)
    {
        Flexible = _touch;
        _title.Visible = !Flexible;
        if (!Flexible)
        {
            float scale = Mathf.Min(viewport.X / 1672, viewport.Y / 941);
            _canvas.Scale = Vector2.One * scale; _canvas.Position = (viewport - new Vector2(1672, 941) * scale) / 2;
            Place(_resources, 1064, 2, 1); Place(_chambers, 14, 71, 1); Place(_mapFrame, 14, 651, 1);
            Place(_selectionBlock, 271, 709, 1); Place(_cardsBlock, 682, 689, 1); Place(_commandsBlock, 1398, 709, 1);
            Place(_network, 320, 52, 1);
            _selectionPanel.Size = new Vector2(1123, 221); // one panel runs on under the cards
            _mapFrame.Visible = true; _cardsPanel.Visible = false;
            TopEdge = _canvas.Position.Y + 42 * scale; GuideScale = 1;
            MapRect = new Rect2(_canvas.Position + new Vector2(14, 651) * scale, new Vector2(254, 267) * scale);
            return;
        }
        _canvas.Scale = Vector2.One; _canvas.Position = Vector2.Zero;
        _selectionPanel.Size = _selectionSize; _cardsPanel.Visible = true;
        const float m = 12, gap = 10;
        float bottom = viewport.Y - m, k;
        {
            // one row along the bottom edge; on a narrower screen (a 4:3 tablet) the commands step up above the cards
            float row = _selectionSize.X + _cardsSize.X + _commandsSize.X + 2 * gap;
            k = Mathf.Min(Mathf.Min((viewport.X - 2 * m) / row, viewport.Y * 0.34f / _cardsSize.Y), 1.25f);
            if (k * MinButton < 44)
            {
                k = Mathf.Min(Mathf.Min((viewport.X - 2 * m - gap) / (_selectionSize.X + _cardsSize.X), viewport.Y * 0.55f / (_cardsSize.Y + gap + _commandsSize.Y)), 1.25f);
                Place(_selectionBlock, m, bottom - _selectionSize.Y * k, k);
                Place(_cardsBlock, viewport.X - m - _cardsSize.X * k, bottom - _cardsSize.Y * k, k);
                Place(_commandsBlock, viewport.X - m - _commandsSize.X * k, bottom - (_cardsSize.Y + _commandsSize.Y) * k - gap, k);
            }
            else
            {
                float left = m + _selectionSize.X * k, right = viewport.X - m - _commandsSize.X * k;
                Place(_selectionBlock, m, bottom - _selectionSize.Y * k, k);
                Place(_cardsBlock, (left + right - _cardsSize.X * k) / 2, bottom - _cardsSize.Y * k, k);
                Place(_commandsBlock, right, bottom - _commandsSize.Y * k, k);
            }
        }
        BottomEdge = Mathf.Min(_cardsBlock.Position.Y, Mathf.Min(_selectionBlock.Position.Y, _commandsBlock.Position.Y));
        float top = Mathf.Min(k, (viewport.X - 2 * m) / _resourcesSize.X);
        Place(_resources, viewport.X - m - _resourcesSize.X * top, m, top);
        TopEdge = m + _resourcesSize.Y * top;
        Place(_network, viewport.X - m - _resourcesSize.X * top, TopEdge + 4, top);
        float mapScale = k * 0.85f;
        Place(_mapFrame, m, m, mapScale);
        Place(_chambers, m, m, k * 0.95f);
        _mapFrame.Visible = !Game.NestView; // the chamber list takes its corner in build mode
        MapRect = new Rect2(m, m, new Vector2(254, 267) * mapScale);
        GuideScale = Mathf.Min(k, 1.15f);
    }
    float MinButton => _touch ? 46 : 26; // the shortest button in the blocks (the tabs), before scaling

    Panel Panel(Node parent, float x, float y, float w, float h)
    {
        var p = At(new Panel(), x, y, w, h);
        p.AddThemeStyleboxOverride("panel", UiTheme.Box(Background, Edge, 1, 5, 0)); parent.AddChild(p);
        return p;
    }
    Label Text(Node parent, string text, float x, float y, float w, float h, int size = 13, bool title = false, Color? color = null)
    {
        var label = At(UiTheme.Text(text, size, title ? UiTheme.Title : UiTheme.Body, color ?? UiTheme.Ink), x, y, w, h);
        label.MouseFilter = MouseFilterEnum.Ignore; parent.AddChild(label); return label;
    }
    Button Button(Node parent, string name, float x, float y, float w, float h, Action action)
    {
        var b = At(new Button { Text = name }, x, y, w, h);
        b.AddThemeFontSizeOverride("font_size", _touch ? 15 : 13);
        b.AddThemeStyleboxOverride("normal", UiTheme.Box(new Color(0.035f, 0.03f, 0.02f, 0.98f), Edge, 1, 3, 0));
        b.AddThemeStyleboxOverride("hover", UiTheme.Box(new Color(0.18f, 0.13f, 0.055f), Gold, 1, 3, 0));
        b.AddThemeStyleboxOverride("disabled", UiTheme.Box(new Color(0.035f, 0.03f, 0.02f, 0.98f), new Color(Edge, 0.6f), 1, 3, 0));
        b.Pressed += () => { Game.Audio.Ui("ui_click"); action(); };
        parent.AddChild(b); return b;
    }
    Texture2D Region(Rect2 rect)
    {
        if (!_regions.TryGetValue(rect, out var texture)) _regions[rect] = texture = new AtlasTexture { Atlas = _atlas, Region = rect, FilterClip = true };
        return texture;
    }
    Texture2D UnitArt(ProdKind kind)
    {
        if (Config.IsHeroKind(kind) && HeroArt.Card(Config.UnitOf(kind), Local.Me) is { } hero) return hero;
        int i = Array.IndexOf(Units, kind);
        if (i < 0) i = kind == ProdKind.Warlord ? 2 : kind == ProdKind.Matron ? 4 : 3;
        return Region(new Rect2(685 + i * 133, 750, 125, 64));
    }
    Texture2D RoomArt(ChamberKind kind) => Region(new Rect2(24, 106 + Array.IndexOf(Rooms, kind) * 48, 75, 36));
    Texture2D RoomPortrait(ChamberKind kind) => kind switch
    {
        ChamberKind.Brood => Region(new Rect2(286, 726, 154, 190)),
        ChamberKind.Royal => Region(new Rect2(788, 506, 160, 160)),
        ChamberKind.Storage => Region(new Rect2(640, 92, 200, 105)),
        ChamberKind.Forge => Region(new Rect2(1010, 108, 185, 100)),
        ChamberKind.Armory => Region(new Rect2(1085, 289, 246, 120)),
        ChamberKind.Wings => Region(new Rect2(484, 247, 190, 104)),
        _ => Region(new Rect2(430, 446, 200, 114)),
    };
    TextureRect Art(Node parent, Texture2D texture, float x, float y, float w, float h)
    {
        var t = At(new TextureRect { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore }, x, y, w, h);
        parent.AddChild(t); return t;
    }
    ProgressBar Bar(Node parent, float x, float y, float w, float h)
    {
        var b = At(new ProgressBar { MaxValue = 1, ShowPercentage = false, MouseFilter = MouseFilterEnum.Ignore }, x, y, w, h);
        b.AddThemeStyleboxOverride("fill", UiTheme.Box(Gold, Gold, 0, 2, 0)); parent.AddChild(b); return b;
    }
    void BuildResources()
    {
        float d = _touch ? 8 : 0; // a taller bar on phones, so its buttons are finger-sized
        _resourcesSize = new Vector2(_touch ? 624 : 606, 40 + 2 * d);
        var p = Panel(_canvas, 1064, 2, _resourcesSize.X, _resourcesSize.Y); _resources = p;
        Text(p, "FOOD", 12, 12 + d, 35, 16, 10);
        _food = Text(p, "", 50, 6 + d, 118, 25, 17, false, Gold);
        Text(p, "BROOD", 160, 12 + d, 42, 16, 10);
        _brood = Text(p, "", 206, 6 + d, 85, 25, 17);
        _queen = Text(p, "", 295, 10 + d, 136, 20, 11, false, Gold);
        if (_touch)
        {
            Button(p, "Build", 433, 5, 96, 46, () => Game.Input.ToggleBuild());
            Button(p, "Menu", 537, 5, 80, 46, () => Game.Hud.TogglePause());
        }
        else
        {
            Button(p, "Build  N", 433, 5, 82, 30, () => Game.Input.ToggleBuild());
            Button(p, "Menu", 527, 5, 69, 30, () => Game.Hud.TogglePause());
        }
    }
    static Upgrade? RoomUpgrade(ChamberKind k) => k switch
    { ChamberKind.Royal => Upgrade.Queen, ChamberKind.Wings => Upgrade.Wings, ChamberKind.Forge => Upgrade.Mandibles, ChamberKind.Armory => Upgrade.Chitin, ChamberKind.Silk => Upgrade.SilkCutters, _ => null };
    void OrderRoom(ChamberKind kind)
    { if (RoomUpgrade(kind) is { } u) Game.TryResearch(u); else Game.TryDig(kind); }
    void ResearchCancel(Button button, Upgrade upgrade)
    {
        SecondaryAction(button, () =>
        {
            var c = Game.Sim.Col[Local.Me];
            if (c.Researching && c.ResearchKind == upgrade) Game.Issue(new Cmd { Kind = CmdKind.CancelResearch, Team = (byte)Local.Me });
        });
    }

    /// A right-click, or on a touch screen a long press (a finger has no right button), runs the secondary action.
    void SecondaryAction(Button button, Action action)
    {
        ulong pressed = 0;
        button.GuiInput += e =>
        {
            if (e is not InputEventMouseButton mb) return;
            if (mb.Pressed && mb.ButtonIndex == MouseButton.Right) action();
            else if (mb.ButtonIndex == MouseButton.Left && mb.Device == InputEvent.DeviceIdEmulation)
            {
                if (mb.Pressed) pressed = Time.GetTicksMsec();
                else if (pressed > 0 && Time.GetTicksMsec() - pressed > LongPressMs) action();
                if (!mb.Pressed) pressed = 0;
            }
        };
    }
    const ulong LongPressMs = 450; // as long as the press that turns a tap into attack-move
    // The campaign reveals the game a chapter at a time: what it has not introduced yet is left out, not greyed.
    Expedition Story => Game.Sim.Mission;
    bool Shown(ProdKind k) => Story is not { } m || m.AllowsProd(k);
    bool Shown(ChamberKind k) => Story is not { } m || m.AllowsChamber(k);
    bool Shown(Upgrade u) => Story is not { } m || m.ShowsResearch(u);
    bool ShownTab(int page) => page switch { 2 => Story is not { } m || m.ShowsAnyResearch, 3 => Story is not { } h || h.ShowsHeroes, _ => true };
    ChamberKind[] ShownRooms => System.Array.FindAll(Rooms, Shown);

    void BuildRoomList()
    {
        float row = _touch ? 54 : 48, h = _touch ? 48 : 42, d = (h - 42) / 2;
        var rooms = ShownRooms;
        _chambersSize = new Vector2(285, _touch || rooms.Length < Rooms.Length ? 32 + rooms.Length * row + 4 : 369);
        _chambers = Panel(_canvas, 14, 71, _chambersSize.X, _chambersSize.Y);
        Text(_chambers, "Chambers", 13, 4, 240, 25, 18, true, Gold);
        for (int i = 0; i < rooms.Length; i++)
        {
            var kind = rooms[i];
            var b = Button(_chambers, "", 7, 32 + i * row, 270, h, () => OrderRoom(kind));
            if (RoomUpgrade(kind) is { } upgrade) ResearchCancel(b, upgrade);
            Art(b, RoomArt(kind), 3, 3 + d, 76, 36);
            Text(b, Sim.ChamberName(kind), 89, 9 + d, 143, 24, _touch ? 14 : 13);
            var status = Text(b, "", 203, 10 + d, 61, 24, 12);
            status.HorizontalAlignment = HorizontalAlignment.Right;
            _rooms.Add((b, kind, status));
        }
    }
    void BuildBottom()
    {
        // selection: what is selected, its health and what it does
        _selectionSize = new Vector2(410, 221);
        _selectionBlock = Block(271, 709, _selectionSize.X, _selectionSize.Y);
        _selectionPanel = Panel(_selectionBlock, 0, 0, 1123, 221);
        _selectedArt = Art(_selectionBlock, Region(new Rect2(286, 726, 154, 190)), 15, 17, 155, 190);
        _selection = Text(_selectionBlock, "BROOD CHAMBER", 180, 15, 226, 30, 18, true, Gold);
        _health = Bar(_selectionBlock, 182, 49, 213, 6);
        _counts = Text(_selectionBlock, "", 184, 74, 214, 30, _touch ? 14 : 13);
        _detail = Text(_selectionBlock, "", 185, 119, 212, _touch ? 90 : 84, _touch ? 14 : 12);
        _detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        // tabs, cards and the colony queue (taller tabs and queue on phones)
        float tab = _touch ? 46 : 26, q = _touch ? 48 : 43, cardsY = tab + 10, queueY = cardsY + 131;
        _cardsSize = new Vector2(_touch ? 730 : 712, queueY + 19 + q + (_touch ? 8 : 0));
        _cardsBlock = Block(682, 689, _cardsSize.X, _cardsSize.Y);
        // on a computer the selection panel runs on under the cards; standing alone they need their own
        _cardsPanel = Panel(_cardsBlock, -8, cardsY - 6, _cardsSize.X + 16, _cardsSize.Y - cardsY + 6);
        string[] names = { "Chambers", "Ants", "Research", "Heroes" };
        for (int i = 0; i < names.Length; i++)
        {
            int page = i;
            var tabButton = Button(_cardsBlock, names[i], 4 + i * 115, 0, 115, tab, () => { _page = page; RebuildCards(); });
            _tabs.Add(tabButton);
        }
        for (int i = 0, x = 0; i < _tabs.Count; i++)
        {
            _tabs[i].Visible = ShownTab(i);
            if (_tabs[i].Visible) _tabs[i].Position = new Vector2(4 + x++ * 115, 0);
        }
        _cards = At(new Control { MouseFilter = MouseFilterEnum.Ignore }, 0, cardsY, 691, 125); _cardsBlock.AddChild(_cards);
        Text(_cardsBlock, _touch ? "COLONY QUEUE  ·  HOLD TO CANCEL" : "COLONY QUEUE", 0, queueY, 290, 17, _touch ? 12 : 11);
        queueY += 19;
        var first = Button(_cardsBlock, "", 0, queueY, 304, q, () => { });
        first.TooltipText = "Right-click to cancel the last queued batch and refund its food.";
        SecondaryAction(first, () => Game.Issue(new Cmd { Kind = CmdKind.CancelLast, Team = (byte)Local.Me }));
        float d = (q - 43) / 2;
        _queueArt = Art(first, UnitArt(ProdKind.Worker), 3, 3 + d, 55, 37);
        _queueName = Text(first, "", 70, 2 + d, 210, 18, _touch ? 14 : 12);
        _queueProgress = Bar(first, 70, 26 + d, 200, 8);
        _queueTime = Text(first, "", 276, 20 + d, 27, 18, 11);
        var next = Panel(_cardsBlock, _touch ? 312 : 316, queueY, _touch ? 200 : 221, q);
        _nextArt = Art(next, UnitArt(ProdKind.Worker), 3, 3 + d, 78, 37);
        _queueNext = Text(next, "", 90, 5 + d, _touch ? 108 : 130, 30, _touch ? 14 : 12);
        for (int i = 0; i < 4; i++)
        {
            float w = _touch ? 48 : 35;
            var b = Button(_cardsBlock, "", (_touch ? 520 + i * 53 : 542 + i * 40), queueY, w, q, () => { });
            _queueSlots.Add((b, Art(b, UnitArt(ProdKind.Worker), 2, 2, w - 4, q - 4)));
        }
        // orders for the selected ants
        float h = _touch ? 46 : 37;
        _commandsSize = new Vector2(268, _touch ? 12 + 4 * 54 : 209);
        var commands = Panel(_canvas, 1398, 709, _commandsSize.X, _commandsSize.Y); _commandsBlock = commands;
        (string name, string key, Action act)[] actions = {
            ("Attack", "A", () => Game.Input.BeginAttackMove()), ("Stop", "S", () => Game.Input.Stop(false)),
            ("Hold", "H", () => Game.Input.Stop(true)), ("Gather", "G", () => Game.Input.BeginGather()),
            ("Reinforce", "B", () => Game.Input.Reinforce()), ("Army", "F2", () => Game.Input.SelectArmy()), ("Workers", "F3", () => Game.Input.SelectWorkers()) };
        for (int i = 0; i < actions.Length; i++)
        {
            var a = actions[i];
            float x = i is 1 or 3 or 6 ? 137 : 12, y = i < 4 ? 12 + i / 2 * 47 : i == 4 ? 107 : 156;
            if (_touch) y = 12 + (i < 4 ? i / 2 : i == 4 ? 2 : 3) * 54;
            float w = i == 4 ? 244 : 119, dy = (h - 37) / 2;
            var b = Button(commands, "", x, y, w, h, a.act);
            Text(b, a.name, 41, 8 + dy, 75, 23, _touch ? 14 : 12);
            if (!_touch) Text(b, a.key, w - 25, 9, 20, 22, 12, false, UiTheme.Dim); // phones have no keys to hint
            b.AddChild(At(new CommandMark { Kind = i, MouseFilter = MouseFilterEnum.Ignore }, 9, 8 + dy, 23, 23));
        }
    }
    void RebuildCards()
    {
        foreach (var child in _cards.GetChildren()) { _cards.RemoveChild(child); child.QueueFree(); }
        _units.Clear(); _research.Clear();
        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].AddThemeStyleboxOverride("normal", UiTheme.Box(i == _page ? new Color("624619") : Background, i == _page ? Gold : Edge, 1, 3, 0));
            _tabs[i].AddThemeColorOverride("font_color", i == _page ? Gold : UiTheme.Ink);
        }
        if (_page is 1 or 3)
        {
            var list = _page == 1 ? Units : new[] { ProdKind.Warlord, ProdKind.Matron, ProdKind.Marshal };
            for (int i = 0, slot = 0; i < list.Length; i++)
            {
                var kind = list[i];
                if (!Shown(kind)) continue;
                string key = _page == 1 ? Keys[i] : new[] { "Y", "U", "I" }[i];
                var b = Button(_cards, "", slot++ * 133, 0, 129, 125, () => Game.TryEnqueue(kind));
                var title = Text(b, Config.ProdName(kind), 2, 4, 125, 24, 13); title.HorizontalAlignment = HorizontalAlignment.Center;
                Art(b, UnitArt(kind), 3, 26, 123, 64);
                Text(b, $"{Config.ProdCost(kind):0} food", 26, 91, 94, 17, _touch ? 13 : 12);
                Text(b, $"{Config.SquadSize(Config.UnitOf(kind))} ant{(Config.SquadSize(Config.UnitOf(kind)) == 1 ? "" : "s")}", 26, 106, 88, 17, _touch ? 13 : 12);
                if (!_touch) Text(b, key, 105, 101, 20, 18, 13);
                Art(b, Region(new Rect2(691, 820, 13, 22)), 8, 94, 13, 22);
                _units.Add((b, kind));
            }
        }
        else if (_page == 0)
        {
            var rooms = ShownRooms;
            for (int i = 0; i < rooms.Length; i++)
            {
                var kind = rooms[i];
                var b = Button(_cards, "", i * 98, 0, 94, 125, () => OrderRoom(kind));
                Art(b, RoomArt(kind), 3, 4, 88, 64);
                var l = Text(b, Sim.ChamberName(kind), 5, 74, 85, 45, 11); l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                b.TooltipText = kind == ChamberKind.Brood ? "Incubator: eggs, larvae and cocoons. +50 population capacity. 100 food." : Sim.ChamberName(kind);
            }
        }
        else
        {
            for (int i = 0, slot = 0; i < 5; i++)
            {
                var u = (Upgrade)i;
                if (!Shown(u)) continue;
                var b = Button(_cards, "", slot++ * 133, 0, 129, 125, () => Game.TryResearch(u));
                ResearchCancel(b, u);
                Art(b, RoomArt(Sim.KindOf(u)), 3, 3, 123, 67);
                Text(b, Sim.ChamberName(Sim.KindOf(u)), 4, 73, 121, 22, 11);
                var status = Text(b, "", 7, 99, 115, 21, 12);
                _research.Add((b, u, status));
            }
        }
    }
    public void Refresh(float dt)
    {
        Arrange(GetViewport().GetVisibleRect().Size);
        UpdateRoomLabels();
        _refresh -= dt; if (_refresh > 0) return; _refresh = 0.12f;
        var sim = Game.Sim; var c = sim.Col[Local.Me];
        _food.Text = $"{(int)c.Food} / {(int)c.FoodCap}"; _brood.Text = $"{c.PopUsed} / {c.PopCap}";
        _queen.Text = Config.QueenTitles[c.T(Upgrade.Queen)].ToUpperInvariant();
        _chambers.Visible = Game.NestView;
        foreach (var (button, kind, status) in _rooms)
        {
            bool episodeLock = sim.Mission != null && (!sim.Mission.AllowsChamber(kind) || RoomUpgrade(kind) is { } lu && !sim.Mission.AllowsResearch(lu, c.T(lu) + 1));
            if (RoomUpgrade(kind) is { } u)
            {
                bool complete = c.T(u) >= Config.MaxTier(u);
                status.Text = c.Researching && c.ResearchKind == u ? $"{100 * c.ResearchProgress / c.ResearchTime:0}%" : complete ? "Complete" : episodeLock ? (Shown(u) ? $"Rank {c.T(u)}" : "") : $"{Config.ResearchCost(u, c.T(u) + 1):0}";
                button.Disabled = sim.CanResearch(Local.Me, u) != ResearchResult.Ok;
            }
            else { status.Text = $"{(kind == ChamberKind.Brood ? Sim.BroodCost : Sim.StorageCost):0}"; button.Disabled = sim.CanDig(Local.Me, kind) != Sim.DigResult.Ok; }
            button.TooltipText = kind == ChamberKind.Brood ? "Incubator: eggs hatch here. Each completed chamber adds 50 population capacity." : Sim.ChamberName(kind);
            if (RoomUpgrade(kind) == c.ResearchKind && c.Researching) button.TooltipText = "Researching. Right-click to cancel and refund food.";
        }
        foreach (var (b, k) in _units)
        {
            var availability = sim.CanEnqueue(Local.Me, k);
            b.Disabled = availability != QueueResult.Ok;
            b.Modulate = new Color(1, 1, 1, availability == QueueResult.Locked ? 0.4f : b.Disabled ? 0.65f : 1f);
            b.TooltipText = $"{Config.ProdName(k)} · {Config.ProdCost(k):0} food · {Config.ProdTime(k):0}s · {Config.ProdPop(k)} population";
        }
        foreach (var (b, u, status) in _research)
        {
            b.Disabled = sim.CanResearch(Local.Me, u) != ResearchResult.Ok;
            bool active = c.Researching && c.ResearchKind == u;
            status.Text = active ? $"Research {100 * c.ResearchProgress / c.ResearchTime:0}%" : c.T(u) >= Config.MaxTier(u) ? "Complete" : sim.Mission != null && !sim.Mission.AllowsResearch(u, c.T(u) + 1) ? $"Rank {c.T(u)}" : $"{Config.ResearchCost(u, c.T(u) + 1):0} food";
            b.TooltipText = active ? "Right-click to cancel research and refund food." : Sim.ChamberName(Sim.KindOf(u));
        }
        RefreshSelection(c); RefreshQueue(c);
        if (Game.Net is { } net) _network.Text = $"vs {net.PeerName} · {net.PingMs:0} ms" + (net.Desyncs > 0 ? " · SYNC ERROR" : Game.NetStall > 0.4f ? " · Waiting for player…" : "");
    }
    void RefreshSelection(Colony c)
    {
        var input = Game.Input;
        if (Game.NestView && input.SelectedRoom >= 0 && input.SelectedRoom < c.Chambers.Count)
        {
            var room = c.Chambers[input.SelectedRoom];
            _selection.Text = Sim.ChamberName(room.Kind).ToUpperInvariant();
            _selectedArt.Texture = RoomPortrait(room.Kind);
            _health.Value = room.Progress;
            _counts.Text = room.Kind == ChamberKind.Brood ? $"Eggs {c.Queue.Count}   ·   Capacity +{Config.ChamberPop}" : room.Digging ? $"{room.Diggers} workers · {room.Progress * 100:0}%" : $"Level {room.Tier} · Complete";
            _detail.Text = room.Kind switch { ChamberKind.Brood => "Incubate eggs, larvae and cocoons. Hatch workers and fighting squads.", ChamberKind.Storage => $"Seeds, fruit and insect protein. Food stored: {(int)c.Food} / {(int)c.FoodCap}.", ChamberKind.Royal => "The queen lays eggs here. Workers care for her and carry brood to incubators.", ChamberKind.Forge => "Dark mandibles and amber resin strengthen your warriors' bites.", ChamberKind.Armory => "Overlapping beetle-shell plates protect the colony.", ChamberKind.Wings => "Winged ants prepare among root perches and cocoons.", _ => "Workers process silk on stretched threads and wind bundles." };
            return;
        }
        int n = 0; float hp = 0, max = 0; AntType type = AntType.Worker;
        foreach (int id in input.Selection) if (Game.Sim.Ants[id].Alive) { var a = Game.Sim.Ants[id]; type = a.Type; hp += a.Hp; max += a.MaxHp; n++; }
        _selection.Text = n > 0 ? n == 1 ? Config.Of(type).Name : $"{n} ANTS SELECTED" : "THE COLONY";
        _health.Value = n > 0 ? hp / Mathf.Max(1, max) : 1;
        ProdKind kind = type switch { AntType.Soldier => ProdKind.Soldier, AntType.Major => ProdKind.Major, AntType.Flyer => ProdKind.Flyer, AntType.Gunner => ProdKind.Gunner,
            AntType.Warlord => ProdKind.Warlord, AntType.Matron => ProdKind.Matron, AntType.Marshal => ProdKind.Marshal, _ => ProdKind.Worker };
        // a selected hero shows its own upright bust; the wide card art serves the queue and the Heroes tab
        _selectedArt.Texture = n > 0 && Config.IsHero(type) && HeroArt.Bust(type, Local.Me) is { } bust ? bust : n > 0 && type != AntType.Queen ? UnitArt(kind) : RoomPortrait(ChamberKind.Royal);
        _counts.Text = n > 0 ? $"{hp:0} / {max:0} health" : $"Workers {c.Count[0]}  ·  Soldiers {c.Count[1]}";
        _detail.Text = _touch
            ? n > 0 ? "Tap the ground to move or an enemy to attack. Hold for attack-move." : "Tap ants to command them. Tap the nest hole or Build to see the chambers."
            : n > 0 ? "Right-click to move or attack. Workers gather food and dig chambers." : "Select ants to command them. Click a chamber to inspect it. N opens the nest.";
        if (input.InspectFood is { Alive: true } food && n == 0) { _selection.Text = Config.FoodName(food.Kind); _counts.Text = $"{food.Amount:0} loads remaining"; _detail.Text = Config.FoodEffectShort(food.Kind); }
        if (n == 0 && Game.Sim.Valid(input.Inspect, input.InspectSerial))
        {
            var enemy = Game.Sim.Ants[input.Inspect];
            _selection.Text = Config.Of(enemy.Type).Name;
            if (Config.IsHero(enemy.Type) && HeroArt.Bust(enemy.Type, enemy.Team) is { } enemyBust) _selectedArt.Texture = enemyBust;
            _health.Value = enemy.Hp / enemy.MaxHp;
            _counts.Text = $"{enemy.Hp:0} / {enemy.MaxHp:0} health";
            _detail.Text = enemy.Team == Config.Wild ? "Wild insect. Defeat it for experience and protein." : "Enemy colony. Select your attackers and right-click to attack.";
        }
    }
    void RefreshQueue(Colony c)
    {
        bool active = c.Queue.Count > 0;
        _queueArt.Visible = active;
        _queueName.Text = active ? $"{Config.ProdName(c.Queue[0].Kind)} ×{Config.SquadSize(Config.UnitOf(c.Queue[0].Kind))}" : Game.Sim.ChamberCount(c.Team, ChamberKind.Brood, true) > 0 ? "Incubator ready" : "Build an incubator";
        _queueProgress.Value = active ? c.Queue[0].Progress / c.Queue[0].Time : 0;
        _queueTime.Text = active ? c.PopBlocked ? "Full" : $"{Mathf.Ceil(c.Queue[0].Time - c.Queue[0].Progress):0}s" : "";
        if (active) _queueArt.Texture = UnitArt(c.Queue[0].Kind);
        _nextArt.Visible = c.Queue.Count > 1;
        if (_nextArt.Visible) _nextArt.Texture = UnitArt(c.Queue[1].Kind);
        _queueNext.Text = c.Queue.Count > 1 ? $"Next: {Config.ProdName(c.Queue[1].Kind)}" : "Queue is clear";
        for (int i = 0; i < _queueSlots.Count; i++)
        {
            var slot = _queueSlots[i]; slot.button.Text = "";
            slot.art.Visible = c.Queue.Count > i + 2;
            if (slot.art.Visible) slot.art.Texture = UnitArt(c.Queue[i + 2].Kind);
            if (i == _queueSlots.Count - 1 && c.Queue.Count > 6) { slot.art.Visible = false; slot.button.Text = $"+{c.Queue.Count - 5}"; }
        }
    }
    /// Room names float over their chambers in build mode, never under a panel or off the screen.
    void UpdateRoomLabels()
    {
        foreach (var entry in _labels) entry.Value.Visible = false;
        if (!Game.NestView || Game.Camera?.Cam == null) return;
        var sim = Game.Sim; var home = Game.World.Nests[Local.Me].Pos;
        float scale = Flexible ? Mathf.Max(1, _cardsBlock.Scale.X * 0.9f) : _canvas.Scale.X;
        var screen = new Rect2(Vector2.Zero, GetViewport().GetVisibleRect().Size);
        foreach (var ch in sim.Col[Local.Me].Chambers)
        {
            if (!_labels.TryGetValue(ch.Id, out var panel))
            {
                panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
                panel.AddThemeStyleboxOverride("panel", UiTheme.Box(Background, Edge, 1, 3, 5));
                var l = UiTheme.Text(Sim.ChamberName(ch.Kind).ToUpperInvariant(), 11, UiTheme.Bold, Gold); l.MouseFilter = MouseFilterEnum.Ignore; panel.AddChild(l);
                _labelLayer.AddChild(panel); _labels[ch.Id] = panel;
            }
            var p = sim.ChamberPos(Local.Me, ch);
            var world = new Vector3(p.X, Game.World.HF.Sample(home.X, home.Y) - NestGeometry.Depth + 0.3f, p.Y);
            if (Game.Camera.Cam.IsPositionBehind(world)) continue;
            panel.Scale = Vector2.One * scale;
            var size = panel.Size * scale;
            var rect = new Rect2(Game.Camera.Cam.UnprojectPosition(world) - new Vector2(size.X * 0.5f, 60 * scale), size);
            if (!screen.Encloses(rect) || Covered(rect)) continue;
            panel.Position = rect.Position;
            panel.Visible = true;
        }
    }

    bool Covered(Rect2 r)
    {
        foreach (var block in new[] { _resources, _chambers, _mapFrame, _selectionBlock, _cardsBlock, _commandsBlock })
            if (block.IsVisibleInTree() && Bounds(block).Grow(4).Intersects(r)) return true;
        return Game.Hud.EpisodeCard is { } card && card.IsVisibleInTree() && card.GetGlobalRect().Intersects(r);
    }

    /// A block's rectangle on screen (on a computer the selection block's panel runs on under the cards).
    Rect2 Bounds(Control block)
    {
        var size = block == _selectionBlock ? _selectionPanel.Size : block.Size;
        var t = block.GetGlobalTransformWithCanvas();
        return new Rect2(t.Origin, size * t.Scale);
    }
}

public partial class CommandMark : Control
{
    public int Kind;
    public override void _Draw()
    {
        var gold = new Color("f1c65c"); var c = Size / 2;
        if (Kind == 0) { DrawCircle(c, 7, gold, false, 1.5f, true); DrawLine(c - Vector2.Right * 11, c + Vector2.Right * 11, gold, 1.5f, true); DrawLine(c - Vector2.Down * 11, c + Vector2.Down * 11, gold, 1.5f, true); }
        else if (Kind is 1 or 2) { Vector2[] p = { c + new Vector2(-7, -6), c + new Vector2(0, -9), c + new Vector2(7, -6), c + new Vector2(6, 5), c + new Vector2(0, 10), c + new Vector2(-6, 5) }; DrawColoredPolygon(p, gold); }
        else if (Kind == 4) { DrawLine(new Vector2(5, 2), new Vector2(5, 22), gold, 2); DrawColoredPolygon(new[] { new Vector2(6, 3), new Vector2(20, 7), new Vector2(6, 12) }, gold); }
        else if (Kind == 5) { for (int i = 0; i < 3; i++) { DrawCircle(new Vector2(5 + i * 7, 6), 2.6f, gold); DrawRect(new Rect2(2 + i * 7, 10, 6, 10), gold); } }
        else { DrawCircle(c, 3, gold); DrawCircle(c + new Vector2(0, 7), 4, gold); DrawCircle(c + new Vector2(0, -6), 2.5f, gold); for (int i = -1; i <= 1; i++) { DrawLine(c + new Vector2(-2, i * 3), c + new Vector2(-10, i * 8), gold, 1.4f, true); DrawLine(c + new Vector2(2, i * 3), c + new Vector2(10, i * 8), gold, 1.4f, true); } }
    }
}
