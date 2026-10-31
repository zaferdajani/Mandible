using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The rooms of an ant nest. Real colonies keep brood shallow where the soil is warm, stores at
/// mid depth and the queen deepest; every room is dug by workers, not conjured.
public enum ChamberKind : byte { Royal, Brood, Storage, Forge, Armory, Wings, Silk }

public sealed class NestChamber
{
    public int Id;
    public ChamberKind Kind;
    public int Tier = 1;          // royal / forge / armory grow with their upgrades
    public Vector2 Offset;        // from the nest centre, in map units (the underground is a second 2D layer)
    public float Depth;           // visual depth below the surface
    public float Work, Done;      // worker-seconds needed and spent on the current dig
    public bool Built;            // usable; an expansion keeps Built while Done climbs again
    public int Diggers;           // workers currently digging here (recounted every tick)
    public float Progress => Work <= 0 ? 1 : Mathf.Clamp(Done / Work, 0, 1);
    public bool Digging => Done < Work;
}

public sealed partial class Sim
{
    public const float NestRadius = 13f;          // how far the underground layer reaches from the shaft
    public const int MaxDiggers = 4;              // per chamber
    public const float FoundingWork = 150f;       // worker-seconds to dig the shaft and royal chamber
    public const float BroodWork = 90f, StorageWork = 80f;
    public const float BroodCost = 100f, StorageCost = 80f;
    public const int MaxStorages = 4;

    /// The underground layer that belongs to a colony (0 is the surface).
    public static int NestLayer(int team) => team + 1;

    public NestChamber Royal(int team) => Col[team].Chambers.Count > 0 ? Col[team].Chambers[0] : null;

    /// World position of a chamber (its layer is the colony's nest layer).
    public Vector2 ChamberPos(int team, NestChamber ch) => W.Nests[team].Pos + ch.Offset;

    public int ChamberCount(int team, ChamberKind k, bool builtOnly)
    {
        int n = 0;
        foreach (var ch in Col[team].Chambers) if (ch.Kind == k && (!builtOnly || ch.Built)) n++;
        return n;
    }

    public NestChamber ChamberOf(int team, ChamberKind k)
    {
        foreach (var ch in Col[team].Chambers) if (ch.Kind == k) return ch;
        return null;
    }

    public static ChamberKind KindOf(Upgrade u) => u switch
    {
        Upgrade.Queen => ChamberKind.Royal, Upgrade.Mandibles => ChamberKind.Forge, Upgrade.Chitin => ChamberKind.Armory,
        Upgrade.Wings => ChamberKind.Wings, _ => ChamberKind.Silk,
    };

    public static string ChamberName(ChamberKind k) => k switch
    {
        ChamberKind.Royal => "Royal Chamber", ChamberKind.Brood => "Brood Chamber", ChamberKind.Storage => "Storage",
        ChamberKind.Forge => "Mandible Forge", ChamberKind.Armory => "Chitin Armory", ChamberKind.Wings => "Wing Chamber", _ => "Silk Cutters",
    };

    /// Where a new chamber goes: a layout that fans out from the shaft like a real nest, each kind at its own depth.
    (Vector2 offset, float depth) Layout(int team, ChamberKind k, int index)
    {
        var nest = W.Nests[team].Pos;
        var away = (nest - new Vector2(Config.MapSize, Config.MapSize) * 0.5f).Normalized();
        float baseAngle = away.AngleD();
        float ang, r, depth;
        switch (k)
        {
            case ChamberKind.Royal: ang = baseAngle; r = 7f; depth = 11f; break;
            case ChamberKind.Brood: ang = baseAngle + (index % 2 == 0 ? 1 : -1) * (1.25f + 0.55f * (index / 2)); r = 5.5f + 0.9f * (index / 2); depth = 3.5f; break;
            case ChamberKind.Storage: ang = baseAngle + DMath.Pi + (index % 2 == 0 ? 0.45f : -0.45f) * (1 + index / 2); r = 6.5f + 1.2f * (index / 2); depth = 7f; break;
            case ChamberKind.Forge: ang = baseAngle + 2.45f; r = 8.5f; depth = 8.5f; break;
            case ChamberKind.Armory: ang = baseAngle - 2.45f; r = 8.5f; depth = 8.5f; break;
            case ChamberKind.Wings: ang = baseAngle + 0.7f; r = 9.5f; depth = 5f; break;
            default: ang = baseAngle - 0.7f; r = 9.5f; depth = 6f; break;
        }
        return (MathX.Dir(ang) * r, depth);
    }

    NestChamber AddChamber(int team, ChamberKind k, float work, bool built)
    {
        var c = Col[team];
        int index = ChamberCount(team, k, false);
        var (off, depth) = Layout(team, k, index);
        var ch = new NestChamber { Id = c.Chambers.Count, Kind = k, Offset = off, Depth = depth, Work = work, Done = built ? work : 0, Built = built };
        c.Chambers.Add(ch);
        return ch;
    }

