using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

public partial class Hud : CanvasLayer
{
    public GameSession Game;
    Control _root;
    Label _food, _pop, _clock, _alert, _selTitle, _selDetail, _hint;
    HBoxContainer _selIcons;
    ProgressBar _selHp;
    readonly Button[] _prod = new Button[9];
    readonly Control[] _prodLock = new Control[9];
    readonly Label[] _heroStatus = new Label[9];
    readonly Button[] _res = new Button[5];
    readonly Label[] _resTier = new Label[5], _resCost = new Label[5];
    ProgressBar _resBar;
    Label _resName, _queenTitle;
    HBoxContainer _queueRow;
    readonly List<Button> _cmd = new();
    public Minimap Minimap;
    public SelectionBox Box;
    Control _pause, _end, _help;
    float _alertT;
    float _refresh;

    public override void _Ready()
    {
        Layer = 5;
        _root = new Control { Theme = UiTheme.Build(), MouseFilter = Control.MouseFilterEnum.Ignore };
        UiTheme.Place(_root, 0, 0, 1, 1, 0, 0, 0, 0);
        AddChild(_root);

        Box = new SelectionBox { MouseFilter = Control.MouseFilterEnum.Ignore };
        UiTheme.Place(Box, 0, 0, 1, 1, 0, 0, 0, 0);
        _root.AddChild(Box);

        BuildTopBar();
        BuildMinimap();
        BuildSelection();
        BuildProduction();
        BuildResearch();
        BuildCommands();

        _alert = UiTheme.Text("", 24, UiTheme.Title, UiTheme.Gold);
        _alert.HorizontalAlignment = HorizontalAlignment.Center;
        UiTheme.Place(_alert, 0.5f, 0, 0.5f, 0, -500, 86, 500, 130);
        _alert.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(_alert);

        _hint = UiTheme.Text("Drag to select  ·  Right-click to command  ·  A attack-move  ·  Z X C V brood  ·  Tab trails  ·  F1 help", 14, null, new Color(UiTheme.Dim, 0.8f));
        UiTheme.Place(_hint, 0.5f, 1, 0.5f, 1, -420, -24, 420, -4);
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        _hint.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(_hint);

        BuildHelp();
    }

    // ----------------------------------------------------------- layout
    void BuildTopBar()
    {
        var bar = new PanelContainer();
        UiTheme.Place(bar, 1, 0, 1, 0, -560, 12, -12, 62);
        var h = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        h.AddThemeConstantOverride("separation", 22);
        bar.AddChild(h);
        _food = Stat(h, "Food", new Color(0.95f, 0.78f, 0.35f));
        _pop = Stat(h, "Brood", UiTheme.Ink);
        _clock = Stat(h, "", UiTheme.Dim);
        var menu = new Button { Text = "Menu", TooltipText = "Pause (Esc)" };
        menu.Pressed += () => { Game.Audio.Ui("ui_click"); TogglePause(); };
        h.AddChild(menu);
        _root.AddChild(bar);

        var title = UiTheme.Text("MANDIBLE", 22, UiTheme.Title, new Color(UiTheme.Gold, 0.85f));
        UiTheme.Place(title, 0, 0, 0, 0, 22, 16, 300, 56);
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(title);
    }

    static Label Stat(HBoxContainer h, string caption, Color c)
    {
        var box = new HBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        if (caption != "") box.AddChild(UiTheme.Text(caption.ToUpper(), 12, UiTheme.Bold, UiTheme.Dim));
        var v = UiTheme.Text("0", 20, UiTheme.Bold, c);
        box.AddChild(v);
        h.AddChild(box);
        return v;
    }

    void BuildMinimap()
    {
        var frame = new PanelContainer();
        UiTheme.Place(frame, 0, 1, 0, 1, 12, -300, 300, -12);
        Minimap = new Minimap { Game = Game, CustomMinimumSize = new Vector2(268, 268) };
        frame.AddChild(Minimap);
        _root.AddChild(frame);
    }

