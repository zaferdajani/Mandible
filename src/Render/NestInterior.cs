using System.Collections.Generic;
using Godot;

namespace Mandible;

/// What lies under each mound: sloping galleries from the entrance to rooms at their own depths, the rooms'
/// contents (eggs and larvae, stores, royal jelly, grit, shed husks, hung wings, silk), the sunlight falling
/// in, and the status over the entrance while workers dig. Seen through the glass earth (N) or whenever the
/// camera is low enough. Everything here is rendering only.
public sealed class NestInterior
{
    readonly GameSession _g;
    readonly Node3D _root;
    readonly Node3D[] _nest = new Node3D[2];
    readonly Dictionary<(int team, int id), Room> _rooms = new();
    readonly Label3D[] _status = new Label3D[2];
    readonly MeshInstance3D[] _plug = new MeshInstance3D[2];
    readonly ShaderMaterial[] _wall = new ShaderMaterial[2], _cavity = new ShaderMaterial[2], _floor = new ShaderMaterial[2], _earth = new ShaderMaterial[2];
    readonly List<ShaderMaterial> _soils = new(), _shafts = new();
    readonly List<MeshInstance3D> _shaftNodes = new();
    readonly List<MultiMeshInstance3D> _rootNodes = new();
    float _refresh;
    int _tierStamp = -1;

    /// The way the sun falls (world space): the shafts of light below ground follow it.
    static readonly Vector3 LightDir = (Basis.FromEuler(GameSession.SunAngles * (Mathf.Pi / 180f)) * Vector3.Forward).Normalized();

    sealed class Room
    {
        public NestChamber Ch;
        public int Team;
        public Node3D Node, Contents;
        public MeshInstance3D Shell, Floor, Tunnel, Shaft;
        public MultiMeshInstance3D Roots;
        public Label3D Label;
        public int ContentCount = -1, Tier, TierStamp = -1;
        public float ShownProgress = -1;
    }

    public NestInterior(GameSession g)
    {
        _g = g;
        _root = new Node3D { Name = "NestInterior" };
        g.AddChild(_root);
        for (int team = 0; team < 2; team++) BuildNest(team);
        ApplyQuality();
    }

    float Ground(int team) { var p = _g.World.Nests[team].Pos; return _g.World.HF.Sample(p.X, p.Y); }

    /// One shared Earth shader per culling mode; a material per surface role, tuned by how much sun it sees.
    ShaderMaterial Soil(float gy, float shade, float bounce, float grooves, bool back)
    {
        var m = new ShaderMaterial { Shader = Shaders.Get(Shaders.Earth.Replace("cull_CULL", back ? "cull_back" : "cull_disabled")) };
        m.SetShaderParameter("noise_b", _g.World.NoiseB);
        m.SetShaderParameter("normal_c", _g.World.NormalC);
        m.SetShaderParameter("ground_y", gy);
        m.SetShaderParameter("shade", shade);
        m.SetShaderParameter("bounce", bounce);
        m.SetShaderParameter("grooves", grooves);
        _soils.Add(m);
        return m;
    }

