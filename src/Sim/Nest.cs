using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The rooms of an ant nest. Real colonies keep brood shallow where the soil is warm, stores at
/// mid depth and the queen deepest; every room is dug by workers, not conjured, and every room
/// hangs off the tunnel network that starts at the entrance.
public enum ChamberKind : byte { Royal, Brood, Storage, Forge, Armory, Wings, Silk }

public sealed class NestChamber
{
    public int Id;
    public ChamberKind Kind;
    public int Tier = 1;          // royal / forge / armory grow with their upgrades
    public Vector2 Offset;        // from the nest centre, in map units (the underground is a second 2D layer)
    public float Depth;           // visual depth below the surface
    public int Parent = -1;       // the node its gallery is dug from: another chamber's id, or -1 for the entrance
    public float Radius = 1.6f;   // walkable floor radius
    public float Work, Done;      // worker-seconds needed and spent on the current dig
    public bool Built;            // usable; an expansion keeps Built while Done climbs again
    public int Diggers;           // workers currently digging here (recounted every tick)
    public float Progress => Work <= 0 ? 1 : Mathf.Clamp(Done / Work, 0, 1);
    public bool Digging => Done < Work;
}

public sealed partial class Sim
{
    public const float MaxTunnel = 12f;           // a new room must lie within this of a node it can be dug from
    public const float NestReachMax = 42f;        // how far from the entrance the network may grow
    public const float RoomSpacing = 3.6f;        // rooms never overlap
    public const int MaxDiggers = 4;              // per chamber
    public const float FoundingWork = 150f;       // worker-seconds to dig the shaft and royal chamber
    public const float BroodWork = 90f, StorageWork = 80f, TunnelWorkPerUnit = 5f;
    public const float BroodCost = 100f, StorageCost = 80f;
    public const int MaxStorages = 6;

    /// The underground layer that belongs to a colony (0 is the surface).
    public static int NestLayer(int team) => team + 1;

    public NestChamber Royal(int team) => Col[team].Chambers.Count > 0 ? Col[team].Chambers[0] : null;

    /// World position of a chamber (its layer is the colony's nest layer).
    public Vector2 ChamberPos(int team, NestChamber ch) => W.Nests[team].Pos + ch.Offset;

    /// World position of a network node: a chamber id, or -1 for the entrance.
    public Vector2 NodePos(int team, int id) => id < 0 ? W.Nests[team].Pos : ChamberPos(team, Col[team].Chambers[id]);

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

