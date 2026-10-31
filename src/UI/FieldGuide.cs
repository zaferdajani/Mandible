using System;
using System.Collections.Generic;
using Godot;
using GEnv = Godot.Environment;

namespace Mandible;

/// A live 3D portrait of an ant or bug: the real game mesh and material, lit like a museum
/// specimen and slowly turning on a turntable.
public partial class AntPortrait : SubViewportContainer
{
    public AntType Type;
    public int Team;
    public bool Spin = true;
    SubViewport _vp;
    Node3D _turn;
    MultiMesh _body, _wings;
    float _t;

    public override void _Ready()
    {
        Stretch = true;
        MouseFilter = MouseFilterEnum.Ignore;
        _vp = new SubViewport
        {
            OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa4X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible,
        };
        AddChild(_vp);
        _vp.AddChild(new WorldEnvironment
        {
            Environment = new GEnv
            {
                BackgroundMode = GEnv.BGMode.Color, BackgroundColor = new Color(0.16f, 0.125f, 0.095f),
                AmbientLightSource = GEnv.AmbientSource.Color, AmbientLightColor = new Color(0.62f, 0.56f, 0.5f), AmbientLightEnergy = 0.6f,
                TonemapMode = GEnv.ToneMapper.Aces, TonemapExposure = 1.2f,
                FogEnabled = true, FogLightColor = new Color(0.16f, 0.125f, 0.095f), FogDensity = 0.04f, FogSkyAffect = 0,
            },
        });
        // the same single sun as the battlefield, from the same angle
        _vp.AddChild(new DirectionalLight3D { LightEnergy = 1.7f, LightColor = new Color(1f, 0.93f, 0.82f), RotationDegrees = GameSession.SunAngles, ShadowEnabled = true, ShadowBlur = 2.5f });

        _turn = new Node3D();
        _vp.AddChild(_turn);
        int team = Config.IsBug(Type) ? Config.Wild : Team;
        var mesh = AntMesh.Build(Type);
        var aabb = mesh.GetAabb();
        var holder = new Node3D { Position = -aabb.GetCenter() };
        _turn.AddChild(holder);
        _body = OneInstance(mesh);
        holder.AddChild(new MultiMeshInstance3D { Multimesh = _body, MaterialOverride = AntRenderer.AntMat(Shaders.Get(Shaders.Ant), team, Type, false) });
        if (Config.Flies(Type))
        {
            var wm = new ShaderMaterial { Shader = Shaders.Get(Shaders.Wing) };
            if (Type == AntType.Wasp) { wm.SetShaderParameter("tint", new Color(0.82f, 0.62f, 0.3f)); wm.SetShaderParameter("vein", new Color(0.35f, 0.2f, 0.05f)); }
            if (Type == AntType.Marshal) { wm.SetShaderParameter("tint", new Color(0.95f, 0.85f, 0.6f)); wm.SetShaderParameter("vein", new Color(0.6f, 0.42f, 0.1f)); }
            _wings = OneInstance(AntMesh.Wings(Type == AntType.Marshal ? AntType.Flyer : Type));
            _wings.SetInstanceCustomData(0, new Color(Type == AntType.Wasp ? 46f : 36f, 1f, 0, 0));
            holder.AddChild(new MultiMeshInstance3D { Multimesh = _wings, MaterialOverride = wm, SortingOffset = 5 });
        }
        // the specimen stands on a patch of soil that catches its shadow
        float r = aabb.Size.Length() * 0.5f;
        var soil = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.32f, 0.23f), Roughness = 1 };
        _vp.AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(r * 12, r * 12) }, MaterialOverride = soil,
            Position = new Vector3(0, aabb.Position.Y - aabb.GetCenter().Y - (Config.Flies(Type) ? r * 0.35f : 0), 0),
        });
        // frame the whole specimen from a three-quarter view
        float dist = r / Mathf.Sin(Mathf.DegToRad(17f));
        var cam = new Camera3D { Fov = 26, Near = 0.01f, Far = dist * 4 };
        _vp.AddChild(cam);
        cam.LookAtFromPosition(new Vector3(0, 0.55f, 1).Normalized() * dist, Vector3.Zero, Vector3.Up);
        _t = (float)GD.RandRange(0, 10);
    }

    static MultiMesh OneInstance(Mesh m)
    {
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = m, InstanceCount = 1 };
        mm.SetInstanceTransform(0, Transform3D.Identity);
        mm.SetInstanceCustomData(0, new Color(0, 0, 0, 0));
        return mm;
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        _t += (float)delta;
        if (Spin) _turn.Rotation = new Vector3(0, 0.7f + _t * 0.45f, 0);
        // a slow idle shuffle so the legs and antennae are alive
        _body.SetInstanceCustomData(0, new Color(_t * 0.35f % 1f, 0.18f, Mathf.Max(0, Mathf.Sin(_t * 0.9f)) * 0.5f, 0));
    }
}

