using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The general's skill bar and skill tree, Warcraft III style. The bar shows the selected general's three skills
/// (Q W E): rank marks, cooldown, autocast (right-click or long-press toggles it). Every level-up is a skill point:
/// the tree opens on its own and the Skills button glows until the point is spent (O opens it any time).
/// Every action here goes through a Cmd (Learn, Cast, SkillAuto), so multiplayer and saved games replay it.
public partial class GeneralSkills : Control
{
    public GameSession Game;
    Control _bar, _tree;
    readonly Button[] _btn = new Button[3];
    readonly Label[] _key = new Label[3], _name = new Label[3], _rank = new Label[3], _cd = new Label[3];
    readonly ColorRect[] _shade = new ColorRect[3];
    Button _learn;
    Label _treeTitle, _treeSub;
    readonly Label[] _colName = new Label[3], _colLore = new Label[3];
    readonly VBoxContainer[] _colRanks = new VBoxContainer[3];
    readonly Button[] _colLearn = new Button[3];
    int _shown = -1, _shownSerial;
    float _refresh, _pulse;
    readonly ulong[] _pressMs = new ulong[3];
    static readonly Color Gold = new("f4cd70"), Edge = new("a77e32"), Bg = new(0.035f, 0.031f, 0.022f, 0.94f);
    static readonly Color Ready = new(1f, 0.86f, 0.45f), Spent = new(0.95f, 0.72f, 0.3f), Locked = new(0.5f, 0.42f, 0.32f);
    public bool TreeOpen => _tree.Visible;