    void BuildSelection()
    {
        var p = new PanelContainer();
        UiTheme.Place(p, 0.5f, 1, 0.5f, 1, -330, -176, 300, -30);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 6);
        _selTitle = UiTheme.Text("", 20, UiTheme.Title, UiTheme.Gold);
        _selDetail = UiTheme.Text("", 15, null, UiTheme.Dim);
        _selDetail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _selIcons = new HBoxContainer();
        _selHp = new ProgressBar { CustomMinimumSize = new Vector2(0, 10), ShowPercentage = false, MaxValue = 1 };
        v.AddChild(_selTitle);
        v.AddChild(_selIcons);
        v.AddChild(_selHp);
        v.AddChild(_selDetail);
        p.AddChild(v);
        _root.AddChild(p);
    }

    void BuildProduction()
    {
        var p = new PanelContainer();
        UiTheme.Place(p, 1, 1, 1, 1, -452, -300, -12, -178);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 6);
        var head = new HBoxContainer();
        head.AddChild(UiTheme.Text("BROOD CHAMBER", 13, UiTheme.Bold, UiTheme.Dim));
        _queueRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.End };
        _queueRow.AddThemeConstantOverride("separation", 4);
        head.AddChild(_queueRow);
        v.AddChild(head);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        (ProdKind kind, string key)[] brood = { (ProdKind.Worker, "Z"), (ProdKind.Soldier, "X"), (ProdKind.Major, "C"), (ProdKind.Chamber, "V"), (ProdKind.Flyer, "F"), (ProdKind.Gunner, "D") };
        foreach (var (kind, key) in brood) row.AddChild(ProdButton(kind, key, new Vector2(70, 70)));
        v.AddChild(row);
        p.AddChild(v);
        _root.AddChild(p);

        // royal chamber: one of each hero
        var hp = new PanelContainer();
        UiTheme.Place(hp, 1, 1, 1, 1, -452, -566, -12, -450);
        var hv = new VBoxContainer();
        hv.AddThemeConstantOverride("separation", 5);
        hv.AddChild(UiTheme.Text("ROYAL CHAMBER  ·  HEROES", 13, UiTheme.Bold, UiTheme.Dim));
        var hrow = new HBoxContainer();
        hrow.AddThemeConstantOverride("separation", 6);
        (ProdKind kind, string key)[] heroes = { (ProdKind.Warlord, "Y"), (ProdKind.Matron, "U"), (ProdKind.Marshal, "I") };
        foreach (var (kind, key) in heroes)
        {
            var b = ProdButton(kind, key, new Vector2(138, 66));
            var status = UiTheme.Text("", 11, UiTheme.Bold, UiTheme.Ink);
            UiTheme.Place(status, 0, 1, 1, 1, 46, -20, -6, -2);
            status.HorizontalAlignment = HorizontalAlignment.Right;
            b.AddChild(status);
            _heroStatus[(int)kind] = status;
            hrow.AddChild(b);
        }
        hv.AddChild(hrow);
        hp.AddChild(hv);
        _root.AddChild(hp);
    }

    Button ProdButton(ProdKind kind, string key, Vector2 size)
    {
        int i = (int)kind;
        var b = new Button { CustomMinimumSize = size, TooltipText = ProdTooltip(kind, key) };
        Control g = kind == ProdKind.Chamber ? new ChamberGlyph() : new AntGlyph { Type = Config.UnitOf(kind) };
        g.MouseFilter = Control.MouseFilterEnum.Ignore;
        if (Config.IsHeroKind(kind)) UiTheme.Place(g, 0, 0, 0, 1, 4, 4, 50, -4);
        else UiTheme.Place(g, 0, 0, 1, 1, 6, 4, -24, -20);
        b.AddChild(g);
        if (Config.IsHeroKind(kind))
        {
            var name = UiTheme.Text(Config.ProdName(kind), 12, UiTheme.Title, UiTheme.Gold);
            UiTheme.Place(name, 0, 0, 1, 0, 50, 22, -4, 40);
            b.AddChild(name);
        }
        var lockGlyph = new LockGlyph { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        UiTheme.Place(lockGlyph, 1, 1, 1, 1, -22, -24, -4, -4);
        b.AddChild(lockGlyph);
        _prodLock[i] = lockGlyph;
        var keyLabel = UiTheme.Text(key, 13, UiTheme.Bold, UiTheme.Gold);
        UiTheme.Place(keyLabel, 1, 0, 1, 0, -20, 4, -5, 22);
        b.AddChild(keyLabel);
        var cost = UiTheme.Text(((int)Config.ProdCost(kind)).ToString(), 12, UiTheme.Bold, new Color(0.95f, 0.78f, 0.35f));
        UiTheme.Place(cost, 0, 1, 1, 1, Config.IsHeroKind(kind) ? 50 : 7, -20, -6, -2);
        b.AddChild(cost);
        b.Pressed += () => Game.TryEnqueue(kind);
        b.MouseEntered += () => Game.Audio.Ui("ui_hover");
        _prod[i] = b;
        return b;
    }

    static string ProdTooltip(ProdKind k, string key)
    {
        if (k == ProdKind.Chamber)
            return $"Excavate Chamber  [{key}]\nDig a new brood chamber: +{Config.ChamberPop} brood capacity.\nCost {Config.ChamberCost} food · {Config.ChamberTime}s";
        var s = Config.Of(Config.UnitOf(k));
        string role = Config.Role(k);
        return $"{s.Name}  [{key}]\n{role}\nHP {s.Hp} · Bite {s.Damage} · Cost {s.Cost} food · {s.BuildTime}s · {s.Pop} brood";
    }

    static readonly string[] ResKeys = { "Q", "W", "E", "R", "T" };

    void BuildResearch()
    {
        var p = new PanelContainer();
        UiTheme.Place(p, 1, 1, 1, 1, -452, -444, -12, -306);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 5);
        var head = new HBoxContainer();
        head.AddChild(UiTheme.Text("COLONY CHAMBERS", 13, UiTheme.Bold, UiTheme.Dim));
        _queenTitle = UiTheme.Text("", 13, UiTheme.Title, UiTheme.Gold);
        _queenTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _queenTitle.HorizontalAlignment = HorizontalAlignment.Right;
        head.AddChild(_queenTitle);
        v.AddChild(head);
        var row = new HBoxContainer();
        for (int i = 0; i < 5; i++)
        {
            var u = (Upgrade)i;
            var b = new Button { CustomMinimumSize = new Vector2(80, 62) };
            var g = new ResearchGlyph { Kind = u, MouseFilter = Control.MouseFilterEnum.Ignore };
            UiTheme.Place(g, 0, 0, 1, 1, 6, 4, -30, -18);
            b.AddChild(g);
            var key = UiTheme.Text(ResKeys[i], 13, UiTheme.Bold, UiTheme.Gold);
            UiTheme.Place(key, 1, 0, 1, 0, -20, 3, -5, 20);
            b.AddChild(key);
            var tier = UiTheme.Text("", 11, UiTheme.Bold, UiTheme.Ink);
            UiTheme.Place(tier, 1, 1, 1, 1, -34, -20, -5, -2);
            tier.HorizontalAlignment = HorizontalAlignment.Right;
            b.AddChild(tier);
            var cost = UiTheme.Text("", 12, UiTheme.Bold, new Color(0.95f, 0.78f, 0.35f));
            UiTheme.Place(cost, 0, 1, 1, 1, 7, -20, -6, -2);
            b.AddChild(cost);
            b.Pressed += () => Game.TryResearch(u);
            b.GuiInput += e =>
            {
                if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right) { Game.Sim.CancelResearch(Config.Player); Game.Audio.Ui("ui_click"); }
            };
            b.MouseEntered += () => Game.Audio.Ui("ui_hover");
            _res[i] = b; _resTier[i] = tier; _resCost[i] = cost;
            row.AddChild(b);
        }
        v.AddChild(row);
        var prog = new HBoxContainer();
        _resName = UiTheme.Text("", 12, null, UiTheme.Dim);
        _resName.CustomMinimumSize = new Vector2(190, 0);
        _resBar = new ProgressBar { CustomMinimumSize = new Vector2(0, 8), ShowPercentage = false, MaxValue = 1, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        prog.AddChild(_resName);
        prog.AddChild(_resBar);
        v.AddChild(prog);
        p.AddChild(v);
        _root.AddChild(p);
    }

    static string ResearchTooltip(Colony c, Upgrade u)
    {
        int next = c.T(u) + 1;
        string head = next > Config.MaxTier(u) ? $"{Config.ResearchName(u, c.T(u))}  (complete)" : $"{Config.ResearchName(u, next)}  [{ResKeys[(int)u]}]";
        string body = Config.ResearchDesc(u);
        if (next > Config.MaxTier(u)) return $"{head}\n{body}";
        int need = Config.QueenNeeded(u, next);
        string req = c.T(Upgrade.Queen) < need ? $"\nRequires a {Config.QueenTitles[need]} queen." : "";
        return $"{head}\n{body}{req}\nCost {Config.ResearchCost(u, next)} food · {Config.ResearchTime(u, next)}s   (right-click to cancel research)";
    }

    void BuildCommands()
    {
        var p = new PanelContainer();
        UiTheme.Place(p, 1, 1, 1, 1, -452, -170, -12, -30);
        var grid = new GridContainer { Columns = 4 };
        (string label, string tip, Action act)[] cmds =
        {
            ("Attack  A", "Attack-move: next click sends units forward, fighting anything on the way.", () => Game.Input.BeginAttackMove()),
            ("Stop  S", "Stop and stand ground.", () => Game.Input.Stop(false)),
            ("Hold  H", "Hold position: bite only what comes in reach.", () => Game.Input.Stop(true)),
            ("Gather  G", "Workers: next click on food assigns them to that pile.", () => Game.Input.BeginGather()),
            ("Army  F2", "Select every soldier and major.", () => Game.Input.SelectArmy()),
            ("Workers  F3", "Select every worker.", () => Game.Input.SelectWorkers()),
            ("Trails  Tab", "Show your colony's food pheromone trails.", () => Game.ToggleTrails()),
            ("Nest  Home", "Jump to your nest and select it.", () => Game.Input.SelectNest()),
        };
        foreach (var (label, tip, act) in cmds)
        {
            var b = new Button { Text = label, TooltipText = tip, CustomMinimumSize = new Vector2(100, 58) };
            b.AddThemeFontSizeOverride("font_size", 14);
            b.Pressed += () => { Game.Audio.Ui("ui_click"); act(); };
            b.MouseEntered += () => Game.Audio.Ui("ui_hover");
            grid.AddChild(b);
            _cmd.Add(b);
        }
        p.AddChild(grid);
        _root.AddChild(p);
    }

    void BuildHelp()
    {
        _help = Overlay("FIELD MANUAL", new[]
        {
            "Your queen lives at the black crater. Keep her alive; destroy the red queen to win.",
            "Workers forage on their own: they find food, lay pheromone trails home, and the colony follows the trails.",
            "Fallen ants become food. Win a fight and your workers will haul the dead home.",
            "",
            "Left-click / drag  select ants   ·   Shift adds   ·   Double-click selects all of that kind on screen",
            "Right-click  move / attack / gather (workers on food)   ·   A + click  attack-move",
            "S stop   ·   H hold   ·   G gather   ·   Ctrl+1-9 save group, 1-9 recall",
            "Brood:  Z worker  ·  X soldier  ·  C major  ·  V chamber  ·  F flying ant  ·  D acid gunner",
            "Chambers:  Q royal jelly (queen level)  ·  W wings  ·  E mandible forge  ·  R chitin  ·  T silk cutters",
            "Heroes:  Y Warlord  ·  U Acid Matron  ·  I Sky Marshal. They level to 10 and cast specials on their own.",
            "Wild bugs guard rich food. Beetles sweep, spiders shoot webs, wasps dive and sting. Their carcasses are feasts.",
            "Click the nest, then right-click the ground to set a rally point.",
            "Arrows / screen edge pan   ·   Mouse wheel zoom   ·   Middle-drag rotate   ·   Space jump to last alert",
            "Hold Alt to show health bars   ·   F11 fullscreen   ·   Esc pause",
        }, ("Close", () => _help.Visible = false));
    }

    Control Overlay(string title, string[] lines, params (string, Action)[] buttons)
    {
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f), Visible = false };
        UiTheme.Place(dim, 0, 0, 1, 1, 0, 0, 0, 0);
        var p = new PanelContainer();
        UiTheme.Place(p, 0.5f, 0.5f, 0.5f, 0.5f, -430, -260, 430, 260);
        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.AddThemeConstantOverride("separation", 10);
        var t = UiTheme.Text(title, 34, UiTheme.Title, UiTheme.Gold);
        t.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(t);
        foreach (var l in lines)
        {
            var lab = UiTheme.Text(l, 16, null, UiTheme.Ink);
            lab.HorizontalAlignment = HorizontalAlignment.Center;
            lab.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            v.AddChild(lab);
        }
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        foreach (var (label, act) in buttons)
        {
            var b = new Button { Text = label, CustomMinimumSize = new Vector2(180, 48) };
            b.Pressed += () => { Game.Audio.Ui("ui_click"); act(); };
            b.MouseEntered += () => Game.Audio.Ui("ui_hover");
            row.AddChild(b);
        }
        v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        v.AddChild(row);
        p.AddChild(v);
        dim.AddChild(p);
        _root.AddChild(dim);
        return dim;
    }

    public void ToggleHelp() => _help.Visible = !_help.Visible;

    public void TogglePause()
    {
        if (_end != null && _end.Visible) return;
        if (_pause == null)
            _pause = Overlay("PAUSED", new[] { "The colony waits." },
                ("Resume", () => TogglePause()), ("Restart", () => Game.Restart()), ("Main Menu", () => Game.QuitToMenu()));
        _pause.Visible = !_pause.Visible;
        Game.SetPaused(_pause.Visible);
    }

    public bool Blocking => (_pause?.Visible ?? false) || (_end?.Visible ?? false) || _help.Visible;

    public void ShowEnd(bool victory, Colony me, Colony foe, float time)
    {
        var lines = new[]
        {
            victory ? "The red queen has fallen. The backyard is yours." : "Your queen is dead. The colony scatters into the grass.",
            "",
            $"Time  {(int)time / 60}:{(int)time % 60:00}",
            $"Food gathered  {(int)me.Gathered}   ·   Brood raised  {me.Produced}",
            $"Enemies slain  {me.Killed}   ·   Ants lost  {me.Lost}",
        };
        _end = Overlay(victory ? "VICTORY" : "DEFEAT", lines, ("Play Again", () => Game.Restart()), ("Main Menu", () => Game.QuitToMenu()));
        _end.Visible = true;
    }

    public void Alert(string text, Color? c = null)
    {
        _alert.Text = text;
        _alert.AddThemeColorOverride("font_color", c ?? UiTheme.Gold);
        _alertT = 3.2f;
    }

    // ----------------------------------------------------------- refresh
    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (Game?.Sim == null) return;
        _alertT -= dt;
        _alert.Modulate = new Color(1, 1, 1, Mathf.Clamp(_alertT, 0, 1));
        _refresh -= dt;
        if (_refresh > 0) return;
        _refresh = 0.1f;
        var sim = Game.Sim;
        var c = sim.Col[Config.Player];
        _food.Text = ((int)c.Food).ToString();
        _pop.Text = $"{c.PopUsed} / {c.PopCap}";
        _pop.AddThemeColorOverride("font_color", c.PopUsed >= c.PopCap ? new Color(1, 0.45f, 0.3f) : UiTheme.Ink);
        _clock.Text = $"{(int)sim.Time / 60}:{(int)sim.Time % 60:00}";
        for (int i = 0; i < _prod.Length; i++)
        {
            var k = (ProdKind)i;
            bool unlocked = Config.Unlocked(k, c.Tier);
            bool ok = unlocked && c.Alive && c.Food >= Config.ProdCost(k) && c.Queue.Count < Config.QueueLimit;
            if (k == ProdKind.Chamber && c.PopCap + (c.PendingChambers() + 1) * Config.ChamberPop > Config.MaxPopCap) ok = false;
            if (Config.IsHeroKind(k))
            {
                var ht = Config.UnitOf(k);
                bool alive = c.Count[(int)ht] > 0, queued = c.Queue.Exists(q => q.Kind == k);
                if (alive || queued) ok = false;
                string st = "";
                if (alive)
                    for (int j = 0; j < sim.High; j++)
                        if (sim.Ants[j].Alive && sim.Ants[j].Team == Config.Player && sim.Ants[j].Type == ht) { st = $"Lv {sim.Ants[j].Level}"; break; }
                _heroStatus[i].Text = alive ? st : queued ? "hatching" : unlocked ? $"x{Config.HeroTierMul(ht, c.T(Upgrade.Queen)):0.00}" : "";
                _heroStatus[i].AddThemeColorOverride("font_color", alive ? new Color(0.6f, 1f, 0.5f) : UiTheme.Ink);
            }
            _prod[i].Modulate = ok ? Colors.White : new Color(1, 1, 1, unlocked ? 0.45f : 0.3f);
            _prodLock[i].Visible = !unlocked;
        }
        for (int i = 0; i < 5; i++)
        {
            var u = (Upgrade)i;
            int next = c.T(u) + 1;
            bool maxed = next > Config.MaxTier(u);
            var r = Game.Sim.CanResearch(Config.Player, u);
            bool active = c.Researching && c.ResearchKind == u;
            _res[i].Modulate = active ? new Color(1.2f, 1.1f, 0.8f) : r == ResearchResult.Ok ? Colors.White : new Color(1, 1, 1, maxed ? 0.55f : 0.35f);
            _res[i].TooltipText = ResearchTooltip(c, u);
            _resTier[i].Text = Config.MaxTier(u) > 1 ? $"{c.T(u)}/{Config.MaxTier(u)}" : (c.T(u) > 0 ? "done" : "");
            _resCost[i].Text = maxed ? "" : ((int)Config.ResearchCost(u, next)).ToString();
        }
        _queenTitle.Text = Config.QueenTitles[c.T(Upgrade.Queen)];
        if (c.Researching)
        {
            _resName.Text = Config.ResearchName(c.ResearchKind, c.T(c.ResearchKind) + 1);
            _resBar.Value = c.ResearchProgress / c.ResearchTime;
        }
        else { _resName.Text = "No research underway"; _resBar.Value = 0; }
        RefreshQueue(c);
        RefreshSelection();
    }

    void RefreshQueue(Colony c)
    {
        while (_queueRow.GetChildCount() < Config.QueueLimit)
        {
            var slot = new QueueSlot { CustomMinimumSize = new Vector2(26, 26), MouseFilter = Control.MouseFilterEnum.Stop, TooltipText = "Right-click to cancel the last order" };
            slot.GuiInput += e =>
            {
                if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right) { Game.Sim.CancelLast(Config.Player); Game.Audio.Ui("ui_click"); }
            };
            _queueRow.AddChild(slot);
        }
        for (int i = 0; i < Config.QueueLimit; i++)
        {
            var slot = (QueueSlot)_queueRow.GetChild(i);
            if (i < c.Queue.Count)
            {
                slot.Visible = true;
                slot.Kind = c.Queue[i].Kind;
                slot.Progress = i == 0 ? c.Queue[0].Progress / c.Queue[0].Time : 0;
                slot.Blocked = i == 0 && c.PopBlocked;
                slot.QueueRedraw();
            }
            else slot.Visible = false;
        }
    }

    void RefreshSelection()
    {
        var sim = Game.Sim;
        var sel = Game.Input.Selection;
        int[] counts = new int[13];
        float hp = 0, max = 0;
        int alive = 0, single = -1;
        foreach (int id in sel)
        {
            ref var a = ref sim.Ants[id];
            if (!a.Alive) continue;
            counts[(int)a.Type]++;
            hp += a.Hp; max += a.MaxHp;
            alive++;
            single = id;
        }
        foreach (var ch in _selIcons.GetChildren()) ch.QueueFree();
        if (Game.Input.NestSelected)
        {
            var c = sim.Col[Config.Player];
            ref var q = ref sim.Ants[c.QueenId];
            _selTitle.Text = "The Nest";
            _selHp.Value = q.Alive ? q.Hp / Config.Stats[3].Hp : 0;
            _selDetail.Text = $"Queen {(int)Mathf.Max(0, q.Hp)} / {Config.Stats[3].Hp}.  Right-click the ground to set a rally point.  {c.Count[0]} workers · {c.Count[1]} soldiers · {c.Count[2]} majors.";
            AddIcon(AntType.Queen, 1);
            return;
        }
        if (alive == 0 && sim.Valid(Game.Input.Inspect, Game.Input.InspectSerial) && (Game.Fog == null || Game.Fog.Visible(sim.Ants[Game.Input.Inspect].Pos)))
        {
            ref var a = ref sim.Ants[Game.Input.Inspect];
            var st = Config.Of(a.Type);
            bool wild = a.Team == Config.Wild;
            _selTitle.Text = $"{st.Name}  ·  Level {a.Level}";
            _selTitle.AddThemeColorOverride("font_color", Config.TeamUi(a.Team));
            _selHp.Value = a.Hp / a.MaxHp;
            _selDetail.Text = wild
                 ? $"Wild bug guarding its lair.  HP {(int)a.Hp} / {(int)a.MaxHp}  ·  Bite {st.Damage * a.DmgMul:0}.  Slay it for experience and a feast of {st.CorpseFood:0} food."
                : $"Red colony.  HP {(int)a.Hp} / {(int)a.MaxHp}  ·  Bite {st.Damage * a.DmgMul:0}";
            AddIcon(a.Type, 1);
            return;
        }
        _selTitle.AddThemeColorOverride("font_color", UiTheme.Gold);
        if (alive == 0)
        {
            var c = sim.Col[Config.Player];
            _selTitle.Text = $"Black Colony  ·  {Config.QueenTitles[c.T(Upgrade.Queen)]}";
            ref var q = ref sim.Ants[c.QueenId];
            _selHp.Value = q.Alive ? q.Hp / Config.Stats[3].Hp : 0;
            _selDetail.Text = "Nothing selected.  Drag across your ants to command them.";
            for (int t = 0; t < 3; t++) AddIcon((AntType)t, c.Count[t]);
            foreach (int t in new[] { 8, 9, 10, 11, 12 }) if (c.Count[t] > 0) AddIcon((AntType)t, c.Count[t]);
            return;
        }
        _selHp.Value = hp / Mathf.Max(1, max);
        if (alive == 1)
        {
            ref var a = ref sim.Ants[single];
            var st = Config.Of(a.Type);
            _selTitle.Text = Config.IsWarrior(a.Type) ? $"{st.Name}  ·  Level {a.Level}" : st.Name;
            string doing = a.State switch
            {
                AntState.Forage => "Searching for food",
                AntState.Gather => "Heading to food",
                AntState.Return => "Carrying food home",
                AntState.Move => "Moving",
                AntState.AttackMove => "Advancing",
                AntState.Engage => "Fighting",
                AntState.Hold => "Holding position",
                _ => "Standing guard",
            };
            string xp = !Config.IsWarrior(a.Type) ? "" : a.Level >= Config.MaxLevelOf(a.Type) ? "  ·  Elite (max rank)" : $"  ·  XP {(int)a.Xp} / {Sim.NextXp(a)}";
            if (Config.IsHero(a.Type)) xp += a.AbilityCd <= 0 ? $"  ·  {Config.AbilityName(a.Type)} ready" : $"  ·  {Config.AbilityName(a.Type)} in {a.AbilityCd:0}s";
            if (a.BuffT > 0) xp += "  ·  War Cry!";
            _selDetail.Text = $"{doing}.  HP {(int)a.Hp} / {(int)a.MaxHp}  ·  Bite {st.Damage * a.DmgMul:0.#}{xp}";
            AddIcon(a.Type, 1);
            return;
        }
        var parts = new List<string>();
        foreach (int t in new[] { 0, 1, 2, 8, 9, 10, 11, 12 }) if (counts[t] > 0) { AddIcon((AntType)t, counts[t]); parts.Add($"{counts[t]} {Config.Stats[t].Name.ToLower()}{(counts[t] > 1 ? "s" : "")}"); }
        int vets = 0;
        foreach (int id in sel) if (sim.Ants[id].Alive && sim.Ants[id].Level > 1) vets++;
        if (vets > 0) parts.Add($"{vets} veteran{(vets > 1 ? "s" : "")}");
        _selTitle.Text = $"{alive} ants";
        _selDetail.Text = string.Join(" · ", parts) + ".  Right-click to command.";
    }

    void AddIcon(AntType t, int count)
    {
        var box = new Control { CustomMinimumSize = new Vector2(54, 40) };
        var g = new AntGlyph { Type = t, Tint = UiTheme.Gold };
        UiTheme.Place(g, 0, 0, 0, 0, 0, 0, 38, 40);
        box.AddChild(g);
        var l = UiTheme.Text(count.ToString(), 15, UiTheme.Bold, UiTheme.Ink);
        UiTheme.Place(l, 0, 0, 0, 0, 34, 16, 60, 40);
        box.AddChild(l);
        _selIcons.AddChild(box);
    }
}