/// Small drawn icon for each kind of food.
public partial class FoodIcon : Control
{
    public FoodKind Kind;

    public static Color Tint(FoodKind k) => k switch
    {
        FoodKind.Crumb => new Color(0.86f, 0.7f, 0.45f),
        FoodKind.Berry => new Color(0.86f, 0.18f, 0.3f),
        FoodKind.Sugar => new Color(1f, 0.74f, 0.25f),
        FoodKind.Seed => new Color(0.66f, 0.5f, 0.3f),
        _ => new Color(0.55f, 0.6f, 0.45f),
    };

    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; }

    public override void _Draw()
    {
        var s = Size;
        var c = s * 0.5f;
        float r = Mathf.Min(s.X, s.Y) * 0.42f;
        var col = Tint(Kind);
        switch (Kind)
        {
            case FoodKind.Berry:
                DrawLine(c + new Vector2(0, -r * 0.85f), c + new Vector2(r * 0.35f, -r * 1.05f), new Color(0.35f, 0.6f, 0.2f), r * 0.14f);
                for (int i = 0; i < 7; i++)
                {
                    var o = i == 0 ? Vector2.Zero : Vector2.FromAngle(i * Mathf.Tau / 6) * r * 0.48f;
                    DrawCircle(c + o, r * 0.36f, col.Darkened(0.15f + 0.05f * (i % 2)));
                    DrawCircle(c + o + new Vector2(-r * 0.1f, -r * 0.1f), r * 0.09f, new Color(1, 1, 1, 0.55f));
                }
                break;
            case FoodKind.Sugar:
                DrawColoredPolygon(new[] { c + new Vector2(0, -r * 1.05f), c + new Vector2(r * 0.62f, r * 0.05f), c + new Vector2(-r * 0.62f, r * 0.05f) }, col);
                DrawCircle(c + new Vector2(0, r * 0.25f), r * 0.66f, col);
                DrawCircle(c + new Vector2(-r * 0.22f, r * 0.05f), r * 0.16f, new Color(1, 1, 1, 0.6f));
                break;
            case FoodKind.Seed:
                DrawSetTransform(c, -0.5f, new Vector2(0.62f, 1f));
                DrawCircle(Vector2.Zero, r, col);
                DrawLine(new Vector2(0, -r * 0.85f), new Vector2(0, r * 0.85f), col.Darkened(0.35f), r * 0.12f);
                DrawSetTransform(Vector2.Zero, 0, Vector2.One);
                break;
            case FoodKind.Corpse:
                for (int i = 0; i < 3; i++)
                    for (int sd = -1; sd <= 1; sd += 2)
                    {
                        var a = c + new Vector2(sd * r * 0.3f, (i - 1) * r * 0.32f);
                        DrawLine(a, a + new Vector2(sd * r * 0.65f, (i - 1) * r * 0.25f - r * 0.2f), col.Darkened(0.4f), r * 0.09f);
                    }
                DrawSetTransform(c, 0, new Vector2(0.62f, 1f));
                DrawCircle(new Vector2(0, r * 0.15f), r * 0.62f, col.Darkened(0.2f));
                DrawCircle(new Vector2(0, -r * 0.62f), r * 0.32f, col.Darkened(0.35f));
                DrawSetTransform(Vector2.Zero, 0, Vector2.One);
                break;
            default:
                DrawColoredPolygon(new[]
                {
                    c + new Vector2(-r * 0.9f, -r * 0.2f), c + new Vector2(-r * 0.4f, -r * 0.75f), c + new Vector2(r * 0.5f, -r * 0.65f),
                    c + new Vector2(r * 0.9f, 0), c + new Vector2(r * 0.55f, r * 0.7f), c + new Vector2(-r * 0.6f, r * 0.6f),
                }, col);
                DrawCircle(c + new Vector2(-r * 0.2f, -r * 0.1f), r * 0.1f, col.Darkened(0.3f));
                DrawCircle(c + new Vector2(r * 0.3f, r * 0.2f), r * 0.08f, col.Darkened(0.3f));
                break;
        }
    }
}

