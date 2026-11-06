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
    float _t, _ripple;
    static readonly Color Glow = new(0.72f, 0.55f, 1f);
    static readonly Color Green = new(0.6f, 1f, 0.7f);

    public MutagenGlow(GameSession g) { _g = g; g.AddChild(_root); }

    public void Update(float dt)
    {
        var sim = _g.Sim;
        _t += dt;
        _ripple -= dt;
        float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 2.4f);
        foreach (var m in sim.Mutagens)
        {
            if (!_sources.TryGetValue(m.Id, out var s))
            {
                var node = new Node3D();
                var light = new OmniLight3D { OmniRange = 3.2f, LightEnergy = 0.8f, ShadowEnabled = false, LightColor = Glow };
                node.AddChild(light);
                MeshInstance3D body = null;
                if (m.From is AntType.Wasp)
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
            s.node.Position = _g.World.HF.Point(m.Pos, m.Carried ? 0.5f : 0.9f);
            s.node.Rotation = new Vector3(0, -m.Heading, 0);
            if (s.body != null) s.body.Visible = !m.Free;
            // a subtle radiance, never a lamp: it breathes slowly between violet and green
            s.light.LightColor = Glow.Lerp(Green, 0.5f + 0.5f * Mathf.Sin(_t * 0.9f));
            s.light.LightEnergy = 0.45f + 0.6f * pulse;
            // the sixth sense: a ripple over a source the colony feels, even through the fog
            if (_ripple <= 0 && sim.Senses(Local.Me, m)) _g.Effects.Marker(_g.World.HF.Point(m.Pos, 0.2f), Glow.Lerp(Green, 0.3f), 2.2f);
        }
        if (_ripple <= 0) _ripple = 2.2f;
        // the royal larva glows in its nursery; the cocoon throbs harder, faster as it nears bursting
        var c = sim.Col[Local.Me];
        var room = sim.Nursery(Local.Me);
        bool lit = room != null && c.LarvaStage >= 2;
        if (lit && _nursery == null) { _nursery = new OmniLight3D { OmniRange = 3.6f, ShadowEnabled = false }; _root.AddChild(_nursery); }
        if (_nursery == null) return;
        _nursery.Visible = lit && _g.NestView;
        if (!lit) return;
        _nursery.Position = _g.World.HF.Point(sim.NestEntrance(Local.Me)) + new Vector3(room.Offset.X, -NestGeometry.Depth + 0.7f, room.Offset.Y);
        float beat = c.LarvaStage == 3 ? 0.5f + 0.5f * Mathf.Sin(_t * (3f + 6f * (1 - c.CocoonT / Mathf.Max(1, c.CocoonTime)))) : pulse;
        _nursery.LightColor = c.LarvaStage == 3 ? new Color(1f, 0.72f, 0.42f).Lerp(Glow, 0.3f) : Glow.Lerp(Green, 0.4f);
        _nursery.LightEnergy = (c.LarvaStage == 3 ? 1.2f : 0.5f) + beat * (c.LarvaStage == 3 ? 1.1f : 0.4f);
    }
}
