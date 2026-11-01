using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Generates a 180°-symmetric backyard battlefield at ant scale.
public static class WorldBuilder
{
    const float S = Config.MapSize;
    static readonly Vector2 Center = new(S * 0.5f, S * 0.5f);
    static Vector2 Mirror(Vector2 p) => new(S - p.X, S - p.Y);

    public static World Build(Node3D parent, uint seed)
    {
        var w = new World { Root = new Node3D { Name = "World" } };
        parent.AddChild(w.Root);
        var rng = new Rng(seed);                 // gameplay: rocks, food, lairs (must match on every machine)
        var deco = new Rng(seed ^ 0x5EEDu);      // decoration only: leaves, twigs, grass, litter
        var hf = w.HF = new Heightfield(S, 1f);

        BuildTextures(w);
        BaseHeights(hf, seed);

        w.Nests[Config.Player] = new Nest { Team = Config.Player, Pos = NestA, Radius = 9 };
        w.Nests[Config.Enemy] = new Nest { Team = Config.Enemy, Pos = Mirror(NestA), Radius = 13 };
        // rocks are placed before the soil settles so the ground can sag around them (the rock itself sits on the sagged soil)
        var rocks = PlaceRocks(w, rng);
        RockDips(hf, rocks);
        Crater(hf, w.Nests[0].Pos);
        Thatch(hf, w.Nests[1].Pos);
        hf.SnapshotGround();

        w.IndexObstacles();
        var leaves = PlaceLeaves(w, deco, rocks);
        var twigs = PlaceTwigs(w, deco);
        var ao = ComputeAO(hf, rocks);

        BuildTerrainMesh(w, ao);
        BuildSkirt(w);
        foreach (var l in leaves) BuildLeaf(w, l);
        foreach (var t in twigs) BuildTwig(w, t);
        foreach (var r in rocks) BuildRock(w, r);
        BuildGrass(w, deco);
        BuildNestDressing(w, deco);
        PlaceFood(w, rng);
        PlaceCamps(w, rng);
        w.IndexObstacles();
        BuildLitter(w, deco);
        BuildMinimapBase(w, leaves, rocks);
        return w;
    }

