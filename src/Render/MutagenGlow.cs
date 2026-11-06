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
        float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 2.4f);
        foreach (var m in sim.Mutagens)
        {
            if (!_sources.TryGetValue(m.Id, out var s))
            {
                var node = new Node3D();
                var light = new OmniLight3D { OmniRange = 3.2f, LightEnergy = 0.8f, ShadowEnabled = false, LightColor = Glow };
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
            s.light.OmniRange = m.Free ? 2.4f : 2.6f;
            s.light.LightEnergy = (m.Free ? 0.3f : 0.3f) + (m.Free ? 0.4f : 0.4f) * pulse; // a violet-green pool on the soil round the carrier: the gland is findable from play zoom
            // the sixth sense: a faint ring of disturbed air spreads from a source the colony feels, even through the fog
            bool sensed = !m.Carried && sim.Senses(Local.Me, m);
            var ring = Ring(m.Id);
            ring.Visible = sensed;
            if (sensed)
            {
                float k = (_t % 2.6f) / 2.6f;
                ring.Position = _g.World.HF.Point(m.Pos, 0.15f);
                ring.Scale = Vector3.One * (2f + 16f * k);
                ((ShaderMaterial)ring.MaterialOverride).SetShaderParameter("t", k);
            }
        }
        // the royal larva glows in its nursery; the cocoon throbs harder, faster as it nears bursting
        var c = sim.Col[Local.Me];
        var room = sim.Nursery(Local.Me);
        bool lit = room != null && c.LarvaStage >= 2;
        if (lit && _nursery == null) { _nursery = new OmniLight3D { OmniRange = 3.6f, ShadowEnabled = false }; _root.AddChild(_nursery); }
        if (_nursery == null) return;
        _nursery.Visible = lit && _g.NestView;
        if (!lit) return;
        _nursery.Position = _g.World.HF.Point(sim.NestEntrance(Local.Me)) + new Vector3(room.Offset.X, -NestGeometry.Depth + 1.8f, room.Offset.Y);
        float beat = c.LarvaStage == 3 ? 0.5f + 0.5f * Mathf.Sin(_t * (3f + 6f * (1 - c.CocoonT / Mathf.Max(1, c.CocoonTime)))) : pulse;
        _nursery.LightColor = c.LarvaStage == 3 ? new Color(1f, 0.72f, 0.42f).Lerp(Glow, 0.3f) : Glow.Lerp(Green, 0.4f);
        _nursery.LightEnergy = (c.LarvaStage == 3 ? 0.6f : 0.2f) + beat * (c.LarvaStage == 3 ? 0.6f : 0.15f);
    }
}
