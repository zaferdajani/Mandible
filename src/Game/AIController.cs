using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Computer colony: balances brood production, expands chambers, defends the mound and
/// sends growing raids at the opposing nest.
public sealed class AIController : IColonyAI
{
    readonly Sim _sim;
    readonly int _team;
    public int Team => _team;
    readonly Difficulty _diff;
    float _timer, _firstWave, _nextWave;
    int _waveSize;
    readonly List<int> _tmp = new();
    public bool Passive; // used for menu attract mode pacing
    public IOrders Orders;

    // orders go through IOrders so the same AI can play locally or across the network
    QueueResult Q(ProdKind k)
    {
        var r = _sim.CanEnqueue(_team, k);
        if (r == QueueResult.Ok) Orders.Issue(new Cmd { Kind = CmdKind.Enqueue, Team = (byte)_team, Arg = (byte)k });
        return r;
    }

    ResearchResult R(Upgrade u)
    {
        var r = _sim.CanResearch(_team, u);
        if (r == ResearchResult.Ok)
        {
            var kind = Sim.KindOf(u);
            var spot = _sim.ChamberOf(_team, kind) == null ? _sim.DefaultSpot(_team, kind) : Vector2.Zero; // a new room goes to its default spot
            Orders.Issue(new Cmd { Kind = CmdKind.Research, Team = (byte)_team, Arg = (byte)u, P = spot });
        }
        return r;
    }

    void Dig(ChamberKind k)
    {
        if (_sim.CanDig(_team, k) == Sim.DigResult.Ok) Orders.Issue(new Cmd { Kind = CmdKind.Dig, Team = (byte)_team, Arg = (byte)k, P = _sim.DefaultSpot(_team, k) });
    }

    void Move(List<int> ids, Vector2 p, bool attack)
    {
        var c = Cmd.Units(attack ? CmdKind.AttackMove : CmdKind.Move, _team, ids, _sim);
        c.P = p;
        Orders.Issue(c);
    }

    public AIController(Sim sim, int team, Difficulty diff)
    {
        _sim = sim; _team = team; _diff = diff;
        Orders = new DirectOrders(sim);
        var c = sim.Col[team];
        c.FoodMult = diff switch { Difficulty.Easy => 0.75f, Difficulty.Hard => 1.3f, _ => 1f };
        _firstWave = diff switch { Difficulty.Easy => 330, Difficulty.Hard => 180, _ => 250 };
        _waveSize = diff switch { Difficulty.Easy => 14, Difficulty.Hard => 26, _ => 20 };
        _nextWave = _firstWave;
    }

    public void Tick(float dt)
    {
        _timer += dt;
        if (_timer < 0.8f) return;
        _timer = 0;
        var c = _sim.Col[_team];
        if (!c.Alive) return;
        Produce(c);
        Reinforce(c);
        Defend(c);
        Raid(c);
        Creep(c);
    }

    /// Battered squads that are not fighting go home to heal and refill.
    void Reinforce(Colony c)
    {
        var nest = _sim.W.Nests[_team].Pos;
        foreach (var s in _sim.Squads)
        {
            if (s.Team != _team || !s.Alive || s.Recalled || s.Count * 2 > s.Size) continue;
            if (_sim.Centroid(s).DistanceTo(nest) < Config.ReinforceRadius) continue;
            bool fighting = false;
            foreach (int m in s.Members) if (_sim.Ants[m].State == AntState.Engage) { fighting = true; break; }
            if (fighting) continue;
            Orders.Issue(Cmd.Units(CmdKind.Reinforce, _team, s.Members, _sim));
        }
    }

    float _nextCreep = 75;