    /// How far the network reaches from the entrance (for the glass and the camera).
    public float NestReach(int team)
    {
        float r = 14f;
        foreach (var ch in Col[team].Chambers) r = Mathf.Max(r, ch.Offset.Length() + 7f);
        return r;
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

    /// Each kind of room has its natural depth (brood shallow and warm, the queen deepest).
    public static float DepthOf(ChamberKind k) => k switch
    {
        ChamberKind.Royal => 11f, ChamberKind.Brood => 3.5f, ChamberKind.Storage => 7f, ChamberKind.Wings => 5f, ChamberKind.Silk => 6f, _ => 8.5f,
    };

    static float RadiusOf(ChamberKind k) => k switch { ChamberKind.Royal => 2.2f, ChamberKind.Storage => 1.7f, _ => 1.5f };

    /// A sensible default spot for a room (the AI and the founding use it): fans out around the shaft.
    public Vector2 DefaultSpot(int team, ChamberKind k)
    {
        var nest = W.Nests[team].Pos;
        var away = (nest - new Vector2(Config.MapSize, Config.MapSize) * 0.5f).Normalized();
        float baseAngle = away.AngleD();
        int index = ChamberCount(team, k, false);
        float ang, r;
        switch (k)
        {
            case ChamberKind.Royal: ang = baseAngle; r = 7f; break;
            case ChamberKind.Brood: ang = baseAngle + (index % 2 == 0 ? 1 : -1) * (1.25f + 0.55f * (index / 2)); r = 5.5f + 0.9f * (index / 2); break;
            case ChamberKind.Storage: ang = baseAngle + DMath.Pi + (index % 2 == 0 ? 0.45f : -0.45f) * (1 + index / 2); r = 6.5f + 1.2f * (index / 2); break;
            case ChamberKind.Forge: ang = baseAngle + 2.45f; r = 8.5f; break;
            case ChamberKind.Armory: ang = baseAngle - 2.45f; r = 8.5f; break;
            case ChamberKind.Wings: ang = baseAngle + 0.7f; r = 9.5f; break;
            default: ang = baseAngle - 0.7f; r = 9.5f; break;
        }
        var p = nest + MathX.Dir(ang) * r;
        // slide out of the way of rooms already there
        for (int tries = 0; tries < 12 && !Clear(team, p); tries++) p += MathX.Dir(ang + 1.7f * tries) * 2.5f;
        return p;
    }

    bool Clear(int team, Vector2 p)
    {
        foreach (var ch in Col[team].Chambers) if (ChamberPos(team, ch).DistanceTo(p) < RoomSpacing) return false;
        return p.DistanceTo(W.Nests[team].Pos) >= 3f;
    }

    /// The node (chamber id or -1) a room at p would be dug from: the nearest one within tunnel reach.
    int ConnectFrom(int team, Vector2 p, bool builtOnly)
    {
        int best = -1;
        float bestD = p.DistanceTo(W.Nests[team].Pos);
        foreach (var ch in Col[team].Chambers)
        {
            if (builtOnly && !ch.Built) continue;
            float d = ChamberPos(team, ch).DistanceTo(p);
            if (d < bestD) { bestD = d; best = ch.Id; }
        }
        return bestD <= MaxTunnel ? best : int.MinValue;
    }

    NestChamber AddChamber(int team, ChamberKind k, float work, bool built, Vector2? at = null)
    {
        var c = Col[team];
        var pos = at ?? DefaultSpot(team, k);
        int parent = c.Chambers.Count == 0 ? -1 : ConnectFrom(team, pos, false);
        if (parent == int.MinValue) parent = -1; // the founding rooms always hang off the entrance
        float tunnel = pos.DistanceTo(NodePos(team, parent));
        var ch = new NestChamber
        {
            Id = c.Chambers.Count, Kind = k, Offset = pos - W.Nests[team].Pos, Depth = DepthOf(k), Parent = parent, Radius = RadiusOf(k),
            Work = work + (built ? 0 : tunnel * TunnelWorkPerUnit), Built = built,
        };
        ch.Done = built ? ch.Work : 0;
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
        if (c.QueenId >= 0 && Ants[c.QueenId].Alive)
        {
            ref var q = ref Ants[c.QueenId];
            q.Layer = (byte)NestLayer(team);
            q.Pos = q.PrevPos = ChamberPos(team, Royal(team));
            q.Anchor = q.Pos;
        }
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

    /// Brood chambers set how many ants the colony can raise; stores set how much food it can keep.
    void RecountCapacity(Colony c)
    {
        int brood = ChamberCount(c.Team, ChamberKind.Brood, true);
        int stores = ChamberCount(c.Team, ChamberKind.Storage, true);
        c.PopCap = c.Founded ? Mathf.Min(Config.MaxPopCap, Config.StartPopCap + Config.ChamberPop * brood) : Config.FoundingPop;
        c.FoodCap = Mode == GameMode.Lanes ? 1e9f : Config.BaseFoodCap + Config.StorageFoodCap * stores;
    }

    // ------------------------------------------------------------------ dig orders
    public enum DigResult { Ok, NoFood, NotFounded, TooMany, Busy, Dead, TooFar, Overlap, Exists }

    /// May a room of this kind be dug at all (food, counts, founding)? Position rules are in PlaceCheck.
    public DigResult CanDig(int team, ChamberKind k)
    {
        var c = Col[team];
        if (!c.Alive) return DigResult.Dead;
        if (Mode == GameMode.Lanes) return DigResult.Busy;
        if (!c.Founded) return DigResult.NotFounded;
        if (k == ChamberKind.Brood && c.PopCap + Config.ChamberPop * (1 + CountDigging(team, k)) > Config.MaxPopCap + Config.ChamberPop) return DigResult.TooMany;
        if (k == ChamberKind.Storage && ChamberCount(team, k, false) >= MaxStorages) return DigResult.TooMany;
        if (k is not (ChamberKind.Brood or ChamberKind.Storage))
        {
            if (ChamberOf(team, k) != null) return DigResult.Exists; // upgrade rooms are enlarged in place by research
            return DigResult.Ok; // the research order carries the food check
        }
        if (c.Food < (k == ChamberKind.Brood ? BroodCost : StorageCost)) return DigResult.NoFood;
        return DigResult.Ok;
    }

    /// Settlers rule: a room must sit within tunnel reach of an existing node and clear of other rooms.
    public DigResult PlaceCheck(int team, Vector2 p)
    {
        if (p.DistanceTo(W.Nests[team].Pos) > NestReachMax) return DigResult.TooFar;
        if (!Clear(team, p)) return DigResult.Overlap;
        if (ConnectFrom(team, p, false) == int.MinValue) return DigResult.TooFar;
        return DigResult.Ok;
    }

    public static string DigMessage(DigResult r, ChamberKind k) => r switch
    {
        DigResult.NoFood => "Not enough food",
        DigResult.NotFounded => "The nest is still being dug",
        DigResult.TooMany => k == ChamberKind.Brood ? "The nest cannot hold more brood chambers" : $"At most {MaxStorages} stores",
        DigResult.TooFar => $"Too far from the tunnels: a room must be within {MaxTunnel:0} of the entrance or another room",
        DigResult.Overlap => "Too close to another room",
        DigResult.Exists => "That room exists: its upgrade enlarges it in place",
        _ => "Cannot dig there",
    };

    int CountDigging(int team, ChamberKind k)
    {
        int n = 0;
        foreach (var ch in Col[team].Chambers) if (ch.Kind == k && !ch.Built) n++;
        return n;
    }

    /// Orders a new brood chamber or store at p: pays the food and opens the dig; workers are sent on the next tick.
    public DigResult Dig(int team, ChamberKind k, Vector2 p)
    {
        var r = CanDig(team, k);
        if (r != DigResult.Ok) return r;
        r = PlaceCheck(team, p);
        if (r != DigResult.Ok) return r;
        var c = Col[team];
        float cost = k == ChamberKind.Brood ? BroodCost : StorageCost;
        c.Food -= cost;
        c.Spent += cost;
        AddChamber(team, k, k == ChamberKind.Brood ? BroodWork : StorageWork, false, p);
        return DigResult.Ok;
    }

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
            for (int i = 0; i < High; i++)
            {
                ref var a = ref Ants[i];
                if (!a.Alive || a.Team != c.Team || a.DigP1 == 0) continue;
                if (a.DigP1 > c.Chambers.Count) { a.DigP1 = 0; continue; }
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
        a.Goal = site;
        var to = site - a.Pos;
        if (to.Length() < 1.4f) return Vector2.Zero;
        return to;
    }

    // ------------------------------------------------------------------ the tunnel network
    /// The network node nearest to a point: returns the chamber id whose gallery (parent -> room) or floor
    /// the point is closest to, the nearest point on it, and how far along the gallery it lies (0 parent, 1 room).
    int NearestOnNetwork(int team, Vector2 p, out Vector2 nearest, out float along)
    {
        var c = Col[team];
        var entrance = W.Nests[team].Pos;
        nearest = entrance;
        along = 0;
        int best = -1;
        float bestD = p.DistanceTo(entrance) - 1.4f; // the shaft mouth is walkable
        foreach (var ch in c.Chambers)
        {
            var a = NodePos(team, ch.Parent);
            var b = ChamberPos(team, ch);
            var ab = b - a;
            float len2 = ab.LengthSquared();
            float t = len2 < 1e-4f ? 1 : Mathf.Clamp((p - a).Dot(ab) / len2, 0, 1);
            var q = a + ab * t;
            float d = p.DistanceTo(q) - (t > 0.98f ? ch.Radius : 0.9f);
            if (d < bestD) { bestD = d; best = ch.Id; nearest = q; along = t; }
        }
        return best;
    }

    /// Keeps an underground ant on the galleries and floors.
    Vector2 ClampToNetwork(int team, Vector2 p)
    {
        var c = Col[team];
        var entrance = W.Nests[team].Pos;
        float slack = 0.9f;
        if (p.DistanceTo(entrance) <= 1.4f) return p;
        foreach (var ch in c.Chambers)
            if (ChamberPos(team, ch).DistanceTo(p) <= ch.Radius) return p;
        int id = NearestOnNetwork(team, p, out var q, out _);
        float allowed = id < 0 ? 1.4f : slack;
        var d = p - q;
        float l = d.Length();
        return l <= allowed ? p : q + d / l * allowed;
    }

    /// Which node an ant should head for next to reach `goal` through the tunnels (a tree rooted at the entrance).
    public Vector2 RouteStep(int team, Vector2 from, Vector2 goal)
    {
        var c = Col[team];
        int here = NearestOnNetwork(team, from, out _, out float alongHere);
        int there = NearestOnNetwork(team, goal, out var goalPoint, out float alongThere);
        if (here == there) return goal; // same gallery or room: walk straight
        // the nodes from the goal up to the entrance (its room first when the goal lies on the far half of a gallery)
        var upThere = Chain(c, there >= 0 && alongThere > 0.5f ? there : there >= 0 ? c.Chambers[there].Parent : -1);
        // standing on a gallery between its parent and its room
        if (here >= 0)
        {
            int room = here, parent = c.Chambers[here].Parent;
            bool atRoom = alongHere > 0.97f;
            // the goal is in this room or beyond it: go down to the room, then on toward the goal
            if (upThere.Contains(room))
            {
                if (!atRoom) return NodePos(team, room);
                int idx = upThere.IndexOf(room);
                return idx > 0 ? NodePos(team, upThere[idx - 1]) : goalPoint;
            }
            // otherwise the way out is up the gallery to the parent
            if (!atRoom && alongHere > 0.03f) return NodePos(team, parent);
            if (atRoom) return NodePos(team, parent);
            here = parent; // we stand at the parent node itself
        }
        // at a node: climb the chain until it meets the goal's, then step down the other branch
        var upHere = Chain(c, here);
        int joinHere = -1, joinThere = -1;
        for (int i = 0; i < upHere.Count && joinHere < 0; i++)
            for (int j = 0; j < upThere.Count; j++)
                if (upHere[i] == upThere[j]) { joinHere = i; joinThere = j; break; }
        if (joinHere < 0) return goal;
        if (joinHere > 0) return NodePos(team, upHere[1]);
        return joinThere > 0 ? NodePos(team, upThere[joinThere - 1]) : goalPoint;
    }

    /// Nodes from a node up to the entrance (-1), the node itself first.
    static List<int> Chain(Colony c, int node)
    {
        var list = new List<int>();
        int p = node;
        while (p >= 0) { list.Add(p); p = c.Chambers[p].Parent; }
        list.Add(-1);
        return list;
    }

    // ------------------------------------------------------------------ layers
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

    /// Steers an ant through the nest entrance when its business is on another layer, and along the
    /// galleries while it is below. Returns the (possibly replaced) desired direction.
    Vector2 Layering(ref Ant a, Vector2 desired)
    {
        int want = WantLayer(ref a);
        if (want != a.Layer)
        {
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
            desired = entrance - a.Pos;
        }
        if (a.Layer > 0 && desired.LengthSquared() > 1e-4f)
        {
            // below ground every journey follows the galleries
            var goal = a.Pos + desired;
            var step = RouteStep(a.Layer - 1, a.Pos, goal);
            if (step.DistanceTo(a.Pos) > 0.5f) desired = step - a.Pos;
        }
        return desired;
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
        if (to.Length() < 0.8f) return Vector2.Zero;
        var step = RouteStep(a.Team, a.Pos, royal);
        return step - a.Pos;
    }

    /// Visual depth of a point on a nest's underground layer: galleries slope from their parent node's
    /// depth to their room's depth; the entrance is at zero. Rendering only.
    public float UndergroundDepth(int team, Vector2 p)
    {
        var c = Col[team];
        if (c.Chambers.Count == 0) return 0.3f;
        int id = NearestOnNetwork(team, p, out _, out float along);
        if (id < 0) return 0.3f + Mathf.Min(1f, p.DistanceTo(W.Nests[team].Pos) / 1.4f) * 0.5f;
        var ch = c.Chambers[id];
        float parentDepth = ch.Parent < 0 ? 0.3f : c.Chambers[ch.Parent].Depth;
        float roomDepth = Mathf.Lerp(parentDepth, ch.Depth, Mathf.Max(0.18f, ch.Progress));
        return Mathf.Lerp(parentDepth, roomDepth, along);
    }
}
