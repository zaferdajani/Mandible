using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The lamps of the dug nest: a warm light in every chamber and one every ~10 units along the galleries, like
/// mining lamps. The sun and the sky ambient are kept out of the nest (see NestInterior.UndergroundLayer and the
/// ambient handling in Shaders.Earth), so these are what you see by. Every lamp keeps its own OmniLight3D; only
/// the few nearest the camera are switched on, and only the nearest of those cast shadows (none on Low or on
/// phones), so ants throw moving shadows without the cost of cube shadow maps everywhere. Energies fade in and
/// out, so nothing pops when the camera moves or the earth opens. Rendering only.
sealed class NestLights
{
    static readonly Color Warm = new(1.0f, 0.72f, 0.45f);
    const int MaxLamps = 24;
    const float TunnelSpacing = 10f;

    sealed class Lamp
    {
        public OmniLight3D Light;
        public Vector3 Pos;
        public float Energy, Range, Cur;
        public bool On, Shadow;
        public float Dist;
    }

    readonly GameSession _g;
    readonly Node3D _root = new() { Name = "NestLamps" };
    readonly Dictionary<int, Lamp> _lamps = new();
    readonly List<Lamp> _order = new();
    readonly System.Action<Vector4[]> _pools;
    readonly Vector4[] _poolData = new Vector4[8];
    float _timer;
    string _signature = "";

    /// `pools` receives the nearest lamps as (x, y, z, range) once a frame on Low, where the cavity walls are
    /// painted by the shader instead of lit by real lights.
    public NestLights(GameSession g, Node parent, System.Action<Vector4[]> pools)
    {
        _g = g;
        _pools = pools;
        parent.AddChild(_root);
    }

    /// Lamps lit at once, and how many of those cast shadows, by graphics tier.
    static int Budget => Settings.Gfx switch { Settings.Quality.Low => 6, Settings.Quality.Medium => 8, Settings.Quality.High => 12, _ => 16 };
    // cube shadow maps are dear: on High five of them halved the chamber frame rate (42 -> 21 fps on a GTX 1650)
    static int ShadowBudget => Settings.Mobile ? 0 : Settings.Gfx switch { Settings.Quality.Low => 0, Settings.Quality.Medium => 1, Settings.Quality.High => 2, _ => 4 };

    /// `open` is the Glass Earth fade (0 = ground closed, 1 = fully open).
    public void Update(float dt, float open)
    {
        _timer -= dt;
        if (_timer <= 0) { _timer = 0.25f; Refresh(); Select(); }
        bool low = Settings.Gfx == Settings.Quality.Low;
        int p = 0;
        if (low) System.Array.Clear(_poolData);
        foreach (var l in _order)
        {
            float target = l.On ? l.Energy * open : 0f;
            l.Cur = Mathf.MoveToward(l.Cur, target, dt * l.Energy * 3f);
            l.Light.LightEnergy = l.Cur;
            l.Light.Visible = l.Cur > 0.01f;
            if (low && l.On && p < _poolData.Length && l.Cur > 0.01f) _poolData[p++] = new Vector4(l.Pos.X, l.Pos.Y, l.Pos.Z, l.Range * l.Cur / l.Energy);
        }
        if (low) _pools(_poolData);
    }

    /// Lamps for the rooms and galleries dug so far; rebuilt only when the layout changes.
    void Refresh()
    {
        var world = _g.World;
        var nest = world.Nests[Local.Me].Pos;
        float ground = world.HF.Sample(nest.X, nest.Y);
        var chambers = _g.Sim.Col[Local.Me].Chambers;
        var sig = new System.Text.StringBuilder();
        foreach (var ch in chambers) sig.Append(ch.Id).Append(',').Append(ch.Parent).Append(',').Append(ch.Built ? 1 : ch.Progress > 0.4f ? 2 : 0).Append(';');
        sig.Append(ground.ToString("0.00"));
        if (sig.ToString() == _signature) return;
        _signature = sig.ToString();

        var want = new Dictionary<int, (Vector3 pos, float energy, float range)>();
        float y = ground - NestGeometry.Depth + 0.95f;
        foreach (var ch in chambers)
        {
            bool built = ch.Built;
            if (!built && ch.Progress <= 0.4f) continue;
            var pos = nest + ch.Offset;
            want[ch.Id * 8] = (new Vector3(pos.X, y, pos.Y), 3.2f, 6f);
            if (!built) continue;
            // gallery lamps between the parent and this room, kept clear of both ends and of the entrance shaft
            var from = ch.Parent >= 0 && ch.Parent < chambers.Count ? chambers[ch.Parent].Offset : Vector2.Zero;
            float len = from.DistanceTo(ch.Offset);
            int n = Mathf.Min((int)(len / TunnelSpacing), 6);
            for (int i = 1; i <= n; i++)
            {
                var q = nest + from.Lerp(ch.Offset, i / (float)(n + 1));
                if (q.DistanceTo(nest) < 3f) continue;
                want[ch.Id * 8 + i] = (new Vector3(q.X, y + 0.1f, q.Y), 2.2f, 4.5f);
            }
        }
        foreach (var key in new List<int>(_lamps.Keys))
            if (!want.ContainsKey(key)) { _lamps[key].Light.QueueFree(); _order.Remove(_lamps[key]); _lamps.Remove(key); }
        foreach (var (key, w) in want)
        {
            if (_lamps.Count >= MaxLamps && !_lamps.ContainsKey(key)) continue;
            if (!_lamps.TryGetValue(key, out var lamp))
            {
                lamp = new Lamp
                {
                    Light = new OmniLight3D
                    {
                        LightColor = Warm, OmniAttenuation = 1.7f, LightEnergy = 0f, Visible = false, ShadowBias = 0.04f,
                        LightSpecular = 0.5f, OmniShadowMode = OmniLight3D.ShadowMode.Cube,
                    },
                };
                _root.AddChild(lamp.Light);
                _lamps[key] = lamp;
                _order.Add(lamp);
            }
            lamp.Pos = w.pos; lamp.Energy = w.energy; lamp.Range = w.range;
            lamp.Light.GlobalPosition = w.pos;
            lamp.Light.OmniRange = w.range;
        }
    }

    /// The lamps nearest the camera focus are on; the nearest few of those cast shadows.
    void Select()
    {
        var f = _g.Camera.Focus;
        foreach (var l in _order) l.Dist = new Vector2(l.Pos.X - f.X, l.Pos.Z - f.Z).LengthSquared();
        _order.Sort((a, b) => a.Dist.CompareTo(b.Dist));
        int on = Budget, shadows = ShadowBudget;
        for (int i = 0; i < _order.Count; i++)
        {
            var l = _order[i];
            l.On = i < on;
            bool s = i < shadows;
            if (s != l.Shadow) { l.Shadow = s; l.Light.ShadowEnabled = s; }
        }
    }
}
