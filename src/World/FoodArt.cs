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
        // drawn a little larger than the sim's radius (rocks and ants stay true to scale) so a pile reads at play zoom
        float size = f.Radius * 1.4f;
        // a soft dark bed of shade and damp under every pile, to lift it off the sand
        var bed = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.07f, 0.03f, 0.4f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 1, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        var bedMesh = new MeshInstance3D { Mesh = DropMesh(rng, size * 1.25f, 0.01f), MaterialOverride = bed, Position = new Vector3(0, 0.015f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        pile.Root.AddChild(bedMesh);
        switch (f.Kind)
        {
            case FoodKind.Crumb: Crumbs(w, rng, pile, size); break;
            case FoodKind.Seed: Seeds(w, rng, pile, size); break;
            case FoodKind.Sugar: Sugar(w, rng, pile, size); break;
            default: if (f.Radius > 4.8f) Apple(w, rng, pile, size); else Berries(w, rng, pile, size); break; // the sim's size picks the kind
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
        // torn bread crumbs modelled in Blender (lumpy, pitted chunks) in the Crumb shader: golden crust on the side
        // that was the loaf's outside, soft porous crumb on the torn faces. One big chunk, a scatter around it.
        var shapes = NatureAssets.Kind("food_crumb");
        if (shapes.Count == 0) shapes = NatureAssets.Kind("stone");
        var mat = new ShaderMaterial { Shader = Shaders.Get(Shaders.Crumb) };
        mat.SetShaderParameter("noise_b", w.NoiseB);
        int n = rng.I(10, 16);
        for (int i = 0; i < n; i++)
        {
            var p = shapes[rng.I(0, shapes.Count)];
            bool chunk = i == 0;
            float across = chunk ? size * 1.25f : size * Mathf.Lerp(0.3f, 0.75f, Mathf.Pow(rng.F(), 1.4f));
            float s = across / p.Footprint;
            var m = (ShaderMaterial)mat.Duplicate();
            m.SetShaderParameter("seed", rng.F());
            var at = chunk ? Vector2.Zero : MathX.Dir(rng.R(0, Mathf.Tau)) * size * (0.5f + Mathf.Pow(rng.F(), 1.4f) * 1.1f);
            var mi = new MeshInstance3D { Mesh = p.Mesh, MaterialOverride = m };
            mi.Basis = new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Right, rng.R(-0.4f, 0.4f)) * Basis.FromScale(Vector3.One * s);
            mi.Position = new Vector3(at.X, -p.Size.Y * s * 0.08f, at.Y);
            Add(pile, mi, chunk ? 0 : (float)i / n, null);
        }
    }

    static void Seeds(World w, Rng rng, Pile pile, float size)
    {
        // a heap of sunflower seeds modelled in Blender (four shells and two split halves), each turned its own way,
        // the heap higher in the middle; a few split shells lie open on their kernels at the edge
        var whole = NatureAssets.Kind("food_seed");
        var halves = NatureAssets.Kind("food_seedhalf");
        int n = (int)(size * rng.R(5, 7));
        for (int i = 0; i < n; i++)
        {
            var at = rng.InDisk(size * 0.9f) * Mathf.Sqrt(rng.F());
            bool split = halves.Count > 0 && at.Length() > size * 0.5f && rng.F() < 0.3f;
            var list = split ? halves : whole;
            if (list.Count == 0) continue;
            var p = list[rng.I(0, list.Count)];
            var m = new ShaderMaterial { Shader = Shaders.Get(Shaders.Seed) };
            m.SetShaderParameter("noise_b", w.NoiseB);
            m.SetShaderParameter("seed", rng.F());
            bool grassSeed = !split && rng.F() < 0.3f; // smaller, husked grass grains among the sunflower seeds
            m.SetShaderParameter("stripes", grassSeed ? 0f : rng.R(3.5f, 6.5f));
            m.SetShaderParameter("contrast", rng.R(0.35f, 0.75f));
            m.SetShaderParameter("wear", rng.F());
            float len = grassSeed ? rng.R(0.55f, 0.85f) : rng.R(1.1f, 1.8f); // a sunflower seed is about an ant and a half long
            float s = len / p.Size.Y;
            float heap = Mathf.Max(0, 1 - at.Length() / size) * size * 0.25f;
            // the scan stands along y: lay it down, on its flat side mostly
            var basis = new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Right, Mathf.Pi / 2 + rng.R(-0.35f, 0.35f))
                        * new Basis(Vector3.Up, rng.F() < 0.75f ? 0 : Mathf.Pi / 2) * Basis.FromScale(Vector3.One * s);
            var mi = new MeshInstance3D { Mesh = p.Mesh, MaterialOverride = m, Basis = basis, Position = new Vector3(at.X, heap + p.Size.Z * s * 0.4f, at.Y) };
            Add(pile, mi, (float)i / n * 0.95f, null);
        }
    }

    static void Sugar(World w, Rng rng, Pile pile, float size)
    {
        // spilled sugar: a drop of amber syrup with a raised, glossy edge (its meniscus), cut crystals sitting in and
        // around it from cube size down to single grains, all clear enough for the soil to show through
        var syrup = new StandardMaterial3D
        {
            // amber, dark where it pools deep, the soil showing through at the thin edge; a wet mirror on top
            AlbedoColor = new Color(0.36f, 0.17f, 0.02f, 0.8f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.12f, MetallicSpecular = 0.35f, // glossy, but no sun disc blooming off it
            ClearcoatEnabled = true, Clearcoat = 1f, ClearcoatRoughness = 0.0f,
            BacklightEnabled = true, Backlight = new Color(0.32f, 0.15f, 0.01f),
        };
        Add(pile, new MeshInstance3D { Mesh = SyrupMesh(rng, size * 0.8f), MaterialOverride = syrup, Position = new Vector3(size * 0.35f, 0.06f, size * 0.2f) }, 0.6f, null);
        var crystal = new StandardMaterial3D
        {
            // opaque enough to keep crisp facets at play zoom (a thin alpha read as a soft white smudge from afar)
            AlbedoColor = new Color(0.93f, 0.92f, 0.89f, 0.86f), Transparency = BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass,
            Roughness = 0.04f, MetallicSpecular = 1f, // no refraction: with it the screen copy ignores alpha and the crystal turns solid
            RimEnabled = true, Rim = 1f, RimTint = 0.05f, ClearcoatEnabled = true, Clearcoat = 1f, ClearcoatRoughness = 0f,
            EmissionEnabled = true, Emission = new Color(1f, 0.96f, 0.86f), EmissionEnergyMultiplier = 0.03f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var crystals = NatureAssets.Kind("food_sugar");
        var cube = CrystalMesh();
        Mesh Crystal() => crystals.Count > 0 ? crystals[rng.I(0, crystals.Count)].Mesh : cube;
        float unit = crystals.Count > 0 ? 1f / crystals[0].Longest : 1f;
        // sizes follow a power law: a few cube-sized crystals, many grains
        int n = (int)(size * 22);
        for (int i = 0; i < n; i++)
        {
            float u = Mathf.Pow(rng.F(), 3.2f);
            float s = size * Mathf.Lerp(0.05f, 0.5f, u);
            var at = rng.InDisk(size * (1.1f - u * 0.6f));
            var mi = new MeshInstance3D { Mesh = Crystal(), MaterialOverride = crystal, Basis = Tumble(rng) * Basis.FromScale(Vector3.One * s * unit * rng.R(0.85f, 1.2f)), Position = new Vector3(at.X, s * 0.25f, at.Y) };
            Add(pile, mi, (float)i / n, null);
        }
    }

    /// A drop of syrup spread on the soil: flat on top, its edge swelling into a rounded rim, a lobed outline.
    static ArrayMesh SyrupMesh(Rng rng, float r)
    {
        var k = new MeshKit();
        var white = new Color(1, 1, 1);
        var noise = new FastNoiseLite { Seed = rng.I(0, 9999), Frequency = 1.6f };
        const int rings = 10, sides = 40;
        int centre = k.Add(new Vector3(0, r * 0.07f, 0), Vector3.Up, white, new Vector2(0.5f, 0.5f), Vector2.Zero);
        for (int j = 1; j <= rings; j++)
        {
            float t = j / (float)rings;
            for (int sIdx = 0; sIdx < sides; sIdx++)
            {
                float a = sIdx / (float)sides * Mathf.Tau;
                float edge = r * (1 + 0.22f * noise.GetNoise2D(Mathf.Cos(a) * 2, Mathf.Sin(a) * 2));
                // flat middle, a swollen rim, then down to the soil
                float h = r * (0.07f + 0.05f * Mathf.Exp(-Mathf.Pow((t - 0.86f) / 0.08f, 2))) * (t > 0.95f ? (1 - t) / 0.05f : 1);
                var p = new Vector3(Mathf.Cos(a) * edge * t, h, Mathf.Sin(a) * edge * t);
                k.Add(p, Vector3.Up, white, new Vector2(0.5f + 0.5f * Mathf.Cos(a) * t, 0.5f + 0.5f * Mathf.Sin(a) * t), Vector2.Zero);
            }
        }
        for (int sIdx = 0; sIdx < sides; sIdx++) k.Tri(centre, 1 + sIdx, 1 + (sIdx + 1) % sides);
        for (int j = 0; j < rings - 1; j++)
            for (int sIdx = 0; sIdx < sides; sIdx++)
            {
                int a = 1 + j * sides + sIdx, b = 1 + j * sides + (sIdx + 1) % sides;
                k.Quad(a, a + sides, b + sides, b);
            }
        k.RecalcNormals();
        return k.Commit();
    }

    static void Berries(World w, Rng rng, Pile pile, float size)
    {
        // fallen wild fruit: red currants of different sizes and reds, now and then a blackberry, a few bruised dark,
        // one split and leaking; all crown up with a tiny withered calyx and a thread of stalk (Blender models)
        var currants = NatureAssets.Kind("food_berry").FindAll(p => p.File != "berry_4_0.res");
        var blacks = NatureAssets.Kind("food_blackberry");
        if (currants.Count == 0) return;
        int n = Mathf.Clamp((int)(size * 2.6f), 5, 16);
        for (int i = 0; i < n; i++)
        {
            bool black = blacks.Count > 0 && rng.F() < 0.12f;
            var p = black ? blacks[rng.I(0, blacks.Count)] : currants[rng.I(0, currants.Count)];
            float d = size * (black ? rng.R(0.4f, 0.5f) : rng.R(0.28f, 0.44f));
            var at = i == 0 ? Vector2.Zero : MathX.Dir(rng.R(0, Mathf.Tau)) * size * Mathf.Sqrt(rng.F()) * 1.1f;
            var m = new ShaderMaterial { Shader = Shaders.Get(Shaders.BerrySkin) };
            m.SetShaderParameter("noise_b", w.NoiseB);
            float bruise = rng.F() < 0.2f ? rng.R(0.35f, 0.6f) : rng.R(0.8f, 1.15f);
            m.SetShaderParameter("tone", bruise);
            m.SetShaderParameter("black", black ? 1f : 0f);
            m.SetShaderParameter("split", !black && i == 1 ? 1f : 0f);
            var mi = new MeshInstance3D { Mesh = p.Mesh, MaterialOverride = m,
                Basis = new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Right, rng.R(-0.5f, 0.5f)) * Basis.FromScale(Vector3.One * d / p.Footprint * rng.R(0.9f, 1.1f)),
                Position = new Vector3(at.X, -d * 0.06f, at.Y) };
            Add(pile, mi, (float)i / n, null);
        }
        var juice = new StandardMaterial3D { AlbedoColor = new Color(0.22f, 0.02f, 0.03f, 0.5f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.1f, MetallicSpecular = 0.8f };
        Add(pile, new MeshInstance3D { Mesh = DropMesh(rng, size * 0.45f, 0.02f), MaterialOverride = juice, Position = new Vector3(size * 0.3f, 0.02f, size * 0.2f) }, 0.85f, null);
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
        // food pieces are tiny meshes scaled up hundreds of times: keep their full detail, or the mesh LOD picks a
        // faceted level for them at play distance
        if (n is GeometryInstance3D g) { g.CastShadow = GeometryInstance3D.ShadowCastingSetting.On; g.LodBias = 64f; }
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
