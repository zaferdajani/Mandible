using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Lays photoscanned stones, bark, twigs, leaves and roots over the yard the way they lie in nature, never as one
/// mesh repeated across an area. Everything is decoration (the sim's rocks keep their own obstacles) and is drawn
/// from the map's decoration seed, so every player sees the same yard.
///
/// How a yard is dressed:
/// - each gameplay rock is a scanned rock (species picked by the rock's own seed), sunk into the soil and tilted to
///   the slope, with an apron of smaller stones of other species packed against its foot;
/// - stones gather in clusters (Neyman-Scott: cluster centres, then members spread around each one), dense where
///   water would wash them (hollows, the foot of slopes) and along trail edges, absent in wide bare gaps set by a
///   slow noise field; sizes follow a power law, a couple of big stones per cluster and many small ones;
/// - each stone gets its own species, scale, yaw, tilt, sink and a slight colour shift;
/// - bark, twigs, leaves and roots lie flat (rotated to the ground), never on a walked trail.
/// One MultiMesh per piece keeps it to a few dozen draw calls.
public static class NatureScatter
{
    public sealed class Site
    {
        public Vector2 P; public float R, Yaw; public int Seed;
    }

    sealed class Batch
    {
        public readonly List<Transform3D> Xf = new();
        public readonly List<Color> Col = new();
    }

    /// Collects placed pieces and commits them as one MultiMesh per piece. Hand-dressed places (level 1's alley)
    /// use it directly; Build() uses it for a whole yard.
    public sealed class Dresser
    {
        readonly World _w; readonly Rng _rng;
        readonly Dictionary<NatureAssets.Piece, Batch> _batches = new();
        public readonly List<NatureAssets.Piece> Stones, Rocks, SmallStones, Bark, Twigs, Leaves, Roots, Grass, DryGrass;

        public Dresser(World w, Rng rng)
        {
            _w = w; _rng = rng;
            Stones = NatureAssets.Kind("stone"); Rocks = NatureAssets.Kind("rock");
            Bark = NatureAssets.Kind("bark"); Leaves = NatureAssets.Kind("leaf").FindAll(p => !p.Src.StartsWith("dry_quiver")); /* the quiver-tree leaf is a dark succulent spike, wrong on a woodland floor */ Roots = NatureAssets.Kind("root").FindAll(p => p.Src == "single_root"); // the root cluster scan stands up off the soil in a tangle a stone high
            Twigs = NatureAssets.Kind("twig").FindAll(p => p.Src.StartsWith("dry_branches")); // the quiver-tree branches are bushy 3D forks that stand up off the soil
            Grass = NatureAssets.Kind("grass").FindAll(p => !p.Dead && p.Size.Y > 0.05f); DryGrass = NatureAssets.Kind("grass").FindAll(p => p.Dead);
            SmallStones = new List<NatureAssets.Piece>(Stones);
            foreach (var b in Rocks) if (b.Src.StartsWith("namaqualand_rocks")) SmallStones.Add(b); // the hand-sized ones
        }

        public NatureAssets.Piece Pick(List<NatureAssets.Piece> l) => l[_rng.I(0, l.Count)];

