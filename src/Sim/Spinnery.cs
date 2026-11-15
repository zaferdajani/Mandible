using System.Collections.Generic;
using Godot;

namespace Mandible;

/// A trap web spun by a colony's silk ants. Enemies that walk into it are stuck and bitten harder; a flyer passing
/// low is snagged. Every catch wears it, and the web post it belongs to spins it again.
public sealed class TrapWeb
{
    public Vector2 Pos;
    public float R;
    public int Team, Charges, Work;
    public float Age;      // seconds since it was spun (rendering: it unrolls)
    public float CatchT;   // seconds since the last catch (rendering: it shakes)
}

/// What the colony builds above ground from straw once it has silk (Web Throwing).
/// Spinnery: the offence, a silk barracks beside the mound that hatches silk ants (soldiers that also snare ground enemies).
/// Post: the defence, a web post that keeps one trap web; it is improved into one of two kinds:
/// Tower (silk ants up the stems fling webs at whatever comes in reach) or TrapNest (three trap webs, re-aimed by the player).
public enum SilkKind : byte { Spinnery, Post, Tower, TrapNest }

public sealed class SilkWork
{
    public int Id, Team;
    public SilkKind Kind;
    public Vector2 Pos;
    public float Done;               // 0..1 while it is being spun
    public bool Built => Done >= 1;
    public bool Upgrading;           // a post being improved
    public SilkKind UpTo;
    public float UpDone;
    public float SpinT, ShotCd, HatchT;
    public int Queue;                // silk ants ordered from a spinnery
    public readonly List<Vector2> Spots = new(); // where its trap webs go
    public float ShotAnim;           // rendering only
}

public enum SilkResult { Ok, Locked, NoFood, NoStraw, Full, TooFar, TooClose, Blocked, Dead, Busy, NoPop }

public sealed partial class Sim
{
    public readonly List<SilkWork> Works = new();
    public readonly List<TrapWeb> Traps = new();
    int _nextWork;
    bool _strawSeeded;
    /// Tests only (--nettest with MANDIBLE_SILK set): both colonies have silk without the mutation, so two machines exercise it.
    public static bool SilkForAll;

    public bool HasSilk(int team) => team is 0 or 1 && (Has(team, Mutation.WebThrowing) || SilkForAll) && Mode != GameMode.Lanes;
    public int SilkCap(int team) => 2 + System.Math.Max(1, Col[team].WebRank);
    public int SilkCount(int team) { int n = 0; foreach (var w in Works) if (w.Team == team) n++; return n; }
    public SilkWork WorkById(int id) { foreach (var w in Works) if (w.Id == id) return w; return null; }
    public static int TrapsOf(SilkKind k) => k == SilkKind.TrapNest ? 3 : k == SilkKind.Post ? 1 : 0;
    public static float StrawCost(SilkKind k) => k == SilkKind.Spinnery ? Config.SpinneryStraw : k == SilkKind.Post ? Config.PostStraw : Config.SilkUpStraw;
    public static float FoodCost(SilkKind k) => k == SilkKind.Spinnery ? Config.SpinneryFood : k == SilkKind.Post ? 0 : Config.SilkUpFood;
    public static string SilkName(SilkKind k) => k switch { SilkKind.Spinnery => "Spinnery", SilkKind.Post => "Web Post", SilkKind.Tower => "Web Tower", _ => "Trap Nest" };

    /// Workers bring straw home while the colony has silk and its straw store is low.
    public bool WantsStraw(int team) => HasSilk(team) && Col[team].Straw < Config.StrawWant;

    SilkResult SilkBase(int team)
    {
        if (team is < 0 or > 1) return SilkResult.Locked;
        if (!Col[team].Alive) return SilkResult.Dead;
        return HasSilk(team) && Col[team].Founded ? SilkResult.Ok : SilkResult.Locked;
    }

    /// Whether the ground at p can take a silk structure of this colony.
    public SilkResult SiteCheck(int team, Vector2 p)
    {
        var nest = NestEntrance(team);
        float d = p.DistanceSquaredTo(nest);
        if (d > Config.SilkReach * Config.SilkReach) return SilkResult.TooFar;
        if (d < Config.SilkGap * Config.SilkGap) return SilkResult.TooClose;
        foreach (var w in Works) if (w.Pos.DistanceSquaredTo(p) < Config.SilkGap * Config.SilkGap) return SilkResult.TooClose;
        return W.Blocked(p, 1.5f) ? SilkResult.Blocked : SilkResult.Ok;
    }