    /// Between raids, clear the weakest wild lair the idle army can safely beat: food and veterans.
    void Creep(Colony c)
    {
        if (Passive || _sim.Time < _nextCreep) return;
        _nextCreep = _sim.Time + 20;
        var nest = _sim.W.Nests[_team].Pos;
        _tmp.Clear();
        float army = 0;
        for (int i = 0; i < _sim.High; i++)
        {
            ref var a = ref _sim.Ants[i];
            if (!a.Alive || a.Team != _team || !Config.IsWarrior(a.Type)) continue;
            if (a.State == AntState.Idle || a.State == AntState.Move)
            {
                _tmp.Add(i);
                army += Config.Of(a.Type).Hp * a.DmgMul * (a.MaxHp / Config.Of(a.Type).Hp) * Config.Of(a.Type).Damage;
            }
        }
        if (_tmp.Count < 6) { Regroup(nest); return; }
        Camp best = null;
        float bestScore = float.MaxValue;
        foreach (var camp in _sim.W.Camps)
        {
            float threat = 0;
            for (int i = 0; i < _sim.High; i++)
            {
                ref var b = ref _sim.Ants[i];
                if (b.Alive && b.Team == Config.Wild && b.Anchor.DistanceTo(camp.Pos) < 8) threat += b.MaxHp * Config.Of(b.Type).Damage * b.DmgMul;
            }
            if (threat <= 0 || army < threat * (_diff == Difficulty.Hard ? 1.6f : 2.2f)) continue;
            float score = camp.Pos.DistanceTo(nest) + threat * 0.0004f;
            if (camp.Pos.DistanceTo(_sim.W.Nests[1 - _team].Pos) < camp.Pos.DistanceTo(nest) * 0.8f) continue; // not on the enemy's doorstep
            if (score < bestScore) { bestScore = score; best = camp; }
        }
        if (best != null) Move(_tmp, best.Pos, true);
        else Regroup(nest);
    }

    readonly List<int> _stray = new();