        /// `size`: footprint across as it lies, in world units. `sink`: fraction of its height pushed into the soil.
        public void Place(NatureAssets.Piece p, Vector2 at, float size, float sink, float tilt, Basis lie, float yaw, float tone, float squash = 1, float slopeFollow = 1)
        {
            var hf = _w.HF;
            var laid = (lie * p.Size).Abs();
            float s = size / Mathf.Max(laid.X, laid.Z);
            var n = hf.Normal(at.X, at.Y);
            var axis = n.Cross(Vector3.Up);
            var toSlope = axis.LengthSquared() > 1e-6f ? new Basis(axis.Normalized(), -Mathf.Acos(Mathf.Clamp(n.Y, -1, 1)) * slopeFollow) : Basis.Identity;
            var wobble = new Basis(Vector3.Right, _rng.R(-tilt, tilt)) * new Basis(Vector3.Forward, _rng.R(-tilt, tilt));
            var basis = toSlope * new Basis(Vector3.Up, yaw) * wobble * Basis.FromScale(new Vector3(s, s * squash, s)) * lie;
            float h = laid.Y * s * squash;
            // the pieces are based at y = 0; once laid down, whatever corner ends lowest must sit on the soil
            float minY = 0;
            for (int c = 0; c < 8; c++)
            {
                var corner = new Vector3((c & 1) == 0 ? -0.5f : 0.5f, (c & 2) == 0 ? 0 : 1, (c & 4) == 0 ? -0.5f : 0.5f) * p.Size;
                minY = Mathf.Min(minY, (lie * corner).Y);
            }
            float lift = -minY * s * squash;
            var pos = new Vector3(at.X, hf.Sample(at.X, at.Y) + lift - h * sink, at.Y);
            if (!_batches.TryGetValue(p, out var b)) _batches[p] = b = new Batch();
            b.Xf.Add(new Transform3D(basis, pos));
            b.Col.Add(new Color(tone * _rng.R(0.96f, 1.04f), tone * _rng.R(0.95f, 1.02f), tone * _rng.R(0.92f, 1.0f)));
        }

        /// A grass sprig `height` tall, leaning a little; `dry` 0..1 picks dead, straw-coloured sprigs more often.
        public void Sprig(Vector2 at, float height, float dry)
        {
            bool dead = DryGrass.Count > 0 && _rng.F() < dry * 0.6f;
            var p = Pick(dead ? DryGrass : Grass);
            float s = dead ? height * 0.5f / p.Footprint : height / Mathf.Max(p.Size.Y, 0.02f); // dead tufts lie flat: sized by their spread
            float tone = Mathf.Lerp(1f, 0.82f, dry) * _rng.R(0.9f, 1.05f);
            var basis = new Basis(Vector3.Up, _rng.R(0, Mathf.Tau)) * new Basis(Vector3.Right, _rng.R(-0.25f, 0.25f)) * Basis.FromScale(Vector3.One * s);
            var pos = new Vector3(at.X, _w.HF.Sample(at.X, at.Y) - 0.05f, at.Y);
            if (!_batches.TryGetValue(p, out var b)) _batches[p] = b = new Batch();
            b.Xf.Add(new Transform3D(basis, pos));
            // dry blades drift from green toward straw
            b.Col.Add(new Color(tone * (1f + 0.25f * dry), tone * (1f - 0.05f * dry), tone * (1f - 0.35f * dry)));
        }

        /// A stone `size` across, of a species that comes in that size.
        public void Stone(Vector2 at, float size, float sink) =>
            Place(Pick(size > 1.4f ? Rocks : SmallStones), at, size, sink, 0.45f, Basis.Identity, _rng.R(0, Mathf.Tau), _rng.R(0.82f, 1.02f));

        /// Bark, a twig, a leaf or a root `len` long, lying flat.
        public void Debris(List<NatureAssets.Piece> list, Vector2 at, float len, float yaw)
        {
            if (list.Count == 0) return;
            var p = Pick(list);
            Place(p, at, len, list == Twigs || list == Roots ? 0.3f : 0.1f, 0.08f, p.Lie, yaw, _rng.R(0.75f, 1.0f));
        }

