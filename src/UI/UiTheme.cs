using Godot;

namespace Mandible;

public static class UiTheme
{
    public static readonly Color Ink = new(0.95f, 0.9f, 0.8f);
    public static readonly Color Dim = new(0.72f, 0.66f, 0.56f);
    public static readonly Color Gold = new(0.92f, 0.70f, 0.32f);
    public static readonly Color Bronze = new(0.55f, 0.38f, 0.18f);
    public static readonly Color PanelBg = new(0.07f, 0.055f, 0.04f, 0.88f);
    static Font _title, _body, _bodyBold;

    public static Font Title => _title ??= Variation("res://assets/fonts/Cinzel.ttf", 700);
    public static Font Body => _body ??= Variation("res://assets/fonts/Inter.ttf", 450);
    public static Font Bold => _bodyBold ??= Variation("res://assets/fonts/Inter.ttf", 650);

    static Font Variation(string path, int weight)
    {
        if (!ResourceLoader.Exists(path)) return ThemeDB.FallbackFont;
        var file = GD.Load<FontFile>(path);
        var v = new FontVariation { BaseFont = file };
        var ts = TextServerManager.GetPrimaryInterface();
        var d = new Godot.Collections.Dictionary { { ts.NameToTag("wght"), weight } };
        v.VariationOpentype = d;
        return v;
    }

    public static StyleBoxFlat Box(Color bg, Color border, int bw = 1, int radius = 6, int pad = 10)
    {
        var s = new StyleBoxFlat
        {
            BgColor = bg, BorderColor = border, CornerDetail = 6,
            ShadowColor = new Color(0, 0, 0, 0.45f), ShadowSize = 6,
        };
        s.SetBorderWidthAll(bw);
        s.SetCornerRadiusAll(radius);
        s.SetContentMarginAll(pad);
        return s;
    }

    public static Theme Build()
    {
        var t = new Theme { DefaultFont = Body, DefaultFontSize = 17 };
        var border = new Color(Bronze, 0.9f);
        t.SetStylebox("panel", "PanelContainer", Box(PanelBg, border));
        t.SetStylebox("panel", "Panel", Box(PanelBg, border));
        var normal = Box(new Color(0.13f, 0.1f, 0.07f, 0.95f), border, 1, 5, 8);
        var hover = Box(new Color(0.22f, 0.16f, 0.09f, 0.98f), Gold, 1, 5, 8);
        var pressed = Box(new Color(0.32f, 0.22f, 0.1f, 1f), Gold, 2, 5, 8);
        var disabled = Box(new Color(0.08f, 0.07f, 0.06f, 0.8f), new Color(0.3f, 0.25f, 0.2f), 1, 5, 8);
        foreach (var (k, v) in new (string, StyleBox)[] { ("normal", normal), ("hover", hover), ("pressed", pressed), ("disabled", disabled), ("focus", new StyleBoxEmpty()) })
            t.SetStylebox(k, "Button", v);
        t.SetColor("font_color", "Button", Ink);
        t.SetColor("font_hover_color", "Button", new Color(1, 0.92f, 0.7f));
        t.SetColor("font_pressed_color", "Button", Gold);
        t.SetColor("font_disabled_color", "Button", new Color(0.5f, 0.45f, 0.4f));
        t.SetFont("font", "Button", Bold);
        t.SetColor("font_color", "Label", Ink);
        t.SetColor("font_shadow_color", "Label", new Color(0, 0, 0, 0.7f));
        t.SetConstant("shadow_offset_x", "Label", 1);
        t.SetConstant("shadow_offset_y", "Label", 1);
        var tip = Box(new Color(0.05f, 0.04f, 0.03f, 0.97f), Gold, 1, 4, 10);
        t.SetStylebox("panel", "TooltipPanel", tip);
        t.SetColor("font_color", "TooltipLabel", Ink);
        t.SetFontSize("font_size", "TooltipLabel", 15);
        t.SetStylebox("background", "ProgressBar", Box(new Color(0.05f, 0.04f, 0.03f, 0.9f), new Color(0.3f, 0.22f, 0.12f), 1, 3, 0));
        t.SetStylebox("fill", "ProgressBar", Box(Gold, Gold, 0, 3, 0));
        var field = Box(new Color(0.03f, 0.025f, 0.02f, 0.95f), new Color(Bronze, 0.9f), 1, 4, 8);
        var fieldFocus = Box(new Color(0.05f, 0.04f, 0.03f, 1f), Gold, 1, 4, 8);
        t.SetStylebox("normal", "LineEdit", field);
        t.SetStylebox("focus", "LineEdit", fieldFocus);
        t.SetColor("font_color", "LineEdit", Ink);
        t.SetColor("font_placeholder_color", "LineEdit", new Color(Dim, 0.6f));
        t.SetColor("caret_color", "LineEdit", Gold);
        t.SetStylebox("panel", "ItemList", field);
        t.SetColor("font_color", "ItemList", Ink);
        t.SetColor("font_selected_color", "ItemList", Gold);
        t.SetConstant("separation", "HBoxContainer", 8);
        t.SetConstant("separation", "VBoxContainer", 8);
        t.SetConstant("h_separation", "GridContainer", 6);
        t.SetConstant("v_separation", "GridContainer", 6);
        return t;
    }