    void Regroup(Vector2 nest)
    {
        _stray.Clear();
        for (int i = 0; i < _sim.High; i++)
        {
            ref var a = ref _sim.Ants[i];
            if (a.Alive && a.Team == _team && Config.IsWarrior(a.Type) && a.State == AntState.Idle && a.Pos.DistanceTo(nest) > 60) _stray.Add(i);
        }
        var toCenter = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - nest).Normalized();
        if (_stray.Count > 0) Move(_stray, nest + toCenter * 14, false);
    }

    void Produce(Colony c)
    {
        if (!c.Founded) return; // the founders are digging; nothing can hatch yet
        if (c.Queue.Count >= 3) return;
        int workers = c.Count[0], soldiers = c.Count[1], majors = c.Count[2];
        int wantWorkers = _diff switch { Difficulty.Easy => 40, Difficulty.Hard => 100, _ => 75 };
        // more stores before the food rots in full ones
        if (c.Food > c.FoodCap * 0.8f && _sim.CanDig(_team, ChamberKind.Storage) == Sim.DigResult.Ok) { Dig(ChamberKind.Storage); return; }
        // dig brood chambers ahead of need, saving up for them rather than spending on brood
        int digging = 0;
        foreach (var ch in c.Chambers) if (ch.Kind == ChamberKind.Brood && !ch.Built) digging++;
        int capAhead = c.PopCap + digging * Config.ChamberPop;
        if (c.PopUsed + 14 >= capAhead && capAhead < Config.MaxPopCap && digging == 0)
        {
            if (c.Food >= Sim.BroodCost) Dig(ChamberKind.Brood);
            return;
        }
        // tech timing: when research is due, bank food for it, but never below a working economy and a home guard
        int warriors = soldiers + majors + c.Count[(int)AntType.Gunner] + c.Count[(int)AntType.Flyer];
        if (Research(c) && workers >= wantWorkers * 0.7f && warriors >= 18 && c.QueenAlarm <= 0) return;
        // squads cost a lot at once: never let a beaten colony's economy shrink below what can rebuild them
        if (workers < 32 && workers < wantWorkers) { Q(ProdKind.Worker); return; }
        // a hero whenever one is available and not alive, saved for only while a home guard stands
        foreach (var hk in new[] { ProdKind.Warlord, ProdKind.Matron, ProdKind.Marshal })
            if (Config.Unlocked(hk, c.Tier) && c.Count[(int)Config.UnitOf(hk)] == 0 && !c.Queue.Exists(q => q.Kind == hk))
            {
                if (Q(hk) == QueueResult.Ok) return;
                if (c.Food < Config.ProdCost(hk) && warriors >= 12) return;
            }
        int gunners = c.Count[(int)AntType.Gunner], flyers = c.Count[(int)AntType.Flyer];
        ProdKind pick;
        if (workers < wantWorkers && workers < soldiers * 2 + 20) pick = ProdKind.Worker;
        else if (Config.Unlocked(ProdKind.Major, c.Tier) && majors * 4 < soldiers) pick = ProdKind.Major;
        else if (Config.Unlocked(ProdKind.Gunner, c.Tier) && gunners * (c.Race == Race.BlackGarden ? 3 : 7) < soldiers) pick = ProdKind.Gunner; // fire ants have no acid
        else if (Config.Unlocked(ProdKind.Flyer, c.Tier) && flyers * 5 < soldiers) pick = ProdKind.Flyer;
        else pick = ProdKind.Soldier;
        Q(pick); // a major simply waits until the food is there
    }

    // the colony's tech path: queen first, then forges, wings and silk cutters as she unlocks them
    static readonly (Upgrade u, int tier, float after)[] Plan =
    {
        (Upgrade.Queen, 2, 150), (Upgrade.Mandibles, 1, 200), (Upgrade.Chitin, 1, 240), (Upgrade.Wings, 1, 300),
        (Upgrade.Queen, 3, 330), (Upgrade.Mandibles, 2, 420), (Upgrade.Chitin, 2, 460), (Upgrade.SilkCutters, 1, 500),
        (Upgrade.Queen, 4, 560), (Upgrade.Mandibles, 3, 650), (Upgrade.Chitin, 3, 700), (Upgrade.Queen, 5, 780),
    };

    /// Starts the next research when affordable; returns true while the colony is saving up for it.
    bool Research(Colony c)
    {
        if (c.Researching) return false;
        float pace = _diff switch { Difficulty.Easy => 1.5f, Difficulty.Hard => 0.8f, _ => 1f };
        foreach (var (u, tier, after) in Plan)
        {
            if (c.T(u) >= tier) continue;
            if (_sim.Time < after * pace) return false;
            var r = R(u);
            if (r == ResearchResult.Ok) return false;
            return r == ResearchResult.NoFood;
        }
        return false;
    }

    void Defend(Colony c)
    {
        var nest = _sim.W.Nests[_team].Pos;
        int threat = -1;
        for (int i = 0; i < _sim.High; i++)
        {
            ref var a = ref _sim.Ants[i];
            if (!a.Alive || a.Team == _team || a.Team == Config.Wild || a.Type == AntType.Worker) continue;
            if (a.Pos.DistanceTo(nest) < 48) { threat = i; break; }
        }
        if (threat < 0) return;
        _tmp.Clear();
        for (int i = 0; i < _sim.High; i++)
        {
            ref var a = ref _sim.Ants[i];
            if (!a.Alive || a.Team != _team || !Config.IsWarrior(a.Type)) continue;
            if (a.State == AntState.Engage) continue;
            if (a.Pos.DistanceTo(nest) < 90) _tmp.Add(i);
        }
        if (_tmp.Count > 0) Move(_tmp, _sim.Ants[threat].Pos, true);
    }

    void Raid(Colony c)
    {
        if (Passive || _sim.Time < _nextWave) return;
        _tmp.Clear();
        var nest = _sim.W.Nests[_team].Pos;
        for (int i = 0; i < _sim.High; i++)
        {
            ref var a = ref _sim.Ants[i];
            if (!a.Alive || a.Team != _team || !Config.IsWarrior(a.Type)) continue;
            if (a.State == AntState.Idle || a.State == AntState.Move) _tmp.Add(i); // wherever they stand, even back from a lair
        }
        int army = 0;
        foreach (int i in _tmp) army += Config.Of(_sim.Ants[i].Type).Pop;
        // the yard is dangerous now: if the full wave never comes together, go with what there is;
        // and a colony that clearly outnumbers its rival does not wait for a parade-ground wave
        bool overdue = _sim.Time > _nextWave + 150;
        int enemyArmy = 0;
        for (int i = 0; i < _sim.High; i++)
        {
            ref var b = ref _sim.Ants[i];
            if (b.Alive && b.Team == 1 - _team && Config.IsWarrior(b.Type)) enemyArmy += Config.Of(b.Type).Pop;
        }
        bool stronger = overdue && army >= 8 && army >= enemyArmy * 1.2f;
        if (army < _waveSize && !(overdue && army >= _waveSize * 0.6f) && !stronger) return;
        var target = _sim.W.Nests[1 - _team].Pos;
        // keep a small home guard
        int guard = _tmp.Count / 6;
        _tmp.RemoveRange(0, guard);
        Move(_tmp, target, true);
        _waveSize = Mathf.Min(60, _waveSize + (_diff == Difficulty.Hard ? 6 : 4));
        _nextWave = _sim.Time + (_diff == Difficulty.Easy ? 110 : 75);
    }
}
