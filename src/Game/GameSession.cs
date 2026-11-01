using System.Collections.Generic;
using Godot;

namespace Mandible;

/// One battle (or the attract-mode demo behind the main menu).
public partial class GameSession : Node3D
{
    public bool Demo;
    public int FastForward;
    public Difficulty Diff = Difficulty.Normal;
    public Main App;
    public World World;
    public Sim Sim;
    public AntRenderer Ants;
    public Effects Effects;
    public FogOfWar Fog;
    public RtsCamera Camera;
    public Hud Hud;
    public PlayerInput Input;
    public AudioDirector Audio => App.Audio;
    public NestInterior Interior;
    public Lockstep Net;          // null in single player
    public uint Seed = 20261031;
    public IOrders Orders;
    public bool AutoPlayLocal;    // network self-test: an AI plays our colony through the network
    public GameMode Mode = GameMode.Colony;
    public bool TutorialMode;     // a guided first battle
    AIController _tutorialFoe;
    public Tutorial Tutor;
    public AntType[] Heroes = { AntType.Warlord, AntType.Warlord }; // Lane War hero of each team
    public bool Lanes => Mode == GameMode.Lanes;
    const float TickDt = 1f / 30f;
    float _acc, _alpha, _stall;
    bool _netEnded;
    readonly List<IColonyAI> _ai = new();
    Image _pherImg;
    ImageTexture _pherTex;
    byte[] _pherPx;
    float _pherAcc, _trailView = 0.18f, _trailTarget = 0.18f;
    bool _paused, _ended;
    float _combat, _queenAlertCd, _attackAlertCd, _popAlertCd, _levelAlertCd;
    float _demoT;
    DirectionalLight3D _sun;
    /// The one light in the world: a mid-morning sun. Everything, above and below ground, is lit and shadowed by it.
    public static readonly Vector3 SunAngles = new(-48, 140, 0);

    public override void _Ready()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        World = WorldBuilder.Build(this, Seed);
        long tWorld = sw.ElapsedMilliseconds;
        Sim = new Sim(World, Mode, Heroes[0], Heroes[1]);
        Orders = Net != null ? Net : new DirectOrders(Sim);
        long tSim = sw.ElapsedMilliseconds;
        Ants = new AntRenderer(this);
        long tAnts = sw.ElapsedMilliseconds;
        Effects = new Effects(this, World.HF);
        Interior = new NestInterior(this);
        BuildLighting();
        if (Lanes) BuildLaneDressing();
        GD.Print($"[startup] session: world {tWorld} ms, sim {tSim - tWorld} ms, ant meshes {tAnts - tSim} ms, rest {sw.ElapsedMilliseconds - tAnts} ms");

        Camera = new RtsCamera { HF = World.HF, UserControl = !Demo, EdgeScroll = !Demo && !Settings.Mobile && !App.TestRun }; // touch laptops still have a mouse; test runs leave the cursor wherever it is
        AddChild(Camera);
        var home = World.Nests[Local.Me].Pos;
        // start behind our own nest, looking out toward the middle of the yard
        var outward = (home - new Vector2(Config.MapSize, Config.MapSize) * 0.5f).Normalized();
        Camera.JumpTo(home - outward * 14, 62);
        Camera.SetYaw(Mathf.Atan2(outward.X, outward.Y), true);

        _pherPx = new byte[Sim.Ph.N * Sim.Ph.N];
        _pherImg = Image.CreateFromData(Sim.Ph.N, Sim.Ph.N, false, Image.Format.R8, _pherPx);
        _pherTex = ImageTexture.CreateFromImage(_pherImg);
        World.TerrainMaterial.SetShaderParameter("pher_tex", _pherTex);

