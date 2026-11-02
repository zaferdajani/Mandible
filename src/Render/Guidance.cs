using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Where to go and what it is worth. With fighters, heroes or workers selected, a gold arrow stands over the best
/// remains in reach, with a label saying what eating them does for the selected ant and what hauling them home gives
/// the colony; with no remains about, it points at the nearest bug lair to hunt. The minimap marks the same spot.
/// Rendering only. The level-system campaign can point it anywhere with Show / Clear.
public sealed class Guidance
{
    readonly Sprite3D _arrow;
    readonly Batch _ring;
    readonly Label3D _title, _line;
    readonly ShaderMaterial _ringMat;
    readonly Heightfield _hf;
    float _t;
    Vector2? _forced; string _forcedTitle, _forcedLine;

    /// The spot the guide points at this frame (the minimap draws it too), or null.
    public Vector2? Target { get; private set; }

    public Guidance(Node3D parent, Heightfield hf)
    {
        _hf = hf;
        var root = new Node3D { Name = "Guidance" };
        parent.AddChild(root);
        // a gold arrow, drawn once and always facing the camera, so it reads as an arrow from any angle
        _arrow = new Sprite3D
        {
            Texture = ArrowTexture(), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.00055f * (Settings.Mobile ? 1.35f : 1f),
            NoDepthTest = true, RenderPriority = 12, Shaded = false, Visible = false, Offset = new Vector2(0, 64),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
        };
        root.AddChild(_arrow);
        _ringMat = new ShaderMaterial { Shader = Shaders.Get(Shaders.Ring), RenderPriority = 1 }; // after the soil (drawn in the transparent pass)
        _ring = new Batch(root, new PlaneMesh { Size = Vector2.One }, _ringMat, false, "GuidanceRing");
        Label3D Label(int size, Font font, Color color)
        {
            var l = new Label3D
            {
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, FixedSize = true, FontSize = size,
                OutlineSize = size / 4, Font = font, Modulate = color, OutlineModulate = new Color(0.02f, 0.015f, 0.01f, 0.92f),
                RenderPriority = 12, OutlineRenderPriority = 11, Visible = false, PixelSize = 0.0006f,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            root.AddChild(l);
            return l;
        }
        bool phone = Settings.Mobile;
        _title = Label(phone ? 80 : 56, UiTheme.Title, UiTheme.Gold);
        _line = Label(phone ? 56 : 38, UiTheme.Bold ?? UiTheme.Body, new Color(1f, 0.93f, 0.8f));
    }

    /// The arrow: a gold shaft and head with a dark rim, lit from above, on a transparent field.
    static ImageTexture ArrowTexture()
    {
        const int W = 96, H = 132;
        var img = Image.CreateEmpty(W, H, true, Image.Format.Rgba8);
        bool Inside(float x, float y)
        {
            if (x > 33 && x < 63 && y > 2 && y < 66) return true; // the shaft
            if (y < 58 || y > 128) return false; // the head, a triangle pointing down
            float half = 44f * (128f - y) / 70f;
            return Mathf.Abs(x - 48f) < half;
        }
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                bool core = Inside(x, y);
                bool rim = !core && (Inside(x - 4, y) || Inside(x + 4, y) || Inside(x, y - 4) || Inside(x, y + 4) || Inside(x - 3, y - 3) || Inside(x + 3, y + 3) || Inside(x - 3, y + 3) || Inside(x + 3, y - 3));
                if (core)
                {
                    float shade = 1f - 0.35f * y / H + 0.12f * Mathf.Cos((x - 48f) / 20f);
                    var c = new Color(1f, 0.74f, 0.26f) * shade;
                    img.SetPixel(x, y, new Color(c.R, c.G, c.B, 1f));
                }
                else img.SetPixel(x, y, rim ? new Color(0.12f, 0.07f, 0.02f, 0.92f) : new Color(0, 0, 0, 0));
            }
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }

    /// Points the guide at a spot of the campaign's choosing (a food site, a hunting ground), until Clear.
    public void Show(Vector2 at, string title, string line) { _forced = at; _forcedTitle = title; _forcedLine = line; }
    public void Clear() { _forced = null; }

