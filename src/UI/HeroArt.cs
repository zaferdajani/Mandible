using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GEnv = Godot.Environment;

namespace Mandible;

/// The three heroes' own portrait art, baked from their game meshes by <see cref="HeroStudio"/> and kept under
/// assets/ui/heroes: a wide card (the caste cards' 2:1 framing) and an upright bust, for each colony's colours.
public static class HeroArt
{
    public enum Framing { Card, Bust }
    static readonly Dictionary<string, Texture2D> _cache = new();

    public static string Slug(AntType hero) => hero switch { AntType.Warlord => "warlord", AntType.Matron => "matron", _ => "marshal" };
    public static string TeamName(int team) => team == Config.Enemy ? "red" : "black";
    public static string File(AntType hero, int team, Framing f) => $"{Slug(hero)}_{TeamName(team)}_{(f == Framing.Card ? "card" : "bust")}.png";

    public static Texture2D Get(AntType hero, int team, Framing f)
    {
        string path = "res://assets/ui/heroes/" + File(hero, team, f);
        if (!_cache.TryGetValue(path, out var tex)) _cache[path] = tex = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        return tex;
    }
    public static Texture2D Card(AntType hero, int team) => Get(hero, team, Framing.Card);
    public static Texture2D Bust(AntType hero, int team) => Get(hero, team, Framing.Bust);

    /// A painted-card frame for menus outside the command HUD: the bust, cropped to fill, under a bronze edge.
    public static Control Frame(AntType hero, int team, Framing f, Vector2 min)
    {
        var p = new PanelContainer { CustomMinimumSize = min, MouseFilter = Control.MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.035f, 0.03f, 0.02f), new Color("a77e32"), 1, 4, 2));
        p.AddChild(new TextureRect
        {
            Texture = Get(hero, team, f), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        return p;
    }
}

/// Bakes the hero portraits: --bake-heroes=<absolute dir> (run windowed). Each hero's real mesh and material stand
/// on warm sandy soil with pebbles and straw, lit by a low warm key and a bright rim like the painted caste cards,
/// posed for its special: the Warlord roaring its war cry, the Acid Matron dripping venom, the Sky Marshal's wings open.
public static class HeroStudio
{
    const int Supersample = 2;

    public static async Task Bake(Main app, string dir)
    {
        DirAccess.MakeDirRecursiveAbsolute(dir);
        await Render(app, AntType.Warlord, Config.Player, HeroArt.Framing.Card, new Vector2I(64, 32)); // compiles every shader first
        foreach (var hero in new[] { AntType.Warlord, AntType.Matron, AntType.Marshal })
            foreach (int team in new[] { Config.Player, Config.Enemy })
                foreach (var f in new[] { HeroArt.Framing.Card, HeroArt.Framing.Bust })
                {
                    var size = f == HeroArt.Framing.Card ? new Vector2I(640, 320) : new Vector2I(400, 490);
                    var img = await Render(app, hero, team, f, size * Supersample);
                    img.Resize(size.X, size.Y, Image.Interpolation.Lanczos);
                    Grade(img);
                    string path = $"{dir}/{HeroArt.File(hero, team, f)}";
                    img.SavePng(path);
                    GD.Print($"[heroes] {path}");
                }
        app.GetTree().Quit();
    }

