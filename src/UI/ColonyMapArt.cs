using Godot;

namespace Mandible;

/// The colony map's picture: a photoreal cut through the nest (assets/ui/colony-map.jpg, made from the approved
/// target in docs/art-targets) with one wandering shaft and its chambers. Level n is chamber `Anchors[n-1]`: the first
/// just under the entrance, the royal chamber at the bottom last. A shader fills chambers the colony has not dug yet
/// with soil, lights won ones fully, and makes the selected and the next chamber glow.
public partial class ColonyMapArt : Control
{
    public LevelMap Map;
    public static readonly Vector2 ArtSize = new(2048, 1158);

    /// Chamber centre and half-size on the picture, in 0..1 of its width and height, in level order.
    public static readonly Vector4[] Anchors =
    {
        new(0.309f, 0.127f, 0.036f, 0.046f),   // I    just under the entrance
        new(0.488f, 0.216f, 0.088f, 0.057f),
        new(0.209f, 0.332f, 0.078f, 0.050f),
        new(0.722f, 0.332f, 0.076f, 0.050f),
        new(0.347f, 0.398f, 0.069f, 0.050f),
        new(0.581f, 0.448f, 0.100f, 0.061f),
        new(0.838f, 0.492f, 0.069f, 0.044f),
        new(0.172f, 0.532f, 0.100f, 0.064f),
        new(0.400f, 0.570f, 0.053f, 0.040f),
        new(0.281f, 0.697f, 0.106f, 0.061f),
        new(0.731f, 0.719f, 0.063f, 0.050f),
        new(0.500f, 0.852f, 0.150f, 0.105f),   // XII  the royal chamber at the bottom
    };

    /// Every painted chamber, including two that are not levels (a small store by the shaft and an empty one).
    static readonly Vector4[] AllChambers = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(Anchors, new[]
    {
        new Vector4(0.381f, 0.293f, 0.035f, 0.035f), new Vector4(0.584f, 0.625f, 0.075f, 0.055f),
    }));

    ShaderMaterial _mat;
    float _t;

    public ColonyMapArt() { MouseFilter = MouseFilterEnum.Ignore; }

    public override void _Ready()
    {
        _mat = new ShaderMaterial { Shader = new Shader { Code = Code } };
        var c = new Godot.Collections.Array();
        foreach (var a in Anchors) c.Add(a);
        _mat.SetShaderParameter("ch", c);
        _mat.SetShaderParameter("count", Anchors.Length);
        // an undug chamber is filled with the soil beside it: pick a side that is plain earth, not another chamber
        var fill = new Godot.Collections.Array();
        foreach (var a in Anchors)
        {
            Vector2 best = new(a.Z * 2.25f, 0);
            foreach (var o in new[] { new Vector2(a.Z * 2.25f, 0), new Vector2(-a.Z * 2.25f, 0), new Vector2(0, -a.W * 2.4f), new Vector2(0, a.W * 2.4f) })
            {
                var q = new Vector2(a.X, a.Y) + o;
                if (q.X - a.Z < 0.01f || q.X + a.Z > 0.99f || q.Y - a.W < 0.08f || q.Y + a.W > 0.99f) continue;
                bool clear = true;
                foreach (var b in AllChambers)
                {
                    if (b == a) continue;
                    var d = (q - new Vector2(b.X, b.Y)) / (new Vector2(b.Z + a.Z, b.W + a.W) * 1.05f);
                    if (d.LengthSquared() < 1) { clear = false; break; }
                }
                if (clear) { best = o; break; }
            }
            fill.Add(best);
        }
        _mat.SetShaderParameter("fill", fill);
        Material = _mat;
        Refresh();
    }

    /// Re-reads which chambers are dug and won, and which is selected.
    public void Refresh()
    {
        if (_mat == null) return;
        var st = new float[Anchors.Length];
        for (int n = 1; n <= Anchors.Length && n <= Levels.Count; n++)
        {
            int id = Levels.IdAt(n);
            st[n - 1] = CampaignProgress.Completed(id) ? 2 : CampaignProgress.Unlocked(id) ? 1 : 0;
        }
        _mat.SetShaderParameter("st", st);
        _mat.SetShaderParameter("selected", Map.Selected - 1);
        int next = CampaignProgress.Next;
        _mat.SetShaderParameter("next", next > 0 ? Levels.NumberOf(next) - 1 : -1);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        _mat?.SetShaderParameter("t", _t);
    }

    public override void _Draw()
    {
        var r = Map.ArtRect;
        DrawTextureRect(NatureAssets.UiTexture("colony-map"), new Rect2(r.Position - Position, r.Size), false);
    }

    const string Code = @"
shader_type canvas_item;
uniform vec4 ch[12];
uniform vec2 fill[12];
uniform float st[12];
uniform int count = 12;
uniform int selected = -1;
uniform int next = -1;
uniform float t = 0.0;
void fragment() {
    vec4 c = texture(TEXTURE, UV);
    float undug = 0.0, open = 0.0, sel = 0.0, nxt = 0.0, seam = 0.0;
    vec2 fillFrom = UV;
    for (int i = 0; i < count; i++) {
        vec2 d = (UV - ch[i].xy) / ch[i].zw;
        float r = length(d);
        float m = 1.0 - smoothstep(0.82, 1.15, r);
        if (st[i] < 0.5) {
            if (m > undug) {
                // packed earth: the soil just beside the chamber, mirrored in so its strata line up
                fillFrom = UV + fill[i];
            }
            undug = max(undug, m);
            seam = max(seam, exp(-pow((r - 0.92) / 0.05, 2.0)));   // where the colony will dig, the soil is loosened
        }
        else if (st[i] < 1.5) open = max(open, m);
        if (i == selected) sel = max(sel, m);
        if (i == next) nxt = max(nxt, 1.0 - smoothstep(0.9, 1.6, r));
    }
    // undug: the cavity is still packed soil, the same earth as around it, unlit
    vec3 soil = texture(TEXTURE, fillFrom).rgb * 0.62;
    c.rgb = mix(c.rgb, soil, undug);
    c.rgb *= 1.0 - seam * 0.28;
    c.rgb *= 1.0 - open * 0.2;                                   // dug but not yet won: dimmer
    c.rgb *= 1.0 + sel * (0.22 + 0.08 * sin(t * 2.2));           // the selected chamber brightens
    c.rgb += vec3(1.0, 0.62, 0.25) * nxt * (1.0 - undug * 0.4) * (0.06 + 0.05 * sin(t * 2.8)); // the next one breathes
    COLOR = c;
}
";
}
