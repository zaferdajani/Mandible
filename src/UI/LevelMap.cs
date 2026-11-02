using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The level map: a sunny stretch of yard with an ant trail winding through four seasons, one medallion per
/// level with its stars, and a card for the chosen level. Composed for landscape on every device.
public partial class LevelMap : Control
{
    public Main App;
    public Action Back;
    int _selected;
    float _t;
    Control _card, _look;
    Label _total, _nextLabel;
    ProgressBar _nextBar;

    // node centres as fractions of the map area (left of the card), snaking through the seasons
    static readonly Vector2[] Spots =
    {
        new(0.07f, 0.74f), new(0.15f, 0.44f), new(0.24f, 0.70f),
        new(0.33f, 0.36f), new(0.41f, 0.64f), new(0.49f, 0.33f),
        new(0.58f, 0.63f), new(0.66f, 0.32f), new(0.74f, 0.62f),
        new(0.81f, 0.36f), new(0.87f, 0.68f), new(0.92f, 0.38f),
    };
    static readonly Color[] SeasonTint = { new(0.55f, 0.7f, 0.3f, 0.10f), new(1f, 0.78f, 0.3f, 0.08f), new(0.9f, 0.45f, 0.18f, 0.11f), new(0.75f, 0.35f, 0.25f, 0.12f) };

    const float CardW = 560, TopH = 118, BottomH = 96;

    public override void _Ready()
    {
        UiTheme.Place(this, 0, 0, 1, 1, 0, 0, 0, 0);
        MouseFilter = MouseFilterEnum.Stop;
        int next = CampaignProgress.Next;
        _selected = next > 0 ? Levels.NumberOf(next) : Levels.Count;
        BuildChrome();
        BuildCard();
    }

    Rect2 MapRect => new(new Vector2(36, TopH + 10), new Vector2(Size.X - CardW - 72, Size.Y - TopH - BottomH - 20));
    Vector2 Spot(int number) { var r = MapRect; var f = Spots[number - 1]; return r.Position + new Vector2(f.X * r.Size.X, f.Y * r.Size.Y); }
    float NodeR => Mathf.Clamp(MapRect.Size.X / 30, 26, 40);

    // ------------------------------------------------------------------ chrome: title, totals, buttons
    void BuildChrome()
    {
        var title = UiTheme.Text("THE YARD", 40, UiTheme.Title, UiTheme.Gold);
        UiTheme.Place(title, 0, 0, 0, 0, 120, 22, 700, 78);
        AddChild(title);
        var sub = UiTheme.Text($"War Beneath the Grass  ·  {CampaignProgress.CompletedCount} of {Levels.Count} levels won", 16, null, UiTheme.Ink);
        UiTheme.Place(sub, 0, 0, 0, 0, 122, 72, 800, 100);
        AddChild(sub);

        _total = UiTheme.Text("", 30, UiTheme.Title, Emblem.Gold);
        _total.HorizontalAlignment = HorizontalAlignment.Right;
        UiTheme.Place(_total, 1, 0, 1, 0, -CardW - 330, 22, -CardW - 70, 66);
        AddChild(_total);
        _nextLabel = UiTheme.Text("", 14, UiTheme.Bold, UiTheme.Ink);
        _nextLabel.HorizontalAlignment = HorizontalAlignment.Right;
        UiTheme.Place(_nextLabel, 1, 0, 1, 0, -CardW - 620, 66, -CardW - 70, 88);
        AddChild(_nextLabel);
        _nextBar = new ProgressBar { ShowPercentage = false, MinValue = 0, MaxValue = 1, Step = 0.001 };
        UiTheme.Place(_nextBar, 1, 0, 1, 0, -CardW - 400, 92, -CardW - 70, 100);
        AddChild(_nextBar);
        RefreshTotals();

        var back = Button("Back", 170);
        UiTheme.Place(back, 0, 1, 0, 1, 36, -BottomH + 14, 206, -24);
        back.Pressed += () => { App.Audio.Ui("ui_click"); Back?.Invoke(); };
        AddChild(back);
        var look = Button("Colony banner", 230);
        UiTheme.Place(look, 0, 1, 0, 1, 222, -BottomH + 14, 452, -24);
        look.Pressed += () => { App.Audio.Ui("ui_click"); ToggleLook(); };
        AddChild(look);
        int next = CampaignProgress.Next;
        var cont = Button(next > 0 ? $"Continue  ·  Level {Levels.NumberOf(next)}" : "Every level won  ·  chase the stars", 360);
        cont.AddThemeColorOverride("font_color", Emblem.Gold);
        UiTheme.Place(cont, 1, 1, 1, 1, -CardW - 70 - 360, -BottomH + 14, -CardW - 70, -24);
        cont.Disabled = next == 0;
        cont.Pressed += () => { App.Audio.Ui("ui_click"); App.StartEpisode(next); };
        AddChild(cont);
    }

