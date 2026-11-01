using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Anything that plays a colony for the computer.
public interface IColonyAI
{
    int Team { get; }
    void Tick(float dt);
}

/// Plays a Lane War hero: pushes a lane behind its own creeps, trades with the enemy hero when it
/// has the upper hand, respects enemy guardians, retreats to the healing pool, casts and shops.
public sealed class LaneAI : IColonyAI
{
    readonly Sim _sim;
    readonly int _team;
    readonly Difficulty _diff;
    public int Team => _team;
    public IOrders Orders;
    float _timer;
    bool _retreat;
    int _lane = 1, _shopTurn;
    float _laneTimer;
    readonly List<int> _one = new();

    public LaneAI(Sim sim, int team, Difficulty diff)
    {
        _sim = sim; _team = team; _diff = diff;
        Orders = new DirectOrders(sim);
        sim.Col[team].FoodMult = diff switch { Difficulty.Easy => 0.75f, Difficulty.Hard => 1.3f, _ => 1f };
    }

    public void Tick(float dt)
    {
        _timer += dt;
        if (_timer < 0.4f) return;
        _timer = 0;
        Shop();
        int id = _sim.HeroId[_team];
        if (id < 0) return;
        ref var h = ref _sim.Ants[id];
        if (!h.Alive) return;
        var nest = _sim.W.Nests[_team].Pos;

        // heal up at home when hurt; Easy heroes are greedier
        float low = _diff == Difficulty.Easy ? 0.18f : _diff == Difficulty.Hard ? 0.35f : 0.28f;
        if (h.Hp < h.MaxHp * low) _retreat = true;
        if (_retreat && h.Hp > h.MaxHp * 0.9f) _retreat = false;
        if (_retreat)
        {
            if (h.Pos.DistanceTo(nest) > Lane.FountainRadius * 0.6f) Order(CmdKind.Move, id, nest);
            return;
        }

        // the ability: whenever enemies are bunched nearby, or the Old Stag is in reach
        bool stagClose = _sim.StagAwake && _sim.StagTeam != _team && _sim.Ants[_sim.StagId].Pos.DistanceTo(h.Pos) < 9;
        if (h.AbilityCd <= 0 && h.CastReq <= 0 && (Enemies(h.Pos, 11) >= 2 || stagClose)) Orders.Issue(new Cmd { Kind = CmdKind.Ability, Team = (byte)_team });

        int foe = _sim.HeroId[1 - _team];
        bool foeAlive = foe >= 0 && _sim.Ants[foe].Alive;
        int foeLevel = foeAlive ? _sim.Ants[foe].Level : _sim.HeroLevel[1 - _team];
        // ahead: the enemy hero is down, two levels behind, or has lost more posts. Then press, do not farm.
        bool ahead = !foeAlive || h.Level >= foeLevel + 2 || _sim.GuardiansLeft(_team) > _sim.GuardiansLeft(1 - _team) + 1;

        // which lane: follow our roused Old Stag; when ahead, the lane where our wave has gone furthest;
        // otherwise (Hard, or anyone who is behind) the lane where the enemy is pushing hardest
        _laneTimer -= 0.4f;
        if (_sim.StagAwake && _sim.StagTeam == _team) { _lane = _sim.Ants[_sim.StagId].Lane - 1; _laneTimer = 15; }
        else if (_laneTimer <= 0)
        {
            _laneTimer = ahead ? 15 : 45;
            if (ahead) _lane = BestPushLane();
            else if (_diff == Difficulty.Hard || _sim.GuardiansLeft(_team) < _sim.GuardiansLeft(1 - _team)) _lane = MostThreatened();
        }

        // objectives: fell the Old Stag once strong enough, milk the aphid herd when nobody contests it
        if (_diff != Difficulty.Easy && _sim.StagAwake && _sim.StagTeam < 0 && h.Level >= (foeAlive ? 6 : 5) && h.Hp > h.MaxHp * (foeAlive ? 0.75f : 0.6f))
        {
            var stagPos = _sim.Ants[_sim.StagId].Pos;
            bool foeStronger = foeAlive && _sim.Ants[foe].Pos.DistanceTo(stagPos) < 16 && _sim.Ants[foe].Hp > h.Hp;
            if (!foeStronger) { Order(CmdKind.Attack, id, Vector2.Zero, _sim.StagId); return; }
        }
        if (_sim.HerdHere && h.Hp > h.MaxHp * 0.55f && _sim.HerdTeam != 1 - _team)
        {
            bool foeThere = foeAlive && _sim.Ants[foe].Pos.DistanceTo(_sim.Pasture) < Lane.PastureRadius + 5 && _sim.Ants[foe].Hp > h.Hp * 0.9f;
            if (!foeThere)
            {
                if (h.Pos.DistanceTo(_sim.Pasture) > 3.5f) Order(CmdKind.AttackMove, id, _sim.Pasture);
                return;
            }
        }

        // fight the enemy hero if it is close and weaker
        if (foe >= 0 && _sim.Ants[foe].Alive && _sim.Ants[foe].Pos.DistanceTo(h.Pos) < 13
            && _sim.Ants[foe].Hp / _sim.Ants[foe].MaxHp < h.Hp / h.MaxHp + 0.1f && !UnderEnemyGuardian(_sim.Ants[foe].Pos))
        {
            Order(CmdKind.Attack, id, Vector2.Zero, foe);
            return;
        }

        // siege: while our creeps (or our stag) are at an enemy post, bite its weakest guardian
        int post = WeakestGuardianNear(h.Pos, 13);
        if (post >= 0 && (CreepsNear(_sim.Ants[post].Pos, Lane.SiegeRadius) || StagNear(_sim.Ants[post].Pos, Lane.SiegeRadius)))
        {
            if (h.Target != post) Order(CmdKind.Attack, id, Vector2.Zero, post);
            return;
        }

        // stand just behind the front of our wave; without a wave, hold at our outer guardian
        var front = Front(out bool any);
        var spot = any ? front : _sim.AlongLane(_team, _lane, 0.33f);
        if (UnderEnemyGuardian(spot) && !CreepsNear(spot, 7) && !StagNear(spot, 9)) spot = _sim.AlongLane(_team, _lane, 0.33f);
        if (h.State != AntState.Engage || h.Pos.DistanceTo(spot) > 20) Order(CmdKind.AttackMove, id, spot);
    }