public partial class QueueSlot : Control
{
    public ProdKind Kind;
    public float Progress;
    public bool Blocked;

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        DrawRect(r, new Color(0.1f, 0.08f, 0.06f, 0.95f));
        if (Progress > 0) DrawRect(new Rect2(0, Size.Y * (1 - Progress), Size.X, Size.Y * Progress), Blocked ? new Color(0.8f, 0.25f, 0.15f, 0.6f) : new Color(UiTheme.Gold, 0.45f));
        DrawRect(r, UiTheme.Bronze, false, 1);
        string s = Kind switch { ProdKind.Worker => "W", ProdKind.Soldier => "S", ProdKind.Major => "M", _ => "+" };
        DrawString(UiTheme.Bold, new Vector2(0, Size.Y * 0.7f), s, HorizontalAlignment.Center, Size.X, 14, UiTheme.Ink);
    }
}

public partial class LockGlyph : Control
{
    public override void _Draw()
    {
        var s = Size;
        var col = new Color(1f, 0.55f, 0.35f);
        DrawArc(new Vector2(s.X / 2, s.Y * 0.45f), s.X * 0.26f, Mathf.Pi, Mathf.Tau, 12, col, 2f, true);
        DrawRect(new Rect2(s.X * 0.18f, s.Y * 0.45f, s.X * 0.64f, s.Y * 0.5f), col);
    }
}

