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
    readonly List<Node3D> _nodes = new();
    readonly List<Vector3> _lift = new();

    public StrawProps(GameSession g) { _g = g; g.AddChild(_root); }

    /// One straw mesh scaled to the sim's length, its length along local +X.
    /// The generated straw scan (kind "straw"), its length along the holder's +X. The scan is a thick reed: it is
    /// thinned across to about 1:20, a grass culm's build. Each variant rolls it about its length and may turn it end for end, so
    /// the split sheath and the frayed tip never sit the same way twice, and wears its own shade of dead grass.
    public static Node3D Make(int variant, bool carried = true)
    {
        // the carried and bridged straws are the clean culms; the one with a leaf still on lies loose on the shore
        var kind = NatureAssets.Kind("straw").FindAll(k => !carried || Mathf.Max(k.Size.Y, k.Size.Z) < k.Size.X * 0.2f);
        if (kind.Count == 0) return null;
        var p = kind[variant % kind.Count];
        float s = Expedition.StrawLength / p.Size.X;
        float thin = Mathf.Clamp(Thick / (Mathf.Min(p.Size.Y, p.Size.Z) * s), 0.25f, 1f); // every scan to the same girth
        var mat = (StandardMaterial3D)p.Material.Duplicate();
        mat.AlbedoColor = Tones[variant % Tones.Length];
        var local = new Basis(Vector3.Up, variant % 2 == 1 ? Mathf.Pi : 0) * new Basis(Vector3.Right, variant * 1.9f) * Basis.FromScale(new Vector3(s, s * thin, s * thin));
        var holder = new Node3D();
        holder.AddChild(new MeshInstance3D { Mesh = p.Mesh, MaterialOverride = mat, Basis = local, Position = new Vector3(0, p.Size.Y * s * thin * 0.5f, 0) - local * new Vector3(0, p.Size.Y * 0.5f, 0) }); // rolled about its own axis, resting on the ground
        return holder;
    }

    static readonly Color[] Tones =
    {
        new(1f, 0.97f, 0.9f), new(0.9f, 0.82f, 0.68f), new(1.04f, 0.98f, 0.86f), new(0.8f, 0.7f, 0.55f),
        new(0.96f, 0.9f, 0.76f), new(0.86f, 0.78f, 0.62f), new(1.02f, 0.92f, 0.74f),
    };

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
                float lift = s.HoldCount >= Expedition.StrawCrew ? 0.22f : s.HoldCount > 0 ? 0.08f : 0f;
                at = new Vector3(s.Pos.X, hf.Sample(s.Pos.X, s.Pos.Y) - 0.06f + lift, s.Pos.Y);
            }
            var target = new Transform3D(new Basis(Vector3.Up, -s.Heading), at);
            n.GlobalTransform = n.IsInsideTree() && _lift[i] != Vector3.Zero ? n.GlobalTransform.InterpolateWith(target, k) : target;
            _lift[i] = at;
        }
    }
}
