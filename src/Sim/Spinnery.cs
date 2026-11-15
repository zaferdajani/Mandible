using System.Collections.Generic;
using Godot;

namespace Mandible;

/// A trap web spun by a colony's silk ants on an approach to its nest. Enemies that walk into it are stuck and bitten
/// harder; a flyer passing low is snagged. Every catch wears it, and the spinnery spins it again.
public sealed class TrapWeb
{
    public Vector2 Pos;
    public float R;
    public int Team, Charges;
    public float Age;      // seconds since it was spun (rendering: it unrolls)
    public float CatchT;   // seconds since the last catch (rendering: it shakes)
}

public enum SpinResult { Ok, Locked, NoFood, Exists, Dead }
public enum TrapResult { Ok, NoSpinnery, TooFar, Blocked }

/// The Spinnery: after Web Throwing, the colony's silk ants live above ground in a silk outpost strung between grass
/// stems beside the mound, and lay trap webs on the approaches (2 + the mutation's rank). The player may re-aim them
/// (`CmdKind.Trap`). Counters: a major tears a web in half the time and ruins it, acid burns one away, and Silk
/// Cutters halve the time stuck (as for every web).
public sealed partial class Sim
{
    public readonly List<TrapWeb> Traps = new();
    /// Tests only (--nettest with MANDIBLE_SILK set): both colonies may spin without the mutation, so two machines exercise the traps.
    public static bool SilkForAll;

    public int TrapCap(int team) => 2 + System.Math.Max(1, Col[team].WebRank);

    public SpinResult CanSpin(int team)
    {
        if (team is < 0 or > 1) return SpinResult.Locked;
        var c = Col[team];
        if (!c.Alive) return SpinResult.Dead;
        if (!(Has(team, Mutation.WebThrowing) || SilkForAll) || !c.Founded || Mode == GameMode.Lanes) return SpinResult.Locked;
        if (c.SpinState != 0) return SpinResult.Exists;
        return c.Food < Config.SpinneryFood ? SpinResult.NoFood : SpinResult.Ok;
    }

    /// The silk ants start the outpost beside the mound.
    public SpinResult OrderSpinnery(int team)
    {
        var r = CanSpin(team);
        if (r != SpinResult.Ok) return r;
        var c = Col[team];
        c.Food -= Config.SpinneryFood;
        c.Spent += Config.SpinneryFood;
        c.SpinState = 1;
        c.SpinDone = 0;
        var nest = NestEntrance(team);
        var dir = Approach(team);
        var side = new Vector2(-dir.Y, dir.X);
        c.SpinPos = nest + side * Config.SpinneryOffset;
        if (W.Blocked(c.SpinPos, 1.5f)) c.SpinPos = nest - side * Config.SpinneryOffset;
        if (W.Blocked(c.SpinPos, 1.5f)) c.SpinPos = nest - dir * Config.SpinneryOffset;
        Events.Add(new SimEvent { Kind = EvKind.SpinneryBegun, Pos = c.SpinPos, Team = team });
        return SpinResult.Ok;
    }

    public TrapResult TrapCheck(int team, Vector2 p)
    {
        if (team is < 0 or > 1 || Col[team].SpinState != 2) return TrapResult.NoSpinnery;
        if (p.DistanceSquaredTo(NestEntrance(team)) > Config.TrapReach * Config.TrapReach) return TrapResult.TooFar;
        return W.Blocked(p, 1f) ? TrapResult.Blocked : TrapResult.Ok;
    }

    /// Re-aims the trap webs: a free place takes the spot; with every place taken the oldest one is given up for it.
    public TrapResult PlaceTrap(int team, Vector2 p)
    {
        var r = TrapCheck(team, p);
        if (r != TrapResult.Ok) return r;
        var c = Col[team];
        while (c.TrapSpots.Count >= TrapCap(team))
        {
            var old = c.TrapSpots[0];
            c.TrapSpots.RemoveAt(0);
            for (int k = Traps.Count - 1; k >= 0; k--)
                if (Traps[k].Team == team && Traps[k].Pos.DistanceSquaredTo(old) < 0.01f) Traps.RemoveAt(k);
        }
        c.TrapSpots.Add(p);
        return TrapResult.Ok;
    }

    /// The way raiders come: toward the rival mound, or the middle of the yard where there is none.
    Vector2 Approach(int team)
    {
        var nest = NestEntrance(team);
        var to = (W.Nests.Length > 1 ? W.Nests[1 - team].Pos : new Vector2(Config.MapSize / 2, Config.MapSize / 2)) - nest;
        if (to.LengthSquared() < 1f) to = new Vector2(team == 0 ? 1 : -1, 0);
        return to / Mathf.Sqrt(to.LengthSquared());
    }

    // fan of trap places across the approach: 0, +-35 and +-70 degrees (cos, sin as constants: no trig in the sim)
    static readonly (float c, float s)[] Fan = { (1f, 0f), (0.8192f, 0.5736f), (0.8192f, -0.5736f), (0.342f, 0.9397f), (0.342f, -0.9397f) };

    void DefaultTrapSpots(Colony c)
    {
        var nest = NestEntrance(c.Team);
        var d = Approach(c.Team);
        int cap = TrapCap(c.Team);
        for (int k = 0; k < Fan.Length && c.TrapSpots.Count < cap; k++)
        {
            var dir = new Vector2(d.X * Fan[k].c - d.Y * Fan[k].s, d.X * Fan[k].s + d.Y * Fan[k].c);
            for (float r = Config.TrapRing; r <= Config.TrapRing + 6f; r += 2f)
            {
                var p = nest + dir * r;
                if (W.Blocked(p, 1f)) continue;
                c.TrapSpots.Add(p);
                break;
            }
        }
    }

    bool TrapAt(int team, Vector2 p)
    {
        foreach (var z in Traps) if (z.Team == team && z.Pos.DistanceSquaredTo(p) < 0.01f) return true;
        return false;
    }

    void TickTraps(float dt)
    {
        foreach (var c in Col)
        {
            if (c.SpinState == 0 || !c.Alive) continue;
            if (c.SpinState == 1)
            {
                c.SpinDone += dt / Config.SpinneryTime;
                if (c.SpinDone < 1) continue;
                c.SpinDone = 1;
                c.SpinState = 2;
                c.SpinT = Config.TrapSpinTime * 0.5f;
                DefaultTrapSpots(c);
                Events.Add(new SimEvent { Kind = EvKind.SpinneryDone, Pos = c.SpinPos, Team = c.Team });
            }
            int cap = System.Math.Min(TrapCap(c.Team), c.TrapSpots.Count), missing = -1;
            for (int s = 0; s < cap && missing < 0; s++) if (!TrapAt(c.Team, c.TrapSpots[s])) missing = s;
            if (missing < 0) { c.SpinT = Config.TrapSpinTime; continue; }
            c.SpinT -= dt;
            if (c.SpinT > 0) continue;
            c.SpinT = Config.TrapSpinTime;
            var at = c.TrapSpots[missing];
            Traps.Add(new TrapWeb { Pos = at, R = Config.TrapRadius, Team = c.Team, Charges = Config.TrapCharges });
            Events.Add(new SimEvent { Kind = EvKind.TrapSpun, Pos = at, To = c.SpinPos, Team = c.Team, Radius = Config.TrapRadius });
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
