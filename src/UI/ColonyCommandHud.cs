using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Live command interface measured against the supplied 1672 x 941 reference.
/// Only portrait artwork is sampled from the reference atlas. Text, buttons, queues,
/// minimap, selection and all world geometry remain live game systems.
public partial class ColonyCommandHud : Control
{
    public GameSession Game;
    public Minimap Map;
    Control _canvas, _chambers, _cards;
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
        _canvas = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(1672, 941) }; AddChild(_canvas);
        Text(_canvas, "MANDIBLE", 22, 3, 280, 39, 27, true, Gold);
        Text(_canvas, Game.TutorialMode ? "THE FIRST NEST" : Game.Skirmish ? "SKIRMISH" : "COLONY WAR", 23, 38, 260, 18, 12, false, UiTheme.Dim);
        BuildResources(); BuildRoomList(); BuildBottom();
        _network = Text(_canvas, "", 320, 52, 650, 20, 12);
        var mapFrame = Panel(_canvas, 14, 651, 254, 267);
        Map = new Minimap { Game = Game, MouseFilter = MouseFilterEnum.Stop };
        At(Map, 5, 5, 244, 257); mapFrame.AddChild(Map);
        RebuildCards();
    }

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
        b.AddThemeFontSizeOverride("font_size", 13);
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
        var p = Panel(_canvas, 1064, 2, 606, 40);
        Text(p, "FOOD", 12, 12, 35, 16, 10);
        _food = Text(p, "", 50, 6, 118, 25, 17, false, Gold);
        Text(p, "BROOD", 160, 12, 42, 16, 10);
        _brood = Text(p, "", 206, 6, 85, 25, 17);
        _queen = Text(p, "", 295, 10, 136, 20, 11, false, Gold);
        Button(p, "Build  N", 433, 5, 82, 30, () => Game.Input.ToggleBuild());
        Button(p, "Menu", 527, 5, 69, 30, () => Game.Hud.TogglePause());
    }
    static Upgrade? RoomUpgrade(ChamberKind k) => k switch
    { ChamberKind.Royal => Upgrade.Queen, ChamberKind.Wings => Upgrade.Wings, ChamberKind.Forge => Upgrade.Mandibles, ChamberKind.Armory => Upgrade.Chitin, ChamberKind.Silk => Upgrade.SilkCutters, _ => null };
    void OrderRoom(ChamberKind kind)
    { if (RoomUpgrade(kind) is { } u) Game.TryResearch(u); else Game.TryDig(kind); }
    void ResearchCancel(Button button, Upgrade upgrade)
    {
        button.GuiInput += e =>
        {
            var c = Game.Sim.Col[Local.Me];
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } && c.Researching && c.ResearchKind == upgrade)
                Game.Issue(new Cmd { Kind = CmdKind.CancelResearch, Team = (byte)Local.Me });
        };
    }
    void BuildRoomList()
    {
        _chambers = Panel(_canvas, 14, 71, 285, 369);
        Text(_chambers, "Chambers", 13, 4, 240, 25, 18, true, Gold);
        for (int i = 0; i < Rooms.Length; i++)
        {
            var kind = Rooms[i];
            var b = Button(_chambers, "", 7, 32 + i * 48, 270, 42, () => OrderRoom(kind));
            if (RoomUpgrade(kind) is { } upgrade) ResearchCancel(b, upgrade);
            Art(b, RoomArt(kind), 3, 3, 76, 36);
            Text(b, Sim.ChamberName(kind), 89, 9, 143, 24, 13);
            var status = Text(b, "", 203, 10, 61, 24, 12);
            status.HorizontalAlignment = HorizontalAlignment.Right;
            _rooms.Add((b, kind, status));
        }
    }
    void BuildBottom()
    {
        Panel(_canvas, 271, 709, 1123, 221);
        _selectedArt = Art(_canvas, Region(new Rect2(286, 726, 154, 190)), 286, 726, 155, 190);
        _selection = Text(_canvas, "BROOD CHAMBER", 451, 724, 226, 30, 18, true, Gold);
        _health = Bar(_canvas, 453, 758, 213, 6);
        _counts = Text(_canvas, "", 455, 783, 214, 30, 13);
        _detail = Text(_canvas, "", 456, 828, 212, 84, 12);
        _detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        string[] names = { "Chambers", "Ants", "Research", "Heroes" };
        for (int i = 0; i < names.Length; i++)
        {
            int page = i;
            var tab = Button(_canvas, names[i], 686 + i * 115, 689, 115, 26, () => { _page = page; RebuildCards(); });
            _tabs.Add(tab);
        }
        _cards = At(new Control { MouseFilter = MouseFilterEnum.Ignore }, 682, 725, 691, 125); _canvas.AddChild(_cards);
        Text(_canvas, "COLONY QUEUE", 682, 856, 290, 17, 11);
        var first = Button(_canvas, "", 682, 875, 304, 43, () => { });
        first.TooltipText = "Right-click to cancel the last queued batch and refund its food.";
        first.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) Game.Issue(new Cmd { Kind = CmdKind.CancelLast, Team = (byte)Local.Me }); };
        _queueArt = Art(first, UnitArt(ProdKind.Worker), 3, 3, 55, 37);
        _queueName = Text(first, "", 70, 2, 210, 18, 12);
        _queueProgress = Bar(first, 70, 26, 200, 8);
        _queueTime = Text(first, "", 276, 20, 27, 18, 11);
        var next = Panel(_canvas, 998, 875, 221, 43);
        _nextArt = Art(next, UnitArt(ProdKind.Worker), 3, 3, 78, 37);
        _queueNext = Text(next, "", 90, 5, 130, 30, 12);
        for (int i = 0; i < 4; i++)
        {
            var b = Button(_canvas, "", 1224 + i * 40, 875, 35, 43, () => { });
            _queueSlots.Add((b, Art(b, UnitArt(ProdKind.Worker), 2, 2, 31, 39)));
        }
        var commands = Panel(_canvas, 1398, 709, 268, 209);
        (string name, string key, Action act)[] actions = {
            ("Attack", "A", () => Game.Input.BeginAttackMove()), ("Stop", "S", () => Game.Input.Stop(false)),
            ("Hold", "H", () => Game.Input.Stop(true)), ("Gather", "G", () => Game.Input.BeginGather()),
            ("Reinforce", "B", () => Game.Input.Reinforce()), ("Army", "F2", () => Game.Input.SelectArmy()), ("Workers", "F3", () => Game.Input.SelectWorkers()) };
        for (int i = 0; i < actions.Length; i++)
        {
            var a = actions[i];
            float x = i is 1 or 3 or 6 ? 137 : 12, y = i < 4 ? 12 + i / 2 * 47 : i == 4 ? 107 : 156;
            float w = i == 4 ? 244 : 119;
            var b = Button(commands, "", x, y, w, 37, a.act);
            Text(b, a.name, 41, 8, 75, 23, 12);
            Text(b, a.key, w - 25, 9, 20, 22, 12, false, UiTheme.Dim);
            b.AddChild(At(new CommandMark { Kind = i, MouseFilter = MouseFilterEnum.Ignore }, 9, 8, 23, 23));
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
            for (int i = 0; i < list.Length; i++)
            {
                var kind = list[i];
                string key = _page == 1 ? Keys[i] : new[] { "Y", "U", "I" }[i];
                var b = Button(_cards, "", i * 133, 0, 129, 125, () => Game.TryEnqueue(kind));
                var title = Text(b, Config.ProdName(kind), 2, 4, 125, 24, 13); title.HorizontalAlignment = HorizontalAlignment.Center;
                Art(b, UnitArt(kind), 3, 26, 123, 64);
                Text(b, $"{Config.ProdCost(kind):0} food", 26, 91, 94, 17, 12);
                Text(b, $"{Config.SquadSize(Config.UnitOf(kind))} ant{(Config.SquadSize(Config.UnitOf(kind)) == 1 ? "" : "s")}", 26, 106, 88, 17, 12);
                Text(b, key, 105, 101, 20, 18, 13);
                Art(b, Region(new Rect2(691, 820, 13, 22)), 8, 94, 13, 22);
                _units.Add((b, kind));
            }
        }
        else if (_page == 0)
        {
            for (int i = 0; i < Rooms.Length; i++)
            {
                var kind = Rooms[i];
                var b = Button(_cards, "", i * 98, 0, 94, 125, () => OrderRoom(kind));
                Art(b, RoomArt(kind), 3, 4, 88, 64);
                var l = Text(b, Sim.ChamberName(kind), 5, 74, 85, 45, 11); l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                b.TooltipText = kind == ChamberKind.Brood ? "Incubator: eggs, larvae and cocoons. +50 population capacity. 100 food." : Sim.ChamberName(kind);
            }
        }
        else
        {
            for (int i = 0; i < 5; i++)
            {
                var u = (Upgrade)i;
                var b = Button(_cards, "", i * 133, 0, 129, 125, () => Game.TryResearch(u));
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
        var viewport = GetViewport().GetVisibleRect().Size;
        float scale = Mathf.Min(viewport.X / 1672, viewport.Y / 941);
        _canvas.Scale = Vector2.One * scale; _canvas.Position = (viewport - new Vector2(1672, 941) * scale) / 2;
        UpdateRoomLabels(scale);
        _refresh -= dt; if (_refresh > 0) return; _refresh = 0.12f;
        var sim = Game.Sim; var c = sim.Col[Local.Me];
        _food.Text = $"{(int)c.Food} / {(int)c.FoodCap}"; _brood.Text = $"{c.PopUsed} / {c.PopCap}";
        _queen.Text = Config.QueenTitles[c.T(Upgrade.Queen)].ToUpperInvariant();
        _chambers.Visible = Game.NestView;
        foreach (var (button, kind, status) in _rooms)
        {
            bool episodeLock = sim.Mission != null && (kind is not (ChamberKind.Brood or ChamberKind.Storage));
            if (RoomUpgrade(kind) is { } u)
            {
                bool complete = c.T(u) >= Config.MaxTier(u);
                status.Text = c.Researching && c.ResearchKind == u ? $"{100 * c.ResearchProgress / c.ResearchTime:0}%" : complete ? "Complete" : episodeLock ? "Later" : $"{Config.ResearchCost(u, c.T(u) + 1):0}";
                button.Disabled = sim.CanResearch(Local.Me, u) != ResearchResult.Ok;
            }
            else { status.Text = $"{(kind == ChamberKind.Brood ? Sim.BroodCost : Sim.StorageCost):0}"; button.Disabled = sim.CanDig(Local.Me, kind) != Sim.DigResult.Ok; }
            button.TooltipText = kind == ChamberKind.Brood ? "Incubator: eggs hatch here. Each completed chamber adds 50 population capacity." : episodeLock ? "Introduced in a later episode. Available in Skirmish." : Sim.ChamberName(kind);
            if (RoomUpgrade(kind) == c.ResearchKind && c.Researching) button.TooltipText = "Researching. Right-click to cancel and refund food.";
        }
        foreach (var (b, k) in _units)
        {
            var availability = sim.CanEnqueue(Local.Me, k);
            b.Disabled = availability != QueueResult.Ok;
            b.Modulate = new Color(1, 1, 1, availability == QueueResult.Locked ? 0.4f : b.Disabled ? 0.65f : 1f);
            b.TooltipText = sim.Mission != null && k is not (ProdKind.Worker or ProdKind.Soldier) ? "Introduced in later episodes; available in Skirmish." : $"{Config.ProdName(k)} · {Config.ProdCost(k):0} food · {Config.ProdTime(k):0}s · {Config.ProdPop(k)} population";
        }
        foreach (var (b, u, status) in _research)
        {
            b.Disabled = sim.CanResearch(Local.Me, u) != ResearchResult.Ok;
            bool active = c.Researching && c.ResearchKind == u;
            status.Text = active ? $"Research {100 * c.ResearchProgress / c.ResearchTime:0}%" : c.T(u) >= Config.MaxTier(u) ? "Complete" : sim.Mission != null ? "Later episode" : $"{Config.ResearchCost(u, c.T(u) + 1):0} food";
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
        _detail.Text = n > 0 ? "Right-click to move or attack. Workers gather food and dig chambers." : "Select ants to command them. Click a chamber to inspect it. N opens the nest.";
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
    void UpdateRoomLabels(float scale)
    {
        foreach (var entry in _labels) entry.Value.Visible = false;
        if (!Game.NestView || Game.Camera?.Cam == null) return;
        var sim = Game.Sim; var home = Game.World.Nests[Local.Me].Pos;
        foreach (var ch in sim.Col[Local.Me].Chambers)
        {
            if (!_labels.TryGetValue(ch.Id, out var panel))
            {
                panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
                panel.AddThemeStyleboxOverride("panel", UiTheme.Box(Background, Edge, 1, 3, 5));
                var l = UiTheme.Text(Sim.ChamberName(ch.Kind).ToUpperInvariant(), 11, UiTheme.Bold, Gold); l.MouseFilter = MouseFilterEnum.Ignore; panel.AddChild(l);
                _canvas.AddChild(panel); _labels[ch.Id] = panel;
            }
            var p = sim.ChamberPos(Local.Me, ch);
            var world = new Vector3(p.X, Game.World.HF.Sample(home.X, home.Y) - NestGeometry.Depth + 0.3f, p.Y);
            if (Game.Camera.Cam.IsPositionBehind(world)) continue;
            var screen = (Game.Camera.Cam.UnprojectPosition(world) - _canvas.Position) / scale;
            if (screen.X < 315 || screen.X > 1610 || screen.Y < 90 || screen.Y > 670) continue;
            panel.Position = screen - new Vector2(panel.Size.X * 0.5f, 60);
            panel.Visible = true;
        }
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
