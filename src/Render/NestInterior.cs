using System.Collections.Generic;
using Godot;

namespace Mandible;

/// A connected horizontal excavation beneath a translucent soil roof. Room contents sit on the same
/// floor as their galleries; the simulation retains its own graph and depths. Everything here is rendering only.
public sealed partial class NestInterior
{
    readonly GameSession _g;
    readonly Node3D _root;
    readonly Node3D[] _nest = new Node3D[2];
    readonly Dictionary<(int team, int id), Room> _rooms = new();
    readonly Label3D[] _status = new Label3D[2];
    readonly MeshInstance3D[] _plug = new MeshInstance3D[2];
    // the open entrance: nothing of it exists until the founders start digging (a site is only a site)
    readonly MeshInstance3D[] _mouth = new MeshInstance3D[2], _mouthShadow = new MeshInstance3D[2];
    readonly Node3D[] _mouthLight = new Node3D[2];
    readonly bool[] _holeCut = { false, false };
    readonly NestGeometry[] _geometry = { new(), new() };
    readonly MeshInstance3D[] _excavation = new MeshInstance3D[2];
    readonly Node3D[] _timber = new Node3D[2];
    static readonly Dictionary<string, ShaderMaterial> _mineMats = new(); // shoring, stones and roots in the banks, rebuilt with the excavation
    readonly int[] _meshRevision = { -1, -1 };
    readonly List<ShaderMaterial> _soils = new(), _shafts = new();
    float _refresh;
    int _tierStamp = -1;
    readonly NestLights _lights;

    /// Render layer of everything dug (the excavation and the rooms' contents). The sun's cull mask leaves it
    /// out, so the nest is lit by its lamps alone, never by daylight.
    public const uint UndergroundLayer = 2;
    public const uint SunMask = 0xFFFFFu & ~UndergroundLayer;

    /// The way the sun falls (world space): the shafts of light below ground follow it.
    static readonly Vector3 LightDir = (Basis.FromEuler(GameSession.SunAngles * (Mathf.Pi / 180f)) * Vector3.Forward).Normalized();

    sealed class Room
    {
        public NestChamber Ch;
        public int Team;
        public Node3D Node, Contents;
        public Label3D Label;
        public int ContentCount = -1, Tier, TierStamp = -1;
        public float ShownProgress = -1;
        public Bed[] Beds;            // a nursery's egg, larva and cocoon beds
    }

    public NestInterior(GameSession g)
    {
        _g = g;
        _root = new Node3D { Name = "NestInterior" };
        g.AddChild(_root);
        for (int team = 0; team < 2; team++) BuildNest(team);
        _lights = new NestLights(g, _root, SetPools);
        InitBrood();
        ApplyQuality();
    }

    /// Low tier: the cavity walls are unshaded, so the nearest lamps are handed to their shader as pools of light.
    void SetPools(Vector4[] pools)
    {
        foreach (var m in _soils)
            if (m.GetShaderParameter("cavity_surface").AsBool()) m.SetShaderParameter("lamps", pools);
    }

    static void MoveToUnderground(Node n)
    {
        if (n is VisualInstance3D v && v.Layers != UndergroundLayer) v.Layers = UndergroundLayer;
        foreach (var c in n.GetChildren()) MoveToUnderground(c);
    }

    float Ground(int team) { var p = _g.World.Nests[team].Pos; return _g.World.HF.Sample(p.X, p.Y); }

    /// One shared Earth shader per culling mode; a material per surface role, tuned by how much sun it sees.
    static Image _relief;

    /// The rock scan's displacement map, read once for the bank relief (nest-local point, repeats like the shader's wall tiling).
    static float Relief(Vector2 p)
    {
        if (_relief == null)
        {
            _relief = GD.Load<Texture2D>("res://assets/nature/ground/rock_face_03_disp.jpg").GetImage();
            if (_relief.IsCompressed()) _relief.Decompress();
        }
        int w = _relief.GetWidth(), h = _relief.GetHeight();
        float u = p.X * 0.32f, v = p.Y * 0.32f;
        int x = ((int)Mathf.Floor((u - Mathf.Floor(u)) * w)) % w, y = ((int)Mathf.Floor((v - Mathf.Floor(v)) * h)) % h;
        return _relief.GetPixel(x, y).R;
    }

