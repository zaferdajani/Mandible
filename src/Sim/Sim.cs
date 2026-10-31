using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

public struct Ant
{
    public bool Alive;
    public int Serial;
    public int Team;
    public AntType Type;
    public Vector2 Pos;
    public float Heading, Speed, Hp;
    public AntState State, Prev;
    public Vector2 Goal, Anchor;
    public int Target, TargetSerial;
    public bool Forced;
    public FoodSource FoodTarget, Assigned;
    public bool Carrying;
    public FoodKind CarryKind;
    public float Gait, Wander, Think, Deposit, Cooldown, AttackAnim, HitFlash, Stuck, DamagedT;
    public bool Selected;
    public float Height; // cached ground height for rendering
}

public enum EvKind { Bite, Death, Pickup, Deliver, Spawn, ProdDone, QueenHit, QueenDead, PopBlocked, UnderAttack, Shower }

public struct SimEvent
{
    public EvKind Kind;
    public Vector2 Pos;
    public int Team;
    public AntType Type;
    public ProdKind Prod;
}

public sealed class ProdItem
{
    public ProdKind Kind;
    public float Progress, Time;
}

public sealed class Colony
{
    public int Team;
    public float Food;
    public int PopUsed, PopCap = Config.StartPopCap;
    public readonly List<ProdItem> Queue = new();
    public Vector2 Rally;
    public bool HasRally;
    public int QueenId = -1;
    public bool Alive = true;
    public float FoodMult = 1f;
    public readonly int[] Count = new int[4];
    public float Gathered, Spent;
    public int Produced, Lost, Killed;
    public bool PopBlocked;
    public float QueenAlarm;

    public int PendingChambers() { int n = 0; foreach (var q in Queue) if (q.Kind == ProdKind.Chamber) n++; return n; }
}

public enum QueueResult { Ok, NoFood, QueueFull, MaxChambers, Dead }

public sealed class Sim
{
    public readonly World W;
    public readonly Pheromones Ph = new();
    public readonly SpatialHash Hash = new(Config.MaxAnts);
    public readonly Ant[] Ants = new Ant[Config.MaxAnts];
    public int High; // highest used index + 1
    readonly Stack<int> _free = new();
    public readonly Colony[] Col = { new() { Team = 0 }, new() { Team = 1 } };
    public readonly List<SimEvent> Events = new();
    public readonly Rng Rng = new(1234);
    public float Time;
    int _serial;
    public int Winner = -1;

