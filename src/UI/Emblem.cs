using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Drawn shapes for the level system: the colony emblems and the rating stars. Everything is vector-drawn so it
/// stays crisp at any UI scale and needs no textures.
public static class Emblem
{
    public static readonly Color Gold = new(1f, 0.8f, 0.32f);
    public static readonly Color Socket = new(0.25f, 0.18f, 0.11f);

    /// A five-pointed star; `filled` false draws an empty socket with a faint rim.
    public static void Star(CanvasItem ci, Vector2 c, float r, bool filled, float glow = 0)
    {
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = -Mathf.Pi / 2 + i * Mathf.Pi / 5;
            pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (i % 2 == 0 ? r : r * 0.45f);
        }
        if (glow > 0)
            for (int i = 0; i < 12; i++)
            {
                // a burst of thin rays rather than a disc: it reads as light on any background
                float a = i * Mathf.Tau / 12 + Mathf.Pi / 12;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                ci.DrawLine(c + d * r * 1.05f, c + d * r * (i % 2 == 0 ? 1.7f : 1.45f), new Color(1f, 0.85f, 0.45f, 0.55f * glow), Mathf.Max(1, r * 0.06f), true);
            }
        ci.DrawColoredPolygon(Offset(pts, new Vector2(0, r * 0.12f)), new Color(0, 0, 0, 0.45f));
        ci.DrawColoredPolygon(pts, filled ? Gold : Socket);
        if (filled)
        {
            // a lit upper facet
            var hi = new Vector2[] { pts[9].Lerp(c, 0.2f), pts[0], pts[1].Lerp(c, 0.2f), c };
            ci.DrawColoredPolygon(hi, new Color(1f, 0.95f, 0.7f, 0.55f));
        }
        var rim = new Vector2[11]; System.Array.Copy(pts, rim, 10); rim[10] = pts[0];
        ci.DrawPolyline(rim, filled ? new Color(0.55f, 0.32f, 0.08f) : new Color(0.55f, 0.42f, 0.26f, 0.8f), Mathf.Max(1, r * 0.09f), true);
    }

    static Vector2[] Offset(Vector2[] p, Vector2 d) { var q = new Vector2[p.Length]; for (int i = 0; i < p.Length; i++) q[i] = p[i] + d; return q; }

    /// The colony banner: a dark disc ringed in the colony colour with the emblem in the middle.
    public static void Banner(CanvasItem ci, Vector2 c, float r, int emblem, Color colour)
    {
        ci.DrawCircle(c + new Vector2(0, r * 0.08f), r * 1.04f, new Color(0, 0, 0, 0.45f));
        ci.DrawCircle(c, r, colour.Darkened(0.35f));
        ci.DrawCircle(c, r * 0.86f, new Color(0.13f, 0.085f, 0.05f));
        ci.DrawArc(c, r * 0.93f, 0, Mathf.Tau, 48, colour, r * 0.1f, true);
        Draw(ci, c, r * 0.62f, emblem, colour);
    }

    /// One emblem, centred on `c` within radius `r`.
    public static void Draw(CanvasItem ci, Vector2 c, float r, int emblem, Color colour)
    {
        var light = colour.Lightened(0.35f);
        float w = Mathf.Max(1.5f, r * 0.12f);
        switch (emblem)
        {
            case 1: // clover: four heart leaves and a stem
                ci.DrawLine(c, c + new Vector2(r * 0.35f, r * 0.95f), new Color(0.5f, 0.62f, 0.2f), w, true);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.Pi / 2 + Mathf.Pi / 4;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var side = new Vector2(-d.Y, d.X);
                    var leaf = c + d * r * 0.42f;
                    ci.DrawCircle(leaf + side * r * 0.16f, r * 0.27f, new Color(0.52f, 0.68f, 0.22f));
                    ci.DrawCircle(leaf - side * r * 0.16f, r * 0.27f, new Color(0.46f, 0.62f, 0.2f));
                }
                ci.DrawCircle(c, r * 0.12f, light);
                break;
            case 2: // raspberry: a cone of drupelets under a leaf crown
                {
                    var berry = new Color(0.78f, 0.2f, 0.25f);
                    for (int row = 0; row < 4; row++)
                        for (int k = 0; k <= 3 - row; k++)
                        {
                            float x = (k - (3 - row) * 0.5f) * r * 0.36f;
                            var p = c + new Vector2(x, -r * 0.3f + row * r * 0.32f);
                            ci.DrawCircle(p, r * 0.2f, berry.Darkened(0.1f * row));
                            ci.DrawCircle(p + new Vector2(-r * 0.06f, -r * 0.06f), r * 0.07f, new Color(1, 0.7f, 0.7f, 0.7f));
                        }
                    for (int i = -1; i <= 1; i++)
                        ci.DrawColoredPolygon(new[] { c + new Vector2(i * r * 0.3f, -r * 0.5f), c + new Vector2(i * r * 0.55f, -r * 0.92f), c + new Vector2(i * r * 0.1f, -r * 0.62f) }, new Color(0.45f, 0.6f, 0.2f));
                }
                break;
            case 3: // stag crest: two curved antlers with tines
                for (int s = -1; s <= 1; s += 2)
                {
                    var pts = new List<Vector2>();
                    for (int i = 0; i <= 8; i++)
                    {
                        float t = i / 8f;
                        pts.Add(c + new Vector2(s * (r * 0.15f + Mathf.Sin(t * 1.9f) * r * 0.62f), r * 0.75f - t * r * 1.55f));
                    }
                    ci.DrawPolyline(pts.ToArray(), light, w * 1.4f, true);
                    ci.DrawLine(pts[4], pts[4] + new Vector2(-s * r * 0.32f, -r * 0.14f), light, w, true);
                    ci.DrawLine(pts[6], pts[6] + new Vector2(-s * r * 0.26f, -r * 0.2f), light, w, true);
                }
                ci.DrawCircle(c + new Vector2(0, r * 0.62f), r * 0.2f, colour);
                break;
            case 4: // crown
                {
                    var pts = new[]
                    {
                        c + new Vector2(-r * 0.8f, r * 0.45f), c + new Vector2(-r * 0.85f, -r * 0.45f), c + new Vector2(-r * 0.42f, -r * 0.05f),
                        c + new Vector2(0, -r * 0.7f), c + new Vector2(r * 0.42f, -r * 0.05f), c + new Vector2(r * 0.85f, -r * 0.45f), c + new Vector2(r * 0.8f, r * 0.45f),
                    };
                    ci.DrawColoredPolygon(pts, colour);
                    ci.DrawRect(new Rect2(c + new Vector2(-r * 0.8f, r * 0.3f), new Vector2(r * 1.6f, r * 0.28f)), colour.Darkened(0.25f));
                    foreach (var tip in new[] { pts[1], pts[3], pts[5] }) ci.DrawCircle(tip, r * 0.12f, light);
                    ci.DrawCircle(c + new Vector2(0, r * 0.18f), r * 0.13f, new Color(0.8f, 0.2f, 0.2f));
                }
                break;
            default: // mandibles: two curved jaws closing on a seed
                for (int s = -1; s <= 1; s += 2)
                {
                    var outer = new List<Vector2>();
                    for (int i = 0; i <= 10; i++)
                    {
                        float t = i / 10f;
                        float a = Mathf.Pi * (0.62f + 0.62f * t);
                        outer.Add(c + new Vector2(s * -Mathf.Cos(a) * r * 0.9f, -Mathf.Sin(a) * r * 0.9f + r * 0.1f));
                    }
                    for (int i = 10; i >= 0; i--)
                    {
                        float t = i / 10f;
                        float a = Mathf.Pi * (0.62f + 0.62f * t);
                        outer.Add(c + new Vector2(s * -Mathf.Cos(a) * r * (0.62f + 0.12f * t), -Mathf.Sin(a) * r * (0.62f + 0.12f * t) + r * 0.1f));
                    }
                    ci.DrawColoredPolygon(outer.ToArray(), colour);
                }
                ci.DrawCircle(c + new Vector2(0, r * 0.12f), r * 0.2f, light);
                break;
        }
    }
}