    /// A colony that begins already dug in (Lane War, tests): shaft, royal chamber, a brood chamber and a store.
    void FoundInstantly(int team)
    {
        var c = Col[team];
        if (c.Founded) return;
        if (c.Chambers.Count == 0) AddChamber(team, ChamberKind.Royal, FoundingWork, true);
        else { c.Chambers[0].Done = c.Chambers[0].Work; c.Chambers[0].Built = true; }
        c.Founded = true;
        if (ChamberCount(team, ChamberKind.Brood, false) == 0) AddChamber(team, ChamberKind.Brood, BroodWork, true);
        if (ChamberCount(team, ChamberKind.Storage, false) == 0) AddChamber(team, ChamberKind.Storage, StorageWork, true);
        RecountCapacity(c);
        // the queen takes her place below
        if (c.QueenId >= 0 && Ants[c.QueenId].Alive)
        {
            ref var q = ref Ants[c.QueenId];
            q.Layer = (byte)NestLayer(team);
            q.Pos = q.PrevPos = ChamberPos(team, Royal(team));
            q.Anchor = q.Pos;
        }
    }

    /// Brood chambers set how many ants the colony can raise; stores set how much food it can keep.
    void RecountCapacity(Colony c)
    {
        int brood = ChamberCount(c.Team, ChamberKind.Brood, true);
        int stores = ChamberCount(c.Team, ChamberKind.Storage, true);
        c.PopCap = c.Founded ? Mathf.Min(Config.MaxPopCap, Config.StartPopCap + Config.ChamberPop * brood) : Config.FoundingPop;
        c.FoodCap = Mode == GameMode.Lanes ? 1e9f : Config.BaseFoodCap + Config.StorageFoodCap * stores;
    }

    /// Tours and tests: a colony with every room dug and its tiers set.
    public void DevelopNest(int team)
    {
        FoundInstantly(team);
        var c = Col[team];
        foreach (ChamberKind k in new[] { ChamberKind.Forge, ChamberKind.Armory, ChamberKind.Wings, ChamberKind.Silk })
            if (ChamberOf(team, k) == null) AddChamber(team, k, 60, true);
        while (ChamberCount(team, ChamberKind.Brood, false) < 3) AddChamber(team, ChamberKind.Brood, BroodWork, true);
        while (ChamberCount(team, ChamberKind.Storage, false) < 2) AddChamber(team, ChamberKind.Storage, StorageWork, true);
        foreach (var ch in c.Chambers)
            ch.Tier = ch.Kind switch { ChamberKind.Royal => c.T(Upgrade.Queen), ChamberKind.Forge => c.T(Upgrade.Mandibles), ChamberKind.Armory => c.T(Upgrade.Chitin), _ => 1 };
        RecountCapacity(c);
    }

    // ------------------------------------------------------------------ dig orders
    public enum DigResult { Ok, NoFood, NotFounded, TooMany, Busy, Dead }

    public DigResult CanDig(int team, ChamberKind k)
    {
        var c = Col[team];
        if (!c.Alive) return DigResult.Dead;
        if (Mode == GameMode.Lanes) return DigResult.Busy;
        if (!c.Founded) return DigResult.NotFounded;
        if (k == ChamberKind.Brood && c.PopCap + Config.ChamberPop * (1 + CountDigging(team, k)) > Config.MaxPopCap + Config.ChamberPop) return DigResult.TooMany;
        if (k == ChamberKind.Storage && ChamberCount(team, k, false) >= MaxStorages) return DigResult.TooMany;
        if (k is not (ChamberKind.Brood or ChamberKind.Storage)) return DigResult.Busy; // upgrade rooms go through research
        if (c.Food < (k == ChamberKind.Brood ? BroodCost : StorageCost)) return DigResult.NoFood;
        return DigResult.Ok;
    }

    int CountDigging(int team, ChamberKind k)
    {
        int n = 0;
        foreach (var ch in Col[team].Chambers) if (ch.Kind == k && !ch.Built) n++;
        return n;
    }

    /// Orders a new brood chamber or store: pays the food and opens the dig; workers are sent on the next tick.
    public DigResult Dig(int team, ChamberKind k)
    {
        var r = CanDig(team, k);
        if (r != DigResult.Ok) return r;
        var c = Col[team];
        float cost = k == ChamberKind.Brood ? BroodCost : StorageCost;
        c.Food -= cost;
        c.Spent += cost;
        AddChamber(team, k, k == ChamberKind.Brood ? BroodWork : StorageWork, false);
        return DigResult.Ok;
    }

