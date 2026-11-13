using System;
using Godot;

namespace Mandible;

/// The queen's mutation trees (Mutations.Trees) on the result card: every tree laid out at once, rooted at the bottom
/// and climbing upward, so the player plans ahead and chooses what the queen becomes. Taken rungs glow violet-green,
/// open rungs are framed in gold and take one core, locked rungs say what opens them. Mutation belongs to the queen;
/// the colony changes with her (and the core's strength becomes colony power).
public sealed partial class MutationTree : PanelContainer
{
    static readonly Color Violet = new(0.62f, 0.42f, 1f), Green = new(0.6f, 1f, 0.7f);
    readonly ColonyRoster _r;
    readonly Func<Mutation, bool> _take;
    readonly Mutation _trait;
    readonly VBoxContainer _body = new();
    public event Action Changed;

    public MutationTree(ColonyRoster r, Func<Mutation, bool> take, Mutation trait)
    {
        _r = r; _take = take; _trait = trait;
        AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.06f, 0.04f, 0.07f, 0.94f), new Color(0.45f, 0.3f, 0.7f, 0.8f), 1, 10, 16));
        _body.AddThemeConstantOverride("separation", 10);
        AddChild(_body);
        Build();
    }

    void Build()
    {
        foreach (var c in _body.GetChildren()) { _body.RemoveChild(c); c.QueueFree(); }
        var head = UiTheme.Text("THE QUEEN'S MUTATIONS", 20, UiTheme.Title, UiTheme.Gold);
        head.HorizontalAlignment = HorizontalAlignment.Center;
        _body.AddChild(head);
        int take = Mutations.CoreTake(_r.Mutagen, _r.Cores);
        string power = _r.Mutagen > 0 ? $"the next core gives the colony +{take} power" : "no core in the reserve: bring one home to mutate";
        var sub = UiTheme.Text($"She consumes a core, and her whole colony changes with her. Climb each tree from its root.\nMUTAGEN {_r.Mutagen}   ·   COLONY POWER {_r.Power}   ·   {power}", 13, UiTheme.Body, UiTheme.Dim);
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        _body.AddChild(sub);

        var cols = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        cols.AddThemeConstantOverride("separation", Settings.Mobile ? 10 : 18);
        _body.AddChild(cols);
        foreach (var t in Mutations.Trees) cols.AddChild(Column(t));
    }

    Control Column(Mutations.Tree t)
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 0);
        var name = UiTheme.Text(t.Name.ToUpperInvariant(), 15, UiTheme.Title, UiTheme.Gold);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(name);
        var line = UiTheme.Text(t.Line, 12, UiTheme.Body, UiTheme.Dim);
        line.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(line);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        // the tree grows upward: its last rung on top, its root at the bottom
        for (int k = t.Rungs.Length - 1; k >= 0; k--)
        {
            col.AddChild(Rung(t.Rungs[k]));
            if (k > 0) col.AddChild(Stem((_r.Mutations & (int)t.Rungs[k - 1]) != 0));
        }
        return col;
    }

    // the stem between two rungs: lit once the rung below is taken
    static Control Stem(bool lit)
    {
        var box = new CenterContainer { CustomMinimumSize = new Vector2(0, 16) };
        box.AddChild(new ColorRect { CustomMinimumSize = new Vector2(lit ? 3 : 2, 16), Color = lit ? Violet.Lerp(Green, 0.4f) : new Color(0.35f, 0.3f, 0.28f, 0.7f), MouseFilter = MouseFilterEnum.Ignore });
        return box;
    }

    Control Rung(Mutation m)
    {
        bool owned = (_r.Mutations & (int)m) != 0;
        bool open = Mutations.CanTake(m, _r.Mutations, _r.WebRank);
        bool afford = open && _r.Mutagen > 0;
        string state;
        Color bg, border, ink;
        int bw = 1;
        if (m == Mutation.WebThrowing && owned)
        {
            state = _r.WebRank >= 3 ? "RANK 3 OF 3" : afford ? $"RANK {_r.WebRank}  ·  DEEPEN: 1 CORE" : $"RANK {_r.WebRank} OF 3";
            bg = new Color(0.16f, 0.09f, 0.24f); border = afford ? UiTheme.Gold : Violet.Lerp(Green, 0.35f); ink = UiTheme.Ink; bw = afford ? 2 : 1;
        }
        else if (owned) { state = "TAKEN"; bg = new Color(0.16f, 0.09f, 0.24f); border = Violet.Lerp(Green, 0.35f); ink = UiTheme.Ink; }
        else if (afford) { state = "MUTATE  ·  1 CORE"; bg = new Color(0.12f, 0.08f, 0.05f); border = UiTheme.Gold; ink = UiTheme.Ink; bw = 2; }
        else if (open) { state = "OPEN  ·  NEEDS A CORE"; bg = new Color(0.1f, 0.075f, 0.055f); border = UiTheme.Bronze; ink = UiTheme.Ink; }
        else if (Mutations.Story(m)) { state = "THE STORY GIVES IT"; bg = new Color(0.07f, 0.055f, 0.045f); border = new Color(0.3f, 0.26f, 0.22f); ink = UiTheme.Dim; }
        else if (m == Mutation.WebThrowing) { state = "EARNED FROM THE SILK SPITTER"; bg = new Color(0.07f, 0.055f, 0.045f); border = new Color(0.3f, 0.26f, 0.22f); ink = UiTheme.Dim; }
        else { state = $"AFTER {Mutations.Name(Mutations.Below(m)).ToUpperInvariant()}"; bg = new Color(0.07f, 0.055f, 0.045f); border = new Color(0.3f, 0.26f, 0.22f); ink = UiTheme.Dim; }
        string mark = m == _trait && open && !owned ? "\n" + "in this queen's blood" : "";
        var b = new Button
        {
            Text = $"{Mutations.Name(m)}\n{state}{mark}",
            TooltipText = $"{Mutations.Text(m)}\n({Mutations.Source(m)})",
            CustomMinimumSize = new Vector2(Settings.Mobile ? 168 : 196, Settings.Mobile ? 74 : 62),
            Disabled = !afford,
            FocusMode = FocusModeEnum.None,
        };
        var box = UiTheme.Box(bg, border, bw, 8, 8);
        foreach (var s in new[] { "normal", "disabled", "focus" }) b.AddThemeStyleboxOverride(s, box);
        b.AddThemeStyleboxOverride("hover", UiTheme.Box(bg.Lightened(0.08f), border.Lightened(0.15f), bw, 8, 8));
        b.AddThemeStyleboxOverride("pressed", UiTheme.Box(bg.Lightened(0.15f), Green, 2, 8, 8));
        foreach (var c in new[] { "font_color", "font_disabled_color", "font_hover_color", "font_pressed_color" }) b.AddThemeColorOverride(c, ink);
        b.AddThemeFontOverride("font", UiTheme.Bold);
        b.AddThemeFontSizeOverride("font_size", 13);
        b.Pressed += () =>
        {
            if (!_take(m)) return;
            Build();
            Changed?.Invoke();
        };
        return b;
    }
}