        public void Commit()
        {
            foreach (var (p, b) in _batches)
            {
                var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = p.Mesh, InstanceCount = b.Xf.Count };
                for (int i = 0; i < b.Xf.Count; i++) { mm.SetInstanceTransform(i, b.Xf[i]); mm.SetInstanceColor(i, b.Col[i]); }
                var mi = new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = p.Material, Name = "Nature_" + p.File.Replace(".res", "") };
                _w.Root.AddChild(mi);
                if (p.Kind != "rock") _w.Scatter.Add((mi, p.Kind == "grass" ? Scatter.Grass : Scatter.Pebbles)); // rocks always cast shadows; small things follow the tier
            }
            _batches.Clear();
        }
    }

    /// `rocks`: the gameplay rocks. `trail`: distance from a point to the nearest trail centre line (or a large
    /// number where there is none). `keepOut`: true where nothing may lie (nest mouths, food, inside rocks).
    public static void Build(World w, Rng rng, IReadOnlyList<Site> rocks, Func<Vector2, float> trail, Func<Vector2, bool> keepOut, Rect2 area, float density)
    {
        var hf = w.HF;
        var d = new Dresser(w, rng);
        var gaps = new FastNoiseLite { Seed = rng.I(1, 99999), Frequency = 0.018f, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth };
        var hollowAt = HollowField(hf);

        // 1. the gameplay rocks, each a scanned rock picked by the rock's own seed, with stones packed around its foot
        foreach (var r in rocks)
        {
            var p = d.Rocks[(int)((uint)r.Seed % (uint)d.Rocks.Count)];
            float s = r.R * 2.15f / p.Footprint;
            float squash = Mathf.Clamp(r.R * 1.25f / (p.Size.Y * s), 0.66f, 1f); // keep tall boulders from towering
            d.Place(p, r.P, r.R * 2.15f, rng.R(0.12f, 0.28f), 0.05f, Basis.Identity, r.Yaw, rng.R(0.92f, 1.03f), squash, 0.6f);
            int apron = (int)(rng.R(3, 9) * Mathf.Sqrt(r.R) * density);
            for (int i = 0; i < apron; i++)
            {
                var at = r.P + MathX.Dir(rng.R(0, Mathf.Tau)) * r.R * rng.R(0.95f, 1.5f);
                if (!keepOut(at)) d.Place(d.Pick(d.SmallStones), at, r.R * Mathf.Pow(rng.F(), 2.2f) * 0.38f + 0.12f, rng.R(0.25f, 0.6f), 0.4f, Basis.Identity, rng.R(0, Mathf.Tau), rng.R(0.85f, 1.0f));
            }
            if (rng.F() < 0.3f * density && d.Roots.Count > 0)
            {
                // a root surfacing from under the rock and diving back into the soil
                float a = rng.R(0, Mathf.Tau);
                var at = r.P + MathX.Dir(a) * r.R * 1.3f;
                if (!keepOut(at)) d.Debris(d.Roots, at, r.R * rng.R(1.4f, 2.4f), a + Mathf.Pi / 2);
            }
        }

        // 2. stone clusters, driven by the land
        float areaSize = area.Size.X * area.Size.Y;
        int centres = (int)(areaSize / 170f * density);
        for (int c = 0; c < centres; c++)
        {
            var at = area.Position + new Vector2(rng.F() * area.Size.X, rng.F() * area.Size.Y);
            float gap = gaps.GetNoise2D(at.X, at.Y) * 0.5f + 0.5f;            // 0 bare .. 1 strewn
            float hol = hollowAt(at.X, at.Y);
            float slope = 1 - hf.Normal(at.X, at.Y).Y;
            float tr = trail(at);
            float edge = tr < 1e5f ? Mathf.Exp(-Mathf.Pow((tr - 6.5f) / 2.5f, 2)) : 0;   // trail edges, not its middle
            float want = Mathf.Clamp(0.35f + hol * 0.8f + slope * 2.5f + edge * 0.9f, 0, 1.4f) * Mathf.SmoothStep(0.3f, 0.6f, gap);
            if (tr < 3.5f) want *= 0.15f; // the walked middle of a trail is swept
            if (rng.F() > want || keepOut(at)) continue;
            float spread = rng.R(1.2f, 4.5f);
            int members = (int)(rng.R(4, 16) * (0.7f + hol));
            for (int i = 0; i < members; i++)
            {
                var o = new Vector2(Gauss(rng), Gauss(rng)) * spread * 0.55f;
                var q = at + o;
                if (keepOut(q)) continue;
                // big ones near the middle, small ones spill outward
                float u = Mathf.Pow(rng.F(), 2.6f) * (1f - Mathf.Clamp(o.Length() / (spread * 1.6f), 0, 0.7f));
                d.Stone(q, 0.25f + u * 3.2f, rng.R(0.15f, 0.5f));
            }
        }

        // 3. grit: thousands of small scanned stones (cut to ~50 triangles) that give the soil real grain at play zoom,
        //    thick in the hollows and along trail edges, thin on crests, never a uniform carpet
        var grit = NatureAssets.Kind("grit");
        if (grit.Count > 0 && Settings.Gfx > Settings.Quality.Low) // phones: the ground set alone carries the grain
        {
            int grains = (int)(areaSize / 6f * density);
            for (int i = 0; i < grains; i++)
            {
                var at = area.Position + new Vector2(rng.F() * area.Size.X, rng.F() * area.Size.Y);
                float hol = hollowAt(at.X, at.Y), tr = trail(at);
                float edge = tr < 1e5f ? Mathf.Exp(-Mathf.Pow((tr - 5.5f) / 3f, 2)) : 0;
                float patch = gaps.GetNoise2D(at.X * 2.3f + 17, at.Y * 2.3f) * 0.5f + 0.5f;
                if (rng.F() > Mathf.Clamp(0.4f + hol * 0.9f + edge * 0.7f, 0, 1) * Mathf.SmoothStep(0.15f, 0.55f, patch) || keepOut(at)) continue;
                d.Place(d.Pick(grit), at, rng.R(0.12f, 0.5f) * (1 + hol * 0.5f), rng.R(0.3f, 0.6f), 0.6f, Basis.Identity, rng.R(0, Mathf.Tau), rng.R(0.8f, 1.05f));
            }
        }

        // 4. ground cover at the scale of the ant: single grass sprigs, thicker where it is damp and off the trail, and
        //    thin elsewhere, with wide bare patches; this is what makes the soil read as a forest floor up close
        int sprigs = (int)(areaSize / 45f * density);
        for (int i = 0; i < sprigs; i++)
        {
            var at = area.Position + new Vector2(rng.F() * area.Size.X, rng.F() * area.Size.Y);
            float g = gaps.GetNoise2D(at.X * 1.3f - 40, at.Y * 1.3f) * 0.5f + 0.5f, hol = hollowAt(at.X, at.Y);
            if (rng.F() > Mathf.SmoothStep(0.3f, 0.7f, g) * (0.7f + hol) || keepOut(at) || trail(at) < 4f) continue;
            int tuft = rng.I(1, 5);
            for (int k = 0; k < tuft; k++) d.Sprig(at + rng.InDisk(0.9f), rng.R(2.5f, 8f), rng.R(0.1f, 0.9f));
        }

        // 5. organic debris: bark, twigs and leaves where the gap noise allows, never on a walked trail
        int bits = (int)(areaSize / 60f * density);
        for (int i = 0; i < bits; i++)
        {
            var at = area.Position + new Vector2(rng.F() * area.Size.X, rng.F() * area.Size.Y);
            float gap = gaps.GetNoise2D(at.X * 1.7f + 50, at.Y * 1.7f) * 0.5f + 0.5f;
            if (rng.F() > Mathf.SmoothStep(0.35f, 0.75f, gap) || keepOut(at) || trail(at) < 3f) continue;
            float roll = rng.F();
            if (roll < 0.4f) d.Debris(d.Leaves, at, rng.R(1.8f, 4.5f), rng.R(0, Mathf.Tau));
            else if (roll < 0.75f) d.Debris(d.Twigs, at, rng.R(2.5f, 9f), rng.R(0, Mathf.Tau));
            else d.Debris(d.Bark, at, rng.R(1.2f, 3.5f), rng.R(0, Mathf.Tau));
        }
        d.Commit();
        LeafLitter(w, rng, trail, keepOut, area, density, gaps);
    }

    /// 6. leaf litter: thousands of small fallen leaves and leaf fragments (the scanned leaf atlas on flat cards, one
    /// MultiMesh), each its own species, age and size, drifted into hollows, against rocks and along trail edges.
    static void LeafLitter(World w, Rng rng, Func<Vector2, float> trail, Func<Vector2, bool> keepOut, Rect2 area, float density, FastNoiseLite gaps)
    {
        if (Settings.Gfx == Settings.Quality.Low) return; // phones: the forest-litter ground set carries it
        var hf = w.HF;
        var hollowAt = HollowField(hf);
        var xf = new List<Transform3D>();
        var col = new List<Color>();
        int n = (int)(area.Size.X * area.Size.Y / 9f * density);
        for (int i = 0; i < n; i++)
        {
            var at = area.Position + new Vector2(rng.F() * area.Size.X, rng.F() * area.Size.Y);
            float hol = hollowAt(at.X, at.Y), tr = trail(at);
            float edge = tr < 1e5f ? Mathf.Exp(-Mathf.Pow((tr - 6f) / 3f, 2)) : 0;
            float drift = gaps.GetNoise2D(at.X * 0.9f + 90, at.Y * 0.9f) * 0.5f + 0.5f;
            if (rng.F() > Mathf.Clamp(0.35f + hol * 0.8f + edge * 0.6f, 0, 1) * Mathf.SmoothStep(0.2f, 0.6f, drift) || keepOut(at) || tr < 3f) continue;
            int sp = rng.F() < 0.55f ? 0 : rng.F() < 0.5f ? 2 : 1;
            float len = rng.R(1.0f, 3.2f) * (rng.F() < 0.1f ? 1.6f : 1);
            float wid = len * (sp == 1 ? 0.3f : 0.55f);
            var nrm = hf.Normal(at.X, at.Y);
            var axis = nrm.Cross(Vector3.Up);
            var toSlope = axis.LengthSquared() > 1e-6f ? new Basis(axis.Normalized(), -Mathf.Acos(Mathf.Clamp(nrm.Y, -1, 1))) : Basis.Identity;
            // flat on the soil, curled a little by a tilt, turned any way
            var basis = toSlope * new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Right, -Mathf.Pi / 2 + rng.R(-0.25f, 0.25f)) * Basis.FromScale(new Vector3(wid, len, 1));
            xf.Add(new Transform3D(basis, new Vector3(at.X, hf.Sample(at.X, at.Y) + 0.04f, at.Y)));
            col.Add(new Color(Mathf.Clamp(0.45f + rng.F() * 0.55f + hol * 0.2f, 0, 1), rng.F(), sp * 0.5f, 1)); // age, seed, species
        }
        if (xf.Count == 0) return;
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = new QuadMesh { Size = Vector2.One }, InstanceCount = xf.Count };
        for (int i = 0; i < xf.Count; i++) { mm.SetInstanceTransform(i, xf[i]); mm.SetInstanceColor(i, col[i]); }
        var m = WorldBuilder.Mat(w, Shaders.Leaf);
        m.SetShaderParameter("leaf_tex", NatureAssets.Texture("tex/leaf_atlas_diff"));
        m.SetShaderParameter("leaf_nor", NatureAssets.Texture("tex/leaf_atlas_nor"));
        var mi = new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = m, Name = "LeafLitter", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        w.Root.AddChild(mi);
    }

    static float Gauss(Rng rng) => (rng.F() + rng.F() + rng.F() - 1.5f) * 1.4f;

    /// How far below its surroundings each point sits (0 level or crest, 1 a clear hollow), from the settled ground.
    static Func<float, float, float> HollowField(Heightfield hf) => (x, z) =>
    {
        float c = hf.Sample(x, z), sum = 0;
        for (int k = 0; k < 8; k++)
        {
            var o = MathX.Dir(k * Mathf.Tau / 8) * 6f;
            sum += hf.Sample(x + o.X, z + o.Y);
        }
        return Mathf.Clamp((sum / 8 - c) * 1.2f, 0, 1);
    };
}