    static async Task<Image> Render(Main app, AntType hero, int team, HeroArt.Framing framing, Vector2I size)
    {
        var vp = new SubViewport
        {
            Size = size, OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa4X, TransparentBg = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always, PositionalShadowAtlasSize = 2048,
        };
        app.AddChild(vp);
        var sand = new Color(0.62f, 0.47f, 0.32f);
        vp.AddChild(new WorldEnvironment
        {
            Environment = new GEnv
            {
                BackgroundMode = GEnv.BGMode.Color, BackgroundColor = sand,
                AmbientLightSource = GEnv.AmbientSource.Color, AmbientLightColor = new Color(0.78f, 0.62f, 0.46f), AmbientLightEnergy = 0.32f,
                TonemapMode = GEnv.ToneMapper.Aces, TonemapExposure = 0.98f, TonemapWhite = 6,
                FogEnabled = true, FogLightColor = new Color(0.66f, 0.5f, 0.34f), FogDensity = 0.035f, FogSkyAffect = 0,
                GlowEnabled = true, GlowIntensity = 0.5f, GlowBloom = 0.04f, GlowHdrThreshold = 1.1f,
                SsaoEnabled = true, SsaoRadius = 0.12f, SsaoIntensity = 1.4f,
                AdjustmentEnabled = true, AdjustmentSaturation = 1.05f, AdjustmentContrast = 1.12f,
            },
        });

        // the subject: the hero's own mesh and team material, posed for its special
        var holder = new Node3D();
        vp.AddChild(holder);
        var mesh = AntMesh.Build(hero, false, team, AntMesh.Detail.Near);
        var aabb = mesh.GetAabb();
        var mat = AntRenderer.AntMat(Shaders.Get(Shaders.Ant), team, hero, false);
        mat.SetShaderParameter("detail", 1f);
        var body = One(mesh, Pose(hero));
        holder.AddChild(new MultiMeshInstance3D { Multimesh = body, MaterialOverride = mat });
        if (hero == AntType.Marshal)
        {
            // a still portrait wing writes depth (or the depth of field blurs it as background) and reads a little denser
            var wm = new ShaderMaterial { Shader = Shaders.Get(Shaders.Wing.Replace("depth_draw_never", "depth_draw_always").Replace("ALPHA = mix(0.22, 0.8, veins)", "ALPHA = mix(0.5, 0.92, veins)")) };
            // deeper amber than in play so the membrane reads against pale sand
            wm.SetShaderParameter("tint", new Color(0.78f, 0.56f, 0.24f)); wm.SetShaderParameter("vein", new Color(0.32f, 0.17f, 0.04f));
            var wings = AntMesh.Wings(hero);
            aabb = aabb.Merge(wings.GetAabb());
            // wings swung open and held still: x = beat speed 0, y = spread
            holder.AddChild(new MultiMeshInstance3D { Multimesh = One(wings, new Color(0, 0.85f, 0, 0)), MaterialOverride = wm, SortingOffset = 5 });
        }
        if (hero == AntType.Matron) Venom(holder, AntMesh.GasterTip(hero, team));
        float floorY = mesh.GetAabb().Position.Y;

        Ground(vp, floorY, (uint)hero * 7 + (uint)team);

        // a low warm key from front-left, a strong rim from behind-right that traces the chitin, a soft warm bounce
        vp.AddChild(new DirectionalLight3D
        {
            LightEnergy = 2.3f, LightColor = new Color(1f, 0.86f, 0.66f), RotationDegrees = new Vector3(-34, -35, 0),
            ShadowEnabled = true, ShadowBlur = 1.6f, DirectionalShadowMaxDistance = 6, DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
        });
        vp.AddChild(new DirectionalLight3D { LightEnergy = 2.6f, LightColor = new Color(1f, 0.9f, 0.72f), RotationDegrees = new Vector3(-22, 160, 0), LightSpecular = 1.6f });
        vp.AddChild(new OmniLight3D { Position = new Vector3(-0.6f, 0.25f, 0.9f), OmniRange = 3, LightEnergy = 0.5f, LightColor = new Color(1f, 0.75f, 0.5f), LightSpecular = 0.3f });

        // framing: three-quarter view, a little above, the card wide and whole, the bust close on head and thorax
        bool card = framing == HeroArt.Framing.Card;
        float yaw = hero switch { AntType.Matron => card ? 72 : 48, AntType.Marshal => card ? 50 : 34, _ => card ? 46 : 30 };
        float elev = card ? 15 : 22;
        var center = aabb.GetCenter();
        Vector3 target; float dist;
        if (card)
        {
            target = new Vector3(0, floorY + (aabb.End.Y - floorY) * 0.45f, center.Z);
            dist = aabb.Size.Length() * (hero == AntType.Marshal ? 1.08f : hero == AntType.Warlord ? 1.14f : 1.0f);
        }
        else
        {
            float headZ = aabb.End.Z - aabb.Size.Z * 0.27f;
            target = new Vector3(0, floorY + (aabb.End.Y - floorY) * 0.55f, Mathf.Lerp(center.Z, headZ, 0.62f));
            dist = aabb.Size.Length() * (hero == AntType.Marshal ? 1.18f : 1.08f);
        }
        float ry = Mathf.DegToRad(yaw), re = Mathf.DegToRad(elev);
        var dirTo = new Vector3(-Mathf.Sin(ry) * Mathf.Cos(re), Mathf.Sin(re), Mathf.Cos(ry) * Mathf.Cos(re));
        var cam = new Camera3D { Fov = card ? 24 : 30, Near = 0.02f, Far = 40, Current = true };
        cam.Attributes = new CameraAttributesPractical
        {
            DofBlurFarEnabled = true, DofBlurFarDistance = dist * 1.12f, DofBlurFarTransition = dist * 1.4f, DofBlurAmount = 0.09f,
        };
        vp.AddChild(cam);
        cam.LookAtFromPosition(target + dirTo * dist, target, Vector3.Up);

        for (int i = 0; i < 12; i++) await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = vp.GetTexture().GetImage();
        img.Convert(Image.Format.Rgb8);
        vp.QueueFree();
        return img;
    }

