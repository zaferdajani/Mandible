using System;
using System.Collections.Generic;
using Godot;
using GEnv = Godot.Environment;

namespace Mandible;

/// The colony map's world: a real cut through the earth, rendered by the engine behind the map's UI. An orthographic
/// camera looks at a wall of soil (the nest soil photograph, darkening from topsoil through loam to subsoil) lit
/// warm and low from the surface. Chambers and galleries are hollows carved into it; won chambers hold the props of
/// what was won there; game ants walk the dug galleries; locked chambers are undug soil with a faint seam. One world
/// unit is `Px` screen pixels, so the map's 2D layout (LevelMap.Spot) places everything.
public partial class NestMapScene : SubViewport
{
    public const float Px = 20f;
    const float Depth = 1.0f;          // how deep the galleries and chambers are cut into the wall
    const float TunnelR = 0.55f;

    public LevelMap Map;
    Node3D _root;
    MultiMesh _ants;
    readonly List<(float offset, float speed, int seed)> _walkers = new();
    Vector3[] _path = Array.Empty<Vector3>();
    float[] _dist = Array.Empty<float>();
    float _len, _t;
    OmniLight3D _selLight, _nextLight;
    Camera3D _cam;

    static Vector3 W(Vector2 px, float z = 0) => new(px.X / Px, -px.Y / Px, z);