    public SilkResult CanBuildSilk(int team, SilkKind kind)
    {
        var r = SilkBase(team);
        if (r != SilkResult.Ok) return r;
        if (kind is not (SilkKind.Spinnery or SilkKind.Post)) return SilkResult.Locked;
        if (SilkCount(team) >= SilkCap(team)) return SilkResult.Full;
        var c = Col[team];
        if (c.Straw < StrawCost(kind)) return SilkResult.NoStraw;
        return c.Food < FoodCost(kind) ? SilkResult.NoFood : SilkResult.Ok;
    }

    public SilkResult BuildSilk(int team, SilkKind kind, Vector2 p)
    {
        var r = CanBuildSilk(team, kind);
        if (r == SilkResult.Ok) r = SiteCheck(team, p);
        if (r != SilkResult.Ok) return r;
        var c = Col[team];
        c.Straw -= StrawCost(kind);
        c.Food -= FoodCost(kind); c.Spent += FoodCost(kind);
        var w = new SilkWork { Id = _nextWork++, Team = team, Kind = kind, Pos = p };
        Works.Add(w);
        Events.Add(new SimEvent { Kind = EvKind.SpinneryBegun, Pos = p, Team = team, Tier = (int)kind });
        return SilkResult.Ok;
    }

    public SilkResult CanUpgradeSilk(int team, SilkWork w, SilkKind to)
    {
        var r = SilkBase(team);
        if (r != SilkResult.Ok) return r;
        if (w == null || w.Team != team || w.Kind != SilkKind.Post || to is not (SilkKind.Tower or SilkKind.TrapNest)) return SilkResult.Locked;
        if (!w.Built || w.Upgrading) return SilkResult.Busy;
        var c = Col[team];
        if (c.Straw < Config.SilkUpStraw) return SilkResult.NoStraw;
        return c.Food < Config.SilkUpFood ? SilkResult.NoFood : SilkResult.Ok;
    }

    /// A web post is improved into a web tower or a trap nest: the choice is made once.
    public SilkResult UpgradeSilk(int team, int id, SilkKind to)
    {
        var w = WorkById(id);
        var r = CanUpgradeSilk(team, w, to);
        if (r != SilkResult.Ok) return r;
        var c = Col[team];
        c.Straw -= Config.SilkUpStraw;
        c.Food -= Config.SilkUpFood; c.Spent += Config.SilkUpFood;
        w.Upgrading = true; w.UpTo = to; w.UpDone = 0;
        return SilkResult.Ok;
    }

    public SilkResult CanHatchSilk(int team, SilkWork w)
    {
        var r = SilkBase(team);
        if (r != SilkResult.Ok) return r;
        if (w == null || w.Team != team || w.Kind != SilkKind.Spinnery) return SilkResult.Locked;
        if (!w.Built) return SilkResult.Busy;
        if (w.Queue >= 3) return SilkResult.Full;
        var c = Col[team];
        if (c.PopUsed + (w.Queue + 1) * Config.Stats[(int)AntType.Soldier].Pop > c.PopCap) return SilkResult.NoPop;
        return c.Food < Config.SilkAntFood ? SilkResult.NoFood : SilkResult.Ok;
    }

    /// A spinnery raises a silk ant: a soldier that also snares ground enemies with thrown silk.
    public SilkResult HatchSilk(int team, int id)
    {
        var w = WorkById(id);
        var r = CanHatchSilk(team, w);
        if (r != SilkResult.Ok) return r;
        Col[team].Food -= Config.SilkAntFood; Col[team].Spent += Config.SilkAntFood;
        w.Queue++;
        return SilkResult.Ok;
    }

    /// The web post or trap nest of this colony that would take a trap web at p (the nearest one in reach), or null.
    public SilkWork TrapOwner(int team, Vector2 p)
    {
        SilkWork best = null;
        float bd = Config.TrapReach * Config.TrapReach;
        foreach (var w in Works)
        {
            if (w.Team != team || !w.Built || TrapsOf(w.Kind) == 0) continue;
            float d = w.Pos.DistanceSquaredTo(p);
            if (d < bd) { bd = d; best = w; }
        }
        return best;
    }

