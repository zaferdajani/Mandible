using System.Collections.Generic;
using Godot;

namespace Mandible;

/// What lies under each mound: sloping galleries from the entrance to rooms at their own depths,
/// the rooms' contents (eggs, stores, royal jelly, trophies), lights, and the status over the
/// entrance while workers dig. Seen through the glass earth (N) or whenever the camera is low enough.
public sealed class NestInterior
{
    readonly GameSession _g;
    readonly Node3D _root;
    readonly Node3D[] _nest = new Node3D[2];
    readonly Dictionary<(int team, int id), Room> _rooms = new();
    readonly Label3D[] _status = new Label3D[2];
    readonly MeshInstance3D[] _plug = new MeshInstance3D[2];
    readonly StandardMaterial3D _wall, _floor, _earth;
    float _refresh;

    sealed class Room
    {
        public NestChamber Ch;
        public int Team;
        public Node3D Node, Contents;
        public MeshInstance3D Shell, Floor, Tunnel;
        public Label3D Label;
        public int ContentCount = -1, Tier;
        public float ShownProgress = -1;
    }

    public NestInterior(GameSession g)
    {
        _g = g;
        _root = new Node3D { Name = "NestInterior" };
        g.AddChild(_root);
        // real earth, lit by the one sun that comes in through the opened ground: the cut face of the soil, the
        // damp walls of the cavities and their trodden floors all carry the yard's own grain (the noise is grey,
        // so the base colours are set brighter than they read)
        var grain = g.World.NoiseB;
        // every underground surface is the ground's own dry-soil colour in shades: floors as light as the surface,
        // gallery walls darker, the deep earth darkest (the grey noise halves them, so the bases are doubled)
        var soil = new Color(0.84f, 0.63f, 0.44f);
        // the pit lies in the surface's own shadow for most of the day, so each surface carries a little warm bounce
        // light (emission) the way sunlit sand throws light into a hole; the sun still shapes whatever it reaches
        StandardMaterial3D Soil(float shade, float scale, BaseMaterial3D.CullModeEnum cull, float bounce, float rough = 1f) => new()
        {
            AlbedoColor = soil * shade, Roughness = rough, CullMode = cull, AlbedoTexture = grain, Uv1Triplanar = true, Uv1Scale = Vector3.One * scale,
            EmissionEnabled = true, Emission = new Color(0.42f, 0.30f, 0.19f) * shade * bounce,
        };
        _wall = Soil(0.78f, 0.6f, BaseMaterial3D.CullModeEnum.Disabled, 0.35f);
        _cavity = Soil(0.6f, 1.4f, BaseMaterial3D.CullModeEnum.Back, 0.35f);
        _floor = Soil(0.98f, 1.8f, BaseMaterial3D.CullModeEnum.Back, 0.3f, 0.95f);
        _earth = Soil(0.7f, 0.12f, BaseMaterial3D.CullModeEnum.Disabled, 0.6f); // the deep earth sees the least sun: the most bounce
        for (int team = 0; team < 2; team++) BuildNest(team);
    }

    readonly StandardMaterial3D _cavity;

    float Ground(int team) { var p = _g.World.Nests[team].Pos; return _g.World.HF.Sample(p.X, p.Y); }

