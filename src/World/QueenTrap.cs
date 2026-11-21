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
    /// The running simulation (set by the session): the diggers are read from it so every piece leaves in an ant's jaws.
    public Sim Sim;
    sealed class Piece
    {
        public Node3D Node; public Vector3 From, To; public Basis Rest; public Vector3 Turn; public float At;
        public int Carrier = -1, Serial; public float Held; public bool Dropped; public Vector3 Scale = Vector3.One;
    }
    readonly List<Piece> _pieces = new();
    float _shown = -1;

    public void Add(Node3D node, Vector3 to, float at, Vector3 turn)
    {
        AddChild(node);
        _pieces.Add(new Piece { Node = node, From = node.Position, To = to, Rest = node.Basis, Turn = turn, At = at, Scale = node.Scale });
    }

    /// A digger at the mouth with free jaws (not already holding a piece), nearest the plug.
    int FreeDigger(Vector3 near)
    {
        if (Sim == null) return -1;
        int best = -1; float bd = 9f;
        for (int i = 0; i < Sim.High; i++)
        {
            ref var a = ref Sim.Ants[i];
            if (!a.Alive || a.Team != 0 || a.Dormant || a.Type == AntType.Queen || a.Layer != 0) continue;
            float d = a.Pos.DistanceSquaredTo(Expedition.DigSite);
            if (d > Expedition.DigRadius * Expedition.DigRadius) continue;
            bool busy = false;
            foreach (var p in _pieces) if (p.Carrier == i && !p.Dropped) { busy = true; break; }
            if (busy) continue;
            float dn = a.Pos.DistanceSquaredTo(new Vector2(near.X, near.Z));
            if (dn < bd) { bd = dn; best = i; }
        }
        return best;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        float d = _shown < 0 ? Dug : Mathf.MoveToward(_shown, Dug, dt * 0.5f);
        _shown = d;
        foreach (var p in _pieces)
        {
            if (p.Dropped) continue;
            float k = Mathf.SmoothStep(p.At, Mathf.Min(1f, p.At + 0.22f), d);
            if (k <= 0) continue;
            // the moment a piece comes loose, a digger at the mouth takes it in its jaws (owner, 2026-11-21: the ants
            // must really carry the plug and the sand away, not stand by while the pieces fly off on their own)
            if (p.Carrier < 0 && Sim != null) { p.Carrier = FreeDigger(p.Node.Position); if (p.Carrier >= 0) p.Serial = Sim.Ants[p.Carrier].Serial; }
            bool held = p.Carrier >= 0 && Sim != null && Sim.Valid(p.Carrier, p.Serial);
            if (held)
            {
                ref var a = ref Sim.Ants[p.Carrier];
                p.Held += dt;
                // in the jaws: shrunk to a mouthful an ant can lift, just ahead of its head
                var head = a.Pos + MathX.Dir(a.Heading) * a.Size * 0.48f;
                var target = new Vector3(head.X, Sim.W.HF.Sample(head.X, head.Y) + a.Size * 0.32f, head.Y);
                p.Node.Position = p.Node.Position.Lerp(target, 1f - Mathf.Exp(-dt * 18f));
                p.Node.Scale = p.Node.Scale.Lerp(p.Scale * 0.42f, 1f - Mathf.Exp(-dt * 6f));
                p.Node.Basis = Basis.FromEuler(p.Turn * Mathf.Min(1f, p.Held * 0.6f)) * p.Rest.Orthonormalized() * Basis.FromScale(p.Node.Scale);
                // set down at the edge of the dig, once it has been carried out of the mouth (or the dig has moved on)
                bool out_ = a.Pos.DistanceTo(Expedition.Mouth) > 2.0f && p.Held > 1.2f;
                if (out_ || k >= 1f || p.Held > 9f)
                {
                    p.Dropped = true;
                    var at = new Vector2(p.Node.Position.X, p.Node.Position.Z);
                    p.Node.Position = new Vector3(at.X, Sim.W.HF.Sample(at.X, at.Y) + 0.05f, at.Y);
                }
                continue;
            }
            // no free jaws (or no simulation, as in a still capture): the old drag aside
            p.Node.Position = p.From.Lerp(p.To, k) + Vector3.Up * (Mathf.Sin(k * Mathf.Pi) * 0.3f);
            p.Node.Basis = Basis.FromEuler(p.Turn * k) * p.Rest;
            if (k >= 1f) p.Dropped = true;
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
