using System.Collections.Generic;
using Godot;

namespace Mandible;

/// What food looks like: piles of real things an ant colony finds, never one rounded lump.
/// - crumbs: pieces broken off the scanned bread bun and croissant (crust outside, porous crumb on the broken faces);
/// - seeds: striped sunflower seeds, a few split open on their pale kernel, and small husked grass grains;
/// - sugar: a cluster of clear crystals sunk in a sticky amber drop;
/// - berries: a cluster of scanned wild-red berries (the lychee scan), one burst on its pulp and bleeding into the
///   soil; the great fruit in the middle of the yard is a fallen apple (the scan) with bites taken out of it.
/// Pieces are cut from the scans by up to three planes in the FoodCut shader, which draws the cut faces as solid
/// interior. As a pile is eaten its pieces are carried off one by one and the cuts bite deeper into what is left.
/// Render-only: the sim's FoodSource is untouched.
public static class FoodArt
{
    sealed class Pile
    {
        public Node3D Root;
        public readonly List<(Node3D node, float keep)> Pieces = new(); // keep: the fraction of food below which it is gone
        public readonly List<ShaderMaterial> Bitten = new();
    }

    static readonly Dictionary<FoodSource, Pile> _piles = new();

    /// Builds the pile for `f` (its node is placed at the food's spot) and returns its root.
    public static Node3D Build(World w, Rng rng, FoodSource f)
    {
        var pile = new Pile { Root = new Node3D { Name = "Food" + f.Id } };
        var hf = w.HF;
        float size = f.Radius;
        switch (f.Kind)
        {
            case FoodKind.Crumb: Crumbs(w, rng, pile, size); break;
            case FoodKind.Seed: Seeds(w, rng, pile, size); break;
            case FoodKind.Sugar: Sugar(w, rng, pile, size); break;
            default: if (size > 4.8f) Apple(w, rng, pile, size); else Berries(w, rng, pile, size); break;
        }
        // every piece sits on the soil where it lies
        var origin = new Vector3(f.Pos.X, hf.Sample(f.Pos.X, f.Pos.Y), f.Pos.Y);
        pile.Root.Position = origin;
        foreach (var (n, _) in pile.Pieces)
        {
            var p = n.Position;
            n.Position = new Vector3(p.X, hf.Sample(f.Pos.X + p.X, f.Pos.Y + p.Z) - origin.Y + p.Y, p.Z);
        }
        _piles[f] = pile;
        return pile.Root;
    }

    /// Follows the food as it is eaten. False if this food has no pile (it is then scaled the old way).
    public static bool Update(FoodSource f)
    {
        if (!_piles.TryGetValue(f, out var pile)) return false;
        float left = f.Max > 0 ? Mathf.Clamp(f.Amount / f.Max, 0, 1) : 0;
        foreach (var (n, keep) in pile.Pieces) n.Visible = left > keep;
        foreach (var m in pile.Bitten) m.SetShaderParameter("bite", 1 - left);
        return true;
    }

    /// A new yard: the last one's piles are gone with it.
    public static void Reset() => _piles.Clear();

    // ------------------------------------------------------------------ the kinds

    static void Crumbs(World w, Rng rng, Pile pile, float size)
    {
        var bread = NatureAssets.Kind("food_bread");
        int n = rng.I(4, 8);
        for (int i = 0; i < n; i++)
        {
            var p = bread[rng.I(0, bread.Count)];
            float s = (i == 0 ? size * 2.1f : size * rng.R(0.5f, 1.1f)) / p.Footprint;
            var m = Cut(p, rng, i == 0 ? 2 : 3, 0.12f, 0.5f, 0);
            var at = i == 0 ? Vector2.Zero : rng.InDisk(size * 1.1f);
            var mi = new MeshInstance3D { Mesh = p.Mesh, MaterialOverride = m };
            mi.Basis = Tumble(rng) * Basis.FromScale(Vector3.One * s);
            mi.Position = new Vector3(at.X, -p.Size.Y * s * 0.25f, at.Y);
            Add(pile, mi, i == 0 ? 0 : (float)i / n, m);
        }
    }

