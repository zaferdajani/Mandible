using System;
using System.Collections.Generic;
using Godot;
using GEnv = Godot.Environment;

namespace Mandible;

/// The selection panel's living portrait: the creature's own in-game model (same mesh, same Ant shader, same
/// instance data as on the battlefield) drawn in one small viewport, framed on the part that makes it what it is.
/// It idles (glances, antennae, jaws, grooming), answers a click or an order in time with the creature's call,
/// mirrors what the real ant is doing (its bite, its flinch, its lost limbs, its acid scars) and hangs its head when
/// it is hurt. One viewport, redrawn at 30 frames a second at most and only while it is on screen.
public partial class LivePortrait : TextureRect
{
    /// What the portrait shows this frame. The HUD fills it from the sim; the capture fills it by hand.
    public struct Subject
    {
        public int Team; public AntType Type;
        public int Seed;            // which individual (size, rhythm): the ant's serial
        public float Health;        // 0..1
        public float HitFlash, Bite; // the real ant's hit flash and attack swing
        public int Lost, Etch, Stage; // missing parts, acid pitting, evolution stage / general's tier (as the Ant shader takes them)
        public bool Block, Charge, Carry;
        public bool Sting;          // a winged ant that carries a wasp's sting: drawn with the stinger's body
        public bool Speaking;       // the queen's telepathic voice is sounding
        public float Voice;         // how loud it is right now, 0..1, or below 0 when unknown
    }

    enum Focus { Head, Tip, Body, Wings }
    /// The portrait rig of one creature: what the lens is on, from where (degrees round from dead ahead, degrees
    /// above), and how tight.
    /// R: how much of the creature the frame holds (model units, 0 = worked out from its body); Fwd: how far ahead
    /// of the neck the lens aims.
    readonly record struct Rig(Focus On, float Az, float El, float R = 0, float Fwd = 0, float Lift = 0);

    static Rig RigOf(AntType t) => t switch
    {
        AntType.Queen => new(Focus.Head, 30, 5, 0.2f, 0.1f, 0.02f),
        AntType.Worker => new(Focus.Head, -30, 3, 0.21f, 0.16f, -0.02f),
        AntType.Soldier => new(Focus.Head, 18, 3, 0.23f, 0.1f),          // low, on the jaws
        AntType.Major => new(Focus.Head, 26, 6, 0.28f, 0.14f, -0.01f),          // the great head
        AntType.Flyer => new(Focus.Wings, 52, 16, 0.33f),                // wings and eyes
        AntType.Gunner => new(Focus.Tip, 148, 14),                       // the gaster tip she fires from
        AntType.Warlord => new(Focus.Head, 26, 5, 0.34f, 0.2f, -0.01f),
        AntType.Matron => new(Focus.Head, 22, 8, 0.27f, 0.12f, -0.03f),
        AntType.Marshal => new(Focus.Wings, 40, 14, 0.36f),
        AntType.WebAnt => new(Focus.Tip, 156, 16),                       // the spinnerets
        AntType.Armoured => new(Focus.Body, 42, 26, 0.5f),               // the shell
        AntType.ScorpionAnt => new(Focus.Body, 62, 16, 0.56f),           // the tail arched over her back
        AntType.Scorpion or AntType.Scorpling => new(Focus.Body, 34, 24, 0.6f), // claws, back and tail
        AntType.Beetle => new(Focus.Body, 128, 34, 0.56f),               // the wing cases
        AntType.StagBeetle => new(Focus.Head, 28, 20, 0.42f, 0.3f, 0.02f),     // the antlers
        AntType.Spider => new(Focus.Head, 12, 6, 0.36f, 0.13f),          // eyes and fangs
        AntType.Wasp => new(Focus.Head, 22, 6, 0.19f, 0.1f),
        _ => new(Focus.Head, 24, 10, 0.2f, 0.07f),
    };

    sealed class Model
    {
        public Node3D Root; public Batch Body, Wings;
        public Vector3 Target; public float Dist, Floor; public Rig Rig;
    }

