using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Level 1's opening: what stops the mouth of the broken tube. The queen and her workers are alive inside the glass,
/// shut in behind a cotton plug jammed in the opening and the sand packed against it; nothing lies on them. Each piece
/// is dragged aside as the soldiers dig (Expedition.Dug): the sand clumps first, the cotton plug last, and they stay
/// where they were dropped as spoil around the mouth. Render only; the pieces are the tube's own cotton and damp
/// substrate (BrokenTube.cs).
public partial class QueenTrap : Node3D
{
    /// How far the mouth is dug open, 0..1; the session copies it from the simulation every frame.
    public float Dug;
    readonly List<(Node3D node, Vector3 from, Vector3 to, Basis rest, Vector3 turn, float at)> _pieces = new();
    float _shown = -1;

    public void Add(Node3D node, Vector3 to, float at, Vector3 turn)
    {
        AddChild(node);
        _pieces.Add((node, node.Position, to, node.Basis, turn, at));
    }

    public override void _Process(double delta)
    {
        float d = _shown < 0 ? Dug : Mathf.MoveToward(_shown, Dug, (float)delta * 0.5f);
        if (Mathf.IsEqualApprox(d, _shown)) return;
        _shown = d;
        foreach (var (node, from, to, rest, turn, at) in _pieces)
        {
            // each piece is worked loose, lifted over the others and dropped clear
            float k = Mathf.SmoothStep(at, Mathf.Min(1f, at + 0.22f), d);
            node.Position = from.Lerp(to, k) + Vector3.Up * (Mathf.Sin(k * Mathf.Pi) * 0.3f);
            node.Basis = Basis.FromEuler(turn * k) * rest;
        }
    }
}

public static partial class WorldBuilder
{
    /// The stopped mouth of the tube, and the sand that spilled with every soldier the fall threw down the alley.
    /// `tube` is the closed half's node (its x runs along the tube from the closed end), `inner` its bore radius.
    static void BuildQueenTrap(World w, ShaderMaterial cotton, StandardMaterial3D damp, Node3D tube, float inner)
    {
        var hf = w.HF;
        var rng = new Rng(48256); // its own sequence: the yard's dressing and food are laid exactly as before
        var trap = new QueenTrap { Name = "QueenTrap" };
        w.Root.AddChild(trap);
        w.Trap = trap;
        float Ground(Vector2 p) => hf.Sample(p.X, p.Y);
        var dir = Expedition.TubeDir;
        var across = new Vector2(-dir.Y, dir.X);
        Vector3 Axis(float along, float up = 0, float side = 0) => tube.Transform * new Vector3(along, up, side); // a point in the tube's own frame
        Vector3 OnGround(Vector2 p, float lift) => new(p.X, Ground(p) + lift, p.Y);

        // the cotton plug, jammed in the mouth just inside the break: it fills the bore, and is the last thing to come away
        {
            var plug = new MeshInstance3D { Mesh = Fluff(rng, inner * 0.99f, 1.9f), MaterialOverride = cotton, Position = Axis(Expedition.TubeLength - 1.2f) };
            plug.Basis = tube.Basis;
            var drop = Expedition.Mouth + dir * 4.6f - across * 5.2f;
            trap.Add(plug, OnGround(drop, inner * 0.8f), 0.68f, new Vector3(0.3f, 1.9f, 0.5f));
        }
        // damp sand packed into the jagged opening and banked against it outside, the big clumps at the bottom
        for (int i = 0; i < 30; i++)
        {
            bool big = i < 12;
            float r = big ? rng.R(0.45f, 0.8f) : rng.R(0.18f, 0.38f);
            Vector3 from;
            if (i % 3 == 0)
            {
                // wedged in the opening itself, across the bore
                float a = rng.R(0, Mathf.Tau), rad = Mathf.Sqrt(rng.F()) * (inner - r * 0.45f);
                from = Axis(Expedition.TubeLength - rng.R(-0.2f, 0.5f), Mathf.Cos(a) * rad, Mathf.Sin(a) * rad);
            }
            else
            {
                // the bank outside: highest against the glass, thinning into the alley
                float outward = rng.R(0.2f, 2.3f), side = rng.R(-1.7f, 1.7f);
                var p = Expedition.Mouth + dir * outward + across * side;
                float crown = Mathf.Max(0, 1f - outward / 2.4f) * Mathf.Max(0, 1f - Mathf.Abs(side) / 1.9f);
                from = OnGround(p, r * 0.2f + crown * (big ? 1.5f : 2.3f));
            }
            var node = new MeshInstance3D { Mesh = PebbleMesh(120 + i), MaterialOverride = damp, Position = from, Scale = new Vector3(r, r * 0.62f, r * rng.R(0.8f, 1.25f)) };
            node.Basis = new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * node.Basis;
            // dragged out into the alley and dropped to either side, clear of the way out
            float sgn = i % 2 == 0 ? 1f : -1f;
            var q = Expedition.Mouth + dir * rng.R(2.4f, 6.2f) + across * sgn * rng.R(3.0f, 5.0f);
            trap.Add(node, OnGround(q, r * 0.18f), Mathf.Min(0.6f, rng.R(0f, 0.45f) + (big ? 0.12f : 0f)), new Vector3(rng.R(-1, 1), rng.R(-2, 2), rng.R(-1, 1)));
        }

        // every stunned soldier lies in a little of the sand it was thrown out with: grains, and a clump or two
        var spill = new NatureScatter.Dresser(w, rng);
        foreach (var c in Expedition.SoldierSpots)
        {
            for (int i = 0; i < 22; i++)
            {
                var p = c + rng.InDisk(1.5f);
                spill.Place(spill.Pick(spill.Stones), p, rng.R(0.08f, 0.24f), 0.3f, 0.5f, Basis.Identity, rng.R(0, Mathf.Tau), rng.R(0.9f, 1.05f));
            }
            for (int i = 0; i < 2; i++)
            {
                var p = c + DMath.Dir(rng.R(0, Mathf.Tau)) * rng.R(0.5f, 0.9f);
                float r = rng.R(0.16f, 0.3f);
                w.Root.AddChild(new MeshInstance3D { Mesh = PebbleMesh(160 + i), MaterialOverride = damp, Position = OnGround(p, r * 0.15f), Rotation = new Vector3(0, rng.R(0, 3), 0), Scale = new Vector3(r, r * 0.6f, r * rng.R(0.8f, 1.3f)) });
            }
        }
        spill.Commit();
    }
}