public partial class ChamberGlyph : Control
{
    public override void _Draw()
    {
        var c = Size / 2;
        float u = Mathf.Min(Size.X, Size.Y) / 10f;
        DrawArc(c + new Vector2(0, u), u * 3.6f, Mathf.Pi, Mathf.Tau, 24, UiTheme.Gold, u * 0.5f, true);
        DrawCircle(c + new Vector2(-u * 1.4f, u * 1.6f), u * 1.0f, UiTheme.Gold);
        DrawCircle(c + new Vector2(u * 1.5f, u * 1.4f), u * 0.8f, UiTheme.Gold);
        DrawLine(c + new Vector2(-u * 4, u * 3), c + new Vector2(u * 4, u * 3), UiTheme.Gold, u * 0.4f);
        DrawString(UiTheme.Bold, c + new Vector2(u * 2.6f, -u * 2.6f), "+", HorizontalAlignment.Left, -1, (int)(u * 4), UiTheme.Gold);
    }
}

public partial class SelectionBox : Control
{
    public bool Active;
    public Rect2 Rect;

    public override void _Draw()
    {
        if (!Active) return;
        var r = Rect.Abs();
        DrawRect(r, new Color(0.55f, 1f, 0.45f, 0.08f));
        DrawRect(r, new Color(0.55f, 1f, 0.45f, 0.85f), false, 1.5f);
    }
}