    /// Every chamber that still wants digging, in the order workers should be sent.
    IEnumerable<NestChamber> OpenDigs(Colony c)
    {
        foreach (var ch in c.Chambers) if (ch.Digging) yield return ch;
    }

    float _digTimer;

    /// Assigns idle or foraging workers to open digs and advances the digs by the workers standing in them.
    void TickNest(float dt)
    {
        _digTimer -= dt;
        bool assign = _digTimer <= 0;
        if (assign) _digTimer = 0.5f;
        foreach (var c in Col)
        {
            if (!c.Alive) continue;
            foreach (var ch in c.Chambers) ch.Diggers = 0;
            // count diggers in place and advance their chambers
            for (int i = 0; i < High; i++)
            {
                ref var a = ref Ants[i];
                if (!a.Alive || a.Team != c.Team || a.DigP1 == 0) continue;
                var ch = c.Chambers[a.DigP1 - 1];
                if (!ch.Digging) { a.DigP1 = 0; a.State = AntState.Forage; continue; }
                var site = c.Founded ? ChamberPos(c.Team, ch) : W.Nests[c.Team].Pos;
                int wantLayer = c.Founded ? NestLayer(c.Team) : 0;
                if (a.Layer == wantLayer && a.Pos.DistanceTo(site) < 2.6f)
                {
                    ch.Diggers++;
                    ch.Done += dt;
                    a.Gait += dt * 0.8f; // mandibles at work
                }
            }
            for (int k = c.Chambers.Count - 1; k >= 0; k--) // FinishDig may add rooms (the founding), so never enumerate here
            {
                var ch = c.Chambers[k];
                if (ch.Done >= ch.Work && ch.Work > 0 && !ch.Built) FinishDig(c, ch);
            }
            if (assign) AssignDiggers(c);
        }
    }