    // ---------------------------------------------------------------- textures
    static void BuildTextures(World w)
    {
        ImageTexture Tex(FastNoiseLite n, int size, bool normal, float bump = 6f)
        {
            var img = n.GetSeamlessImage(size, size, false, false, 0.1f, true);
            if (normal)
            {
                img.Convert(Image.Format.Rgba8);
                img.BumpMapToNormalMap(bump);
            }
            img.GenerateMipmaps();
            return ImageTexture.CreateFromImage(img);
        }
        var a = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.008f, FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 5, Seed = 11 };
        var b = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Frequency = 0.035f, FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 3, Seed = 23, CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance2Sub };
        var bn = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Frequency = 0.05f, FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 2, Seed = 41, CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance };
        // sand grains: rounded cells (distance2 - distance peaks at each cell's centre) over a scatter of finer lumps
        var grains = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Frequency = 0.07f, Seed = 57, CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance2Sub, CellularJitter = 1.0f, FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 2, FractalGain = 0.35f };
        w.NoiseA = Tex(a, 512, false);
        w.NoiseB = Tex(b, 512, false);
        w.NormalA = Tex(a, 512, true, 16f);   // broad undulations: the smooth fbm needs a strong bump scale to register at all
        w.NormalB = Tex(bn, 512, true, 7f);   // clods and pits
        w.NormalC = Tex(grains, 512, true, 9f); // the grains themselves
    }

    /// The sun's direction and what faces it, shared by the baked shading (minimap) and the live light.
    static Vector3 ToSun => -(Basis.FromEuler(GameSession.SunAngles * (Mathf.Pi / 180f)) * Vector3.Forward).Normalized();

    static readonly Vector2 NestA = new(62, 258);

    static ShaderMaterial Mat(World w, string code, bool fow = true)
    {
        var m = new ShaderMaterial { Shader = Shaders.Get(code) };
        m.SetShaderParameter("noise_a", w.NoiseA);
        m.SetShaderParameter("noise_b", w.NoiseB);
        m.SetShaderParameter("normal_a", w.NormalA);
        m.SetShaderParameter("normal_b", w.NormalB);
        m.SetShaderParameter("map_size", S);
        if (fow) w.RegisterFow(m);
        return m;
    }

    // ---------------------------------------------------------------- heights
    /// The lie of the land. Only rendering reads these heights (the sim walks a flat plane and samples them for
    /// drawing), but they are still built from the same FastNoiseLite calls on every platform so all players see
    /// one yard. Four layers: slow swells, dune ridges, shallow cellular hollows (where the pebbles collect) and
    /// small lumps; a worn hollow runs between the nests through the middle, and the ground is held nearly flat
    /// around each nest so the galleries, pane and crater still fit.
    static void BaseHeights(Heightfield hf, uint seed)
    {
        var n1 = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.011f, Seed = (int)seed, FractalOctaves = 3 };
        var n2 = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.06f, Seed = (int)seed + 5 };
        var dune = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.026f, Seed = (int)seed + 11, FractalType = FastNoiseLite.FractalTypeEnum.Ridged, FractalOctaves = 2, FractalGain = 0.45f };
        var dip = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Frequency = 0.04f, Seed = (int)seed + 17, CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance, CellularJitter = 0.9f };
        var fine = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.13f, Seed = (int)seed + 23 };
        var nestB = Mirror(NestA);
        float Relief(float wx, float wz)
        {
            float h = 2.2f * n1.GetNoise2D(wx, wz) + 0.35f * n2.GetNoise2D(wx, wz);
            // ridged noise: sharp crests, broad troughs, like wind-set sand
            float r = 1.7f * (1f - Mathf.Abs(dune.GetNoise2D(wx, wz))) - 0.85f;
            // shallow bowls around each cell point: pebble beds and damp hollows
            float d01 = dip.GetNoise2D(wx, wz) * 0.5f + 0.5f;
            float bowl = -1.1f * (1f - Mathf.SmoothStep(0.05f, 0.6f, d01));
            float lumps = 0.3f * fine.GetNoise2D(wx, wz);
            // the worn hollow of the main path: nest to nest through the middle of the yard
            var p = new Vector2(wx, wz);
            float along = Mathf.Clamp((p - NestA).Dot(nestB - NestA) / (nestB - NestA).LengthSquared(), 0, 1);
            float offPath = p.DistanceTo(NestA + (nestB - NestA) * along);
            float path = -0.6f * (1f - Mathf.SmoothStep(2.5f, 9f, offPath));
            // flat ground around the nests
            float nd = Mathf.Min(p.DistanceTo(NestA), p.DistanceTo(nestB));
            float keep = Mathf.SmoothStep(13f, 26f, nd);
            return h + (r + bowl + lumps + path) * keep;
        }
        for (int z = 0; z < hf.N; z++)
            for (int x = 0; x < hf.N; x++)
            {
                // symmetric: average a point with its mirror so both colonies face identical ground
                float wx = x * hf.Cell, wz = z * hf.Cell;
                float h0 = Relief(wx, wz);
                float h1 = Relief(S - wx, S - wz);
                // banks rise toward the yard edges
                float edge = Mathf.Min(Mathf.Min(wx, S - wx), Mathf.Min(wz, S - wz));
                float bank = Mathf.Pow(Mathf.Clamp(1 - edge / 26f, 0, 1), 2) * 7f;
                hf.H[z * hf.N + x] = (h0 + h1) * 0.5f + bank;
            }
    }

    /// The soil sags around every stone, as if it had pressed itself in, with a faint rim of pushed-up earth.
    static void RockDips(Heightfield hf, List<Rock> rocks)
    {
        foreach (var r in rocks)
        {
            float reach = r.R * 1.9f + 1.5f;
            float depth = 0.22f * Mathf.Sqrt(r.R) + 0.1f;
            hf.ForEachInRadius(r.P.X, r.P.Y, reach, (i, d) =>
            {
                float t = d / reach;
                float sag = -(1f - Mathf.SmoothStep(0.45f, 1f, t));
                float rim = 0.25f * Mathf.Exp(-Mathf.Pow((t - 0.78f) / 0.12f, 2));
                hf.H[i] += depth * (sag + rim);
            });
        }
    }

    static void Crater(Heightfield hf, Vector2 c)
    {
        hf.ForEachInRadius(c.X, c.Y, 12f, (i, r) =>
        {
            // the excavated ring, its lip a little sharper on the inside, and a soft apron of spoil beyond it
            hf.H[i] += 2.3f * Mathf.Exp(-Mathf.Pow((r - 3.6f) / (r < 3.6f ? 1.7f : 2.4f), 2)) + 0.35f * Mathf.Exp(-Mathf.Pow((r - 7.5f) / 2.5f, 2)) - 1.9f * Mathf.Exp(-Mathf.Pow(r / 1.35f, 2));
            hf.Mound[i] = Mathf.Max(hf.Mound[i], Mathf.SmoothStep(10.5f, 3.5f, r));
        });
    }

    static void Thatch(Heightfield hf, Vector2 c)
    {
        hf.ForEachInRadius(c.X, c.Y, 14f, (i, r) =>
        {
            float t = Mathf.Clamp(1 - r / 13.5f, 0, 1);
            hf.H[i] += 4.6f * Mathf.Pow(t, 1.4f) - 1.5f * Mathf.Exp(-Mathf.Pow(r / 1.3f, 2));
            hf.Leaf[i] = 0;
        });
    }

    // ---------------------------------------------------------------- rocks
    sealed class Rock { public Vector2 P; public float R; public Vector3 Radii; public float Yaw; public int Seed; public float Tint, Rust, Moss; }

    static List<Rock> PlaceRocks(World w, Rng rng)
    {
        var list = new List<Rock>();
        bool Ok(Vector2 p, float r)
        {
            if (p.X < 14 || p.Y < 14 || p.X > S - 14 || p.Y > S - 14) return false;
            foreach (var n in w.Nests) if (p.DistanceTo(n.Pos) < r + 26) return false;
            if (p.DistanceTo(Center) < r + 14) return false;
            foreach (var o in list) if (p.DistanceTo(o.P) < r + o.R + 5) return false;
            // keep the mirrored copy apart too
            if (p.DistanceTo(Mirror(p)) < 2 * r + 5) return false;
            return true;
        }
        int tries = 0;
        while (list.Count < 46 && tries++ < 4000)
        {
            float r = rng.F() < 0.18f ? rng.R(6, 10) : rng.R(1.4f, 4.6f);
            var p = new Vector2(rng.R(10, S - 10), rng.R(10, S - 10));
            if (p.X + p.Y > S) continue; // generate one half, mirror the rest
            if (!Ok(p, r) || !Ok(Mirror(p), r)) continue;
            var a = MakeRock(rng, p, r);
            var b = MakeRock(rng, Mirror(p), r);
            b.Radii = a.Radii; b.Yaw = a.Yaw + Mathf.Pi; b.Seed = a.Seed; b.Tint = a.Tint; b.Rust = a.Rust; b.Moss = a.Moss;
            list.Add(a); list.Add(b);
        }
        foreach (var r in list) w.Obstacles.Add(new Obstacle { P = r.P, R = r.R * 0.92f });
        return list;
    }

    static Rock MakeRock(Rng rng, Vector2 p, float r) => new()
    {
        P = p, R = r, Radii = new Vector3(r, r * rng.R(0.55f, 0.85f), r * rng.R(0.8f, 1.15f)), Yaw = rng.R(0, Mathf.Tau),
        Seed = rng.I(1, 99999), Tint = rng.F(), Rust = rng.F() * 0.6f, Moss = rng.F() < 0.4f ? rng.R(0.4f, 1f) : 0f,
    };

    static void BuildRock(World w, Rock r)
    {
        var n = new FastNoiseLite { Seed = r.Seed, Frequency = 1.6f, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth };
        var n2 = new FastNoiseLite { Seed = r.Seed + 1, Frequency = 5f };
        var k = new MeshKit();
        k.Ellipsoid(Vector3.Zero, Basis.FromScale(r.Radii), 28, 18, new Color(1, r.Tint, r.Rust, r.Moss), Vector2.Zero, u =>
        {
            float d = 1 + 0.2f * n.GetNoise3D(u.X, u.Y, u.Z) + 0.05f * n2.GetNoise3D(u.X, u.Y, u.Z);
            if (u.Y < -0.2f) d *= Mathf.Lerp(1f, 0.7f, Mathf.Clamp((-u.Y - 0.2f) * 2, 0, 1));
            return d;
        });
        for (int i = 0; i < k.V.Count; i++)
        {
            var v = k.V[i];
            float ao = Mathf.Lerp(0.45f, 1.05f, Mathf.SmoothStep(-0.5f * r.Radii.Y, 0.5f * r.Radii.Y, v.Y));
            k.C[i] = new Color(ao, r.Tint, r.Rust, r.Moss);
        }
        k.RecalcNormals();
        var mi = new MeshInstance3D { Mesh = k.Commit(), MaterialOverride = Mat(w, Shaders.Rock) };
        float baseY = w.HF.Sample(r.P.X, r.P.Y);
        mi.Transform = new Transform3D(new Basis(Vector3.Up, r.Yaw), new Vector3(r.P.X, baseY - r.Radii.Y * 0.18f, r.P.Y));
        w.Root.AddChild(mi);
    }

    // ---------------------------------------------------------------- leaves
    sealed class LeafDef { public Vector2 P; public float Len, Wid, Yaw, Age, Seed; public Vector3[] Verts; public Vector2[] Uvs; public int Rows, Cols; }

    static float LeafHalfWidth(float v, float wid) => wid * 0.5f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.Pi * Mathf.Pow(v, 0.75f))), 0.85f) * (1 + 0.035f * Mathf.Sin(v * 70));

    static List<LeafDef> PlaceLeaves(World w, Rng rng, List<Rock> rocks)
    {
        var list = new List<LeafDef>();
        bool Ok(Vector2 p, float len)
        {
            if (p.X < 20 || p.Y < 20 || p.X > S - 20 || p.Y > S - 20) return false;
            foreach (var n in w.Nests) if (p.DistanceTo(n.Pos) < len * 0.6f + 16) return false;
            foreach (var r in rocks) if (p.DistanceTo(r.P) < r.R + len * 0.55f) return false;
            foreach (var l in list) if (p.DistanceTo(l.P) < (l.Len + len) * 0.42f) return false;
            return true;
        }
        int tries = 0;
        while (list.Count < 22 && tries++ < 3000)
        {
            float len = rng.R(18, 34);
            var p = new Vector2(rng.R(20, S - 20), rng.R(20, S - 20));
            if (p.X + p.Y > S) continue;
            if (!Ok(p, len) || !Ok(Mirror(p), len) || p.DistanceTo(Mirror(p)) < len) continue;
            var a = new LeafDef { P = p, Len = len, Wid = len * rng.R(0.42f, 0.55f), Yaw = rng.R(0, Mathf.Tau), Age = rng.R(0.15f, 1f), Seed = rng.F() };
            var b = new LeafDef { P = Mirror(p), Len = a.Len, Wid = a.Wid, Yaw = a.Yaw + Mathf.Pi, Age = a.Age, Seed = a.Seed };
            list.Add(a); list.Add(b);
        }
        // drape onto the ground and raise the walkable surface
        var hf = w.HF;
        foreach (var l in list)
        {
            int rows = 28, cols = 12;
            l.Rows = rows; l.Cols = cols;
            l.Verts = new Vector3[(rows + 1) * (cols + 1)];
            l.Uvs = new Vector2[l.Verts.Length];
            var rot = new Basis(Vector3.Up, l.Yaw);
            for (int r = 0; r <= rows; r++)
            {
                float v = (float)r / rows;
                float hw = Mathf.Max(0.05f, LeafHalfWidth(v, l.Wid));
                for (int c = 0; c <= cols; c++)
                {
                    float u = (float)c / cols;
                    float lx = (u - 0.5f) * 2 * hw;
                    float lz = (v - 0.45f) * l.Len;
                    Vector3 wp = rot * new Vector3(lx, 0, lz);
                    float x = l.P.X + wp.X, z = l.P.Y + wp.Z;
                    float curl = 1.4f * Mathf.Pow(Mathf.Abs(u - 0.5f) * 2, 2.2f) * (l.Wid / 14f) + 0.9f * Mathf.Pow(v, 3) * l.Age;
                    float y = hf.Sample(x, z) + 0.3f + curl;
                    l.Verts[r * (cols + 1) + c] = new Vector3(x, y, z);
                    l.Uvs[r * (cols + 1) + c] = new Vector2(u, v);
                }
            }
        }
        // write leaf surface into heightfield (after all meshes sampled the bare ground)
        foreach (var l in list)
        {
            var inv = new Basis(Vector3.Up, -l.Yaw);
            hf.ForEachInRadius(l.P.X, l.P.Y, l.Len * 0.6f, (i, d) =>
            {
                float x = (i % hf.N) * hf.Cell, z = (i / hf.N) * hf.Cell;
                Vector3 lp = inv * new Vector3(x - l.P.X, 0, z - l.P.Y);
                float v = lp.Z / l.Len + 0.45f;
                if (v < 0 || v > 1) return;
                float hw = LeafHalfWidth(v, l.Wid);
                if (Mathf.Abs(lp.X) > hw * 0.97f) return;
                float u = lp.X / (2 * hw) + 0.5f;
                float curl = 1.4f * Mathf.Pow(Mathf.Abs(u - 0.5f) * 2, 2.2f) * (l.Wid / 14f) + 0.9f * Mathf.Pow(v, 3) * l.Age;
                hf.H[i] += 0.3f + curl;
                hf.Leaf[i] = 1;
            });
        }
        return list;
    }

    static void BuildLeaf(World w, LeafDef l)
    {
        var k = new MeshKit();
        var col = new Color(l.Age, l.Seed, 1, 1);
        for (int i = 0; i < l.Verts.Length; i++) k.Add(l.Verts[i], Vector3.Up, col, l.Uvs[i], Vector2.Zero);
        int cols = l.Cols + 1;
        for (int r = 0; r < l.Rows; r++)
            for (int c = 0; c < l.Cols; c++)
            {
                int a = r * cols + c;
                k.Quad(a, a + 1, a + cols + 1, a + cols);
            }
        k.RecalcNormals();
        for (int i = 0; i < k.Nrm.Count; i++) if (k.Nrm[i].Y < 0) k.Nrm[i] = -k.Nrm[i];
        // stem
        var rot = new Basis(Vector3.Up, l.Yaw);
        Vector3 b0 = new Vector3(l.P.X, 0, l.P.Y) + rot * new Vector3(0, 0, -0.45f * l.Len);
        Vector3 b1 = new Vector3(l.P.X, 0, l.P.Y) + rot * new Vector3(0, 0, -0.45f * l.Len - l.Len * 0.18f);
        b0.Y = w.HF.Sample(b0.X, b0.Z) + 0.35f; b1.Y = w.HF.Sample(b1.X, b1.Z) + 0.45f;
        k.Tube(b0, b1, 0.32f, 0.22f, 6, new Color(l.Age, l.Seed, 1, 1), new Color(l.Age, l.Seed, 1, 1), Vector2.Zero);
        var mi = new MeshInstance3D { Mesh = k.Commit(), MaterialOverride = Mat(w, Shaders.Leaf) };
        w.Root.AddChild(mi);
    }

    // ---------------------------------------------------------------- twigs (walkable, ants climb over)
    sealed class TwigDef { public Vector3[] Pts; public float R; }

    static List<TwigDef> PlaceTwigs(World w, Rng rng)
    {
        var list = new List<TwigDef>();
        var hf = w.HF;
        int tries = 0;
        while (list.Count < 10 && tries++ < 400)
        {
            var p = new Vector2(rng.R(30, S - 30), rng.R(30, S - 30));
            if (p.X + p.Y > S) continue;
            bool bad = false;
            foreach (var n in w.Nests) if (p.DistanceTo(n.Pos) < 50) bad = true;
            if (p.DistanceTo(Center) < 30) bad = true;
            if (bad) continue;
            float len = rng.R(26, 55), yaw = rng.R(0, Mathf.Tau), rad = rng.R(0.7f, 1.3f);
            for (int m = 0; m < 2; m++)
            {
                var c = m == 0 ? p : Mirror(p);
                float y0 = m == 0 ? yaw : yaw + Mathf.Pi;
                var pts = new Vector3[4];
                for (int i = 0; i < 4; i++)
                {
                    float t = i / 3f - 0.5f;
                    var d = MathX.Dir(y0 + Mathf.Sin(i * 1.3f) * 0.12f);
                    var q = c + d * t * len;
                    pts[i] = new Vector3(q.X, 0, q.Y);
                }
                list.Add(new TwigDef { Pts = pts, R = rad });
            }
        }
        foreach (var t in list)
        {
            for (int i = 0; i < t.Pts.Length; i++) t.Pts[i].Y = hf.Sample(t.Pts[i].X, t.Pts[i].Z) + t.R * 0.75f;
            for (int s = 0; s < t.Pts.Length - 1; s++)
            {
                var a = t.Pts[s]; var b = t.Pts[s + 1];
                var a2 = new Vector2(a.X, a.Z); var b2 = new Vector2(b.X, b.Z);
                float rr = t.R * 1.15f;
                hf.ForEachInRadius((a.X + b.X) / 2, (a.Z + b.Z) / 2, a2.DistanceTo(b2) / 2 + rr + 1, (i, _) =>
                {
                    var q = new Vector2((i % hf.N) * hf.Cell, (i / hf.N) * hf.Cell);
                    var ab = b2 - a2;
                    float tt = Mathf.Clamp((q - a2).Dot(ab) / ab.LengthSquared(), 0, 1);
                    float d = q.DistanceTo(a2 + ab * tt);
                    if (d >= rr) return;
                    float top = Mathf.Lerp(a.Y, b.Y, tt) + Mathf.Sqrt(1 - (d / rr) * (d / rr)) * t.R;
                    hf.H[i] = Mathf.Max(hf.H[i], top);
                });
            }
        }
        return list;
    }

    static void BuildTwig(World w, TwigDef t)
    {
        var k = new MeshKit();
        var white = new Color(1, 1, 1);
        for (int s = 0; s < t.Pts.Length - 1; s++)
            k.Tube(t.Pts[s], t.Pts[s + 1], t.R * (1 - s * 0.08f), t.R * (1 - (s + 1) * 0.08f), 10, white, white, Vector2.Zero, s == t.Pts.Length - 2);
        // small side branch
        var mid = (t.Pts[1] + t.Pts[2]) * 0.5f;
        var dir = (t.Pts[2] - t.Pts[1]).Normalized().Cross(Vector3.Up);
        k.Tube(mid, mid + dir * 7 + (t.Pts[2] - t.Pts[1]).Normalized() * 4 + Vector3.Up * 0.5f, t.R * 0.45f, t.R * 0.15f, 6, white, white, Vector2.Zero);
        var mi = new MeshInstance3D { Mesh = k.Commit(), MaterialOverride = Mat(w, Shaders.Twig) };
        w.Root.AddChild(mi);
    }

    // ---------------------------------------------------------------- terrain mesh
    static float[] ComputeAO(Heightfield hf, List<Rock> rocks)
    {
        int n = hf.N;
        var ao = new float[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                float sum = 0; int cnt = 0;
                for (int dz = -3; dz <= 3; dz += 3)
                    for (int dx = -3; dx <= 3; dx += 3)
                    {
                        int xx = Math.Clamp(x + dx, 0, n - 1), zz = Math.Clamp(z + dz, 0, n - 1);
                        sum += hf.H[zz * n + xx]; cnt++;
                    }
                float cav = hf.H[i] - sum / cnt;
                ao[i] = Mathf.Clamp(1f + cav * 0.35f, 0.55f, 1.08f);
            }
        foreach (var r in rocks)
            hf.ForEachInRadius(r.P.X, r.P.Y, r.R * 1.5f, (i, d) => ao[i] *= Mathf.Lerp(0.45f, 1f, Mathf.Clamp((d / (r.R * 1.5f) - 0.55f) / 0.45f, 0, 1)));
        return ao;
    }

    static Vector3 GroundNormal(Heightfield hf, int x, int z)
    {
        int n = hf.N;
        float l = hf.Ground[z * n + Math.Max(0, x - 1)], r = hf.Ground[z * n + Math.Min(n - 1, x + 1)];
        float d = hf.Ground[Math.Max(0, z - 1) * n + x], u = hf.Ground[Math.Min(n - 1, z + 1) * n + x];
        return new Vector3(l - r, 2 * hf.Cell, d - u).Normalized();
    }

    static void BuildTerrainMesh(World w, float[] ao)
    {
        var hf = w.HF;
        int n = hf.N;
        var verts = new Vector3[n * n];
        var nrm = new Vector3[n * n];
        var cols = new Color[n * n];
        var uvs = new Vector2[n * n];
        var enemyNest = w.Nests[1].Pos;
        // how far below (or above) its surroundings each point lies, over about eight cells: the shader darkens
        // hollows into damp pebble beds and dries the crests pale
        var hollow = new float[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                float sum = 0; int cnt = 0;
                for (int dz = -8; dz <= 8; dz += 4)
                    for (int dx = -8; dx <= 8; dx += 4)
                    {
                        int xx = Math.Clamp(x + dx, 0, n - 1), zz = Math.Clamp(z + dz, 0, n - 1);
                        sum += hf.Ground[zz * n + xx]; cnt++;
                    }
                hollow[z * n + x] = Mathf.Clamp(0.5f + (sum / cnt - hf.Ground[z * n + x]) * 0.55f, 0, 1);
            }
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                float wx = x * hf.Cell, wz = z * hf.Cell;
                verts[i] = new Vector3(wx, hf.Ground[i], wz);
                nrm[i] = GroundNormal(hf, x, z);
                float thatch = Mathf.SmoothStep(13.5f, 6f, new Vector2(wx, wz).DistanceTo(enemyNest));
                cols[i] = new Color(ao[i], hf.Mound[i], thatch, hollow[i]);
                uvs[i] = new Vector2(wx, wz) / Config.MapSize;
            }
        var idx = new int[(n - 1) * (n - 1) * 6];
        int k = 0;
        for (int z = 0; z < n - 1; z++)
            for (int x = 0; x < n - 1; x++)
            {
                int a = z * n + x;
                // clockwise from above
                idx[k++] = a; idx[k++] = a + 1; idx[k++] = a + n;
                idx[k++] = a + 1; idx[k++] = a + n + 1; idx[k++] = a + n;
            }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Normal] = nrm;
        arrays[(int)Mesh.ArrayType.Color] = cols;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = idx;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        w.TerrainMaterial = Mat(w, Shaders.Terrain);
        w.TerrainMaterial.SetShaderParameter("normal_c", w.NormalC);
        w.TerrainMaterials.Add(w.TerrainMaterial);
        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = w.TerrainMaterial, Name = "Terrain" };
        w.Root.AddChild(mi);
    }

    // ---------------------------------------------------------------- grass
    static ArrayMesh BladeMesh()
    {
        var k = new MeshKit();
        int segs = 5;
        for (int i = 0; i <= segs; i++)
        {
            float t = (float)i / segs;
            float wdt = 0.5f * (1 - t * 0.92f);
            float bend = t * t * 0.35f;
            var c = new Color(1, t, 0, 1);
            k.Add(new Vector3(-wdt, t, bend), Vector3.Back, c, new Vector2(0, t), Vector2.Zero);
            k.Add(new Vector3(wdt, t, bend), Vector3.Back, c, new Vector2(1, t), Vector2.Zero);
        }
        for (int i = 0; i < segs; i++)
        {
            int a = i * 2;
            k.Quad(a, a + 2, a + 3, a + 1);
        }
        k.RecalcNormals();
        return k.Commit();
    }

    /// Ground height anywhere, including outside the yard: beyond the edge the bank keeps rising gently.
    static float SkirtY(Heightfield hf, float x, float z)
    {
        float cx = Mathf.Clamp(x, 0, S), cz = Mathf.Clamp(z, 0, S);
        float beyond = new Vector2(x - cx, z - cz).Length();
        return hf.Sample(cx, cz) + Mathf.Min(7f, beyond * 0.05f);
    }

    /// The land around the yard: the same soil, rising gently, so zooming out never shows the world's edge.
    /// (Its fog of war is the yard's edge stretched outward, so explored edges stay lit and unexplored ones dark.)
    static void BuildSkirt(World w)
    {
        const float step = 8f, reach = 480f;
        var hf = w.HF;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 V(float x, float z) => new(x, SkirtY(hf, x, z), z);
        for (float x = -reach; x < S + reach; x += step)
            for (float z = -reach; z < S + reach; z += step)
            {
                if (x >= 0 && z >= 0 && x + step <= S && z + step <= S) continue; // the yard itself has real terrain
                var a = V(x, z); var b = V(x + step, z); var c = V(x + step, z + step); var d = V(x, z + step);
                foreach (var v in new[] { a, b, c, a, c, d })
                {
                    float far = Mathf.Clamp((new Vector2(v.X, v.Z) - Center).Length() / 520f, 0, 1);
                    st.SetColor(new Color(1f - 0.25f * far, 0, 0, 0.5f)); // r: shade, g/b: no sand or thatch, a: level ground
                    st.AddVertex(v);
                }
            }
        st.GenerateNormals();
        var skirt = Mat(w, Shaders.Terrain);
        skirt.SetShaderParameter("normal_c", w.NormalC);
        w.TerrainMaterials.Add(skirt);
        w.Root.AddChild(new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = skirt, Name = "Skirt" }); // fog of war carries on seamlessly past the edge
    }

    static void BuildGrass(World w, Rng rng)
    {
        var xf = new List<Transform3D>();
        var dry = new List<float>();
        var hf = w.HF;
        void Blade(Vector2 p, float h)
        {
            if (w.Blocked(p, 0.5f)) return;
            var basis = new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Right, rng.R(-0.3f, 0.3f)) * Basis.FromScale(new Vector3(rng.R(0.9f, 1.6f), h, 1));
            xf.Add(new Transform3D(basis, new Vector3(p.X, SkirtY(hf, p.X, p.Y) - 0.2f, p.Y)));
            dry.Add(rng.F());
        }
        bool FarFromNests(Vector2 p, float d)
        {
            foreach (var n in w.Nests) if (p.DistanceTo(n.Pos) < d) return false;
            return true;
        }
        // in-field clumps (mirrored)
        int clumps = 0, tries = 0;
        while (clumps < (int)(34 * Settings.Density) && tries++ < 2000)
        {
            var c = new Vector2(rng.R(20, S - 20), rng.R(20, S - 20));
            if (c.X + c.Y > S || !FarFromNests(c, 34) || !FarFromNests(Mirror(c), 34) || c.DistanceTo(Center) < 22) continue;
            if (hf.Bilinear(hf.Leaf, c.X, c.Y) > 0.1f) continue;
            float rad = rng.R(2.5f, 6.5f);
            int count = (int)(rad * rad * 2.2f);
            float hb = rng.R(10, 24);
            for (int i = 0; i < count; i++)
            {
                var o = rng.InDisk(rad);
                float h = hb * rng.R(0.6f, 1.2f) * (1 - o.Length() / rad * 0.4f);
                Blade(c + o, h);
                Blade(Mirror(c + o), h);
            }
            clumps++;
        }
        // dense border lawn
        for (int i = 0; i < (int)(4200 * Settings.Density); i++)
        {
            float t = rng.F() * S, d = Mathf.Pow(rng.F(), 1.6f) * 22f;
            int side = rng.I(0, 4);
            var p = side switch { 0 => new Vector2(t, d), 1 => new Vector2(t, S - d), 2 => new Vector2(d, t), _ => new Vector2(S - d, t) };
            Blade(p, rng.R(14, 34) * (1.2f - d / 22f * 0.5f));
        }
        // and a tall wild meadow beyond the yard's edge, so the world never just stops
        for (int i = 0; i < (int)(3600 * Settings.Density); i++)
        {
            float t = rng.R(-40, S + 40), d = -Mathf.Pow(rng.F(), 0.8f) * 34f;
            int side = rng.I(0, 4);
            var p = side switch { 0 => new Vector2(t, d), 1 => new Vector2(t, S - d), 2 => new Vector2(d, t), _ => new Vector2(S - d, t) };
            Blade(p, rng.R(22, 44));
        }
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = BladeMesh() };
        mm.InstanceCount = xf.Count;
        for (int i = 0; i < xf.Count; i++)
        {
            mm.SetInstanceTransform(i, xf[i]);
            mm.SetInstanceCustomData(i, new Color(dry[i], 0, 0, 0));
        }
        var mmi = new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = Mat(w, Shaders.Grass), Name = "Grass" };
        w.Root.AddChild(mmi);
        w.Scatter.Add((mmi, Scatter.Grass));
    }

    // ---------------------------------------------------------------- nests
    static void BuildNestDressing(World w, Rng rng)
    {
        var hf = w.HF;
        // pine needles thatch on the red colony dome
        var nk = new MeshKit();
        var white = new Color(1, 1, 1);
        nk.Tube(new Vector3(0, 0, -0.5f), new Vector3(0, 0, 0.5f), 0.05f, 0.03f, 4, white, white, Vector2.Zero);
        var nmm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = nk.Commit() };
        var e = w.Nests[1].Pos;
        int nc = 2600;
        nmm.InstanceCount = nc;
        for (int i = 0; i < nc; i++)
        {
            var off = rng.InDisk(14.5f);
            if (off.Length() < 1.6f) off = off.Normalized() * 1.8f;
            var p = e + off;
            var nrm = hf.Normal(p.X, p.Y);
            var yaw = new Basis(Vector3.Up, rng.R(0, Mathf.Tau));
            var tilt = new Basis(nrm.Cross(Vector3.Up).Normalized() is var ax && ax.LengthSquared() > 0.01f ? ax : Vector3.Right, -Mathf.Acos(Mathf.Clamp(nrm.Y, -1, 1)));
            float len = rng.R(2.2f, 4.2f);
            var b = tilt * yaw * new Basis(Vector3.Right, rng.R(-0.25f, 0.25f)) * Basis.FromScale(new Vector3(1, 1, len));
            nmm.SetInstanceTransform(i, new Transform3D(b, new Vector3(p.X, hf.Sample(p.X, p.Y) + rng.R(0.02f, 0.25f), p.Y)));
            nmm.SetInstanceCustomData(i, new Color(rng.F(), 0, 0, 0));
        }
        w.Root.AddChild(new MultiMeshInstance3D { Multimesh = nmm, MaterialOverride = Mat(w, Shaders.Needle), Name = "Thatch" });
    }

    // ---------------------------------------------------------------- food
    static void PlaceFood(World w, Rng rng)
    {
        var hf = w.HF;
        void Pair(FoodKind kind, Vector2 p, float amount, float size)
        {
            Spawn(w, rng, kind, p, amount, size);
            Spawn(w, rng, kind, Mirror(p), amount, size);
        }
        bool Free(Vector2 p, float r)
        {
            if (p.X < 24 || p.Y < 24 || p.X > S - 24 || p.Y > S - 24) return false;
            if (w.Blocked(p, r + 2)) return false;
            foreach (var n in w.Nests) if (p.DistanceTo(n.Pos) < 18 + r) return false;
            foreach (var f in w.Food) if (p.DistanceTo(f.Pos) < f.Radius + r + 3) return false;
            return true;
        }
        Vector2 Find(Vector2 around, float minD, float maxD, float r)
        {
            for (int t = 0; t < 400; t++)
            {
                var p = around + MathX.Dir(rng.R(0, Mathf.Tau)) * rng.R(minD, maxD);
                if (Free(p, r) && Free(Mirror(p), r) && p.DistanceTo(Mirror(p)) > 2 * r + 6) return p;
            }
            return around;
        }
        var home = w.Nests[0].Pos;
        // starter crumbs near home
        for (int c = 0; c < 2; c++)
        {
            var cc = Find(home, 26, 42, 4);
            for (int i = 0; i < 5; i++)
            {
                var p = cc + rng.InDisk(4.5f);
                if (Free(p, 1.2f)) Pair(FoodKind.Crumb, p, rng.R(40, 70), rng.R(1.0f, 1.8f));
            }
        }
        // mid-range resources
        for (int i = 0; i < 3; i++) Pair(FoodKind.Seed, Find(home, 50, 95, 2.5f), 70, rng.R(2.2f, 2.8f));
        for (int i = 0; i < 2; i++) Pair(FoodKind.Sugar, Find(home, 70, 120, 2.5f), 220, 2.6f);
        for (int c = 0; c < 2; c++)
        {
            var cc = Find(home, 85, 140, 5);
            for (int i = 0; i < 6; i++)
            {
                var p = cc + rng.InDisk(6f);
                if (Free(p, 1.4f)) Pair(FoodKind.Crumb, p, rng.R(50, 90), rng.R(1.1f, 2.2f));
            }
        }
        Pair(FoodKind.Berry, Find(home, 110, 150, 5), 420, 4.2f);
        // contested prize in the middle
        Spawn(w, rng, FoodKind.Berry, Center, 1100, 6.5f);
    }

    /// Wild bug lairs beside the richest food. Rules depend only on distance to the nearest nest,
    /// and food comes in mirrored pairs, so both colonies face identical camps.
    static void PlaceCamps(World w, Rng rng)
    {
        void Add(Vector2 food, float radius, params (AntType, int)[] bugs)
        {
            float d0 = food.DistanceTo(w.Nests[0].Pos), d1 = food.DistanceTo(w.Nests[1].Pos);
            var home = d0 < d1 ? w.Nests[0].Pos : w.Nests[1].Pos;
            var away = (food - home).Normalized();
            var p = food + away * (radius + 4.5f);
            foreach (var c in w.Camps) if (c.Pos.DistanceTo(p) < 16) return;
            w.Camps.Add(new Camp { Pos = p, Facing = away.AngleD(), Bugs = bugs });
        }
        // the great raspberry in the middle: a stag beetle lord and his beetles, each equidistant from both nests
        var mid = Center;
        var bisector = new Vector2(1, 1).Normalized(); // points with x = y are equally far from both nests
        w.Camps.Add(new Camp { Pos = mid + bisector * 11, Facing = bisector.AngleD(), Bugs = new[] { (AntType.StagBeetle, 5) } });
        w.Camps.Add(new Camp { Pos = mid - bisector * 11, Facing = (-bisector).AngleD(), Bugs = new[] { (AntType.Beetle, 3), (AntType.Beetle, 3), (AntType.Wasp, 4) } });
        foreach (var f in w.Food.ToArray())
        {
            float d = Mathf.Min(f.Pos.DistanceTo(w.Nests[0].Pos), f.Pos.DistanceTo(w.Nests[1].Pos));
            if (f.Kind == FoodKind.Berry && f.Max < 1000) Add(f.Pos, f.Radius, (AntType.Spider, 3), (AntType.Beetle, 2));
            else if (f.Kind == FoodKind.Sugar) Add(f.Pos, f.Radius, (AntType.Beetle, 2), (AntType.Wasp, 1));
            else if (f.Kind == FoodKind.Seed && d > 75) Add(f.Pos, f.Radius, (AntType.Wasp, 2));
            else if (f.Kind == FoodKind.Crumb && d > 110) Add(f.Pos, f.Radius, (AntType.Spider, 2));
            else if (f.Kind == FoodKind.Crumb && d > 80) Add(f.Pos, f.Radius, (AntType.Beetle, 1));
        }
        ScatterCamps(w, rng);
        foreach (var c in w.Camps) if (w.Blocked(c.Pos, 1)) c.Pos += MathX.Dir(c.Facing) * 4;
    }

    /// Hunting grounds all over the yard, not just beside food: the farther from both nests, the
    /// bigger and higher-level the pack. Placed in mirrored pairs so both colonies face the same threat.
    static void ScatterCamps(World w, Rng rng)
    {
        const float step = 54f;
        var lone = new[] { AntType.Beetle, AntType.Spider, AntType.Wasp };
        for (float gx = 18; gx < S - 10; gx += step)
            for (float gy = 18; gy < S - 10; gy += step)
            {
                var p = new Vector2(gx + rng.R(-11, 11), gy + rng.R(-11, 11));
                if (p.X + p.Y > S - 12) continue; // one half only; the other half is its mirror
                p = new Vector2(Mathf.Clamp(p.X, 12, S - 12), Mathf.Clamp(p.Y, 12, S - 12));
                float d = Mathf.Min(p.DistanceTo(w.Nests[0].Pos), p.DistanceTo(w.Nests[1].Pos));
                if (d < 60 || p.DistanceTo(Center) < 30 || w.Blocked(p, 2)) continue;
                bool crowded = false;
                foreach (var c in w.Camps) if (c.Pos.DistanceTo(p) < 22 || c.Pos.DistanceTo(Mirror(p)) < 22) { crowded = true; break; }
                if (crowded) continue;
                (AntType, int)[] bugs;
                var a = lone[(int)rng.R(0, 2.999f)];
                var b = lone[(int)rng.R(0, 2.999f)];
                if (d < 95) bugs = new[] { (a, rng.F() < 0.5f ? 1 : 2) };
                else if (d < 140) bugs = new[] { (a, 2), (b, 3) };
                else bugs = new[] { (a, 3), (b, 3), (AntType.Beetle, 4) };
                float face = rng.R(0, DMath.Tau);
                w.Camps.Add(new Camp { Pos = p, Facing = face, Bugs = bugs });
                w.Camps.Add(new Camp { Pos = Mirror(p), Facing = face + DMath.Pi, Bugs = bugs });
            }
    }

    static readonly Dictionary<World, List<ArrayMesh>> _crumbCache = new();

    public static FoodSource Spawn(World w, Rng rng, FoodKind kind, Vector2 p, float amount, float size)
    {
        var f = new FoodSource { Id = w.NextFoodId++, Kind = kind, Pos = p, Amount = amount, Max = amount, Radius = size };
        // mid-game crumb showers reuse a few pre-built crumb shapes, scaled, to avoid hitches
        if (kind == FoodKind.Crumb && w.Food.Count > 40)
        {
            if (!_crumbCache.TryGetValue(w, out var cache))
            {
                cache = new List<ArrayMesh>();
                for (int i = 0; i < 6; i++)
                {
                    var tmp = Spawn(w, rng, FoodKind.Crumb, new Vector2(-100, -100), 0, 1f, true);
                    cache.Add((ArrayMesh)((MeshInstance3D)tmp.Node).Mesh);
                    tmp.Node.Free();
                }
                _crumbCache[w] = cache;
            }
            var cm = Mat(w, Shaders.Food);
            cm.SetShaderParameter("kind", 0);
            var node = new MeshInstance3D { Mesh = cache[rng.I(0, cache.Count)], MaterialOverride = cm };
            float yy = w.HF.Sample(p.X, p.Y) + size * 0.7f * 0.75f * 0.9f;
            node.Transform = new Transform3D(new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * Basis.FromScale(Vector3.One * size), new Vector3(p.X, yy, p.Y));
            w.Root.AddChild(node);
            f.Node = node;
            f.BaseScale = Vector3.One * size;
            f.Block = new Obstacle { P = p, R = size * 0.85f, BaseR = size * 0.85f };
            w.AddObstacle(f.Block);
            w.Food.Add(f);
            return f;
        }
        return Spawn(w, rng, kind, p, amount, size, false);
    }

    static FoodSource Spawn(World w, Rng rng, FoodKind kind, Vector2 p, float amount, float size, bool templateOnly)
    {
        var f = new FoodSource { Id = w.NextFoodId++, Kind = kind, Pos = p, Amount = amount, Max = amount, Radius = size };
        var k = new MeshKit();
        var nz = new FastNoiseLite { Seed = rng.I(1, 9999), Frequency = 1.3f };
        Vector3 radii;
        Func<Vector3, float> disp;
        float sink = 0.25f;
        switch (kind)
        {
            case FoodKind.Berry:
                radii = new Vector3(size, size * 0.85f, size * 1.1f);
                disp = u => (1 + 0.04f * nz.GetNoise3D(u.X * 4, u.Y * 4, u.Z * 4)) * (u.Y < -0.4f ? 0.85f : 1f) * (1 - Mathf.Max(0, u.Z) * 0.25f * Mathf.Max(0, -u.Y));
                break;
            case FoodKind.Sugar:
                radii = Vector3.One * size * 0.62f;
                disp = u => SuperEllipse(u, 7f) * (1 + 0.02f * nz.GetNoise3D(u.X * 9, u.Y * 9, u.Z * 9));
                sink = 0.05f;
                break;
            case FoodKind.Seed:
                radii = new Vector3(size * 0.38f, size * 0.24f, size * 0.85f);
                disp = u => 1 - Mathf.Max(0, u.Z) * 0.35f;
                sink = 0.3f;
                break;
            default:
                radii = new Vector3(size, size * 0.7f, size * 0.9f);
                disp = u => SuperEllipse(u, 2.6f) * (1 + 0.16f * nz.GetNoise3D(u.X, u.Y, u.Z));
                break;
        }
        float blockR = kind switch { FoodKind.Berry => size * 0.95f, FoodKind.Sugar => size * 0.6f, FoodKind.Seed => size * 0.5f, _ => size * 0.85f };
        if (kind == FoodKind.Berry)
        {
            // raspberry: a core plus a shell of glossy drupelets
            k.Ellipsoid(Vector3.Zero, Basis.FromScale(radii * 0.86f), 18, 12, new Color(0.35f, 0, 0, 1), Vector2.Zero);
            int count = 70;
            for (int i = 0; i < count; i++)
            {
                float yy = 1 - 2 * (i + 0.5f) / count;
                if (yy < -0.75f) continue;
                float rr = Mathf.Sqrt(1 - yy * yy), th = i * 2.39996f;
                var dir = new Vector3(Mathf.Cos(th) * rr, yy, Mathf.Sin(th) * rr);
                var c = dir * radii * 0.9f;
                var up = dir.Normalized();
                var side = up.Cross(Mathf.Abs(up.Y) > 0.9f ? Vector3.Right : Vector3.Up).Normalized();
                var b = new Basis(side, up, side.Cross(up)) * Basis.FromScale(new Vector3(0.24f, 0.2f, 0.24f) * size);
                float tone = rng.R(0.75f, 1f);
                k.Ellipsoid(c, b, 16, 11, new Color(tone, 0, 0, 1), Vector2.Zero);
            }
        }
        else
        {
            k.Ellipsoid(Vector3.Zero, Basis.FromScale(radii), 24, 16, new Color(1, 1, 1), Vector2.Zero, disp);
            k.RecalcNormals();
        }
        var m = Mat(w, Shaders.Food);
        m.SetShaderParameter("kind", kind == FoodKind.Corpse ? 0 : (int)kind);
        var mi = new MeshInstance3D { Mesh = k.Commit(), MaterialOverride = m };
        if (templateOnly) { f.Node = mi; return f; }
        var y = w.HF.Sample(p.X, p.Y) + radii.Y * (1 - sink) * 0.9f;
        mi.Transform = new Transform3D(new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Right, rng.R(-0.15f, 0.15f)), new Vector3(p.X, y, p.Y));
        w.Root.AddChild(mi);
        f.Node = mi;
        f.BaseScale = Vector3.One;
        f.Block = new Obstacle { P = p, R = blockR, BaseR = blockR };
        w.AddObstacle(f.Block);
        w.Food.Add(f);
        return f;
    }

    static float SuperEllipse(Vector3 u, float p)
    {
        float s = Mathf.Pow(Mathf.Abs(u.X), p) + Mathf.Pow(Mathf.Abs(u.Y), p) + Mathf.Pow(Mathf.Abs(u.Z), p);
        return 1f / Mathf.Pow(s, 1f / p);
    }

    // ---------------------------------------------------------------- litter: pebbles, grit, dry grass bits
    static ArrayMesh PebbleMesh(int seed)
    {
        var k = new MeshKit();
        var n = new FastNoiseLite { Seed = seed, Frequency = 1.8f };
        k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(1, 0.6f, 0.85f)), 10, 7, new Color(1, 1, 1), Vector2.Zero, u => 1 + 0.28f * n.GetNoise3D(u.X, u.Y, u.Z));
        k.RecalcNormals();
        return k.Commit();
    }

    static void BuildLitter(World w, Rng rng)
    {
        var hf = w.HF;
        var mat = Mat(w, Shaders.Pebble);
        bool Clear(Vector2 p, float pad)
        {
            if (hf.Bilinear(hf.Leaf, p.X, p.Y) > 0.05f || w.Blocked(p, pad)) return false;
            foreach (var n in w.Nests) if (p.DistanceTo(n.Pos) < 2.2f) return false;
            return true;
        }
        for (int variant = 0; variant < 3; variant++)
        {
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = PebbleMesh(17 + variant) };
            var xf = new List<Transform3D>();
            var cd = new List<Color>();
            for (int i = 0; i < (int)(4200 * Settings.Density); i++)
            {
                var p = new Vector2(rng.R(4, S - 4), rng.R(4, S - 4));
                if (!Clear(p, 0.3f)) continue;
                // mostly grit, some proper pebbles
                float s = rng.F() < 0.85f ? rng.R(0.08f, 0.35f) : rng.R(0.4f, 1.1f);
                var basis = new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Right, rng.R(-0.3f, 0.3f)) * Basis.FromScale(Vector3.One * s);
                xf.Add(new Transform3D(basis, new Vector3(p.X, hf.Sample(p.X, p.Y) + s * 0.12f, p.Y)));
                cd.Add(new Color(rng.F(), rng.F() < 0.15f ? 1 : 0, rng.F() < 0.2f ? rng.R(0.3f, 0.8f) : 0, rng.F()));
            }
            // excavated grains around the crater
            if (variant == 0)
            {
                var c = w.Nests[0].Pos;
                for (int i = 0; i < 1100; i++)
                {
                    float a = rng.R(0, Mathf.Tau), r = 2.2f + Mathf.Abs(rng.R(-1, 1) + rng.R(-1, 1)) * 3.3f;
                    var p = c + MathX.Dir(a) * r;
                    float s = rng.R(0.1f, 0.38f);
                    xf.Add(new Transform3D(new Basis(Vector3.Up, rng.R(0, 6)) * Basis.FromScale(Vector3.One * s), new Vector3(p.X, hf.Sample(p.X, p.Y) + s * 0.15f, p.Y)));
                    cd.Add(new Color(rng.R(0.5f, 1f), rng.F() < 0.1f ? 1 : 0, rng.R(0, 0.3f), rng.F()));
                }
            }
            mm.InstanceCount = xf.Count;
            for (int i = 0; i < xf.Count; i++) { mm.SetInstanceTransform(i, xf[i]); mm.SetInstanceCustomData(i, cd[i]); }
            var pmi = new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat, Name = "Pebbles" + variant };
            w.Root.AddChild(pmi);
            w.Scatter.Add((pmi, Scatter.Pebbles));
        }
        // dry grass bits and pine needles lying around
        var sk = new MeshKit();
        var white = new Color(1, 1, 1);
        sk.Tube(new Vector3(0, 0, -0.5f), new Vector3(0, 0, 0.5f), 0.06f, 0.04f, 4, white, white, Vector2.Zero);
        var smm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = sk.Commit() };
        var sx = new List<Transform3D>();
        var sc = new List<float>();
        for (int i = 0; i < (int)(2600 * Settings.Density); i++)
        {
            var p = new Vector2(rng.R(4, S - 4), rng.R(4, S - 4));
            if (!Clear(p, 0.5f)) continue;
            float len = rng.R(1.5f, 6f);
            var basis = new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Right, rng.R(-0.06f, 0.06f)) * Basis.FromScale(new Vector3(rng.R(0.8f, 1.6f), 1, len));
            sx.Add(new Transform3D(basis, new Vector3(p.X, hf.Sample(p.X, p.Y) + 0.04f, p.Y)));
            sc.Add(rng.R(0.4f, 1f));
        }
        smm.InstanceCount = sx.Count;
        for (int i = 0; i < sx.Count; i++) { smm.SetInstanceTransform(i, sx[i]); smm.SetInstanceCustomData(i, new Color(sc[i], 0, 0, 0)); }
        var strawMi = new MultiMeshInstance3D { Multimesh = smm, MaterialOverride = Mat(w, Shaders.Needle), Name = "Straw" };
        w.Root.AddChild(strawMi);
        w.Scatter.Add((strawMi, Scatter.Straw));
        w.ApplyQuality();
    }

    // ---------------------------------------------------------------- minimap
    static void BuildMinimapBase(World w, List<LeafDef> leaves, List<Rock> rocks)
    {
        int res = 200;
        var img = Image.CreateEmpty(res, res, false, Image.Format.Rgba8);
        var hf = w.HF;
        var sun = ToSun; // the minimap is lit by the same sun as the yard
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float wx = (x + 0.5f) / res * S, wz = (y + 0.5f) / res * S;
                var n = hf.Normal(wx, wz);
                float shade = Mathf.Clamp(0.75f + (n.X * sun.X + n.Z * sun.Z) * 2.5f, 0.45f, 1.2f);
                Color c = new Color(0.42f, 0.33f, 0.24f);
                if (hf.Bilinear(hf.Leaf, wx, wz) > 0.5f) c = new Color(0.45f, 0.38f, 0.15f);
                float m = hf.Bilinear(hf.Mound, wx, wz);
                c = c.Lerp(new Color(0.66f, 0.56f, 0.42f), m);
                if (new Vector2(wx, wz).DistanceTo(w.Nests[1].Pos) < 13) c = new Color(0.36f, 0.2f, 0.1f);
                float edge = Mathf.Min(Mathf.Min(wx, S - wx), Mathf.Min(wz, S - wz));
                if (edge < 16) c = c.Lerp(new Color(0.16f, 0.26f, 0.08f), Mathf.Clamp(1 - edge / 16f, 0, 1));
                img.SetPixel(x, y, new Color(c.R * shade, c.G * shade, c.B * shade));
            }
        foreach (var r in rocks)
        {
            int cx = (int)(r.P.X / S * res), cy = (int)(r.P.Y / S * res), rr = Mathf.Max(1, (int)(r.R / S * res));
            for (int y = -rr; y <= rr; y++)
                for (int x = -rr; x <= rr; x++)
                    if (x * x + y * y <= rr * rr && cx + x >= 0 && cy + y >= 0 && cx + x < res && cy + y < res)
                        img.SetPixel(cx + x, cy + y, new Color(0.55f, 0.53f, 0.5f) * (1.1f - 0.4f * (float)(x + y) / (2 * rr)));
        }
        w.MinimapBase = img;
    }
}
