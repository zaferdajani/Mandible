using System.Collections.Generic;
using Godot;

namespace Mandible;

public static partial class WorldBuilder
{
    /// Level 1's grass alley. Its banks delimit the same corridor the movement constraint uses, but they are
    /// dressed the way a woodland path edge looks: stones of every size in clusters with gaps between, some
    /// half-buried, clumps of grass of uneven height, twigs, husks, crumbs and a few fallen leaves at different
    /// stages of decay. At the start lies the collector's broken tube and the sand that spilled out of it.
    static void BuildExpedition(World w, Rng rng)
    {
        var hf = w.HF;
        var noise = new FastNoiseLite { Seed = 4711, Frequency = 0.09f };       // where the bank is crowded or bare
        var stoneMat = Mat(w, Shaders.Pebble);
        var stones = new List<(Transform3D xf, Color cd)>[3];
        for (int v = 0; v < 3; v++) stones[v] = new List<(Transform3D, Color)>();
        var blades = new List<Transform3D>();
        var dry = new List<float>();
        var husks = new List<Transform3D>();

        void Stone(Vector2 p, float s, float bury)
        {
            var basis = new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Right, rng.R(-0.35f, 0.35f)) * new Basis(Vector3.Forward, rng.R(-0.2f, 0.2f))
                * Basis.FromScale(new Vector3(s * rng.R(0.85f, 1.25f), s * rng.R(0.55f, 1f), s * rng.R(0.8f, 1.2f)));
            var cd = new Color(rng.F(), rng.F() < 0.12f ? 1 : 0, rng.F() < 0.3f ? rng.R(0.2f, 0.7f) : 0, rng.F());
            stones[rng.I(0, 3)].Add((new Transform3D(basis, new Vector3(p.X, hf.Sample(p.X, p.Y) + s * (0.25f - bury), p.Y)), cd));
        }
        void Clump(Vector2 c, float rad, float height, float dryness)
        {
            int n = (int)(rad * rad * rng.R(3f, 6f)) + 3;
            for (int i = 0; i < n; i++)
            {
                var o = rng.InDisk(rad);
                float h = height * rng.R(0.45f, 1.15f) * (1 - o.Length() / rad * 0.45f);
                var basis = new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Right, rng.R(-0.35f, 0.35f)) * Basis.FromScale(new Vector3(rng.R(0.5f, 0.9f), h, 1));
                var p = c + o;
                blades.Add(new Transform3D(basis, new Vector3(p.X, hf.Sample(p.X, p.Y) - 0.1f, p.Y)));
                dry.Add(Mathf.Clamp(dryness + rng.R(-0.25f, 0.25f), 0, 1));
            }
        }

        // the two banks, walked in uneven steps rather than a fixed rhythm
        foreach (int side in new[] { -1, 1 })
        {
            float x = 37 + rng.R(0, 2);
            while (x < 124)
            {
                float crowd = noise.GetNoise2D(x, side * 40) * 0.5f + 0.5f;       // 0 bare .. 1 crowded
                float edge = 256 + side * (7.4f + rng.R(0, 2.2f) + crowd * 1.5f);
                var at = new Vector2(x, edge);
                if (crowd > 0.32f)
                {
                    // a cluster: one or two big stones with smaller ones tucked around them
                    int big = crowd > 0.7f ? 2 : 1;
                    for (int b = 0; b < big; b++) Stone(at + new Vector2(rng.R(-1.5f, 1.5f), side * rng.R(0.5f, 2.5f)), rng.R(1.1f, 2.4f), rng.R(0.1f, 0.5f));
                    int small = rng.I(2, 7);
                    for (int s = 0; s < small; s++) Stone(at + new Vector2(rng.R(-3, 3), side * rng.R(-0.8f, 3.5f)), rng.R(0.2f, 0.8f), rng.R(0, 0.6f));
                }
                else if (rng.F() < 0.5f) Stone(at + new Vector2(0, side * rng.R(0, 2)), rng.R(0.3f, 1f), rng.R(0.2f, 0.7f)); // a lone, half-sunk stone
                // grass grows where the bank is quiet, thick in some stretches and thin in others
                if (rng.F() < 0.35f + crowd * 0.4f)
                    Clump(at + new Vector2(rng.R(-2, 2), side * rng.R(1.5f, 4.5f)), rng.R(0.6f, 2.2f), rng.R(3f, 11f) * (rng.F() < 0.2f ? 1.5f : 1), rng.R(0, 0.8f));
                x += rng.R(1.2f, 5.5f) * (crowd < 0.32f ? 1.6f : 1);
            }
        }
        // grit and seed husks on the path itself, more along the edges
        for (int i = 0; i < 240; i++)
        {
            var p = new Vector2(rng.R(38, 122), 256 + rng.R(-7, 7) * Mathf.Sqrt(rng.F()) * (rng.F() < 0.5f ? 1 : -1));
            if (rng.F() < 0.65f) Stone(p, rng.R(0.06f, 0.22f), 0.1f);
            else
            {
                float s = rng.R(0.12f, 0.3f);
                husks.Add(new Transform3D(new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Right, rng.R(-0.4f, 0.4f)) * Basis.FromScale(new Vector3(s, s * 0.45f, s * 1.8f)), new Vector3(p.X, hf.Sample(p.X, p.Y) + s * 0.2f, p.Y)));
            }
        }
        for (int v = 0; v < 3; v++)
        {
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = PebbleMesh(31 + v), InstanceCount = stones[v].Count };
            for (int i = 0; i < stones[v].Count; i++) { mm.SetInstanceTransform(i, stones[v][i].xf); mm.SetInstanceCustomData(i, stones[v][i].cd); }
            w.Root.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = stoneMat, Name = "AlleyStones" + v });
        }
        var gm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = BladeMesh(), InstanceCount = blades.Count };
        for (int i = 0; i < blades.Count; i++) { gm.SetInstanceTransform(i, blades[i]); gm.SetInstanceCustomData(i, new Color(dry[i], 0, 0, 0)); }
        w.Root.AddChild(new MultiMeshInstance3D { Multimesh = gm, MaterialOverride = Mat(w, Shaders.Grass), Name = "AlleyGrass" });
        var hm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new SphereMesh { Radius = 0.5f, Height = 1, RadialSegments = 8, Rings = 4 }, InstanceCount = husks.Count };
        for (int i = 0; i < husks.Count; i++) hm.SetInstanceTransform(i, husks[i]);
        w.Root.AddChild(new MultiMeshInstance3D { Multimesh = hm, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.55f, 0.3f), Roughness = 0.8f }, Name = "Husks" });

        // twigs fallen across the banks, a few small leaves at different stages of decay
        var twigMat = Mat(w, Shaders.Twig);
        for (int i = 0; i < 7; i++)
        {
            int side = rng.F() < 0.5f ? -1 : 1;
            var a = new Vector2(rng.R(42, 118), 256 + side * rng.R(6.5f, 11));
            float yaw = rng.R(0, Mathf.Tau), len = rng.R(2.5f, 8f), r = rng.R(0.1f, 0.28f);
            var b = a + MathX.Dir(yaw) * len;
            if (Mathf.Abs(b.Y - 256) < 6.5f) b = new Vector2(b.X, 256 + side * 6.5f); // twigs lie on the bank, never across the path
            var k = new MeshKit();
            var white = new Color(1, 1, 1);
            var mid = a.Lerp(b, 0.55f) + new Vector2(rng.R(-0.4f, 0.4f), rng.R(-0.4f, 0.4f));
            Vector3 P(Vector2 q, float lift) => new(q.X, hf.Sample(q.X, q.Y) + r + lift, q.Y);
            k.Tube(P(a, 0), P(mid, 0.05f), r, r * 0.85f, 7, white, white, Vector2.Zero);
            k.Tube(P(mid, 0.05f), P(b, 0), r * 0.85f, r * 0.55f, 7, white, white, Vector2.Zero, true);
            if (rng.F() < 0.6f) k.Tube(P(mid, 0.05f), P(mid + MathX.Dir(yaw + 0.8f) * len * 0.3f, 0.2f), r * 0.5f, r * 0.2f, 5, white, white, Vector2.Zero);
            w.Root.AddChild(new MeshInstance3D { Mesh = k.Commit(), MaterialOverride = twigMat });
        }
        foreach (var (at, len, age) in new[] { (new Vector2(57, 266.5f), 3.2f, 0.85f), (new Vector2(91, 245.5f), 4.4f, 0.35f), (new Vector2(112, 265f), 2.6f, 0.6f) })
            SmallLeaf(w, at, len, rng.R(0, Mathf.Tau), age, rng.F());

        BrokenTube(w, rng);

        Spawn(w, rng, FoodKind.Seed, Expedition.Clearing + new Vector2(12, -9), 1000, 2.2f);
        Spawn(w, rng, FoodKind.Seed, Expedition.Clearing + new Vector2(15, 5), 800, 1.8f);
        Spawn(w, rng, FoodKind.Berry, Expedition.Clearing + new Vector2(9, 14), 700, 2);
        Spawn(w, rng, FoodKind.Sugar, Expedition.Clearing + new Vector2(-10, -12), 500, 1.4f);
        // No ready-made carcass here: the only beetle is the live one in the alley, and its remains are the protein.
        // A dead beetle lying in the clearing read as the enemy and could not be attacked (owner playtest).
    }

    /// A small fallen leaf draped on the ground, with the same shader (and decay tint) as the yard's big leaves.
    static void SmallLeaf(World w, Vector2 at, float len, float yaw, float age, float seed)
    {
        var l = new LeafDef { P = at, Len = len, Wid = len * 0.5f, Yaw = yaw, Age = age, Seed = seed, Rows = 10, Cols = 6 };
        l.Verts = new Vector3[(l.Rows + 1) * (l.Cols + 1)];
        l.Uvs = new Vector2[l.Verts.Length];
        var rot = new Basis(Vector3.Up, yaw);
        for (int r = 0; r <= l.Rows; r++)
        {
            float v = (float)r / l.Rows, hw = Mathf.Max(0.02f, LeafHalfWidth(v, l.Wid));
            for (int c = 0; c <= l.Cols; c++)
            {
                float u = (float)c / l.Cols;
                var wp = rot * new Vector3((u - 0.5f) * 2 * hw, 0, (v - 0.45f) * len);
                float curl = 0.35f * Mathf.Pow(Mathf.Abs(u - 0.5f) * 2, 2) * (age + 0.3f) + 0.4f * Mathf.Pow(v, 3) * age;
                l.Verts[r * (l.Cols + 1) + c] = new Vector3(at.X + wp.X, w.HF.Sample(at.X + wp.X, at.Y + wp.Z) + 0.06f + curl, at.Y + wp.Z);
                l.Uvs[r * (l.Cols + 1) + c] = new Vector2(u, v);
            }
        }
        BuildLeaf(w, l);
    }
}