    Button Button(string text, float w)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(w, 56) };
        b.AddThemeFontOverride("font", UiTheme.Title);
        b.AddThemeFontSizeOverride("font_size", 18);
        b.MouseEntered += () => App.Audio.Ui("ui_hover");
        return b;
    }

    void RefreshTotals()
    {
        int stars = CampaignProgress.TotalStars;
        _total.Text = $"{stars} / {Levels.Count * 3}";
        var next = Levels.NextUnlock(stars);
        int prev = 0;
        foreach (var u in Levels.Unlocks) if (u.Stars <= stars) prev = u.Stars;
        _nextLabel.Text = next == null ? "Every unlock earned: the Eternal Mother" : $"Next unlock at {next.Stars} stars:  {next.Name}";
        _nextBar.Value = next == null ? 1 : (stars - prev) / (float)Mathf.Max(1, next.Stars - prev);
        QueueRedraw();
    }

    // ------------------------------------------------------------------ the card for the chosen level
    void BuildCard()
    {
        _card?.QueueFree();
        int id = Levels.IdAt(_selected);
        var info = Levels.Info(id);
        bool open = CampaignProgress.Unlocked(id), done = CampaignProgress.Completed(id);
        int bits = CampaignProgress.StarBits(id);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.09f, 0.065f, 0.04f, 0.94f), new Color(UiTheme.Bronze, 0.95f), 2, 10, 26));
        UiTheme.Place(panel, 1, 0, 1, 1, -CardW - 24, 24, -24, -24);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        panel.AddChild(v);
        v.AddChild(UiTheme.Text($"LEVEL {_selected}  ·  {Levels.Seasons[info.Season].ToUpperInvariant()}  ·  {Levels.SeasonThemes[info.Season].ToUpperInvariant()}", 14, UiTheme.Bold, UiTheme.Dim));
        var name = UiTheme.Text(info.Name, 36, UiTheme.Title, open ? UiTheme.Gold : UiTheme.Dim);
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        v.AddChild(name);
        v.AddChild(new Pill { Text = "NEW  ·  " + info.Novelty, Fill = open ? new Color(0.45f, 0.3f, 0.1f) : new Color(0.2f, 0.16f, 0.12f) });
        var blurb = UiTheme.Text(open ? info.Blurb : $"Win Level {_selected - 1} to open this level.", 17, null, UiTheme.Ink);
        blurb.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        v.AddChild(blurb);
        if (open && info.Id >= 1)
        {
            var intro = Levels.Info(id).Id >= 5 ? Levels.Intro(id) : CampaignEpisodes.Intro(id);
            if (intro.Length > 0)
            {
                var story = UiTheme.Text(intro[0], 15, null, UiTheme.Dim);
                story.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                v.AddChild(story);
            }
        }
        v.AddChild(new HSeparator());
        string foe = info.Rival switch
        {
            RivalKind.None => "No rival colony: the level is won by its own objective.",
            RivalKind.Young => "A young red colony. Win by killing its queen.",
            RivalKind.Grown => "A grown red colony. Win by killing its queen.",
            RivalKind.Awake => "An awake red colony that fights back. Win by killing its queen.",
            _ => "The red Empress, awake and raiding. Win by killing her.",
        };
        var rival = UiTheme.Text(foe, 15, UiTheme.Bold, info.Rival == RivalKind.None ? UiTheme.Ink : new Color(1f, 0.6f, 0.45f));
        rival.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        v.AddChild(rival);
        v.AddChild(UiTheme.Text("STARS", 14, UiTheme.Bold, UiTheme.Dim));
        var list = new StarList { Size_ = 14, FontSize = 17 };
        list.Set(new[] { ((bits & 1) != 0, Levels.StarText(id, 0)), ((bits & 2) != 0, Levels.StarText(id, 1)), ((bits & 4) != 0, Levels.StarText(id, 2)) });
        v.AddChild(list);
        float best = CampaignProgress.Best(id);
        if (best > 0) v.AddChild(UiTheme.Text($"Best time  {(int)best / 60}:{(int)best % 60:00}", 15, UiTheme.Bold, UiTheme.Ink));
        v.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        var play = new Button { Text = !open ? "Locked" : done ? "Replay level" : "Play level", Disabled = !open, CustomMinimumSize = new Vector2(0, 66) };
        play.AddThemeFontOverride("font", UiTheme.Title);
        play.AddThemeFontSizeOverride("font_size", 24);
        play.AddThemeColorOverride("font_color", Emblem.Gold);
        play.MouseEntered += () => App.Audio.Ui("ui_hover");
        play.Pressed += () => { App.Audio.Ui("ui_click"); App.StartEpisode(id); };
        v.AddChild(play);
        _card = panel;
        AddChild(panel);
    }

    /// The colony banner: colour and emblem, each opened by stars.
    public void ToggleLook()
    {
        if (_look != null) { _look.QueueFree(); _look = null; return; }
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.09f, 0.065f, 0.04f, 0.97f), Emblem.Gold, 2, 10, 22));
        UiTheme.Place(panel, 0, 1, 0, 1, 36, -BottomH - 344, 676, -BottomH - 20);
        var v = new VBoxContainer();
        panel.AddChild(v);
        v.AddChild(UiTheme.Text("COLONY BANNER", 20, UiTheme.Title, Emblem.Gold));
        v.AddChild(UiTheme.Text("Colour (your ants' crests, selection and minimap)", 14, UiTheme.Bold, UiTheme.Dim));
        var colours = new HBoxContainer();
        v.AddChild(colours);
        for (int i = 0; i < Levels.Colours.Length; i++)
        {
            int c = i;
            bool open = CampaignProgress.ColourOpen(c);
            var b = new Swatch { Colour = Levels.Colours[c], Label = open ? Levels.ColourNames[c] : Levels.ColourNames[c], Open = open, Chosen = CampaignProgress.Colour == c, Cost = Cost(u => u.Colour == c) };
            b.Pressed += () => { if (!open) { App.Audio.Ui("ui_error"); return; } App.Audio.Ui("ui_click"); CampaignProgress.Choose(c, CampaignProgress.Emblem); ToggleLook(); ToggleLook(); QueueRedraw(); };
            colours.AddChild(b);
        }
        v.AddChild(UiTheme.Text("Emblem", 14, UiTheme.Bold, UiTheme.Dim));
        var emblems = new HBoxContainer();
        v.AddChild(emblems);
        for (int i = 0; i < Levels.EmblemNames.Length; i++)
        {
            int e = i;
            bool open = CampaignProgress.EmblemOpen(e);
            var b = new Swatch { Colour = Levels.Colours[CampaignProgress.Colour], EmblemId = e, Label = Levels.EmblemNames[e], Open = open, Chosen = CampaignProgress.Emblem == e, Cost = Cost(u => u.Emblem == e) };
            b.Pressed += () => { if (!open) { App.Audio.Ui("ui_error"); return; } App.Audio.Ui("ui_click"); CampaignProgress.Choose(CampaignProgress.Colour, e); ToggleLook(); ToggleLook(); QueueRedraw(); };
            emblems.AddChild(b);
        }
        _look = panel;
        AddChild(panel);
    }

    static int Cost(Predicate<Levels.Unlock> which) { foreach (var u in Levels.Unlocks) if (which(u)) return u.Stars; return 0; }

    // ------------------------------------------------------------------ input and drawing
    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
            for (int n = 1; n <= Levels.Count; n++)
                if (mb.Position.DistanceTo(Spot(n)) < NodeR * 1.3f)
                {
                    App.Audio.Ui("ui_click");
                    if (_selected == n && CampaignProgress.Unlocked(Levels.IdAt(n)) && mb.DoubleClick) { App.StartEpisode(Levels.IdAt(n)); return; }
                    _selected = n; BuildCard(); QueueRedraw();
                    return;
                }
    }

    /// Selects a level as a click would (the capture test uses it).
    public void Select(int number) { _selected = Mathf.Clamp(number, 1, Levels.Count); BuildCard(); QueueRedraw(); }

    public override void _Process(double delta) { _t += (float)delta; QueueRedraw(); }

    public override void _Draw()
    {
        DrawYard();
        DrawTrail();
        for (int n = 1; n <= Levels.Count; n++) DrawNode(n);
        // the colony banner beside the title
        Emblem.Banner(this, new Vector2(70, 60), 38, CampaignProgress.Emblem, Levels.Colours[CampaignProgress.Colour]);
        // the stars total's own star
        var font = UiTheme.Title;
        float w = font.GetStringSize(_total.Text, HorizontalAlignment.Left, -1, 30).X;
        Emblem.Star(this, new Vector2(Size.X - CardW - 70 - w - 26, 46), 16, true, 0.6f);
    }

    void DrawYard()
    {
        // warm sand, lit from the top left
        var top = new Color(0.68f, 0.53f, 0.32f);
        var bottom = new Color(0.5f, 0.37f, 0.22f);
        DrawPolygon(new[] { Vector2.Zero, new Vector2(Size.X, 0), Size, new Vector2(0, Size.Y) }, new[] { top, top.Darkened(0.06f), bottom, bottom.Lightened(0.04f) });
        var rng = new Rng(20261102);
        var map = MapRect;
        // season washes behind each third of the trail
        for (int s = 0; s < 4; s++)
        {
            float x0 = map.Position.X + map.Size.X * (s == 0 ? -0.1f : (Spots[s * 3].X + Spots[s * 3 - 1].X) / 2);
            float x1 = s == 3 ? Size.X - CardW - 40 : map.Position.X + map.Size.X * (Spots[s * 3 + 3].X + Spots[s * 3 + 2].X) / 2;
            DrawRect(new Rect2(x0, TopH, x1 - x0, Size.Y - TopH - BottomH + 10), SeasonTint[s]);
            var label = $"{Levels.Seasons[s].ToUpperInvariant()}  ·  {Levels.SeasonThemes[s].ToUpperInvariant()}";
            var lw = UiTheme.Title.GetStringSize(label, HorizontalAlignment.Left, -1, 15).X;
            DrawString(UiTheme.Title, new Vector2((x0 + x1) / 2 - lw / 2 + 1, TopH + 31), label, HorizontalAlignment.Left, -1, 15, new Color(0, 0, 0, 0.45f));
            DrawString(UiTheme.Title, new Vector2((x0 + x1) / 2 - lw / 2, TopH + 30), label, HorizontalAlignment.Left, -1, 15, new Color(0.98f, 0.9f, 0.7f, 0.85f));
            if (s > 0) DrawLine(new Vector2(x0, TopH + 46), new Vector2(x0, Size.Y - BottomH), new Color(0.3f, 0.2f, 0.1f, 0.25f), 2);
        }
        // damp soil patches and pebbles
        for (int i = 0; i < 70; i++)
        {
            var p = new Vector2(rng.R(0, Size.X), rng.R(TopH, Size.Y));
            float r = rng.R(14, 60);
            DrawSetTransform(p, rng.R(0, 3), new Vector2(1, rng.R(0.35f, 0.7f)));
            DrawCircle(Vector2.Zero, r, new Color(0.38f, 0.26f, 0.14f, rng.R(0.06f, 0.16f)));
        }
        for (int i = 0; i < 160; i++)
        {
            var p = new Vector2(rng.R(0, Size.X), rng.R(TopH, Size.Y));
            float r = rng.R(2, 7);
            DrawSetTransform(p, rng.R(0, 3), new Vector2(1, rng.R(0.6f, 0.9f)));
            var tone = new Color(rng.R(0.55f, 0.75f), rng.R(0.45f, 0.58f), rng.R(0.33f, 0.42f));
            DrawCircle(new Vector2(r * 0.15f, r * 0.25f), r, new Color(0, 0, 0, 0.25f));
            DrawCircle(Vector2.Zero, r, tone);
            DrawCircle(new Vector2(-r * 0.3f, -r * 0.3f), r * 0.4f, new Color(1, 0.95f, 0.85f, 0.25f));
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        // grass tufts along the top and bottom edges, autumn leaves in the later seasons
        for (int i = 0; i < 120; i++)
        {
            bool topEdge = i % 2 == 0;
            var root = new Vector2(rng.R(0, Size.X), topEdge ? TopH + rng.R(-6, 12) : Size.Y - BottomH + rng.R(-10, 30));
            float season = Mathf.Clamp((root.X - map.Position.X) / map.Size.X, 0, 1);
            var green = new Color(0.42f, 0.5f, 0.17f).Lerp(new Color(0.62f, 0.5f, 0.2f), season);
            for (int b = 0; b < 4; b++)
            {
                float lean = rng.R(-0.5f, 0.5f), h = rng.R(18, 46) * (topEdge ? 0.8f : 1);
                var tip = root + new Vector2(lean * h, -h);
                DrawColoredPolygon(new[] { root + new Vector2(-2.5f, 0), tip, root + new Vector2(2.5f, 0) }, green.Darkened(rng.R(0, 0.25f)));
            }
        }
        for (int i = 0; i < 26; i++)
        {
            float f = rng.R(0.55f, 1.05f);
            var p = new Vector2(map.Position.X + map.Size.X * f, rng.R(TopH + 50, Size.Y - BottomH - 20));
            DrawSetTransform(p, rng.R(0, 6), Vector2.One);
            var leaf = new Color(0.78f, 0.38f, 0.14f).Lerp(new Color(0.85f, 0.62f, 0.2f), rng.F());
            DrawColoredPolygon(new[] { new Vector2(-12, 0), new Vector2(0, -5), new Vector2(12, 0), new Vector2(0, 5) }, new Color(leaf, 0.8f));
            DrawLine(new Vector2(-12, 0), new Vector2(14, 0), leaf.Darkened(0.4f), 1);
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// A smooth trail through every level; the walked part is a bright pheromone line with ants marching on it.
    void DrawTrail()
    {
        var pts = new List<Vector2>();
        for (int n = 1; n <= Levels.Count; n++) pts.Add(Spot(n));
        var curve = new List<Vector2>();
        var walkedTo = 0;
        for (int n = 1; n <= Levels.Count; n++) if (CampaignProgress.Unlocked(Levels.IdAt(n))) walkedTo = n;
        int walkedPoints = 0;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            var p0 = pts[Math.Max(0, i - 1)]; var p1 = pts[i]; var p2 = pts[i + 1]; var p3 = pts[Math.Min(pts.Count - 1, i + 2)];
            for (int k = 0; k < 16; k++)
            {
                float t = k / 16f;
                float t2 = t * t, t3 = t2 * t;
                curve.Add(0.5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3));
            }
            if (i + 1 < walkedTo) walkedPoints = curve.Count;
        }
        curve.Add(pts[^1]);
        if (walkedTo >= Levels.Count) walkedPoints = curve.Count;
        // the worn path in the sand
        DrawPolyline(curve.ToArray(), new Color(0.34f, 0.23f, 0.12f, 0.35f), 16, true);
        // unwalked: faint dots
        for (int i = walkedPoints; i < curve.Count; i += 3) DrawCircle(curve[i], 2.2f, new Color(0.95f, 0.85f, 0.6f, 0.35f));
        if (walkedPoints > 1)
        {
            var walked = curve.GetRange(0, walkedPoints).ToArray();
            DrawPolyline(walked, new Color(1f, 0.72f, 0.28f, 0.28f), 9, true);
            DrawPolyline(walked, new Color(1f, 0.84f, 0.45f, 0.75f), 2.5f, true);
            // ants walking the trail toward the next level
            float len = 0; var seg = new float[walkedPoints];
            for (int i = 1; i < walkedPoints; i++) { len += walked[i].DistanceTo(walked[i - 1]); seg[i] = len; }
            int ants = Mathf.Clamp((int)(len / 46), 3, 40);
            for (int a = 0; a < ants; a++)
            {
                float d = Mathf.PosMod(a * len / ants + _t * 22, len);
                int j = Array.BinarySearch(seg, d); if (j < 0) j = ~j; j = Math.Clamp(j, 1, walkedPoints - 1);
                float f = (d - seg[j - 1]) / Mathf.Max(0.001f, seg[j] - seg[j - 1]);
                var p = walked[j - 1].Lerp(walked[j], f);
                var dir = (walked[j] - walked[j - 1]).Normalized();
                DrawAnt(p + new Vector2(-dir.Y, dir.X) * (a % 2 == 0 ? 3 : -3), dir, a);
            }
        }
    }

    void DrawAnt(Vector2 p, Vector2 dir, int seed)
    {
        var side = new Vector2(-dir.Y, dir.X);
        var body = new Color(0.12f, 0.08f, 0.05f);
        float wig = Mathf.Sin(_t * 18 + seed) * 1.2f;
        for (int l = -1; l <= 1; l++)
            for (int s = -1; s <= 1; s += 2)
                DrawLine(p + dir * l * 2.2f, p + dir * (l * 2.6f + s * wig * 0.4f) + side * s * 4.2f, body, 1, true);
        DrawCircle(p - dir * 4.2f, 2.6f, body);
        DrawCircle(p, 1.8f, body);
        DrawCircle(p + dir * 3.2f, 2f, body);
        DrawCircle(p - dir * 4.8f + side * -0.8f, 0.9f, new Color(1, 0.85f, 0.6f, 0.35f));
    }

    void DrawNode(int n)
    {
        int id = Levels.IdAt(n);
        var c = Spot(n);
        float r = NodeR;
        bool open = CampaignProgress.Unlocked(id), done = CampaignProgress.Completed(id), sel = n == _selected, next = id == CampaignProgress.Next;
        var season = Levels.Info(id).Season;
        var face = new[] { new Color(0.42f, 0.55f, 0.2f), new Color(0.78f, 0.55f, 0.16f), new Color(0.72f, 0.33f, 0.12f), new Color(0.56f, 0.2f, 0.16f) }[season];
        if (next) DrawCircle(c, r * (1.45f + 0.12f * Mathf.Sin(_t * 3)), new Color(1f, 0.8f, 0.35f, 0.18f));
        if (sel) DrawArc(c, r * 1.32f, 0, Mathf.Tau, 48, Emblem.Gold, 3, true);
        DrawCircle(c + new Vector2(0, r * 0.14f), r * 1.04f, new Color(0, 0, 0, 0.45f));
        DrawCircle(c, r, open ? new Color(0.62f, 0.42f, 0.18f) : new Color(0.32f, 0.25f, 0.18f));
        DrawCircle(c, r * 0.84f, open ? done ? face : face.Darkened(0.35f) : new Color(0.2f, 0.16f, 0.12f));
        DrawArc(c, r * 0.84f, Mathf.Pi * 1.1f, Mathf.Pi * 1.9f, 20, new Color(1, 0.95f, 0.8f, open ? 0.35f : 0.1f), 2, true);
        if (open)
        {
            var label = n.ToString();
            float fs = r * 0.95f;
            var size = UiTheme.Title.GetStringSize(label, HorizontalAlignment.Left, -1, (int)fs);
            DrawString(UiTheme.Title, c + new Vector2(-size.X / 2 + 1, fs * 0.36f + 1), label, HorizontalAlignment.Left, -1, (int)fs, new Color(0, 0, 0, 0.55f));
            DrawString(UiTheme.Title, c + new Vector2(-size.X / 2, fs * 0.36f), label, HorizontalAlignment.Left, -1, (int)fs, new Color(1f, 0.93f, 0.75f));
        }
        else
        {
            // a padlock
            DrawArc(c + new Vector2(0, -r * 0.12f), r * 0.22f, Mathf.Pi, Mathf.Tau, 12, new Color(0.62f, 0.52f, 0.4f), 3, true);
            DrawRect(new Rect2(c + new Vector2(-r * 0.32f, -r * 0.12f), new Vector2(r * 0.64f, r * 0.5f)), new Color(0.62f, 0.52f, 0.4f));
        }
        int bits = CampaignProgress.StarBits(id);
        for (int s = 0; s < 3; s++)
            Emblem.Star(this, c + new Vector2((s - 1) * r * 0.62f, r * 1.12f + (s == 1 ? r * 0.12f : 0)), r * 0.3f, (bits & 1 << s) != 0);
        // the name, under the stars
        var name = Levels.Info(id).Name;
        int nf = 14;
        var ns = UiTheme.Bold.GetStringSize(name, HorizontalAlignment.Left, -1, nf);
        var at = c + new Vector2(-ns.X / 2, r * 1.75f + 14);
        DrawString(UiTheme.Bold, at + new Vector2(1, 1), name, HorizontalAlignment.Left, -1, nf, new Color(0, 0, 0, 0.6f));
        DrawString(UiTheme.Bold, at, name, HorizontalAlignment.Left, -1, nf, open ? new Color(1f, 0.94f, 0.8f) : new Color(0.75f, 0.66f, 0.55f, 0.75f));
    }

    // ------------------------------------------------------------------ small widgets
    partial class Pill : Control
    {
        public string Text;
        public Color Fill;
        public Pill() { CustomMinimumSize = new Vector2(0, 32); MouseFilter = MouseFilterEnum.Ignore; }
        public override void _Draw()
        {
            var w = UiTheme.Bold.GetStringSize(Text, HorizontalAlignment.Left, -1, 15).X + 28;
            DrawStyleBox(UiTheme.Box(Fill, Emblem.Gold, 1, 14, 0), new Rect2(0, 0, Mathf.Min(w, Size.X), 30));
            DrawString(UiTheme.Bold, new Vector2(14, 21), Text, HorizontalAlignment.Left, Size.X - 28, 15, new Color(1f, 0.9f, 0.65f));
        }
    }

    partial class Swatch : Godot.Button
    {
        public Color Colour;
        public int EmblemId = -1, Cost;
        public string Label;
        public bool Open, Chosen;
        public override void _Ready() { CustomMinimumSize = new Vector2(112, 112); Text = ""; TooltipText = Open ? Label : $"{Label}: {Cost} stars"; }
        public override void _Draw()
        {
            var c = new Vector2(Size.X / 2, 44);
            if (EmblemId >= 0) Emblem.Banner(this, c, 30, EmblemId, Open ? Colour : new Color(0.4f, 0.34f, 0.28f));
            else { DrawCircle(c, 30, new Color(0, 0, 0, 0.4f)); DrawCircle(c, 27, Open ? Colour : new Color(Colour.Darkened(0.3f), 0.35f)); }
            if (Chosen) DrawArc(c, 35, 0, Mathf.Tau, 40, Emblem.Gold, 3, true);
            var text = Open ? Label : $"{Cost}";
            var w = UiTheme.Bold.GetStringSize(text, HorizontalAlignment.Left, -1, 13).X;
            float x = Size.X / 2 - w / 2 - (Open ? 0 : 9);
            DrawString(UiTheme.Bold, new Vector2(x, 98), text, HorizontalAlignment.Left, -1, 13, Open ? UiTheme.Ink : UiTheme.Dim);
            if (!Open) Emblem.Star(this, new Vector2(x + w + 11, 93), 7, false);
        }
    }
}
