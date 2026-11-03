using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The level map: a cut through the colony's nest (ColonyMapArt). Level 1 is the first chamber below the entrance;
/// every level after it is dug a little deeper, act by act, down to the royal chamber at the bottom. Locked chambers
/// are still soil, won chambers are lit, each carries its numeral, stars and name. Landscape on every device.
public partial class LevelMap : Control
{
    public Main App;
    public Action Back;
    int _selected;
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
        _nextLabel.Text = next == null ? "Every unlock earned: the Eternal Mother" : $"Next unlock at {next.Stars} stars:  {CampaignProgress.UnlockName(next)}";
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
        // a locked level keeps what it introduces to itself until it opens (the campaign reveals the game gradually)
        v.AddChild(new Pill { Text = open ? "NEW  ·  " + info.Novelty : "LOCKED  ·  WHAT IT BRINGS IS STILL UNDER THE SOIL", Fill = open ? new Color(0.45f, 0.3f, 0.1f) : new Color(0.2f, 0.16f, 0.12f) });
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
                if (InChamber(mb.Position, n))
                {
                    App.Audio.Ui("ui_click");
                    if (_selected == n && CampaignProgress.Unlocked(Levels.IdAt(n)) && mb.DoubleClick) { App.StartEpisode(Levels.IdAt(n)); return; }
                    _selected = n; BuildCard(); QueueRedraw(); _scene?.Refresh();
                    return;
                }
    }

    /// Replays a level's comic beat over the map.
    public void PlayStory(Comic.Panel[] beat, LevelInfo info)
        => AddChild(new ComicStrip { Panels = beat, Heading = $"{Levels.Seasons[info.Season].ToUpperInvariant()}  ·  {Levels.SeasonThemes[info.Season].ToUpperInvariant()}" });

    /// Selects a level as a click would (the capture test uses it).
    public void Select(int number) { _selected = Mathf.Clamp(number, 1, Levels.Count); BuildCard(); QueueRedraw(); _scene?.Refresh(); }

    public override void _Process(double delta) { if (Size != _built) BuildScene(); }

    // ------------------------------------------------------------------ the painted nest behind the UI
    ColonyMapArt _scene;
    Vector2 _built;

    void BuildScene()
    {
        if (Size.X < 10 || Size == _built) return;
        _built = Size;
        if (_scene == null)
        {
            var under = new ColorRect { Color = new Color(0.13f, 0.07f, 0.04f), MouseFilter = MouseFilterEnum.Ignore };
            UiTheme.Place(under, 0, 0, 1, 1, 0, 0, 0, 0);
            AddChild(under);
            MoveChild(under, 0);
            _scene = new ColonyMapArt { Map = this };
            UiTheme.Place(_scene, 0, 0, 1, 1, 0, 0, 0, 0);
            AddChild(_scene);
            MoveChild(_scene, 1);
            // the map's own drawing (title band, act tags, plates, names) sits on top of the picture
            _overlay = new Overlay { Map = this };
            UiTheme.Place(_overlay, 0, 0, 1, 1, 0, 0, 0, 0);
            AddChild(_overlay);
            MoveChild(_overlay, 2);
        }
        _scene.QueueRedraw();
        _overlay.QueueRedraw();
    }

    Overlay _overlay;
    partial class Overlay : Control
    {
        public LevelMap Map;
        public Overlay() { MouseFilter = MouseFilterEnum.Ignore; }
        public override void _Draw() => Map.DrawOverlay(this);
    }

    public int Selected => _selected;
    /// Half the height of chamber n on screen.
    public float ChamberRadius(int n) => ColonyMapArt.Anchors[n - 1].W * ArtRect.Size.Y;

    bool InChamber(Vector2 p, int n)
    {
        var a = ColonyMapArt.Anchors[n - 1];
        var d = (p - Spot(n)) / (new Vector2(a.Z, a.W) * ArtRect.Size * 1.15f);
        return d.LengthSquared() < 1;
    }

    public override void _Draw() => _overlay?.QueueRedraw();

    void DrawOverlay(Control ci)
    {
        var size = Size;
        // the title band: near-black over the turf so the gold reads
        ci.DrawPolygon(new[] { Vector2.Zero, new Vector2(size.X, 0), new Vector2(size.X, TopH - 4), new Vector2(0, TopH - 4) },
            new[] { new Color(0.04f, 0.025f, 0.015f, 0.94f), new Color(0.04f, 0.025f, 0.015f, 0.94f), new Color(0.04f, 0.025f, 0.015f, 0.0f), new Color(0.04f, 0.025f, 0.015f, 0.0f) });
        Emblem.Engraved(ci, new Vector2(64, 56), 32, CampaignProgress.Emblem, true);
        var map = MapRect;
        for (int b = 0; b < 4; b++)
        {
            // the act, carved upright in the margin beside its chambers (the card names it in full)
            var act = Levels.Seasons[b].ToUpperInvariant();
            float lw = UiTheme.Title.GetStringSize(act, HorizontalAlignment.Left, -1, LabelSize + 4).X;
            float mid = 0;
            for (int n = b * 3 + 1; n <= b * 3 + 3; n++) mid += Spot(n).Y / 3;
            mid += lw / 2;
            ci.DrawSetTransform(new Vector2(map.Position.X + LabelSize + 6, mid), -Mathf.Pi / 2, Vector2.One);
            ci.DrawString(UiTheme.Title, new Vector2(1, 1), act, HorizontalAlignment.Left, -1, LabelSize + 4, new Color(0.05f, 0.02f, 0.01f, 0.7f));
            ci.DrawString(UiTheme.Title, Vector2.Zero, act, HorizontalAlignment.Left, -1, LabelSize + 4, new Color(0.95f, 0.78f, 0.5f, 0.75f));
            ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
        for (int n = 1; n <= Levels.Count; n++)
        {
            int id = Levels.IdAt(n);
            bool open = CampaignProgress.Unlocked(id);
            var c = Spot(n);
            var name = Levels.Info(id).Name;
            int nf = Settings.Mobile ? 18 : 15;
            var ns = UiTheme.Title.GetStringSize(name, HorizontalAlignment.Left, -1, nf);
            float rr = ChamberRadius(n);
            // the numeral on a small bronze plate above the chamber, the stars below it
            var numeral = Roman(n);
            float pf = Settings.Mobile ? 20 : 16;
            var ps = UiTheme.Title.GetStringSize(numeral, HorizontalAlignment.Left, -1, (int)pf);
            var plate = new Rect2(c + new Vector2(-ps.X / 2 - 10, -rr - pf - 14), new Vector2(ps.X + 20, pf + 8));
            ci.DrawStyleBox(UiTheme.Box(open ? new Color(0.2f, 0.12f, 0.06f, 0.9f) : new Color(0.1f, 0.07f, 0.05f, 0.85f), open ? Emblem.Gold : new Color(0.45f, 0.32f, 0.2f), 1, 4, 0), plate);
            ci.DrawString(UiTheme.Title, plate.Position + new Vector2(10, pf + 1), numeral, HorizontalAlignment.Left, -1, (int)pf, open ? new Color(1f, 0.8f, 0.42f) : new Color(0.8f, 0.62f, 0.42f));
            if (!open)
            {
                // a small padlock beside the plate: the chamber is still to be dug
                var lk = plate.Position + new Vector2(plate.Size.X + 6, 4);
                ci.DrawArc(lk + new Vector2(6, 6), 4.5f, Mathf.Pi, Mathf.Tau, 12, new Color(0.85f, 0.66f, 0.42f), 2);
                ci.DrawRect(new Rect2(lk + new Vector2(0, 6), new Vector2(12, 10)), new Color(0.85f, 0.66f, 0.42f));
            }
            int bits = CampaignProgress.StarBits(id);
            for (int st = 0; st < 3; st++)
            {
                var sp = c + new Vector2((st - 1) * 15, rr + 9);
                bool on = (bits & 1 << st) != 0;
                ci.DrawCircle(sp, on ? 5.5f : 4f, on ? new Color(1f, 0.66f, 0.18f) : new Color(0.12f, 0.07f, 0.04f, 0.85f));
                if (on) ci.DrawCircle(sp + new Vector2(-1.5f, -1.5f), 1.6f, new Color(1f, 0.92f, 0.7f));
            }
            var np = c + new Vector2(-ns.X / 2, rr + nf + 20);
            var col = n == _selected ? new Color(1f, 0.8f, 0.42f) : open ? new Color(0.98f, 0.9f, 0.74f) : new Color(0.72f, 0.52f, 0.33f, 0.9f);
            // the name on a dark plate so it reads over the busy soil; the selected chamber gets a gold ring
            var nameBox = new Rect2(np + new Vector2(-8, -nf - 2), new Vector2(ns.X + 16, nf + 10));
            ci.DrawStyleBox(UiTheme.Box(new Color(0.06f, 0.035f, 0.02f, open ? 0.72f : 0.82f), new Color(0, 0, 0, 0), 0, 6, 0), nameBox);
            ci.DrawString(UiTheme.Title, np + new Vector2(1, 1), name, HorizontalAlignment.Left, -1, nf, new Color(0.04f, 0.02f, 0.01f, 0.85f));
            ci.DrawString(UiTheme.Title, np, name, HorizontalAlignment.Left, -1, nf, open ? col : new Color(0.86f, 0.68f, 0.46f));
            if (n == _selected)
            {
                var a = ColonyMapArt.Anchors[n - 1];
                var half = new Vector2(a.Z, a.W) * ArtRect.Size * 1.12f;
                var pts = new Vector2[49];
                for (int k = 0; k <= 48; k++) { float t = k / 48f * Mathf.Tau; pts[k] = c + new Vector2(Mathf.Cos(t) * half.X, Mathf.Sin(t) * half.Y); }
                ci.DrawPolyline(pts, new Color(0.05f, 0.03f, 0.01f, 0.8f), 6, true);
                ci.DrawPolyline(pts, new Color(1f, 0.78f, 0.35f), 3, true);
            }
        }
    }

    int LabelSize => Settings.Mobile ? 19 : 15;

    /// Where the nest picture lies: as wide as the map area allows (with its outer chambers kept clear of the card),
    /// its surface just under the title band; on short screens it shrinks to fit above the buttons.
    public Rect2 ArtRect
    {
        get
        {
            float areaW = Size.X - CardW - 36, top = TopH - 64, avail = Size.Y - top - BottomH + 40;
            float s = Mathf.Min(areaW / (0.84f * ColonyMapArt.ArtSize.X), avail / ColonyMapArt.ArtSize.Y);
            var size = ColonyMapArt.ArtSize * s;
            return new Rect2(new Vector2(areaW / 2 - size.X / 2 + 18, top), size);
        }
    }

    /// Chamber centres: the painted chambers, in level order.
    public Vector2 Spot(int number)
    {
        var a = ColonyMapArt.Anchors[Mathf.Clamp(number, 1, ColonyMapArt.Anchors.Length) - 1];
        var r = ArtRect;
        return r.Position + new Vector2(a.X, a.Y) * r.Size;
    }

    static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", 10 => "X", 11 => "XI", _ => "XII" };

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
            // near-black tiles with a fine gold edge, like the rest of the command UI
            AddThemeStyleboxOverride("normal", UiTheme.Box(new Color(0.06f, 0.04f, 0.025f, 0.96f), new Color(0.5f, 0.36f, 0.18f), 1, 3, 4));
            AddThemeStyleboxOverride("hover", UiTheme.Box(new Color(0.1f, 0.065f, 0.035f, 0.98f), Emblem.Gold, 1, 3, 4));
            AddThemeStyleboxOverride("pressed", UiTheme.Box(new Color(0.12f, 0.08f, 0.04f, 1f), Emblem.Gold, 2, 3, 4));
        }
        public override void _Draw()
        {
            var c = new Vector2(Size.X / 2, 44);
            if (EmblemId >= 0) Emblem.Engraved(this, c, 30, EmblemId, Open);
            else Emblem.Chitin(this, c, 34, 22, Open ? Colour : Colour.Darkened(0.55f), Open);
            if (Chosen) DrawRect(new Rect2(4, 4, Size.X - 8, Size.Y - 8), Emblem.Gold, false, 2);
            var text = Open ? Label.ToUpperInvariant() : $"{Cost} STARS";
            var w = UiTheme.Title.GetStringSize(text, HorizontalAlignment.Left, -1, 12).X;
            DrawString(UiTheme.Title, new Vector2(Size.X / 2 - w / 2, 98), text, HorizontalAlignment.Left, -1, 12, Open ? new Color(0.95f, 0.82f, 0.58f) : new Color(0.6f, 0.45f, 0.3f));
        }
    }
}