    ShaderMaterial Soil(float gy, float shade, float bounce, float grooves, bool back, bool cavity = false)
    {
        var mode = cavity ? "cull_disabled, shadows_disabled, ambient_light_disabled" : back ? "cull_back" : "cull_disabled";
        var m = new ShaderMaterial { Shader = Shaders.Get(Shaders.Earth.Replace("cull_CULL", mode)) };
        m.SetShaderParameter("cavity_surface", cavity);
        m.SetShaderParameter("noise_b", _g.World.NoiseB);
        m.SetShaderParameter("normal_c", _g.World.NormalC);
        m.SetShaderParameter("soil_albedo", NatureAssets.Texture("ground/red_mud_stones_diff")); // photographed clay with embedded stones (CC0)
        if (cavity)
        {
            // the mine: Poly Haven rock_face_03 on the walls, coast_sand_05 on the floor (CC0, docs/asset-credits.md)
            m.SetShaderParameter("mine", true);
            m.SetShaderParameter("wall_albedo", NatureAssets.Texture("ground/rock_face_03_diff"));
            m.SetShaderParameter("wall_normal", NatureAssets.Texture("ground/rock_face_03_nor"));
            m.SetShaderParameter("wall_arm", NatureAssets.Texture("ground/rock_face_03_arm"));
            m.SetShaderParameter("floor_albedo", NatureAssets.Texture("ground/coast_sand_05_diff"));
            m.SetShaderParameter("floor_normal", NatureAssets.Texture("ground/coast_sand_05_nor"));
            NestGeometry.WallRelief ??= Relief;
        }
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
        var earth = Soil(gy, 0.94f, 0.12f, 0.45f, false, true);
        // The soil roof is a cutaway presentation. Its physical shadow map cannot
        // light the exposed underground floor: the bank shader supplies that shading.
        _excavation[team] = new MeshInstance3D
        {
            Name = "ConnectedExcavation", MaterialOverride = earth, Layers = UndergroundLayer,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false,
        };
        n.AddChild(_excavation[team]);
        // The entrance is the only genuinely open hole in the surface.
        // the shaft: the ragged funnel runs from the lip all the way down to the pit floor
        _mouth[team] = new MeshInstance3D { Mesh = RoughMouth(), MaterialOverride = Soil(gy, 0.65f, 0.2f, 1f, false), Scale = new Vector3(1, (NestGeometry.Depth - 0.2f) / 1.9f, 1) };
        n.AddChild(_mouth[team]);
        n.AddChild(_mouthShadow[team] = new MeshInstance3D
        {
            Name = "EntranceShadow", Mesh = new CylinderMesh { TopRadius = 0.8f, BottomRadius = 0.8f, Height = 0.03f, RadialSegments = 24 },
            Position = new Vector3(0, -NestGeometry.Depth + 0.08f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.035f, 0.018f, 0.007f) },
        });
        // the plug of soil the founders dig away (sinks and shrinks with progress)
        _plug[team] = new MeshInstance3D { Mesh = RoughLump(), Scale = new Vector3(1.25f, 0.3f, 1.25f), MaterialOverride = Soil(gy + 1.2f, 1.0f, 0.3f, 0.5f, false), Position = new Vector3(0, 0.25f, 0) };
        n.AddChild(_plug[team]);
        // the sun falling down the shaft
        n.AddChild(_mouthLight[team] = MakeShaft(team, new Vector3(0, 0.3f, 0), 0.9f, 1.8f, 6f));
        // status over the entrance
        _status[team] = new Label3D
        {
            Text = "", Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, FontSize = 36, OutlineSize = 14, PixelSize = 0.011f,
            // white on a heavy black outline: the old sand-gold vanished against the ground it floats over
            Modulate = Colors.White, OutlineModulate = new Color(0.01f, 0.008f, 0.005f, 1f), Font = UiTheme.Title,
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
        Shaders.SetNestOpen(strength);
        // with the earth opened for building, the chambers must read through the roof: a warm fill rises in the cavities
        // (the lamps still pool on top of it), so the cut never shows as dark blotches under the soil
        foreach (var m in _soils)
            if (m.GetShaderParameter("cavity_surface").AsBool()) { m.SetShaderParameter("ambient_scale", 0.1f + 0.5f * strength); m.SetShaderParameter("cutaway", strength); }
        foreach (var m in _mineMats.Values) m.SetShaderParameter("cutaway", strength);
        float shaft = Settings.Gfx >= Settings.Quality.Medium ? strength * 0.15f : 0f;
        foreach (var m in _shafts) m.SetShaderParameter("strength", shaft);
    }