    SubViewport _vp;
    Camera3D _cam;
    CameraAttributesPractical _lens;
    MeshInstance3D _ground, _backdrop;
    Control _wound;
    readonly Dictionary<(int, AntType, bool), Model> _models = new();
    Model _cur;
    Subject _s;
    bool _has;
    float _t, _acc = 1, _yaw, _pitch, _sacT, _sacYaw, _sacPitch, _groomT = 6, _groomLeft;
    int _kind; float _kindStart, _kindLen;
    float _talk, _talkUntil, _buzz, _snap, _snapUntil, _hurt, _lastHit, _heardPeak;
    readonly Random _rnd = new(7);

    /// Low keeps the portrait small and at half the rate; everywhere else 30 a second.
    static bool Small => Settings.Gfx == Settings.Quality.Low;
    public static bool Enabled => Settings.LivePortrait;

    public override void _Ready()
    {
        ExpandMode = ExpandModeEnum.IgnoreSize; StretchMode = StretchModeEnum.KeepAspectCovered; MouseFilter = MouseFilterEnum.Ignore;
        _vp = new SubViewport
        {
            OwnWorld3D = true, TransparentBg = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            HandleInputLocally = false, GuiDisableInput = true, PositionalShadowAtlasSize = 0,
        };
        AddChild(_vp);
        ApplyQuality();
        var dusk = new Color(0.035f, 0.028f, 0.018f); // the dark behind a macro subject: the creature is lit, the world is not
        var env = new GEnv
        {
            BackgroundMode = GEnv.BGMode.Color, BackgroundColor = dusk,
            AmbientLightSource = GEnv.AmbientSource.Color, AmbientLightColor = new Color(0.8f, 0.62f, 0.46f), AmbientLightEnergy = 0.6f,
            TonemapMode = GEnv.ToneMapper.Aces, TonemapExposure = 1.4f, TonemapWhite = 6,
            FogEnabled = true, FogLightColor = dusk, FogDensity = 0.1f, FogSkyAffect = 1,
            AdjustmentEnabled = true, AdjustmentSaturation = 0.95f, AdjustmentContrast = 1.08f,
        };
        // a warm key from front-left, a rim from behind that traces the chitin, a low amber bounce: no shadows to pay for
        _vp.AddChild(new DirectionalLight3D { LightEnergy = 3.8f, LightColor = new Color(1f, 0.9f, 0.72f), RotationDegrees = new Vector3(-30, -40, 0), LightCullMask = 1 });
        _vp.AddChild(new DirectionalLight3D { LightEnergy = 3.4f, LightColor = new Color(1f, 0.78f, 0.48f), RotationDegrees = new Vector3(-20, 160, 0), LightSpecular = 1.5f, LightCullMask = 1 });
        _vp.AddChild(new DirectionalLight3D { LightEnergy = 1.6f, LightColor = new Color(1f, 0.74f, 0.5f), RotationDegrees = new Vector3(-50, -30, 0), LightSpecular = 0.2f });
        // the ground she stands on: the nest's own soil photograph, thrown out of focus by the lens
        var soil = new StandardMaterial3D
        {
            AlbedoTexture = GD.Load<Texture2D>("res://assets/materials/nest-soil-albedo.png"), AlbedoColor = new Color(0.2f, 0.23f, 0.22f),
            Uv1Scale = new Vector3(110, 110, 1), Roughness = 0.95f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
        };
        _ground = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(240, 240) }, MaterialOverride = soil, Layers = 2 }; // layer 2: only the low bounce light reaches the ground, so it stays in shade under her
        _vp.AddChild(_ground);
        // far behind her, out of focus: the forest floor in low light (the game's own bark-litter photograph), carried by the lens
        _backdrop = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(1, 1) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = GD.Load<Texture2D>("res://assets/nature/tex/bark_debris_01_diff.jpg"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(0.62f, 0.56f, 0.3f), DisableFog = true, Uv1Scale = new Vector3(0.6f, 0.6f, 1), Uv1Offset = new Vector3(0.2f, 0.15f, 0),
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
            },
        };
        _lens = new CameraAttributesPractical { DofBlurFarEnabled = true, DofBlurAmount = 0.12f };
        _cam = new Camera3D { Fov = 26, Near = 0.02f, Far = 40, Current = true, Attributes = _lens, Environment = env };
        _vp.AddChild(_cam);
        _cam.AddChild(_backdrop);
        Texture = _vp.GetTexture();
        // hurt: the edges of the frame darken to the colour of hemolymph and pulse with the breath
        var g = new Gradient { Offsets = new[] { 0f, 0.55f, 1f }, Colors = new[] { new Color(0.3f, 0.02f, 0f, 0f), new Color(0.3f, 0.02f, 0f, 0f), new Color(0.32f, 0.03f, 0.01f, 0.95f) } };
        _wound = new TextureRect
        {
            Texture = new GradientTexture2D { Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1.1f, 0.5f), Width = 64, Height = 64 },
            ExpandMode = ExpandModeEnum.IgnoreSize, StretchMode = StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0),
        };
        _wound.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_wound);
    }

    Vector2I _vpSize;
    public void ApplyQuality()
    {
        if (_vp == null) return;
        // the frame in the panel is 155 x 190: the same shape, about 256 tall
        var size = Small ? new Vector2I(156, 192) : new Vector2I(208, 256);
        if (size == _vpSize) return;
        _vpSize = size; _vp.Size = size;
        _vp.Msaa3D = Small ? Viewport.Msaa.Disabled : Viewport.Msaa.Msaa2X;
        if (_lens != null) _lens.DofBlurFarEnabled = !Small;
    }

    Model Load(int team, AntType type, bool sting = false)
    {
        if (_models.TryGetValue((team, type, sting), out var m)) return m;
        m = new Model { Root = new Node3D { Name = $"Portrait{team}_{type}", Visible = false }, Rig = RigOf(type) };
        _vp.AddChild(m.Root);
        var mesh = AntMesh.Build(type, false, team, team == Config.Wild ? null : AntMesh.Detail.Near);
        if (sting && type == AntType.Flyer && team is 0 or 1 && AntMesh.Stinger(team, AntMesh.Detail.Near) is { } stinger) mesh = stinger;
        var mat = AntRenderer.AntMat(Shaders.Get(Shaders.Ant), team, type, false);
        mat.SetShaderParameter("leg_count", 0); // a portrait sitter stands at rest: no planted stride, or a foreleg is held out across her face
        m.Body = new Batch(m.Root, mesh, mat, false, "Body");
        var box = mesh.GetAabb();
        if (type is AntType.Flyer or AntType.Marshal or AntType.Wasp)
        {
            var wm = new ShaderMaterial { Shader = Shaders.Get(Shaders.Wing) };
            if (type == AntType.Wasp) { wm.SetShaderParameter("tint", new Color(0.82f, 0.62f, 0.3f)); wm.SetShaderParameter("vein", new Color(0.35f, 0.2f, 0.05f)); }
            if (type == AntType.Marshal) { wm.SetShaderParameter("tint", new Color(0.95f, 0.85f, 0.6f)); wm.SetShaderParameter("vein", new Color(0.6f, 0.42f, 0.1f)); }
            m.Wings = new Batch(m.Root, AntMesh.Wings(type), wm, false, "Wings");
            m.Wings.Node.SortingOffset = 5;
        }
        m.Floor = box.Position.Y;
        var neck = team == Config.Wild ? AntMesh.Neck.GetValueOrDefault(type, new Vector3(0, box.GetCenter().Y, box.End.Z - box.Size.Z * 0.25f)) : AntMesh.NeckOf(type, team);
        float r = m.Rig.R;
        switch (m.Rig.On)
        {
            case Focus.Tip:
                var waist = AntMesh.WaistOf(type, team); var tip = AntMesh.GasterTip(type, team);
                m.Target = waist.Lerp(tip, 0.62f); if (r <= 0) r = waist.DistanceTo(tip) * 0.75f;
                break;
            case Focus.Body:
                m.Target = box.GetCenter();
                break;
            case Focus.Wings:
                m.Target = new Vector3(0, neck.Y + box.Size.Y * 0.12f, Mathf.Lerp(box.GetCenter().Z, neck.Z, 0.7f));
                break;
            default:
                m.Target = neck + new Vector3(0, 0, m.Rig.Fwd);
                break;
        }
        m.Target.Y += m.Rig.Lift;
        m.Dist = r / Mathf.Tan(Mathf.DegToRad(13));
        _models[(team, type, sting)] = m;
        return m;
    }

    /// The HUD has something alive to show.
    public void Show(in Subject s)
    {
        bool swap = !_has || s.Team != _s.Team || s.Type != _s.Type || s.Sting != _s.Sting;
        bool other = swap || s.Seed != _s.Seed;
        _s = s; _has = true;
        if (swap)
        {
            if (_cur != null) _cur.Root.Visible = false;
            _cur = Load(s.Team, s.Type, s.Sting);
            _cur.Root.Visible = true;
            _ground.Position = new Vector3(0, _cur.Floor, 0);
            _yaw = 0; _pitch = 0; _kind = 0; _talk = 0; _buzz = 0.6f; _sacT = 0.4f;
        }
        if (other) { _lastHit = s.HitFlash; _hurt = 1 - s.Health; _acc = 1; }
        Visible = true;
    }

    public void Clear() { _has = false; Visible = false; }
    /// Whose portrait is up (tests).
    public (int team, AntType type) Shown => _has ? (_s.Team, _s.Type) : (-1, AntType.Worker);

    /// The creature just called: clicked ("voc") or ordered ("ack"). Lasts as long as the call does.
    public void React(bool order, float seconds = 0.55f)
    {
        _kind = order ? 1 : 3; _kindStart = _t; _kindLen = order ? 0.45f : 0.6f;
        _talkUntil = _t + seconds; _buzz = 1;
        if (order) { _snap = _rnd.Next(2) == 0 ? -0.55f : 0.55f; _snapUntil = _t + 0.7f; }
    }

    /// Every frame from the HUD. Animates, and asks for a new picture when one is due.
    public void Advance(float dt)
    {
        if (!_has || _cur == null || !IsVisibleInTree()) return;
        ApplyQuality();
        _t += dt;
        float ease = 1 - Mathf.Exp(-dt * 7);
        // hurt builds as health falls; a fresh wound flinches
        _hurt += (Mathf.Clamp(1 - _s.Health, 0, 1) - _hurt) * (1 - Mathf.Exp(-dt * 3));
        if (_s.HitFlash > _lastHit + 0.5f && (_kind == 0 || _t > _kindStart + _kindLen)) { _kind = 2; _kindStart = _t; _kindLen = 0.25f; }
        _lastHit = _s.HitFlash;
        if (_kind != 0 && _t > _kindStart + _kindLen) _kind = 0;
        float bad = Mathf.SmoothStep(0.35f, 0.8f, _hurt);

        // speech: the queen's voice opens her jaws with its loudness; a creature's call chatters for as long as it sounds
        bool calling = _t < _talkUntil;
        float want = 0;
        if (_s.Speaking)
        {
            if (_s.Voice >= 0) _heardPeak = Mathf.Max(_heardPeak, _s.Voice);
            float syll = Mathf.Clamp(0.45f + 0.4f * Mathf.Sin(_t * 15.7f) + 0.3f * Mathf.Sin(_t * 9.1f + 1.3f), 0, 1) * Mathf.Clamp(0.6f + 0.6f * Mathf.Sin(_t * 2.1f), 0, 1);
            want = _heardPeak > 0.05f ? Mathf.Clamp(_s.Voice * 1.5f, 0, 1) * (0.55f + 0.45f * syll) : syll;
        }
        else
        {
            _heardPeak = 0;
            if (calling) want = Mathf.Clamp(0.5f + 0.5f * Mathf.Sin(_t * 26f), 0, 1) * Mathf.Clamp((_talkUntil - _t) * 5, 0, 1);
        }
        _talk += (want - _talk) * (1 - Mathf.Exp(-dt * 22));
        _buzz = Mathf.Max(0, _buzz - dt * 1.6f);

        // where she looks: idle glances, a slow turn of the head while speaking, a snap to the order, hanging when hurt
        _sacT -= dt;
        if (_sacT <= 0)
        {
            _sacT = Mathf.Lerp(1f, 3f, (float)_rnd.NextDouble()) * (1 + bad);
            _sacYaw = Mathf.Lerp(-0.38f, 0.38f, (float)_rnd.NextDouble()) * (1 - 0.5f * bad);
            _sacPitch = Mathf.Lerp(-0.12f, 0.16f, (float)_rnd.NextDouble());
        }
        // a face portrait: the head keeps half-turned to the lens (+ yaw is toward the mesh's -X)
        float face = _cur.Rig.On == Focus.Head ? -Mathf.DegToRad(_cur.Rig.Az) * 0.45f : 0;
        float wantYaw = face + _sacYaw, wantPitch = _sacPitch;
        if (_s.Speaking) { wantYaw = face + 0.34f * Mathf.Sin(_t * 0.9f) + 0.1f * Mathf.Sin(_t * 2.7f); wantPitch = 0.1f + 0.1f * Mathf.Sin(_t * 1.7f) + 0.08f * _talk; }
        if (_t < _snapUntil) { wantYaw = face + _snap; wantPitch = 0.05f; }
        if (_cur.Rig.On == Focus.Head) wantPitch += 0.14f; // chin up for the lens
        wantPitch -= 0.4f * bad;
        wantYaw += 0.03f * bad * Mathf.Sin(_t * 33f); // a tremor
        float k = _t < _snapUntil ? Mathf.Min(1, ease * 2.5f) : ease;
        _yaw += (wantYaw - _yaw) * k;
        _pitch += (Mathf.Clamp(wantPitch, -0.6f, 0.6f) - _pitch) * k;

        // now and then she cleans her antennae (not while she is busy or hurt)
        _groomT -= dt;
        if (_groomT <= 0) { _groomT = Mathf.Lerp(7f, 14f, (float)_rnd.NextDouble()); _groomLeft = 1.6f; }
        _groomLeft -= dt;
        bool busy = _s.Speaking || calling || _kind != 0 || _s.Bite > 0.05f || bad > 0.3f;
        int pose = _groomLeft > 0 && !busy && _cur.Rig.On == Focus.Head ? 3 : 0;

        _acc += dt;
        if (_acc < (Small ? 1f / 15 : 1f / 30)) return;
        _acc = 0;

        float bite = Mathf.Max(_s.Bite, _talk * (_cur.Rig.On == Focus.Tip ? 0.5f : 0.6f));
        var data = AntRenderer.Pack(_s.Seed, pose, 0, _yaw, _pitch, 0, _s.Carry, bite, _kind, _kind != 0 ? (_t - _kindStart) / _kindLen : 0);
        // the wounds of the real ant: its hit flash, lost limbs and acid pits; her head hangs and trembles when she is badly hurt
        data.B += (_s.Block ? 2 : 0) + (_s.Charge ? 4 : 0) + 64 * (int)Mathf.Clamp(_s.HitFlash * 15f, 0, 15) + 1024 * _s.Lost;
        data.A += 64 * Math.Clamp(_s.Etch, 0, 7) + 512 * Math.Clamp(_s.Stage, 0, 3);
        _cur.Body.Begin(); _cur.Body.Add(Basis.Identity, Vector3.Zero, data); _cur.Body.End();
        if (_cur.Wings != null)
        {
            // folded and still, a shiver now and then; they whirr when she answers
            float shiver = Mathf.Max(0, Mathf.Sin(_t * 0.7f + 1) - 0.9f) * 3f;
            _cur.Wings.Begin(); _cur.Wings.Add(Basis.Identity, Vector3.Zero, new Color(_s.Type == AntType.Wasp ? 46f : 36f, Mathf.Clamp(0.05f + 0.9f * _buzz + shiver, 0, 1), 0, 0)); _cur.Wings.End();
        }

        // the lens: a hand-held macro, breathing a little
        var rig = _cur.Rig;
        float az = Mathf.DegToRad(rig.Az + 1.6f * Mathf.Sin(_t * 0.31f)), el = Mathf.DegToRad(rig.El + 1.0f * Mathf.Sin(_t * 0.43f + 2));
        var dir = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
        float dist = _cur.Dist * (1 + 0.012f * Mathf.Sin(_t * 0.5f));
        _cam.LookAtFromPosition(_cur.Target + dir * dist, _cur.Target, Vector3.Up);
        _backdrop.Position = new Vector3(0, dist * 0.9f, -dist * 6); _backdrop.Scale = Vector3.One * dist * 5.5f;
        _lens.DofBlurFarDistance = dist * 1.25f; _lens.DofBlurFarTransition = dist * 1.2f;
        _wound.Modulate = new Color(1, 1, 1, bad * (0.55f + 0.25f * Mathf.Sin(_t * 3.2f)));
        _vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    /// The picture as last drawn (captures and tests).
    public Image Snapshot() => _vp.GetTexture().GetImage();
}