    void BuildNest(int team)
    {
        var nest = _g.World.Nests[team].Pos;
        float gy = Ground(team);
        var n = new Node3D { Name = $"Nest{team}", Position = new Vector3(nest.X, gy, nest.Y) };
        _root.AddChild(n);
        _nest[team] = n;
        // the dark earth that fills the view through the glass: a bowl of soil below everything
        float r = Sim.NestReachMax + 12f;
        // shallow, like the nest itself now spreads: its floor stays in the sun, so the opened ground reads as a dug pit
        n.AddChild(new MeshInstance3D { Mesh = RoughBowl(), Scale = new Vector3(r, 7.5f, r), MaterialOverride = _earth, Position = new Vector3(0, -0.4f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        // the ragged mouth of the shaft under the entrance (its inside faces show; the lip is uneven)
        n.AddChild(new MeshInstance3D { Mesh = RoughMouth(), MaterialOverride = _wall, Position = new Vector3(0, 0, 0) });
        // the plug of soil the founders dig away (sinks and shrinks with progress)
        _plug[team] = new MeshInstance3D
        {
            Mesh = RoughLump(), Scale = new Vector3(1.25f, 0.3f, 1.25f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.84f, 0.63f, 0.44f) * 0.9f, Roughness = 1, AlbedoTexture = _g.World.NoiseB, Uv1Triplanar = true, Uv1Scale = new Vector3(1.5f, 1.5f, 1.5f) },
            Position = new Vector3(0, 0.25f, 0),
        };
        n.AddChild(_plug[team]);
        // status over the entrance
        _status[team] = new Label3D
        {
            Text = "", Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, FontSize = 36, OutlineSize = 10, PixelSize = 0.011f,
            Modulate = new Color(1f, 0.9f, 0.6f), OutlineModulate = new Color(0.05f, 0.03f, 0.02f, 0.85f), Font = UiTheme.Title,
            Position = new Vector3(0, 4.2f, 0), Visible = false,
        };
        n.AddChild(_status[team]);
        // the glass pane itself: a disc that follows the ground, drawn only while the earth is opened
        if (team == Local.Me)
        {
            float gr = Sim.NestReachMax + 6f; // the pane covers wherever the network may grow; its shader cuts it to the tunnels
            var k = new MeshKit();
            const int sides = 48, rings = 6;
            int c0 = k.Add(new Vector3(0, 0.06f, 0), Vector3.Up, Colors.White, Vector2.Zero, Vector2.Zero);
            for (int ri = 1; ri <= rings; ri++)
            {
                float rr = gr * ri / rings;
                for (int s = 0; s <= sides; s++)
                {
                    float t = (float)s / sides * Mathf.Tau;
                    var p = nest + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * rr;
                    k.Add(new Vector3(Mathf.Cos(t) * rr, _g.World.HF.Sample(p.X, p.Y) - gy + 0.06f, Mathf.Sin(t) * rr), Vector3.Up, Colors.White, Vector2.Zero, Vector2.Zero);
                }
            }
            int row = sides + 1;
            for (int s = 0; s < sides; s++) k.Tri(c0, 1 + s + 1, 1 + s);
            for (int ri = 1; ri < rings; ri++)
                for (int s = 0; s < sides; s++)
                {
                    int a = 1 + (ri - 1) * row + s, b = a + row;
                    k.Quad(a, a + 1, b + 1, b);
                }
            _glassMat = new ShaderMaterial { Shader = Shaders.Get(Shaders.Glass) };
            _glassMat.SetShaderParameter("radius", gr);
            _glassMat.SetShaderParameter("strength", 0f);
            n.AddChild(new MeshInstance3D { Mesh = k.Commit(), MaterialOverride = _glassMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, SortingOffset = 50 });
        }
    }

    ShaderMaterial _glassMat;

    /// How open the earth is (0..1), driven by the session's Glass Earth fade.
    public void SetGlass(float strength) => _glassMat?.SetShaderParameter("strength", OS.GetEnvironment("MANDIBLE_NOPANE") != "" ? 0f : strength);

    /// The tunnel network as the shaders see it: one capsule per gallery (parent node to room) and the room's own disc.
    /// The same packed array feeds the terrain cut and the pane, so both open exactly over the galleries.
    public void SetNetwork(float[] net, int count, Vector2 centre)
    {
        if (_glassMat == null) return;
        _glassMat.SetShaderParameter("net", net);
        _glassMat.SetShaderParameter("net_count", count);
        _glassMat.SetShaderParameter("xray_center", centre);
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

    // Ants do not dig with lathes: every opening is lumpy. Rough meshes are built once and shared.
    static readonly FastNoiseLite Lump = new() { Seed = 9, Frequency = 1.9f, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth };
    static ArrayMesh _roughRoom, _roughTube, _roughMouth, _roughFloor;

    /// A unit ellipsoid cavity with uneven walls (scaled per room), seen from inside: the ceiling nearest the
    /// camera is culled away, so from above you look down onto the lit floor and the far wall of the hollow.
    static ArrayMesh RoughRoom() => _roughRoom ??= Build(k =>
    {
        k.Ellipsoid(Vector3.Zero, Basis.Identity, 26, 13, Colors.White, Vector2.Zero,
            u => 1f + 0.16f * Lump.GetNoise3D(u.X * 3, u.Y * 3, u.Z * 3) + 0.06f * Lump.GetNoise3D(u.X * 9 + 5, u.Y * 9, u.Z * 9));
        InsideOut(k);
    }, false);

    /// The same lumpy ellipsoid, solid: the plug of soil the founders dig away.
    static ArrayMesh _roughLump;
    static ArrayMesh RoughLump() => _roughLump ??= Build(k => k.Ellipsoid(Vector3.Zero, Basis.Identity, 26, 13, Colors.White, Vector2.Zero,
        u => 1f + 0.16f * Lump.GetNoise3D(u.X * 3, u.Y * 3, u.Z * 3) + 0.06f * Lump.GetNoise3D(u.X * 9 + 5, u.Y * 9, u.Z * 9)));

    /// Flips a closed mesh so its inside is what is drawn and lit.
    static void InsideOut(MeshKit k)
    {
        k.RecalcNormals();
        for (int i = 0; i < k.Nrm.Count; i++) k.Nrm[i] = -k.Nrm[i];
        for (int i = 0; i + 2 < k.I.Count; i += 3) (k.I[i + 1], k.I[i + 2]) = (k.I[i + 2], k.I[i + 1]);
    }

    /// A unit-length tube along +Y (radius 1) whose walls bulge and pinch like a dug gallery, drawn from inside.
    static ArrayMesh RoughTube() => _roughTube ??= Build(k =>
    {
        TubeShell(k);
        InsideOut(k);
    }, false);

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

    /// The dark earth under a mound: a rough bowl from the rim (y = 0) down to y = -1, radius 1 at the rim
    /// and narrowing with depth. Only below ground, so the sky never turns to soil.
    static ArrayMesh _roughBowl;
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
        // the bottom
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

    static ArrayMesh Build(System.Action<MeshKit> f, bool recalc = true)
    {
        var k = new MeshKit();
        f(k);
        if (recalc) k.RecalcNormals();
        return k.Commit();
    }

    static (float w, float h, float d) RoomSize(NestChamber ch) => ch.Kind switch
    {
        ChamberKind.Royal => (3.4f + 0.3f * ch.Tier, 1.5f + 0.1f * ch.Tier, 2.8f + 0.2f * ch.Tier),
        ChamberKind.Brood => (2.6f, 1.2f, 2.1f),
        ChamberKind.Storage => (2.8f, 1.4f, 2.3f),
        _ => (2.3f + 0.2f * ch.Tier, 1.3f, 2.0f + 0.15f * ch.Tier),
    };

    Room MakeRoom(int team, NestChamber ch)
    {
        var room = new Room { Ch = ch, Team = team, Node = new Node3D { Name = $"Room_{ch.Kind}_{ch.Id}" } };
        _nest[team].AddChild(room.Node);
        // the cavities face inward, with back faces culled: the ceiling between the camera and the room is not drawn
        room.Shell = new MeshInstance3D { Mesh = RoughRoom(), MaterialOverride = _cavity, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        room.Floor = new MeshInstance3D { Mesh = RoughFloor(), MaterialOverride = _floor, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        room.Tunnel = new MeshInstance3D { Mesh = RoughTube(), MaterialOverride = _cavity, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        room.Contents = new Node3D();
        room.Label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, FontSize = 30, OutlineSize = 10, PixelSize = 0.02f,
            Modulate = new Color(1f, 0.88f, 0.6f, 0.95f), OutlineModulate = new Color(0.05f, 0.03f, 0.02f, 0.85f), Font = UiTheme.Title,
        };
        room.Node.AddChild(room.Shell);
        room.Node.AddChild(room.Floor);
        room.Node.AddChild(room.Contents);
        room.Node.AddChild(room.Label);
        _nest[team].AddChild(room.Tunnel);
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
        // the gallery from the node it was dug from (the entrance shaft or a parent room) to this room's mouth
        var chambers = _g.Sim.Col[room.Team].Chambers;
        var parent = ch.Parent >= 0 && ch.Parent < chambers.Count ? chambers[ch.Parent] : null;
        var from = parent == null ? new Vector3(0, -0.9f, 0) : new Vector3(parent.Offset.X, -parent.Depth * Mathf.Max(0.18f, parent.Progress) + 0.6f, parent.Offset.Y);
        var dir = new Vector3(ch.Offset.X - from.X, 0, ch.Offset.Y - from.Z);
        dir = dir.LengthSquared() < 0.01f ? Vector3.Forward : dir.Normalized();
        var to = centre + new Vector3(0, 0.6f, 0) - dir * (w * p * 0.3f); // ends inside the room's wall
        var mid = (from + to) * 0.5f;
        float len = from.DistanceTo(to);
        room.Tunnel.Position = mid;
        room.Tunnel.Scale = new Vector3(0.8f, len, 0.8f);
        var axis = (to - from).Normalized();
        var up = Mathf.Abs(axis.Dot(Vector3.Up)) > 0.98f ? Vector3.Right : Vector3.Up;
        room.Tunnel.Basis = Basis.LookingAt(axis, up) * new Basis(Vector3.Right, Mathf.Pi / 2);
        room.Label.Position = new Vector3(0, h + 0.9f + 0.45f * (ch.Id % 3), 0); // staggered so neighbours' names do not overlap
    }

    void FillRoom(int team, Room room)
    {
        var ch = room.Ch;
        var sim = _g.Sim;
        var col = sim.Col[team];
        int count = ch.Kind switch
        {
            ChamberKind.Brood => ch.Built ? 3 + System.Math.Min(9, col.Queue.Count * 3) : 0,
            ChamberKind.Storage => ch.Built ? (int)Mathf.Clamp(col.Food / col.FoodCap * 12, 0, 12) : 0,
            ChamberKind.Royal => ch.Built ? 2 + 2 * ch.Tier : 0,
            _ => ch.Built ? 4 + 2 * ch.Tier : 0,
        };
        if (count == room.ContentCount && ch.Tier == room.Tier) return;
        room.ContentCount = count;
        room.Tier = ch.Tier;
        foreach (var c in room.Contents.GetChildren()) c.QueueFree();
        var rng = new Rng((uint)(team * 131 + ch.Id * 17 + 5));
        var (w, _, d) = RoomSize(ch);
        for (int i = 0; i < count; i++)
        {
            Mesh mesh; Material mat; float s;
            switch (ch.Kind)
            {
                case ChamberKind.Brood: // eggs and larvae
                    mesh = new SphereMesh { Radius = 0.14f, Height = 0.42f, RadialSegments = 10, Rings = 6 };
                    mat = new StandardMaterial3D { AlbedoColor = new Color(0.96f, 0.94f, 0.85f), Roughness = 0.35f }; s = rng.R(0.8f, 1.2f); break;
                case ChamberKind.Storage: // seeds and crumbs
                    mesh = new SphereMesh { Radius = 0.22f, Height = 0.36f, RadialSegments = 10, Rings = 6 };
                    mat = new StandardMaterial3D { AlbedoColor = i % 3 == 0 ? new Color(0.85f, 0.7f, 0.45f) : new Color(0.62f, 0.46f, 0.28f), Roughness = 0.8f }; s = rng.R(0.8f, 1.3f); break;
                case ChamberKind.Royal: // royal jelly
                    mesh = new SphereMesh { Radius = 0.26f, Height = 0.4f, RadialSegments = 12, Rings = 6 };
                    mat = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.7f, 0.22f), Roughness = 0.05f, EmissionEnabled = true, Emission = new Color(0.3f, 0.18f, 0.03f) }; s = rng.R(0.8f, 1.2f); break;
                case ChamberKind.Forge:
                    mesh = new CapsuleMesh { Radius = 0.06f, Height = 0.7f, RadialSegments = 8, Rings = 4 };
                    mat = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.14f, 0.05f), Roughness = 0.2f, Metallic = 0.2f }; s = rng.R(0.8f, 1.3f); break;
                case ChamberKind.Armory:
                    mesh = new SphereMesh { Radius = 0.3f, Height = 0.16f, RadialSegments = 12, Rings = 5 };
                    mat = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.38f, 0.12f), Roughness = 0.15f }; s = rng.R(0.8f, 1.2f); break;
                case ChamberKind.Wings:
                    mesh = AntMesh.Wings(AntType.Flyer);
                    mat = new ShaderMaterial { Shader = Shaders.Get(Shaders.Wing) }; s = 1.4f; break;
                default:
                    mesh = AntMesh.Silk();
                    mat = new ShaderMaterial { Shader = Shaders.Get(Shaders.Silk) }; s = 1.6f; break;
            }
            var o = rng.InDisk(1f);
            var pos = new Vector3(o.X * w * 0.4f, 0.1f, o.Y * d * 0.4f);
            var basis = new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Right, ch.Kind == ChamberKind.Forge ? Mathf.Pi / 2 : 0) * Basis.FromScale(Vector3.One * s);
            room.Contents.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = mat, Transform = new Transform3D(basis, pos), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
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
                if (room.ShownProgress != ch.Progress || room.Tier != ch.Tier) { LayoutRoom(room); room.ShownProgress = ch.Progress; }
                if (full)
                {
                    FillRoom(team, room);
                    string name = ch.Kind == ChamberKind.Royal ? $"Royal Chamber · {Config.QueenTitles[System.Math.Min(5, col.T(Upgrade.Queen))]}" : Sim.ChamberName(ch.Kind);
                    room.Label.Text = ch.Digging ? $"{name}  {ch.Progress * 100:0}%  ({ch.Diggers} digging)" : name;
                    room.Label.Modulate = ch.Digging ? new Color(1f, 0.75f, 0.35f) : new Color(1f, 0.88f, 0.6f, 0.95f);
                    room.Label.Visible = mine && (nestView || ch.Digging && !col.Founded);
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
