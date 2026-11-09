using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Level 1's straws (targets K1-K3): the Blender straws (tools/blender/straw_models.py, kind "straw") lying on the
/// shore, lifted to jaw height while a crew carries one, and laid side by side over the water as the bridge, floating a
/// little low in it. Rendering only: it reads Sim.Mission.Straws.
public sealed class StrawProps
{
    /// How thick a straw is drawn (world units): the bridge deck's walk height above the water (PondWorld.PondDeck).
    public const float Thick = 0.3f;
    readonly GameSession _g;
    readonly Node3D _root = new() { Name = "Straws" };
    readonly List<MeshInstance3D> _nodes = new();
    readonly List<Vector3> _lift = new();

    public StrawProps(GameSession g) { _g = g; g.AddChild(_root); }

    /// One straw mesh scaled to the sim's length, its length along local +X.
    public static MeshInstance3D Make(int variant)
    {
        var kind = NatureAssets.Kind("straw");
        if (kind.Count == 0) return null;
        var p = kind[variant % kind.Count];
        float s = Expedition.StrawLength / p.Size.X;
        return new MeshInstance3D { Mesh = p.Mesh, MaterialOverride = p.Material, Scale = Vector3.One * s };
    }

    public void Update(float dt)
    {
        var m = _g.Sim.Mission;
        if (m == null || m.Episode != 1) return;
        var hf = _g.World.HF;
        while (_nodes.Count < m.Straws.Count)
        {
            var n = Make(_nodes.Count + 1);
            if (n == null) return;
            _root.AddChild(n);
            _nodes.Add(n);
            _lift.Add(Vector3.Zero);
        }
        float k = 1 - Mathf.Exp(-dt * 18f);
        for (int i = 0; i < m.Straws.Count; i++)
        {
            var s = m.Straws[i];
            var n = _nodes[i];
            n.Visible = _g.Fog == null || _g.Fog.Explored(s.Pos) || s.Laid;
            Vector3 at;
            if (s.Laid)
                at = new Vector3(s.Pos.X, WorldBuilder.PondLevel - Thick * 0.35f, s.Pos.Y); // floating, a little low in the water
            else
            {
                // carried at jaw height while a crew holds it, otherwise lying in the grit
                float lift = s.HoldCount >= Expedition.StrawCrew ? 0.38f : s.HoldCount > 0 ? 0.12f : 0f;
                at = new Vector3(s.Pos.X, hf.Sample(s.Pos.X, s.Pos.Y) - 0.06f + lift, s.Pos.Y);
            }
            var target = new Transform3D(new Basis(Vector3.Up, -s.Heading).Scaled(n.Scale), at);
            n.GlobalTransform = n.IsInsideTree() && _lift[i] != Vector3.Zero ? n.GlobalTransform.InterpolateWith(target, k) : target;
            _lift[i] = at;
        }
    }
}