    public static Label Text(string s, int size = 17, Font font = null, Color? color = null)
    {
        var l = new Label { Text = s };
        l.AddThemeFontSizeOverride("font_size", size);
        if (font != null) l.AddThemeFontOverride("font", font);
        if (color.HasValue) l.AddThemeColorOverride("font_color", color.Value);
        return l;
    }

    public static T Place<T>(T c, float al, float at, float ar, float ab, float ol, float ot, float or, float ob) where T : Control
    {
        c.AnchorLeft = al; c.AnchorTop = at; c.AnchorRight = ar; c.AnchorBottom = ab;
        c.OffsetLeft = ol; c.OffsetTop = ot; c.OffsetRight = or; c.OffsetBottom = ob;
        return c;
    }
}

/// Icons for the colony chambers: crown, wings, mandibles, chitin shield, silk cutters.
public partial class ResearchGlyph : Control
{
    public Upgrade Kind;

    public override void _Draw()
    {
        var c = Size / 2;
        float u = Mathf.Min(Size.X, Size.Y) / 10f;
        var col = UiTheme.Gold;
        float lw = Mathf.Max(1.5f, u * 0.35f);
        switch (Kind)
        {
            case Upgrade.Queen:
                DrawColoredPolygon(new[] { c + new Vector2(-u * 3.4f, u * 2f), c + new Vector2(-u * 3.4f, -u * 1.6f), c + new Vector2(-u * 1.7f, 0), c + new Vector2(0, -u * 2.8f), c + new Vector2(u * 1.7f, 0), c + new Vector2(u * 3.4f, -u * 1.6f), c + new Vector2(u * 3.4f, u * 2f) }, col);
                DrawCircle(c + new Vector2(0, -u * 3.2f), u * 0.55f, col);
                DrawCircle(c + new Vector2(-u * 3.4f, -u * 2f), u * 0.5f, col);
                DrawCircle(c + new Vector2(u * 3.4f, -u * 2f), u * 0.5f, col);
                break;
            case Upgrade.Wings:
                for (int s = -1; s <= 1; s += 2)
                {
                    DrawColoredPolygon(new[] { c, c + new Vector2(s * u * 4.2f, -u * 3f), c + new Vector2(s * u * 4.6f, -u * 1.4f), c + new Vector2(s * u * 0.8f, u * 0.4f) }, new Color(col, 0.85f));
                    DrawColoredPolygon(new[] { c + new Vector2(0, u * 0.4f), c + new Vector2(s * u * 3.6f, u * 1.2f), c + new Vector2(s * u * 3.2f, u * 2.6f), c + new Vector2(s * u * 0.6f, u * 1.2f) }, new Color(col, 0.6f));
                }
                break;
            case Upgrade.Mandibles:
                for (int s = -1; s <= 1; s += 2)
                    DrawPolyline(new[] { c + new Vector2(s * u * 1.2f, u * 3f), c + new Vector2(s * u * 3f, 0), c + new Vector2(s * u * 2.2f, -u * 2.6f), c + new Vector2(s * u * 0.3f, -u * 3.2f) }, col, lw * 1.6f, true);
                DrawCircle(c + new Vector2(0, u * 3.2f), u * 1.4f, col);
                break;
            case Upgrade.Chitin:
                DrawColoredPolygon(new[] { c + new Vector2(0, -u * 3.6f), c + new Vector2(u * 3f, -u * 2.4f), c + new Vector2(u * 2.6f, u * 1.4f), c + new Vector2(0, u * 3.6f), c + new Vector2(-u * 2.6f, u * 1.4f), c + new Vector2(-u * 3f, -u * 2.4f) }, col);
                DrawLine(c + new Vector2(-u * 2.4f, -u * 0.6f), c + new Vector2(u * 2.4f, -u * 0.6f), new Color(0.1f, 0.07f, 0.04f), lw);
                DrawLine(c + new Vector2(-u * 1.8f, u * 1.4f), c + new Vector2(u * 1.8f, u * 1.4f), new Color(0.1f, 0.07f, 0.04f), lw);
                break;
            default:
                // a web being cut
                for (int k = 0; k < 6; k++) DrawLine(c, c + MathX.Dir(k * Mathf.Tau / 6) * u * 3.6f, new Color(col, 0.7f), lw * 0.6f);
                DrawArc(c, u * 2.2f, 0, Mathf.Tau, 18, new Color(col, 0.7f), lw * 0.6f);
                DrawLine(c + new Vector2(-u * 3.5f, u * 3.5f), c + new Vector2(u * 3.5f, -u * 3.5f), col, lw * 1.6f);
                break;
        }
    }
}

