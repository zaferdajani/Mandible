using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The story told in cinematic panels: the opening (the collector's tube falls and breaks) before level 1, a beat
/// before every later level and an epilogue after the last.
public static class Comic
{
    public sealed class Panel { public string File, Caption; }

    const int Epilogue = 0;

    /// The panels shown before a level (by id), or after the last one (0: the epilogue).
    public static Panel[] For(int id) => _beats.TryGetValue(id, out var p) ? p : null;
    public static Panel[] Ending => _beats[Epilogue];

    public static IEnumerable<Panel> Panels { get { foreach (int id in new[] { 1, 5, 2, 6, 3, 7, 10, 8, 9, 4, 11, 12, Epilogue }) foreach (var p in _beats[id]) yield return p; } }

    // The art is cinematic, photoreal stills in the game's look, generated with Higgsfield from the game's own hero
    // renders and a gameplay frame as references (prompts and provenance: docs/story-art.md). Captions stay live text.
    static readonly Dictionary<int, Panel[]> _beats = new()
    {
        [1] = new[]
        {
            new Panel { File = "o1-tube", Caption = "The Glass Queen was carried in a collector's tube, with old Burr, his soldiers and a handful of workers." },
            new Panel { File = "o2-fall", Caption = "On a woodland path, the backpack pocket gaped open." },
            new Panel { File = "o3-break", Caption = "The glass burst on the stones." },
            new Panel { File = "o4-woods", Caption = "She led her colony out of the wreck and into the woods. The Giant walked on and never looked back." },
        },
        [5] = new[]
        {
            new Panel { File = "b-rain", Caption = "Rain came to the woods, and with it sweet honeydew. Across the alley ran a scent trail no ant of the tube had smelled before." },
        },
        [2] = new[]
        {
            new Panel { File = "b-scout", Caption = "From a pebble at the edge of the clearing, a red scout watched the black ants work. Ashling's mound was close." },
        },
        [6] = new[]
        {
            new Panel { File = "b-log", Caption = "In a rotting log at the clearing's edge, beetles and spiders had smelled the brood." },
        },
        [3] = new[]
        {
            new Panel { File = "b-cinder", Caption = "Across the clearing Queen Cinder raised her mound and sent her soldiers out." },
        },
        [7] = new[]
        {
            new Panel { File = "b-arrival", Caption = "Then the red ants came: hundreds of them, walking the black colony's own trails." },
            new Panel { File = "b-vetch", Caption = "In the bug country a scout found glass, and beside it, alive, old Vetch." },
        },
        [10] = new[]
        {
            new Panel { File = "b-hornet", Caption = "High in the woods hung a nest of paper, and in it something with wings: the Hornet Mother." },
        },
        [8] = new[]
        {
            new Panel { File = "b-drought", Caption = "Then the rain stopped." },
        },
        [9] = new[]
        {
            new Panel { File = "b-wings", Caption = "In the royal chamber a new caste hatched: ants with wings." },
        },
        [4] = new[]
        {
            new Panel { File = "b-princesses", Caption = "On a warm evening the Empress's princesses climbed to the top of her mound." },
        },
        [11] = new[]
        {
            new Panel { File = "b-march", Caption = "Burr gathered the army for one last march before winter." },
        },
        [12] = new[]
        {
            new Panel { File = "b-empress", Caption = "Beneath the last hill the Ember Empress waited." },
        },
        [Epilogue] = new[]
        {
            new Panel { File = "e-home", Caption = "The woods belonged to the colony from the glass." },
        },
    };
}

/// Shows the story panels one at a time on a dark cutscene stage, each with a subtitle-style caption; Skip closes it
/// at once. Landscape, warm palette, finger-sized buttons.
public partial class ComicStrip : Control
{
    public Comic.Panel[] Panels;
    public Action Done;
    public string Heading = "";
    int _shown = 1;
    float _t;
    readonly Dictionary<string, Texture2D> _tex = new();
    Button _next;

    public override void _Ready()
    {
        UiTheme.Place(this, 0, 0, 1, 1, 0, 0, 0, 0);
        MouseFilter = MouseFilterEnum.Stop;
        foreach (var p in Panels)
        {
            string path = $"res://assets/ui/comic/{p.File}.jpg";
            if (ResourceLoader.Exists(path)) _tex[p.File] = GD.Load<Texture2D>(path);
        }
        var skip = Button("Skip");
        UiTheme.Place(skip, 1, 0, 1, 0, -200, 24, -24, 82);
        skip.Pressed += Close;
        AddChild(skip);
        _next = Button(Panels.Length > 1 ? "Next" : "Continue");
        UiTheme.Place(_next, 1, 1, 1, 1, -260, -86, -24, -24);
        _next.Pressed += Advance;
        AddChild(_next);
    }

    Button Button(string text)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(150, 58) };
        b.AddThemeFontOverride("font", UiTheme.Title);
        b.AddThemeFontSizeOverride("font_size", 22);
        b.AddThemeStyleboxOverride("normal", UiTheme.Box(new Color(0.16f, 0.09f, 0.04f, 0.92f), new Color(0.78f, 0.58f, 0.28f), 1, 6, 8));
        b.AddThemeStyleboxOverride("hover", UiTheme.Box(new Color(0.3f, 0.17f, 0.07f, 0.95f), new Color(1f, 0.8f, 0.4f), 1, 6, 8));
        b.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.68f));
        return b;
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Advance();
    }

    public void Advance()
    {
        if (_shown < Panels.Length) { _shown++; _t = 0; if (_shown == Panels.Length) _next.Text = "Continue"; QueueRedraw(); return; }
        Close();
    }

    public void Close() { QueueFree(); Done?.Invoke(); }

    public override void _Process(double delta) { _t += (float)delta; if (_t < 1) QueueRedraw(); }

    public override void _Draw()
    {
        // a dark warm stage, like a cutscene letterbox
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.07f, 0.04f, 0.02f));
        var page = new Rect2(new Vector2(28, 24), Size - new Vector2(56, 120));
        if (Heading != "")
        {
            var hs = UiTheme.Title.GetStringSize(Heading, HorizontalAlignment.Left, -1, 26);
            DrawString(UiTheme.Title, new Vector2(Size.X / 2 - hs.X / 2, Size.Y - 44), Heading, HorizontalAlignment.Left, -1, 26, new Color(1f, 0.84f, 0.55f));
        }
        var cells = Layout(page.Grow(-22), Panels.Length);
        for (int i = 0; i < _shown; i++)
        {
            var r = cells[i];
            float k = i == _shown - 1 ? Mathf.Clamp(_t * 3, 0, 1) : 1;
            var a = new Color(1, 1, 1, k);
            if (_tex.TryGetValue(Panels[i].File, out var tex)) DrawTextureRect(tex, r, false, a);
            else DrawRect(r, new Color(0.3f, 0.18f, 0.08f, k));
            DrawRect(r.Grow(2), new Color(0.78f, 0.58f, 0.28f, 0.85f * k), false, 2);
            Caption(r, Panels[i].Caption, k);
        }
    }

    /// One, two or four panels to a page; the opening's four read like a comic page, single beats fill it.
    static Rect2[] Layout(Rect2 area, int n)
    {
        const float gap = 22;
        if (n <= 1) return new[] { Fit(area, 1.5f) };
        if (n == 2)
        {
            float w = (area.Size.X - gap) / 2;
            return new[] { Fit(new Rect2(area.Position, new Vector2(w, area.Size.Y)), 1.5f), Fit(new Rect2(area.Position + new Vector2(w + gap, 0), new Vector2(w, area.Size.Y)), 1.5f) };
        }
        var cells = new Rect2[n];
        int cols = 2, rows = (n + 1) / 2;
        float cw = (area.Size.X - gap) / cols, ch = (area.Size.Y - gap * (rows - 1)) / rows;
        for (int i = 0; i < n; i++)
            cells[i] = Fit(new Rect2(area.Position + new Vector2((i % cols) * (cw + gap), (i / cols) * (ch + gap)), new Vector2(cw, ch)), 1.5f);
        return cells;
    }

    static Rect2 Fit(Rect2 box, float aspect)
    {
        var size = box.Size.X / box.Size.Y > aspect ? new Vector2(box.Size.Y * aspect, box.Size.Y) : new Vector2(box.Size.X, box.Size.X / aspect);
        return new Rect2(box.Position + (box.Size - size) / 2, size);
    }

    void Caption(Rect2 r, string text, float k)
    {
        int fs = Settings.Mobile ? 22 : Mathf.Clamp((int)(r.Size.Y / 26), 15, 24);
        float maxW = r.Size.X * 0.62f;
        var font = UiTheme.Title; // Cinzel, in the game's gold, like an engraved caption card
        var lines = Wrap(font, text, fs, maxW - 24);
        float lh = fs * 1.25f;
        float w = 0; foreach (var l in lines) w = Mathf.Max(w, font.GetStringSize(l, HorizontalAlignment.Left, -1, fs).X);
        // the caption sits low on the panel like a film subtitle card
        var box = new Rect2(new Vector2(r.Position.X + 18, r.End.Y - lines.Count * lh - 36), new Vector2(w + 28, lines.Count * lh + 18));
        DrawRect(box, new Color(0.08f, 0.045f, 0.02f, 0.82f * k));
        DrawRect(box, new Color(0.78f, 0.58f, 0.28f, 0.9f * k), false, 1.5f);
        for (int i = 0; i < lines.Count; i++)
        {
            var at = box.Position + new Vector2(14, 9 + fs + i * lh - fs * 0.2f);
            DrawString(font, at + new Vector2(1, 1), lines[i], HorizontalAlignment.Left, -1, fs, new Color(0.03f, 0.015f, 0.005f, 0.8f * k));
            DrawString(font, at, lines[i], HorizontalAlignment.Left, -1, fs, new Color(1f, 0.8f, 0.42f, k));
        }
    }

    static List<string> Wrap(Font font, string text, int fs, float width)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' '))
        {
            var test = line == "" ? word : line + " " + word;
            if (font.GetStringSize(test, HorizontalAlignment.Left, -1, fs).X > width && line != "") { lines.Add(line); line = word; }
            else line = test;
        }
        if (line != "") lines.Add(line);
        return lines;
    }
}