    public void Update(Sim sim, List<int> selection, System.Func<Vector2, bool> visible, float dt, Vector3 camPos = default)
    {
        _t += dt;
        string title = null, line = "";
        Vector2 at = default;
        float height = 2f;
        if (_forced.HasValue) { at = _forced.Value; title = _forcedTitle; line = _forcedLine; }
        else if (sim.CarrionRules) Pick(sim, selection, visible, ref at, ref title, ref line, ref height);
        Target = title != null ? at : null;
        bool on = title != null;
        _arrow.Visible = _title.Visible = _line.Visible = on;
        _ring.Begin();
        if (!on) { _ring.End(); return; }
        float ground = _hf.Sample(at.X, at.Y);
        float bob = Mathf.Sin(_t * 3.2f) * 0.35f;
        float top = ground + height + 3.2f + bob;
        _arrow.Position = new Vector3(at.X, ground + height + 0.6f, at.Y);
        _arrow.Offset = new Vector2(0, 70 + 14 * Mathf.Sin(_t * 3.2f)); // bobs above the spot, its point toward it
        float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 4f);
        _ringMat.SetShaderParameter("color", new Color(1f, 0.72f, 0.25f, 0.55f + 0.35f * pulse));
        _ring.Add(Basis.FromScale(Vector3.One * (height * 1.6f + 2.4f + pulse * 0.8f)), new Vector3(at.X, ground + 0.12f, at.Y), new Color(1, 0, 0, 0));
        _ring.End();
        _title.Text = title;
        _title.Position = _line.Position = new Vector3(at.X, top + 2.6f, at.Y);
        _title.Offset = new Vector2(0, line.Contains('\n') ? 74 : 52);
        _line.Text = line;
        _line.Visible = line.Length > 0;
    }

    /// The best thing for the selection to do: the remains with the most in them for its eaters (or, for workers, to
    /// haul), else the nearest lair of bugs to hunt.
    static void Pick(Sim sim, List<int> selection, System.Func<Vector2, bool> visible, ref Vector2 at, ref string title, ref string line, ref float height)
    {
        int eater = -1, workers = 0; float need = 0; var centre = Vector2.Zero; int n = 0; bool cannibal = false;
        foreach (int id in selection)
        {
            ref var a = ref sim.Ants[id];
            if (!a.Alive || a.Team != Local.Me) continue;
            centre += a.Pos; n++;
            if (a.Type == AntType.Worker) workers++;
            if (!sim.Eats(a)) continue;
            float nd = sim.EvolutionNeed(a);
            if (eater < 0 || nd > need) { eater = id; need = nd; }
            cannibal |= sim.Cannibal(a);
        }
        if (n == 0 || (eater < 0 && workers == 0)) return;
        centre /= n;
        FoodSource best = null; float bestScore = 0;
        foreach (var f in sim.W.Food)
        {
            if (f.Kind != FoodKind.Corpse || !f.Alive || f.Layer != 0) continue;
            bool own = f.Team == Local.Me;
            if (own && !(cannibal && eater >= 0)) continue;
            if (!own && !visible(f.Pos)) continue;
            float score = Sim.RemainsValue(f) / (12f + f.Pos.DistanceTo(centre));
            if (score > bestScore) { bestScore = score; best = f; }
        }
        if (best != null)
        {
            at = best.Pos;
            height = best.Radius * 1.4f;
            float organic = Sim.RemainsValue(best);
            title = $"{Config.Of(best.AntType).Name.ToUpperInvariant()} REMAINS";
            if (eater >= 0 && need > 0)
            {
                ref var e = ref sim.Ants[eater];
                bool hero = Config.IsHero(e.Type);
                float share = hero ? organic / Mathf.Max(1f, need + 0.01f) : organic / Config.StageCost(e.Stage + 1);
                string gain = organic >= need ? (hero ? "A FULL LEVEL" : "THE NEXT STAGE") : $"+{share * 100:0}% {(hero ? "OF A LEVEL" : "EVOLUTION")}";
                line = best.Team == Local.Me ? $"DEVOUR THE FALLEN · {gain}" : $"FEAST · {gain}";
                if (best.Team != Local.Me) line += $"\nOR HAUL HOME · +{organic:0} ORGANIC";
            }
            else line = $"HAUL HOME · +{organic:0} ORGANIC";
            return;
        }
        // nothing on the ground: the nearest lair whose bugs still stand
        Camp lair = null; float bestD = float.MaxValue; AntType bug = AntType.Beetle;
        foreach (var camp in sim.W.Camps)
        {
            AntType kind = AntType.Beetle; bool alive = false;
            for (int i = 0; i < sim.High && !alive; i++)
            {
                ref var b = ref sim.Ants[i];
                if (b.Alive && b.Team == Config.Wild && !b.Boss && b.Home.DistanceSquaredTo(camp.Pos) < 1f) { alive = true; kind = b.Type; }
            }
            if (!alive) continue;
            float d = camp.Pos.DistanceTo(centre);
            if (d < bestD) { bestD = d; lair = camp; bug = kind; }
        }
        if (lair == null || eater < 0) return;
        at = lair.Pos;
        height = 2.5f;
        title = $"{Config.Of(bug).Name.ToUpperInvariant()} LAIR";
        line = "HUNT HERE\nTHEN EAT THE REMAINS";
    }
}