/// An animated picture of how real ants find and haul food: scouts wander, a finder lays a
/// pheromone trail home, recruits follow it out, and every returning ant strengthens it.
public partial class ForagingDiagram : Control
{
    float _t;
    readonly Vector2[] _scout = new Vector2[6];
    readonly float[] _head = new float[6];
    readonly RandomNumberGenerator _rng = new() { Seed = 7 };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        for (int i = 0; i < _scout.Length; i++) { _scout[i] = new Vector2(0.18f, 0.6f); _head[i] = i * 1.1f; }
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        float dt = (float)delta;
        _t += dt;
        for (int i = 0; i < _scout.Length; i++)
        {
            _head[i] += _rng.RandfRange(-3f, 3f) * dt;
            var p = _scout[i] + Vector2.FromAngle(_head[i]) * dt * 0.05f;
            if (p.X < 0.05f || p.X > 0.55f || p.Y < 0.2f || p.Y > 0.95f) { _head[i] += Mathf.Pi; p = _scout[i]; }
            _scout[i] = p;
        }
        QueueRedraw();
    }

    Vector2 Path(float u, Vector2 a, Vector2 b)
    {
        // a gentle S-curve: real trails bend around obstacles
        var mid1 = a.Lerp(b, 0.33f) + new Vector2(0, -Size.Y * 0.55f);
        var mid2 = a.Lerp(b, 0.66f) + new Vector2(0, Size.Y * 0.45f);
        float v = 1 - u;
        return v * v * v * a + 3 * v * v * u * mid1 + 3 * v * u * u * mid2 + u * u * u * b;
    }

    void Ant(Vector2 p, Vector2 dir, Color c, bool carrying)
    {
        var f = dir.Normalized();
        float k = 1.7f;
        var side = new Vector2(-f.Y, f.X);
        for (int l = -1; l <= 1; l++)
            for (int sd = -1; sd <= 1; sd += 2)
                DrawLine(p + f * l * 2.5f * k, p + f * (l * 3.5f + Mathf.Sin(_t * 14 + l + sd) * 1.2f) * k + side * sd * 5 * k, c, 1.2f);
        DrawCircle(p - f * 5 * k, 3.4f * k, c);
        DrawCircle(p, 2.2f * k, c);
        DrawCircle(p + f * 4 * k, 2.6f * k, c);
        if (carrying) DrawCircle(p + f * 8 * k, 2.8f * k, new Color(0.95f, 0.3f, 0.35f));
    }

    public override void _Draw()
    {
        var s = Size;
        DrawRect(new Rect2(Vector2.Zero, s), new Color(0.16f, 0.12f, 0.08f, 0.9f));
        var nest = new Vector2(s.X * 0.14f, s.Y * 0.62f);
        var food = new Vector2(s.X * 0.86f, s.Y * 0.4f);
        // the trail: brighter where traffic is heavier, pulsing as it is re-laid
        var pts = new Vector2[48];
        for (int i = 0; i < pts.Length; i++) pts[i] = Path(i / (float)(pts.Length - 1), nest, food);
        float pulse = 0.55f + 0.2f * Mathf.Sin(_t * 2.2f);
        DrawPolyline(pts, new Color(0.35f, 0.9f, 0.45f, 0.18f), 14, true);
        DrawPolyline(pts, new Color(0.45f, 1f, 0.55f, pulse * 0.6f), 4, true);
        // nest mound and food
        DrawCircle(nest, 26, new Color(0.42f, 0.32f, 0.22f));
        DrawCircle(nest, 9, new Color(0.08f, 0.06f, 0.04f));
        for (int i = 0; i < 7; i++)
        {
            var o = i == 0 ? Vector2.Zero : Vector2.FromAngle(i * Mathf.Tau / 6) * 11;
            DrawCircle(food + o, 9, new Color(0.8f, 0.16f, 0.28f));
        }
        // scouts searching at random
        foreach (var (p, h) in Zip()) Ant(new Vector2(p.X * s.X, p.Y * s.Y), Vector2.FromAngle(h), new Color(0.9f, 0.85f, 0.7f), false);
        // recruits going out, finders coming home loaded
        for (int i = 0; i < 9; i++)
        {
            float u = (_t * 0.09f + i / 9f) % 1f;
            var p = Path(u, nest, food);
            Ant(p + new Vector2(0, -5), Path(Mathf.Min(1, u + 0.01f), nest, food) - p, new Color(0.08f, 0.07f, 0.06f), false);
            float w = 1 - (_t * 0.07f + i / 9f + 0.05f) % 1f;
            var q = Path(w, nest, food);
            Ant(q + new Vector2(0, 5), Path(Mathf.Max(0, w - 0.01f), nest, food) - q, new Color(0.08f, 0.07f, 0.06f), true);
        }
        var font = UiTheme.Bold;
        DrawString(font, nest + new Vector2(-22, 46), "NEST", HorizontalAlignment.Left, -1, 14, UiTheme.Gold);
        DrawString(font, food + new Vector2(-20, 34), "FOOD", HorizontalAlignment.Left, -1, 14, UiTheme.Gold);
        DrawString(font, new Vector2(s.X * 0.04f, s.Y * 0.14f), "1  Scouts wander at random", HorizontalAlignment.Left, -1, 14, UiTheme.Ink);
        DrawString(font, Path(0.45f, nest, food) + new Vector2(-70, -26), "2  A finder lays a scent trail home", HorizontalAlignment.Left, -1, 14, new Color(0.6f, 1f, 0.65f));
        DrawString(font, Path(0.7f, nest, food) + new Vector2(-40, 44), "3  Recruits follow it out; each return makes it stronger", HorizontalAlignment.Left, -1, 14, UiTheme.Ink);
    }

    IEnumerable<(Vector2, float)> Zip() { for (int i = 0; i < _scout.Length; i++) yield return (_scout[i], _head[i]); }
}