    public void Build(Vector2 size)
    {
        Size = new Vector2I((int)size.X, (int)size.Y);
        OwnWorld3D = true; Msaa3D = Msaa.Msaa4X; TransparentBg = false;
        RenderTargetUpdateMode = UpdateMode.Always;
        _root?.QueueFree();
        _root = new Node3D();
        AddChild(_root);
        _walkers.Clear();

        var soilTex = GD.Load<Texture2D>("res://assets/materials/nest-soil-albedo.png");
        _root.AddChild(new WorldEnvironment
        {
            Environment = new GEnv
            {
                BackgroundMode = GEnv.BGMode.Color, BackgroundColor = new Color(0.36f, 0.2f, 0.09f),
                AmbientLightSource = GEnv.AmbientSource.Color, AmbientLightColor = new Color(0.62f, 0.44f, 0.3f), AmbientLightEnergy = 0.24f,
                TonemapMode = GEnv.ToneMapper.Aces, TonemapExposure = 1.05f, TonemapWhite = 6,
                SsaoEnabled = true, SsaoRadius = 1.2f, SsaoIntensity = 2.2f,
                GlowEnabled = true, GlowIntensity = 0.35f, GlowBloom = 0.03f,
                AdjustmentEnabled = true, AdjustmentContrast = 1.08f, AdjustmentSaturation = 0.95f,
            },
        });
        // the sun is low and warm, falling in from the surface above and a little in front
        _root.AddChild(new DirectionalLight3D { LightEnergy = 1.25f, LightColor = new Color(1f, 0.8f, 0.58f), RotationDegrees = new Vector3(-58, 22, 0), ShadowEnabled = true, ShadowBlur = 2f, DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal, DirectionalShadowMaxDistance = 120 });
        _root.AddChild(new DirectionalLight3D { LightEnergy = 0.35f, LightColor = new Color(1f, 0.6f, 0.35f), RotationDegrees = new Vector3(-10, -40, 0) });

        float wpx = size.X, hpx = size.Y, ground = Map.GroundPx;
        // the wall of earth, below the surface
        var wall = new ShaderMaterial { Shader = new Shader { Code = WallShader } };
        wall.SetShaderParameter("soil", soilTex);
        wall.SetShaderParameter("ground", -ground / Px);
        wall.SetShaderParameter("depth_span", (hpx - ground) / Px);
        var plane = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(wpx / Px + 20, (hpx - ground) / Px + 4), SubdivideWidth = 1, SubdivideDepth = 1 }, MaterialOverride = wall };
        plane.RotationDegrees = new Vector3(90, 0, 0);
        plane.Position = new Vector3(wpx / Px / 2, -(ground + (hpx - ground) / 2) / Px - 1, -Depth);
        _root.AddChild(plane);
        // the surface: the lip of topsoil, its crumbs, and grass standing against the evening sky
        var sky = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(wpx / Px + 20, ground / Px + 4) }, MaterialOverride = new ShaderMaterial { Shader = new Shader { Code = SkyShader } } };
        sky.Position = new Vector3(wpx / Px / 2, -ground / Px / 2 + 1.5f, -Depth - 3);
        _root.AddChild(sky);
        var rng = new Rng(20261102);
        var grassMats = new StandardMaterial3D[4];
        for (int i = 0; i < 4; i++) grassMats[i] = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.36f, 0.12f).Lerp(new Color(0.55f, 0.45f, 0.2f), i / 4f), Roughness = 0.75f, CullMode = BaseMaterial3D.CullModeEnum.Disabled, BacklightEnabled = true, Backlight = new Color(0.5f, 0.4f, 0.15f) };
        for (float x = -1; x < wpx / Px + 1; x += rng.R(0.15f, 0.55f))
        {
            float h = rng.R(1.2f, 4.2f) * (rng.F() < 0.12f ? 1.6f : 1);
            var blade = new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(rng.R(0.18f, 0.32f), h, 0.04f) }, MaterialOverride = grassMats[rng.I(0, 4)] };
            blade.Position = new Vector3(x, -ground / Px + h / 2 - 0.15f, rng.R(-Depth - 0.6f, 0.6f));
            blade.Rotation = new Vector3(rng.R(-0.2f, 0.2f), rng.R(0, 3), rng.R(-0.3f, 0.3f));
            _root.AddChild(blade);
        }
        // pebbles and grit set into the wall, roots hanging from the turf
        var stone = new StandardMaterial3D { AlbedoTexture = soilTex, AlbedoColor = new Color(0.78f, 0.72f, 0.66f), Uv1Triplanar = true, Uv1Scale = new Vector3(0.6f, 0.6f, 0.6f), Roughness = 0.7f };
        for (int i = 0; i < 90; i++)
        {
            float s = rng.F() < 0.88f ? rng.R(0.08f, 0.3f) : rng.R(0.4f, 0.8f);
            var p = new Vector3(rng.R(0, wpx / Px), -rng.R(ground / Px + 1, hpx / Px), -Depth + s * 0.25f);
            _root.AddChild(new MeshInstance3D { Mesh = WorldBuilder.PebbleMesh(i % 9 + 3), MaterialOverride = stone, Position = p, Rotation = new Vector3(rng.R(0, 3), rng.R(0, 3), rng.R(0, 3)), Scale = Vector3.One * s });
        }
        var bark = new StandardMaterial3D { AlbedoColor = new Color(0.33f, 0.2f, 0.11f), Roughness = 0.9f };
        for (int i = 0; i < 11; i++)
        {
            var k = new MeshKit();
            var white = new Color(1, 1, 1);
            var a = new Vector3(rng.R(2, wpx / Px - 2), -ground / Px + 0.2f, -Depth + 0.15f);
            float len = rng.R(4, 14), dx = rng.R(-0.35f, 0.35f);
            var prev = a;
            for (int s = 1; s <= 7; s++)
            {
                var next = a + new Vector3(dx * len * s / 7 + Mathf.Sin(s * 1.7f + i) * 0.5f, -len * s / 7, 0);
                k.Tube(prev, next, Mathf.Lerp(0.16f, 0.03f, (s - 1) / 7f), Mathf.Lerp(0.16f, 0.03f, s / 7f), 6, white, white, Vector2.Zero);
                prev = next;
            }
            _root.AddChild(new MeshInstance3D { Mesh = k.Commit(), MaterialOverride = bark });
        }

        // the galleries: dug up to the last open chamber, a seam of loose soil beyond
        var (curve, dug) = Map.TunnelCurve();
        var cut = new ShaderMaterial { Shader = new Shader { Code = WallShader.Replace("uniform float inside = 0.0;", "uniform float inside = 1.0;") } };
        cut.SetShaderParameter("soil", soilTex);
        cut.SetShaderParameter("ground", -ground / Px);
        cut.SetShaderParameter("depth_span", (hpx - ground) / Px);
        cut.RenderPriority = 1;
        if (dug > 1)
        {
            var k = new MeshKit();
            var white = new Color(1, 1, 1);
            // the galleries stop at the chamber walls: inside a chamber its own hollow is the floor
            for (int i = 1; i < dug; i++)
                if (!InChamber(curve[i - 1]) || !InChamber(curve[i]))
                    k.Tube(W(curve[i - 1], -Depth * 0.45f), W(curve[i], -Depth * 0.45f), TunnelR, TunnelR, 14, white, white, Vector2.Zero);
            var tm = (ShaderMaterial)cut.Duplicate(); tm.SetShaderParameter("cull_front", 1.0f);
            _root.AddChild(new MeshInstance3D { Mesh = k.Commit(), MaterialOverride = tm });
        }
        for (int i = dug; i < curve.Count; i += 4)
        {
            var p = W(curve[i], -Depth + 0.05f);
            _root.AddChild(new MeshInstance3D { Mesh = WorldBuilder.PebbleMesh(i % 5 + 11), MaterialOverride = stone, Position = p, Scale = Vector3.One * rng.R(0.07f, 0.13f) });
        }
        _path = new Vector3[Math.Max(0, dug)];
        _dist = new float[_path.Length];
        _len = 0;
        for (int i = 0; i < _path.Length; i++)
        {
            _path[i] = W(curve[i], 0);
            if (i > 0) { _len += _path[i].DistanceTo(_path[i - 1]); _dist[i] = _len; }
        }

        // the chambers
        for (int n = 1; n <= Levels.Count; n++) Chamber(n, soilTex, cut, stone, rng);

        // ants on the galleries
        int ants = Mathf.Clamp((int)(_len / 2.4f), 0, 70);
        if (ants > 0)
        {
            _ants = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = AntMesh.Build(AntType.Worker), InstanceCount = ants };
            var mat = AntRenderer.AntMat(Shaders.Get(Shaders.Ant), Config.Player, AntType.Worker, false);
            mat.SetShaderParameter("detail", 1f);
            _root.AddChild(new MultiMeshInstance3D { Multimesh = _ants, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
            for (int i = 0; i < ants; i++) _walkers.Add((i * _len / ants + rng.R(0, 1.5f), rng.R(1.1f, 1.7f) * (i % 2 == 0 ? 1 : -1), i % 16));
        }

        _selLight = new OmniLight3D { LightColor = new Color(1f, 0.74f, 0.42f), LightEnergy = 1.6f, OmniRange = 7, ShadowEnabled = false };
        _nextLight = new OmniLight3D { LightColor = new Color(1f, 0.66f, 0.3f), LightEnergy = 0.9f, OmniRange = 5 };
        _root.AddChild(_selLight); _root.AddChild(_nextLight);
        _cam = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = hpx / Px, Near = 0.1f, Far = 200, Current = true };
        _cam.Position = new Vector3(wpx / Px / 2, -hpx / Px / 2, 40);
        _root.AddChild(_cam);
        Refresh();
    }

    void Chamber(int n, Texture2D soilTex, ShaderMaterial cut, StandardMaterial3D stone, Rng rng)
    {
        int id = Levels.IdAt(n);
        var c2 = Map.Spot(n);
        float r = Map.ChamberRadius(n) / Px;
        var c = W(c2, 0);
        bool open = CampaignProgress.Unlocked(id), done = CampaignProgress.Completed(id);
        if (open)
        {
            // the hollow: the far half of a flattened ellipsoid seen from inside
            var m = (ShaderMaterial)cut.Duplicate(); m.SetShaderParameter("cull_front", 1.0f);
            _root.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 36, Rings = 18 }, MaterialOverride = m, Position = c + new Vector3(0, 0, -0.02f), Scale = new Vector3(r * 1.55f, r, Depth) });
            var glow = new OmniLight3D { LightColor = new Color(1f, 0.62f, 0.3f), LightEnergy = done ? 1.1f : 0.45f, OmniRange = r * 2.6f, Position = c + new Vector3(0, r * 0.25f, 0.2f) };
            _root.AddChild(glow);
            if (done) { var props = new Node3D { Position = c + new Vector3(0, -r * 0.62f, -Depth * 0.5f), Scale = Vector3.One * 1.6f }; _root.AddChild(props); _props = props; Props(id, Vector3.Zero, r / 1.6f, rng); _props = null; }
        }
        else
        {
            // undug: only a seam where the soil has been loosened, as if the colony had begun to mark it
            var seam = new StandardMaterial3D { AlbedoTexture = soilTex, AlbedoColor = new Color(0.66f, 0.48f, 0.32f, 0.8f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Uv1Triplanar = true, Uv1Scale = new Vector3(0.4f, 0.4f, 0.4f), Roughness = 1 };
            _root.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.975f, OuterRadius = 1.015f, Rings = 64, RingSegments = 6 }, MaterialOverride = seam, Position = c + new Vector3(0, 0, -Depth + 0.01f), Rotation = new Vector3(Mathf.Pi / 2, 0, 0), Scale = new Vector3(r * 1.55f, 0.02f, r) });
        }
        // the engraved plate above the chamber, and its stars as amber resin set into the earth below
        var plateAt = c + new Vector3(0, r * 1.22f + 0.2f, -Depth + 0.08f);
        // dark bronze for chambers the colony holds, dull iron-brown for the ones still to dig; the numeral is inlaid gold
        var bronze = new StandardMaterial3D { AlbedoColor = open ? new Color(0.3f, 0.2f, 0.1f) : new Color(0.17f, 0.12f, 0.08f), Metallic = 0.55f, Roughness = open ? 0.42f : 0.75f };
        _root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2.5f, 0.86f, 0.14f) }, MaterialOverride = bronze, Position = plateAt });
        _root.AddChild(new Label3D
        {
            Text = Roman(n), Font = UiTheme.Title, FontSize = 64, PixelSize = 0.0105f, Position = plateAt + new Vector3(0, 0, 0.07f),
            Modulate = open ? new Color(1f, 0.78f, 0.38f) : new Color(0.45f, 0.32f, 0.2f), OutlineModulate = new Color(0.06f, 0.03f, 0.01f, 0.8f), OutlineSize = 4,
            Shaded = false, DoubleSided = false,
        });
        int bits = CampaignProgress.StarBits(id);
        var resin = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.6f, 0.12f, 0.9f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.08f, EmissionEnabled = true, Emission = new Color(1f, 0.5f, 0.1f), EmissionEnergyMultiplier = 0.7f, RimEnabled = true, Rim = 0.6f };
        var pit = new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.09f, 0.04f), Roughness = 1, CullMode = BaseMaterial3D.CullModeEnum.Front };
        for (int s = 0; s < 3; s++)
        {
            var at = c + new Vector3((s - 1) * 0.5f, -r * 1.12f, -Depth + 0.05f);
            bool on = (bits & 1 << s) != 0;
            _root.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = on ? 0.17f : 0.13f, Height = on ? 0.3f : 0.2f, RadialSegments = 16, Rings = 8 }, MaterialOverride = on ? resin : pit, Position = at });
        }
    }

    Node3D _props;
    void Put(Node3D n) => (_props ?? _root).AddChild(n);

    /// What each won chamber keeps, as real objects on its floor.
    void Props(int id, Vector3 floor, float r, Rng rng)
    {
        StandardMaterial3D M(Color c, float rough = 0.6f, float metal = 0) => new() { AlbedoColor = c, Roughness = rough, Metallic = metal };
        void Egg(Vector3 at) => Put(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.14f, Height = 0.36f, RadialSegments = 12, Rings = 6 }, MaterialOverride = M(new Color(0.96f, 0.92f, 0.82f), 0.3f), Position = at, Rotation = new Vector3(Mathf.Pi / 2, rng.R(0, 3), rng.R(-0.5f, 0.5f)) });
        // every won chamber has a little brood: the colony lives there now
        for (int i = 0; i < 5; i++) Egg(floor + new Vector3(-r * 0.7f + i * 0.18f + rng.R(-0.05f, 0.05f), 0.12f, rng.R(-0.15f, 0.15f)));
        var at = floor + new Vector3(r * 0.45f, 0.1f, 0);
        switch (id)
        {
            case 1: // a shard of the tube
                Put(new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(0.7f, 0.9f, 0.04f) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.97f, 0.95f, 0.4f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.02f, MetallicSpecular = 1, RimEnabled = true, Rim = 1 }, Position = at + new Vector3(0, 0.35f, 0), Rotation = new Vector3(0, 0.6f, 0.2f) });
                break;
            case 5: // a stored drop of honeydew
                Put(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.26f, Height = 0.46f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.7f, 0.2f, 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f, EmissionEnabled = true, Emission = new Color(1f, 0.55f, 0.15f), EmissionEnergyMultiplier = 0.5f }, Position = at + new Vector3(0, 0.16f, 0) });
                break;
            case 2: case 8: // seeds of the harvest
                for (int i = 0; i < 6; i++) Put(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.11f, Height = 0.3f }, MaterialOverride = M(new Color(0.72f, 0.52f, 0.28f), 0.5f), Position = at + new Vector3(rng.R(-0.3f, 0.3f), 0.05f + (i / 3) * 0.12f, rng.R(-0.2f, 0.2f)), Rotation = new Vector3(rng.R(0, 3), rng.R(0, 3), 0) });
                break;
            case 3: case 11: case 12: // the severed mandibles of a red queen's guard
                var red = M(new Color(0.55f, 0.16f, 0.07f), 0.35f, 0.1f);
                for (int s = -1; s <= 1; s += 2) Put(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.07f, Height = 0.7f }, MaterialOverride = red, Position = at + new Vector3(s * 0.15f, 0.12f, 0), Rotation = new Vector3(0, 0, Mathf.Pi / 2 + s * 0.5f) });
                if (id == 12) Put(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.3f, Height = 0.45f }, MaterialOverride = red, Position = at + new Vector3(-0.5f, 0.2f, 0) });
                break;
            case 6: case 10: // a hornet's sting, a scrap of paper nest
                if (id == 10) Put(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.6f, 0.04f, 0.4f) }, MaterialOverride = M(new Color(0.7f, 0.6f, 0.45f), 1), Position = at + new Vector3(0, 0.03f, 0), Rotation = new Vector3(0.1f, 0.4f, 0.15f) });
                Put(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.0f, BottomRadius = 0.06f, Height = 0.6f }, MaterialOverride = M(new Color(0.25f, 0.16f, 0.06f), 0.3f), Position = at + new Vector3(0.1f, 0.12f, 0), Rotation = new Vector3(0, 0, -1.3f) });
                break;
            case 7: // a drop of Vetch's acid
                Put(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.18f, Height = 0.3f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.9f, 0.15f, 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f, EmissionEnabled = true, Emission = new Color(0.4f, 0.9f, 0.1f), EmissionEnergyMultiplier = 1.2f }, Position = at + new Vector3(0, 0.1f, 0) });
                break;
            default: // a shed wing
                Put(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.5f, Height = 0.02f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = id == 4 ? new Color(1f, 0.6f, 0.4f, 0.5f) : new Color(1f, 0.88f, 0.6f, 0.5f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.2f, CullMode = BaseMaterial3D.CullModeEnum.Disabled }, Position = at + new Vector3(0, 0.08f, 0), Scale = new Vector3(0.4f, 1, 1.2f), Rotation = new Vector3(0.2f, 0.9f, 0.1f) });
                break;
        }
    }

    bool InChamber(Vector2 px)
    {
        for (int n = 1; n <= Levels.Count; n++)
        {
            if (!CampaignProgress.Unlocked(Levels.IdAt(n))) continue;
            var d = px - Map.Spot(n);
            float r = Map.ChamberRadius(n) * 0.92f;
            if (d.X * d.X / (r * r * 2.4f) + d.Y * d.Y / (r * r) < 1) return true;
        }
        return false;
    }

    static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", 10 => "X", 11 => "XI", _ => "XII" };

    /// Moves the selection and next-chamber lights to their chambers.
    public void Refresh()
    {
        if (_selLight == null) return;
        int sel = Map.Selected;
        var c = W(Map.Spot(sel), 1.2f);
        _selLight.Position = c;
        int next = CampaignProgress.Next;
        _nextLight.Visible = next > 0;
        if (next > 0) _nextLight.Position = W(Map.Spot(Levels.NumberOf(next)), 0.6f);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (_nextLight != null) _nextLight.LightEnergy = 0.7f + 0.35f * Mathf.Sin(_t * 2.4f);
        if (_ants == null || _path.Length < 2) return;
        for (int i = 0; i < _walkers.Count; i++)
        {
            var (offset, speed, seed) = _walkers[i];
            float d = Mathf.PosMod(offset + _t * speed, _len);
            int j = Array.BinarySearch(_dist, d); if (j < 0) j = ~j; j = Math.Clamp(j, 1, _path.Length - 1);
            float f = (d - _dist[j - 1]) / Mathf.Max(0.0001f, _dist[j] - _dist[j - 1]);
            var p = _path[j - 1].Lerp(_path[j], f);
            var fwd = (_path[j] - _path[j - 1]).Normalized() * Mathf.Sign(speed);
            var up = new Vector3(-fwd.Y, fwd.X, 0); if (up.Y < 0) up = -up;
            var right = up.Cross(fwd);
            // on the floor of the gallery, scaled to the map
            var basis = new Basis(right, up, fwd) * Basis.FromScale(Vector3.One * AntScale);
            var pos = p - up * (TunnelR - 0.12f) + new Vector3(0, 0, -Depth * 0.45f);
            _ants.SetInstanceTransform(i, new Transform3D(basis, pos));
            float gait = Mathf.PosMod(d * 3.2f, 1f) * 0.999f;
            _ants.SetInstanceCustomData(i, new Color(seed + gait, 128 * 256 + 128 + 0.6f, 0, 0));
        }
    }

    static float _antScale;
    static float AntScale => _antScale > 0 ? _antScale : _antScale = 1.0f / AntMesh.Build(AntType.Worker).GetAabb().Size.Z; // a worker about 15 px long

    const string WallShader = @"