        if (Demo)
        {
            _ai.Add(new AIController(Sim, Local.Me, Difficulty.Normal));
            _ai.Add(new AIController(Sim, Local.Foe, Difficulty.Normal));
            Ants.VisibleToPlayer = _ => true;
            _trailTarget = _trailView = 0.12f;
        }
        else
        {
            Fog = new FogOfWar(World);
            Ants.VisibleToPlayer = Fog.Visible;
            if (Lanes)
            {
                if (Net == null) _ai.Add(new LaneAI(Sim, Local.Foe, Diff));
                if (AutoPlayLocal) _ai.Add(new LaneAI(Sim, Local.Me, Difficulty.Normal) { Orders = Orders });
            }
            else
            {
                // the tutorial's opponent stays peaceful until the lessons are done
                var foe = new AIController(Sim, Local.Foe, Diff) { Passive = TutorialMode };
                if (Net == null) _ai.Add(foe);
                if (AutoPlayLocal) _ai.Add(new AIController(Sim, Local.Me, Difficulty.Normal) { Orders = Orders });
                _tutorialFoe = foe;
            }
            if (Net != null)
            {
                Net.OnDisconnected += () => { if (!_ended) { _netEnded = true; } };
            }
            Input = new PlayerInput { Game = this };
            AddChild(Input);
            Hud = new Hud { Game = this };
            AddChild(Hud);
            if (TutorialMode)
            {
                Sim.Col[Local.Me].Food = 600; // enough to try every step
                var foe = _tutorialFoe;
                Tutor = new Tutorial { Game = this, OnFinished = () => foe.Passive = false };
                Hud.AddPanel(Tutor);
                Hud.Alert("Tutorial: follow the card on the left", UiTheme.Gold);
            }
            else if (Lanes) Hud.Alert($"Lane War: guide your {Config.Of(Heroes[Local.Me]).Name} down the lanes. Break the guardians, then kill the enemy queen.");
            else Hud.Alert(Net == null ? $"You lead the {Local.ColonyName(Local.Me)}. Destroy the {Local.ColonyName(Local.Foe)}' queen." : $"You lead the {Local.ColonyName(Local.Me)}. Destroy {Net.PeerName}'s queen.");
        }
    }

    Environment _env;

    /// One sun, one sky. The ProceduralSkyMaterial draws its sun disc where the DirectionalLight3D points, the
    /// ambient and reflections come from that sky, and the sun is the only light in the scene: every shadow in
    /// the yard falls the same way. The key light is strong and warm and the sky fill cool and modest, so the
    /// lit and shadowed sides of an ant differ the way they do outdoors at mid-morning.
    void BuildLighting()
    {
        _env = new Environment
        {
            BackgroundMode = Environment.BGMode.Sky,
            Sky = new Sky
            {
                SkyMaterial = new ProceduralSkyMaterial
                {
                    SkyTopColor = new Color(0.30f, 0.50f, 0.80f), SkyHorizonColor = new Color(0.76f, 0.80f, 0.82f),
                    GroundHorizonColor = new Color(0.52f, 0.46f, 0.37f), GroundBottomColor = new Color(0.2f, 0.16f, 0.12f),
                    SunAngleMax = 30, SunCurve = 0.12f, SkyEnergyMultiplier = 1.0f,
                },
            },
            AmbientLightSource = Environment.AmbientSource.Sky,
            AmbientLightSkyContribution = 1.0f,
            AmbientLightEnergy = 0.42f,
            ReflectedLightSource = Environment.ReflectionSource.Sky,
            TonemapMode = Environment.ToneMapper.Aces,
            TonemapExposure = 1.05f,
            TonemapWhite = 6f,
            SsaoRadius = 1.2f, SsaoIntensity = 2.2f, SsaoPower = 1.5f, SsaoDetail = 0.8f,
            SsilRadius = 5f, SsilIntensity = 0.45f,
            GlowIntensity = 0.4f, GlowBloom = 0.03f, GlowHdrThreshold = 1.15f,
            FogEnabled = true, FogLightColor = new Color(0.74f, 0.77f, 0.76f), FogDensity = 0.0011f, FogAerialPerspective = 0.3f, FogSkyAffect = 0.3f,
            // Ultra: a breath of dust in the air that the sun shafts through; set up once, switched by the tier
            VolumetricFogDensity = 0.004f, VolumetricFogAlbedo = new Color(0.95f, 0.9f, 0.82f), VolumetricFogEmission = new Color(0.2f, 0.17f, 0.12f), VolumetricFogEmissionEnergy = 0.02f,
            VolumetricFogAnisotropy = 0.55f, VolumetricFogLength = 180f, VolumetricFogDetailSpread = 1.6f, VolumetricFogAmbientInject = 0.15f, VolumetricFogSkyAffect = 0.2f,
            AdjustmentEnabled = true, AdjustmentSaturation = 1.1f, AdjustmentContrast = 1.07f, AdjustmentBrightness = 1.0f,
        };
        AddChild(new WorldEnvironment { Environment = _env });
        _sun = new DirectionalLight3D
        {
            LightColor = new Color(1f, 0.94f, 0.84f), LightEnergy = 1.85f, ShadowEnabled = true,
            DirectionalShadowBlendSplits = true, DirectionalShadowFadeStart = 0.85f, DirectionalShadowPancakeSize = 10f,
            ShadowBias = 0.03f, ShadowNormalBias = 1.6f, ShadowOpacity = 1f,
        };
        _sun.RotationDegrees = SunAngles;
        AddChild(_sun);
        ApplyQuality();
    }

    /// Puts the current graphics tier into effect, on a running battle too: screen-space effects, anti-aliasing,
    /// the sun's shadow maps, the soil shader's detail and which small things cast shadows.
    public void ApplyQuality()
    {
        bool noSs = System.Environment.GetEnvironmentVariable("MANDIBLE_NOSS") != null; // tests on machines without the effects
        var q = Settings.Gfx;
        _env.SsaoEnabled = Settings.Ssao && !noSs;
        _env.SsilEnabled = Settings.Ssil && !noSs;
        _env.GlowEnabled = Settings.Glow;
        _env.VolumetricFogEnabled = Settings.Haze && !noSs;
        var vp = GetViewport();
        vp.Msaa3D = q switch { Settings.Quality.Ultra => Viewport.Msaa.Msaa4X, Settings.Quality.High => Viewport.Msaa.Msaa2X, _ => Viewport.Msaa.Disabled };
        vp.ScreenSpaceAA = q is Settings.Quality.Medium or Settings.Quality.High ? Viewport.ScreenSpaceAAEnum.Fxaa : Viewport.ScreenSpaceAAEnum.Disabled;
        // shadows: how far out the sun's maps reach, how many splits share them, and how soft the edge is
        switch (q)
        {
            case Settings.Quality.Low:
                _sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits; _sun.DirectionalShadowMaxDistance = 110;
                _sun.LightAngularDistance = 0f; _sun.ShadowBlur = 0.7f;
                RenderingServer.DirectionalShadowAtlasSetSize(2048, true);
                RenderingServer.DirectionalSoftShadowFilterSetQuality(RenderingServer.ShadowQuality.Hard);
                break;
            case Settings.Quality.Medium:
                _sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits; _sun.DirectionalShadowMaxDistance = 150;
                _sun.LightAngularDistance = 0.5f; _sun.ShadowBlur = 1.0f;
                RenderingServer.DirectionalShadowAtlasSetSize(4096, true);
                RenderingServer.DirectionalSoftShadowFilterSetQuality(RenderingServer.ShadowQuality.SoftLow);
                break;
            case Settings.Quality.High:
                _sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits; _sun.DirectionalShadowMaxDistance = 200;
                _sun.LightAngularDistance = 0.8f; _sun.ShadowBlur = 1.1f;
                RenderingServer.DirectionalShadowAtlasSetSize(4096, true);
                RenderingServer.DirectionalSoftShadowFilterSetQuality(RenderingServer.ShadowQuality.SoftMedium);
                break;
            default:
                _sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits; _sun.DirectionalShadowMaxDistance = 260;
                _sun.LightAngularDistance = 1.2f; _sun.ShadowBlur = 1.6f;
                RenderingServer.DirectionalShadowAtlasSetSize(4096, true);
                RenderingServer.DirectionalSoftShadowFilterSetQuality(RenderingServer.ShadowQuality.SoftHigh);
                break;
        }
        World?.ApplyQuality();
        Ants?.ApplyQuality();
    }

    // ------------------------------------------------------------- api for UI/input
    public void TryEnqueue(ProdKind k)
    {
        var r = Sim.CanEnqueue(Local.Me, k);
        if (r == QueueResult.Ok) Issue(new Cmd { Kind = CmdKind.Enqueue, Team = (byte)Local.Me, Arg = (byte)k });
        switch (r)
        {
            case QueueResult.Ok: Audio.Ui("ui_queue"); break;
            case QueueResult.NoFood: Hud.Alert("Not enough food", new Color(1, 0.5f, 0.35f)); Audio.Ui("ui_error"); break;
            case QueueResult.QueueFull: Hud.Alert("The brood chamber is full", new Color(1, 0.5f, 0.35f)); Audio.Ui("ui_error"); break;
            case QueueResult.MaxChambers: Hud.Alert("The nest cannot grow any deeper", new Color(1, 0.5f, 0.35f)); Audio.Ui("ui_error"); break;
            case QueueResult.HeroExists: Hud.Alert($"Only one {Config.ProdName(k)} may live at a time", new Color(1, 0.5f, 0.35f)); Audio.Ui("ui_error"); break;
            case QueueResult.Locked:
                Hud.Alert(LockReason(k), new Color(1, 0.5f, 0.35f));
                Audio.Ui("ui_error");
                break;
        }
    }

    static string LockReason(ProdKind k) => k switch
    {
        ProdKind.Major or ProdKind.Warlord => $"{Config.ProdName(k)} needs a Matriarch queen (Royal Jelly, Q)",
        ProdKind.Gunner or ProdKind.Matron => $"{Config.ProdName(k)} needs an Empress queen (Royal Jelly, Q)",
        ProdKind.Marshal => "The Sky Marshal needs an Ancient Empress and the Wing Chamber",
        _ => "Flying ants need the Wing Chamber (W)",
    };

    public void TryResearch(Upgrade u, Vector2? at = null)
    {
        var r = Sim.CanResearch(Local.Me, u);
        var c = Sim.Col[Local.Me];
        var bad = new Color(1, 0.5f, 0.35f);
        // a room that does not exist yet is placed by the player, Settlers-style, within reach of the tunnels
        if (r == ResearchResult.Ok && Sim.ChamberOf(Local.Me, Sim.KindOf(u)) == null && !at.HasValue) { Input.BeginDig(Sim.KindOf(u), u); return; }
        if (r == ResearchResult.Ok && at.HasValue)
        {
            var place = Sim.PlaceCheck(Local.Me, at.Value);
            if (place != Sim.DigResult.Ok) { Hud.Alert(Sim.DigMessage(place, Sim.KindOf(u)), bad); Audio.Ui("ui_error"); return; }
        }
        if (r == ResearchResult.Ok) Issue(new Cmd { Kind = CmdKind.Research, Team = (byte)Local.Me, Arg = (byte)u, P = at ?? Vector2.Zero });
        switch (r)
        {
            case ResearchResult.Ok: Audio.Ui("ui_queue"); Hud.Alert($"Research begun: {Config.ResearchName(u, c.T(u) + 1)}", UiTheme.Dim); break;
            case ResearchResult.NoFood: Hud.Alert("Not enough food", bad); Audio.Ui("ui_error"); break;
            case ResearchResult.Busy: Hud.Alert("The colony is already researching (right-click it to cancel)", bad); Audio.Ui("ui_error"); break;
            case ResearchResult.Maxed: Hud.Alert("Already fully developed", bad); Audio.Ui("ui_error"); break;
            case ResearchResult.Locked:
                Hud.Alert($"The queen must first become {Config.QueenTitles[Config.QueenNeeded(u, c.T(u) + 1)]} (Royal Jelly, Q)", bad);
                Audio.Ui("ui_error");
                break;
        }
    }

    readonly List<(int id, int serial, Node3D mound)> _mounds = new();

    /// Lane War scenery: worn dirt paths along the three lanes and an earth mound under every guardian.
    void BuildLaneDressing()
    {
        var hf = World.HF;
        var grad = new Gradient();
        grad.SetColor(0, new Color(1, 1, 1, 0));
        grad.AddPoint(0.35f, new Color(1, 1, 1, 1));
        grad.AddPoint(0.65f, new Color(1, 1, 1, 1));
        grad.SetColor(grad.GetPointCount() - 1, new Color(1, 1, 1, 0));
        var path = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.25f, 0.19f, 0.13f, 0.55f), AlbedoTexture = new GradientTexture2D { Gradient = grad, Width = 64, Height = 4 },
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 1, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new QuadMesh { Size = new Vector2(7f, 3.2f) } };
        var xforms = new List<Transform3D>();
        foreach (var lane in Sim.Lanes)
            for (int i = 0; i + 1 < lane.Length; i++)
            {
                var a = lane[i];
                var b = lane[i + 1];
                float len = a.DistanceTo(b);
                var dir = (b - a) / len;
                for (float d = 9; d < len - 1; d += 2.6f)
                {
                    var p = a + dir * d;
                    // lie flat on the ground, the strip's width across the lane
                    var basis = new Basis(Vector3.Up, -Mathf.Atan2(dir.Y, dir.X)) * new Basis(Vector3.Right, -Mathf.Pi / 2);
                    xforms.Add(new Transform3D(basis, new Vector3(p.X, hf.Sample(p.X, p.Y) + 0.06f, p.Y)));
                }
            }
        mm.InstanceCount = xforms.Count;
        for (int i = 0; i < xforms.Count; i++) mm.SetInstanceTransform(i, xforms[i]);
        AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = path, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Name = "LanePaths" });

        var soil = new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.27f, 0.18f), Roughness = 1 };
        for (int i = 0; i < Sim.High; i++)
        {
            ref var g = ref Sim.Ants[i];
            if (!g.Alive || !g.Guardian) continue;
            var mound = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 1.6f, BottomRadius = 4.2f, Height = 1.1f, RadialSegments = 24, Rings = 2 },
                MaterialOverride = soil, Position = new Vector3(g.Pos.X, hf.Sample(g.Pos.X, g.Pos.Y) + 0.1f, g.Pos.Y),
            };
            AddChild(mound);
            _mounds.Add((i, g.Serial, mound));
        }
    }

    void UpdateMounds()
    {
        for (int i = _mounds.Count - 1; i >= 0; i--)
        {
            var (id, serial, mound) = _mounds[i];
            if (Sim.Valid(id, serial)) continue;
            // the guardian fell: its mound slumps into the ground
            mound.CreateTween().TweenProperty(mound, "scale", new Vector3(1.2f, 0.05f, 1.2f), 1.5f);
            _mounds.RemoveAt(i);
        }
    }

    /// Lane War shop, with feedback when the purchase is not possible.
    public void TryBuy(int item)
    {
        int team = Local.Me;
        if (item < 3 && Sim.Items[team, item] >= Lane.MaxItemTier) { Hud.Alert($"{Lane.ItemNames[item]} is fully upgraded", UiTheme.Dim); Audio.Ui("ui_error"); return; }
        if (item == 3 && Sim.HeroId[team] < 0) { Hud.Alert("Your hero is respawning", UiTheme.Dim); Audio.Ui("ui_error"); return; }
        if (!Sim.CanBuy(team, item))
        {
            Hud.Alert(item == 3 && Sim.HeroId[team] >= 0 && Sim.Ants[Sim.HeroId[team]].Hp >= Sim.Ants[Sim.HeroId[team]].MaxHp ? "Your hero is already at full health" : "Not enough food", new Color(1, 0.5f, 0.35f));
            Audio.Ui("ui_error");
            return;
        }
        Issue(new Cmd { Kind = CmdKind.BuyItem, Team = (byte)team, Arg = (byte)item });
        Audio.Ui("research", -4);
    }

    /// Lane War: ask the hero to use its ability (it fires on the best target in reach).
    public void TryAbility()
    {
        int id = Sim.HeroId[Local.Me];
        if (id < 0) { Audio.Ui("ui_error"); return; }
        ref var h = ref Sim.Ants[id];
        if (h.AbilityCd > 0) { Hud.Alert($"{Config.AbilityName(h.Type)} ready in {h.AbilityCd:0}s", UiTheme.Dim); Audio.Ui("ui_error"); return; }
        Issue(new Cmd { Kind = CmdKind.Ability, Team = (byte)Local.Me });
        Audio.Ui("ui_click", -4);
    }

    /// V / L: choose where the new brood chamber or store goes (it must connect to the tunnels).
    public void TryDig(ChamberKind k)
    {
        var r = Sim.CanDig(Local.Me, k);
        if (r != Sim.DigResult.Ok) { Hud.Alert(Sim.DigMessage(r, k), new Color(1, 0.5f, 0.35f)); Audio.Ui("ui_error"); return; }
        Input.BeginDig(k, null);
    }

    /// The player clicked a spot for a room: dig it there if the tunnels can reach it.
    public bool TryDigAt(ChamberKind k, Vector2 at)
    {
        var bad = new Color(1, 0.5f, 0.35f);
        var r = Sim.CanDig(Local.Me, k);
        if (r == Sim.DigResult.Ok) r = Sim.PlaceCheck(Local.Me, at);
        if (r != Sim.DigResult.Ok) { Hud.Alert(Sim.DigMessage(r, k), bad); Audio.Ui("ui_error"); return false; }
        Issue(new Cmd { Kind = CmdKind.Dig, Team = (byte)Local.Me, Arg = (byte)k, P = at });
        Hud.Alert($"Workers are digging a new {Sim.ChamberName(k).ToLower()} and its gallery", UiTheme.Dim);
        Audio.Ui("ui_queue");
        Effects.Marker(World.HF.Point(at), new Color(1f, 0.85f, 0.4f));
        return true;
    }

    /// Glass Earth: the ground over your nest turns translucent so the chambers show; the surface stays in view.
    public bool NestView { get; private set; }
    float _glass; // 0..1 animated
    bool _glassWasOpen;

    public void ToggleNestView()
    {
        if (Demo) return;
        NestView = !NestView;
        Audio.Ui("ui_click", -4);
        var home = World.Nests[Local.Me].Pos;
        if (NestView && Camera.Focus2D.DistanceTo(home) > 30) { Camera.PanTo(home); Camera.TargetDistance = Mathf.Min(Camera.TargetDistance, 40); }
        if (NestView && !Sim.Col[Local.Me].Founded) Hud.Alert("Nothing below yet: the workers are still digging", UiTheme.Dim);
        else if (NestView) Hud.Alert("Glass Earth: looking into the nest (N to close)", UiTheme.Dim);
    }

    void UpdateGlass(float dt)
    {
        _glass = Mathf.MoveToward(_glass, NestView ? 1 : 0, dt * 3);
        var nest = World.Nests[Local.Me].Pos;
        // the earth opens only over the galleries and rooms, never over the plain ground around them; the
        // ground dressing (pebbles, straw, thatch) shares the cut, so nothing hovers over an opened gallery
        bool wasOpen = _glassWasOpen;
        _glassWasOpen = _glass > 0;
        if (_glass > 0 || wasOpen)
        {
            var (net, count) = Interior.Network(Local.Me);
            if (OS.GetEnvironment("MANDIBLE_NONET") != "") count = 0; // perf A/B: the cut's shader loop never runs
            foreach (var m in World.FowMaterials)
            {
                m.SetShaderParameter("xray_on", _glass);
                if (_glass > 0) { m.SetShaderParameter("xray_center", nest); m.SetShaderParameter("net", net); m.SetShaderParameter("net_count", count); }
            }
            Interior.SetNetwork(net, count, nest);
        }
        Interior.SetGlass(_glass);
        Camera.NestView = NestView && Camera.Focus2D.DistanceTo(nest) < 40;
    }

    public void Autopilot(bool on)
    {
        _ai.RemoveAll(a => a.Team == Local.Me);
        if (on) _ai.Add(Lanes ? new LaneAI(Sim, Local.Me, Difficulty.Normal) : new AIController(Sim, Local.Me, Difficulty.Normal) { Passive = true });
    }

    /// During a battle on a computer the mouse stays inside the game window, so pushing it against
    /// any edge scrolls the map (in a window it would otherwise slide off onto the desktop).
    void ConfineMouse()
    {
        if (Demo || Settings.Mobile) return;
        var want = _paused || _ended || !DisplayServer.WindowIsFocused() ? Godot.Input.MouseModeEnum.Visible : Godot.Input.MouseModeEnum.Confined;
        if (Godot.Input.MouseMode != want) Godot.Input.MouseMode = want;
    }

    public override void _ExitTree()
    {
        if (!Demo) Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible; // back to the menu: free the mouse
    }

    public int TrailToggles; // the tutorial watches these
    public void ToggleTrails() { TrailToggles++; _trailTarget = _trailTarget > 0.5f ? 0.18f : 1.4f; Audio.Ui("ui_click", -6); }
    public void SetPaused(bool p) { _paused = p && Net == null; }
    public void Restart()
    {
        if (Net != null) QuitToMenu();
        else if (Lanes) App.StartLanes(Diff, Local.Me, Heroes[Local.Me]);
        else App.StartBattle(Diff, Local.Me);
    }
    public void QuitToMenu() { Net?.Leave(); App.ShowMenu(); }

    // ------------------------------------------------------------- frame
    public override void _Process(double delta)
    {
        float dt = Mathf.Min((float)delta, 0.05f);
        StepSimulation((float)delta);
        Input?.Prune();
        Ants.ShowAllBars = !Demo && Godot.Input.IsKeyPressed(Key.Alt);
        Fog?.Update(Sim, dt);
        Interior.Update(Fog, NestView, _alpha);
        if (!Demo) UpdateGlass(dt);
        if (!Demo) Input?.UpdateHover();
        Ants.Update(Sim, Camera.Cam, _alpha);
        Effects.Update(_paused ? 0 : dt);
        UpdateFood();
        UpdatePheromoneTexture(dt);
        UpdateAudio(dt);
        if (Demo) DemoCamera(dt);
        if (Lanes) UpdateMounds();
        ConfineMouse();
        CheckEnd();
    }

    /// Fixed 30 Hz simulation: identical on every machine, with rendering interpolated between ticks.
    /// In multiplayer each 3-tick turn waits until both colonies' orders have arrived.
    void StepSimulation(float delta)
    {
        bool frozen = _paused && Net == null;
        if (frozen || _ended) { _alpha = 1; return; }
        _acc = Mathf.Min(_acc + Mathf.Min(delta, 0.25f) * (FastForward + 1), 0.5f * (FastForward + 1));
        int steps = 0, maxSteps = 6 * (FastForward + 1);
        bool waiting = false;
        while (_acc >= TickDt && steps < maxSteps)
        {
            if (Net != null && Sim.Ticks % Lockstep.TicksPerTurn == 0)
            {
                int turn = Sim.Ticks / Lockstep.TicksPerTurn;
                if (Net.State != Lockstep.Phase.InGame || !Net.Ready(turn)) { waiting = true; break; }
                Net.Execute(turn, Sim);
            }
            Sim.Tick(TickDt);
            foreach (var ai in _ai) ai.Tick(TickDt);
            HandleEvents();
            _acc -= TickDt;
            steps++;
        }
        if (waiting) { _stall += delta; _acc = Mathf.Min(_acc, TickDt); }
        else _stall = 0;
        _alpha = Mathf.Clamp(_acc / TickDt, 0, 1);
    }

    /// Seconds the game has been waiting on the network (0 when flowing).
    public float NetStall => _stall;

    public void Issue(Cmd c) => Orders.Issue(c);

    void UpdateFood()
    {
        foreach (var f in World.Food)
        {
            if (f.Node == null) continue;
            if (!f.Alive)
            {
                if (f.Node.Visible) { f.Node.Visible = false; Effects.Burst(World.HF.Point(f.Pos, 0.5f), 10, 2, 3, 0.5f, 0.8f); }
                continue;
            }
            f.Node.Scale = f.BaseScale * Sim.FoodScale(f);
        }
    }

    void UpdatePheromoneTexture(float dt)
    {
        _trailView = Mathf.MoveToward(_trailView, _trailTarget, dt * 3);
        World.TerrainMaterial.SetShaderParameter("pher_view", _trailView);
        _pherAcc += dt;
        if (_pherAcc < 0.25f) return;
        _pherAcc = 0;
        var g = Sim.Ph.Grid[Local.Me];
        for (int i = 0; i < g.Length; i++) _pherPx[i] = (byte)Mathf.Min(255, g[i] * 40);
        _pherImg.SetData(Sim.Ph.N, Sim.Ph.N, false, Image.Format.R8, _pherPx);
        _pherTex.Update(_pherImg);
    }

    bool Audible(Vector2 p) => new Vector2(Camera.Focus.X, Camera.Focus.Z).DistanceTo(p) < Camera.Distance * 1.6f + 20;

    void HandleEvents()
    {
        foreach (var e in Sim.Events)
        {
            var wp = World.HF.Point(e.Pos, 0.3f);
            bool seen = Demo || e.Team == Local.Me || Fog == null || Fog.Visible(e.Pos);
            switch (e.Kind)
            {
                case EvKind.Bite:
                    _combat += 0.04f;
                    if (seen && Audible(e.Pos))
                    {
                        bool bug = Config.IsBug(e.Type);
                        if (bug) Audio.At("bite", wp, 3, 0.05f, 0.08f, 0.6f);
                        else Audio.At("bite", wp, e.Type == AntType.Major ? 2 : -3, 0.035f);
                        Effects.Burst(wp, bug || e.Type == AntType.Major ? 3 : 1, 1, 2.2f, 0.12f, 0.4f);
                    }
                    break;
                case EvKind.Death:
                    if (seen)
                    {
                        bool bug = Config.IsBug(e.Type);
                        Audio.At("death", wp, bug ? 4 : e.Type == AntType.Worker ? -2 : 1, 0.05f, 0.08f, bug ? 0.55f : 1f);
                        Effects.Burst(wp, bug ? 22 : 8, 1, bug ? 5 : 3.5f, bug ? 0.3f : 0.16f, 0.7f);
                        Effects.Burst(wp, bug ? 10 : 4, 0, 1.5f, bug ? 1.4f : 0.6f, 1.2f);
                        if (bug && !Demo && Audible(e.Pos)) Hud.Alert($"{Config.Of(e.Type).Name} slain! Its carcass is a feast.", new Color(0.85f, 0.7f, 1f));
                    }
                    break;
                case EvKind.WebShot:
                    if (seen && Audible(e.Pos)) Audio.At("web", wp, 2, 0.1f);
                    break;
                case EvKind.WebLand:
                    if (Demo || Fog == null || Fog.Visible(e.Pos))
                    {
                        Effects.Burst(wp, 14, 3, 3f, 0.25f, 0.9f);
                        if (Audible(e.Pos)) Audio.At("web_land", wp, 0, 0.1f);
                    }
                    break;
                case EvKind.Sweep:
                    _combat += 0.15f;
                    if (seen)
                    {
                        Effects.Marker(wp, new Color(0.85f, 0.6f, 0.3f), e.Radius * 0.6f);
                        Effects.Burst(wp, 26, 0, 6f, 0.9f, 1.1f);
                        if (Audible(e.Pos)) Audio.At("sweep", wp, 4, 0.2f, 0.05f, e.Type == AntType.StagBeetle ? 0.8f : 1f);
                    }
                    break;
                case EvKind.Sting:
                    if (seen && Audible(e.Pos)) { Audio.At("sting", wp, 2, 0.15f); Effects.Burst(wp, 6, 1, 3f, 0.2f, 0.6f); Effects.Burst(wp + Vector3.Up * 0.3f, 10, 5, 2.2f, 0.22f, 0.9f); }
                    break;
                case EvKind.Charge:
                    // a stag lowering its antlers, or a spider springing: dust kicked up behind it
                    if (seen) { Effects.Burst(wp, e.Type == AntType.StagBeetle ? 18 : 8, 0, 4f, e.Type == AntType.StagBeetle ? 0.9f : 0.5f, 0.9f); if (Audible(e.Pos)) Audio.At("sweep", wp, -4, 0.15f, 0.05f, e.Type == AntType.StagBeetle ? 0.7f : 1.3f); }
                    break;
                case EvKind.Toss:
                    _combat += 0.2f;
                    if (seen)
                    {
                        Effects.Marker(wp, new Color(1f, 0.6f, 0.25f), e.Radius * 0.7f);
                        Effects.Burst(wp, 30, 0, 7f, 1.0f, 1.1f);
                        Effects.Burst(World.HF.Point(e.To, 0.6f), 14, 1, 4f, 0.25f, 0.8f);
                        if (Audible(e.Pos)) Audio.At("sweep", wp, 5, 0.2f, 0.05f, 0.75f);
                    }
                    break;
                case EvKind.Lunge:
                    if (seen) { Effects.Burst(wp, 12, 3, 3.5f, 0.28f, 0.8f); Effects.Burst(wp, 8, 1, 3f, 0.2f, 0.6f); if (Audible(e.Pos)) Audio.At("bite", wp, 5, 0.05f, 0.08f, 0.7f); }
                    break;
                case EvKind.ShellWall:
                    if (seen) { Effects.Marker(wp, new Color(0.35f, 0.95f, 0.8f), e.Radius * 1.6f); Effects.Burst(wp + Vector3.Up * 0.5f, 16, 6, 2.5f, 0.22f, 1.0f); if (Audible(e.Pos)) Audio.At("web_land", wp, 2, 0.1f, 0.05f, 0.6f); }
                    break;
                case EvKind.Rally:
                    if (seen)
                    {
                        Effects.Marker(wp, e.Team == 0 ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.4f, 0.2f), e.Radius * 0.5f);
                        Effects.Burst(wp + Vector3.Up * 1.2f, 24, e.Team == 0 ? 2 : 7, 5f, 0.28f, 1.3f);
                        if (Audible(e.Pos)) Audio.At("alert", wp, -3, 0.5f, 0.02f, 1.6f);
                        if (!Demo && e.Team == Local.Me) Hud.Alert("RALLY! The marshal's dive drives the army forward.", new Color(1f, 0.8f, 0.35f));
                    }
                    break;
                case EvKind.ResearchDone:
                {
                    if (!Demo && e.Team == Local.Me)
                    {
                        string what = e.Upgrade == Upgrade.Queen ? $"Our queen has become {Config.QueenTitles[e.Tier]}!" : $"{Config.ResearchName(e.Upgrade, e.Tier)} complete";
                        Hud.Alert(what, new Color(1f, 0.85f, 0.4f));
                        Audio.Ui("research", -2);
                        Effects.Burst(World.HF.Point(e.Pos, 1f), 24, 2, 4f, 0.3f, 1.4f);
                    }
                    break;
                }
                case EvKind.NestFounded:
                    if (!Demo && e.Team == Local.Me) { Hud.Alert("The nest is dug. The queen goes below to lay.", new Color(1f, 0.85f, 0.4f)); Audio.Ui("research", -2); }
                    Effects.Burst(World.HF.Point(e.Pos, 0.5f), 30, 2, 4f, 0.3f, 1.2f);
                    break;
                case EvKind.QueenInside:
                    if (!Demo && e.Team == Local.Me) { Hud.Alert("The queen is in her chamber. Brood can hatch now (Z for a worker).  Press N to look inside.", UiTheme.Gold); Audio.Ui("ui_ready", -4); }
                    break;
                case EvKind.ChamberDone:
                    if (!Demo && e.Team == Local.Me) { Hud.Alert($"{Sim.ChamberName(e.Chamber)} dug{(e.Chamber == ChamberKind.Brood ? ": brood capacity increased" : e.Chamber == ChamberKind.Storage ? ": the colony can store more food" : "")}", new Color(0.7f, 1f, 0.55f)); Audio.Ui("ui_ready", -6); }
                    break;
                case EvKind.StoreFull:
                    if (!Demo && e.Team == Local.Me) { Hud.Alert("The stores are full: food is going to waste. Dig a store (L) or spend it.", new Color(1, 0.55f, 0.35f)); Audio.Ui("ui_error"); }
                    break;
                case EvKind.Spit:
                {
                    var from = World.HF.Point(e.Pos, 0.8f);
                    var to = World.HF.Point(e.To, 0.5f);
                    if (seen)
                    {
                        for (int s = 0; s < 4; s++) Effects.Burst(from.Lerp(to, 0.25f * s + 0.2f), 2, e.Type == AntType.Matron ? 4 : 1, 0.8f, e.Type == AntType.Matron ? 0.3f : 0.18f, 0.35f);
                        if (e.Type == AntType.Matron) Effects.Burst(to, 5, 4, 2.5f, 0.2f, 0.5f); // the splash
                        if (Audible(e.Pos)) Audio.At("web", from, -6, 0.06f, 0.1f, 1.7f);
                    }
                    break;
                }
                case EvKind.WarCry:
                    if (seen)
                    {
                        Effects.Marker(wp, e.Team == 0 ? new Color(1f, 0.75f, 0.25f) : new Color(1f, 0.4f, 0.2f), e.Radius * 0.55f);
                        Effects.Marker(wp, new Color(1f, 0.9f, 0.7f), Config.WarCryShockR * 0.6f); // the shock itself
                        Effects.Burst(wp + Vector3.Up, 30, e.Team == 0 ? 2 : 7, 6f, 0.3f, 1.2f);
                        Effects.Burst(wp, 22, 0, 6f, 0.9f, 1.0f);
                        if (Audible(e.Pos)) Audio.At("alert", wp, 0, 0.5f, 0.02f, 1.35f);
                        if (!Demo && e.Team == Local.Me) Hud.Alert("WAR CRY! Our warriors fight with fury.", new Color(1f, 0.8f, 0.35f));
                    }
                    break;
                case EvKind.AcidBurst:
                {
                    var to = World.HF.Point(e.To, 0.5f);
                    if (Demo || Fog == null || Fog.Visible(e.To))
                    {
                        Effects.Marker(to, new Color(0.5f, 1f, 0.2f), e.Radius * 0.6f);
                        Effects.Burst(to, 40, 4, 5f, 0.35f, 1.3f);
                        Effects.Burst(to + Vector3.Up * 0.5f, 16, 5, 3f, 0.3f, 1.6f);
                        if (Audible(e.To)) Audio.At("web_land", to, 4, 0.15f, 0.05f, 0.7f);
                    }
                    break;
                }
                case EvKind.DiveBomb:
                    if (seen)
                    {
                        Effects.Marker(wp, new Color(1f, 0.9f, 0.6f), e.Radius * 0.65f);
                        Effects.Burst(wp, 36, 0, 7f, 1.0f, 1.2f);
                        Effects.Burst(wp + Vector3.Up * 0.5f, 14, e.Team == 0 ? 2 : 7, 4f, 0.25f, 0.9f);
                        if (Audible(e.Pos)) Audio.At("sweep", wp, 5, 0.2f, 0.05f, 1.15f);
                    }
                    break;
                case EvKind.Pickup:
                    if (seen && Audible(e.Pos)) Audio.At("pickup", wp, -10, 0.12f);
                    break;
                case EvKind.Deliver:
                    if (Audible(e.Pos)) Audio.At("deliver", wp, -14, 0.25f);
                    break;
                case EvKind.Spawn:
                    if (seen) { Effects.Burst(wp, 8, 0, 2f, 0.8f, 1f); if (Audible(e.Pos)) Audio.At("spawn", wp, -8, 0.2f); }
                    break;
                case EvKind.ProdDone:
                    if (!Demo && e.Team == Local.Me && e.Prod != ProdKind.Worker)
                    {
                        Audio.Ui("ui_ready", -4);
                    }
                    break;
                case EvKind.LevelUp:
                    if (seen)
                    {
                        Effects.Burst(wp + Vector3.Up * 0.8f, 16, 2, 4.5f, 0.28f, 1.1f);
                        if (Audible(e.Pos)) Audio.At("levelup", wp, e.Team == Local.Me ? 0 : -6, 0.15f, 0.04f);
                    }
                    if (!Demo && e.Team == Local.Me && _levelAlertCd <= 0)
                    {
                        Hud.Alert($"A {Config.Of(e.Type).Name.ToLower()} has been promoted!", new Color(1f, 0.85f, 0.4f));
                        _levelAlertCd = 6;
                    }
                    break;
                case EvKind.SquadReady:
                    if (!Demo && e.Team == Local.Me) { Hud.Alert($"A {Config.Of(e.Type).Name.ToLower()} squad is back at full strength", new Color(0.7f, 1f, 0.55f)); Audio.Ui("ui_ready", -6); }
                    break;
                case EvKind.Shower:
                    Effects.Burst(wp + Vector3.Up * 6, 18, 2, 5, 0.4f, 1.6f);
                    if (!Demo && e.Pos.DistanceTo(World.Nests[Local.Me].Pos) < e.Pos.DistanceTo(World.Nests[Local.Foe].Pos))
                    {
                        Hud.Alert("Crumbs are falling from the picnic table!  (Space to look)", new Color(0.8f, 1f, 0.45f));
                        Audio.Ui("ui_ready", -6);
                        Input.LastAlert = e.Pos;
                    }
                    break;
                case EvKind.PopBlocked:
                    if (!Demo && e.Team == Local.Me && _popAlertCd <= 0)
                    {
                        Hud.Alert("Brood capacity reached: have workers dig a brood chamber (V)", new Color(1, 0.55f, 0.35f));
                        Audio.Ui("ui_error");
                        _popAlertCd = 8;
                    }
                    break;
                case EvKind.QueenHit:
                    if (!Demo && e.Team == Local.Me && _queenAlertCd <= 0)
                    {
                        Hud.Alert("Our queen is under attack!", new Color(1, 0.35f, 0.25f));
                        Audio.Ui("alert", -2);
                        Input.LastAlert = e.Pos;
                        _queenAlertCd = 12;
                    }
                    break;
                case EvKind.UnderAttack:
                    if (!Demo && e.Team == Local.Me && _attackAlertCd <= 0 && !Audible(e.Pos))
                    {
                        Hud.Alert("Our ants are under attack  (Space to look)", new Color(1, 0.55f, 0.35f));
                        Audio.Ui("alert", -9);
                        Input.LastAlert = e.Pos;
                        _attackAlertCd = 25;
                    }
                    break;
            }
        }
        _queenAlertCd -= 1f / 60; _attackAlertCd -= 1f / 60; _popAlertCd -= 1f / 60; _levelAlertCd -= 1f / 60;
    }

    void UpdateAudio(float dt)
    {
        _combat = Mathf.Clamp(_combat - dt * 0.18f, 0, 1.4f);
        Audio.SetIntensity(Demo ? _combat * 0.5f : Mathf.Clamp(_combat, 0, 1));
        // swarm loudness: ants near the camera focus, louder when zoomed in
        var f = new Vector2(Camera.Focus.X, Camera.Focus.Z);
        float r = 12 + Camera.Distance * 0.35f;
        int n = 0;
        for (int i = 0; i < Sim.High; i++)
            if (Sim.Ants[i].Alive && Sim.Ants[i].Speed > 0.5f && Sim.Ants[i].Pos.DistanceSquaredTo(f) < r * r) n++;
        float zoom = Mathf.Clamp(1.2f - Camera.Distance / RtsCamera.MaxDist, 0.2f, 1f);
        Audio.SetSwarm(Mathf.Clamp(n / 60f, 0, 1) * zoom);
    }

    void DemoCamera(float dt)
    {
        _demoT += dt;
        float a = _demoT * 0.03f;
        var center = new Vector2(Config.MapSize, Config.MapSize) * 0.5f;
        // drift between the two nests and the centre
        var p = center + new Vector2(Mathf.Cos(a * 0.7f), Mathf.Sin(a * 0.7f)) * 70;
        Camera.PanTo(p);
        Camera.SetYaw(-a, false);
        Camera.TargetDistance = 55 + Mathf.Sin(_demoT * 0.05f) * 15;
    }

    void CheckEnd()
    {
        if (_netEnded && !_ended && !Demo)
        {
            _ended = true;
            Audio.FadeMusic(0.15f);
            Hud.Alert($"{Net.PeerName} left the game.", new Color(1f, 0.85f, 0.4f));
            Hud.ShowEnd(true, Sim.Col[Local.Me], Sim.Col[Local.Foe], Sim.Time);
            return;
        }
        if (_ended || Demo || Sim.Winner < 0) return;
        _ended = true;
        bool win = Sim.Winner == Local.Me;
        Audio.FadeMusic(0.15f);
        Audio.Ui(win ? "victory" : "defeat");
        Camera.PanTo(World.Nests[win ? Local.Foe : Local.Me].Pos);
        Hud.ShowEnd(win, Sim.Col[Local.Me], Sim.Col[Local.Foe], Sim.Time);
    }
}