    StyleBoxFlat Frame(Color bg, Color border, int bw = 1) => UiTheme.Box(bg, border, bw, 4, 6);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        UiTheme.Place(this, 0, 0, 1, 1, 0, 0, 0, 0);
        BuildBar();
        BuildTree();
    }

    void BuildBar()
    {
        _bar = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(452, 104), Visible = false };
        AddChild(_bar);
        for (int s = 0; s < 3; s++)
        {
            int slot = s;
            var b = new Button { Position = new Vector2(s * 112, 0), Size = new Vector2(104, 104), FocusMode = FocusModeEnum.None, ClipContents = true };
            b.AddThemeStyleboxOverride("normal", Frame(Bg, Edge));
            b.AddThemeStyleboxOverride("hover", Frame(new Color(0.09f, 0.07f, 0.04f, 0.96f), Gold));
            b.AddThemeStyleboxOverride("pressed", Frame(new Color(0.14f, 0.1f, 0.05f, 0.96f), Gold, 2));
            b.AddThemeStyleboxOverride("disabled", Frame(Bg, new Color(Edge, 0.5f)));
            b.GuiInput += e => SkillInput(slot, e);
            b.MouseEntered += () => Game.Audio.Ui("ui_hover");
            _shade[s] = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Ignore, Position = Vector2.Zero, Size = new Vector2(104, 0) };
            b.AddChild(_shade[s]);
            _key[s] = Lbl(b, Skills.Keys[s], 6, 3, 30, 22, 17, UiTheme.Title, Gold);
            _rank[s] = Lbl(b, "", 44, 5, 54, 18, 13, UiTheme.Bold, Spent);
            _rank[s].HorizontalAlignment = HorizontalAlignment.Right;
            _name[s] = Lbl(b, "", 4, 30, 96, 46, 14, UiTheme.Bold, UiTheme.Ink);
            _name[s].HorizontalAlignment = HorizontalAlignment.Center;
            _name[s].AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _cd[s] = Lbl(b, "", 4, 74, 96, 26, 17, UiTheme.Bold, UiTheme.Ink);
            _cd[s].HorizontalAlignment = HorizontalAlignment.Center;
            _btn[s] = b;
            _bar.AddChild(b);
        }
        _learn = new Button { Position = new Vector2(336, 0), Size = new Vector2(112, 104), FocusMode = FocusModeEnum.None, Text = "SKILLS\nO" };
        _learn.AddThemeFontOverride("font", UiTheme.Title);
        _learn.AddThemeFontSizeOverride("font_size", 15);
        _learn.AddThemeStyleboxOverride("normal", Frame(Bg, Edge));
        _learn.AddThemeStyleboxOverride("hover", Frame(new Color(0.09f, 0.07f, 0.04f, 0.96f), Gold));
        _learn.AddThemeStyleboxOverride("pressed", Frame(new Color(0.14f, 0.1f, 0.05f, 0.96f), Gold, 2));
        _learn.TooltipText = "The skill tree: spend skill points (one each level).";
        _learn.Pressed += () => { Game.Audio.Ui("ui_click"); ToggleTree(); };
        _bar.AddChild(_learn);
    }

    static Label Lbl(Control parent, string text, float x, float y, float w, float h, int size, Font font, Color c)
    {
        var l = UiTheme.Text(text, size, font, c);
        l.Position = new Vector2(x, y); l.Size = new Vector2(w, h);
        l.MouseFilter = MouseFilterEnum.Ignore;
        l.ClipText = false;
        parent.AddChild(l);
        return l;
    }

    void BuildTree()
    {
        _tree = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Stop };
        _tree.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.03f, 0.025f, 0.018f, 0.96f), Gold, 2, 6, 18));
        AddChild(_tree);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        _tree.AddChild(v);
        var head = new HBoxContainer();
        _treeTitle = UiTheme.Text("", 26, UiTheme.Title, Gold);
        _treeTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(_treeTitle);
        var close = new Button { Text = Settings.Mobile ? "Close" : "Close  (O)", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(120, 40) };
        close.Pressed += () => { Game.Audio.Ui("ui_click"); _tree.Visible = false; };
        head.AddChild(close);
        v.AddChild(head);
        _treeSub = UiTheme.Text("", 15, UiTheme.Body, UiTheme.Dim);
        v.AddChild(_treeSub);
        var cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 12);
        v.AddChild(cols);
        for (int s = 0; s < 3; s++)
        {
            int slot = s;
            var col = new PanelContainer { CustomMinimumSize = new Vector2(330, 0) };
            col.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.06f, 0.05f, 0.035f, 0.95f), Edge, 1, 4, 12));
            var cv = new VBoxContainer();
            cv.AddThemeConstantOverride("separation", 6);
            col.AddChild(cv);
            _colName[s] = UiTheme.Text("", 19, UiTheme.Title, Gold);
            cv.AddChild(_colName[s]);
            _colLore[s] = UiTheme.Text("", 13, UiTheme.Body, UiTheme.Dim);
            _colLore[s].AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _colLore[s].CustomMinimumSize = new Vector2(300, 54);
            cv.AddChild(_colLore[s]);
            _colRanks[s] = new VBoxContainer();
            _colRanks[s].AddThemeConstantOverride("separation", 4);
            cv.AddChild(_colRanks[s]);
            _colLearn[s] = new Button { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 44) };
            _colLearn[s].AddThemeFontOverride("font", UiTheme.Title);
            _colLearn[s].Pressed += () => Learn(slot);
            cv.AddChild(_colLearn[s]);
            cols.AddChild(col);
        }
    }

    /// The general the bar is about: in Lane War your hero; otherwise the first general in the selection.
    public int General()
    {
        var sim = Game.Sim;
        if (Game.Lanes) { int h = sim.HeroId[Local.Me]; return h >= 0 && sim.Ants[h].Alive ? h : -1; }
        foreach (int id in Game.Input.Selection)
            if (id >= 0 && id < sim.High && sim.Ants[id].Alive && sim.Ants[id].Team == Local.Me && Config.IsHero(sim.Ants[id].Type)) return id;
        return -1;
    }

    /// Your general with a point to spend, selected or not (the level-up opens the tree for it).
    int PendingGeneral()
    {
        var sim = Game.Sim;
        int sel = General();
        if (sel >= 0 && sim.Ants[sel].SkPts > 0) return sel;
        for (int i = 0; i < sim.High; i++)
            if (sim.Ants[i].Alive && sim.Ants[i].Team == Local.Me && Config.IsHero(sim.Ants[i].Type) && Sim.AnyLearnable(sim.Ants[i])) return i;
        return -1;
    }

    public void ToggleTree()
    {
        if (_tree.Visible) { _tree.Visible = false; return; }
        int id = General();
        if (id < 0 || !Sim.AnyLearnable(Game.Sim.Ants[id])) { int p = PendingGeneral(); if (p >= 0) id = p; }
        if (id < 0) { Game.Hud.Alert("Select a general to see its skills", UiTheme.Dim); Game.Audio.Ui("ui_error"); return; }
        Open(id);
    }

    /// A general of ours levelled up: show the tree so the point can be spent at once.
    public void OnLevelUp(int id)
    {
        if (id < 0 || Game.Demo) return;
        Open(id);
    }

    void Open(int id)
    {
        _shown = id;
        _shownSerial = Game.Sim.Ants[id].Serial;
        _tree.Visible = true;
        RefreshTree();
        Game.Audio.Ui("research", -6);
    }

    void Learn(int slot)
    {
        if (_shown < 0 || !Game.Sim.Valid(_shown, _shownSerial)) return;
        ref var a = ref Game.Sim.Ants[_shown];
        if (!Sim.CanLearn(a, slot)) { Game.Audio.Ui("ui_error"); return; }
        Game.Issue(new Cmd { Kind = CmdKind.Learn, Team = (byte)Local.Me, Ids = new[] { _shown }, Serials = new[] { _shownSerial }, Arg = (byte)slot });
        Game.Audio.Ui("research", -2);
        Game.Hud.Alert($"{Skills.Name(Skills.At(a.Type, slot)).ToUpperInvariant()}: rank {Sim.Rank(a, slot) + 1} learnt", Gold);
        // the order lands this tick (or on the next lockstep turn); close when nothing more can be learnt
        if (a.SkPts <= 1) _tree.Visible = false;
    }

    void SkillInput(int slot, InputEvent e)
    {
        if (e is not InputEventMouseButton mb) return;
        if (mb.ButtonIndex == MouseButton.Right && mb.Pressed) { Game.ToggleAutocast(slot); AcceptEvent(); return; }
        if (mb.ButtonIndex != MouseButton.Left) return;
        if (mb.Pressed) { _pressMs[slot] = Time.GetTicksMsec(); return; }
        // a finger has no right button: a long press toggles autocast
        if (Settings.Mobile && Time.GetTicksMsec() - _pressMs[slot] > 500) Game.ToggleAutocast(slot);
        else Game.TrySkill(slot);
        AcceptEvent();
    }

    public override void _Process(double delta)
    {
        _refresh -= (float)delta;
        _pulse += (float)delta;
        if (_refresh > 0) return;
        _refresh = 0.1f;
        var vp = GetViewportRect().Size;
        int id = General();
        _bar.Visible = id >= 0 && !Game.Hud.Blocking;
        // above the bottom panels, centred; scaled with the rest of the interface
        float k = Mathf.Clamp(Mathf.Min(vp.X / 1672f, vp.Y / 941f), 0.6f, 1.4f);
        if (Settings.Mobile) k = Mathf.Max(k, 0.9f);
        float bottom = Game.Hud.CommandHud != null ? Game.Hud.CommandHud.BottomEdge : vp.Y - 34;
        _bar.Scale = Vector2.One * k;
        _bar.Position = new Vector2((vp.X - 452 * k) / 2, bottom - 112 * k);
        if (_tree.Visible)
        {
            var ts = _tree.GetCombinedMinimumSize();
            float tk = Mathf.Min(1f, Mathf.Min((vp.X - 24) / ts.X, (vp.Y - 24) / ts.Y));
            _tree.Scale = Vector2.One * tk;
            _tree.Position = (vp - ts * tk) / 2;
            RefreshTree();
        }
        if (id < 0) return;
        ref var a = ref Game.Sim.Ants[id];
        for (int s = 0; s < 3; s++)
        {
            var sk = Skills.At(a.Type, s);
            int r = Sim.Rank(a, s), max = Skills.MaxRank(sk);
            float cd = Sim.SkillCd(a, s), full = Skills.Cooldown(sk, Mathf.Max(1, r), a.Level);
            _name[s].Text = Skills.Name(sk);
            _name[s].AddThemeColorOverride("font_color", r == 0 ? Locked : UiTheme.Ink);
            _rank[s].Text = r == 0 ? "" : new string('◆', r) + new string('◇', max - r);
            bool auto = (a.AutoMask & (1 << s)) != 0 && !a.Manual;
            _cd[s].Text = r == 0 ? (Skills.ReqLevel(sk, 1) > a.Level ? $"Lv {Skills.ReqLevel(sk, 1)}" : "learn") : cd > 0 ? $"{Mathf.Ceil(cd):0}" : auto ? "AUTO" : "READY";
            _cd[s].AddThemeColorOverride("font_color", r == 0 ? Locked : cd > 0 ? UiTheme.Ink : auto ? Spent : Ready);
            _shade[s].Size = new Vector2(104, r == 0 ? 104 : cd > 0 ? 104 * Mathf.Clamp(cd / full, 0, 1) : 0);
            var border = r > 0 && cd <= 0 ? (auto ? Spent : Ready) : Edge;
            _btn[s].AddThemeStyleboxOverride("normal", Frame(Bg, border, r > 0 && cd <= 0 ? 2 : 1));
            _btn[s].TooltipText = Tip(a, s);
        }
        bool points = Sim.AnyLearnable(a);
        float glow = points ? 0.5f + 0.5f * Mathf.Sin(_pulse * 5f) : 0;
        _learn.Text = points ? $"SKILLS  O\n+{a.SkPts} point{(a.SkPts == 1 ? "" : "s")}" : Settings.Mobile ? "SKILLS" : "SKILLS\nO";
        _learn.AddThemeStyleboxOverride("normal", Frame(points ? new Color(0.16f, 0.11f, 0.04f, 0.96f).Lerp(new Color(0.3f, 0.2f, 0.06f, 0.96f), glow) : Bg, points ? Gold : Edge, points ? 2 : 1));
        _learn.AddThemeColorOverride("font_color", points ? Gold.Lerp(Colors.White, glow * 0.4f) : UiTheme.Ink);
    }

    /// Tooltip for a skill button: what it does now and what the next rank adds.
    static string Tip(in Ant a, int s)
    {
        var sk = Skills.At(a.Type, s);
        int r = Sim.Rank(a, s), max = Skills.MaxRank(sk);
        var t = $"{Skills.Name(sk).ToUpperInvariant()}  ({Skills.Keys[s]})   rank {r}/{max}{(Skills.Ultimate(sk) ? "  ·  ultimate" : "")}\n{Skills.Lore(sk)}\n";
        if (r > 0) t += $"\nNow: {Skills.RankLine(sk, r)}\nCooldown {Skills.Cooldown(sk, r, a.Level):0}s.";
        if (r < max) t += $"\nNext (level {Skills.ReqLevel(sk, r + 1)}): {Skills.RankLine(sk, r + 1)}";
        t += Settings.Mobile ? "\nLong-press: autocast on/off." : "\nRight-click: autocast on/off.";
        return t;
    }

    void RefreshTree()
    {
        var sim = Game.Sim;
        if (_shown < 0 || !sim.Valid(_shown, _shownSerial)) { _tree.Visible = false; return; }
        ref var a = ref sim.Ants[_shown];
        _treeTitle.Text = $"{Config.Of(a.Type).Name.ToUpperInvariant()}  ·  LEVEL {a.Level}";
        _treeSub.Text = a.SkPts > 0
            ? $"{a.SkPts} skill point{(a.SkPts == 1 ? "" : "s")} to spend. A new rank works at once.  Basic ranks open at levels 1, 3, 5, 7; the ultimate at 6, 8 and 10."
            : a.Level >= Config.HeroMaxLevel ? "Every skill mastered." : "No skill point to spend: generals level up by eating remains. Each level brings one point.";
        for (int s = 0; s < 3; s++)
        {
            var sk = Skills.At(a.Type, s);
            int r = Sim.Rank(a, s), max = Skills.MaxRank(sk);
            _colName[s].Text = $"{Skills.Keys[s]}   {Skills.Name(sk)}{(Skills.Ultimate(sk) ? "   (ultimate)" : "")}";
            _colLore[s].Text = Skills.Lore(sk);
            var rows = _colRanks[s];
            while (rows.GetChildCount() < max)
            {
                var l = UiTheme.Text("", 13, UiTheme.Body, UiTheme.Ink);
                l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                l.CustomMinimumSize = new Vector2(300, 0);
                rows.AddChild(l);
            }
            for (int k = 1; k <= max; k++)
            {
                var l = (Label)rows.GetChild(k - 1);
                bool have = k <= r, next = k == r + 1;
                int req = Skills.ReqLevel(sk, k);
                l.Text = $"{(have ? "◆" : "◇")}  Rank {k}  ·  level {req}\n      {Skills.RankLine(sk, k)}";
                l.AddThemeColorOverride("font_color", have ? Spent : next && a.Level >= req ? UiTheme.Ink : Locked);
            }
            bool can = Sim.CanLearn(a, s);
            _colLearn[s].Disabled = !can;
            _colLearn[s].Text = r >= max ? "MASTERED" : can ? $"LEARN RANK {r + 1}" : a.SkPts <= 0 ? "NO POINT" : $"AT LEVEL {Skills.ReqLevel(sk, r + 1)}";
        }
    }
}