    /// Ant shader instance data (see Shaders.Ant): standing idle, head and jaws set for each hero's character, at
    /// its full mutation tier (target J: every thorn, spur and horn grown in).
    static Color Pose(AntType hero)
    {
        // y = yaw8 * 256 + pitch8: 128 is straight ahead; pitch above 128 looks up
        int yaw8 = 150, pitch8 = hero == AntType.Warlord ? 190 : 150;
        float bite = hero == AntType.Warlord ? 0.98f : hero == AntType.Marshal ? 0.35f : 0.2f; // the war cry: jaws flung wide
        return new Color(5 + 0.3f, yaw8 * 256 + pitch8, bite, 512 * AntRenderer.MutationTier(Config.MaxLevelOf(hero)));
    }

    static MultiMesh One(Mesh m, Color custom)
    {
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = m, InstanceCount = 1 };
        mm.SetInstanceTransform(0, Transform3D.Identity);
        mm.SetInstanceCustomData(0, custom);
        return mm;
    }

    /// The Matron's acid: a bead hanging from the acidopore at the gaster tip (never the mouth) and a smoking splash
    /// on the soil below.
    static void Venom(Node3D holder, Vector3 nozzle)
    {
        var acid = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.45f, 0.85f, 0.12f, 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.05f, Metallic = 0, EmissionEnabled = true, Emission = new Color(0.4f, 0.95f, 0.1f), EmissionEnergyMultiplier = 1.6f,
            RimEnabled = true, Rim = 0.6f,
        };
        holder.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.016f, Height = 0.042f }, MaterialOverride = acid, Position = nozzle + new Vector3(0, -0.022f, -0.004f) });
        holder.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.009f, Height = 0.02f }, MaterialOverride = acid, Position = nozzle + new Vector3(0.002f, -0.06f, -0.01f) });
        var splash = (StandardMaterial3D)acid.Duplicate();
        splash.EmissionEnergyMultiplier = 0.9f;
        var rng = new Rng(17);
        for (int i = 0; i < 6; i++)
        {
            float r = i == 0 ? 0.05f : rng.R(0.008f, 0.02f);
            var at = new Vector3(rng.R(-0.07f, 0.07f), 0.001f, nozzle.Z - 0.04f + rng.R(-0.06f, 0.06f));
            if (i == 0) at = new Vector3(0, 0.001f, nozzle.Z - 0.03f);
            holder.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = r, BottomRadius = r, Height = 0.004f, RadialSegments = 20 }, MaterialOverride = splash, Position = at });
        }
        holder.AddChild(new OmniLight3D { Position = nozzle + new Vector3(0, -0.04f, -0.03f), OmniRange = 0.45f, LightEnergy = 0.9f, LightColor = new Color(0.5f, 1f, 0.2f) });
    }

    /// Sandy loam with pebbles and straw, the same soil photograph the nest chambers use.
    static void Ground(SubViewport vp, float y, uint seed)
    {
        var soil = new StandardMaterial3D
        {
            AlbedoTexture = GD.Load<Texture2D>("res://assets/materials/nest-soil-albedo.png"), AlbedoColor = new Color(0.74f, 0.6f, 0.46f),
            Uv1Scale = new Vector3(9, 9, 1), Roughness = 0.95f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        vp.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(24, 24) }, MaterialOverride = soil, Position = new Vector3(0, y, 0) });
        var rng = new Rng(seed * 131 + 7);
        // pebbles wear the soil photograph too (triplanar), tinted to weathered quartz and flint
        StandardMaterial3D Stone(Color tint) => new()
        {
            AlbedoTexture = soil.AlbedoTexture, AlbedoColor = tint, Uv1Triplanar = true, Uv1Scale = new Vector3(3, 3, 3), Roughness = 0.62f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        var stone = Stone(new Color(0.95f, 0.9f, 0.84f));
        var stoneDark = Stone(new Color(0.62f, 0.55f, 0.48f));
        for (int i = 0; i < 26; i++)
        {
            float a = rng.R(0, Mathf.Tau), d = rng.R(0.95f, 4.5f);
            float r = rng.R(0.04f, 0.2f) * (d > 2 ? 1.6f : 1f);
            var p = new Vector3(Mathf.Cos(a) * d, y + r * 0.25f, Mathf.Sin(a) * d - 0.6f);
            add(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = r, Height = r * 1.3f, RadialSegments = 18, Rings = 9 }, MaterialOverride = i % 3 == 0 ? stoneDark : stone,
                Position = p, Rotation = new Vector3(rng.R(-0.3f, 0.3f), rng.R(0, 3), rng.R(-0.3f, 0.3f)), Scale = new Vector3(1, 1, rng.R(0.7f, 1.3f)),
            });
        }
        var straw = new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.56f, 0.3f), Roughness = 0.7f };
        for (int i = 0; i < 7; i++)
        {
            float a = rng.R(0, Mathf.Tau), d = rng.R(1.1f, 3.8f), len = rng.R(0.8f, 2.2f);
            add(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.016f, Height = len, RadialSegments = 6 }, MaterialOverride = straw,
                Position = new Vector3(Mathf.Cos(a) * d, y + 0.014f, Mathf.Sin(a) * d - 0.8f), Rotation = new Vector3(Mathf.Pi / 2, 0, rng.R(0, Mathf.Tau)),
            });
        }
        void add(Node3D n) => vp.AddChild(n);
    }

    /// A painter's finish on the bake: warm the shadows, lift the centre, darken the corners like the caste cards.
    static void Grade(Image img)
    {
        int w = img.GetWidth(), h = img.GetHeight();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = img.GetPixel(x, y);
                float u = (x + 0.5f) / w - 0.5f, v = (y + 0.5f) / h - 0.45f;
                float vig = 1 - Mathf.Clamp((u * u * 1.6f + v * v * 2.2f) - 0.05f, 0, 1) * 0.62f;
                float lum = c.R * 0.3f + c.G * 0.59f + c.B * 0.11f;
                float shadow = 1 - Mathf.Clamp(lum * 2.2f, 0, 1);
                c = new Color(c.R + shadow * 0.018f, c.G + shadow * 0.008f, c.B - shadow * 0.004f);
                img.SetPixel(x, y, new Color(c.R * vig, c.G * vig, c.B * vig));
            }
    }
}
