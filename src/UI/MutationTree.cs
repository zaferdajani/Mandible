using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The queen's mutation trees (Mutations.Trees), built to the owner's approved target N1
/// (docs/art-targets/n1-mutation-tree.jpg): the trees climb as tunnels through the nest's earth from the queen's
/// chamber, each skill a hexagonal chitin plate holding its emblem, its name beneath. Taken plates glow violet-green and
/// light the tunnel above them, open plates burn amber and take one core, locked plates lie dark and say what opens
/// them. Art: assets/ui/mutation (generated from N1). Mutation belongs to the queen; her colony changes with her.
public sealed partial class MutationTree : PanelContainer
{
    const string Art = "res://assets/ui/mutation/";
    static readonly Color Violet = new(0.66f, 0.38f, 1f), Green = new(0.55f, 1f, 0.55f), Amber = new(1f, 0.66f, 0.22f);
    readonly ColonyRoster _r;
    readonly Func<Mutation, bool> _take;
    readonly Mutation _trait;
    readonly Control _body = new();
    public event Action Changed;

    static readonly Dictionary<string, Texture2D> _tex = new();
    static Texture2D Tex(string name)
    {
        if (_tex.TryGetValue(name, out var t)) return t;
        string path = Art + name;
        return _tex[name] = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    static string Icon(Mutation m) => m switch
    {
        Mutation.Stinger => "ic_stinger.png", Mutation.FireBlood => "ic_fireblood.png", Mutation.AcidBlood => "ic_acidblood.png",
        Mutation.IronChitin => "ic_ironchitin.png", Mutation.Regrowth => "ic_regrowth.png", Mutation.HornetWings => "ic_hornetwings.png",
        Mutation.RoyalLarva => "ic_royallarva.png", Mutation.SwiftBrood => "ic_swiftbrood.png", Mutation.HungryGut => "ic_hungrygut.png",
        _ => "ic_webthrowing.png",
    };

    // light added on top (glows, lit tunnels): additive, never a flat fill
    static CanvasItemMaterial Add => new() { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

    float S => Settings.Mobile ? 0.82f : 1f;     // the whole screen scales down on phones

    public MutationTree(ColonyRoster r, Func<Mutation, bool> take, Mutation trait)
    {
        _r = r; _take = take; _trait = trait;
        AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.05f, 0.03f, 0.02f, 1f), new Color(0.55f, 0.36f, 0.16f, 0.9f), 2, 12, 0));
        CustomMinimumSize = new Vector2(1060, 660) * S;
        ClipContents = true;
        AddChild(_body);
        Build();
    }

    void Build()
    {
        foreach (var c in _body.GetChildren()) { _body.RemoveChild(c); c.QueueFree(); }
        var size = CustomMinimumSize;
        _body.CustomMinimumSize = size;
        // the nest's earth: the queen's chamber glowing at the foot, four tunnels rising (generated from N1)
        if (Tex("tree_bg.jpg") is { } bg)
            _body.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Texture = bg, Size = size, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore });
        // a soft shade over the upper soil so the names read, fading out toward the queen's chamber (no hard edge)
        var fade = new Gradient(); fade.SetColor(0, new Color(0.03f, 0.02f, 0.01f, 0.6f)); fade.SetColor(1, new Color(0.03f, 0.02f, 0.01f, 0f));
        _body.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Texture = new GradientTexture2D { Gradient = fade, FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 1), Width = 4, Height = 64 }, Size = new Vector2(size.X, size.Y * 0.75f), StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore });

        var title = UiTheme.Text("THE QUEEN'S MUTATIONS", (int)(30 * S), UiTheme.Title, new Color(1f, 0.78f, 0.42f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.Position = new Vector2(0, 14 * S); title.Size = new Vector2(size.X, 40 * S);
        title.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        title.AddThemeConstantOverride("shadow_offset_y", 2);
        _body.AddChild(title);
        var sub = UiTheme.Text("She consumes a core, and her whole colony changes with her. Each tree climbs from its root.", (int)(13 * S), UiTheme.Body, new Color(0.9f, 0.8f, 0.66f));
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        sub.Position = new Vector2(0, 52 * S); sub.Size = new Vector2(size.X, 20 * S);
        _body.AddChild(sub);

        int n = Mutations.Trees.Length;
        float colW = size.X / n;
        for (int k = 0; k < n; k++) Column(Mutations.Trees[k], colW * (k + 0.5f), size);

        // the legend and the points, bottom corners, as in N1
        var legend = UiTheme.Text("◆ TAKEN  violet      ◆ OPEN  amber      ◆ LOCKED  dark", (int)(12 * S), UiTheme.Bold, new Color(0.86f, 0.76f, 0.62f));
        legend.Position = new Vector2(18 * S, size.Y - 30 * S);
        _body.AddChild(legend);
        int take = Mutations.CoreTake(_r.Mutagen, _r.Cores);
        var points = UiTheme.Text($"CORES  {_r.Mutagen}      COLONY POWER  {_r.Power}" + (_r.Mutagen > 0 ? $"  (next core +{take})" : ""), (int)(14 * S), UiTheme.Title, new Color(1f, 0.8f, 0.45f));
        points.HorizontalAlignment = HorizontalAlignment.Right;
        points.Position = new Vector2(size.X * 0.45f, size.Y - 32 * S); points.Size = new Vector2(size.X * 0.55f - 18 * S, 24 * S);
        _body.AddChild(points);
    }

    void Column(Mutations.Tree t, float cx, Vector2 size)
    {
        var head = UiTheme.Text(t.Name.ToUpperInvariant(), (int)(17 * S), UiTheme.Title, new Color(1f, 0.76f, 0.4f));
        head.HorizontalAlignment = HorizontalAlignment.Center;
        head.Position = new Vector2(cx - 120 * S, 84 * S); head.Size = new Vector2(240 * S, 24 * S);
        head.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        _body.AddChild(head);
        var line = UiTheme.Text(t.Line, (int)(11 * S), UiTheme.Body, new Color(0.82f, 0.72f, 0.58f));
        line.HorizontalAlignment = HorizontalAlignment.Center;
        line.Position = new Vector2(cx - 120 * S, 106 * S); line.Size = new Vector2(240 * S, 16 * S);
        _body.AddChild(line);

        // rungs from the root (bottom) up; the last rung sits nearest the tree's name
        float bottom = size.Y - 128 * S, step = 158 * S;
        for (int k = 0; k < t.Rungs.Length; k++)
        {
            float cy = bottom - k * step;
            if (k > 0) Tunnel(cx, cy + step - 48 * S, cy + 82 * S, (_r.Mutations & (int)t.Rungs[k - 1]) != 0);
            Plate(t.Rungs[k], new Vector2(cx, cy));
        }
        // the root draws light from the queen's chamber when taken
        Tunnel(cx, size.Y - 42 * S, bottom + 82 * S, (_r.Mutations & (int)t.Rungs[0]) != 0);
    }

    // a tunnel lit violet-green from below once the rung beneath it is taken
    void Tunnel(float x, float from, float to, bool lit)
    {
        if (!lit) return;
        var glow = new Line2D { Points = new[] { new Vector2(x, from), new Vector2(x + 3 * S, (from + to) / 2), new Vector2(x, to) }, Width = 9 * S, DefaultColor = Violet with { A = 0.35f }, Material = Add, JointMode = Line2D.LineJointMode.Round };
        var core = new Line2D { Points = glow.Points, Width = 2.5f * S, DefaultColor = Green with { A = 0.55f }, Material = Add };
        _body.AddChild(glow); _body.AddChild(core);
    }

    void Plate(Mutation m, Vector2 c)
    {
        bool owned = (_r.Mutations & (int)m) != 0;
        bool open = Mutations.CanTake(m, _r.Mutations, _r.WebRank);
        bool afford = open && _r.Mutagen > 0;
        bool web = m == Mutation.WebThrowing && owned;
        var w = new Vector2(112, 98) * S;

        // the glow behind the plate: violet-green for taken, amber for open
        Color? halo = owned && !afford ? Violet : afford ? Amber : open ? Amber with { A = 0.35f } : null;
        if (halo is { } hc && Tex("plate.png") is { } pt)
            _body.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Texture = pt, Position = c - w * 0.62f, Size = w * 1.24f, StretchMode = TextureRect.StretchModeEnum.Scale, Modulate = hc * new Color(1.6f, 1.6f, 1.6f, 0.9f), Material = Add, MouseFilter = MouseFilterEnum.Ignore });

        var b = new TextureButton
        {
            TextureNormal = Tex("plate.png"), IgnoreTextureSize = true, StretchMode = TextureButton.StretchModeEnum.Scale,
            Position = c - w / 2, Size = w, CustomMinimumSize = w,
            Disabled = !afford, FocusMode = FocusModeEnum.None,
            TooltipText = $"{Mutations.Name(m)}\n{Mutations.Text(m)}\n({Mutations.Source(m)})",
            MouseDefaultCursorShape = afford ? CursorShape.PointingHand : CursorShape.Arrow,
            Modulate = owned || open ? Colors.White : new Color(0.55f, 0.5f, 0.46f),
        };
        _body.AddChild(b);
        if (Tex(Icon(m)) is { } ic)
            b.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Texture = ic, Position = w * 0.08f, Size = w * 0.84f, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore,
                Modulate = owned ? Colors.White : open ? new Color(1, 0.95f, 0.88f) : new Color(0.42f, 0.38f, 0.34f, 0.85f) });
        b.MouseEntered += () => { if (afford) b.Scale = Vector2.One * 1.06f; };
        b.MouseExited += () => b.Scale = Vector2.One;
        b.PivotOffset = w / 2;
        b.Pressed += () =>
        {
            if (!_take(m)) return;
            Build();
            Changed?.Invoke();
        };

        // the name plaque beneath, and the plate's state
        string state = web ? (_r.WebRank >= 3 ? "RANK 3 OF 3" : afford ? $"RANK {_r.WebRank}  ·  DEEPEN: 1 CORE" : $"RANK {_r.WebRank} OF 3")
            : owned ? "TAKEN"
            : afford ? "MUTATE  ·  1 CORE"
            : open ? "OPEN  ·  NEEDS A CORE"
            : Mutations.Story(m) ? "THE STORY GIVES IT"
            : m == Mutation.WebThrowing ? "FROM THE SILK SPITTER"
            : $"AFTER {Mutations.Name(Mutations.Below(m)).ToUpperInvariant()}";
        if (m == _trait && afford) state += "  ·  IN THIS QUEEN'S BLOOD";
        var plaque = new PanelContainer { Position = new Vector2(c.X - 92 * S, c.Y + w.Y * 0.5f - 2 * S), CustomMinimumSize = new Vector2(184 * S, 0), MouseFilter = MouseFilterEnum.Ignore };
        plaque.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.09f, 0.05f, 0.03f, 0.88f), owned ? Violet.Lerp(Green, 0.3f) : afford ? Amber : new Color(0.36f, 0.26f, 0.18f), 1, 7, 3));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", -2);
        var nm = UiTheme.Text(Mutations.Name(m).ToUpperInvariant(), (int)(12 * S), UiTheme.Title, owned || open ? new Color(1f, 0.9f, 0.74f) : new Color(0.62f, 0.56f, 0.5f));
        nm.HorizontalAlignment = HorizontalAlignment.Center;
        var st = UiTheme.Text(state, (int)(9 * S), UiTheme.Bold, owned ? Green.Lerp(Violet, 0.3f) : afford ? Amber : new Color(0.6f, 0.53f, 0.46f));
        st.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(nm); v.AddChild(st);
        plaque.AddChild(v);
        _body.AddChild(plaque);
    }
}