shader_type spatial;
render_mode cull_disabled;
uniform sampler2D soil : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float ground = 0.0;
uniform float depth_span = 30.0;
uniform float inside = 0.0;
uniform float cull_front = 0.0;
varying vec3 wpos;
float n2(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }
float vnoise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
    return mix(mix(n2(i), n2(i + vec2(1, 0)), f.x), mix(n2(i + vec2(0, 1)), n2(i + vec2(1, 1)), f.x), f.y); }
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
    if (cull_front > 0.5 && FRONT_FACING) discard;   // a hollow: show only the far side of the cut
    vec2 uv = wpos.xy * 0.11;
    vec3 tex = texture(soil, uv).rgb;
    vec3 tex2 = texture(soil, uv * 0.37 + vec2(0.31, 0.7)).rgb;
    float d = clamp((ground - wpos.y) / depth_span, 0.0, 1.0);
    // strata with wandering boundaries: dark humus under the turf, warm loam, ochre clay, deep red-brown subsoil
    float w = (vnoise(wpos.xy * vec2(0.12, 0.6)) - 0.5) * 0.06;
    vec3 humus = vec3(0.17, 0.11, 0.07), loam = vec3(0.42, 0.31, 0.21), clay = vec3(0.52, 0.37, 0.23), sub = vec3(0.33, 0.21, 0.13);
    vec3 band = d + w < 0.06 ? humus : d + w < 0.36 ? mix(humus, loam, smoothstep(0.06, 0.12, d + w)) : d + w < 0.66 ? mix(loam, clay, smoothstep(0.36, 0.44, d + w)) : mix(clay, sub, smoothstep(0.66, 0.78, d + w));
    vec3 col = tex * mix(tex2, vec3(1.0), 0.5) * band * 1.75;
    col = mix(vec3(dot(col, vec3(0.333))), col, 0.72);   // earth, not paint: keep the saturation down
    col *= 1.0 - 0.42 * d;                               // the light from the surface fades with depth
    // thin seams of pale grit and dark organic matter between the layers
    float seam = abs(fract((d + w * 0.6) * 9.0) - 0.5);
    col *= 1.0 - 0.18 * smoothstep(0.03, 0.0, seam - 0.47);
    col *= mix(1.0, 0.8, inside);                        // the inside of a cut is shaded and damp
    EMISSION = col * inside * 0.16 * vec3(1.0, 0.75, 0.5);   // and lit warmly from within, where the colony lives
    ALBEDO = col;
    ROUGHNESS = mix(0.95, 0.82, inside);
    float h = dot(tex, vec3(0.33));
    NORMAL = normalize(NORMAL + vec3(-dFdx(h), -dFdy(h), 0.0) * 6.0);
}";

    const string SkyShader = @"
shader_type spatial;
render_mode unshaded;
void fragment() {
    float v = UV.y;
    ALBEDO = mix(vec3(0.95, 0.62, 0.32), vec3(0.48, 0.24, 0.12), smoothstep(0.0, 1.0, 1.0 - v));
}";
}