/// One colony's card: portraits, real species, strengths and weaknesses.
public partial class RaceCard : PanelContainer
{
    public int Team;
    public Action<int> OnLead;

    public static Color RaceColor(Race r) => r == Race.BlackGarden ? new Color(0.9f, 0.82f, 0.62f) : new Color(1f, 0.42f, 0.28f);

    public static string Wild(Race r) => r == Race.BlackGarden
        ? "In the wild: Lasius niger is Europe's common garden ant. Colonies reach 10,000+ workers, herd aphids for their honeydew and defend themselves with sprayed formic acid instead of a sting."
        : "In the wild: Solenopsis invicta, the red imported fire ant, stings with alkaloid venom, hunts insects for protein and swarms out of its mound when disturbed. Many mounds hold several queens.";

    public override void _Ready()
    {
        var race = Config.RaceOf(Team);
        var rc = RaceColor(race);
        AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.07f, 0.055f, 0.04f, 1f), new Color(rc, 0.7f), 2, 10, 16));
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 6);
        AddChild(v);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        foreach (var t in new[] { AntType.Worker, AntType.Soldier, AntType.Major })
            row.AddChild(new AntPortrait { Type = t, Team = Team, CustomMinimumSize = new Vector2(200, 170) });
        v.AddChild(row);

        var name = UiTheme.Text(Config.RaceName(race).ToUpper(), 26, UiTheme.Title, rc);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(name);
        var latin = UiTheme.Text($"{Config.RaceLatin(race)}  ·  {Config.RaceMotto(race)}", 14, null, UiTheme.Dim);
        latin.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(latin);

        v.AddChild(Heading("STRENGTHS", new Color(0.55f, 0.9f, 0.45f)));
        foreach (var s in Config.RaceStrengths(race)) v.AddChild(Bullet(s, "+", new Color(0.55f, 0.9f, 0.45f)));
        v.AddChild(Heading("WEAKNESSES", new Color(1f, 0.5f, 0.38f)));
        foreach (var s in Config.RaceWeaknesses(race)) v.AddChild(Bullet(s, "−", new Color(1f, 0.5f, 0.38f)));
        var wild = UiTheme.Text(Wild(race), 13, null, new Color(UiTheme.Dim, 0.9f));
        wild.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        wild.CustomMinimumSize = new Vector2(380, 0);
        v.AddChild(wild);
        if (OnLead != null)
        {
            var b = new Button { Text = $"Lead the {Config.RaceName(race)}", CustomMinimumSize = new Vector2(0, 52) };
            b.AddThemeFontOverride("font", UiTheme.Title);
            b.AddThemeFontSizeOverride("font_size", 18);
            b.Pressed += () => OnLead(Team);
            v.AddChild(b);
        }
    }

    static Label Heading(string s, Color c)
    {
        var l = UiTheme.Text(s, 13, UiTheme.Bold, c);
        l.AddThemeConstantOverride("line_spacing", 0);
        return l;
    }

    static Control Bullet(string s, string mark, Color c)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 8);
        h.AddChild(UiTheme.Text(mark, 15, UiTheme.Bold, c));
        var l = UiTheme.Text(s, 14, null, UiTheme.Ink);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        l.CustomMinimumSize = new Vector2(340, 0);
        h.AddChild(l);
        return h;
    }
}

/// Full-screen chooser shown before a battle: pick which colony (race) to lead.
public partial class RacePicker : ColorRect
{
    public Action<int> OnPick;
    public Action OnBack;