    static void Seeds(World w, Rng rng, Pile pile, float size)
    {
        int n = (int)(size * rng.R(6, 9));
        var seedMesh = SeedMesh();
        for (int i = 0; i < n; i++)
        {
            bool grass = rng.F() < 0.35f;
            bool split = !grass && rng.F() < 0.18f;
            var m = new ShaderMaterial { Shader = Shaders.Get(Shaders.FoodCut) };
            m.SetShaderParameter("noise_b", w.NoiseB);
            m.SetShaderParameter("outside", grass ? 2 : 1);
            m.SetShaderParameter("inside", 3);
            if (split) m.SetShaderParameter("cut0", new Vector4(0, 1, 0, 0.02f)); // the shell lies open on its kernel
            float len = grass ? rng.R(0.35f, 0.5f) : rng.R(0.6f, 0.95f);
            var mi = new MeshInstance3D { Mesh = seedMesh, MaterialOverride = m };
            // seeds heap: denser and higher in the middle
            var at = rng.InDisk(size * 0.9f) * Mathf.Sqrt(rng.F());
            float heap = Mathf.Max(0, 1 - at.Length() / size) * size * 0.25f;
            mi.Basis = new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Right, rng.R(-0.5f, 0.5f)) * new Basis(Vector3.Forward, rng.F() < 0.3f ? Mathf.Pi / 2 : rng.R(-0.3f, 0.3f))
                       * Basis.FromScale(new Vector3(grass ? 0.42f : 0.5f, grass ? 0.32f : 0.3f, 1) * len);
            mi.Position = new Vector3(at.X, heap + len * 0.08f, at.Y);
            Add(pile, mi, (float)i / n * 0.95f, null);
        }
    }

    static void Sugar(World w, Rng rng, Pile pile, float size)
    {
        // the sticky drop the crystals sit in
        var syrup = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.55f, 0.27f, 0.04f, 0.78f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.04f,
            MetallicSpecular = 0.9f, RefractionEnabled = true, RefractionScale = 0.05f, RimEnabled = true, Rim = 0.4f,
        };
        var drop = new MeshInstance3D { Mesh = DropMesh(rng, size * 1.15f), MaterialOverride = syrup };
        Add(pile, drop, 0.6f, null);
        var crystal = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.97f, 0.96f, 0.93f, 0.7f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.12f,
            MetallicSpecular = 1f, RefractionEnabled = true, RefractionScale = 0.04f, RimEnabled = true, Rim = 0.7f, RimTint = 0.1f,
        };
        var cube = CrystalMesh();
        int n = rng.I(8, 15);
        for (int i = 0; i < n; i++)
        {
            float s = size * rng.R(0.15f, 0.42f) * (i == 0 ? 1.5f : 1);
            var at = rng.InDisk(size * 0.75f);
            var mi = new MeshInstance3D { Mesh = cube, MaterialOverride = crystal, Basis = Tumble(rng) * Basis.FromScale(new Vector3(1, rng.R(0.8f, 1.15f), rng.R(0.85f, 1.2f)) * s), Position = new Vector3(at.X, s * 0.2f, at.Y) };
            Add(pile, mi, (float)i / n, null);
        }
    }

    static void Berries(World w, Rng rng, Pile pile, float size)
    {
        // wild berries fallen in a little cluster: the scanned lychee's bumpy red skin, each one turned differently
        var fruit = NatureAssets.Kind("food_lychee");
        if (fruit.Count == 0) return;
        var p = fruit[0];
        int n = Mathf.Clamp((int)(size * 1.6f), 3, 9);
        for (int i = 0; i < n; i++)
        {
            float d = size * rng.R(0.55f, 0.85f) * (i == 0 ? 1.15f : 1);
            var at = i == 0 ? Vector2.Zero : MathX.Dir(rng.R(0, Mathf.Tau)) * size * rng.R(0.45f, 0.95f);
            var m = i == 1 ? Cut(p, rng, 1, 0.1f, 0.25f, 2) : (ShaderMaterial)null; // one burst open on its pulp
            var mi = new MeshInstance3D { Mesh = p.Mesh, MaterialOverride = m ?? p.Material, Basis = Tumble(rng) * Basis.FromScale(Vector3.One * d / p.Footprint), Position = new Vector3(at.X, -d * 0.08f, at.Y) };
            Add(pile, mi, (float)i / n, m);
        }
        // juice soaked into the soil around the burst one
        var juice = new StandardMaterial3D { AlbedoColor = new Color(0.25f, 0.03f, 0.03f, 0.5f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.2f };
        var stain = new MeshInstance3D { Mesh = DropMesh(rng, size * 0.7f, 0.04f), MaterialOverride = juice, Position = new Vector3(size * 0.4f, 0.02f, size * 0.2f) };
        Add(pile, stain, 0.85f, null);
    }

    static void Apple(World w, Rng rng, Pile pile, float size)
    {
        var apples = NatureAssets.Kind("food_apple");
        if (apples.Count == 0) { Berries(w, rng, pile, size); return; }
        var p = apples[0];
        float s = size * 2.1f / p.Footprint;
        var m = Cut(p, rng, 2, 0.25f, 0.45f, 1);
        var mi = new MeshInstance3D { Mesh = p.Mesh, MaterialOverride = m, Basis = new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Forward, 0.25f) * Basis.FromScale(Vector3.One * s), Position = new Vector3(0, -p.Size.Y * s * 0.08f, 0) };
        Add(pile, mi, 0, m);
        // a wedge that broke off where it hit the ground
        var m2 = Cut(p, rng, 3, 0.05f, 0.25f, 1);
        var wedge = new MeshInstance3D { Mesh = p.Mesh, MaterialOverride = m2, Basis = Tumble(rng) * Basis.FromScale(Vector3.One * s * 0.55f), Position = new Vector3(size * 1.3f, 0, size * 0.4f) };
        Add(pile, wedge, 0.5f, m2);
    }

    // ------------------------------------------------------------------ helpers

    static void Add(Pile pile, Node3D n, float keep, ShaderMaterial bitten)
    {
        if (n is GeometryInstance3D g) g.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
        pile.Root.AddChild(n);
        pile.Pieces.Add((n, keep));
        if (bitten != null) pile.Bitten.Add(bitten);
    }

    static Basis Tumble(Rng rng) => new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Right, rng.R(-0.9f, 0.9f)) * new Basis(Vector3.Forward, rng.R(-0.9f, 0.9f));

    /// A FoodCut material for a scanned piece, with `cuts` random planes passing between `near` and `far` of the
    /// piece's half-size from its middle. `inside`: 0 bread crumb, 1 apple flesh.
    static ShaderMaterial Cut(NatureAssets.Piece p, Rng rng, int cuts, float near, float far, int inside)
    {
        var m = new ShaderMaterial { Shader = Shaders.Get(Shaders.FoodCut) };
        m.SetShaderParameter("albedo_tex", NatureAssets.Texture($"tex/{p.Src}_diff"));
        m.SetShaderParameter("normal_tex", NatureAssets.Texture($"tex/{p.Src}_nor"));
        m.SetShaderParameter("noise_b", NatureAssets.Texture("ground/gravelly_sand_arm"));
        m.SetShaderParameter("inside", inside);
        m.SetShaderParameter("outside", 0);
        m.SetShaderParameter("bite_depth", p.Longest * 0.3f);
        var mid = new Vector3(0, p.Size.Y * 0.5f, 0);
        float half = p.Longest * 0.5f;
        for (int i = 0; i < cuts; i++)
        {
            var nrm = new Vector3(rng.R(-1, 1), rng.R(-0.6f, 1), rng.R(-1, 1)).Normalized();
            float d = nrm.Dot(mid) + half * rng.R(near, far);
            m.SetShaderParameter($"cut{i}", new Vector4(nrm.X, nrm.Y, nrm.Z, d));
        }
        return m;
    }

    static ArrayMesh _seed, _crystal;

    /// A seed: a flattened teardrop, pointed at the end where it held to the flower head.
    static ArrayMesh SeedMesh()
    {
        if (_seed != null) return _seed;
        var k = new MeshKit();
        k.Ellipsoid(Vector3.Zero, Basis.Identity, 20, 12, new Color(1, 1, 1), Vector2.Zero, u => 1 - Mathf.Max(0, u.Z) * 0.45f * (1 - Mathf.Abs(u.Y)));
        for (int i = 0; i < k.V.Count; i++) k.V[i] *= 0.5f; // a unit long along z
        k.RecalcNormals();
        return _seed = k.Commit();
    }

    /// A sugar crystal: a slightly irregular block with bevelled edges (a unit across).
    static ArrayMesh CrystalMesh()
    {
        if (_crystal != null) return _crystal;
        var k = new MeshKit();
        k.Ellipsoid(Vector3.Zero, Basis.FromScale(Vector3.One * 0.5f), 16, 10, new Color(1, 1, 1), Vector2.Zero, u =>
        {
            // a rounded cube: superellipse of power 8
            float s = Mathf.Pow(Mathf.Abs(u.X), 8) + Mathf.Pow(Mathf.Abs(u.Y), 8) + Mathf.Pow(Mathf.Abs(u.Z), 8);
            return 1f / Mathf.Pow(s, 1f / 8);
        });
        k.RecalcNormals();
        return _crystal = k.Commit();
    }

    /// A drop spread flat on the soil, its edge wandering.
    static ArrayMesh DropMesh(Rng rng, float r, float height = 0.18f)
    {
        var k = new MeshKit();
        var noise = new FastNoiseLite { Seed = rng.I(0, 9999), Frequency = 1.4f };
        k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(r, r * height, r)), 32, 10, new Color(1, 1, 1), Vector2.Zero, u => (1 + 0.22f * noise.GetNoise2D(u.X * 2, u.Z * 2)) * (u.Y < 0 ? 0.05f : 1));
        k.RecalcNormals();
        return k.Commit();
    }
}