    public SilkResult TrapCheck(int team, Vector2 p)
    {
        if (team is < 0 or > 1) return SilkResult.Locked;
        bool any = false;
        foreach (var w in Works) if (w.Team == team && w.Built && TrapsOf(w.Kind) > 0) any = true;
        if (!any) return SilkResult.Locked;
        if (TrapOwner(team, p) == null) return SilkResult.TooFar;
        return W.Blocked(p, 1f) ? SilkResult.Blocked : SilkResult.Ok;
    }

    /// Re-aims a trap web: the nearest post or trap nest takes the spot, giving up its oldest one.
    public SilkResult PlaceTrap(int team, Vector2 p)
    {
        var r = TrapCheck(team, p);
        if (r != SilkResult.Ok) return r;
        var w = TrapOwner(team, p);
        while (w.Spots.Count >= TrapsOf(w.Kind))
        {
            var old = w.Spots[0];
            w.Spots.RemoveAt(0);
            for (int k = Traps.Count - 1; k >= 0; k--)
                if (Traps[k].Work == w.Id && Traps[k].Pos.DistanceSquaredTo(old) < 0.01f) Traps.RemoveAt(k);
        }
        w.Spots.Add(p);
        return SilkResult.Ok;
    }

    /// The way raiders come: toward the rival mound, or the middle of the yard where there is none.
    public Vector2 Approach(int team)
    {
        var nest = NestEntrance(team);
        var to = (W.Nests.Length > 1 ? W.Nests[1 - team].Pos : new Vector2(Config.MapSize / 2, Config.MapSize / 2)) - nest;
        if (to.LengthSquared() < 1f) to = new Vector2(team == 0 ? 1 : -1, 0);
        return to / Mathf.Sqrt(to.LengthSquared());
    }

    // a fan across the approach: 0, +-35 and +-70 degrees (cos, sin as constants: no trig in the sim)
    static readonly (float c, float s)[] Fan = { (1f, 0f), (0.8192f, 0.5736f), (0.8192f, -0.5736f), (0.342f, 0.9397f), (0.342f, -0.9397f) };
    static Vector2 Turn(Vector2 d, int k) => new(d.X * Fan[k].c - d.Y * Fan[k].s, d.X * Fan[k].s + d.Y * Fan[k].c);

    /// Where a colony puts a structure when nobody chooses (the AI, and the tests): the spinnery beside the mound,
    /// posts fanned across the approach.
    public Vector2 DefaultSilkSpot(int team, SilkKind kind)
    {
        var nest = NestEntrance(team);
        var d = Approach(team);
        if (kind == SilkKind.Spinnery)
        {
            var side = new Vector2(-d.Y, d.X);
            foreach (var p in new[] { nest + side * 7f, nest - side * 7f, nest - d * 7f, nest + side * 11f, nest - side * 11f })
                if (SiteCheck(team, p) == SilkResult.Ok) return p;
        }
        for (float r = 11f; r <= 23f; r += 4f)
            for (int k = 0; k < Fan.Length; k++)
            {
                var p = nest + Turn(d, k) * r;
                if (SiteCheck(team, p) == SilkResult.Ok) return p;
            }
        return nest + d * 11f;
    }

    void DefaultTrapSpots(SilkWork w)
    {
        var d = w.Pos - NestEntrance(w.Team);
        d = d.LengthSquared() < 1f ? Approach(w.Team) : d / Mathf.Sqrt(d.LengthSquared());
        int want = TrapsOf(w.Kind);
        foreach (int k in new[] { 0, 3, 4 })
        {
            if (w.Spots.Count >= want) break;
            for (float r = Config.TrapOut; r <= Config.TrapOut + 4f; r += 2f)
            {
                var p = w.Pos + Turn(d, k) * r;
                if (W.Blocked(p, 1f)) continue;
                bool taken = false;
                foreach (var s in w.Spots) if (s.DistanceSquaredTo(p) < 4f) taken = true;
                if (taken) continue;
                w.Spots.Add(p);
                break;
            }
        }
    }

    bool TrapAt(int work, Vector2 p)
    {
        foreach (var z in Traps) if (z.Work == work && z.Pos.DistanceSquaredTo(p) < 0.01f) return true;
        return false;
    }