    public override void _Ready()
    {
        Color = new Color(0, 0, 0, 0.55f);
        UiTheme.Place(this, 0, 0, 1, 1, 0, 0, 0, 0);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 14);
        UiTheme.Place(v, 0.04f, 0.03f, 0.96f, 0.97f, 0, 0, 0, 0);
        AddChild(v);
        var t = UiTheme.Text("CHOOSE YOUR COLONY", 34, UiTheme.Title, UiTheme.Gold);
        t.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(t);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var h = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        h.AddThemeConstantOverride("separation", 24);
        h.AddChild(new RaceCard { Team = 0, OnLead = OnPick });
        h.AddChild(new RaceCard { Team = 1, OnLead = OnPick });
        scroll.AddChild(h);
        v.AddChild(scroll);
        var back = new Button { Text = "Back", CustomMinimumSize = new Vector2(200, 46), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        back.Pressed += () => OnBack?.Invoke();
        v.AddChild(back);
    }
}

/// Lane War: choose the hero you will steer.
public partial class HeroPicker : ColorRect
{
    public int Team;
    public Action<AntType> OnPick;
    public Action OnBack;

    public override void _Ready()
    {
        Color = new Color(0, 0, 0, 0.55f);
        UiTheme.Place(this, 0, 0, 1, 1, 0, 0, 0, 0);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 14);
        UiTheme.Place(v, 0.06f, 0.06f, 0.94f, 0.94f, 0, 0, 0, 0);
        AddChild(v);
        var t = UiTheme.Text("CHOOSE YOUR HERO", 34, UiTheme.Title, UiTheme.Gold);
        t.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(t);
        var sub = UiTheme.Text("Lane War: three lanes, waves of soldiers on both sides, guardians as towers. You steer one hero; it levels up to 10 and buys upgrades with food.", 15, null, UiTheme.Dim);
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        sub.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        v.AddChild(sub);
        var h = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        h.AddThemeConstantOverride("separation", 18);
        foreach (var type in new[] { AntType.Warlord, AntType.Matron, AntType.Marshal })
        {
            var p = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            p.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.07f, 0.055f, 0.04f, 1f), new Color(UiTheme.Gold, 0.6f), 2, 10, 14));
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 8);
            col.AddChild(new AntPortrait { Type = type, Team = Team, CustomMinimumSize = new Vector2(0, 330) });
            var name = UiTheme.Text(Config.Of(type).Name.ToUpper(), 24, UiTheme.Title, UiTheme.Gold);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            col.AddChild(name);
            var st = Config.Of(type);
            var stats = UiTheme.Text($"HP {st.Hp}  ·  Bite {st.Damage}  ·  Speed {st.Speed}{(Config.Ranged(type) ? "  ·  ranged" : "")}{(Config.Flies(type) ? "  ·  flies" : "")}", 14, UiTheme.Bold, UiTheme.Dim);
            stats.HorizontalAlignment = HorizontalAlignment.Center;
            col.AddChild(stats);
            var blurb = UiTheme.Text(Lane.HeroBlurb(type), 15, null, UiTheme.Ink);
            blurb.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            blurb.CustomMinimumSize = new Vector2(240, 0);
            col.AddChild(blurb);
            var b = new Button { Text = $"Play the {st.Name}", CustomMinimumSize = new Vector2(0, 50) };
            b.AddThemeFontOverride("font", UiTheme.Title);
            b.AddThemeFontSizeOverride("font_size", 17);
            b.Pressed += () => OnPick?.Invoke(type);
            col.AddChild(b);
            p.AddChild(col);
            h.AddChild(p);
        }
        v.AddChild(h);
        var back = new Button { Text = "Back", CustomMinimumSize = new Vector2(200, 46), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        back.Pressed += () => OnBack?.Invoke();
        v.AddChild(back);
    }
}

/// The Field Guide: colonies, castes, wildlife, how foraging works, food and what a colony needs.
public partial class FieldGuide : ColorRect
{
    public Action OnClose;
    public string StartTab = "Colonies";
    Control _page;
    readonly List<Button> _tabs = new();
    int _casteTeam = 0;

    public override void _Ready()
    {
        Color = new Color(0, 0, 0, 0.6f);
        UiTheme.Place(this, 0, 0, 1, 1, 0, 0, 0, 0);
        var p = new PanelContainer();
        UiTheme.Place(p, 0.03f, 0.03f, 0.97f, 0.97f, 0, 0, 0, 0);
        AddChild(p);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        p.AddChild(v);

        var head = new HBoxContainer();
        var title = UiTheme.Text("FIELD GUIDE", 30, UiTheme.Title, UiTheme.Gold);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        var close = new Button { Text = "Close", CustomMinimumSize = new Vector2(120, 40) };
        close.Pressed += () => OnClose?.Invoke();
        head.AddChild(close);
        v.AddChild(head);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        foreach (var name in new[] { "Colonies", "The Nest", "Castes", "Wildlife", "Foraging", "Food", "Colony Needs" })
        {
            var b = new Button { Text = name, ToggleMode = true, CustomMinimumSize = new Vector2(130, 38) };
            b.Pressed += () => Show(name);
            tabs.AddChild(b);
            _tabs.Add(b);
        }
        v.AddChild(tabs);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _page = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(_page);
        v.AddChild(scroll);
        Show(StartTab);
    }

