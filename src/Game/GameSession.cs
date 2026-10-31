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
    readonly List<AIController> _ai = new();
    Image _pherImg;
    ImageTexture _pherTex;
    byte[] _pherPx;
    float _pherAcc, _trailView = 0.18f, _trailTarget = 0.18f;
    bool _paused, _ended;
    float _combat, _queenAlertCd, _attackAlertCd, _popAlertCd;
    float _demoT;
    DirectionalLight3D _sun;

    public override void _Ready()
    {
        World = WorldBuilder.Build(this, 20261031);
        Sim = new Sim(World);
        Ants = new AntRenderer(this);
        Effects = new Effects(this, World.HF);
        BuildLighting();

        Camera = new RtsCamera { HF = World.HF, UserControl = !Demo, EdgeScroll = !Demo };
        AddChild(Camera);
        var home = World.Nests[Config.Player].Pos;
        Camera.JumpTo(home + new Vector2(10, -12), 62);
        Camera.SetYaw(Mathf.Pi * 0.25f + Mathf.Pi, true);

        _pherPx = new byte[Sim.Ph.N * Sim.Ph.N];
        _pherImg = Image.CreateFromData(Sim.Ph.N, Sim.Ph.N, false, Image.Format.R8, _pherPx);
        _pherTex = ImageTexture.CreateFromImage(_pherImg);
        World.TerrainMaterial.SetShaderParameter("pher_tex", _pherTex);

        if (Demo)
        {
            _ai.Add(new AIController(Sim, Config.Player, Difficulty.Normal));
            _ai.Add(new AIController(Sim, Config.Enemy, Difficulty.Normal));
            Ants.VisibleToPlayer = _ => true;
            _trailTarget = _trailView = 0.12f;
        }
        else
        {
            Fog = new FogOfWar(World);
            Ants.VisibleToPlayer = Fog.Visible;
            _ai.Add(new AIController(Sim, Config.Enemy, Diff));
            Input = new PlayerInput { Game = this };
            AddChild(Input);
            Hud = new Hud { Game = this };
            AddChild(Hud);
            Hud.Alert("Defend the queen. Destroy the red colony.");
        }
    }

    void BuildLighting()
    {
        var env = new Environment
        {
            BackgroundMode = Environment.BGMode.Sky,
            Sky = new Sky
            {
                SkyMaterial = new ProceduralSkyMaterial
                {
                    SkyTopColor = new Color(0.33f, 0.52f, 0.78f), SkyHorizonColor = new Color(0.78f, 0.80f, 0.78f),
                    GroundHorizonColor = new Color(0.55f, 0.48f, 0.38f), GroundBottomColor = new Color(0.2f, 0.16f, 0.12f),
                    SunAngleMax = 30, SunCurve = 0.12f,
                },
            },
            AmbientLightSource = Environment.AmbientSource.Sky,
            AmbientLightSkyContribution = 0.8f,
            AmbientLightEnergy = 0.5f,
            ReflectedLightSource = Environment.ReflectionSource.Sky,
            TonemapMode = Environment.ToneMapper.Aces,
            TonemapExposure = 1.1f,
            TonemapWhite = 6f,
            SsaoEnabled = true, SsaoRadius = 1.4f, SsaoIntensity = 2.6f, SsaoPower = 1.6f, SsaoDetail = 0.8f,
            SsilEnabled = true, SsilRadius = 6f, SsilIntensity = 0.5f,
            GlowEnabled = true, GlowIntensity = 0.45f, GlowBloom = 0.04f, GlowHdrThreshold = 1.1f,
            FogEnabled = true, FogLightColor = new Color(0.74f, 0.76f, 0.72f), FogDensity = 0.0012f, FogAerialPerspective = 0.25f, FogSkyAffect = 0.3f,
            AdjustmentEnabled = true, AdjustmentSaturation = 1.12f, AdjustmentContrast = 1.06f, AdjustmentBrightness = 1.0f,
        };
        AddChild(new WorldEnvironment { Environment = env });
        _sun = new DirectionalLight3D
        {
            LightColor = new Color(1f, 0.92f, 0.8f), LightEnergy = 1.5f, ShadowEnabled = true,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits, DirectionalShadowMaxDistance = 240,
            LightAngularDistance = 1.0f, ShadowBias = 0.04f, ShadowNormalBias = 1.2f, ShadowBlur = 1.2f,
        };
        _sun.RotationDegrees = new Vector3(-40, 140, 0);
        AddChild(_sun);
    }

    // ------------------------------------------------------------- api for UI/input
    public void TryEnqueue(ProdKind k)
    {
        var r = Sim.Enqueue(Config.Player, k);
        switch (r)
        {
            case QueueResult.Ok: Audio.Ui("ui_queue"); break;
            case QueueResult.NoFood: Hud.Alert("Not enough food", new Color(1, 0.5f, 0.35f)); Audio.Ui("ui_error"); break;
            case QueueResult.QueueFull: Hud.Alert("The brood chamber is full", new Color(1, 0.5f, 0.35f)); Audio.Ui("ui_error"); break;
            case QueueResult.MaxChambers: Hud.Alert("The nest cannot grow any deeper", new Color(1, 0.5f, 0.35f)); Audio.Ui("ui_error"); break;
        }
    }

    public void Autopilot(bool on)
    {
        _ai.RemoveAll(a => a.Team == Config.Player);
        if (on) _ai.Add(new AIController(Sim, Config.Player, Difficulty.Normal) { Passive = true });
    }

    public void ToggleTrails() { _trailTarget = _trailTarget > 0.5f ? 0.18f : 1.4f; Audio.Ui("ui_click", -6); }
    public void SetPaused(bool p) { _paused = p; }
    public void Restart() => App.StartBattle(Diff);
    public void QuitToMenu() => App.ShowMenu();

    // ------------------------------------------------------------- frame
    public override void _Process(double delta)
    {
        float dt = Mathf.Min((float)delta, 0.05f);
        if (!_paused)
        {
            for (int k = 0; k <= FastForward; k++)
            {
                Sim.Tick(dt);
                foreach (var ai in _ai) ai.Tick(dt);
                if (k == FastForward) HandleEvents();
            }
        }
        Input?.Prune();
        Ants.ShowAllBars = !Demo && Godot.Input.IsKeyPressed(Key.Alt);
        Fog?.Update(Sim, dt);
        Ants.Update(Sim);
        Effects.Update(_paused ? 0 : dt);
        UpdateFood();
        UpdatePheromoneTexture(dt);
        UpdateAudio(dt);
        if (Demo) DemoCamera(dt);
        CheckEnd();
    }

    void UpdateFood()
    {
        foreach (var f in World.Food)
        {
            if (f.Node == null) continue;
            if (!f.Alive)
            {
                if (f.Node.Visible) { f.Node.Visible = false; Effects.Burst(World.HF.Point(f.Pos, 0.5f), 10, 2, 3, 0.5f, 0.8f); }
                if (f.Block != null) f.Block.R = 0;
                continue;
            }
            float s = Mathf.Lerp(0.35f, 1f, Mathf.Pow(f.Amount / f.Max, 0.5f));
            f.Node.Scale = f.BaseScale * s;
            if (f.Block != null) f.Block.R = f.Block.BaseR * s;
        }
    }

    void UpdatePheromoneTexture(float dt)
    {
        _trailView = Mathf.MoveToward(_trailView, _trailTarget, dt * 3);
        World.TerrainMaterial.SetShaderParameter("pher_view", _trailView);
        _pherAcc += dt;
        if (_pherAcc < 0.25f) return;
        _pherAcc = 0;
        var g = Sim.Ph.Grid[Config.Player];
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
            bool seen = Demo || e.Team == Config.Player || Fog == null || Fog.Visible(e.Pos);
            switch (e.Kind)
            {
                case EvKind.Bite:
                    _combat += 0.04f;
                    if (seen && Audible(e.Pos))
                    {
                        Audio.At("bite", wp, e.Type == AntType.Major ? 2 : -3, 0.035f);
                        Effects.Burst(wp, e.Type == AntType.Major ? 3 : 1, 1, 2.2f, 0.12f, 0.4f);
                    }
                    break;
                case EvKind.Death:
                    if (seen)
                    {
                        Audio.At("death", wp, e.Type == AntType.Worker ? -2 : 1, 0.05f);
                        Effects.Burst(wp, 8, 1, 3.5f, 0.16f, 0.6f);
                        Effects.Burst(wp, 4, 0, 1.2f, 0.6f, 1.0f);
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
                    if (!Demo && e.Team == Config.Player && e.Prod != ProdKind.Worker)
                    {
                        Audio.Ui("ui_ready", -4);
                        if (e.Prod == ProdKind.Chamber) Hud.Alert("A new chamber is dug: brood capacity increased");
                    }
                    break;
                case EvKind.Shower:
                    Effects.Burst(wp + Vector3.Up * 6, 18, 2, 5, 0.4f, 1.6f);
                    if (!Demo && e.Pos.DistanceTo(World.Nests[Config.Player].Pos) < e.Pos.DistanceTo(World.Nests[Config.Enemy].Pos))
                    {
                        Hud.Alert("Crumbs are falling from the picnic table!  (Space to look)", new Color(0.8f, 1f, 0.45f));
                        Audio.Ui("ui_ready", -6);
                        Input.LastAlert = e.Pos;
                    }
                    break;
                case EvKind.PopBlocked:
                    if (!Demo && e.Team == Config.Player && _popAlertCd <= 0)
                    {
                        Hud.Alert("Brood capacity reached: excavate a chamber (V)", new Color(1, 0.55f, 0.35f));
                        Audio.Ui("ui_error");
                        _popAlertCd = 8;
                    }
                    break;
                case EvKind.QueenHit:
                    if (!Demo && e.Team == Config.Player && _queenAlertCd <= 0)
                    {
                        Hud.Alert("Our queen is under attack!", new Color(1, 0.35f, 0.25f));
                        Audio.Ui("alert", -2);
                        Input.LastAlert = e.Pos;
                        _queenAlertCd = 12;
                    }
                    break;
                case EvKind.UnderAttack:
                    if (!Demo && e.Team == Config.Player && _attackAlertCd <= 0 && !Audible(e.Pos))
                    {
                        Hud.Alert("Our ants are under attack  (Space to look)", new Color(1, 0.55f, 0.35f));
                        Audio.Ui("alert", -9);
                        Input.LastAlert = e.Pos;
                        _attackAlertCd = 25;
                    }
                    break;
            }
        }
        _queenAlertCd -= 1f / 60; _attackAlertCd -= 1f / 60; _popAlertCd -= 1f / 60;
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
        if (_ended || Demo || Sim.Winner < 0) return;
        _ended = true;
        bool win = Sim.Winner == Config.Player;
        Audio.FadeMusic(0.15f);
        Audio.Ui(win ? "victory" : "defeat");
        Camera.PanTo(World.Nests[win ? Config.Enemy : Config.Player].Pos);
        Hud.ShowEnd(win, Sim.Col[Config.Player], Sim.Col[Config.Enemy], Sim.Time);
    }
}