    /// Dry grass lying round each mound: the raw material silk is strung on. It appears with the first silk.
    void SeedStraw()
    {
        _strawSeeded = true;
        for (int team = 0; team < W.Nests.Length && team < 2; team++)
        {
            var nest = NestEntrance(team);
            var d = Approach(team);
            int n = 0;
            foreach (var dir in new[] { Turn(d, 3), Turn(d, 4), -d, Turn(d, 1), Turn(d, 2) })
            {
                if (n >= 3) break;
                for (float r = 15f; r <= 27f; r += 4f)
                {
                    var p = nest + dir * r;
                    if (p.X < 8 || p.Y < 8 || p.X > Config.MapSize - 8 || p.Y > Config.MapSize - 8 || W.Blocked(p, 2.5f)) continue;
                    WorldBuilder.Spawn(W, new Rng((uint)(7717 + team * 31 + n)), FoodKind.Straw, p, Config.StrawPile, 2.0f);
                    n++;
                    break;
                }
            }
        }
    }

    /// A silk ant flings a snare at the nearest ground enemy in reach: it is held where it stands for a moment.
    void ThrowSnare(int i, ref Ant a)
    {
        int best = -1;
        float bd = Config.SnareRange * Config.SnareRange;
        foreach (int j in EnemiesNear(a.Pos, Config.SnareRange, a.Team, true))
        {
            ref var b = ref Ants[j];
            if (b.Type == AntType.Queen || b.Root > 0 || b.SnareCd > 0 || Config.Flies(b.Type)) continue;
            float d = a.Pos.DistanceSquaredTo(b.Pos);
            if (d < bd) { bd = d; best = j; }
        }
        if (best < 0) return;
        ref var t = ref Ants[best];
        bool strong = t.Type == AntType.Major || t.Boss || Config.IsHero(t.Type);
        t.Root = Config.SnareRoot * (strong ? 0.5f : 1f);
        t.Snared = true;
        t.SnareCd = Config.SnareRoot + Config.TrapGrace;
        a.SpecialCd = Mutations.WebCooldown;
        a.AttackAnim = 1;
        Events.Add(new SimEvent { Kind = EvKind.WebShot, Pos = a.Pos, To = t.Pos, Team = a.Team, Type = a.Type });
    }

