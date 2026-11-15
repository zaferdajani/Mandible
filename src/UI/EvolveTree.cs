using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The evolve tree (docs/progression.md): the one place the colony's organic matter is spent. It climbs through the
/// nest's earth from the queen's chamber, in the look of the approved target N1 (assets/ui/mutation): the queen's own
/// trunk (her levels: better brood), the generals' (their levels: stronger and new skills), and one branch for every
/// unit a mutation core has given her. A branch whose core she has not taken lies dark and says whose core it needs.
/// Bought plates glow violet-green, plates the balance can pay for burn amber. Every purchase is an order (Cmd), so it
/// replays in saves and stays in step in multiplayer.
public sealed partial class EvolveTree : PanelContainer
{
    const string Art = "res://assets/ui/mutation/";
    static readonly Color Violet = new(0.66f, 0.38f, 1f), Green = new(0.55f, 1f, 0.55f), Amber = new(1f, 0.66f, 0.22f);
    readonly GameSession _game;
    readonly Control _body = new();
    string _sig = "";

    static readonly Dictionary<string, Texture2D> _tex = new();
    static Texture2D Tex(string name)
    {
        if (_tex.TryGetValue(name, out var t)) return t;
        string path = Art + name;
        return _tex[name] = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    static string Icon(Mutation m) => m switch
    {
        Mutation.Winged => "ic_hornetwings.png", Mutation.Stinger => "ic_stinger.png", Mutation.Web => "ic_webthrowing.png",
        Mutation.Armoured => "ic_ironchitin.png", _ => "ic_royallarva.png",
    };

    // light added on top (glows, lit tunnels): additive, never a flat fill
    static CanvasItemMaterial Add => new() { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

    float S => Settings.Mobile ? 0.82f : 1f;     // the whole screen scales down on phones

    public EvolveTree(GameSession game)
    {
        _game = game;
        AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.05f, 0.03f, 0.02f, 1f), new Color(0.55f, 0.36f, 0.16f, 0.9f), 2, 12, 0));
        CustomMinimumSize = new Vector2(1120, 660) * S;
        ClipContents = true;
        AddChild(_body);
        Build();
    }

    /// Everything the screen shows, as one string: it is rebuilt only when this changes.
    string Signature()
    {
        var sim = _game.Sim; var c = sim.Col[Local.Me];
        var sb = new System.Text.StringBuilder();
        sb.Append((int)c.Organic).Append('|').Append(c.Mutations).Append('|').Append(c.Waiting).Append('|').Append(c.T(Upgrade.Queen)).Append(c.Researching ? 'r' : '-');
        foreach (byte e in c.Evo) sb.Append(e);
        foreach (int g in Generals()) sb.Append('|').Append(g).Append(':').Append(sim.Ants[g].Level);
        return sb.ToString();
    }

    List<int> Generals()
    {
        var sim = _game.Sim; var list = new List<int>();
        for (int i = 0; i < sim.High; i++)
            if (sim.Ants[i].Alive && sim.Ants[i].Team == Local.Me && Config.IsHero(sim.Ants[i].Type) && !sim.Ants[i].Summoned && sim.Ants[i].CharmP1 == 0) list.Add(i);
        return list;
    }

    public override void _Process(double delta)
    {
        string sig = Signature();
        if (sig == _sig) return;
        Build();
    }

    void Build()
    {
        _sig = Signature();
        foreach (var ch in _body.GetChildren()) { _body.RemoveChild(ch); ch.QueueFree(); }
        var size = CustomMinimumSize;
        _body.CustomMinimumSize = size;
        var sim = _game.Sim; var c = sim.Col[Local.Me];
        // the nest's earth: the queen's chamber glowing at the foot, the tunnels rising
        if (Tex("tree_bg.jpg") is { } bg)
            _body.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Texture = bg, Size = size, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore });
        // a soft shade over the upper soil so the names read, fading out toward the queen's chamber (no hard edge)
        var fade = new Gradient(); fade.SetColor(0, new Color(0.03f, 0.02f, 0.01f, 0.66f)); fade.SetColor(1, new Color(0.03f, 0.02f, 0.01f, 0f));
        _body.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Texture = new GradientTexture2D { Gradient = fade, FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 1), Width = 4, Height = 64 }, Size = new Vector2(size.X, size.Y * 0.8f), StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore });

        var title = UiTheme.Text("THE EVOLVE TREE", (int)(30 * S), UiTheme.Title, new Color(1f, 0.78f, 0.42f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.Position = new Vector2(0, 14 * S); title.Size = new Vector2(size.X, 40 * S);
        title.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        title.AddThemeConstantOverride("shadow_offset_y", 2);
        _body.AddChild(title);
        var sub = UiTheme.Text("Organic matter levels up the queen and her generals. Every unit a mutation core gives her grows a branch of its own.", (int)(13 * S), UiTheme.Body, new Color(0.9f, 0.8f, 0.66f));
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        sub.Position = new Vector2(0, 52 * S); sub.Size = new Vector2(size.X, 20 * S);
        _body.AddChild(sub);

        float colW = size.X / 6f;
        float bottom = size.Y - 128 * S, step = 150 * S;
        Vector2 At(int col, int row) => new(colW * (col + 0.5f), bottom - row * step);

        // ---- the queen: her levels are better brood
        Head(0, colW, "THE QUEEN", "better brood");
        {
            int tier = c.T(Upgrade.Queen);
            bool max = tier >= Config.MaxTier(Upgrade.Queen);
            var can = sim.CanResearch(Local.Me, Upgrade.Queen);
            float cost = Evolve.QueenCost(tier + 1);
            string state = max ? $"LEVEL {tier}  ·  THE HIGHEST"
                : c.Researching && c.ResearchKind == Upgrade.Queen ? $"LEVEL {tier}  ·  GROWING"
                : can == ResearchResult.Ok ? $"LEVEL {tier}  ·  NEXT: {cost:0} ORGANIC"
                : can == ResearchResult.NoOrganic ? $"LEVEL {tier}  ·  NEEDS {cost:0} ORGANIC"
                : can == ResearchResult.Busy ? $"LEVEL {tier}  ·  THE NEST IS BUSY"
                : $"LEVEL {tier}  ·  NOT YET";
            Plate(At(0, 0), "ic_swiftbrood.png", Config.QueenTitles[Mathf.Clamp(tier, 1, 5)].ToUpperInvariant(), state, tier > 1, !max && can == ResearchResult.Ok,
                $"The queen's level.\nEvery ant she lays from then on hatches with +{Evolve.BroodHp * 100:0}% health and +{Evolve.BroodBite * 100:0}% bite per level, and the nest holds {Evolve.BroodRoom} more.\nHer levels also open the heavier castes.",
                () => _game.TryResearch(Upgrade.Queen));
            Tunnel(At(0, 0).X, size.Y - 42 * S, bottom + 76 * S, tier > 1);
        }

        // ---- the generals: a level is a skill point
        Head(1, colW, "THE GENERALS", "stronger skills, new skills");
        {
            var gens = Generals();
            if (gens.Count == 0)
                Plate(At(1, 0), "ic_royallarva.png", "NO GENERAL YET", sim.Has(Local.Me, Mutation.RoyalLarva) || sim.Mission == null ? "LAY ONE FIRST" : "NEEDS A MUTATED QUEEN'S CORE", false, false,
                    "A general's levels are bought here with organic matter. Each level is a skill point: stronger skills, then new ones.", null);
            for (int k = 0; k < gens.Count && k < 3; k++)
            {
                int id = gens[k];
                var a = sim.Ants[id];
                var can = sim.CanLevelGeneral(id);
                float cost = Evolve.GeneralCost(a.Level + 1);
                string name = sim.Mission != null && sim.Mission.Tracked.TryGetValue(id, out var tv) && tv.serial == a.Serial && tv.vet.Name != "" ? tv.vet.Name : Config.Of(a.Type).Name;
                string state = can == EvolveResult.Maxed ? $"LEVEL {a.Level}  ·  THE HIGHEST"
                    : can == EvolveResult.Ok ? $"LEVEL {a.Level}  ·  NEXT: {cost:0} ORGANIC"
                    : can == EvolveResult.NoOrganic ? $"LEVEL {a.Level}  ·  NEEDS {cost:0} ORGANIC" : $"LEVEL {a.Level}";
                Plate(At(1, k), "ic_royallarva.png", name.ToUpperInvariant(), state, a.Level > 1, can == EvolveResult.Ok,
                    $"{Config.Of(a.Type).Name}, level {a.Level}.\nThe next level makes it stronger and gives one skill point for its skill tree (O).",
                    () => _game.Issue(Cmd.Units(CmdKind.Evolve, Local.Me, new List<int> { id }, sim)));
                if (k > 0) Tunnel(At(1, k).X, At(1, k - 1).Y - 48 * S, At(1, k).Y + 76 * S, true);
            }
            Tunnel(At(1, 0).X, size.Y - 42 * S, bottom + 76 * S, gens.Count > 0);
        }

        // ---- one branch per mutation unit
        int col = 2;
        foreach (var mu in new[] { Mutation.Winged, Mutation.Stinger, Mutation.Web, Mutation.Armoured })
        {
            bool owned = (c.Mutations & (int)mu) != 0;
            Head(col, colW, Mutations.Name(mu).ToUpperInvariant(), Mutations.Source(mu));
            if (!owned)
            {
                bool waiting = (c.Waiting & (int)mu) != 0;
                var needs = Mutations.Needs(mu);
                string state = waiting ? "ITS CORE WAITS IN THE NEST" : needs != Mutation.None && (c.Mutations & (int)needs) == 0 ? $"AFTER {Mutations.Name(needs).ToUpperInvariant()}" : $"NEEDS {Mutations.Source(mu).ToUpperInvariant()}";
                Plate(At(col, 0), Icon(mu), Mutations.Name(mu).ToUpperInvariant(), state, false, false, $"{Mutations.Text(mu)}\nCut the core from a mutated creature and carry it to the queen.", null);
            }
            else
            {
                int row = 0;
                foreach (var d in Evolve.Nodes)
                {
                    if (d.Branch != mu) continue;
                    int rank = c.Evo[(int)d.Node];
                    var can = sim.CanEvolve(Local.Me, d.Node);
                    float cost = Evolve.Cost(d.Node, rank + 1);
                    string state = can == EvolveResult.Maxed ? $"RANK {rank} OF {d.Ranks}"
                        : can == EvolveResult.Ok ? $"RANK {rank} OF {d.Ranks}  ·  {cost:0} ORGANIC"
                        : $"RANK {rank} OF {d.Ranks}  ·  NEEDS {cost:0}";
                    var node = d.Node;
                    Plate(At(col, row), Icon(mu), d.Name.ToUpperInvariant(), state, rank > 0, can == EvolveResult.Ok, $"{d.Name}\n{d.Text}",
                        () => _game.Issue(new Cmd { Kind = CmdKind.Evolve, Team = (byte)Local.Me, Arg = (byte)node }));
                    if (row > 0) Tunnel(At(col, row).X, At(col, row - 1).Y - 48 * S, At(col, row).Y + 76 * S, true);
                    row++;
                }
                Tunnel(At(col, 0).X, size.Y - 42 * S, bottom + 76 * S, true);
            }
            col++;
        }

        var legend = UiTheme.Text("◆ BOUGHT  violet      ◆ CAN BUY  amber      ◆ LOCKED  dark", (int)(12 * S), UiTheme.Bold, new Color(0.86f, 0.76f, 0.62f));
        legend.Position = new Vector2(18 * S, size.Y - 30 * S);
        _body.AddChild(legend);
        var balance = UiTheme.Text($"ORGANIC MATTER  {(int)c.Organic}", (int)(16 * S), UiTheme.Title, new Color(1f, 0.8f, 0.45f));
        balance.HorizontalAlignment = HorizontalAlignment.Right;
        balance.Position = new Vector2(size.X * 0.45f, size.Y - 34 * S); balance.Size = new Vector2(size.X * 0.55f - 18 * S, 24 * S);
        _body.AddChild(balance);
    }

    void Head(int col, float colW, string name, string line)
    {
        float cx = colW * (col + 0.5f);
        var head = UiTheme.Text(name, (int)(15 * S), UiTheme.Title, new Color(1f, 0.76f, 0.4f));
        head.HorizontalAlignment = HorizontalAlignment.Center;
        head.Position = new Vector2(cx - colW / 2, 84 * S); head.Size = new Vector2(colW, 22 * S);
        head.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        _body.AddChild(head);
        var sub = UiTheme.Text(line, (int)(11 * S), UiTheme.Body, new Color(0.82f, 0.72f, 0.58f));
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        sub.Position = new Vector2(cx - colW / 2, 104 * S); sub.Size = new Vector2(colW, 16 * S);
        _body.AddChild(sub);
    }

    // a tunnel lit violet-green from below once the plate above it is reached
    void Tunnel(float x, float from, float to, bool lit)
    {
        if (!lit) return;
        var glow = new Line2D { Points = new[] { new Vector2(x, from), new Vector2(x + 3 * S, (from + to) / 2), new Vector2(x, to) }, Width = 9 * S, DefaultColor = Violet with { A = 0.35f }, Material = Add, JointMode = Line2D.LineJointMode.Round };
        var core = new Line2D { Points = glow.Points, Width = 2.5f * S, DefaultColor = Green with { A = 0.55f }, Material = Add };
        _body.AddChild(glow); _body.AddChild(core);
    }

    void Plate(Vector2 c, string icon, string name, string state, bool bought, bool afford, string tip, System.Action press)
    {
        var w = new Vector2(104, 92) * S;
        Color? halo = afford ? Amber : bought ? Violet : null;
        if (halo is { } hc && Tex("plate.png") is { } pt)
            _body.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Texture = pt, Position = c - w * 0.62f, Size = w * 1.24f, StretchMode = TextureRect.StretchModeEnum.Scale, Modulate = hc * new Color(1.6f, 1.6f, 1.6f, 0.9f), Material = Add, MouseFilter = MouseFilterEnum.Ignore });
        var b = new TextureButton
        {
            TextureNormal = Tex("plate.png"), IgnoreTextureSize = true, StretchMode = TextureButton.StretchModeEnum.Scale,
            Position = c - w / 2, Size = w, CustomMinimumSize = w,
            Disabled = !afford || press == null, FocusMode = FocusModeEnum.None, TooltipText = tip,
            MouseDefaultCursorShape = afford ? CursorShape.PointingHand : CursorShape.Arrow,
            Modulate = bought || afford ? Colors.White : new Color(0.55f, 0.5f, 0.46f),
            PivotOffset = w / 2,
        };
        _body.AddChild(b);
        if (Tex(icon) is { } ic)
            b.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Texture = ic, Position = w * 0.08f, Size = w * 0.84f, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore,
                Modulate = bought ? Colors.White : afford ? new Color(1, 0.95f, 0.88f) : new Color(0.42f, 0.38f, 0.34f, 0.85f) });
        b.MouseEntered += () => { if (afford) b.Scale = Vector2.One * 1.06f; };
        b.MouseExited += () => b.Scale = Vector2.One;
        if (press != null) b.Pressed += () => { press(); _game.Audio.Ui("research"); };

        var plaque = new PanelContainer { Position = new Vector2(c.X - 86 * S, c.Y + w.Y * 0.5f - 2 * S), CustomMinimumSize = new Vector2(172 * S, 0), MouseFilter = MouseFilterEnum.Ignore };
        plaque.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.09f, 0.05f, 0.03f, 0.88f), afford ? Amber : bought ? Violet.Lerp(Green, 0.3f) : new Color(0.36f, 0.26f, 0.18f), 1, 7, 3));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", -2);
        var nm = UiTheme.Text(name, (int)(11 * S), UiTheme.Title, bought || afford ? new Color(1f, 0.9f, 0.74f) : new Color(0.62f, 0.56f, 0.5f));
        nm.HorizontalAlignment = HorizontalAlignment.Center;
        var st = UiTheme.Text(state, (int)(9 * S), UiTheme.Bold, afford ? Amber : bought ? Green.Lerp(Violet, 0.3f) : new Color(0.6f, 0.53f, 0.46f));
        st.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(nm); v.AddChild(st);
        plaque.AddChild(v);
        _body.AddChild(plaque);
    }
}