    /// Rendering-only knobs that follow the graphics tier: soil detail, the light shafts, roots and prop counts.
    public void ApplyQuality()
    {
        int d = Settings.TerrainDetail;
        foreach (var m in _soils)
        {
            m.SetShaderParameter("detail", d);
            if (!m.GetShaderParameter("cavity_surface").AsBool()) continue;
            bool low = Settings.Gfx == Settings.Quality.Low;
            // The Low cutaway supplies its own inexpensive directional shading;
            // it does not depend on lighting buffers rebuilt when MSAA/SSAO change.
            m.Shader = Shaders.Get(Shaders.Earth.Replace("cull_CULL", low ? "cull_disabled, unshaded" : "cull_disabled, shadows_disabled, ambient_light_disabled"));
            m.SetShaderParameter("flat_lighting", low);
            m.SetShaderParameter("shade", low ? 0.7f : 0.94f); // Low is unshaded: a flat base dim enough for the lamp pools to show, bright enough to read
        }
        for (int team = 0; team < 2; team++) _meshRevision[team] = -1;
        _tierStamp = (int)Settings.Gfx; // rooms refill their props at the new density on the next refresh
    }

    public (float[] net, int count) Network(int team)
    {
        var geometry = _geometry[team];
        geometry.Refresh(_g.Sim.Col[team].Chambers);
        return (geometry.Network, geometry.Count);
    }

    /// Hit the same plane the colony is drawn on, rather than the terrain above it.
    public bool ScreenToColony(Vector2 screen, out Vector2 point)
    {
        var cam = _g.Camera.Cam;
        var from = cam.ProjectRayOrigin(screen);
        var dir = cam.ProjectRayNormal(screen);
        point = Vector2.Zero;
        if (dir.Y >= -0.001f) return false;
        float t = (Ground(Local.Me) - NestGeometry.Depth - from.Y) / dir.Y;
        if (t < 0) return false;
        var hit = from + dir * t;
        point = new Vector2(hit.X, hit.Z);
        return true;
    }

    public int PickRoom(Vector2 screen)
    {
        if (!ScreenToColony(screen, out var p)) return -1;
        int best = -1;
        float nearest = 1f;
        foreach (var ch in _g.Sim.Col[Local.Me].Chambers)
        {
            var (w, _, d) = RoomSize(ch);
            float progress = NestGeometry.Progress(ch);
            var q = (p - _g.Sim.ChamberPos(Local.Me, ch)) / new Vector2(w * progress, d * progress);
            if (q.LengthSquared() < nearest) { nearest = q.LengthSquared(); best = ch.Id; }
        }
        return best;
    }

    // ------------------------------------------------------------------ shared meshes
    // Ants do not dig with lathes: every opening is lumpy. Rough meshes are built once and shared.
    static readonly FastNoiseLite Lump = new() { Seed = 9, Frequency = 1.9f, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth };
    static ArrayMesh _roughMouth, _roughLump, _shaft;
    static ArrayMesh _egg, _larva, _cocoon, _seed, _drop, _grit, _thread, _husk, _pulp;

    static float Lumpy(Vector3 u) => 1f + 0.16f * Lump.GetNoise3D(u.X * 3, u.Y * 3, u.Z * 3) + 0.06f * Lump.GetNoise3D(u.X * 9 + 5, u.Y * 9, u.Z * 9);

