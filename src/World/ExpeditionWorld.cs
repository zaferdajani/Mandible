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
        var dress = new NatureScatter.Dresser(w, rng);
        var blades = new List<Transform3D>();
        var dry = new List<float>();
        var husks = new List<Transform3D>();

        // `s` is the old ellipsoid radius: a scanned stone twice that across, sunk by `bury`
        void Stone(Vector2 p, float s, float bury) => dress.Stone(p, s * 2.1f, Mathf.Clamp(bury + 0.1f, 0.1f, 0.7f));
        void Clump(Vector2 c, float rad, float height, float dryness)
        {
            int n = (int)(rad * rad * rng.R(1.2f, 2.4f)) + 2;
            for (int i = 0; i < n; i++)
            {
                var o = rng.InDisk(rad);
                float h = height * rng.R(0.45f, 1.15f) * (1 - o.Length() / rad * 0.45f);
                dress.Sprig(c + o, h * 1.3f, Mathf.Clamp(dryness + rng.R(-0.25f, 0.25f), 0, 1));
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
        foreach (var h in husks) dress.Debris(dress.Bark, new Vector2(h.Origin.X, h.Origin.Z), h.Basis.Scale.Z * 1.2f, rng.R(0, Mathf.Tau));
        // twigs fallen across the banks, a few small leaves at different stages of decay
        for (int i = 0; i < 7; i++)
        {
            int side = rng.F() < 0.5f ? -1 : 1;
            var a = new Vector2(rng.R(42, 118), 256 + side * rng.R(7.5f, 11));
            dress.Debris(dress.Twigs, a, rng.R(2.5f, 8f), side > 0 ? rng.R(-0.5f, 0.5f) + Mathf.Pi / 2 : rng.R(-0.5f, 0.5f) - Mathf.Pi / 2); // along the bank, never across the path
        }
        dress.Commit();
        foreach (var (at, len, age) in new[] { (new Vector2(57, 266.5f), 3.2f, 0.85f), (new Vector2(91, 245.5f), 4.4f, 0.35f), (new Vector2(112, 265f), 2.6f, 0.6f) })
            SmallLeaf(w, at, len, rng.R(0, Mathf.Tau), age, rng.F());

        BrokenTube(w, rng);

        Spawn(w, rng, FoodKind.Seed, Expedition.Clearing + new Vector2(12, -9), 1000, 2.2f);
        Spawn(w, rng, FoodKind.Seed, Expedition.Clearing + new Vector2(15, 5), 800, 1.8f);
        Spawn(w, rng, FoodKind.Berry, Expedition.Clearing + new Vector2(9, 14), 700, 2);
        Spawn(w, rng, FoodKind.Sugar, Expedition.Clearing + new Vector2(-10, -12), 500, 1.4f);
        w.Food.Add(new FoodSource { Id = w.NextFoodId++, Kind = FoodKind.Corpse, Pos = Expedition.Clearing + new Vector2(16, -1), Amount = 900, Max = 900, Radius = 2.3f, Team = Config.Wild, AntType = AntType.Beetle, Heading = 0.7f });
    }

    /// A small fallen leaf draped on the ground, with the same shader (and decay tint) as the yard's big leaves.
    static void SmallLeaf(World w, Vector2 at, float len, float yaw, float age, float seed)
    {
        int sp = (int)(seed * 2.99f);
        var l = new LeafDef { P = at, Len = len, Wid = len * LeafAspect[sp], Yaw = yaw, Age = age, Seed = seed, Species = sp, Rows = 10, Cols = 6 };
        l.Verts = new Vector3[(l.Rows + 1) * (l.Cols + 1)];
        l.Uvs = new Vector2[l.Verts.Length];
        var rot = new Basis(Vector3.Up, yaw);
        for (int r = 0; r <= l.Rows; r++)
        {
            float v = (float)r / l.Rows, hw = l.Wid * 0.5f;
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
