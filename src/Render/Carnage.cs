using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Combat you can feel, the way a nature documentary films it: hemolymph (amber in ants, yellow-green in beetles,
/// cloudy in spiders; never red) sprays from a hard bite and soaks into the soil, legs and antennae come off,
/// the wounded limp and leak, acid pits the chitin, and the dead lie in pieces that workers carry home.
/// Rendering only: it reads the sim's events and state and never writes them.
public sealed class Carnage
{
    // part masks, as the Ant shader decodes them (instance custom z / 1024): a set bit hides that part
    public const int Legs = 63, Antennae = 192, HeadBit = 256, ThoraxBit = 512, GasterBit = 1024, All = 2047;
    static int Leg(int k) => 1 << k;

    struct Drop { public Vector3 Pos, Vel; public float Size, Life, Max, Floor, Grav; public int Kind, StainKind; public bool Stains; }
    struct Blot { public Vector3 Pos; public float R, Born, Life, Seed; public int Kind; }
    struct Bit { public Vector3 Pos; public float Heading, Born, Size; public int Team, Mask; public AntType Type; public bool Flip; }
    sealed class Piece { public int Mask; public Vector3 Off; public float Yaw, Raise; public bool Flip; }
    sealed class Look { public readonly List<Piece> Pieces = new(); public float Born; public Vector2 Shown; public bool Placed, Whole = true; }

    readonly Batch _drops, _stains;
    readonly List<Drop> _dropList = new();
    readonly Blot[] _blots = new Blot[700];
    int _blotNext;
    readonly List<Bit> _bits = new();
    readonly Dictionary<int, Look> _looks = new();
    readonly int[] _lost = new int[Config.MaxAnts], _serial = new int[Config.MaxAnts];
    readonly float[] _etch = new float[Config.MaxAnts], _etchT = new float[Config.MaxAnts], _leak = new float[Config.MaxAnts];
    readonly List<int> _wounded = new();
    readonly Rng _rng = new(777);
    readonly Heightfield _hf;
    readonly Effects _fx;
    float _now, _last = -1;

    public Carnage(Node3D parent, Heightfield hf, Effects fx)
    {
        _hf = hf;
        _fx = fx;
        var root = new Node3D { Name = "Carnage" };
        parent.AddChild(root);
        // the soil is drawn in the transparent pass (its glass cut writes alpha), so the stains must come after it
        _stains = new Batch(root, new PlaneMesh { Size = Vector2.One }, new ShaderMaterial { Shader = Shaders.Get(Shaders.Stain), RenderPriority = 1 }, false, "Stains");
        _stains.Node.SortingOffset = 1;
        _drops = new Batch(root, new QuadMesh { Size = Vector2.One }, new ShaderMaterial { Shader = Shaders.Get(Shaders.Particle) }, false, "Hemolymph");
    }

    /// Parts this living ant has lost (0 for a slot whose ant changed).
    public int Lost(int id, int serial) => _serial[id] == serial ? _lost[id] : 0;

    /// How deeply acid has pitted this ant's chitin, 0..7 (the scars fade over half a minute).
    public int Etch(int id, int serial) => _serial[id] == serial ? (int)Mathf.Clamp(_etch[id] - (_now - _etchT[id]) * 0.25f, 0, 7) : 0;

    static bool Bug(AntType t) => Config.IsBug(t);

    // what the gore test reads back
    internal int DropCount => _dropList.Count;
    internal int StainCount { get { int n = 0; foreach (var b in _blots) if (b.Life > 0 && _now - b.Born < b.Life) n++; return n; } }
    internal int LooseParts => _bits.Count;
    internal int BrokenCarcasses { get { int n = 0; foreach (var l in _looks.Values) if (!l.Whole) n++; return n; } }
    internal int Wounded => _wounded.Count;
    internal Vector2? BrokenAt(Sim sim)
    {
        foreach (var f in sim.W.Food)
            if (f.Kind == FoodKind.Corpse && f.Alive && f.Layer == 0 && _looks.TryGetValue(f.Id, out var l) && !l.Whole) return f.Pos;
        return null;
    }

    /// Particle kind for the droplets (Particle shader) and the stain kind they leave (Stain shader).
    static (int drop, int stain) Fluid(AntType t) => t switch
    {
        AntType.Beetle or AntType.StagBeetle => (1, 1), AntType.Spider => (9, 2), _ => (8, 0),
    };
    static bool Heavy(AntType by) => by is AntType.Major or AntType.Warlord or AntType.StagBeetle or AntType.Beetle or AntType.Spider or AntType.Wasp or AntType.Queen;

    float Floor(Sim sim, int layer, Vector2 p) => layer == 0 ? _hf.Sample(p.X, p.Y) : _hf.Sample(sim.W.Nests[layer - 1].Pos.X, sim.W.Nests[layer - 1].Pos.Y) - NestGeometry.Depth;

    int Budget(int n) { float f = n * Settings.GoreBudget; int k = (int)f; return k + (_rng.F() < f - k ? 1 : 0); }

    void Spray(Vector3 at, Vector2 away, int n, float speed, float size, AntType type, float floor, float stainChance)
    {
        var (dk, sk) = Fluid(type);
        int cap = (int)(900 * Settings.GoreBudget) + 40;
        for (int k = 0; k < n && _dropList.Count < cap; k++)
        {
            var side = new Vector2(-away.Y, away.X) * _rng.R(-0.7f, 0.7f);
            var h = (away * _rng.R(0.5f, 1f) + side) * speed * _rng.R(0.5f, 1.1f);
            _dropList.Add(new Drop
            {
                Pos = at, Vel = new Vector3(h.X, speed * _rng.R(0.5f, 1.1f), h.Y), Size = size * _rng.R(0.6f, 1.3f), Max = 2.5f, Floor = floor,
                Grav = 11f, Kind = dk, StainKind = sk, Stains = _rng.F() < stainChance,
            });
        }
    }

    void Stain(Vector3 at, float r, int kind, float life = 60f)
    {
        int cap = Math.Max(60, (int)(_blots.Length * Settings.GoreBudget));
        if (_blotNext >= cap) _blotNext = 0;
        _blots[_blotNext++] = new Blot { Pos = at, R = r, Born = _now, Life = life, Seed = _rng.F(), Kind = kind };
    }

    public void Event(in SimEvent e, Sim sim, bool seen)
    {
        switch (e.Kind)
        {
            case EvKind.Hurt: if (seen) Hurt(e, sim); break;
            case EvKind.Death: Death(e, sim, seen); break;
            case EvKind.Feast:
                if (seen)
                {
                    var p = new Vector3(e.Pos.X, _hf.Sample(e.Pos.X, e.Pos.Y) + 0.25f, e.Pos.Y);
                    Spray(p, (e.To - e.Pos).Normalized(), Budget(3), 1.4f, 0.07f, e.Type, p.Y - 0.25f, 0.6f);
                }
                break;
            case EvKind.Bury:
                if (seen) _fx.Burst(_hf.Point(e.Pos, 0.05f), 3, 0, 0.8f, 0.2f, 0.6f); // a little dust as the body is laid down
                break;
        }
    }

    void Hurt(in SimEvent e, Sim sim)
    {
        int who = e.Who - 1;
        if (who < 0) return;
        ref var a = ref sim.Ants[who];
        float floor = Floor(sim, a.Layer, a.Pos);
        var at = new Vector3(a.Pos.X, floor + a.Size * 0.16f + a.Alt, a.Pos.Y);
        var away = e.Pos - e.To;
        away = away.LengthSquared() < 1e-4f ? MathX.Dir(_rng.R(0, Mathf.Tau)) : away.Normalized();
        bool acid = e.By is AntType.Gunner or AntType.Matron;
        Spray(at, away, Budget((int)Mathf.Clamp(e.Amount * 28, 1, 7)), 2.6f, 0.1f + 0.05f * Mathf.Sqrt(a.Size), a.Type, floor, 0.5f);
        if (acid)
        {
            // the chitin hisses as the acid bites into it
            for (int k = 0; k < Budget(3); k++)
                _dropList.Add(new Drop { Pos = at + new Vector3(_rng.R(-0.2f, 0.2f), 0.1f, _rng.R(-0.2f, 0.2f)) * a.Size, Vel = new Vector3(_rng.R(-0.3f, 0.3f), _rng.R(0.6f, 1.2f), _rng.R(-0.3f, 0.3f)),
                    Size = 0.18f * Mathf.Sqrt(a.Size), Max = 0.9f, Floor = floor - 1, Grav = -0.4f, Kind = 10 });
            if (_rng.F() < 0.5f) Stain(new Vector3(a.Pos.X, floor, a.Pos.Y), 0.18f * a.Size, 3, 40f);
            float cur = Etch(who, a.Serial);
            Claim(who, a.Serial);
            _etch[who] = Mathf.Min(7.5f, cur + 1.3f);
            _etchT[who] = _now;
        }
        // a heavy blow can take a leg or an antenna clean off
        if (!Settings.Severing || a.Type == AntType.Queen || e.Amount < 0.14f) return;
        float chance = (Heavy(e.By) ? 0.4f : 0.16f) * Mathf.Clamp(e.Amount * 3f, 0.5f, 1.5f);
        if (_rng.F() > chance) return;
        Sever(sim, who, away, floor, at);
    }

    /// Takes a leg (or now and then an antenna) off a living ant and throws it on the ground, twitching.
    internal void Sever(Sim sim, int who, Vector2 away, float floor, Vector3 at)
    {
        ref var a = ref sim.Ants[who];
        int lost = Lost(who, a.Serial);
        if (BitCount(lost & Legs) >= 2) return;
        int part;
        if (_rng.F() < 0.25f && (lost & Antennae) != Antennae) part = (lost & 64) == 0 ? 64 : 128;
        else { int k = _rng.I(0, 6); while ((lost & Leg(k)) != 0) k = (k + 1) % 6; part = Leg(k); }
        Claim(who, a.Serial);
        _lost[who] = lost | part;
        if (!_wounded.Contains(who)) _wounded.Add(who);
        _leak[who] = _now + _rng.R(0.3f, 0.8f);
        int cap = Settings.Gfx == Settings.Quality.Low ? 50 : 140;
        if (_bits.Count >= cap) _bits.RemoveAt(0);
        var fling = away * _rng.R(0.5f, 1.4f) * Mathf.Max(1f, a.Size * 0.6f);
        _bits.Add(new Bit
        {
            Pos = new Vector3(a.Pos.X + fling.X, floor, a.Pos.Y + fling.Y), Heading = _rng.R(0, Mathf.Tau), Born = _now, Size = a.Size,
            Team = a.Team, Type = a.Type, Mask = All & ~part,
        });
        Spray(at, away, Budget(5), 3f, 0.13f * Mathf.Sqrt(a.Size), a.Type, floor, 0.7f);
    }

    /// A slot's render memory belongs to the ant in it now; a reused slot starts clean.
    void Claim(int id, int serial)
    {
        if (_serial[id] == serial) return;
        _serial[id] = serial; _lost[id] = 0; _etch[id] = 0;
    }

    static int BitCount(int v) { int n = 0; while (v != 0) { n += v & 1; v >>= 1; } return n; }

    void Death(in SimEvent e, Sim sim, bool seen)
    {
        int who = e.Who - 1;
        if (who < 0) return;
        ref var a = ref sim.Ants[who];
        int lost = Lost(who, a.Serial);
        _lost[who] = 0; _etch[who] = 0; _serial[who] = 0;
        float floor = Floor(sim, a.Layer, a.Pos);
        if (seen)
        {
            var at = new Vector3(a.Pos.X, floor + a.Size * 0.15f, a.Pos.Y);
            var away = e.Pos - e.To;
            away = away.LengthSquared() < 1e-4f ? MathX.Dir(_rng.R(0, Mathf.Tau)) : away.Normalized();
            bool bug = Bug(a.Type);
            Spray(at, away, Budget(bug ? 18 : 10), bug ? 3.6f : 2.8f, (bug ? 0.17f : 0.12f) * Mathf.Sqrt(a.Size), a.Type, floor, 0.6f);
            Stain(new Vector3(a.Pos.X, floor, a.Pos.Y), a.Size * _rng.R(0.32f, 0.45f), Fluid(a.Type).stain); // the pool it bleeds out into
        }
        if (e.FoodP1 <= 0) return;
        var look = new Look { Born = _now };
        bool broken = Settings.Severing && a.Type != AntType.Queen && (e.Amount > 0.22f || Heavy(e.By) || _rng.F() < 0.3f);
        Build(look, a.Type, lost, broken);
        _looks[e.FoodP1 - 1] = look;
    }

    /// How a body lies: whole on its back, legs curled; or in pieces, the way a hard bite or the stag's jaws leave it.
    void Build(Look look, AntType type, int lost, bool broken)
    {
        look.Whole = !broken;
        if (!broken)
        {
            look.Pieces.Add(new Piece { Mask = lost, Flip = true, Raise = 0.24f });
            return;
        }
        // one or two more legs come away with the blow that killed it
        int loose = 0;
        for (int n = _rng.I(1, 3), tries = 0; n > 0 && tries < 12; tries++)
        {
            int k = _rng.I(0, 6);
            if (((lost | loose) & Leg(k)) == 0) { loose |= Leg(k); n--; }
        }
        int attached = Legs & ~lost & ~loose;
        for (int k = 0; k < 6; k++)
            if ((loose & Leg(k)) != 0)
            {
                float side = k < 3 ? 1 : -1;
                look.Pieces.Add(new Piece { Mask = All & ~Leg(k), Off = new Vector3(side * _rng.R(0.28f, 0.5f), 0, _rng.R(-0.25f, 0.3f)), Yaw = _rng.R(-1.5f, 1.5f), Raise = -0.05f });
            }
        bool spider = type == AntType.Spider;
        if (!spider)
        {
            // the head with its jaws and whatever antennae it kept
            look.Pieces.Add(new Piece { Mask = All & ~(HeadBit | (Antennae & ~lost)), Off = new Vector3(_rng.R(-0.12f, 0.12f), 0, _rng.R(0.1f, 0.24f)), Yaw = _rng.R(-1.2f, 1.2f), Raise = -0.07f });
            bool flip = _rng.F() < 0.5f;
            look.Pieces.Add(new Piece { Mask = All & ~GasterBit, Off = new Vector3(_rng.R(-0.1f, 0.1f), 0, -_rng.R(0.08f, 0.2f)), Yaw = _rng.R(-0.8f, 0.8f), Flip = flip, Raise = flip ? 0.2f : -0.03f });
            look.Pieces.Add(new Piece { Mask = All & ~(ThoraxBit | attached), Flip = true, Raise = 0.24f });
        }
        else
        {
            // a spider comes apart at the waist: the abdomen, and the cephalothorax with its legs
            look.Pieces.Add(new Piece { Mask = All & ~GasterBit, Off = new Vector3(_rng.R(-0.1f, 0.1f), 0, -_rng.R(0.1f, 0.2f)), Yaw = _rng.R(-0.6f, 0.6f), Raise = -0.04f });
            look.Pieces.Add(new Piece { Mask = All & ~(HeadBit | ThoraxBit | attached | (Antennae & ~lost)), Flip = true, Raise = 0.26f });
        }
    }

    /// Every frame: droplets fly and land, stains soak in, severed limbs twitch and settle, the wounded leak, and
    /// every carcass and the midden are drawn through the ant renderer's dead batches.
    public void Draw(Sim sim, AntRenderer ants, Func<Vector2, bool> visible)
    {
        _now = sim.Time;
        float dt = _last < 0 ? 0 : Mathf.Clamp(_now - _last, 0, 0.1f);
        _last = _now;
        _drops.Begin(); _stains.Begin();
        for (int i = _dropList.Count - 1; i >= 0; i--)
        {
            var d = _dropList[i];
            d.Life += dt;
            d.Vel.Y -= d.Grav * dt;
            d.Pos += d.Vel * dt;
            if (d.Pos.Y <= d.Floor || d.Life >= d.Max)
            {
                if (d.Stains && d.Pos.Y <= d.Floor + 0.05f) Stain(new Vector3(d.Pos.X, d.Floor, d.Pos.Z), d.Size * _rng.R(1.3f, 2.4f), d.StainKind);
                _dropList[i] = _dropList[^1]; _dropList.RemoveAt(_dropList.Count - 1);
                continue;
            }
            _dropList[i] = d;
            float alpha = d.Kind == 10 ? 0.35f * (1 - d.Life / d.Max) : 0.95f;
            float size = d.Kind == 10 ? d.Size * (1 + d.Life * 2) : d.Size;
            _drops.Add(Basis.Identity, d.Pos, new Color(alpha, size, d.Kind, 0));
        }
        for (int i = 0; i < _blots.Length; i++)
        {
            ref var b = ref _blots[i];
            if (b.Life <= 0) continue;
            float age = _now - b.Born;
            if (age > b.Life || age < 0) { b.Life = 0; continue; }
            float alpha = Mathf.Min(1, age * 10) * (1 - Mathf.SmoothStep(b.Life * 0.55f, b.Life, age));
            float wet = 1 - Mathf.SmoothStep(0.4f, 8f, age);
            var n = _hf.Normal(b.Pos.X, b.Pos.Z);
            _stains.Add(AntRenderer.Orient(n, b.Seed * Mathf.Tau, b.R * 2), b.Pos + n * (0.05f + 0.003f * (i % 5)), new Color(alpha, b.Seed, b.Kind, wet));
        }
        // the wounded leak a little as they go
        for (int k = _wounded.Count - 1; k >= 0; k--)
        {
            int id = _wounded[k];
            ref var a = ref sim.Ants[id];
            if (!a.Alive || _serial[id] != a.Serial || _lost[id] == 0) { _wounded.RemoveAt(k); continue; }
            if (_now < _leak[id]) continue;
            _leak[id] = _now + _rng.R(1.1f, 2.2f);
            if (a.Team != Local.Me && !visible(a.Pos)) continue;
            float floor = Floor(sim, a.Layer, a.Pos);
            Spray(new Vector3(a.Pos.X, floor + a.Size * 0.12f + a.Alt, a.Pos.Y), MathX.Dir(a.Heading + Mathf.Pi), 1, 0.4f, 0.08f * Mathf.Sqrt(a.Size), a.Type, floor, 0.9f);
        }
        DrawBits(sim, ants, visible);
        DrawCarcasses(sim, ants, visible);
        DrawMiddens(sim, ants, visible);
        DrawLarders(sim, ants);
        _drops.End(); _stains.End();
    }

    /// The four floats of a dead instance: a twitch that dies away, the parts it is missing.
    Color DeadData(int seed, float born, int mask)
    {
        float t = _now - born;
        float twitch = Mathf.Clamp(1 - t / 3f, 0, 1);
        float gait = twitch > 0 ? Mathf.PosMod(_now * (5f + seed % 3) + seed * 0.37f, 1f) * 0.999f : 0.25f;
        float amp = twitch > 0 ? Mathf.Min(0.996f, 0.15f + 0.8f * twitch * (0.6f + 0.4f * Mathf.Sin(_now * 9 + seed))) : 0f;
        return new Color((seed & 15) + gait, AntRenderer.NeutralLook + amp, mask * 1024f, 0);
    }

    void DrawBits(Sim sim, AntRenderer ants, Func<Vector2, bool> visible)
    {
        for (int k = _bits.Count - 1; k >= 0; k--)
        {
            var b = _bits[k];
            float t = _now - b.Born;
            if (t > 45f || t < 0) { _bits.RemoveAt(k); continue; }
            if (b.Team != Local.Me && !visible(new Vector2(b.Pos.X, b.Pos.Z))) continue;
            var n = _hf.Normal(b.Pos.X, b.Pos.Z);
            float sink = Mathf.Max(0, t - 38f) / 7f * 0.12f * b.Size; // drawn down into the soil at last
            var basis = AntRenderer.Orient(n, b.Heading, b.Size);
            ants.AddDead(b.Team, b.Type, basis, b.Pos + n * (-0.05f * b.Size - sink), DeadData(k * 7 + 3, b.Born, b.Mask));
        }
    }

    void DrawCarcasses(Sim sim, AntRenderer ants, Func<Vector2, bool> visible)
    {
        foreach (var f in sim.W.Food)
        {
            if (f.Kind != FoodKind.Corpse || !f.Alive) continue;
            if (!visible(f.Pos) && f.Team != Local.Me) continue;
            if (!_looks.TryGetValue(f.Id, out var look))
            {
                look = new Look { Born = _now - 100 };
                look.Pieces.Add(new Piece { Flip = true, Raise = 0.24f });
                _looks[f.Id] = look;
            }
            // a dragged carcass slides smoothly between ticks
            if (!look.Placed || look.Shown.DistanceSquaredTo(f.Pos) > 25) { look.Shown = f.Pos; look.Placed = true; }
            else look.Shown = look.Shown.Lerp(f.Pos, 0.25f);
            var p = look.Shown;
            float s = f.Radius / 0.45f;
            if (look.Whole) s *= Mathf.Lerp(0.6f, 1f, f.Amount / f.Max); // eaten from inside, it shrinks
            float floor = Floor(sim, f.Layer, p);
            var n = f.Layer == 0 ? _hf.Normal(p.X, p.Y) : Vector3.Up;
            var frame = AntRenderer.Orient(n, f.Heading, s);
            // workers carry it off a piece at a time: loose legs first, the thorax last
            int count = look.Pieces.Count;
            int shown = look.Whole ? count : Mathf.Clamp(Mathf.CeilToInt(count * f.Amount / f.Max), 1, count);
            for (int k = count - shown; k < count; k++)
            {
                var pc = look.Pieces[k];
                var b = AntRenderer.Orient(n, f.Heading + pc.Yaw, s);
                if (pc.Flip) b = new Basis(-b.X, -b.Y, b.Z);
                var o = new Vector3(p.X, floor, p.Y) + frame * pc.Off + n * (pc.Raise * s);
                ants.AddDead(f.Team, f.AntType, b, o, DeadData(f.Id * 5 + k, look.Born, pc.Mask));
            }
        }
        // forget the looks of carcasses that are gone
        if (_looks.Count > 64 && _rng.F() < 0.05f)
        {
            var alive = new HashSet<int>();
            foreach (var f in sim.W.Food) if (f.Kind == FoodKind.Corpse) alive.Add(f.Id);
            var gone = new List<int>();
            foreach (var id in _looks.Keys) if (!alive.Contains(id)) gone.Add(id);
            foreach (var id in gone) _looks.Remove(id);
        }
    }

    /// The organic reserve, stacked where the workers lay it down: in the store chamber (or at the nest mouth before
    /// there is one), a heap of bug legs, wing cases and enemy heads that grows and shrinks with the reserve.
    void DrawLarders(Sim sim, AntRenderer ants)
    {
        if (!sim.CarrionRules) return;
        AntType[] kinds = { AntType.Beetle, AntType.Spider, AntType.Wasp, AntType.Worker, AntType.Soldier };
        for (int team = 0; team < 2; team++)
        {
            if (team != Local.Me) continue; // the enemy's stores are out of sight
            var (pos, layer) = sim.FoodDeliveryPoint(team);
            int n = Math.Min(36, (int)(sim.Col[team].Organic / 3f));
            float floor = Floor(sim, layer, pos);
            for (int k = 0; k < n; k++)
            {
                float ang = k * 2.39996f + 0.7f;
                var at = pos + MathX.Dir(ang) * (0.2f + 0.24f * Mathf.Sqrt(k));
                var type = kinds[k % kinds.Length];
                int owner = Config.IsBug(type) ? Config.Wild : 1 - team;
                float size = (Config.IsBug(type) ? 0.9f : 1.1f) * (0.85f + 0.1f * (k % 3));
                var b = AntRenderer.Orient(Vector3.Up, ang * 1.9f, size);
                bool flip = k % 3 != 1;
                if (flip) b = new Basis(-b.X, -b.Y, b.Z);
                // a single piece of each: a leg, a head, a gaster or a wing case
                int mask = (k % 4) switch { 0 => All & ~Leg(k % 6), 1 => All & ~(HeadBit | Antennae), 2 => All & ~GasterBit, _ => All & ~(Leg(k % 6) | Leg((k + 2) % 6)) };
                float lift = 0.03f * Mathf.Min(k, 14) / 14f;
                ants.AddDead(owner, type, b, new Vector3(at.X, floor + lift + (flip ? 0.2f : -0.04f) * size, at.Y), new Color((k & 15) + 0.25f, AntRenderer.NeutralLook, mask * 1024f, 0));
            }
        }
    }

    /// The refuse pile each colony builds out of its dead: bodies and pieces heaped on a dark blot of soil.
    void DrawMiddens(Sim sim, AntRenderer ants, Func<Vector2, bool> visible)
    {
        for (int team = 0; team < 2; team++)
        {
            var c = sim.Col[team];
            if (c.Buried <= 0 || !c.HasMidden) continue;
            var m = c.MiddenPos;
            if (team != Local.Me && !visible(m)) continue;
            int n = Math.Min(c.Buried, 42);
            float floor = _hf.Sample(m.X, m.Y);
            var up = _hf.Normal(m.X, m.Y);
            _stains.Add(AntRenderer.Orient(up, team * 1.3f, 2.4f + 0.35f * Mathf.Sqrt(n)), new Vector3(m.X, floor, m.Y) + up * 0.045f, new Color(0.85f, 0.37f + team * 0.2f, 0, 0));
            float size = Config.Of(AntType.Worker).Size * 0.95f;
            for (int k = 0; k < n; k++)
            {
                float ang = k * 2.39996f + team;
                var at = m + MathX.Dir(ang) * (0.25f + 0.33f * Mathf.Sqrt(k));
                float h = _hf.Sample(at.X, at.Y) + 0.04f * Mathf.Min(k, 12) / 12f; // heaped: the later ones lie on the earlier
                var nn = _hf.Normal(at.X, at.Y);
                var b = AntRenderer.Orient(nn, ang * 1.7f, size);
                int kind = k % 4;
                bool flip = kind != 2;
                if (flip) b = new Basis(-b.X, -b.Y, b.Z);
                int mask = kind switch { 1 => HeadBit | Antennae, 2 => All & ~GasterBit, 3 => Leg(k % 6) | Leg((k + 3) % 6), _ => 0 };
                ants.AddDead(team, AntType.Worker, b, new Vector3(at.X, h, at.Y) + nn * ((flip ? 0.24f : -0.03f) * size), new Color((k & 15) + 0.25f, AntRenderer.NeutralLook, mask * 1024f, 0));
            }
        }
    }
}