    public Sim(World w)
    {
        W = w;
        foreach (var c in Col)
        {
            c.Food = Config.StartFood;
            var nest = W.Nests[c.Team].Pos;
            var toCenter = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - nest).Normalized();
            c.QueenId = Spawn(c.Team, AntType.Queen, nest + toCenter * 3.4f, toCenter.Angle());
            for (int i = 0; i < 18; i++) Spawn(c.Team, AntType.Worker, nest + Rng.InDisk(6), Rng.R(0, Mathf.Tau));
            for (int i = 0; i < 6; i++)
            {
                int id = Spawn(c.Team, AntType.Soldier, nest + toCenter * 8 + Rng.InDisk(4), toCenter.Angle());
                Ants[id].State = AntState.Idle;
            }
        }
    }

    public Vector2 NestEntrance(int team) => W.Nests[team].Pos;

    public bool Valid(int id, int serial) => id >= 0 && id < High && Ants[id].Alive && Ants[id].Serial == serial;

    public int Spawn(int team, AntType type, Vector2 p, float heading)
    {
        int id;
        if (_free.Count > 0) id = _free.Pop();
        else if (High < Config.MaxAnts) id = High++;
        else return -1;
        var st = Config.Of(type);
        Ants[id] = new Ant
        {
            Alive = true, Serial = ++_serial, Team = team, Type = type, Pos = p, Heading = heading, Hp = st.Hp,
            State = type == AntType.Worker ? AntState.Forage : AntState.Idle, Target = -1, Anchor = p, Think = Rng.F() * 0.3f,
            Gait = Rng.F(),
        };
        Col[team].Count[(int)type]++;
        return id;
    }

    // ------------------------------------------------------------------ commands
    public void CmdMove(List<int> ids, Vector2 p, bool attack)
    {
        float spacing = 0;
        foreach (int id in ids) spacing = Mathf.Max(spacing, Config.Of(Ants[id].Type).Size);
        spacing *= 0.95f;
        int k = 0;
        foreach (int id in ids)
        {
            ref var a = ref Ants[id];
            if (!a.Alive || a.Type == AntType.Queen) continue;
            var off = k == 0 ? Vector2.Zero : MathX.Dir(k * 2.39996f) * spacing * Mathf.Sqrt(k) * 0.9f;
            k++;
            a.Goal = ClampMap(p + off);
            a.State = attack ? AntState.AttackMove : AntState.Move;
            a.Forced = false;
            a.Target = -1;
            a.Assigned = null;
            a.Stuck = 0;
        }
    }

    public void CmdAttack(List<int> ids, int target)
    {
        foreach (int id in ids)
        {
            ref var a = ref Ants[id];
            if (!a.Alive || a.Type == AntType.Queen) continue;
            a.State = AntState.Engage;
            a.Prev = AntState.Idle;
            a.Target = target;
            a.TargetSerial = Ants[target].Serial;
            a.Forced = true;
            a.Anchor = Ants[target].Pos;
        }
    }

    public void CmdGather(List<int> ids, FoodSource f)
    {
        var others = new List<int>();
        foreach (int id in ids)
        {
            ref var a = ref Ants[id];
            if (!a.Alive) continue;
            if (a.Type != AntType.Worker) { others.Add(id); continue; }
            a.Assigned = f;
            a.FoodTarget = f;
            a.State = a.Carrying ? AntState.Return : AntState.Gather;
        }
        if (others.Count > 0) CmdMove(others, f.Pos, false);
    }

    public void CmdStop(List<int> ids, bool hold)
    {
        foreach (int id in ids)
        {
            ref var a = ref Ants[id];
            if (!a.Alive || a.Type == AntType.Queen) continue;
            a.Assigned = null;
            a.Forced = false;
            a.Anchor = a.Pos;
            a.State = a.Type == AntType.Worker && !hold ? AntState.Forage : (hold ? AntState.Hold : AntState.Idle);
        }
    }

    public QueueResult Enqueue(int team, ProdKind kind)
    {
        var c = Col[team];
        if (!c.Alive) return QueueResult.Dead;
        if (c.Queue.Count >= Config.QueueLimit) return QueueResult.QueueFull;
        if (kind == ProdKind.Chamber && c.PopCap + (c.PendingChambers() + 1) * Config.ChamberPop > Config.MaxPopCap) return QueueResult.MaxChambers;
        float cost = Config.ProdCost(kind);
        if (c.Food < cost) return QueueResult.NoFood;
        c.Food -= cost;
        c.Spent += cost;
        c.Queue.Add(new ProdItem { Kind = kind, Time = Config.ProdTime(kind) });
        return QueueResult.Ok;
    }

    public void CancelLast(int team)
    {
        var c = Col[team];
        if (c.Queue.Count == 0) return;
        var q = c.Queue[^1];
        c.Queue.RemoveAt(c.Queue.Count - 1);
        c.Food += Config.ProdCost(q.Kind);
        c.Spent -= Config.ProdCost(q.Kind);
    }

    // ------------------------------------------------------------------ tick
    public void Tick(float dt)
    {
        if (dt <= 0) return;
        Time += dt;
        Events.Clear();
        Hash.Clear();
        for (int i = 0; i < High; i++)
            if (Ants[i].Alive) Hash.Insert(i, Ants[i].Pos);

        foreach (var c in Col) TickColony(c, dt);
        TickShowers();
        for (int i = 0; i < High; i++)
            if (Ants[i].Alive) TickAnt(i, dt);
        Ph.Update(dt);
        TickCorpses(dt);
    }

    void TickColony(Colony c, float dt)
    {
        c.PopUsed = c.Count[0] * Config.Stats[0].Pop + c.Count[1] * Config.Stats[1].Pop + c.Count[2] * Config.Stats[2].Pop;
        c.QueenAlarm -= dt;
        if (!c.Alive || c.Queue.Count == 0) { c.PopBlocked = false; return; }
        var item = c.Queue[0];
        if (item.Kind != ProdKind.Chamber && c.PopUsed + Config.Stats[(int)item.Kind].Pop > c.PopCap)
        {
            if (!c.PopBlocked) Events.Add(new SimEvent { Kind = EvKind.PopBlocked, Team = c.Team });
            c.PopBlocked = true;
            return;
        }
        c.PopBlocked = false;
        item.Progress += dt;
        if (item.Progress < item.Time) return;
        c.Queue.RemoveAt(0);
        var nest = NestEntrance(c.Team);
        if (item.Kind == ProdKind.Chamber)
        {
            c.PopCap = Mathf.Min(Config.MaxPopCap, c.PopCap + Config.ChamberPop);
        }
        else
        {
            var type = (AntType)(int)item.Kind;
            int id = Spawn(c.Team, type, nest + Rng.InDisk(1.2f), Rng.R(0, Mathf.Tau));
            if (id >= 0)
            {
                c.Produced++;
                ref var a = ref Ants[id];
                if (type != AntType.Worker)
                {
                    var toCenter = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - nest).Normalized();
                    a.Goal = c.HasRally ? c.Rally + Rng.InDisk(3) : nest + toCenter * 10 + Rng.InDisk(6);
                    a.State = AntState.Move;
                }
                else if (c.HasRally && !W.Blocked(c.Rally, 0))
                {
                    var f = NearestFood(c.Rally, 12, null);
                    if (f != null) { a.Assigned = f; a.FoodTarget = f; a.State = AntState.Gather; }
                }
                Events.Add(new SimEvent { Kind = EvKind.Spawn, Pos = nest, Team = c.Team, Type = type });
            }
        }
        Events.Add(new SimEvent { Kind = EvKind.ProdDone, Pos = nest, Team = c.Team, Prod = item.Kind });
    }

    float _nextShower = 150;

    /// Crumbs fall from the picnic table above, always in mirrored pairs so neither side is favoured.
    void TickShowers()
    {
        if (Time < _nextShower) return;
        _nextShower = Time + Rng.R(70, 95);
        float S = Config.MapSize;
        for (int tries = 0; tries < 60; tries++)
        {
            var p = new Vector2(Rng.R(40, S - 40), Rng.R(40, S - 40));
            var m = new Vector2(S - p.X, S - p.Y);
            if (p.DistanceTo(m) < 30 || W.Blocked(p, 8) || W.Blocked(m, 8)) continue;
            if (p.DistanceTo(W.Nests[0].Pos) < 45 || p.DistanceTo(W.Nests[1].Pos) < 45) continue;
            int n = Rng.I(3, 6);
            for (int i = 0; i < n; i++)
            {
                var off = Rng.InDisk(5);
                float amount = Rng.R(35, 70), size = Rng.R(1.0f, 1.9f);
                if (W.Blocked(p + off, size) || W.Blocked(new Vector2(S, S) - (p + off), size)) continue;
                WorldBuilder.Spawn(W, Rng, FoodKind.Crumb, p + off, amount, size);
                WorldBuilder.Spawn(W, Rng, FoodKind.Crumb, new Vector2(S, S) - (p + off), amount, size);
            }
            Events.Add(new SimEvent { Kind = EvKind.Shower, Pos = p, Team = -1 });
            Events.Add(new SimEvent { Kind = EvKind.Shower, Pos = m, Team = -1 });
            return;
        }
    }

    void TickCorpses(float dt)
    {
        var list = W.Food;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var f = list[i];
            if (f.Kind == FoodKind.Corpse)
            {
                f.Age += dt;
                if (f.Age > 150) f.Amount = 0;
            }
            if (!f.Alive && f.Kind == FoodKind.Corpse) list.RemoveAt(i);
        }
    }

    // ------------------------------------------------------------------ queries
    public int FindEnemy(Vector2 p, int team, float radius)
    {
        int best = -1;
        float bestD = radius * radius;
        int r = (int)(radius / SpatialHash.Cell) + 1;
        int cx = (int)(p.X / SpatialHash.Cell), cz = (int)(p.Y / SpatialHash.Cell);
        for (int z = cz - r; z <= cz + r; z++)
        {
            if (z < 0 || z >= Hash.N) continue;
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || x >= Hash.N) continue;
                for (int j = Hash.Head[z * Hash.N + x]; j >= 0; j = Hash.Next[j])
                {
                    ref var b = ref Ants[j];
                    if (!b.Alive || b.Team == team) continue;
                    float d = p.DistanceSquaredTo(b.Pos);
                    // prefer fighters over workers, workers over the queen when at equal range
                    if (b.Type == AntType.Worker) d *= 1.3f;
                    if (d < bestD) { bestD = d; best = j; }
                }
            }
        }
        return best;
    }

    public FoodSource NearestFood(Vector2 p, float radius, FoodSource prefer)
    {
        FoodSource best = null;
        float bestD = radius * radius;
        foreach (var f in W.Food)
        {
            if (!f.Alive) continue;
            float rr = radius + f.Radius;
            float d = p.DistanceSquaredTo(f.Pos);
            if (d < rr * rr && d - f.Radius * f.Radius < bestD) { bestD = d - f.Radius * f.Radius; best = f; }
        }
        return best;
    }

    static Vector2 ClampMap(Vector2 p) => new(Mathf.Clamp(p.X, 6, Config.MapSize - 6), Mathf.Clamp(p.Y, 6, Config.MapSize - 6));

    // ------------------------------------------------------------------ ant brain
    void TickAnt(int i, float dt)
    {
        ref var a = ref Ants[i];
        var st = Config.Of(a.Type);
        a.Cooldown -= dt;
        a.Think -= dt;
        a.HitFlash = Mathf.Max(0, a.HitFlash - dt * 5);
        a.DamagedT -= dt;
        a.AttackAnim = Mathf.Max(0, a.AttackAnim - dt * 3.5f);
        Vector2 desired = Vector2.Zero;
        float speedMul = 1f;
        bool think = a.Think <= 0;
        if (think) a.Think = 0.2f + Rng.F() * 0.1f;

        if (a.Type == AntType.Queen)
        {
            if (think) a.Target = FindEnemy(a.Pos, a.Team, st.Reach + 2.5f);
            if (a.Target >= 0 && Ants[a.Target].Alive) TryBite(i, a.Target, st);
            a.Hp = Mathf.Min(st.Hp, a.Hp + dt * 5f);
            a.Height = W.HF.Sample(a.Pos.X, a.Pos.Y);
            a.Gait += dt * 0.05f;
            return;
        }

        switch (a.State)
        {
            case AntState.Forage:
            case AntState.Gather:
            case AntState.Return:
                desired = WorkerLogic(i, ref a, st, dt, think, ref speedMul);
                break;
            case AntState.Move:
                desired = a.Goal - a.Pos;
                if (desired.Length() < 0.9f)
                {
                    desired = Vector2.Zero;
                    a.Anchor = a.Pos;
                    a.State = a.Type == AntType.Worker ? AntState.Forage : AntState.Idle;
                }
                break;
            case AntState.AttackMove:
                if (think)
                {
                    int e = FindEnemy(a.Pos, a.Team, st.Aggro);
                    if (e >= 0) { Engage(ref a, e, AntState.AttackMove); break; }
                }
                desired = a.Goal - a.Pos;
                if (desired.Length() < 0.9f) { desired = Vector2.Zero; a.Anchor = a.Pos; a.State = AntState.Idle; }
                break;
            case AntState.Idle:
            case AntState.Hold:
                if (think)
                {
                    float r = a.State == AntState.Hold ? st.Reach + st.Size * 0.5f + 1f : st.Aggro;
                    if (a.Type == AntType.Worker) r = st.Aggro;
                    int e = FindEnemy(a.Pos, a.Team, r);
                    if (e >= 0)
                    {
                        if (a.State == AntState.Hold) { a.Target = e; a.TargetSerial = Ants[e].Serial; }
                        else { Engage(ref a, e, AntState.Idle); break; }
                    }
                }
                if (a.State == AntState.Hold && Valid(a.Target, a.TargetSerial)) TryBite(i, a.Target, st);
                // drift back to anchor if shoved
                if (a.State == AntState.Idle && a.Pos.DistanceTo(a.Anchor) > 2.5f) { desired = a.Anchor - a.Pos; speedMul = 0.6f; }
                break;
            case AntState.Engage:
                desired = EngageLogic(i, ref a, st, think);
                break;
        }

        Move(i, ref a, st, desired, speedMul, dt);
    }

    void Engage(ref Ant a, int e, AntState prev)
    {
        if (a.State != AntState.Engage) a.Prev = prev;
        if (prev == AntState.Idle) a.Anchor = a.Pos;
        a.State = AntState.Engage;
        a.Target = e;
        a.TargetSerial = Ants[e].Serial;
    }

    Vector2 EngageLogic(int i, ref Ant a, UnitStats st, bool think)
    {
        if (!Valid(a.Target, a.TargetSerial))
        {
            int e = FindEnemy(a.Pos, a.Team, st.Aggro);
            if (e >= 0 && !a.Forced) { a.Target = e; a.TargetSerial = Ants[e].Serial; }
            else
            {
                a.Forced = false;
                a.State = a.Prev == AntState.AttackMove ? AntState.AttackMove : (a.Type == AntType.Worker ? AntState.Forage : AntState.Idle);
                if (a.State == AntState.Idle && a.Pos.DistanceTo(a.Anchor) > 4) { a.Goal = a.Anchor; a.State = AntState.Move; }
                return Vector2.Zero;
            }
        }
        ref var t = ref Ants[a.Target];
        // leash for idle defenders
        if (!a.Forced && a.Prev == AntState.Idle && a.Pos.DistanceTo(a.Anchor) > st.Aggro * 2.2f)
        {
            a.Goal = a.Anchor; a.State = AntState.Move;
            return Vector2.Zero;
        }
        // switch to a closer threat now and then
        if (think && !a.Forced)
        {
            int e = FindEnemy(a.Pos, a.Team, st.Reach + 2f);
            if (e >= 0 && e != a.Target && Ants[e].Type != AntType.Queen) { a.Target = e; a.TargetSerial = Ants[e].Serial; }
        }
        float reach = st.Reach + Config.Of(t.Type).Size * 0.38f + st.Size * 0.3f;
        var to = t.Pos - a.Pos;
        if (to.Length() > reach) return to;
        a.Heading += MathX.AngleDiff(a.Heading, to.Angle()) * 0.3f;
        TryBite(i, a.Target, st);
        return Vector2.Zero;
    }

    void TryBite(int i, int target, UnitStats st)
    {
        ref var a = ref Ants[i];
        ref var t = ref Ants[target];
        float reach = st.Reach + Config.Of(t.Type).Size * 0.38f + st.Size * 0.3f + 0.3f;
        if (a.Pos.DistanceTo(t.Pos) > reach) return;
        if (a.Cooldown > 0) return;
        a.Cooldown = st.Cooldown * Rng.R(0.85f, 1.15f);
        a.AttackAnim = 1;
        Damage(target, st.Damage * Rng.R(0.85f, 1.15f), i);
        Events.Add(new SimEvent { Kind = EvKind.Bite, Pos = t.Pos, Team = a.Team, Type = a.Type });
    }

    public void Damage(int target, float dmg, int attacker)
    {
        ref var t = ref Ants[target];
        if (!t.Alive) return;
        t.Hp -= dmg;
        t.HitFlash = 1;
        t.DamagedT = 3f;
        if (t.Type == AntType.Queen)
        {
            Events.Add(new SimEvent { Kind = EvKind.QueenHit, Pos = t.Pos, Team = t.Team });
            Col[t.Team].QueenAlarm = 4f;
        }
        else Events.Add(new SimEvent { Kind = EvKind.UnderAttack, Pos = t.Pos, Team = t.Team });
        if (t.Hp <= 0) { Kill(target, attacker); return; }
        // retaliate
        if (attacker >= 0 && Ants[attacker].Alive)
        {
            if (t.Type != AntType.Worker && t.State == AntState.Idle)
                Engage(ref t, attacker, AntState.Idle);
            else if (t.Type == AntType.Worker && t.State != AntState.Engage && Rng.F() < 0.5f && Col[t.Team].Count[0] > 0)
            {
                // workers bite back only when cornered
                if (t.Pos.DistanceTo(Ants[attacker].Pos) < 1.6f) { t.Target = attacker; t.TargetSerial = Ants[attacker].Serial; }
            }
        }
    }

    void Kill(int id, int killer)
    {
        ref var t = ref Ants[id];
        t.Alive = false;
        var st = Config.Of(t.Type);
        var c = Col[t.Team];
        c.Count[(int)t.Type]--;
        c.Lost++;
        if (killer >= 0) Col[Ants[killer].Team].Killed++;
        W.Food.Add(new FoodSource
        {
            Id = -1, Kind = FoodKind.Corpse, Pos = t.Pos, Amount = st.CorpseFood, Max = st.CorpseFood, Radius = st.Size * 0.45f,
            Team = t.Team, AntType = t.Type, Heading = t.Heading,
        });
        Events.Add(new SimEvent { Kind = EvKind.Death, Pos = t.Pos, Team = t.Team, Type = t.Type });
        if (t.Type == AntType.Queen)
        {
            c.Alive = false;
            c.Queue.Clear();
            Events.Add(new SimEvent { Kind = EvKind.QueenDead, Pos = t.Pos, Team = t.Team });
            if (Winner < 0) Winner = 1 - t.Team;
        }
        _free.Push(id);
    }

    // ------------------------------------------------------------------ foraging
    static readonly float[] SensorAngles = { -0.6f, 0f, 0.6f };

    Vector2 WorkerLogic(int i, ref Ant a, UnitStats st, float dt, bool think, ref float speedMul)
    {
        // bite whoever is chewing on us
        if (a.Target >= 0 && Valid(a.Target, a.TargetSerial) && a.Pos.DistanceTo(Ants[a.Target].Pos) < 2f) TryBite(i, a.Target, st);

        var nest = NestEntrance(a.Team);
        // the colony swarms out to defend its queen
        if (think && Col[a.Team].QueenAlarm > 0 && a.Pos.DistanceTo(nest) < 26)
        {
            int e = FindEnemy(a.Pos, a.Team, 12);
            if (e >= 0) { a.Prev = AntState.Forage; a.State = AntState.Engage; a.Target = e; a.TargetSerial = Ants[e].Serial; a.Forced = false; return Vector2.Zero; }
        }
        if (a.State == AntState.Return)
        {
            a.Deposit -= dt;
            if (a.Deposit <= 0) { a.Deposit = 0.12f; Ph.Deposit(a.Team, a.Pos, 0.9f); }
            var to = nest - a.Pos;
            if (to.Length() < 1.8f)
            {
                a.Carrying = false;
                var c = Col[a.Team];
                float gain = (a.CarryKind == FoodKind.Berry || a.CarryKind == FoodKind.Sugar ? 4f : 3f) * c.FoodMult;
                c.Food += gain;
                c.Gathered += gain;
                Events.Add(new SimEvent { Kind = EvKind.Deliver, Pos = nest, Team = a.Team });
                a.Heading += Mathf.Pi;
                if (a.Assigned != null && a.Assigned.Alive) { a.FoodTarget = a.Assigned; a.State = AntState.Gather; }
                else { a.Assigned = null; a.FoodTarget = null; a.State = AntState.Forage; }
                return Vector2.Zero;
            }
            // path integration home with a little meander
            return to.Normalized() + MathX.Dir(a.Heading + Mathf.Sin(Time * 3 + i) * 0.4f) * 0.25f;
        }

        if (a.State == AntState.Gather && (a.FoodTarget == null || !a.FoodTarget.Alive))
        {
            a.FoodTarget = null;
            if (a.Assigned != null && !a.Assigned.Alive)
            {
                // assigned pile is gone: look for the next one nearby
                var next = NearestFood(a.Assigned.Pos, 14, null);
                a.Assigned = next;
                a.FoodTarget = next;
            }
            if (a.FoodTarget == null) a.State = AntState.Forage;
        }

        if (a.State == AntState.Forage && think)
        {
            var f = NearestFood(a.Pos, 8.5f, null);
            if (f != null) a.FoodTarget = f;
            else if (a.FoodTarget != null && !a.FoodTarget.Alive) a.FoodTarget = null;
        }

        if (a.FoodTarget != null && a.FoodTarget.Alive)
        {
            var f = a.FoodTarget;
            var to = f.Pos - a.Pos;
            if (to.Length() < f.Radius + 0.7f)
            {
                f.Amount -= 1;
                a.Carrying = true;
                a.CarryKind = f.Kind;
                a.State = AntState.Return;
                a.FoodTarget = null;
                if (a.Assigned == null && f.Amount > 3) a.Assigned = f;
                Ph.Deposit(a.Team, a.Pos, 3f);
                Events.Add(new SimEvent { Kind = EvKind.Pickup, Pos = f.Pos, Team = a.Team });
                return Vector2.Zero;
            }
            return to;
        }

        // pheromone following
        float best = 0.04f; int bi = -1;
        for (int s = 0; s < 3; s++)
        {
            var sp = a.Pos + MathX.Dir(a.Heading + SensorAngles[s]) * 3.2f;
            float v = Ph.Sample(a.Team, sp);
            if (v > best) { best = v; bi = s; }
        }
        a.Wander = Mathf.Clamp(a.Wander + Rng.R(-1, 1) * dt * 6, -1.4f, 1.4f);
        float h = a.Heading + a.Wander * dt * 2.2f;
        if (bi >= 0)
        {
            h += SensorAngles[bi] * Mathf.Min(1, dt * 10);
            // trails point both ways: prefer the direction that leads away from home
            var away = (a.Pos - nest).Normalized();
            if (MathX.Dir(h).Dot(away) < -0.3f && a.Pos.DistanceTo(nest) > 12) h += MathX.AngleDiff(h, away.Angle()) * dt * 1.5f;
        }
        var dir = MathX.Dir(h);
        float far = a.Pos.DistanceTo(nest);
        if (far > 150) dir = (dir + (nest - a.Pos).Normalized() * ((far - 150) / 40f)).Normalized();
        speedMul = 0.85f;
        return dir;
    }

    // ------------------------------------------------------------------ locomotion
    void Move(int i, ref Ant a, UnitStats st, Vector2 desired, float speedMul, float dt)
    {
        float radius = st.Size * 0.3f;
        float targetSpeed = 0;
        if (desired.LengthSquared() > 1e-6f)
        {
            float want = desired.Angle();
            var dest = a.Pos + desired; // when desired is a full vector this is the destination
            // look-ahead obstacle steering: slide along the rim of rocks
            var ahead = a.Pos + MathX.Dir(want) * (2.2f + radius);
            foreach (var o in W.ObstaclesNear(ahead))
            {
                float rr = o.R + radius + 0.4f;
                if (o.R <= 0 || dest.DistanceTo(o.P) < o.R + 1.5f) continue; // heading for this very thing
                if (ahead.DistanceSquaredTo(o.P) < rr * rr)
                {
                    var n = (a.Pos - o.P).Normalized();
                    var tangent = new Vector2(-n.Y, n.X);
                    if (tangent.Dot(MathX.Dir(want)) < 0) tangent = -tangent;
                    want = (tangent * 1.0f + n * 0.25f).Angle();
                    break;
                }
            }
            float diff = MathX.AngleDiff(a.Heading, want);
            float turn = (a.Type == AntType.Major ? 5f : 8f) * dt;
            a.Heading += Mathf.Clamp(diff, -turn, turn);
            targetSpeed = st.Speed * speedMul * (Mathf.Abs(diff) > 1.3f ? 0.35f : 1f);
        }
        a.Speed = Mathf.MoveToward(a.Speed, targetSpeed, st.Speed * dt * 6);
        var pos = a.Pos + MathX.Dir(a.Heading) * a.Speed * dt;

        // separation
        Vector2 push = Vector2.Zero;
        int cx = (int)(a.Pos.X / SpatialHash.Cell), cz = (int)(a.Pos.Y / SpatialHash.Cell);
        int seen = 0;
        for (int z = cz - 1; z <= cz + 1 && seen < 10; z++)
        {
            if (z < 0 || z >= Hash.N) continue;
            for (int x = cx - 1; x <= cx + 1 && seen < 10; x++)
            {
                if (x < 0 || x >= Hash.N) continue;
                for (int j = Hash.Head[z * Hash.N + x]; j >= 0 && seen < 10; j = Hash.Next[j])
                {
                    if (j == i) continue;
                    ref var b = ref Ants[j];
                    if (!b.Alive) continue;
                    float rb = Config.Of(b.Type).Size * 0.3f;
                    float min = radius + rb;
                    var d = pos - b.Pos;
                    float l2 = d.LengthSquared();
                    if (l2 >= min * min) continue;
                    seen++;
                    float l = Mathf.Sqrt(l2) + 1e-4f;
                    float w = b.Type == AntType.Queen ? 1f : (Config.Of(b.Type).Size / (Config.Of(b.Type).Size + st.Size));
                    if (l2 < 1e-6f) d = MathX.Dir(i * 1.7f);
                    push += d / l * (min - l) * w;
                }
            }
        }
        pos += push * 0.6f;

        // hard obstacle resolution
        foreach (var o in W.ObstaclesNear(pos))
        {
            if (o.R <= 0) continue;
            float rr = o.R + radius;
            var d = pos - o.P;
            float l = d.Length();
            if (l < rr && l > 1e-4f) pos = o.P + d / l * rr;
        }
        pos = ClampMap(pos);
        float moved = pos.DistanceTo(a.Pos);
        a.Pos = pos;
        a.Gait = (a.Gait + moved / (0.55f * st.Size)) % 1f;
        a.Height = W.HF.Sample(pos.X, pos.Y);
    }
}