    void TickSilk(float dt)
    {
        if (!_strawSeeded && (HasSilk(0) || HasSilk(1))) SeedStraw();
        for (int k = Works.Count - 1; k >= 0; k--)
        {
            var w = Works[k];
            var c = Col[w.Team];
            if (!c.Alive) { Works.RemoveAt(k); continue; }
            if (!w.Built)
            {
                w.Done += dt / (w.Kind == SilkKind.Spinnery ? Config.SpinneryTime : Config.PostTime);
                if (w.Done < 1) continue;
                w.Done = 1;
                w.SpinT = Config.TrapSpinTime * 0.4f;
                DefaultTrapSpots(w);
                Events.Add(new SimEvent { Kind = EvKind.SpinneryDone, Pos = w.Pos, Team = w.Team, Tier = (int)w.Kind });
            }
            if (w.Upgrading)
            {
                w.UpDone += dt / Config.SilkUpTime;
                if (w.UpDone >= 1)
                {
                    w.Upgrading = false; w.Kind = w.UpTo; w.UpDone = 0;
                    if (TrapsOf(w.Kind) == 0)
                    {
                        w.Spots.Clear();
                        for (int t = Traps.Count - 1; t >= 0; t--) if (Traps[t].Work == w.Id) Traps.RemoveAt(t);
                    }
                    else DefaultTrapSpots(w);
                    Events.Add(new SimEvent { Kind = EvKind.SpinneryDone, Pos = w.Pos, Team = w.Team, Tier = (int)w.Kind });
                }
            }
            switch (w.Kind)
            {
                case SilkKind.Spinnery:
                    if (w.Queue <= 0) { w.HatchT = 0; break; }
                    w.HatchT += dt;
                    if (w.HatchT < Config.SilkAntTime || c.PopUsed + Config.Stats[(int)AntType.Soldier].Pop > c.PopCap) break;
                    w.HatchT = 0; w.Queue--;
                    var d = Approach(w.Team);
                    int id = Spawn(w.Team, AntType.Soldier, w.Pos + d * 2.2f, 0);
                    if (id >= 0) { Ants[id].Silk = true; Events.Add(new SimEvent { Kind = EvKind.Spawn, Pos = w.Pos, To = Ants[id].Pos, HatchP1 = id + 1, Team = w.Team, Type = AntType.Soldier }); }
                    break;
                case SilkKind.Tower:
                    w.ShotCd -= dt;
                    if (w.ShotCd > 0) break;
                    w.ShotCd = 0.3f; // look again soon
                    int best = -1;
                    float bd = Config.TowerRange * Config.TowerRange;
                    foreach (int j in EnemiesNear(w.Pos, Config.TowerRange, w.Team, false))
                    {
                        ref var b = ref Ants[j];
                        if (b.Type == AntType.Queen || b.SnareCd > 0 || b.Root > 0 || b.PinT > 0) continue;
                        float dd = w.Pos.DistanceSquaredTo(b.Pos);
                        if (dd < bd) { bd = dd; best = j; }
                    }
                    if (best < 0) break;
                    ref var t2 = ref Ants[best];
                    bool strong = t2.Type == AntType.Major || t2.Boss || Config.IsHero(t2.Type);
                    if (Config.Flies(t2.Type) && t2.Alt > 1.5f) t2.PinT = Config.TrapPin * (strong ? 0.5f : 1f);
                    else t2.Root = Config.TowerRoot * (strong ? 0.5f : 1f);
                    t2.Snared = true;
                    t2.SnareCd = Config.TowerRoot + Config.TrapGrace;
                    w.ShotCd = Config.TowerCd;
                    Events.Add(new SimEvent { Kind = EvKind.WebShot, Pos = w.Pos, To = t2.Pos, Team = w.Team, Type = AntType.Soldier, Radius = 4f });
                    break;
                default:
                    int cap = System.Math.Min(TrapsOf(w.Kind), w.Spots.Count), missing = -1;
                    for (int s = 0; s < cap && missing < 0; s++) if (!TrapAt(w.Id, w.Spots[s])) missing = s;
                    if (missing < 0) { w.SpinT = Config.TrapSpinTime; break; }
                    w.SpinT -= dt;
                    if (w.SpinT > 0) break;
                    w.SpinT = Config.TrapSpinTime;
                    var at = w.Spots[missing];
                    Traps.Add(new TrapWeb { Pos = at, R = Config.TrapRadius, Team = w.Team, Charges = Config.TrapCharges, Work = w.Id });
                    Events.Add(new SimEvent { Kind = EvKind.TrapSpun, Pos = at, To = w.Pos, Team = w.Team, Radius = Config.TrapRadius });
                    break;
            }
        }
        for (int k = Traps.Count - 1; k >= 0; k--)
        {
            var z = Traps[k];
            z.Age += dt;
            z.CatchT += dt;
            bool burnt = false;
            if (!Col[z.Team].Alive) z.Charges = 0;
            foreach (var p in Pools)
            {
                float reach = p.R + z.R * 0.5f;
                if (p.Team != z.Team && p.Pos.DistanceSquaredTo(z.Pos) < reach * reach) { z.Charges = 0; burnt = true; break; } // acid eats silk
            }
            if (z.Charges > 0)
                foreach (int j in EnemiesNear(z.Pos, z.R, z.Team, false))
                {
                    ref var b = ref Ants[j];
                    if (b.Type == AntType.Queen || b.SnareCd > 0) continue;
                    bool strong = b.Type == AntType.Major || b.Boss || Config.IsHero(b.Type);
                    if (Config.Flies(b.Type))
                    {
                        if (b.PinT > 0 || b.Alt > Config.TrapSnagAlt) continue; // high flyers pass over it
                        b.PinT = Config.TrapPin * (strong ? 0.5f : 1f);
                    }
                    else
                    {
                        if (b.Root > 0 || b.Alt > 1.5f) continue;
                        b.Root = Config.TrapRoot * (strong ? 0.5f : 1f);
                    }
                    b.Snared = true;
                    b.SnareCd = Config.TrapRoot + Config.TrapGrace;
                    z.CatchT = 0;
                    z.Charges -= b.Type == AntType.Major ? Config.TrapCharges : 1; // a major rips the whole web apart as it tears free
                    Events.Add(new SimEvent { Kind = EvKind.TrapCatch, Pos = b.Pos, To = z.Pos, Team = z.Team, Type = b.Type, Radius = z.R });
                    if (z.Charges <= 0) break;
                }
            if (z.Charges > 0) continue;
            Traps.RemoveAt(k);
            Events.Add(new SimEvent { Kind = EvKind.TrapTorn, Pos = z.Pos, Team = z.Team, Radius = z.R, Tier = burnt ? 1 : 0 });
        }
    }
}
