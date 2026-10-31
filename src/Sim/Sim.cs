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
    public int Level;
    public float Xp, MaxHp, Size, DmgMul;
    public float Alt;        // height above ground (flyers)
    public float Root;       // seconds left stuck in spider silk
    public float AbilityCd;  // special attack cooldown
    public int Forge;        // mandible-forge tier this warrior hatched with
    public float SpeedMul;
    public Vector2 PrevPos;           // state at the previous tick, for smooth rendering between ticks
    public float PrevHeading, PrevAlt;
    public int HeroTier;              // queen tier a hero hatched under
    public float BuffT, BuffMul;      // war cry
    public float PoisonT, PoisonDps;  // acid
    public int PoisonSrc;
}

public enum EvKind { Bite, Death, Pickup, Deliver, Spawn, ProdDone, QueenHit, QueenDead, PopBlocked, UnderAttack, Shower, LevelUp, WebShot, WebLand, Sweep, Sting, ResearchDone, Spit, WarCry, AcidBurst, DiveBomb }

public struct SimEvent
{
    public EvKind Kind;
    public Vector2 Pos, To;
    public int Team;
    public AntType Type;
    public ProdKind Prod;
    public Upgrade Upgrade;
    public int Tier;
    public float Radius;
}

public sealed class WebZone
{
    public Vector2 Pos;
    public float R, Life, Max, Root;
}

sealed class WebShotFx
{
    public Vector2 From, To;
    public float T, Dur, R, Life, Root;
}

public enum ResearchResult { Ok, NoFood, Locked, Maxed, Busy, Dead }

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
    public readonly int[] Count = new int[13];
    public float Gathered, Spent;
    public int Produced, Lost, Killed;
    public bool PopBlocked;
    public float QueenAlarm;
    // research: the queen starts at tier 1, everything else at 0
    public readonly int[] Tier = { 1, 0, 0, 0, 0 };
    public bool Researching;
    public Upgrade ResearchKind;
    public float ResearchProgress, ResearchTime;

    public int T(Upgrade u) => Tier[(int)u];

    public int PendingChambers() { int n = 0; foreach (var q in Queue) if (q.Kind == ProdKind.Chamber) n++; return n; }
}

public enum QueueResult { Ok, NoFood, QueueFull, MaxChambers, Dead, Locked, HeroExists }