    void FinishDig(Colony c, NestChamber ch)
    {
        bool wasBuilt = ch.Built;
        ch.Built = true;
        ch.Work = ch.Done; // no longer digging
        if (ch.Kind is not (ChamberKind.Brood or ChamberKind.Storage or ChamberKind.Royal) || ch.Kind == ChamberKind.Royal && c.Founded)
            return; // upgrade rooms are completed by the research that ordered them (TickResearch)
        if (ch.Kind == ChamberKind.Royal && !c.Founded)
        {
            c.Founded = true;
            if (ChamberCount(c.Team, ChamberKind.Brood, false) == 0) AddChamber(c.Team, ChamberKind.Brood, BroodWork, true);
            if (ChamberCount(c.Team, ChamberKind.Storage, false) == 0) AddChamber(c.Team, ChamberKind.Storage, StorageWork, true);
            Events.Add(new SimEvent { Kind = EvKind.NestFounded, Pos = W.Nests[c.Team].Pos, Team = c.Team });
        }
        else if (!wasBuilt) Events.Add(new SimEvent { Kind = EvKind.ChamberDone, Pos = W.Nests[c.Team].Pos, Team = c.Team, Chamber = ch.Kind });
        RecountCapacity(c);
        // the diggers go back to foraging (they leave the nest on their own)
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (a.Alive && a.Team == c.Team && a.DigP1 == ch.Id + 1) { a.DigP1 = 0; a.State = AntState.Forage; a.FoodTarget = null; a.Assigned = null; }
        }
    }

    void AssignDiggers(Colony c)
    {
        foreach (var ch in OpenDigs(c))
        {
            int have = 0;
            for (int i = 0; i < High; i++) if (Ants[i].Alive && Ants[i].Team == c.Team && Ants[i].DigP1 == ch.Id + 1) have++;
            int want = MaxDiggers - have;
            var site = c.Founded ? ChamberPos(c.Team, ch) : W.Nests[c.Team].Pos;
            while (want-- > 0)
            {
                int best = -1;
                float bestD = float.MaxValue;
                for (int i = 0; i < High; i++)
                {
                    ref var a = ref Ants[i];
                    if (!a.Alive || a.Team != c.Team || a.Type != AntType.Worker || a.DigP1 != 0 || a.Carrying) continue;
                    if (a.State is not (AntState.Forage or AntState.Gather or AntState.Idle)) continue;
                    float d = a.Pos.DistanceSquaredTo(site);
                    if (d < bestD) { bestD = d; best = i; }
                }
                if (best < 0) break;
                ref var w = ref Ants[best];
                w.DigP1 = ch.Id + 1;
                w.State = AntState.Dig;
                w.FoodTarget = null;
                w.Assigned = null;
                w.Goal = site;
                w.GoalLayer = (byte)(c.Founded ? NestLayer(c.Team) : 0);
            }
        }
    }

    /// A digger walks to its chamber and stays there.
    Vector2 DigLogic(ref Ant a)
    {
        var c = Col[a.Team];
        if (a.DigP1 == 0 || a.DigP1 > c.Chambers.Count) { a.State = AntState.Forage; return Vector2.Zero; }
        var ch = c.Chambers[a.DigP1 - 1];
        var site = c.Founded ? ChamberPos(a.Team, ch) : W.Nests[a.Team].Pos;
        a.GoalLayer = (byte)(c.Founded ? NestLayer(a.Team) : 0);
        var to = site - a.Pos;
        // spread around the dig face rather than piling on one spot
        float d = to.Length();
        if (d < 1.4f) return Vector2.Zero;
        return to;
    }

    // ------------------------------------------------------------------ layers
    /// The layer this ant needs to be on for what it is doing.
    int WantLayer(ref Ant a)
    {
        switch (a.State)
        {
            case AntState.Forage: case AntState.Gather: case AntState.Return: return 0;
            case AntState.Move: case AntState.AttackMove: case AntState.Dig: return a.GoalLayer;
            case AntState.Engage: return Valid(a.Target, a.TargetSerial) ? Ants[a.Target].Layer : a.Layer;
            default: return a.Layer;
        }
    }

    /// Steers an ant through the nest entrance when its business is on another layer.
    /// Returns the (possibly replaced) desired direction.
    Vector2 Layering(ref Ant a, Vector2 desired)
    {
        int want = WantLayer(ref a);
        if (want == a.Layer) return desired;
        // the entrance that connects the two layers: the nest whose underground is involved
        int nestTeam = (want == 0 ? a.Layer : want) - 1;
        if (nestTeam < 0 || nestTeam > 1 || !Col[nestTeam].Founded) { a.GoalLayer = (byte)a.Layer; return desired; }
        var entrance = W.Nests[nestTeam].Pos;
        if (a.Pos.DistanceTo(entrance) < 1.5f)
        {
            a.Layer = (byte)want;
            a.Pos = entrance + MathX.Dir(a.Heading) * 0.3f;
            a.PrevPos = a.Pos;
            return desired;
        }
        return entrance - a.Pos;
    }

    /// Attackers with nothing left to fight at an enemy mound break in: down the shaft toward the queen.
    bool RaidNest(ref Ant a)
    {
        int foe = 1 - a.Team;
        if (!Col[foe].Alive || !Col[foe].Founded || Config.Flies(a.Type) && a.Alt > 1f) return false;
        if (a.Goal.DistanceTo(W.Nests[foe].Pos) > 8 || a.Pos.DistanceTo(W.Nests[foe].Pos) > 10) return false;
        if (Mode == GameMode.Lanes && QueenShielded(foe)) return false;
        a.Goal = ChamberPos(foe, Royal(foe));
        a.GoalLayer = (byte)NestLayer(foe);
        return true;
    }

    /// The queen's own walk: once the nest is founded she goes below and settles in the royal chamber.
    Vector2 QueenWalk(ref Ant a)
    {
        var c = Col[a.Team];
        if (!c.Founded) return Vector2.Zero;
        var royal = ChamberPos(a.Team, Royal(a.Team));
        if (a.Layer == 0)
        {
            var entrance = W.Nests[a.Team].Pos;
            if (a.Pos.DistanceTo(entrance) < 1.5f) { a.Layer = (byte)NestLayer(a.Team); a.PrevPos = a.Pos; Events.Add(new SimEvent { Kind = EvKind.QueenInside, Pos = entrance, Team = a.Team }); return Vector2.Zero; }
            return entrance - a.Pos;
        }
        var to = royal - a.Pos;
        return to.Length() < 0.8f ? Vector2.Zero : to;
    }

    /// Visual depth of a point on a nest's underground layer: zero at the shaft mouth, each tunnel
    /// sloping down to its chamber's depth. Rendering only.
    public float UndergroundDepth(int team, Vector2 p)
    {
        var c = Col[team];
        var local = p - W.Nests[team].Pos;
        float r = local.Length();
        if (c.Chambers.Count == 0 || r < 0.3f) return Mathf.Min(r / 0.3f, 1) * 0.5f;
        NestChamber best = null;
        float bestA = float.MaxValue;
        foreach (var ch in c.Chambers)
        {
            float da = Mathf.Abs(Mathf.Wrap(local.Angle() - ch.Offset.Angle(), -Mathf.Pi, Mathf.Pi));
            if (da < bestA) { bestA = da; best = ch; }
        }
        // a straight sloping gallery from the mouth to the room (matches the tunnel that is drawn)
        float reach = Mathf.Max(1.5f, best.Offset.Length());
        float t = Mathf.Clamp(r / reach, 0, 1);
        return best.Depth * t * Mathf.Max(0.18f, best.Progress) + 0.3f;
    }
}
