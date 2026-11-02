using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The level map: a cut through the colony's nest. Level 1 is the first chamber below the entrance; every level after
/// it is dug a little deeper, act by act, down to the Empress's chamber at the bottom. Ants walk the dug galleries,
/// locked chambers are still soil, won chambers keep a keepsake and their stars. Landscape on every device.
public partial class LevelMap : Control
{
    public Main App;
    public Action Back;
    int _selected;
    float _t;
    Control _card, _look;
    Label _total, _nextLabel;
    ProgressBar _nextBar;

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

    // ------------------------------------------------------------------ chrome: title, totals, buttons
    void BuildChrome()
    {
        var title = UiTheme.Text("THE COLONY", 40, UiTheme.Title, UiTheme.Gold);
        UiTheme.Place(title, 0, 0, 0, 0, 120, 22, 700, 78);
        AddChild(title);
        var sub = UiTheme.Text($"The Glass Queen's nest  ·  {CampaignProgress.CompletedCount} of {Levels.Count} chambers dug", 16, null, new Color(0.3f, 0.16f, 0.06f));
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
        var beat = Comic.For(id);
        if (open && beat != null)
        {
            var story = new Button { Text = id == 1 ? "Watch the opening" : "Watch the story so far", CustomMinimumSize = new Vector2(0, 52) };
            story.AddThemeFontOverride("font", UiTheme.Title);
            story.Pressed += () => { App.Audio.Ui("ui_click"); PlayStory(beat, info); };
            v.AddChild(story);
        }
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

    /// Replays a level's comic beat over the map.
    public void PlayStory(Comic.Panel[] beat, LevelInfo info)
        => AddChild(new ComicStrip { Panels = beat, Heading = $"{Levels.Seasons[info.Season].ToUpperInvariant()}  ·  {Levels.SeasonThemes[info.Season].ToUpperInvariant()}" });

    /// Selects a level as a click would (the capture test uses it).
    public void Select(int number) { _selected = Mathf.Clamp(number, 1, Levels.Count); BuildCard(); QueueRedraw(); }

    public override void _Process(double delta) { _t += (float)delta; QueueRedraw(); }

    public override void _Draw()
    {
        DrawSoil();
        DrawTunnels();
        for (int n = 1; n <= Levels.Count; n++) DrawChamber(n);
        // the colony banner beside the title
        Emblem.Banner(this, new Vector2(70, 60), 38, CampaignProgress.Emblem, Levels.Colours[CampaignProgress.Colour]);
        // the stars total's own star
        var font = UiTheme.Title;
        float w = font.GetStringSize(_total.Text, HorizontalAlignment.Left, -1, 30).X;
        Emblem.Star(this, new Vector2(Size.X - CardW - 70 - w - 26, 46), 16, true, 0.6f);
    }

    int LabelSize => Settings.Mobile ? 19 : 15;
    float Ground => TopH + 30;   // the surface line: the yard above, the colony below

    /// A cut through the woodland floor: warm evening sky and grass on top, layered earth beneath, darker with depth.
    void DrawSoil()
    {
        var map = MapRect;
        float g = Ground;
        // the evening sky behind the grass
        DrawPolygon(new[] { Vector2.Zero, new Vector2(Size.X, 0), new Vector2(Size.X, g), new Vector2(0, g) },
            new[] { new Color(0.86f, 0.62f, 0.34f), new Color(0.93f, 0.7f, 0.38f), new Color(0.98f, 0.82f, 0.5f), new Color(0.95f, 0.76f, 0.44f) });
        // earth strata: one band per act, each a little darker and redder
        var bands = new[] { new Color(0.62f, 0.45f, 0.26f), new Color(0.53f, 0.35f, 0.19f), new Color(0.44f, 0.27f, 0.14f), new Color(0.35f, 0.2f, 0.1f) };
        var rng = new Rng(20261102);
        float[] edges = BandEdges();
        for (int b = 0; b < 4; b++)
        {
            float y0 = edges[b], y1 = edges[b + 1];
            // a wavy top edge so the strata read as earth, not stripes
            var poly = new List<Vector2>();
            var cols = new List<Color>();
            int steps = 24;
            for (int i = 0; i <= steps; i++)
            {
                float x = Size.X * i / steps;
                float wob = b == 0 ? 0 : Mathf.Sin(i * 1.3f + b * 2.1f) * 7 + Mathf.Sin(i * 0.37f + b) * 9;
                poly.Add(new Vector2(x, y0 + wob)); cols.Add(bands[b].Lightened(0.06f));
            }
            float floor = b == 3 ? Size.Y : y1 + 12;
            poly.Add(new Vector2(Size.X, floor)); cols.Add(bands[b].Darkened(0.06f));
            poly.Add(new Vector2(0, floor)); cols.Add(bands[b].Darkened(0.06f));
            DrawPolygon(poly.ToArray(), cols.ToArray());
            // the seam between strata: a darker line of packed clay
            if (b > 0) DrawPolyline(poly.GetRange(0, steps + 1).ToArray(), new Color(0.22f, 0.12f, 0.05f, 0.45f), 3, true);
            // the act, carved upright in the left margin of its stratum (the card names the act in full)
            var act = Levels.Seasons[b].ToUpperInvariant();
            float lw = UiTheme.Title.GetStringSize(act, HorizontalAlignment.Left, -1, LabelSize + 4).X;
            float mid = (b == 0 ? (y0 + 30 + y1) / 2 : (y0 + y1) / 2) + lw / 2;
            DrawSetTransform(new Vector2(map.Position.X + LabelSize + 6, mid), -Mathf.Pi / 2, Vector2.One);
            DrawString(UiTheme.Title, new Vector2(1, 1), act, HorizontalAlignment.Left, -1, LabelSize + 4, new Color(0.18f, 0.1f, 0.04f, 0.6f));
            DrawString(UiTheme.Title, Vector2.Zero, act, HorizontalAlignment.Left, -1, LabelSize + 4, new Color(1f, 0.86f, 0.6f, 0.92f));
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
        // grit, buried stones and roots
        for (int i = 0; i < 130; i++)
        {
            var p = new Vector2(rng.R(0, Size.X - CardW - 30), rng.R(g + 10, Size.Y - BottomH));
            float r = rng.R(2, 9) * (rng.F() < 0.1f ? 2.4f : 1);
            DrawSetTransform(p, rng.R(0, 3), new Vector2(1, rng.R(0.55f, 0.85f)));
            var tone = new Color(rng.R(0.62f, 0.8f), rng.R(0.48f, 0.6f), rng.R(0.32f, 0.42f));
            DrawCircle(new Vector2(r * 0.15f, r * 0.3f), r, new Color(0.15f, 0.08f, 0.03f, 0.35f));
            DrawCircle(Vector2.Zero, r, tone.Darkened(Mathf.Clamp((p.Y - g) / Size.Y, 0, 0.5f)));
            DrawCircle(new Vector2(-r * 0.3f, -r * 0.35f), r * 0.35f, new Color(1, 0.94f, 0.8f, 0.3f));
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        for (int i = 0; i < 9; i++)
        {
            // roots hanging from the grass, thinning as they go down
            var p = new Vector2(rng.R(60, Size.X - CardW - 60), g + 2);
            var pts = new List<Vector2> { p };
            float len = rng.R(60, 200), dx = rng.R(-0.5f, 0.5f);
            for (int k = 1; k <= 8; k++) pts.Add(p + new Vector2(dx * len * k / 8 + Mathf.Sin(k * 1.7f + i) * 6, len * k / 8));
            for (int k = 1; k < pts.Count; k++) DrawLine(pts[k - 1], pts[k], new Color(0.3f, 0.18f, 0.08f, 0.75f), Mathf.Lerp(4, 1, k / 8f), true);
        }
        // the surface: a grass fringe with a few fallen leaves, the colony's entrance mound on the left
        for (int i = 0; i < 150; i++)
        {
            var root = new Vector2(rng.R(0, Size.X), g + rng.R(-2, 3));
            var green = new Color(0.42f, 0.5f, 0.17f).Lerp(new Color(0.64f, 0.52f, 0.2f), rng.F() * 0.7f);
            for (int b = 0; b < 3; b++)
            {
                float lean = rng.R(-0.45f, 0.45f), h = rng.R(10, 38) * (rng.F() < 0.15f ? 1.6f : 1);
                DrawColoredPolygon(new[] { root + new Vector2(-2.2f, 0), root + new Vector2(lean * h, -h), root + new Vector2(2.2f, 0) }, green.Darkened(rng.R(0, 0.25f)));
            }
        }
        var hole = Entrance;
        DrawColoredPolygon(new[] { hole + new Vector2(-58, 2), hole + new Vector2(-24, -14), hole + new Vector2(24, -14), hole + new Vector2(58, 2) }, new Color(0.62f, 0.45f, 0.26f));
        DrawSetTransform(hole + new Vector2(0, -6), 0, new Vector2(1, 0.45f));
        DrawCircle(Vector2.Zero, 16, new Color(0.14f, 0.07f, 0.03f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    Vector2 Entrance => new(MapRect.Position.X + MapRect.Size.X * 0.06f, Ground);

    float[] BandEdges()
    {
        float top = Ground, bottom = Size.Y - BottomH - 8;   // the deepest chambers stay clear of the buttons
        float h = (bottom - top) / 4;
        return new[] { top, top + h, top + 2 * h, top + 3 * h, bottom };
    }

    /// Chamber centres: four rows, one per act, each a stratum deeper; the rows wind left, right, left, right.
    Vector2 Spot(int number)
    {
        var map = MapRect;
        float[] e = BandEdges();
        int row = (number - 1) / 3, col = (number - 1) % 3;
        if (row % 2 == 1) col = 2 - col;
        float x = map.Position.X + map.Size.X * (0.22f + col * 0.31f + (row % 2 == 0 ? 0 : 0.04f));
        float y = Mathf.Lerp(e[row], e[row + 1], row == 0 ? 0.5f : 0.4f) + (col == 1 ? 6 : 0);
        return new Vector2(x, y);
    }

    /// Chamber size: as wide as the map allows, but never taller than its stratum can hold with its stars and name.
    float NodeR { get { var e = BandEdges(); return Mathf.Clamp(Mathf.Min(MapRect.Size.X / 26, (e[1] - e[0]) * 0.27f), 24, 46); } }

    /// The tunnels: dug and walked up to the last open chamber, a faint line of loose soil beyond it.
    void DrawTunnels()
    {
        var pts = new List<Vector2> { Entrance + new Vector2(0, -2) };
        for (int n = 1; n <= Levels.Count; n++) pts.Add(Spot(n));
        int open = 0;
        for (int n = 1; n <= Levels.Count; n++) if (CampaignProgress.Unlocked(Levels.IdAt(n))) open = n;
        var curve = new List<Vector2>();
        int dug = 0;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            // each tunnel sags a little between chambers, like a real gallery
            var a = pts[i]; var b = pts[i + 1];
            var mid = (a + b) / 2 + new Vector2(0, Mathf.Abs(b.X - a.X) > 40 ? 26 : 0) + new Vector2(Mathf.Abs(b.X - a.X) > 40 ? 0 : (i % 2 == 0 ? 40 : -40), 0);
            for (int k = 0; k < 18; k++)
            {
                float t = k / 18f;
                curve.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * mid + t * t * b);
            }
            if (i < open) dug = curve.Count;
        }
        curve.Add(pts[^1]);
        if (open >= Levels.Count) dug = curve.Count;
        float r = NodeR * 0.32f;
        // the undug route: loose crumbs only
        for (int i = dug; i < curve.Count; i += 3) DrawCircle(curve[i], 2.4f, new Color(0.24f, 0.14f, 0.07f, 0.5f));
        if (dug < 2) return;
        var tunnel = curve.GetRange(0, dug).ToArray();
        DrawPolyline(tunnel, new Color(0.62f, 0.44f, 0.24f, 0.55f), r * 2 + 6, true);   // the packed rim
        DrawPolyline(tunnel, new Color(0.17f, 0.09f, 0.04f), r * 2, true);              // the gallery
        DrawPolyline(tunnel, new Color(0.3f, 0.17f, 0.08f), r * 0.9f, true);            // its worn floor
        // ants walking the galleries
        float len = 0; var seg = new float[dug];
        for (int i = 1; i < dug; i++) { len += tunnel[i].DistanceTo(tunnel[i - 1]); seg[i] = len; }
        int ants = Mathf.Clamp((int)(len / 40), 3, 60);
        for (int a = 0; a < ants; a++)
        {
            bool down = a % 2 == 0;
            float d = Mathf.PosMod(a * len / ants + (down ? 1 : -1) * _t * 20, len);
            int j = Array.BinarySearch(seg, d); if (j < 0) j = ~j; j = Math.Clamp(j, 1, dug - 1);
            float f = (d - seg[j - 1]) / Mathf.Max(0.001f, seg[j] - seg[j - 1]);
            var p = tunnel[j - 1].Lerp(tunnel[j], f);
            var dir = (tunnel[j] - tunnel[j - 1]).Normalized() * (down ? 1 : -1);
            DrawAnt(p + new Vector2(-dir.Y, dir.X) * 2.5f, dir, a);
        }
    }

    void DrawAnt(Vector2 p, Vector2 dir, int seed)
    {
        var side = new Vector2(-dir.Y, dir.X);
        var body = new Color(0.06f, 0.035f, 0.02f);
        float wig = Mathf.Sin(_t * 18 + seed) * 1.2f;
        for (int l = -1; l <= 1; l++)
            for (int s = -1; s <= 1; s += 2)
                DrawLine(p + dir * l * 2.2f, p + dir * (l * 2.6f + s * wig * 0.4f) + side * s * 4.2f, body, 1, true);
        DrawCircle(p - dir * 4.2f, 2.6f, body);
        DrawCircle(p, 1.8f, body);
        DrawCircle(p + dir * 3.2f, 2f, body);
        DrawCircle(p - dir * 4.8f - side * 0.8f, 0.9f, new Color(1, 0.8f, 0.5f, 0.45f));
        if (seed % 5 == 0) DrawCircle(p + dir * 5.4f, 1.6f, new Color(0.95f, 0.85f, 0.55f)); // a seed in the jaws
    }

    /// One chamber: undug soil while locked, a lit hollow once open, its trophy and stars once won.
    void DrawChamber(int n)
    {
        int id = Levels.IdAt(n);
        var c = Spot(n);
        float r = NodeR * (n == Levels.Count ? 1.25f : 1);   // the Empress's chamber, the deepest and widest
        bool open = CampaignProgress.Unlocked(id), done = CampaignProgress.Completed(id), sel = n == _selected, next = id == CampaignProgress.Next;
        var size = new Vector2(r * 1.55f, r);
        if (!open)
        {
            // undug: a faint ring of loosened soil and a carved lock
            DrawSetTransform(c, 0, new Vector2(1.55f, 1));
            DrawArc(Vector2.Zero, r, 0, Mathf.Tau, 40, new Color(0.2f, 0.11f, 0.05f, 0.55f), 2, true);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
            var lockC = new Color(0.62f, 0.4f, 0.2f);
            DrawArc(c + new Vector2(0, -r * 0.14f), r * 0.2f, Mathf.Pi, Mathf.Tau, 12, lockC, 3, true);
            DrawRect(new Rect2(c + new Vector2(-r * 0.3f, -r * 0.14f), new Vector2(r * 0.6f, r * 0.46f)), lockC);
            DrawCircle(c + new Vector2(0, r * 0.06f), r * 0.07f, new Color(0.25f, 0.14f, 0.06f));
        }
        else
        {
            if (next) { DrawSetTransform(c, 0, new Vector2(1.55f, 1)); DrawCircle(Vector2.Zero, r * (1.28f + 0.07f * Mathf.Sin(_t * 3)), new Color(1f, 0.72f, 0.3f, 0.2f)); DrawSetTransform(Vector2.Zero, 0, Vector2.One); }
            // the hollow: a dark rim, then a warm interior lit from within, the floor packed flat
            DrawSetTransform(c, 0, new Vector2(1.55f, 1));
            DrawCircle(new Vector2(0, 2), r * 1.08f, new Color(0.58f, 0.4f, 0.21f, 0.6f));
            DrawCircle(Vector2.Zero, r, new Color(0.13f, 0.065f, 0.03f));
            DrawCircle(new Vector2(0, r * 0.12f), r * 0.86f, done ? new Color(0.46f, 0.27f, 0.11f) : new Color(0.3f, 0.17f, 0.08f));
            DrawCircle(new Vector2(0, r * 0.3f), r * 0.6f, done ? new Color(0.72f, 0.46f, 0.18f, 0.55f) : new Color(0.5f, 0.3f, 0.12f, 0.4f));
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
            DrawLine(c + new Vector2(-r * 1.2f, r * 0.62f), c + new Vector2(r * 1.2f, r * 0.62f), new Color(0.62f, 0.42f, 0.2f), 3, true);
            if (done) DrawTrophy(id, c + new Vector2(r * 0.42f, r * 0.2f), r * 0.42f);
            if (sel) { DrawSetTransform(c, 0, new Vector2(1.55f, 1)); DrawArc(Vector2.Zero, r * 1.16f, 0, Mathf.Tau, 48, Emblem.Gold, 2.5f, true); DrawSetTransform(Vector2.Zero, 0, Vector2.One); }
            // the level number on the chamber wall
            var label = n.ToString();
            int fs = (int)(r * 0.8f);
            var ls = UiTheme.Title.GetStringSize(label, HorizontalAlignment.Left, -1, fs);
            var at = c + new Vector2(-r * 0.55f - ls.X / 2, fs * 0.36f);
            DrawString(UiTheme.Title, at + new Vector2(1, 1), label, HorizontalAlignment.Left, -1, fs, new Color(0, 0, 0, 0.6f));
            DrawString(UiTheme.Title, at, label, HorizontalAlignment.Left, -1, fs, new Color(1f, 0.88f, 0.6f));
        }
        int bits = CampaignProgress.StarBits(id);
        for (int s = 0; s < 3; s++)
            Emblem.Star(this, c + new Vector2((s - 1) * r * 0.62f, r * 0.98f), r * 0.27f, (bits & 1 << s) != 0);
        // the name under the chamber
        var name = Levels.Info(id).Name;
        int nf = Settings.Mobile ? 18 : 15;
        var ns = UiTheme.Bold.GetStringSize(name, HorizontalAlignment.Left, -1, nf);
        var np = c + new Vector2(-ns.X / 2, r * 1.22f + nf);
        DrawString(UiTheme.Bold, np + new Vector2(1, 1), name, HorizontalAlignment.Left, -1, nf, new Color(0.08f, 0.04f, 0.01f, 0.7f));
        DrawString(UiTheme.Bold, np, name, HorizontalAlignment.Left, -1, nf, open ? new Color(1f, 0.92f, 0.74f) : new Color(0.86f, 0.62f, 0.38f));
    }

    /// What each won chamber keeps: a keepsake of its level.
    void DrawTrophy(int id, Vector2 c, float r)
    {
        var amber = new Color(1f, 0.78f, 0.32f);
        switch (id)
        {
            case 1: // a shard of the tube
                DrawColoredPolygon(new[] { c + new Vector2(-r * 0.5f, r * 0.5f), c + new Vector2(-r * 0.1f, -r * 0.7f), c + new Vector2(r * 0.55f, r * 0.45f) }, new Color(0.85f, 0.95f, 0.9f, 0.7f));
                DrawLine(c + new Vector2(-r * 0.1f, -r * 0.7f), c + new Vector2(r * 0.55f, r * 0.45f), new Color(1, 1, 1, 0.9f), 1.5f, true);
                break;
            case 5: DrawCircle(c, r * 0.42f, amber); DrawCircle(c + new Vector2(-r * 0.12f, -r * 0.12f), r * 0.12f, new Color(1, 0.97f, 0.8f)); break; // honeydew
            case 2: DrawCircle(c, r * 0.36f, new Color(0.58f, 0.82f, 0.36f)); DrawCircle(c + new Vector2(r * 0.4f, -r * 0.1f), r * 0.2f, new Color(0.5f, 0.75f, 0.3f)); break; // an aphid
            case 6: case 10: // a sting, a scrap of paper nest
                if (id == 10) DrawColoredPolygon(new[] { c + new Vector2(-r * 0.5f, -r * 0.3f), c + new Vector2(r * 0.5f, -r * 0.4f), c + new Vector2(r * 0.4f, r * 0.4f), c + new Vector2(-r * 0.45f, r * 0.35f) }, new Color(0.8f, 0.68f, 0.5f));
                DrawColoredPolygon(new[] { c + new Vector2(-r * 0.1f, -r * 0.5f), c + new Vector2(r * 0.1f, -r * 0.5f), c + new Vector2(0, r * 0.55f) }, new Color(0.95f, 0.75f, 0.2f));
                break;
            case 3: case 11: // a captured red banner
                DrawLine(c + new Vector2(-r * 0.35f, r * 0.6f), c + new Vector2(-r * 0.35f, -r * 0.6f), new Color(0.45f, 0.3f, 0.15f), 2, true);
                DrawColoredPolygon(new[] { c + new Vector2(-r * 0.35f, -r * 0.6f), c + new Vector2(r * 0.55f, -r * 0.35f), c + new Vector2(-r * 0.35f, -r * 0.1f) }, new Color(0.88f, 0.28f, 0.14f));
                break;
            case 7: DrawCircle(c, r * 0.38f, new Color(0.55f, 0.88f, 0.2f)); DrawCircle(c + new Vector2(-r * 0.1f, -r * 0.12f), r * 0.1f, new Color(0.9f, 1, 0.7f)); break; // a drop of Vetch's acid
            case 8: DrawSetTransform(c, 0.5f, new Vector2(1, 0.55f)); DrawCircle(Vector2.Zero, r * 0.5f, new Color(0.82f, 0.62f, 0.32f)); DrawSetTransform(Vector2.Zero, 0, Vector2.One); break; // a seed husk
            case 9: case 4: // a wing
                var wing = id == 4 ? new Color(1f, 0.55f, 0.35f, 0.75f) : new Color(1f, 0.88f, 0.6f, 0.75f);
                DrawSetTransform(c, -0.5f, new Vector2(1, 0.38f)); DrawCircle(Vector2.Zero, r * 0.62f, wing); DrawSetTransform(Vector2.Zero, 0, Vector2.One);
                break;
            default: Emblem.Draw(this, c, r * 0.6f, 4, amber); break; // the Empress's crown
        }
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
        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(112, 112); Text = ""; TooltipText = Open ? Label : $"{Label}: {Cost} stars";
            // warm earth tiles, never grey
            AddThemeStyleboxOverride("normal", UiTheme.Box(new Color(0.34f, 0.2f, 0.09f), new Color(0.7f, 0.46f, 0.2f), 1, 8, 4));
            AddThemeStyleboxOverride("hover", UiTheme.Box(new Color(0.44f, 0.27f, 0.12f), Emblem.Gold, 1, 8, 4));
            AddThemeStyleboxOverride("pressed", UiTheme.Box(new Color(0.5f, 0.3f, 0.12f), Emblem.Gold, 2, 8, 4));
        }
        public override void _Draw()
        {
            var c = new Vector2(Size.X / 2, 44);
            if (EmblemId >= 0) Emblem.Banner(this, c, 30, EmblemId, Open ? Colour : new Color(0.52f, 0.34f, 0.18f));
            else { DrawCircle(c, 30, new Color(0, 0, 0, 0.4f)); DrawCircle(c, 27, Open ? Colour : new Color(Colour.Darkened(0.3f), 0.35f)); }
            if (Chosen) DrawArc(c, 35, 0, Mathf.Tau, 40, Emblem.Gold, 3, true);
            var text = Open ? Label : $"{Cost}";
            var w = UiTheme.Bold.GetStringSize(text, HorizontalAlignment.Left, -1, 13).X;
            float x = Size.X / 2 - w / 2 - (Open ? 0 : 9);
            DrawString(UiTheme.Bold, new Vector2(x, 98), text, HorizontalAlignment.Left, -1, 13, Open ? UiTheme.Ink : new Color(0.95f, 0.72f, 0.45f));
            if (!Open) Emblem.Star(this, new Vector2(x + w + 11, 93), 7, false);
        }
    }
}