    void Show(string name)
    {
        foreach (var b in _tabs) b.ButtonPressed = b.Text == name;
        foreach (var c in _page.GetChildren()) c.QueueFree();
        _page.AddChild(name switch
        {
            "Colonies" => Colonies(),
            "The Nest" => NestPage(),
            "Castes" => Castes(),
            "Wildlife" => Wildlife(),
            "Foraging" => Foraging(),
            "Food" => Food(),
            _ => Needs(),
        });
    }

    static Control Colonies()
    {
        var h = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        h.AddThemeConstantOverride("separation", 20);
        h.AddChild(new RaceCard { Team = 0 });
        h.AddChild(new RaceCard { Team = 1 });
        return h;
    }

    static readonly (AntType t, string real)[] CasteList =
    {
        (AntType.Worker, "Real workers are sterile daughters of the queen. They forage, nurse larvae, dig, and share food mouth-to-mouth from a 'social stomach' (trophallaxis)."),
        (AntType.Soldier, "Many species raise a defensive subcaste with bigger heads and jaws. They patrol, guard trails and swarm intruders."),
        (AntType.Major, "Majors are the giant workers of species with several worker sizes (fire ants, carpenter ants). Their huge heads crush seeds and enemies."),
        (AntType.Gunner, "Ants of the formicine subfamily, like garden and wood ants, spray formic acid from a gland at the tip of the abdomen. Fire ants have no such gland."),
        (AntType.Flyer, "Winged ants are the young queens and males. They leave the nest together on warm days for the mating flight."),
        (AntType.Queen, "The only egg-layer. She needs protein to lay; a garden-ant queen can live almost 30 years. If she dies, the colony dies with her."),
        (AntType.Warlord, "Heroes are colony legends rather than real castes: one exceptional giant who leads the charge."),
        (AntType.Matron, "Inspired by the acid-spraying matriarchs of wood-ant colonies."),
        (AntType.Marshal, "Inspired by the winged queens, the largest and strongest flyers an ant colony produces."),
    };