    void BuildNest(int team)
    {
        var nest = _g.World.Nests[team].Pos;
        float gy = Ground(team);
        var n = new Node3D { Name = $"Nest{team}", Position = new Vector3(nest.X, gy, nest.Y) };
        _root.AddChild(n);
        _nest[team] = n;
        _earth[team] = Soil(gy, 0.8f, 0.6f, 0.6f, false);   // the pit walls: three layers of soil, mostly in shadow
        _wall[team] = Soil(gy, 1.0f, 0.7f, 1.0f, false);    // the shaft mouth
        _cavity[team] = Soil(gy, 0.95f, 0.9f, 1.0f, true);  // galleries and room walls, seen from inside
        _floor[team] = Soil(gy, 0.6f, 0.3f, 0.0f, false);    // trodden floors (double-sided: the disc's winding must never cull it, or the bowl shows through)
        // the earth that fills the view through the glass: a shallow bowl of soil below everything, its floor in the sun
        float r = Sim.NestReachMax + 12f;
        n.AddChild(new MeshInstance3D { Mesh = RoughBowl(), Scale = new Vector3(r, 7.5f, r), MaterialOverride = _earth[team], Position = new Vector3(0, -0.4f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        // the ragged mouth of the shaft under the entrance (its inside faces show; the lip is uneven)
        n.AddChild(new MeshInstance3D { Mesh = RoughMouth(), MaterialOverride = _wall[team], Position = new Vector3(0, 0, 0) });
        // the plug of soil the founders dig away (sinks and shrinks with progress)
        _plug[team] = new MeshInstance3D { Mesh = RoughLump(), Scale = new Vector3(1.25f, 0.3f, 1.25f), MaterialOverride = Soil(gy + 1.2f, 1.0f, 0.3f, 0.5f, false), Position = new Vector3(0, 0.25f, 0) };
        n.AddChild(_plug[team]);
        // the sun falling down the shaft
        n.AddChild(MakeShaft(team, new Vector3(0, 0.3f, 0), 0.9f, 1.8f, 6f));
        // status over the entrance
        _status[team] = new Label3D
        {
            Text = "", Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, FontSize = 36, OutlineSize = 10, PixelSize = 0.011f,
            Modulate = new Color(1f, 0.9f, 0.6f), OutlineModulate = new Color(0.05f, 0.03f, 0.02f, 0.85f), Font = UiTheme.Title,
            Position = new Vector3(0, 4.2f, 0), Visible = false,
        };
        n.AddChild(_status[team]);
    }

    /// A cone of sunlight from `top` along the sun's direction, `r0` wide at the top, `r1` at the bottom.
    MeshInstance3D MakeShaft(int team, Vector3 top, float r0, float r1, float len, float gain = 1f)
    {
        var m = new ShaderMaterial { Shader = Shaders.Get(Shaders.Shaft) };
        m.SetShaderParameter("strength", 0f);
        m.SetShaderParameter("color", new Color(1f, 0.86f, 0.6f) * gain);
        if (team == Local.Me) _shafts.Add(m); // the enemy's nest is never opened
        var mi = new MeshInstance3D { Mesh = ShaftMesh(), MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, SortingOffset = 40 };
        PlaceShaft(mi, top, r0, r1, len);
        _shaftNodes.Add(mi);
        return mi;
    }

    static void PlaceShaft(MeshInstance3D mi, Vector3 top, float r0, float r1, float len)
    {
        var axis = LightDir;
        var up = Mathf.Abs(axis.Dot(Vector3.Up)) > 0.98f ? Vector3.Right : Vector3.Up;
        // the unit shaft runs along +Y from y = 0 (top, radius 1) to y = 1 (bottom, radius 2)
        mi.Basis = Basis.LookingAt(axis, up) * new Basis(Vector3.Right, Mathf.Pi / 2) * Basis.FromScale(new Vector3(r0, len, r0));
        mi.Position = top;
    }

    /// How open the earth is (0..1), driven by the session's Glass Earth fade. The sun shafts come with the glass.
    float _open;
    public void SetGlass(float strength)
    {
        if (OS.GetEnvironment("MANDIBLE_NOPANE") != "") strength = 0; // perf A/B: no shafts
        _open = strength;
        float shaft = Settings.Gfx >= Settings.Quality.Medium ? strength : 0f;
        foreach (var m in _shafts) m.SetShaderParameter("strength", shaft);
    }

    /// Rendering-only knobs that follow the graphics tier: soil detail, the light shafts, roots and prop counts.
    public void ApplyQuality()
    {
        int d = Settings.TerrainDetail;
        foreach (var m in _soils) m.SetShaderParameter("detail", d);
        bool rich = Settings.Gfx >= Settings.Quality.Medium;
        foreach (var r in _rootNodes) r.Visible = rich;
        _tierStamp = (int)Settings.Gfx; // rooms refill their props at the new density on the next refresh
    }

    readonly float[] _net = new float[224];

    /// Packs a colony's tunnel network for the shaders: the mouth, then one gallery per room from the node it
    /// was dug from (at that node's depth) down to the room (at its own depth), with the room's dug radius at
    /// its end. Rooms grow and sink in the cut as the diggers progress, matching LayoutRoom.
    public (float[] net, int count) Network(int team)
    {
        var sim = _g.Sim;
        var chambers = sim.Col[team].Chambers;
        var nest = _g.World.Nests[team].Pos;
        int n = 0;
        void Seg(Vector2 a, Vector2 b, float r, float depthA, float depthB)
        {
            if (n >= 32) return;
            int k = n * 7;
            _net[k] = a.X; _net[k + 1] = a.Y; _net[k + 2] = b.X; _net[k + 3] = b.Y; _net[k + 4] = r; _net[k + 5] = depthA; _net[k + 6] = depthB;
            n++;
        }
        static float DepthNow(NestChamber ch) => ch.Depth * Mathf.Max(0.18f, ch.Progress);
        Seg(nest, nest, 2.2f, 0, 0);
        if (sim.Col[team].Founded || sim.Royal(team) != null)
            foreach (var ch in chambers)
            {
                float p = Mathf.Max(0.18f, ch.Progress);
                var (w, _, d) = RoomSize(ch);
                var parent = ch.Parent >= 0 && ch.Parent < chambers.Count ? chambers[ch.Parent] : null;
                Seg(sim.NodePos(team, ch.Parent), sim.ChamberPos(team, ch), Mathf.Max(w, d) * p + 0.7f, parent == null ? 0 : DepthNow(parent), DepthNow(ch));
            }
        return (_net, n);
    }

    // ------------------------------------------------------------------ shared meshes
    // Ants do not dig with lathes: every opening is lumpy. Rough meshes are built once and shared.
    static readonly FastNoiseLite Lump = new() { Seed = 9, Frequency = 1.9f, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth };
    static ArrayMesh _roughRoom, _roughVault, _roughTube, _roughMouth, _roughFloor, _roughLump, _roughBowl, _shaft, _roots;
    static ArrayMesh _egg, _larva, _cocoon, _seed, _drop, _grit, _thread, _husk, _pulp;

    static float Lumpy(Vector3 u) => 1f + 0.16f * Lump.GetNoise3D(u.X * 3, u.Y * 3, u.Z * 3) + 0.06f * Lump.GetNoise3D(u.X * 9 + 5, u.Y * 9, u.Z * 9);

    /// A unit ellipsoid cavity with uneven walls (scaled per room), seen from inside: the ceiling nearest the
    /// camera is culled away, so from above you look down onto the lit floor and the far wall of the hollow.
    static ArrayMesh RoughRoom() => _roughRoom ??= Build(k => { k.Ellipsoid(Vector3.Zero, Basis.Identity, 26, 13, Colors.White, Vector2.Zero, Lumpy); InsideOut(k); }, false);

    /// The royal chamber: a broad vault, flatter and wider than the round rooms, with a higher crown.
    static ArrayMesh RoughVault() => _roughVault ??= Build(k =>
    {
        k.Ellipsoid(Vector3.Zero, Basis.Identity, 32, 15, Colors.White, Vector2.Zero, u =>
        {
            float d = Lumpy(u);
            // the ceiling rises in a dome, the floor flattens out
            if (u.Y > 0) d *= 1f + 0.25f * u.Y * u.Y; else d *= 1f - 0.35f * u.Y * u.Y;
            return d;
        });
        InsideOut(k);
    }, false);

    /// The same lumpy ellipsoid, solid: the plug of soil the founders dig away.
    static ArrayMesh RoughLump() => _roughLump ??= Build(k => k.Ellipsoid(Vector3.Zero, Basis.Identity, 26, 13, Colors.White, Vector2.Zero, Lumpy));

    /// Flips a closed mesh so its inside is what is drawn and lit.
    static void InsideOut(MeshKit k)
    {
        k.RecalcNormals();
        for (int i = 0; i < k.Nrm.Count; i++) k.Nrm[i] = -k.Nrm[i];
        for (int i = 0; i + 2 < k.I.Count; i += 3) (k.I[i + 1], k.I[i + 2]) = (k.I[i + 2], k.I[i + 1]);
    }

    /// A unit-length tube along +Y (radius 1) whose walls bulge and pinch like a dug gallery, drawn from inside.
    static ArrayMesh RoughTube() => _roughTube ??= Build(k => { TubeShell(k); InsideOut(k); }, false);

    static void TubeShell(MeshKit k)
    {
        const int sides = 16, rings = 10;
        int start = k.V.Count;
        for (int r = 0; r <= rings; r++)
        {
            float y = (float)r / rings - 0.5f;
            for (int s = 0; s <= sides; s++)
            {
                float t = (float)s / sides * Mathf.Tau;
                var dir = new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t));
                float rad = 1f + 0.22f * Lump.GetNoise3D(dir.X * 2.5f, y * 7f, dir.Z * 2.5f) + 0.08f * Lump.GetNoise3D(dir.X * 7, y * 20 + 3, dir.Z * 7);
                k.Add(new Vector3(dir.X * rad, y, dir.Z * rad), dir, Colors.White, new Vector2((float)s / sides, y), Vector2.Zero);
            }
        }
        int row = sides + 1;
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < sides; s++) { int a = start + r * row + s; k.Quad(a, a + 1, a + row + 1, a + row); }
    }