    void Shop()
    {
        var c = _sim.Col[_team];
        int id = _sim.HeroId[_team];
        if (id >= 0 && _sim.Ants[id].Hp < _sim.Ants[id].MaxHp * 0.45f && _sim.CanBuy(_team, 3)) { Buy(3); return; }
        int item = _shopTurn % 3;
        float reserve = _diff == Difficulty.Easy ? 1.6f : 1f;
        if (_sim.Items[_team, item] >= Lane.MaxItemTier) { _shopTurn++; return; }
        if (c.Food >= Lane.ItemCost(item, _sim.Items[_team, item]) * reserve && _sim.CanBuy(_team, item)) { Buy(item); _shopTurn++; }
    }

    void Buy(int item) => Orders.Issue(new Cmd { Kind = CmdKind.BuyItem, Team = (byte)_team, Arg = (byte)item });

    void Order(CmdKind k, int id, Vector2 p, int target = -1)
    {
        _one.Clear();
        _one.Add(id);
        var c = Cmd.Units(k, _team, _one, _sim);
        c.P = p;
        if (target >= 0) { c.Target = target; c.TargetSerial = _sim.Ants[target].Serial; }
        Orders.Issue(c);
    }

    /// Position a little behind our creep that has advanced furthest down the chosen lane.
    Vector2 Front(out bool any)
    {
        any = false;
        float best = -1;
        Vector2 at = Vector2.Zero;
        var enemyNest = _sim.W.Nests[1 - _team].Pos;
        for (int i = 0; i < _sim.High; i++)
        {
            ref var a = ref _sim.Ants[i];
            if (!a.Alive || a.Team != _team || a.Lane != _lane + 1) continue;
            float progress = 1000 - a.Pos.DistanceTo(enemyNest);
            if (progress > best) { best = progress; at = a.Pos; any = true; }
        }
        if (any) at += (_sim.W.Nests[_team].Pos - at).Normalized() * 4f;
        return at;
    }

    int Enemies(Vector2 p, float r)
    {
        int n = 0;
        for (int i = 0; i < _sim.High; i++)
        {
            ref var a = ref _sim.Ants[i];
            if (a.Alive && a.Team != _team && a.Type != AntType.Worker && a.Pos.DistanceTo(p) < r) n++;
        }
        return n;
    }

    bool CreepsNear(Vector2 p, float r)
    {
        for (int i = 0; i < _sim.High; i++)
            if (_sim.Ants[i].Alive && _sim.Ants[i].Team == _team && _sim.Ants[i].Lane > 0 && _sim.Ants[i].Pos.DistanceTo(p) < r) return true;
        return false;
    }

    bool UnderEnemyGuardian(Vector2 p)
    {
        for (int i = 0; i < _sim.High; i++)
        {
            ref var a = ref _sim.Ants[i];
            if (a.Alive && a.Guardian && a.Team != _team && a.Pos.DistanceTo(p) < (Config.Ranged(a.Type) ? 11 : 8)) return true;
        }
        return false;
    }

    /// The lane where our creeps (or our stag) have gone furthest toward the enemy nest.
    int BestPushLane()
    {
        var enemyNest = _sim.W.Nests[1 - _team].Pos;
        int lane = _lane;
        float best = float.MaxValue;
        for (int i = 0; i < _sim.High; i++)
        {
            ref var a = ref _sim.Ants[i];
            if (!a.Alive || a.Team != _team || a.Lane == 0) continue;
            float d = a.Pos.DistanceTo(enemyNest);
            if (d < best) { best = d; lane = a.Lane - 1; }
        }
        return lane;
    }

    /// The enemy guardian with the least health within r of p, or -1.
    int WeakestGuardianNear(Vector2 p, float r)
    {
        int best = -1;
        float hp = float.MaxValue;
        for (int i = 0; i < _sim.High; i++)
        {
            ref var a = ref _sim.Ants[i];
            if (a.Alive && a.Guardian && a.Team != _team && a.Pos.DistanceTo(p) < r && a.Hp < hp) { hp = a.Hp; best = i; }
        }
        return best;
    }

    bool StagNear(Vector2 p, float r) => _sim.StagAwake && _sim.StagTeam == _team && _sim.Ants[_sim.StagId].Pos.DistanceTo(p) < r;

    /// The lane where enemy creeps stand closest to our nest.
    int MostThreatened()
    {
        var nest = _sim.W.Nests[_team].Pos;
        int lane = 1;
        float best = float.MaxValue;
        for (int i = 0; i < _sim.High; i++)
        {
            ref var a = ref _sim.Ants[i];
            if (!a.Alive || a.Team == _team || a.Team > 1 || a.Lane == 0) continue;
            float d = a.Pos.DistanceTo(nest);
            if (d < best) { best = d; lane = a.Lane - 1; }
        }
        return lane;
    }
}