    /// The same lumpy ellipsoid, solid: the plug of soil the founders dig away.
    static ArrayMesh RoughLump() => _roughLump ??= Build(k => k.Ellipsoid(Vector3.Zero, Basis.Identity, 26, 13, Colors.White, Vector2.Zero, Lumpy));

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
    static StandardMaterial3D MEgg => _mEgg ??= new() { DisableAmbientLight = true, EmissionEnabled = true, EmissionEnergyMultiplier = 0.26f, Emission = new Color(0.82f, 0.73f, 0.57f) * new Color(1f, 0.8f, 0.58f), AlbedoColor = new Color(0.82f, 0.73f, 0.57f), Roughness = 0.28f, SubsurfScatterEnabled = true, SubsurfScatterStrength = 0.18f };
    static StandardMaterial3D MLarva => _mLarva ??= new() { DisableAmbientLight = true, EmissionEnabled = true, EmissionEnergyMultiplier = 0.26f, Emission = new Color(0.8f, 0.68f, 0.48f) * new Color(1f, 0.8f, 0.58f), AlbedoColor = new Color(0.8f, 0.68f, 0.48f), Roughness = 0.4f, SubsurfScatterEnabled = true, SubsurfScatterStrength = 0.25f };
    static StandardMaterial3D MCocoon => _mCocoon ??= new() { DisableAmbientLight = true, EmissionEnabled = true, EmissionEnergyMultiplier = 0.26f, Emission = new Color(0.86f, 0.76f, 0.56f) * new Color(1f, 0.8f, 0.58f), AlbedoColor = new Color(0.86f, 0.76f, 0.56f), Roughness = 0.95f };
    static StandardMaterial3D MSeed => _mSeed ??= new() { DisableAmbientLight = true, EmissionEnabled = true, EmissionEnergyMultiplier = 0.26f, Emission = new Color(0.36f, 0.24f, 0.13f) * new Color(1f, 0.8f, 0.58f), AlbedoColor = new Color(0.36f, 0.24f, 0.13f), Roughness = 0.55f };
    static StandardMaterial3D MDrop => _mDrop ??= new() { DisableAmbientLight = true, AlbedoColor = new Color(0.98f, 0.72f, 0.25f, 0.85f), Roughness = 0.04f, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, EmissionEnabled = true, Emission = new Color(0.45f, 0.26f, 0.05f), Metallic = 0.1f, ClearcoatEnabled = true };
    static StandardMaterial3D MGrit => _mGrit ??= new() { DisableAmbientLight = true, EmissionEnabled = true, EmissionEnergyMultiplier = 0.26f, Emission = new Color(0.16f, 0.13f, 0.11f) * new Color(1f, 0.8f, 0.58f), AlbedoColor = new Color(0.16f, 0.13f, 0.11f), Roughness = 0.75f, Metallic = 0.25f };
    static StandardMaterial3D MQuartz => _mQuartz ??= new() { DisableAmbientLight = true, AlbedoColor = new Color(0.9f, 0.88f, 0.82f), Roughness = 0.2f, EmissionEnabled = true, Emission = new Color(0.12f, 0.11f, 0.1f) };
    static StandardMaterial3D MThread => _mThread ??= new() { DisableAmbientLight = true, AlbedoColor = new Color(0.95f, 0.94f, 0.88f), Roughness = 0.4f, EmissionEnabled = true, Emission = new Color(0.1f, 0.1f, 0.09f) };
    static StandardMaterial3D MHusk => _mHusk ??= new() { DisableAmbientLight = true, EmissionEnabled = true, EmissionEnergyMultiplier = 0.26f, Emission = new Color(0.85f, 0.68f, 0.4f, 0.55f) * new Color(1f, 0.8f, 0.58f), AlbedoColor = new Color(0.85f, 0.68f, 0.4f, 0.55f), Roughness = 0.25f, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled, ClearcoatEnabled = true };
    static StandardMaterial3D MJelly => _mJelly ??= new() { DisableAmbientLight = true, EmissionEnabled = true, EmissionEnergyMultiplier = 0.26f, Emission = new Color(0.69f, 0.37f, 0.055f) * new Color(1f, 0.8f, 0.58f), AlbedoColor = new Color(0.69f, 0.37f, 0.055f), Roughness = 0.12f, ClearcoatEnabled = true };

    static (float w, float h, float d) RoomSize(NestChamber ch) => NestGeometry.RoomSize(ch);