public sealed class Sim
{
    public readonly World W;
    public readonly Pheromones Ph = new();
    public readonly SpatialHash Hash = new(Config.MaxAnts);
    public readonly Ant[] Ants = new Ant[Config.MaxAnts];
    public int High; // highest used index + 1
    readonly Stack<int> _free = new();
    public readonly Colony[] Col = { new() { Team = 0 }, new() { Team = 1 } };
    public int WildCount;
    public readonly List<SimEvent> Events = new();
    public readonly Rng Rng = new(1234);
    public float Time;
    public int Ticks;
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
            c.QueenId = Spawn(c.Team, AntType.Queen, nest + toCenter * 3.4f, toCenter.AngleD());
            for (int i = 0; i < 18; i++) Spawn(c.Team, AntType.Worker, nest + Rng.InDisk(6), Rng.R(0, DMath.Tau));
            for (int i = 0; i < 6; i++)
            {
                int id = Spawn(c.Team, AntType.Soldier, nest + toCenter * 8 + Rng.InDisk(4), toCenter.AngleD());
                Ants[id].State = AntState.Idle;
            }
        }
        foreach (var camp in W.Camps)
            for (int i = 0; i < camp.Bugs.Length; i++)
            {
                var (type, level) = camp.Bugs[i];
                var off = camp.Bugs.Length == 1 ? Vector2.Zero : MathX.Dir(camp.Facing + i * DMath.Tau / camp.Bugs.Length) * 3.5f;
                int id = Spawn(Config.Wild, type, camp.Pos + off, camp.Facing + DMath.Pi, level);
                Ants[id].State = AntState.Idle;
            }
    }

    void ApplyLevel(ref Ant a)
    {
        var st = Config.Of(a.Type);
        float frac = a.MaxHp > 0 ? a.Hp / a.MaxHp : 1;
        a.MaxHp = st.Hp * Config.HpMul(a.Type, a.Level);
        a.DmgMul = Config.DmgMul(a.Type, a.Level);
        a.Size = st.Size * Config.SizeMul(a.Type, a.Level);
        a.SpeedMul = 1;
        if (a.Team < 2 && Config.IsWarrior(a.Type))
        {
            // forge: bigger, harder-biting, slower; chitin: tougher for everyone
            a.MaxHp *= (1 + 0.12f * a.Forge) * (1 + 0.18f * Col[a.Team].T(Upgrade.Chitin));
            a.DmgMul *= 1 + 0.22f * a.Forge;
            a.Size *= 1 + 0.1f * a.Forge;
            a.SpeedMul = 1 - 0.08f * a.Forge;
        }
        if (Config.IsHero(a.Type))
        {
            float m = Config.HeroTierMul(a.Type, a.HeroTier);
            a.MaxHp *= m;
            a.DmgMul *= m;
        }
        if (a.Type == AntType.Queen && a.Team < 2)
        {
            int q = Col[a.Team].T(Upgrade.Queen) - 1;
            a.MaxHp *= 1 + 0.3f * q;
            a.DmgMul *= 1 + 0.25f * q;
            a.Size *= 1 + 0.12f * q;
        }
        a.Hp = a.MaxHp * frac;
    }

    // ------------------------------------------------------------------ research
    public ResearchResult CanResearch(int team, Upgrade u)
    {
        var c = Col[team];
        if (!c.Alive) return ResearchResult.Dead;
        int next = c.T(u) + 1;
        if (next > Config.MaxTier(u)) return ResearchResult.Maxed;
        if (c.T(Upgrade.Queen) < Config.QueenNeeded(u, next)) return ResearchResult.Locked;
        if (c.Researching) return ResearchResult.Busy;
        if (c.Food < Config.ResearchCost(u, next)) return ResearchResult.NoFood;
        return ResearchResult.Ok;
    }

    public ResearchResult StartResearch(int team, Upgrade u)
    {
        var r = CanResearch(team, u);
        if (r != ResearchResult.Ok) return r;
        var c = Col[team];
        int next = c.T(u) + 1;
        float cost = Config.ResearchCost(u, next);
        c.Food -= cost;
        c.Spent += cost;
        c.Researching = true;
        c.ResearchKind = u;
        c.ResearchProgress = 0;
        c.ResearchTime = Config.ResearchTime(u, next);
        return ResearchResult.Ok;
    }

    public void CancelResearch(int team)
    {
        var c = Col[team];
        if (!c.Researching) return;
        float cost = Config.ResearchCost(c.ResearchKind, c.T(c.ResearchKind) + 1);
        c.Food += cost;
        c.Spent -= cost;
        c.Researching = false;
    }

    void TickResearch(Colony c, float dt)
    {
        if (!c.Researching || !c.Alive) return;
        c.ResearchProgress += dt;
        if (c.ResearchProgress < c.ResearchTime) return;
        c.Researching = false;
        var u = c.ResearchKind;
        c.Tier[(int)u]++;
        // chitin hardens every warrior; royal jelly grows the queen
        if (u == Upgrade.Chitin || u == Upgrade.Queen)
            for (int i = 0; i < High; i++)
            {
                ref var a = ref Ants[i];
                if (a.Alive && a.Team == c.Team && (Config.IsWarrior(a.Type) || a.Type == AntType.Queen)) ApplyLevel(ref a);
            }
        Events.Add(new SimEvent { Kind = EvKind.ResearchDone, Pos = W.Nests[c.Team].Pos, Team = c.Team, Upgrade = u, Tier = c.T(u) });
    }

    void GrantXp(int id, float xp)
    {
        ref var a = ref Ants[id];
        int max = Config.MaxLevelOf(a.Type);
        if (!a.Alive || !Config.IsWarrior(a.Type) || a.Level >= max) return;
        a.Xp += xp;
        while (a.Level < max && a.Xp >= Config.XpFor(a.Type, a.Level + 1))
        {
            a.Level++;
            ApplyLevel(ref a);
            a.Hp = Mathf.Min(a.MaxHp, a.Hp + a.MaxHp * 0.35f); // promotion heals
            Events.Add(new SimEvent { Kind = EvKind.LevelUp, Pos = a.Pos, Team = a.Team, Type = a.Type });
        }
    }

    /// XP needed for the next level (0 when maxed).
    public static float NextXp(in Ant a) => a.Level >= Config.MaxLevelOf(a.Type) ? 0 : Config.XpFor(a.Type, a.Level + 1);

    public Vector2 NestEntrance(int team) => W.Nests[team].Pos;

    public bool Valid(int id, int serial) => id >= 0 && id < High && Ants[id].Alive && Ants[id].Serial == serial;

    public int Spawn(int team, AntType type, Vector2 p, float heading, int level = 1)
    {
        int id;
        if (_free.Count > 0) id = _free.Pop();
        else if (High < Config.MaxAnts) id = High++;
        else return -1;
        Ants[id] = new Ant
        {
            Alive = true, Serial = ++_serial, Team = team, Type = type, Pos = p, Heading = heading,
            State = type == AntType.Worker ? AntState.Forage : AntState.Idle, Target = -1, Anchor = p, Think = Rng.F() * 0.3f,
            Gait = Rng.F(), Level = level, Xp = Config.XpFor(type, level), BuffMul = 1,
            HeroTier = team < 2 && Config.IsHero(type) ? Col[team].T(Upgrade.Queen) : 0,
            Forge = team < 2 && Config.IsWarrior(type) ? Col[team].T(Upgrade.Mandibles) : 0,
            Alt = Config.CruiseAlt(type), AbilityCd = Rng.R(1, 3),
        };
        ApplyLevel(ref Ants[id]);
        Ants[id].Hp = Ants[id].MaxHp;
        Ants[id].PrevPos = p; Ants[id].PrevHeading = heading; Ants[id].PrevAlt = Ants[id].Alt;
        if (team < 2) Col[team].Count[(int)type]++;
        else WildCount++;
        return id;
    }

    // ------------------------------------------------------------------ commands
    public void CmdMove(List<int> ids, Vector2 p, bool attack)
    {
        float spacing = 0;
        foreach (int id in ids) spacing = Mathf.Max(spacing, Ants[id].Size);
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
        var r = CanEnqueue(team, kind);
        if (r != QueueResult.Ok) return r;
        var c = Col[team];
        float cost = Config.ProdCost(kind);
        c.Food -= cost;
        c.Spent += cost;
        c.Queue.Add(new ProdItem { Kind = kind, Time = Config.ProdTime(kind) });
        return QueueResult.Ok;
    }

    /// The same checks as Enqueue without changing anything (multiplayer predicts results before sending).
    public QueueResult CanEnqueue(int team, ProdKind kind)
    {
        var c = Col[team];
        if (!c.Alive) return QueueResult.Dead;
        if (!Config.Unlocked(kind, c.Tier)) return QueueResult.Locked;
        if (Config.IsHeroKind(kind) && (c.Count[(int)Config.UnitOf(kind)] > 0 || c.Queue.Exists(q => q.Kind == kind))) return QueueResult.HeroExists;
        if (c.Queue.Count >= Config.QueueLimit) return QueueResult.QueueFull;
        if (kind == ProdKind.Chamber && c.PopCap + (c.PendingChambers() + 1) * Config.ChamberPop > Config.MaxPopCap) return QueueResult.MaxChambers;
        if (c.Food < Config.ProdCost(kind)) return QueueResult.NoFood;
        return QueueResult.Ok;
    }

    /// Food piles shrink as they are eaten; their collision shrinks with them, on the tick (never per frame).
    public static float FoodScale(FoodSource f) => Mathf.Lerp(0.35f, 1f, Mathf.Sqrt(Mathf.Max(0, f.Amount) / f.Max));

    static void ShrinkBlock(FoodSource f)
    {
        if (f.Block == null) return;
        f.Block.R = f.Alive ? f.Block.BaseR * FoodScale(f) : 0;
    }

    /// Hash of the whole simulation state; peers compare it to detect a desync.
    public uint Checksum()
    {
        uint h = 2166136261u;
        void Mix(uint v) { h = (h ^ v) * 16777619u; }
        uint F(float v) => System.BitConverter.SingleToUInt32Bits(v);
        Mix((uint)Ticks);
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (!a.Alive) continue;
            Mix((uint)i); Mix(F(a.Pos.X)); Mix(F(a.Pos.Y)); Mix(F(a.Hp)); Mix(F(a.Heading)); Mix((uint)a.State); Mix((uint)a.Level);
        }
        foreach (var c in Col) { Mix(F(c.Food)); Mix((uint)c.Queue.Count); Mix((uint)c.PopCap); }
        Mix((uint)W.Food.Count);
        return h;
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

        for (int i = 0; i < High; i++)
        {
            ref var p = ref Ants[i];
            p.PrevPos = p.Pos; p.PrevHeading = p.Heading; p.PrevAlt = p.Alt;
        }
        Ticks++;
        foreach (var c in Col) { TickColony(c, dt); TickResearch(c, dt); }
        TickShowers();
        TickWebs(dt);
        for (int i = 0; i < High; i++)
            if (Ants[i].Alive) TickAnt(i, dt);
        Ph.Update(dt);
        TickCorpses(dt);
    }

    void TickColony(Colony c, float dt)
    {
        c.PopUsed = 0;
        for (int t = 0; t < c.Count.Length; t++) c.PopUsed += c.Count[t] * Config.Stats[t].Pop;
        c.QueenAlarm -= dt;
        if (!c.Alive || c.Queue.Count == 0) { c.PopBlocked = false; return; }
        var item = c.Queue[0];
        if (item.Kind != ProdKind.Chamber && c.PopUsed + Config.Of(Config.UnitOf(item.Kind)).Pop > c.PopCap)
        {
            if (!c.PopBlocked) Events.Add(new SimEvent { Kind = EvKind.PopBlocked, Team = c.Team });
            c.PopBlocked = true;
            return;
        }
        c.PopBlocked = false;
        item.Progress += dt * Config.BroodSpeed(c.T(Upgrade.Queen)); // greater queens lay faster
        if (item.Progress < item.Time) return;
        c.Queue.RemoveAt(0);
        var nest = NestEntrance(c.Team);
        if (item.Kind == ProdKind.Chamber)
        {
            c.PopCap = Mathf.Min(Config.MaxPopCap, c.PopCap + Config.ChamberPop);
        }
        else
        {
            var type = Config.UnitOf(item.Kind);
            int id = Spawn(c.Team, type, nest + Rng.InDisk(1.2f), Rng.R(0, DMath.Tau));
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
    /// Nearest enemy; ground units cannot reach targets flying high above them.
    public int FindEnemy(Vector2 p, int team, float radius, bool air = false)
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
                    if (!air && b.Alt > 2.2f) continue;
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
        if (a.PoisonT > 0)
        {
            a.PoisonT -= dt;
            a.Hp -= a.PoisonDps * dt;
            if (a.Hp <= 0) { Kill(i, a.PoisonSrc >= 0 && Ants[a.PoisonSrc].Alive ? a.PoisonSrc : -1); return; }
        }
        a.AttackAnim = Mathf.Max(0, a.AttackAnim - dt * 3.5f);
        Vector2 desired = Vector2.Zero;
        float speedMul = 1f;
        bool think = a.Think <= 0;
        if (think) a.Think = 0.2f + Rng.F() * 0.1f;

        if (a.Type == AntType.Queen)
        {
            if (think) a.Target = FindEnemy(a.Pos, a.Team, st.Reach + 2.5f, Config.Flies(a.Type) || Config.Ranged(a.Type));
            if (a.Target >= 0 && Ants[a.Target].Alive) TryBite(i, a.Target, st);
            a.Hp = Mathf.Min(a.MaxHp, a.Hp + dt * 5f);
            a.Height = W.HF.Sample(a.Pos.X, a.Pos.Y);
            a.Gait += dt * 0.05f;
            return;
        }

        if (Config.Flies(a.Type))
        {
            // flyers cruise high; wasps dive onto their prey, flying ants swoop to bite ground targets
            float want = Config.CruiseAlt(a.Type);
            if (a.State == AntState.Engage && Valid(a.Target, a.TargetSerial))
            {
                ref var tt = ref Ants[a.Target];
                if (a.Pos.DistanceTo(tt.Pos) < 5f) want = a.Type == AntType.Wasp ? tt.Alt + 0.9f : Mathf.Max(tt.Alt, 1.4f);
            }
            a.Alt = Mathf.MoveToward(a.Alt, want, dt * 7f);
        }

        if (a.Root > 0)
        {
            // wrapped in silk: no walking, no biting until it tears free
            bool cutters = a.Team < 2 && Col[a.Team].T(Upgrade.SilkCutters) > 0;
            a.Root -= dt * (cutters ? 2f : 1f);
            a.Speed = 0;
            a.Gait += dt * 0.6f; // legs struggle
            return;
        }

        if (a.Team == Config.Wild && a.State == AntState.Engage) WildAbility(i, ref a, st);
        if (Config.IsHero(a.Type) && think) HeroAbility(i, ref a, st);
        a.AbilityCd -= dt;
        a.BuffT -= dt;

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
                    int e = FindEnemy(a.Pos, a.Team, st.Aggro, Config.Flies(a.Type) || Config.Ranged(a.Type));
                    if (e >= 0) { Engage(ref a, e, AntState.AttackMove); break; }
                }
                desired = a.Goal - a.Pos;
                if (desired.Length() < 0.9f) { desired = Vector2.Zero; a.Anchor = a.Pos; a.State = AntState.Idle; }
                break;
            case AntState.Idle:
            case AntState.Hold:
                if (think)
                {
                    float r = a.State == AntState.Hold ? st.Reach + a.Size * 0.5f + 1f : st.Aggro;
                    if (a.Type == AntType.Worker) r = st.Aggro;
                    int e = FindEnemy(a.Pos, a.Team, r, Config.Flies(a.Type) || Config.Ranged(a.Type));
                    if (e >= 0)
                    {
                        if (a.State == AntState.Hold) { a.Target = e; a.TargetSerial = Ants[e].Serial; }
                        else { Engage(ref a, e, AntState.Idle); break; }
                    }
                }
                if (a.State == AntState.Hold && Valid(a.Target, a.TargetSerial)) TryBite(i, a.Target, st);
                // drift back to anchor if shoved
                if (a.State == AntState.Idle && a.Pos.DistanceTo(a.Anchor) > 2.5f) { desired = a.Anchor - a.Pos; speedMul = 0.6f; }
                // wild bugs lick their wounds at their lair
                if (a.Team == Config.Wild && a.State == AntState.Idle) a.Hp = Mathf.Min(a.MaxHp, a.Hp + a.MaxHp * 0.04f * dt);
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
            int e = FindEnemy(a.Pos, a.Team, st.Aggro, Config.Flies(a.Type) || Config.Ranged(a.Type));
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
        if (!Config.Flies(a.Type) && !Config.Ranged(a.Type) && t.Alt > 2.2f) { a.Target = -1; return Vector2.Zero; } // it flew out of reach
        // leash for idle defenders and wild bugs guarding their lair
        if (!a.Forced && a.Prev == AntState.Idle && a.Pos.DistanceTo(a.Anchor) > (a.Team == Config.Wild ? 20f : st.Aggro * 2.2f))
        {
            a.Goal = a.Anchor; a.State = AntState.Move;
            return Vector2.Zero;
        }
        // switch to a closer threat now and then
        if (think && !a.Forced)
        {
            int e = FindEnemy(a.Pos, a.Team, st.Reach + 2f, Config.Flies(a.Type) || Config.Ranged(a.Type));
            if (e >= 0 && e != a.Target && Ants[e].Type != AntType.Queen) { a.Target = e; a.TargetSerial = Ants[e].Serial; }
        }
        float reach = st.Reach + t.Size * 0.38f + a.Size * 0.3f;
        var to = t.Pos - a.Pos;
        if (to.Length() > reach) return to;
        a.Heading += MathX.AngleDiff(a.Heading, to.AngleD()) * 0.3f;
        TryBite(i, a.Target, st);
        return Vector2.Zero;
    }

    void TryBite(int i, int target, UnitStats st)
    {
        ref var a = ref Ants[i];
        ref var t = ref Ants[target];
        float reach = st.Reach + t.Size * 0.38f + a.Size * 0.3f + 0.3f;
        if (a.Pos.DistanceTo(t.Pos) > reach) return;
        if (!Config.Ranged(a.Type) && Mathf.Abs(a.Alt - t.Alt) > reach + 0.6f) return;
        if (a.Cooldown > 0) return;
        a.Cooldown = st.Cooldown * Rng.R(0.85f, 1.15f);
        a.AttackAnim = 1;
        float dmg = st.Damage * a.DmgMul * (a.BuffT > 0 ? a.BuffMul : 1f) * Rng.R(0.85f, 1.15f);
        if (a.Type == AntType.Wasp && a.AbilityCd <= 0)
        {
            // venomous sting: a heavy blow on a cooldown that shortens with level
            dmg *= 2.5f + 0.3f * (a.Level - 1);
            a.AbilityCd = 5f - 0.4f * (a.Level - 1);
            Events.Add(new SimEvent { Kind = EvKind.Sting, Pos = t.Pos, Team = a.Team, Type = a.Type });
        }
        if (Config.Ranged(a.Type)) Events.Add(new SimEvent { Kind = EvKind.Spit, Pos = a.Pos, To = t.Pos, Team = a.Team, Type = a.Type });
        else Events.Add(new SimEvent { Kind = EvKind.Bite, Pos = t.Pos, Team = a.Team, Type = a.Type });
        if (a.Type == AntType.Warlord)
        {
            // cleave: every bite also catches the enemies crowding the warlord
            var tp = t.Pos;
            foreach (int j in EnemiesNear(tp, 2.6f, a.Team, false).ToArray()) if (j != target) Damage(j, dmg * 0.5f, i);
        }
        Damage(target, dmg, i);
    }

    // ------------------------------------------------------------------ wild bug specials
    readonly List<int> _near = new();
    readonly List<WebShotFx> _shots = new();
    public readonly List<WebZone> Webs = new();

    /// Ground ants (not flyers) of the colonies within r of p.
    List<int> AntsNear(Vector2 p, float r, bool groundOnly = true)
    {
        _near.Clear();
        int cr = (int)(r / SpatialHash.Cell) + 1;
        int cx = (int)(p.X / SpatialHash.Cell), cz = (int)(p.Y / SpatialHash.Cell);
        for (int z = cz - cr; z <= cz + cr; z++)
        {
            if (z < 0 || z >= Hash.N) continue;
            for (int x = cx - cr; x <= cx + cr; x++)
            {
                if (x < 0 || x >= Hash.N) continue;
                for (int j = Hash.Head[z * Hash.N + x]; j >= 0; j = Hash.Next[j])
                {
                    ref var b = ref Ants[j];
                    if (!b.Alive || b.Team >= 2 || (groundOnly && b.Alt > 1.5f)) continue;
                    if (b.Pos.DistanceSquaredTo(p) < r * r) _near.Add(j);
                }
            }
        }
        return _near;
    }

    void WildAbility(int i, ref Ant a, UnitStats st)
    {
        if (a.AbilityCd > 0) return;
        float lv = a.Level - 1;
        if (a.Type == AntType.Beetle || a.Type == AntType.StagBeetle)
        {
            // mandible sweep: crush every ant around the jaws and hurl them back
            bool stag = a.Type == AntType.StagBeetle;
            float r = a.Size * 0.75f + 1.2f + 0.25f * lv;
            var hits = AntsNear(a.Pos, r);
            if (hits.Count < (stag ? 1 : 2)) return;
            float dmg = st.Damage * a.DmgMul * (stag ? 2f : 1.5f);
            var victims = hits.ToArray();
            a.AttackAnim = 1;
            a.AbilityCd = (stag ? 5f : 7f) - 0.4f * lv;
            Events.Add(new SimEvent { Kind = EvKind.Sweep, Pos = a.Pos, Team = a.Team, Type = a.Type, Radius = r });
            foreach (int j in victims)
            {
                ref var b = ref Ants[j];
                var push = (b.Pos - a.Pos).Normalized();
                b.Pos = ClampMap(b.Pos + push * (1.2f + 0.2f * lv));
                Damage(j, dmg, i);
            }
        }
        else if (a.Type == AntType.Spider && Valid(a.Target, a.TargetSerial))
        {
            // web shot: a sticky blob that roots every ant it lands on
            var to = Ants[a.Target].Pos;
            float d = a.Pos.DistanceTo(to);
            if (d > 16) return;
            _shots.Add(new WebShotFx
            {
                From = a.Pos, To = to, Dur = 0.15f + d / 35f, R = 2.2f + 0.35f * lv, Life = 3.5f + 0.6f * lv, Root = 1.8f + 0.45f * lv,
            });
            a.AttackAnim = 1;
            a.AbilityCd = 6.5f - 0.5f * lv;
            Events.Add(new SimEvent { Kind = EvKind.WebShot, Pos = a.Pos, To = to, Team = a.Team, Type = a.Type });
        }
    }

    /// Enemies of `team` (colonies and wild bugs) within r of p.
    List<int> EnemiesNear(Vector2 p, float r, int team, bool groundOnly)
    {
        _near.Clear();
        int cr = (int)(r / SpatialHash.Cell) + 1;
        int cx = (int)(p.X / SpatialHash.Cell), cz = (int)(p.Y / SpatialHash.Cell);
        for (int z = cz - cr; z <= cz + cr; z++)
        {
            if (z < 0 || z >= Hash.N) continue;
            for (int x = cx - cr; x <= cx + cr; x++)
            {
                if (x < 0 || x >= Hash.N) continue;
                for (int j = Hash.Head[z * Hash.N + x]; j >= 0; j = Hash.Next[j])
                {
                    ref var b = ref Ants[j];
                    if (!b.Alive || b.Team == team || (groundOnly && b.Alt > 1.5f)) continue;
                    if (b.Pos.DistanceSquaredTo(p) < r * r) _near.Add(j);
                }
            }
        }
        return _near;
    }

    /// Heroes cast their specials on their own when the fight calls for it; potency grows with level and queen tier.
    void HeroAbility(int i, ref Ant a, UnitStats st)
    {
        if (a.AbilityCd > 0) return;
        float lv = a.Level;
        float tierMul = Config.HeroTierMul(a.Type, a.HeroTier);
        switch (a.Type)
        {
            case AntType.Warlord:
            {
                if (EnemiesNear(a.Pos, 10, a.Team, false).Count < 3) return;
                float mul = (1.3f + 0.03f * lv) * (1 + 0.1f * (tierMul - 1));
                int cr = 4;
                int cx = (int)(a.Pos.X / SpatialHash.Cell), cz = (int)(a.Pos.Y / SpatialHash.Cell);
                for (int z = cz - cr; z <= cz + cr; z++)
                {
                    if (z < 0 || z >= Hash.N) continue;
                    for (int x = cx - cr; x <= cx + cr; x++)
                    {
                        if (x < 0 || x >= Hash.N) continue;
                        for (int j = Hash.Head[z * Hash.N + x]; j >= 0; j = Hash.Next[j])
                        {
                            ref var b = ref Ants[j];
                            if (b.Alive && b.Team == a.Team && Config.IsWarrior(b.Type) && b.Pos.DistanceTo(a.Pos) < 13) { b.BuffT = 8 + 0.3f * lv; b.BuffMul = mul; }
                        }
                    }
                }
                a.AbilityCd = 22 - 0.6f * lv;
                a.AttackAnim = 1;
                Events.Add(new SimEvent { Kind = EvKind.WarCry, Pos = a.Pos, Team = a.Team, Type = a.Type, Radius = 13 });
                break;
            }
            case AntType.Matron:
            {
                if (!Valid(a.Target, a.TargetSerial) || a.State != AntState.Engage) return;
                var to = Ants[a.Target].Pos;
                if (a.Pos.DistanceTo(to) > 11) return;
                float r = 3.5f + 0.15f * lv;
                float dmg = (40 + 8 * lv) * a.DmgMul;
                foreach (int j in EnemiesNear(to, r, a.Team, false).ToArray())
                {
                    ref var b = ref Ants[j];
                    b.PoisonT = 4;
                    b.PoisonDps = (5 + 1.5f * lv) * tierMul;
                    b.PoisonSrc = i;
                    Damage(j, dmg, i);
                }
                a.AbilityCd = 9 - 0.3f * lv;
                a.AttackAnim = 1;
                Events.Add(new SimEvent { Kind = EvKind.AcidBurst, Pos = a.Pos, To = to, Team = a.Team, Type = a.Type, Radius = r });
                break;
            }
            case AntType.Marshal:
            {
                var foes = EnemiesNear(a.Pos, 12, a.Team, true);
                if (foes.Count < 2) return;
                var to = Ants[foes[0]].Pos;
                float r = 3.2f + 0.12f * lv;
                float dmg = (35 + 7 * lv) * a.DmgMul;
                a.Pos = ClampMap(to + (a.Pos - to).Normalized() * 1.5f);
                a.Alt = 0.6f;
                foreach (int j in EnemiesNear(to, r, a.Team, true).ToArray())
                {
                    Ants[j].Root = Mathf.Max(Ants[j].Root, 1.2f + 0.1f * lv);
                    Damage(j, dmg, i);
                }
                a.AbilityCd = 11 - 0.4f * lv;
                a.AttackAnim = 1;
                Events.Add(new SimEvent { Kind = EvKind.DiveBomb, Pos = to, Team = a.Team, Type = a.Type, Radius = r });
                break;
            }
        }
    }

    void TickWebs(float dt)
    {
        for (int s = _shots.Count - 1; s >= 0; s--)
        {
            var w = _shots[s];
            w.T += dt;
            if (w.T < w.Dur) continue;
            _shots.RemoveAt(s);
            Webs.Add(new WebZone { Pos = w.To, R = w.R, Life = w.Life, Max = w.Life, Root = w.Root });
            foreach (int j in AntsNear(w.To, w.R)) Ants[j].Root = Mathf.Max(Ants[j].Root, w.Root);
            Events.Add(new SimEvent { Kind = EvKind.WebLand, Pos = w.To, Team = Config.Wild, Radius = w.R });
        }
        for (int k = Webs.Count - 1; k >= 0; k--)
        {
            var z = Webs[k];
            z.Life -= dt;
            if (z.Life <= 0) { Webs.RemoveAt(k); continue; }
            // ants wading into a fresh web get stuck too
            foreach (int j in AntsNear(z.Pos, z.R)) if (Ants[j].Root <= 0) Ants[j].Root = 0.6f;
        }
    }

    /// Web shots in flight, for rendering: (from, to, progress 0..1).
    public IEnumerable<(Vector2 from, Vector2 to, float t)> WebShots()
    {
        foreach (var w in _shots) yield return (w.From, w.To, w.T / w.Dur);
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
        else if (t.Team < 2 && (attacker < 0 || Ants[attacker].Team != Config.Wild || t.Type != AntType.Worker))
            Events.Add(new SimEvent { Kind = EvKind.UnderAttack, Pos = t.Pos, Team = t.Team });
        if (t.Hp <= 0) { Kill(target, attacker); return; }
        // retaliate
        if (attacker >= 0 && Ants[attacker].Alive)
        {
            if (t.Type != AntType.Worker && (t.State == AntState.Idle || t.State == AntState.Move && t.Team == Config.Wild))
                Engage(ref t, attacker, AntState.Idle);
            else if (t.Type == AntType.Worker && t.State != AntState.Engage && Rng.F() < 0.5f)
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
        var c = t.Team < 2 ? Col[t.Team] : null;
        if (c != null) { c.Count[(int)t.Type]--; c.Lost++; }
        else WildCount--;
        if (killer >= 0 && Ants[killer].Team < 2)
        {
            ref var k = ref Ants[killer];
            Col[k.Team].Killed++;
            // the killer earns full experience, warriors fighting alongside share a part of it
            float xp = Config.Bounty(t.Type, t.Level);
            GrantXp(killer, xp);
            int cx = (int)(t.Pos.X / SpatialHash.Cell), cz = (int)(t.Pos.Y / SpatialHash.Cell);
            for (int z = cz - 4; z <= cz + 4; z++)
            {
                if (z < 0 || z >= Hash.N) continue;
                for (int x = cx - 4; x <= cx + 4; x++)
                {
                    if (x < 0 || x >= Hash.N) continue;
                    for (int j = Hash.Head[z * Hash.N + x]; j >= 0; j = Hash.Next[j])
                        if (j != killer && Ants[j].Alive && Ants[j].Team == k.Team && Ants[j].Pos.DistanceTo(t.Pos) < (Config.IsHero(Ants[j].Type) ? 15 : 11))
                            GrantXp(j, xp * (Config.IsHero(Ants[j].Type) ? 1f : 0.4f));
                }
            }
        }
        W.Food.Add(new FoodSource
        {
            Id = W.NextFoodId++, Kind = FoodKind.Corpse, Pos = t.Pos, Amount = st.CorpseFood, Max = st.CorpseFood, Radius = t.Size * 0.45f,
            Team = t.Team, AntType = t.Type, Heading = t.Heading,
        });
        Events.Add(new SimEvent { Kind = EvKind.Death, Pos = t.Pos, Team = t.Team, Type = t.Type });
        if (t.Type == AntType.Queen && c != null)
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
            int e = FindEnemy(a.Pos, a.Team, 12, Config.Flies(a.Type) || Config.Ranged(a.Type));
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
                float gain = (a.CarryKind == FoodKind.Berry || a.CarryKind == FoodKind.Sugar ? 5f : 4f) * c.FoodMult;
                c.Food += gain;
                c.Gathered += gain;
                Events.Add(new SimEvent { Kind = EvKind.Deliver, Pos = nest, Team = a.Team });
                a.Heading += DMath.Pi;
                if (a.Assigned != null && a.Assigned.Alive) { a.FoodTarget = a.Assigned; a.State = AntState.Gather; }
                else { a.Assigned = null; a.FoodTarget = null; a.State = AntState.Forage; }
                return Vector2.Zero;
            }
            // path integration home with a little meander
            return to.Normalized() + MathX.Dir(a.Heading + DMath.Sin(Time * 3 + i) * 0.4f) * 0.25f;
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
                ShrinkBlock(f);
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
            if (MathX.Dir(h).Dot(away) < -0.3f && a.Pos.DistanceTo(nest) > 12) h += MathX.AngleDiff(h, away.AngleD()) * dt * 1.5f;
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
        float radius = a.Size * 0.3f;
        float targetSpeed = 0;
        bool fly = Config.Flies(a.Type);
        if (desired.LengthSquared() > 1e-6f)
        {
            float want = desired.AngleD();
            var dest = a.Pos + desired; // when desired is a full vector this is the destination
            // look-ahead obstacle steering: slide along the rim of rocks (flyers pass over)
            var ahead = a.Pos + MathX.Dir(want) * (2.2f + radius);
            if (!fly) foreach (var o in W.ObstaclesNear(ahead))
            {
                float rr = o.R + radius + 0.4f;
                if (o.R <= 0 || dest.DistanceTo(o.P) < o.R + 1.5f) continue; // heading for this very thing
                if (ahead.DistanceSquaredTo(o.P) < rr * rr)
                {
                    var n = (a.Pos - o.P).Normalized();
                    var tangent = new Vector2(-n.Y, n.X);
                    if (tangent.Dot(MathX.Dir(want)) < 0) tangent = -tangent;
                    want = (tangent * 1.0f + n * 0.25f).AngleD();
                    break;
                }
            }
            float diff = MathX.AngleDiff(a.Heading, want);
            float turn = (a.Type == AntType.Major ? 5f : 8f) * dt;
            a.Heading += Mathf.Clamp(diff, -turn, turn);
            targetSpeed = st.Speed * a.SpeedMul * (a.BuffT > 0 ? 1.2f : 1f) * speedMul * (Mathf.Abs(diff) > 1.3f ? 0.35f : 1f);
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
                    if (!b.Alive || Config.Flies(b.Type) != fly) continue;
                    float rb = b.Size * 0.3f;
                    float min = radius + rb;
                    var d = pos - b.Pos;
                    float l2 = d.LengthSquared();
                    if (l2 >= min * min) continue;
                    seen++;
                    float l = Mathf.Sqrt(l2) + 1e-4f;
                    float w = b.Type == AntType.Queen ? 1f : (b.Size / (b.Size + a.Size));
                    if (l2 < 1e-6f) d = MathX.Dir(i * 1.7f);
                    push += d / l * (min - l) * w;
                }
            }
        }
        pos += push * 0.6f;

        // hard obstacle resolution
        if (!fly) foreach (var o in W.ObstaclesNear(pos))
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
        a.Gait = (a.Gait + moved / (0.55f * a.Size)) % 1f;
        a.Height = W.HF.Sample(pos.X, pos.Y);
    }
}
