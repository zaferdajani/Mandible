using System.Collections.Generic;
using Godot;

namespace Mandible;

/// PLACEHOLDER visuals for the mutation track, until the owner approves targets H1-H5 (docs/art-targets/
/// REVIEW-mutation.jpg) and the real assets replace them: a faint violet-green light that pulses where mutagen lies
/// (on the dead queen wasp's body, then in its carrier's jaws), the sixth-sense ripple over a source the colony feels
/// through the fog, and the light of the royal larva and its throbbing cocoon in the nursery. No geometry is made
/// here; the queen wasp's body is the game's own wasp mesh.
public sealed class MutagenGlow
{
    readonly GameSession _g;
    readonly Node3D _root = new() { Name = "MutagenGlow" };
    readonly Dictionary<int, (Node3D node, OmniLight3D light, MeshInstance3D body)> _sources = new();
    OmniLight3D _nursery;
    float _t;
    static readonly Color Glow = new(0.72f, 0.55f, 1f);
    static readonly Color Green = new(0.6f, 1f, 0.7f);

    public MutagenGlow(GameSession g) { _g = g; g.AddChild(_root); }

    // the sixth-sense ripple: a soft, thin, broken ring of warm violet light that swells and fades, never a hard disc
    const string RippleShader = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled;
uniform float t = 0.0;
uniform vec3 col : source_color = vec3(0.7, 0.55, 1.0);
void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float r = length(p);
    float a = atan(p.y, p.x);
    float band = exp(-pow((r - 0.85) * 14.0, 2.0));
    float broken = 0.55 + 0.45 * sin(a * 9.0 + t * 4.0) * sin(a * 4.0 - t * 2.0);
    ALBEDO = col;
    ALPHA = band * broken * (1.0 - t) * (1.0 - t) * 0.16;
}
";
    // the radiating core: a camera-facing halo of light (a hot heart, a soft corona and slow faint rays) over the gland,
    // sized and brightened by the core's strength so a weak core reads dim and a queen's core blazes. Light only: the
    // core's body (a faceted organic seed, target M2) waits for the owner's approval of docs/art-targets/REVIEW-mutation-core.jpg.
    const string HaloShader = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