/// A row of rating stars with an optional caption on each: the briefing's goals, the objective card and the end card.
public partial class StarList : Control
{
    public readonly List<(bool on, string text)> Rows = new();
    public float Size_ = 13;
    public int FontSize = 15;
    public bool Centered;

    public StarList() { MouseFilter = MouseFilterEnum.Ignore; }

    public void Set(IEnumerable<(bool, string)> rows)
    {
        Rows.Clear(); Rows.AddRange(rows);
        CustomMinimumSize = new Vector2(CustomMinimumSize.X, Rows.Count * (FontSize + 11));
        QueueRedraw();
    }

    public override void _Draw()
    {
        var font = UiTheme.Bold;
        float lh = FontSize + 11;
        float width = 0;
        foreach (var (_, text) in Rows) width = Mathf.Max(width, font.GetStringSize(text, HorizontalAlignment.Left, -1, FontSize).X);
        float x0 = Centered ? Mathf.Max(0, (Size.X - width - Size_ * 2.6f) / 2) : 0;
        for (int i = 0; i < Rows.Count; i++)
        {
            var (on, text) = Rows[i];
            var c = new Vector2(x0 + Size_, i * lh + lh / 2);
            Emblem.Star(this, c, Size_, on);
            DrawString(font, new Vector2(x0 + Size_ * 2.6f + 1, c.Y + FontSize * 0.36f + 1), text, HorizontalAlignment.Left, -1, FontSize, new Color(0, 0, 0, 0.6f));
            DrawString(font, new Vector2(x0 + Size_ * 2.6f, c.Y + FontSize * 0.36f), text, HorizontalAlignment.Left, -1, FontSize, on ? UiTheme.Ink : UiTheme.Dim);
        }
    }
}