    Control Castes()
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        var toggle = new HBoxContainer();
        toggle.AddChild(UiTheme.Text("Show colours of:", 14, UiTheme.Bold, UiTheme.Dim));
        for (int team = 0; team < 2; team++)
        {
            int tm = team;
            var b = new Button { Text = Config.RaceName(Config.RaceOf(team)), ToggleMode = true, ButtonPressed = _casteTeam == team };
            b.Pressed += () => { _casteTeam = tm; Show("Castes"); };
            toggle.AddChild(b);
        }
        v.AddChild(toggle);
        var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);
        foreach (var (t, real) in CasteList) grid.AddChild(Card(t, _casteTeam, CasteText(t, _casteTeam), real));
        v.AddChild(grid);
        return v;
    }

    static string CasteText(AntType t, int team)
    {
        var st = Config.Of(t);
        if (t == AntType.Queen) return $"HP {st.Hp}.  Lays all brood. Feed her royal jelly (research) to unlock new castes and stronger heroes.";
        var k = t switch
        {
            AntType.Soldier => ProdKind.Soldier, AntType.Major => ProdKind.Major, AntType.Flyer => ProdKind.Flyer, AntType.Gunner => ProdKind.Gunner,
            AntType.Warlord => ProdKind.Warlord, AntType.Matron => ProdKind.Matron, AntType.Marshal => ProdKind.Marshal, _ => ProdKind.Worker,
        };
        string squad = Config.SquadSize(t) > 1 ? $"Hatches as a squad of {Config.SquadSize(t)}.  " : "";
        string race = "";
        var r = Config.RaceOf(team);
        if (t == AntType.Gunner) race = r == Race.BlackGarden ? "  Black garden ants: +30% (formic acid)." : "  Red fire ants: −25% (no acid gland).";
        if (t is AntType.Soldier or AntType.Major or AntType.Warlord && r == Race.RedFire) race = "  Red fire ants: venomous sting.";
        return $"HP {st.Hp}  ·  Bite {st.Damage}  ·  Speed {st.Speed}  ·  Cost {Config.ProdCost(k):0}.  {squad}{Config.Role(k).Replace("\n", " ")}{race}";
    }

    static Control Card(AntType t, int team, string text, string real)
    {
        var p = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        p.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.1f, 0.08f, 0.06f, 0.92f), new Color(UiTheme.Bronze, 0.7f), 1, 8, 10));
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 10);
        h.AddChild(new AntPortrait { Type = t, Team = team, CustomMinimumSize = new Vector2(150, 150) });
        var v = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        v.AddChild(UiTheme.Text(Config.Of(t).Name.ToUpper(), 18, UiTheme.Title, Config.IsHero(t) ? UiTheme.Gold : UiTheme.Ink));
        var l = UiTheme.Text(text, 13, null, UiTheme.Ink);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(200, 0);
        v.AddChild(l);
        var rl = UiTheme.Text(real, 12, null, new Color(0.7f, 0.82f, 0.62f));
        rl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        rl.CustomMinimumSize = new Vector2(200, 0);
        v.AddChild(rl);
        h.AddChild(v);
        p.AddChild(h);
        return p;
    }

    static Control Wildlife()
    {
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);
        foreach (var (t, game, real) in new[]
        {
            (AntType.Beetle, "Guards food lairs. Mandible Sweep: a wide area attack that hits every ant around it.", "Predatory ground beetles hunt insects at night; their hard wing-cases shrug off ant bites."),
            (AntType.Spider, "Web Shot: spits sticky silk that freezes ants in place for a few seconds.", "Wolf spiders hunt by sight and pounce rather than spin webs; our spider cheats a little."),
            (AntType.StagBeetle, "The lord of the yard. Huge HP and a crushing sweep. Kill it for a feast.", "Male stag beetles wrestle rivals with antler-like mandibles."),
            (AntType.Wasp, "Flies. Venom Sting: a heavy strike on a cooldown. Only flyers and spitters can reach it easily.", "Hunting wasps snatch ants and other insects to feed their own larvae."),
        })
            grid.AddChild(Card(t, Config.Wild, $"HP {Config.Of(t).Hp}+  ·  levels 1-5.  {game}  Slay wild bugs to rank up your warriors and haul the carcass home as protein.", real));
        return grid;
    }

    static Control Foraging()
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 12);
        v.AddChild(new ForagingDiagram { CustomMinimumSize = new Vector2(0, 340) });
        v.AddChild(Rich(
            "[b]How real ants find food, and how yours do[/b]\n" +
            "[color=#e8c27a]1. Scouting.[/color]  Workers leave the nest and wander almost at random. In the game, workers with no orders do exactly this.\n" +
            "[color=#e8c27a]2. The find.[/color]  A scout that touches food takes one load and heads straight home, dragging the tip of her abdomen to lay a [b]pheromone trail[/b].\n" +
            "[color=#e8c27a]3. Recruitment.[/color]  Nestmates sense the trail with their antennae and follow it outward. Every ant that comes back loaded lays more scent, so a rich source quickly draws a highway of workers.\n" +
            "[color=#e8c27a]4. Evaporation.[/color]  The scent fades within minutes. When the food runs out nobody re-lays it, the trail vanishes and workers spread out to scout again.\n" +
            "[color=#e8c27a]5. Sharing.[/color]  At home the load is passed mouth-to-mouth from a 'social stomach', so the whole colony is fed.\n\n" +
            "[b]In battle[/b]  Press [b]Tab[/b] to see your trails glowing on the ground. Right-click food with workers to send them to it directly. " +
            "Black garden ants lay trails 35% stronger, so they recruit faster; red fire ants' trails are fainter. Workers fight back if bitten and swarm out if the queen is attacked."));
        return v;
    }

    static Control Food()
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        v.AddChild(Rich("Each pile is made of [b]loads[/b]; one worker carries one load. Click any food in battle to see what it is and how much is left. " +
            "Effects last while that food keeps arriving: each load adds 1.2 seconds, up to 30."));
        foreach (FoodKind k in new[] { FoodKind.Sugar, FoodKind.Berry, FoodKind.Corpse, FoodKind.Seed, FoodKind.Crumb })
        {
            var p = new PanelContainer();
            p.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.1f, 0.08f, 0.06f, 0.92f), new Color(FoodIcon.Tint(k), 0.6f), 1, 8, 10));
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 14);
            h.AddChild(new FoodIcon { Kind = k, CustomMinimumSize = new Vector2(64, 64) });
            var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            col.AddChild(UiTheme.Text(Config.FoodName(k).ToUpper(), 18, UiTheme.Title, FoodIcon.Tint(k)));
            string fav = Config.Favoured(Race.BlackGarden, k) ? "Favoured by black garden ants (+20%)." : Config.Favoured(Race.RedFire, k) ? "Favoured by red fire ants (+25%)." : "";
            var l = UiTheme.Text($"{Config.FoodNutrient(k)}  ·  {Config.FoodValue(k):0} food per load.  {Config.FoodEffect(k)}  {fav}", 14, null, UiTheme.Ink);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.CustomMinimumSize = new Vector2(400, 0);
            col.AddChild(l);
            h.AddChild(col);
            p.AddChild(h);
            v.AddChild(p);
        }
        return v;
    }

    static Control NestPage()
    {
        return Rich(
            "[b]How a colony is founded, and how the nest grows[/b]\n\n" +
            "[color=#e8c27a]Founding.[/color]  A real colony begins as a single mated queen (in this game: a queen with a few workers) on bare soil. The workers dig a shaft; until it is done the queen is exposed on the surface. " +
            "[i]In game:[/i] your workers dig at once. A plug of soil over the entrance sinks as they go, with the progress shown above it. When the shaft and royal chamber are ready the queen walks down and only then starts to lay.\n\n" +
            "[color=#e8c27a]Depth has a purpose.[/color]  Brood is kept shallow where the sun warms the soil, stores sit at mid depth, and the queen lives deepest, farthest from danger. " +
            "[i]In game:[/i] rooms are laid out the same way. New ants hatch in the brood chambers and climb out of the shaft on their own.\n\n" +
            "[color=#e8c27a]Workers build everything.[/color]  There are no builders or buildings: the same workers that forage dig new rooms with their mandibles, carrying the soil out grain by grain. " +
            "[i]In game:[/i] order a room (V brood chamber, L store, Q W E R T the upgrade rooms), then click where it should go. Every room must connect to the tunnel network, like roads in a Settlers town: it has to lie within reach of the entrance or a room already dug, and the workers dig the gallery first, then the room. Up to four workers dig at once; the time depends on how many are digging and how long the gallery is. Brood chambers raise how many ants you can keep; stores raise how much food you can hold; the other rooms carry the colony's upgrades.\n\n" +
            "[color=#e8c27a]The nest is a fortress.[/color]  Raiders have to come in through the entrance and down the galleries, where the defenders meet them. " +
            "[i]In game:[/i] enemies at your mound break in and head for the royal chamber. Right-click your own nest with warriors to garrison it; when the queen is attacked, nearby ants rush below.\n\n" +
            "[color=#e8c27a]Seeing inside.[/color]  Press [b]N[/b] for Glass Earth: the ground over your nest turns translucent and the galleries and rooms show at their real depths, with eggs, stored food and the queen inside. The surface and everything approaching stays visible around and over the glass, so you never lose sight of the yard while you look.");
    }

    static Control Needs()
    {
        return Rich(
            "[b]What a real ant colony needs, and how it works in Mandible[/b]\n\n" +
            "[color=#e8c27a]Protein, for the young.[/color]  Larvae grow on protein, mostly other insects, and a queen needs it to keep laying. " +
            "[i]In game:[/i] insect carcasses make the queen lay 30% faster while they keep coming in. Kill wild bugs and haul them home.\n\n" +
            "[color=#e8c27a]Sugar, for the workers.[/color]  Adult workers run on carbohydrates: honeydew from aphids, nectar, fruit juice. " +
            "[i]In game:[/i] honeydew makes workers 15% faster; it is the black garden ants' speciality.\n\n" +
            "[color=#e8c27a]Water.[/color]  Ants dry out quickly and drink from dew and juicy food. " +
            "[i]In game:[/i] fruit lets every ant slowly regenerate.\n\n" +
            "[color=#e8c27a]Room to grow.[/color]  A colony digs new chambers as it grows; brood needs space and the right warmth. " +
            "[i]In game:[/i] excavate chambers (V) to raise your brood limit.\n\n" +
            "[color=#e8c27a]A queen.[/color]  Only the queen lays eggs. Losing her ends the colony. " +
            "[i]In game:[/i] protect her; destroy the enemy queen to win. Feeding her royal jelly (research) unlocks majors, gunners, flyers and heroes.\n\n" +
            "[color=#e8c27a]Defence.[/color]  Food draws rivals and predators. Colonies keep fighters at home and swarm intruders. " +
            "[i]In game:[/i] squads that come home (B) heal and replace their fallen.");
    }

    static RichTextLabel Rich(string bb)
    {
        var r = new RichTextLabel { BbcodeEnabled = true, FitContent = true, Text = bb, SizeFlagsHorizontal = SizeFlags.ExpandFill, ScrollActive = false };
        r.AddThemeFontOverride("normal_font", UiTheme.Body);
        r.AddThemeFontOverride("bold_font", UiTheme.Bold);
        r.AddThemeFontOverride("italics_font", UiTheme.Body);
        foreach (var f in new[] { "normal_font_size", "bold_font_size", "italics_font_size" }) r.AddThemeFontSizeOverride(f, 16);
        r.AddThemeColorOverride("default_color", UiTheme.Ink);
        return r;
    }
}