    Room MakeRoom(int team, NestChamber ch)
    {
        var room = new Room { Ch = ch, Team = team, Node = new Node3D { Name = $"Room_{ch.Kind}_{ch.Id}" } };
        _nest[team].AddChild(room.Node);
        room.Contents = new Node3D();
        room.Label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
            FontSize = 22, OutlineSize = 5, PixelSize = 0.00055f, FixedSize = true,
            Modulate = new Color(1f, 0.88f, 0.6f, 0.95f),
            OutlineModulate = new Color(0.05f, 0.03f, 0.02f, 0.85f), Font = UiTheme.Body,
        };
        room.Node.AddChild(room.Contents);
        room.Node.AddChild(room.Label);
        return room;
    }

    void LayoutRoom(Room room)
    {
        var ch = room.Ch;
        room.Node.Position = new Vector3(ch.Offset.X, -NestGeometry.Depth + 0.025f, ch.Offset.Y);
        room.Contents.Scale = Vector3.One * NestGeometry.Progress(ch) * 1.3f; // what a room holds is its meaning: shown large
        room.Label.Position = new Vector3(0, 1.1f, 0);
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
            ChamberKind.Brood => ch.Built ? 6 : 0, // the beds themselves follow the queue every frame
            ChamberKind.Storage => ch.Built ? 1 + (int)(fill * 14) : 0,
            ChamberKind.Royal => ch.Built ? 12 + 5 * ch.Tier : 0,
            _ => ch.Built ? 4 + 2 * ch.Tier : 0,
        };
        if (count == room.ContentCount && ch.Tier == room.Tier && room.TierStamp == _tierStamp) return;
        room.ContentCount = count;
        room.Tier = ch.Tier;
        room.TierStamp = _tierStamp;
        foreach (var c in room.Contents.GetChildren()) c.QueueFree();
        room.Beds = null;
        if (count == 0) return;
        float dens = Settings.Gfx switch { Settings.Quality.Low => 0.5f, Settings.Quality.Medium => 0.8f, Settings.Quality.High => 1f, _ => 1.3f };
        FillIdentity(room, dens);
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
            // before the founders dig, the site is plain ground: no hole, no plug, no light down a shaft
            bool dugInto = col.Founded || royal != null;
            _mouthLight[team].Visible = dugInto;
            _mouth[team].Visible = _mouthShadow[team].Visible = false; // the hole opens straight into the hub bowl: no shaft hangs in the pit
            _plug[team].Visible = !col.Founded && royal != null;
            if (dugInto != _holeCut[team])
            {
                // the terrain's own hole at the entrance opens with the dig, not before
                _holeCut[team] = dugInto;
                foreach (var m in _g.World.TerrainMaterials) m.SetShaderParameter(team == 0 ? "entrance_a" : "entrance_b", dugInto ? nestPos : new Vector2(-1000, -1000));
            }
            if (!col.Founded) { _plug[team].Position = new Vector3(0, 0.25f - dug * 1.1f, 0); _plug[team].Scale = new Vector3(1 - dug * 0.6f, 1, 1 - dug * 0.6f); }
            bool shown = mine && _open > 0.001f;
            _excavation[team].Visible = shown;
            if (shown)
            {
                var geometry = _geometry[team];
                geometry.Refresh(col.Chambers);
                if (_meshRevision[team] != geometry.Revision)
                {
                    var previous = _excavation[team].Mesh;
                    _excavation[team].Mesh = geometry.BuildMesh(Settings.Gfx == Settings.Quality.Low ? 0.34f : 0.24f);
                    previous?.Dispose();
                    _meshRevision[team] = geometry.Revision;
                    DressMine(team, geometry);
                }
            }
            foreach (var ch in col.Chambers)
            {
                if (!_rooms.TryGetValue((team, ch.Id), out var room)) _rooms[(team, ch.Id)] = room = MakeRoom(team, ch);
                // the underground is hidden under plain ground until the player opens it; the enemy's never shows
                room.Node.Visible = shown;
                if (room.ShownProgress != ch.Progress || room.Tier != ch.Tier) { LayoutRoom(room); room.ShownProgress = ch.Progress; }
                if (full)
                {
                    FillRoom(team, room);
                    MoveToUnderground(room.Contents);
                    string name = Sim.ChamberName(ch.Kind);
                    room.Label.Text = ch.Digging ? $"{name}  {ch.Progress * 100:0}%  ({ch.Diggers} digging)" : name;
                    room.Label.Modulate = ch.Digging ? new Color(1f, 0.75f, 0.35f) : new Color(1f, 0.88f, 0.6f, 0.95f);
                    room.Label.Visible = _g.Hud?.CommandHud == null && mine && nestView && col.Founded && (_g.Input?.SelectedRoom == ch.Id || ch.Digging);
                }
            }
            if (full)
            {
                // status over the entrance: the founding dig, then whatever is being dug below
                var lines = new List<string>();
                // the first nest's founding dig has its own panel at the top of the screen (FoundingPanel)
                bool panelShowsIt = !col.Founded && sim.Mission is { Episode: 1 };
                if (!col.Founded && !panelShowsIt) lines.Add($"Digging the nest  {dug * 100:0}%  ·  {royal?.Diggers ?? 0} workers");
                else foreach (var ch in col.Chambers) if (ch.Digging) lines.Add($"{Sim.ChamberName(ch.Kind)}  {ch.Progress * 100:0}%  ·  {ch.Diggers} digging");
                // no "digging 0%" floating over a site nobody is digging yet
                _status[team].Visible = mine && lines.Count > 0 && !nestView && (col.Founded || sim.Royal(team) != null);
                _status[team].Text = string.Join("\n", lines);
            }
        }
        UpdateBrood(alpha);
        _lights.Update(1f / 60f, _open);
    }

    /// The worked look of a mine, from the scans the yard already uses: twig props and lintels at every gallery
    /// mouth and along the long galleries (ants shore their tunnels with twigs), stones half-sunk in the banks, and
    /// a root crossing here and there. Everything sits on the excavation's own surface. Rebuilt with the mesh; the
    /// Low tier has none of it, Medium the mouths only.
    void DressMine(int team, NestGeometry geometry)
    {
        _timber[team]?.QueueFree();
        _timber[team] = null;
        if (Settings.Gfx == Settings.Quality.Low) return;
        var root = new Node3D { Name = "MineDressing" };
        _excavation[team].AddChild(root);
        _timber[team] = root;
        var twigs = NatureAssets.Kind("twig").FindAll(p => p.Src.StartsWith("dry_branches"));
        var stones = NatureAssets.Kind("stone");
        var roots = NatureAssets.Kind("root").FindAll(p => p.Src == "single_root");
        if (twigs.Count == 0 || stones.Count == 0) return;
        var rng = new Rng((uint)(0x7133u + (uint)team * 977u));
        var batches = new Dictionary<NatureAssets.Piece, List<(Transform3D t, Vector2 side)>>();
        // `side`: the outward direction of the bank the piece stands in (zero for a lintel, which spans both)
        void Add(NatureAssets.Piece p, Transform3D t, Vector2 side = default) { if (!batches.TryGetValue(p, out var l)) batches[p] = l = new(); l.Add((t, side)); }
        float Y(Vector2 p) => NestGeometry.Height(geometry.Distance(p), NestGeometry.FloorDepth(p));
        bool full = Settings.Gfx >= Settings.Quality.High;
        // a twig laid along `dir` from `at`, its length `len`, lying on the surface at `at` (or standing when `up` is set)
        void Twig(Vector2 at, float y, Vector3 axis, float len, float thick, Vector2 side = default)
        {
            var p = twigs[rng.I(0, twigs.Count)];
            var laid = (p.Lie * p.Size).Abs();
            float s = len / Mathf.Max(0.01f, laid.Z);
            float st = thick / Mathf.Max(0.01f, Mathf.Max(laid.X, laid.Y));
            var up = Mathf.Abs(axis.Dot(Vector3.Up)) > 0.98f ? Vector3.Right : Vector3.Up;
            var basis = Basis.LookingAt(axis, up) * new Basis(Vector3.Forward, rng.R(0, Mathf.Tau)) * Basis.FromScale(new Vector3(st, st, s)) * p.Lie;
            Add(p, new Transform3D(basis, new Vector3(at.X, y, at.Y)), side);
        }
        for (int i = 1; i < geometry.Count; i++)
        {
            int k = i * NestGeometry.Stride;
            var a = new Vector2(geometry.Network[k], geometry.Network[k + 1]);
            var b = new Vector2(geometry.Network[k + 2], geometry.Network[k + 3]);
            var radii = new Vector2(geometry.Network[k + 4], geometry.Network[k + 5]);
            float half = geometry.Network[k + 6];
            var ab = b - a;
            float len = ab.Length();
            if (len < 2.5f) continue;
            var dir = ab / len;
            var side = new Vector2(-dir.Y, dir.X);
            // frames along the gallery: at its mouth into the room and every few units on the way, each a pair of
            // uprights leaning into the banks and a lintel across the top, slightly askew like real shoring
            float roomEdge = len - Mathf.Min(radii.X, radii.Y) * 0.9f;
            var stations = new List<float> { Mathf.Clamp(roomEdge - 0.6f, 2.0f, len - 1f) };
            if (full) for (float d = 3.2f; d < roomEdge - 2.6f; d += rng.R(4.0f, 5.5f)) stations.Add(d);
            foreach (float d in stations)
            {
                var c = a + dir * d;
                float floorY = -NestGeometry.FloorDepth(c);
                float wobble = rng.R(-0.12f, 0.12f);
                float w = half + 0.35f;
                Vector2 l = c + side * (w + wobble), r = c - side * (w - wobble);
                float h = NestGeometry.FloorDepth(c) * rng.R(0.86f, 0.98f);
                var lean = new Vector3(side.X, 0, side.Y) * rng.R(0.06f, 0.16f);
                Twig(l, floorY + 0.05f, (Vector3.Up + lean).Normalized(), h, 0.55f, side);
                Twig(r, floorY + 0.05f, (Vector3.Up - lean).Normalized(), h, 0.55f, -side);
                var top = (l + r) * 0.5f;
                Twig(top, floorY + h - 0.2f, new Vector3(side.X, rng.R(-0.04f, 0.04f), side.Y).Normalized(), l.DistanceTo(r) + 0.7f, 0.5f);
            }
            if (!full) continue;
            // stones pressed into the banks on both sides, and the odd root breaking through near the lip
            for (float d = 0.8f; d < len - 0.5f; d += rng.R(0.9f, 1.7f))
            {
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    if (rng.F() > 0.55f) continue;
                    float off = half + rng.R(0.15f, 1.1f);
                    var at = a + dir * d + side * (sgn * off);
                    float y = Y(at);
                    var p = stones[rng.I(0, stones.Count)];
                    float sz = rng.R(0.5f, 1.3f);
                    float s = sz / Mathf.Max(0.01f, p.Footprint);
                    var basis = new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Right, rng.R(-0.5f, 0.5f)) * Basis.FromScale(Vector3.One * s);
                    Add(p, new Transform3D(basis, new Vector3(at.X, y - p.Size.Y * s * rng.R(0.3f, 0.55f), at.Y)), side * sgn);
                }
                if (roots.Count > 0 && rng.F() < 0.14f)
                {
                    var p = roots[rng.I(0, roots.Count)];
                    int rs = rng.F() < 0.5f ? -1 : 1;
                    var at = a + dir * d + side * rs * (half + 0.9f);
                    float y = Y(at) - 0.1f;
                    float s = rng.R(1.6f, 2.6f) / Mathf.Max(0.01f, p.Longest);
                    var basis = new Basis(Vector3.Up, dir.Angle() + rng.R(-0.4f, 0.4f)) * new Basis(Vector3.Right, rng.R(-0.3f, -0.05f)) * Basis.FromScale(Vector3.One * s) * p.Lie;
                    Add(p, new Transform3D(basis, new Vector3(at.X, y, at.Y)), side * rs);
                }
            }
        }
        foreach (var (piece, list) in batches)
        {
            // the shoring's own shader: the scan's maps, a warm fill (it stands above the lamps), and the build-mode fold
            if (!_mineMats.TryGetValue(piece.Src, out var mat))
            {
                var m = new ShaderMaterial { Shader = Shaders.Get(Shaders.Shoring) };
                m.SetShaderParameter("albedo_tex", NatureAssets.Texture($"tex/{piece.Src}_diff"));
                m.SetShaderParameter("normal_tex", NatureAssets.Texture($"tex/{piece.Src}_nor"));
                m.SetShaderParameter("arm_tex", NatureAssets.Texture($"tex/{piece.Src}_arm"));
                m.SetShaderParameter("tint", ((StandardMaterial3D)piece.Material).AlbedoColor);
                _mineMats[piece.Src] = mat = m;
            }
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = piece.Mesh, InstanceCount = list.Count };
            for (int i = 0; i < list.Count; i++) { mm.SetInstanceTransform(i, list[i].t); mm.SetInstanceCustomData(i, new Color(list[i].side.X, list[i].side.Y, 0, 0)); }
            root.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat, CastShadow = full ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off });
        }
        MoveToUnderground(root); // lit by the nest's lamps, like everything else dug
    }
}