/// Top-down ant silhouette used for icons.
public partial class AntGlyph : Control
{
    public AntType Type;
    public Color Tint = UiTheme.Gold;

    public override void _Draw()
    {
        var s = Size;
        float u = Mathf.Min(s.X, s.Y) / 10f;
        var c = new Vector2(s.X / 2, s.Y / 2 + u * 0.4f);
        float head = Type switch { AntType.Soldier => 1.35f, AntType.Major => 1.8f, _ => 1f };
        float gaster = Type == AntType.Queen ? 1.6f : Type == AntType.Gunner || Type == AntType.Matron ? 1.35f : 1f;
        if (Type == AntType.Warlord) head = 1.7f;
        var col = Tint;
        float lw = Mathf.Max(1.5f, u * 0.28f);
        if (Config.IsBug(Type))
        {
            bool spider = Type == AntType.Spider;
            col = Config.WildUi;
            int legs = spider ? 4 : 3;
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < legs; i++)
                {
                    float t = (i - (legs - 1) * 0.5f) / legs;
                    var hip = c + new Vector2(0, t * u * 2f - u * 0.5f);
                    var knee = hip + new Vector2(side * u * (spider ? 2.4f : 1.8f), t * u * 2.2f - u * 0.8f);
                    var foot = knee + new Vector2(side * u * (spider ? 1.2f : 0.8f), t * u * 1.6f + u * 1.4f);
                    DrawPolyline(new[] { hip, knee, foot }, col, lw, true);
                }
            if (spider)
            {
                DrawCircle(c + new Vector2(0, u * 1.6f), u * 1.6f, col);
                DrawCircle(c - new Vector2(0, u * 0.8f), u * 1.1f, col);
            }
            else
            {
                DrawSetTransform(c + new Vector2(0, u * 0.6f), 0, new Vector2(1, 1.45f));
                DrawCircle(Vector2.Zero, u * 1.7f, col);
                DrawSetTransform(Vector2.Zero, 0, Vector2.One);
                DrawCircle(c - new Vector2(0, u * 1.9f), u * 0.9f, col);
                if (Type == AntType.StagBeetle)
                    for (int side = -1; side <= 1; side += 2)
                        DrawPolyline(new[] { c + new Vector2(side * u * 0.5f, -u * 2.5f), c + new Vector2(side * u * 1.6f, -u * 4f), c + new Vector2(side * u * 0.4f, -u * 4.6f) }, col, lw * 1.5f, true);
            }
            return;
        }
        if (Type == AntType.Flyer)
            for (int side = -1; side <= 1; side += 2)
            {
                // two pairs of swept wings
                DrawColoredPolygon(new[] { c + new Vector2(side * u * 0.3f, -u * 0.3f), c + new Vector2(side * u * 4.2f, -u * 2.2f), c + new Vector2(side * u * 4.4f, -u * 1.2f), c + new Vector2(side * u * 0.5f, u * 0.2f) }, new Color(col, 0.45f));
                DrawColoredPolygon(new[] { c + new Vector2(side * u * 0.3f, u * 0.2f), c + new Vector2(side * u * 3.4f, u * 0.6f), c + new Vector2(side * u * 3.2f, u * 1.5f), c + new Vector2(side * u * 0.4f, u * 0.7f) }, new Color(col, 0.35f));
            }
        // legs
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 3; i++)
            {
                var hip = c + new Vector2(0, (i - 1) * u * 0.55f);
                var knee = hip + new Vector2(side * u * 1.5f, (i - 1) * u * 0.9f - u * 0.3f);
                var foot = knee + new Vector2(side * u * 0.9f, (i - 1) * u * 1.0f + u * 0.4f);
                DrawPolyline(new[] { hip, knee, foot }, col, lw, true);
            }
        // antennae
        var hc = c + new Vector2(0, -u * (1.7f + 0.35f * (head - 1)));
        for (int side = -1; side <= 1; side += 2)
            DrawPolyline(new[] { hc + new Vector2(side * u * 0.3f, -u * 0.4f), hc + new Vector2(side * u * 1.0f, -u * 1.5f), hc + new Vector2(side * u * 1.6f, -u * 2.4f) }, col, lw * 0.8f, true);
        DrawCircle(c + new Vector2(0, u * (2.0f + 0.6f * (gaster - 1))), u * 1.15f * gaster, col);
        DrawCircle(c + new Vector2(0, u * 0.75f), u * 0.35f, col);
        DrawCircle(c, u * 0.62f, col);
        DrawCircle(hc, u * 0.8f * head, col);
        if (Config.IsHero(Type))
            DrawColoredPolygon(new[] { hc + new Vector2(-u * 1.1f, -u * 1.0f * head), hc + new Vector2(-u * 1.1f, -u * 2.0f * head), hc + new Vector2(-u * 0.5f, -u * 1.5f * head), hc + new Vector2(0, -u * 2.3f * head), hc + new Vector2(u * 0.5f, -u * 1.5f * head), hc + new Vector2(u * 1.1f, -u * 2.0f * head), hc + new Vector2(u * 1.1f, -u * 1.0f * head) }, new Color(1f, 0.85f, 0.35f));
        if (Type != AntType.Worker && Type != AntType.Queen)
            for (int side = -1; side <= 1; side += 2)
                DrawPolyline(new[] { hc + new Vector2(side * u * 0.4f * head, -u * 0.6f * head), hc + new Vector2(side * u * 0.5f * head, -u * 1.3f * head), hc + new Vector2(0, -u * 1.5f * head) }, col, lw * 1.2f, true);
    }
}