uniform float t = 0.0;
uniform float strength = 1.0;
uniform vec3 col : source_color = vec3(0.72, 0.55, 1.0);
uniform vec3 hot : source_color = vec3(0.7, 1.0, 0.75);
void vertex() {
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
    MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(vec4(length(MODEL_MATRIX[0].xyz), 0, 0, 0), vec4(0, length(MODEL_MATRIX[1].xyz), 0, 0), vec4(0, 0, length(MODEL_MATRIX[2].xyz), 0), vec4(0, 0, 0, 1));
}
void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float r = length(p);
    float a = atan(p.y, p.x);
    float beat = 0.5 + 0.5 * sin(t * (2.0 + strength));
    float heart = exp(-r * r * 60.0) * (0.8 + 0.4 * beat);
    float corona = exp(-r * r * 7.0) * (0.25 + 0.2 * beat);
    float rays = pow(max(0.0, sin(a * 6.0 + t * 0.6) * sin(a * 3.0 - t * 0.4)), 6.0) * exp(-r * 3.2) * 0.35 * strength / 3.0;
    vec3 c = mix(col, hot, clamp(heart * 1.4 + 0.3 * sin(t * 0.9), 0.0, 1.0));
    ALBEDO = c;
    ALPHA = clamp((heart + corona + rays) * (0.35 + 0.22 * strength), 0.0, 1.0) * smoothstep(1.0, 0.7, r);
}
";
    readonly Dictionary<int, MeshInstance3D> _halos = new();
    MeshInstance3D Halo(int id)
    {
        if (_halos.TryGetValue(id, out var h)) return h;
        var mat = new ShaderMaterial { Shader = Shaders.Get(HaloShader), RenderPriority = 21 };
        h = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _root.AddChild(h);
        return _halos[id] = h;
    }

    /// How hard a core of this strength radiates (1 dim, 2 strong, 3 a queen's blaze).
    public static float Radiance(int strength) => strength switch { <= 1 => 0.55f, 2 => 1f, _ => 1.6f };

    readonly Dictionary<int, MeshInstance3D> _rings = new();
    MeshInstance3D Ring(int id)
    {
        if (_rings.TryGetValue(id, out var r)) return r;
        var mat = new ShaderMaterial { Shader = Shaders.Get(RippleShader), RenderPriority = 20 };
        r = new MeshInstance3D { Mesh = new PlaneMesh { Size = Vector2.One }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _root.AddChild(r);
        return _rings[id] = r;
    }

    public void Update(float dt)
    {
        var sim = _g.Sim;
        _t += dt;
        foreach (var m in sim.Mutagens)
        {
            // a stronger core beats faster and harder
            float pulse = 0.5f + 0.5f * Mathf.Sin(_t * (1.8f + 0.6f * m.Strength) + m.Id);
            float rad = Radiance(m.Strength);
            if (!_sources.TryGetValue(m.Id, out var s))
            {
                var node = new Node3D();
                var light = new OmniLight3D { OmniRange = 3.2f, LightEnergy = 0.8f, ShadowEnabled = false, LightColor = Glow, LightCullMask = ~MutagenProps.SkinLayer };
                node.AddChild(light);
                MeshInstance3D body = null;
                if (m.From is AntType.Wasp && !ResourceLoader.Exists("res://assets/mutation/queen_wasp/queen_wasp.gltf")) // the modelled wasp: MutagenProps
                {
                    body = new MeshInstance3D { Mesh = AntMesh.Build(AntType.Wasp), MaterialOverride = AntRenderer.AntMat(Shaders.Get(Shaders.Ant), Config.Wild, AntType.Wasp, true), Scale = Vector3.One * Config.Of(AntType.Wasp).Size * 1.6f };
                    node.AddChild(body);
                }
                _root.AddChild(node);
                _sources[m.Id] = s = (node, light, body);
            }
            bool seen = _g.Fog == null || _g.Fog.Visible(m.Pos) || m.Carried;
            s.node.Visible = !m.Home && seen;
            if (m.Home) continue;
            // never inside a mesh: a light at zero distance blows its neighbours out to white
            s.node.Position = (_g.Props?.GlandPoint(m.Id) ?? _g.World.HF.Point(m.Pos, 0.2f)) + Vector3.Up * (m.Free ? 0.9f : 1.3f);
            s.node.Rotation = new Vector3(0, -m.Heading, 0);
            if (s.body != null) s.body.Visible = !m.Free;
            // a subtle radiance, never a lamp: it breathes slowly between violet and green
            s.light.LightColor = Glow.Lerp(Green, 0.5f + 0.5f * Mathf.Sin(_t * 0.9f));
            // in the web it lights the silk around her; in a carrier's jaws only the ants' heads and the soil beside it
            // from play zoom the sun drowns a small light: the pool on the soil grows as the camera pulls back
            float zoom = Mathf.Clamp((_g.Camera.Distance - 14f) / 46f, 0, 1);
            s.light.OmniRange = (m.Free ? 2.4f : 2.6f) * (0.75f + 0.25f * rad) * (1 + 0.9f * zoom);
            s.light.LightEnergy = (0.3f + 0.4f * pulse) * rad * (1 + 2.5f * zoom);
            // the core's halo: on the body it marks the ruptured patch; carved out it radiates in the carrier's jaws,
            // and it grows with the camera's distance so the core stays readable from play zoom
            var halo = Halo(m.Id);
            halo.Visible = s.node.Visible;
            halo.Position = s.node.Position + Vector3.Down * (m.Free ? 0.55f : 0.9f);
            halo.Scale = Vector3.One * (m.Free ? 1.1f : 0.8f) * (0.7f + 0.3f * rad) * (1 + 1.6f * zoom);
            var hm = (ShaderMaterial)halo.MaterialOverride;
            hm.SetShaderParameter("t", _t + m.Id);
            hm.SetShaderParameter("strength", (float)m.Strength); // a violet-green pool on the soil round the carrier: the gland is findable from play zoom
            // the sixth sense: a faint ring of disturbed air spreads from a source the colony feels, even through the fog
            // from play zoom a carried gland keeps a faint, slow ripple under it, so the player can follow it across the yard
            bool sensed = (!m.Carried || zoom > 0.5f) && sim.Senses(Local.Me, m);
            var ring = Ring(m.Id);
            ring.Visible = sensed;
            if (sensed)
            {
                float k = (_t % 2.6f) / 2.6f;
                ring.Position = _g.World.HF.Point(m.Pos, 0.15f);
                ring.Scale = Vector3.One * (m.Carried ? 1.5f + 5f * k : 2f + 16f * k);
                ((ShaderMaterial)ring.MaterialOverride).SetShaderParameter("t", k);
            }
        }
        float pulseN = 0.5f + 0.5f * Mathf.Sin(_t * 2.4f);
        // the royal larva glows in its nursery; the cocoon throbs harder, faster as it nears bursting
        var c = sim.Col[Local.Me];
        var room = sim.Nursery(Local.Me);
        bool lit = room != null && c.LarvaStage >= 2;
        if (lit && _nursery == null) { _nursery = new OmniLight3D { OmniRange = 3.6f, ShadowEnabled = false }; _root.AddChild(_nursery); }
        if (_nursery == null) return;
        _nursery.Visible = lit && _g.NestView;
        if (!lit) return;
        _nursery.Position = _g.World.HF.Point(sim.NestEntrance(Local.Me)) + new Vector3(room.Offset.X, -NestGeometry.Depth + 1.8f, room.Offset.Y);
        float beat = c.LarvaStage == 3 ? 0.5f + 0.5f * Mathf.Sin(_t * (3f + 6f * (1 - c.CocoonT / Mathf.Max(1, c.CocoonTime)))) : pulseN;
        _nursery.LightColor = c.LarvaStage == 3 ? new Color(1f, 0.72f, 0.42f).Lerp(Glow, 0.3f) : Glow.Lerp(Green, 0.4f);
        _nursery.LightEnergy = (c.LarvaStage == 3 ? 0.6f : 0.2f) + beat * (c.LarvaStage == 3 ? 0.6f : 0.15f);
    }
}