    /// The entrance: a ragged funnel with a crumbling lip, open at both ends.
    static ArrayMesh RoughMouth() => _roughMouth ??= Build(k =>
    {
        const int sides = 22, rings = 6;
        int start = k.V.Count;
        for (int r = 0; r <= rings; r++)
        {
            float v = (float)r / rings;           // 0 at the lip, 1 at the bottom
            float y = 0.25f - v * 1.9f;
            float baseR = Mathf.Lerp(1.35f, 0.75f, Mathf.Pow(v, 0.7f));
            for (int s = 0; s <= sides; s++)
            {
                float t = (float)s / sides * Mathf.Tau;
                var dir = new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t));
                float rough = 0.18f * Lump.GetNoise3D(dir.X * 3, v * 6, dir.Z * 3) + 0.1f * (1 - v) * Lump.GetNoise3D(dir.X * 11 + 2, 0, dir.Z * 11);
                float lipDrop = (1 - v) * 0.12f * Lump.GetNoise3D(dir.X * 5, 7, dir.Z * 5);
                k.Add(new Vector3(dir.X * (baseR + rough), y + lipDrop, dir.Z * (baseR + rough)), -dir, Colors.White, new Vector2((float)s / sides, v), Vector2.Zero);
            }
        }
        int row = sides + 1;
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < sides; s++) { int a = start + r * row + s; k.Quad(a, a + 1, a + row + 1, a + row); }
    });

    /// The earth under a mound: a rough bowl from the rim (y = 0) down to y = -1, radius 1 at the rim and
    /// narrowing with depth. Only below ground, so the sky never turns to soil.
    static ArrayMesh RoughBowl() => _roughBowl ??= Build(k =>
    {
        const int sides = 28, rings = 8;
        int start = k.V.Count;
        for (int r = 0; r <= rings; r++)
        {
            float v = (float)r / rings;
            float y = -v;
            float baseR = Mathf.Lerp(1f, 0.3f, v * v);
            for (int s = 0; s <= sides; s++)
            {
                float t = (float)s / sides * Mathf.Tau;
                var dir = new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t));
                float rad = baseR * (1f + 0.12f * Lump.GetNoise3D(dir.X * 2, v * 4, dir.Z * 2));
                k.Add(new Vector3(dir.X * rad, y, dir.Z * rad), -dir, Colors.White, new Vector2((float)s / sides, v), Vector2.Zero);
            }
        }
        int row = sides + 1;
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < sides; s++) { int a = start + r * row + s; k.Quad(a, a + 1, a + row + 1, a + row); }
        int centre = k.Add(new Vector3(0, -1, 0), Vector3.Up, Colors.White, new Vector2(0.5f, 0.5f), Vector2.Zero);
        int last = start + rings * row;
        for (int s = 0; s < sides; s++) k.Tri(centre, last + s, last + s + 1);
    });

    /// A trodden, uneven floor disc.
    static ArrayMesh RoughFloor() => _roughFloor ??= Build(k =>
    {
        const int sides = 24;
        int centre = k.Add(Vector3.Zero, Vector3.Up, Colors.White, new Vector2(0.5f, 0.5f), Vector2.Zero);
        int start = k.V.Count;
        for (int s = 0; s <= sides; s++)
        {
            float t = (float)s / sides * Mathf.Tau;
            var dir = new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t));
            float rad = 1f + 0.14f * Lump.GetNoise3D(dir.X * 3, 1, dir.Z * 3);
            k.Add(dir * rad + Vector3.Up * 0.03f * Lump.GetNoise3D(dir.X * 6, 2, dir.Z * 6), Vector3.Up, Colors.White, new Vector2(0.5f + dir.X * 0.5f, 0.5f + dir.Z * 0.5f), Vector2.Zero);
        }
        for (int s = 0; s < sides; s++) k.Tri(centre, start + s + 1, start + s);
    });

    /// A unit cone of light along +Y: radius 1 at y = 0 (UV.y 0), radius 2 at y = 1 (UV.y 1), open at both ends.
    static ArrayMesh ShaftMesh() => _shaft ??= Build(k =>
    {
        const int sides = 20;
        int start = k.V.Count;
        for (int i = 0; i <= sides; i++)
        {
            float t = (float)i / sides * Mathf.Tau;
            var dir = new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t));
            k.Add(dir, dir, Colors.White, new Vector2((float)i / sides, 0), Vector2.Zero);
            k.Add(dir * 2f + Vector3.Up, dir, Colors.White, new Vector2((float)i / sides, 1), Vector2.Zero);
        }
        for (int i = 0; i < sides; i++) { int a = start + i * 2; k.Quad(a, a + 2, a + 3, a + 1); }
    }, false);

    /// A few root fibres crossing a gallery (unit radius, y about 0): the diggers tunnelled around them.
    static ArrayMesh RootsMesh() => _roots ??= Build(k =>
    {
        var rng = new Rng(77);
        var c = new Color(1, 1, 1);
        for (int i = 0; i < 2; i++)
        {
            float a = rng.R(0, Mathf.Tau), y = rng.R(-0.2f, 0.45f);
            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var p0 = -d * 0.95f + new Vector3(0, y, 0);
            var p1 = d * 0.95f + new Vector3(0, y + rng.R(-0.2f, 0.2f), 0);
            var mid = (p0 + p1) * 0.5f + new Vector3(0, rng.R(-0.15f, 0.1f), 0) + d.Cross(Vector3.Up) * rng.R(-0.2f, 0.2f);
            k.Tube(p0, mid, 0.04f, 0.03f, 5, c, c, Vector2.Zero, false);
            k.Tube(mid, p1, 0.03f, 0.022f, 5, c, c, Vector2.Zero, false);
            // a rootlet branching off
            k.Tube(mid, mid + new Vector3(-d.Z, -0.35f, d.X) * 0.5f, 0.02f, 0.008f, 4, c, c, Vector2.Zero);
        }
    });

    // Props. Small shared meshes; every room scatters them in a MultiMesh of its own.
    static ArrayMesh Egg() => _egg ??= Build(k => k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(0.11f, 0.15f, 0.11f)), 10, 6, Colors.White, Vector2.Zero));
    static ArrayMesh Larva() => _larva ??= Build(k => k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(0.12f, 0.11f, 0.26f)), 12, 7, Colors.White, Vector2.Zero,
        u => 1f + 0.08f * Mathf.Sin(u.Z * 14f) * (1 - u.Y * u.Y) - 0.1f * Mathf.Max(0, u.Z) * u.Y)); // segmented, curled a little
    static ArrayMesh Cocoon() => _cocoon ??= Build(k => k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(0.15f, 0.14f, 0.3f)), 12, 7, Colors.White, Vector2.Zero,
        u => 1f + 0.03f * Lump.GetNoise3D(u.X * 20, u.Y * 20, u.Z * 20)));
    static ArrayMesh Seed() => _seed ??= Build(k => k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(0.14f, 0.1f, 0.24f)), 12, 7, new Color(1, 1, 1), Vector2.Zero, u => 1 - Mathf.Max(0, u.Z) * 0.3f));
    static ArrayMesh Drop() => _drop ??= Build(k => k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(0.17f, 0.22f, 0.17f)), 12, 7, Colors.White, Vector2.Zero, u => 1f - 0.25f * Mathf.Max(0, u.Y) * u.Y));
    static ArrayMesh Grit() => _grit ??= Build(k => k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(0.11f, 0.07f, 0.09f)), 8, 5, Colors.White, Vector2.Zero, u => 1 + 0.3f * Lump.GetNoise3D(u.X * 4, u.Y * 4, u.Z * 4)));
    static ArrayMesh Pulp() => _pulp ??= Build(k =>
    {
        var rng = new Rng(31);
        for (int i = 0; i < 9; i++)
        {
            var o = rng.InDisk(0.22f);
            k.Ellipsoid(new Vector3(o.X, rng.R(0.08f, 0.3f), o.Y), Basis.FromScale(Vector3.One * rng.R(0.1f, 0.15f)), 10, 6, new Color(rng.R(0.7f, 1f), 0, 0, 1), Vector2.Zero);
        }
    });
    static ArrayMesh Thread() => _thread ??= Build(k => k.Tube(new Vector3(0, 0, -0.5f), new Vector3(0, 0, 0.5f), 0.018f, 0.018f, 5, Colors.White, Colors.White, Vector2.Zero, false));
    /// A shed exoskeleton: the worker's own shape, drawn hollow and pale.
    static ArrayMesh Husk() => _husk ??= AntMesh.Build(AntType.Worker);

    static ArrayMesh Build(System.Action<MeshKit> f, bool recalc = true)
    {
        var k = new MeshKit();
        f(k);
        if (recalc) k.RecalcNormals();
        return k.Commit();
    }

    // prop materials, shared
    static StandardMaterial3D _mEgg, _mLarva, _mCocoon, _mSeed, _mDrop, _mGrit, _mQuartz, _mThread, _mHusk, _mJelly;
    static StandardMaterial3D MEgg => _mEgg ??= new() { AlbedoColor = new Color(0.97f, 0.95f, 0.88f), Roughness = 0.4f, SubsurfScatterEnabled = true, SubsurfScatterStrength = 0.6f };
    static StandardMaterial3D MLarva => _mLarva ??= new() { AlbedoColor = new Color(0.95f, 0.88f, 0.72f), Roughness = 0.5f, SubsurfScatterEnabled = true, SubsurfScatterStrength = 0.8f };
    static StandardMaterial3D MCocoon => _mCocoon ??= new() { AlbedoColor = new Color(0.86f, 0.76f, 0.56f), Roughness = 0.95f };
    static StandardMaterial3D MSeed => _mSeed ??= new() { AlbedoColor = new Color(0.36f, 0.24f, 0.13f), Roughness = 0.55f };
    static StandardMaterial3D MDrop => _mDrop ??= new() { AlbedoColor = new Color(0.98f, 0.72f, 0.25f, 0.85f), Roughness = 0.04f, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, EmissionEnabled = true, Emission = new Color(0.45f, 0.26f, 0.05f), Metallic = 0.1f, ClearcoatEnabled = true };
    static StandardMaterial3D MGrit => _mGrit ??= new() { AlbedoColor = new Color(0.16f, 0.13f, 0.11f), Roughness = 0.75f, Metallic = 0.25f };
    static StandardMaterial3D MQuartz => _mQuartz ??= new() { AlbedoColor = new Color(0.9f, 0.88f, 0.82f), Roughness = 0.2f, EmissionEnabled = true, Emission = new Color(0.12f, 0.11f, 0.1f) };
    static StandardMaterial3D MThread => _mThread ??= new() { AlbedoColor = new Color(0.95f, 0.94f, 0.88f), Roughness = 0.4f, EmissionEnabled = true, Emission = new Color(0.1f, 0.1f, 0.09f) };
    static StandardMaterial3D MHusk => _mHusk ??= new() { AlbedoColor = new Color(0.85f, 0.68f, 0.4f, 0.55f), Roughness = 0.25f, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled, ClearcoatEnabled = true };
    static StandardMaterial3D _mRoot;
    static StandardMaterial3D MRoot => _mRoot ??= new() { AlbedoColor = new Color(0.66f, 0.52f, 0.34f), Roughness = 0.95f };
    static StandardMaterial3D MJelly => _mJelly ??= new() { AlbedoColor = new Color(0.95f, 0.7f, 0.22f), Roughness = 0.05f, EmissionEnabled = true, Emission = new Color(0.3f, 0.18f, 0.03f) };

    static (float w, float h, float d) RoomSize(NestChamber ch) => ch.Kind switch
    {
        ChamberKind.Royal => (3.8f + 0.3f * ch.Tier, 1.7f + 0.12f * ch.Tier, 3.0f + 0.2f * ch.Tier),
        ChamberKind.Brood => (2.6f, 1.2f, 2.1f),
        ChamberKind.Storage => (2.8f, 1.4f, 2.3f),
        _ => (2.3f + 0.2f * ch.Tier, 1.3f, 2.0f + 0.15f * ch.Tier),
    };

    Room MakeRoom(int team, NestChamber ch)
    {
        var room = new Room { Ch = ch, Team = team, Node = new Node3D { Name = $"Room_{ch.Kind}_{ch.Id}" } };
        _nest[team].AddChild(room.Node);
        // the cavities face inward, with back faces culled: the ceiling between the camera and the room is not drawn
        room.Shell = new MeshInstance3D { Mesh = ch.Kind == ChamberKind.Royal ? RoughVault() : RoughRoom(), MaterialOverride = _cavity[team], CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        room.Floor = new MeshInstance3D { Mesh = RoughFloor(), MaterialOverride = _floor[team], CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        room.Tunnel = new MeshInstance3D { Mesh = RoughTube(), MaterialOverride = _cavity[team], CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        room.Contents = new Node3D();
        room.Label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, FontSize = 30, OutlineSize = 10, PixelSize = 0.00085f, FixedSize = true, // a caption's size on screen, whatever the zoom
            Modulate = new Color(1f, 0.88f, 0.6f, 0.95f), OutlineModulate = new Color(0.05f, 0.03f, 0.02f, 0.85f), Font = UiTheme.Title,
        };
        room.Node.AddChild(room.Shell);
        room.Node.AddChild(room.Floor);
        room.Node.AddChild(room.Contents);
        room.Node.AddChild(room.Label);
        _nest[team].AddChild(room.Tunnel);
        // root fibres the diggers went round, three to a gallery
        var rm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = RootsMesh(), InstanceCount = 2 };
        room.Roots = new MultiMeshInstance3D { Multimesh = rm, MaterialOverride = MRoot, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = Settings.Gfx >= Settings.Quality.Medium };
        _nest[team].AddChild(room.Roots);
        _rootNodes.Add(room.Roots);
        // the sun comes into every opened room from above: a soft shaft (the wing chamber's is the brightest)
        room.Shaft = MakeShaft(team, Vector3.Zero, 0.6f, 1.2f, 2f, ch.Kind == ChamberKind.Wings ? 2.2f : 1f);
        room.Node.AddChild(room.Shaft);
        // no lamps below ground: the one sun lights the rooms through the glass, so every shadow agrees
        return room;
    }

    void LayoutRoom(Room room)
    {
        var ch = room.Ch;
        float p = Mathf.Max(0.18f, ch.Progress);
        var (w, h, d) = RoomSize(ch);
        float depth = ch.Depth * p; // the room sinks as it is dug
        var centre = new Vector3(ch.Offset.X, -depth, ch.Offset.Y);
        room.Node.Position = centre;
        room.Shell.Scale = new Vector3(w * p, h * p, d * p);
        room.Shell.Position = new Vector3(0, h * p * 0.45f, 0);
        room.Floor.Scale = new Vector3(w * p * 0.92f, 1, d * p * 0.92f);
        room.Floor.Position = new Vector3(0, 0.02f, 0);
        // the gallery from the node it was dug from (the entrance shaft or a parent room) to this room's mouth,
        // about one and a half ant-widths across
        var chambers = _g.Sim.Col[room.Team].Chambers;
        var parent = ch.Parent >= 0 && ch.Parent < chambers.Count ? chambers[ch.Parent] : null;
        var from = parent == null ? new Vector3(0, -0.9f, 0) : new Vector3(parent.Offset.X, -parent.Depth * Mathf.Max(0.18f, parent.Progress) + 0.6f, parent.Offset.Y);
        var dir = new Vector3(ch.Offset.X - from.X, 0, ch.Offset.Y - from.Z);
        dir = dir.LengthSquared() < 0.01f ? Vector3.Forward : dir.Normalized();
        var to = centre + new Vector3(0, 0.6f, 0) - dir * (w * p * 0.3f); // ends inside the room's wall
        var mid = (from + to) * 0.5f;
        float len = from.DistanceTo(to);
        const float gal = 0.68f;
        room.Tunnel.Position = mid;
        room.Tunnel.Scale = new Vector3(gal, len, gal);
        var axis = (to - from).Normalized();
        var up = Mathf.Abs(axis.Dot(Vector3.Up)) > 0.98f ? Vector3.Right : Vector3.Up;
        var tb = Basis.LookingAt(axis, up) * new Basis(Vector3.Right, Mathf.Pi / 2);
        room.Tunnel.Basis = tb;
        room.Roots.Visible = Settings.Gfx >= Settings.Quality.Medium && len > 3f;
        for (int i = 0; i < 2; i++)
        {
            var at = from + (to - from) * (0.35f + 0.3f * i);
            room.Roots.Multimesh.SetInstanceTransform(i, new Transform3D(tb * new Basis(Vector3.Up, 1.3f * i + ch.Id) * Basis.FromScale(Vector3.One * gal), at));
        }
        // the shaft of light hangs from the room's crown
        PlaceShaft(room.Shaft, new Vector3(0, h * p * 0.95f, 0), 0.5f * w * p * 0.5f, w * p * 0.6f, h * p * 1.4f);
        room.Label.Position = new Vector3(0, h + 0.9f + 0.45f * (ch.Id % 3), 0); // staggered so neighbours' names do not overlap
    }

    /// Scatters `count` copies of a prop in one MultiMesh under `parent`.
    static void Scatter(Node parent, Mesh mesh, Material mat, int count, System.Func<int, Transform3D> place)
    {
        if (count <= 0) return;
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = count };
        for (int i = 0; i < count; i++) mm.SetInstanceTransform(i, place(i));
        parent.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// What each room holds, so a glance says what it is for. Counts follow the colony (brood queued, stores
    /// filled, tier) and the graphics tier.
    void FillRoom(int team, Room room)
    {
        var ch = room.Ch;
        var sim = _g.Sim;
        var col = sim.Col[team];
        float fill = ch.Kind == ChamberKind.Storage ? Mathf.Clamp(col.Food / Mathf.Max(1, col.FoodCap), 0, 1) : 0;
        int count = ch.Kind switch
        {
            ChamberKind.Brood => ch.Built ? 6 + System.Math.Min(18, col.Queue.Count * 4) : 0,
            ChamberKind.Storage => ch.Built ? (int)(fill * 14) : 0,
            ChamberKind.Royal => ch.Built ? 12 + 5 * ch.Tier : 0,
            _ => ch.Built ? 4 + 2 * ch.Tier : 0,
        };
        if (count == room.ContentCount && ch.Tier == room.Tier && room.TierStamp == _tierStamp) return;
        room.ContentCount = count;
        room.Tier = ch.Tier;
        room.TierStamp = _tierStamp;
        foreach (var c in room.Contents.GetChildren()) c.QueueFree();
        if (count == 0) return;
        float dens = Settings.Gfx switch { Settings.Quality.Low => 0.5f, Settings.Quality.Medium => 0.8f, Settings.Quality.High => 1f, _ => 1.3f };
        int N(float n) => Mathf.Max(1, Mathf.RoundToInt(n * dens));
        var rng = new Rng((uint)(team * 131 + ch.Id * 17 + 5));
        var (w, h, d) = RoomSize(ch);
        var node = room.Contents;
        // a spot on the floor, biased toward the far half of the room (the near half is where the ants come in)
        Vector3 Floor(float spread = 0.42f, float lift = 0f) { var o = rng.InDisk(1f); return new Vector3(o.X * w * spread, lift, o.Y * d * spread); }
        Basis Yaw(float s = 1f) => new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * Basis.FromScale(Vector3.One * s);
        // a spot on the room's wall or ceiling, for things that cling
        Vector3 Wall(float minY)
        {
            var o = rng.InDisk(1f);
            var u = new Vector3(o.X, rng.R(minY, 1f), o.Y).Normalized();
            return new Vector3(u.X * w * 0.9f, h * 0.45f + u.Y * h * 0.9f, u.Z * d * 0.9f);
        }
        switch (ch.Kind)
        {
            case ChamberKind.Brood:
                // eggs in a heap, larvae beside them, cocoons along the wall
                Scatter(node, Egg(), MEgg, N(count), _ => new Transform3D(Yaw(rng.R(0.85f, 1.15f)), Floor(0.3f, 0.13f)));
                Scatter(node, Larva(), MLarva, N(count * 0.4f), _ => new Transform3D(Yaw(rng.R(0.9f, 1.2f)), Floor(0.4f, 0.1f)));
                Scatter(node, Cocoon(), MCocoon, N(count * 0.3f), _ => new Transform3D(Yaw(rng.R(0.9f, 1.1f)), Floor(0.46f, 0.13f)));
                break;
            case ChamberKind.Storage:
                // seeds heaped on the floor, berry pulp beside them, honeydew drops glistening in the walls
                Scatter(node, Seed(), MSeed, N(count), _ => new Transform3D(Yaw(rng.R(0.9f, 1.3f)), Floor(0.4f, 0.09f)));
                if (fill > 0.35f) Scatter(node, Pulp(), FoodMat(1), 1, _ => new Transform3D(Yaw(1.3f), Floor(0.35f)));
                Scatter(node, Drop(), MDrop, N(3 + fill * 7), _ => new Transform3D(Yaw(rng.R(0.8f, 1.2f)), Wall(0.15f)));
                break;
            case ChamberKind.Royal:
                // the queen rests on a bed of eggs; royal jelly glows beside her
                Scatter(node, Egg(), MEgg, N(count), _ => new Transform3D(Yaw(rng.R(0.85f, 1.15f)), Floor(0.34f, 0.13f)));
                Scatter(node, Larva(), MLarva, N(count * 0.25f), _ => new Transform3D(Yaw(), Floor(0.42f, 0.1f)));
                Scatter(node, Drop(), MJelly, N(2 + 2 * ch.Tier), _ => new Transform3D(Yaw(rng.R(1.1f, 1.5f)), Floor(0.44f, 0.2f)));
                break;
            case ChamberKind.Forge:
                // a heap of dark grit, a few quartz grains glinting in it
                Scatter(node, Grit(), MGrit, N(28 + 10 * ch.Tier), _ => { var o = rng.InDisk(1f) * rng.F(); return new Transform3D(Yaw(rng.R(0.8f, 1.5f)), new Vector3(o.X * w * 0.3f, 0.07f + (1 - o.Length()) * 0.28f, o.Y * d * 0.3f)); });
                Scatter(node, Grit(), MQuartz, N(3 + ch.Tier), _ => new Transform3D(Yaw(rng.R(0.9f, 1.3f)), Floor(0.3f, 0.2f)));
                break;
            case ChamberKind.Armory:
                // shed exoskeletons sorted along the walls, pale and hollow
                Scatter(node, Husk(), MHusk, N(count), _ =>
                {
                    bool over = rng.F() < 0.4f;
                    var b = Yaw(rng.R(0.8f, 1.05f)) * new Basis(Vector3.Forward, over ? Mathf.Pi : rng.R(-0.5f, 0.5f));
                    return new Transform3D(b, Floor(0.46f, over ? 0.3f : 0.02f));
                });
                break;
            case ChamberKind.Wings:
                // wings hung from the ceiling to dry, in pairs
                Scatter(node, AntMesh.Wings(AntType.Flyer), new ShaderMaterial { Shader = Shaders.Get(Shaders.Wing) }, N(count), _ => new Transform3D(Yaw(1.4f) * new Basis(Vector3.Right, Mathf.Pi / 2), Wall(0.55f) + Vector3.Down * 0.25f));
                break;
            default:
                // silk cutters: threads strung across the room, cocoons waiting to be sheared
                Scatter(node, Thread(), MThread, N(count * 2), _ =>
                {
                    var a = Wall(-0.1f); var b = Wall(-0.1f);
                    var mid = (a + b) * 0.5f; var ax = b - a; float len = ax.Length();
                    if (len < 0.1f) return new Transform3D(Basis.Identity, mid);
                    return new Transform3D(Basis.LookingAt(ax / len, Mathf.Abs(ax.Normalized().Y) > 0.98f ? Vector3.Right : Vector3.Up) * Basis.FromScale(new Vector3(1, 1, len)), mid);
                });
                Scatter(node, Cocoon(), MCocoon, N(count * 0.8f), _ => new Transform3D(Yaw(rng.R(0.9f, 1.2f)), Floor(0.42f, 0.13f)));
                break;
        }
    }

    static readonly Dictionary<int, ShaderMaterial> _food = new();
    static ShaderMaterial FoodMat(int kind)
    {
        if (!_food.TryGetValue(kind, out var m)) { _food[kind] = m = new ShaderMaterial { Shader = Shaders.Get(Shaders.Food) }; m.SetShaderParameter("kind", kind); }
        return m;
    }

    public void Update(FogOfWar fog, bool nestView, float alpha)
    {
        _refresh -= 1f / 60f;
        bool full = _refresh <= 0;
        if (full) _refresh = 0.25f;
        var sim = _g.Sim;
        for (int team = 0; team < 2; team++)
        {
            var col = sim.Col[team];
            var nestPos = _g.World.Nests[team].Pos;
            bool mine = team == Local.Me;
            // enemy interiors exist too, but you only ever see them with ants of your own inside (fog) and never through glass
            _nest[team].Visible = mine || fog == null || fog.Explored(nestPos);
            // the founders' plug
            var royal = sim.Royal(team);
            float dug = royal == null ? 0 : royal.Progress;
            _plug[team].Visible = !col.Founded;
            if (!col.Founded) { _plug[team].Position = new Vector3(0, 0.25f - dug * 1.1f, 0); _plug[team].Scale = new Vector3(1 - dug * 0.6f, 1, 1 - dug * 0.6f); }
            foreach (var ch in col.Chambers)
            {
                if (!_rooms.TryGetValue((team, ch.Id), out var room)) _rooms[(team, ch.Id)] = room = MakeRoom(team, ch);
                // the underground is hidden under plain ground until the player opens it; the enemy's never shows
                bool shown = mine && _open > 0.001f;
                room.Node.Visible = shown; room.Tunnel.Visible = shown; room.Roots.Visible = shown && Settings.Gfx >= Settings.Quality.Medium;
                if (room.ShownProgress != ch.Progress || room.Tier != ch.Tier) { LayoutRoom(room); room.ShownProgress = ch.Progress; }
                if (full)
                {
                    FillRoom(team, room);
                    string name = ch.Kind == ChamberKind.Royal ? $"Royal Chamber · {Config.QueenTitles[System.Math.Min(5, col.T(Upgrade.Queen))]}" : Sim.ChamberName(ch.Kind);
                    room.Label.Text = ch.Digging ? $"{name}  {ch.Progress * 100:0}%  ({ch.Diggers} digging)" : name;
                    room.Label.Modulate = ch.Digging ? new Color(1f, 0.75f, 0.35f) : new Color(1f, 0.88f, 0.6f, 0.95f);
                    room.Label.Visible = mine && nestView && col.Founded; // while founding, the status over the entrance says it all
                }
            }
            if (full)
            {
                // status over the entrance: the founding dig, then whatever is being dug below
                var lines = new List<string>();
                if (!col.Founded) lines.Add($"Digging the nest  {dug * 100:0}%  ·  {royal?.Diggers ?? 0} workers");
                else foreach (var ch in col.Chambers) if (ch.Digging) lines.Add($"{Sim.ChamberName(ch.Kind)}  {ch.Progress * 100:0}%  ·  {ch.Diggers} digging");
                _status[team].Visible = mine && lines.Count > 0 && !nestView;
                _status[team].Text = string.Join("\n", lines);
            }
        }
    }
}