/// On the objective card: what the queen's cores have given her, the organic balance with the way into the evolve
/// tree, and the royal larva's gauge.
public partial class EvolveStrip : VBoxContainer
{
    public GameSession Game;
    Label _units, _gaugeText;
    ProgressBar _gauge;
    Button _open;

    public static Texture2D Icon => _icon ??= ResourceLoader.Exists("res://assets/ui/mutagen.png") ? GD.Load<Texture2D>("res://assets/ui/mutagen.png") : null;
    static Texture2D _icon;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 4);
        AddChild(new HSeparator());
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        if (Icon != null) row.AddChild(new TextureRect { Texture = Icon, CustomMinimumSize = new Vector2(30, 30), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        _units = UiTheme.Text("", 14, UiTheme.Bold, new Color(0.86f, 0.78f, 1f));
        _units.VerticalAlignment = VerticalAlignment.Center;
        _units.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _units.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(_units);
        AddChild(row);
        _open = new Button { CustomMinimumSize = new Vector2(0, Settings.Mobile ? 52 : 38) };
        _open.AddThemeColorOverride("font_color", new Color(1f, 0.86f, 0.6f));
        _open.Pressed += () => Game.Hud.ToggleEvolveTree();
        AddChild(_open);
        _gaugeText = UiTheme.Text("", 13, UiTheme.Bold, UiTheme.Gold);
        AddChild(_gaugeText);
        _gauge = new ProgressBar { CustomMinimumSize = new Vector2(0, 12), ShowPercentage = false, MaxValue = 1 };
        _gauge.AddThemeStyleboxOverride("background", UiTheme.Box(new Color(0.12f, 0.08f, 0.05f, 0.9f), new Color(UiTheme.Bronze, 0.6f), 1, 3, 0));
        _gauge.AddThemeStyleboxOverride("fill", UiTheme.Box(new Color(0.78f, 0.62f, 0.95f), new Color(0.95f, 0.85f, 0.55f), 0, 3, 0));
        AddChild(_gauge);
    }

    public override void _Process(double delta)
    {
        var sim = Game.Sim;
        var m = sim.Mission;
        Visible = m != null && m.Chapter >= Reveal.Mutagen;
        if (!Visible) return;
        var c = sim.Col[0];
        var names = new List<string>();
        foreach (var mu in Mutations.All) if ((c.Mutations & (int)mu) != 0) names.Add(Mutations.Name(mu));
        _units.Text = names.Count == 0 ? "THE QUEEN HAS TAKEN NO CORE YET" : "THE QUEEN LAYS:  " + string.Join("  ·  ", names).ToUpperInvariant();
        _units.TooltipText = "A mutation core cut from a mutated creature and carried to the queen: one core, and she lays a new kind of ant.";
        _open.Text = $"EVOLVE TREE (P)  ·  {(int)c.Organic} ORGANIC MATTER";
        _open.TooltipText = "Organic matter levels up the queen and her generals, and grows each mutation unit's branch.";
        bool larva = c.LarvaStage > 0;
        _gauge.Visible = _gaugeText.Visible = larva;
        if (!larva) return;
        string hero = Config.Of(c.LarvaHero).Name;
        if (c.LarvaStage == 1) { _gaugeText.Text = $"ROYAL LARVA ({hero})  ·  waiting for its nursery"; _gauge.Value = 0; }
        else if (c.LarvaStage == 2) { _gaugeText.Text = $"ROYAL LARVA ({hero})  ·  fed {(int)c.LarvaFed} / {(int)c.LarvaNeed} organic  ·  health {c.LarvaHp * 100:0}%"; _gauge.Value = c.LarvaFed / Mathf.Max(1, c.LarvaNeed); }
        else { _gaugeText.Text = $"COCOON  ·  bursts in {Mathf.Ceil(c.CocoonT):0}s  ·  health {c.LarvaHp * 100:0}%"; _gauge.Value = 1 - c.CocoonT / Mathf.Max(1, c.CocoonTime); }
    }
}
