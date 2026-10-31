using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Computer colony: balances brood production, expands chambers, defends the mound and
/// sends growing raids at the opposing nest.
public sealed class AIController
{
    readonly Sim _sim;
    readonly int _team;
    public int Team => _team;
    readonly Difficulty _diff;
    float _timer, _firstWave, _nextWave;
    int _waveSize;
    readonly List<int> _tmp = new();
    public bool Passive; // used for menu attract mode pacing

    public AIController(Sim sim, int team, Difficulty diff)
    {
        _sim = sim; _team = team; _diff = diff;
        var c = sim.Col[team];
        c.FoodMult = diff switch { Difficulty.Easy => 0.75f, Difficulty.Hard => 1.3f, _ => 1f };
        _firstWave = diff switch { Difficulty.Easy => 300, Difficulty.Hard => 150, _ => 210 };
        _waveSize = diff switch { Difficulty.Easy => 12, Difficulty.Hard => 22, _ => 16 };
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
        Defend(c);
        Raid(c);
    }

    void Produce(Colony c)
    {
        if (c.Queue.Count >= 3) return;
        int workers = c.Count[0], soldiers = c.Count[1], majors = c.Count[2];
        int wantWorkers = _diff switch { Difficulty.Easy => 40, Difficulty.Hard => 100, _ => 75 };
        int capAhead = c.PopCap + c.PendingChambers() * Config.ChamberPop;
        // dig ahead of need, saving up for it rather than spending on brood
        if (c.PopUsed + 14 >= capAhead && capAhead < Config.MaxPopCap && c.PendingChambers() == 0)
        {
            if (c.Food >= Config.ChamberCost) _sim.Enqueue(_team, ProdKind.Chamber);
            return;
        }
        ProdKind pick;
        if (workers < wantWorkers && workers < soldiers * 2 + 20) pick = ProdKind.Worker;
        else if (_sim.Time > 200 && majors * 4 < soldiers) pick = ProdKind.Major;
        else pick = ProdKind.Soldier;
        _sim.Enqueue(_team, pick); // a major simply waits until the food is there
    }

    void Defend(Colony c)
    {
        var nest = _sim.W.Nests[_team].Pos;
        int threat = -1;
        for (int i = 0; i < _sim.High; i++)
        {
            ref var a = ref _sim.Ants[i];
            if (!a.Alive || a.Team == _team || a.Type == AntType.Worker) continue;
            if (a.Pos.DistanceTo(nest) < 48) { threat = i; break; }
        }
        if (threat < 0) return;
        _tmp.Clear();
        for (int i = 0; i < _sim.High; i++)
        {
            ref var a = ref _sim.Ants[i];
            if (!a.Alive || a.Team != _team || (a.Type != AntType.Soldier && a.Type != AntType.Major)) continue;
            if (a.State == AntState.Engage) continue;
            if (a.Pos.DistanceTo(nest) < 90) _tmp.Add(i);
        }
        if (_tmp.Count > 0) _sim.CmdMove(_tmp, _sim.Ants[threat].Pos, true);
    }

    void Raid(Colony c)
    {
        if (Passive || _sim.Time < _nextWave) return;
        _tmp.Clear();
        var nest = _sim.W.Nests[_team].Pos;
        for (int i = 0; i < _sim.High; i++)
        {
            ref var a = ref _sim.Ants[i];
            if (!a.Alive || a.Team != _team || (a.Type != AntType.Soldier && a.Type != AntType.Major)) continue;
            if ((a.State == AntState.Idle || a.State == AntState.Move) && a.Pos.DistanceTo(nest) < 70) _tmp.Add(i);
        }
        int army = 0;
        foreach (int i in _tmp) army += _sim.Ants[i].Type == AntType.Major ? 3 : 1;
        if (army < _waveSize) return;
        var target = _sim.W.Nests[1 - _team].Pos;
        // keep a small home guard
        int guard = _tmp.Count / 6;
        _tmp.RemoveRange(0, guard);
        _sim.CmdMove(_tmp, target, true);
        _waveSize = Mathf.Min(90, _waveSize + (_diff == Difficulty.Hard ? 10 : 7));
        _nextWave = _sim.Time + (_diff == Difficulty.Easy ? 110 : 75);
    }
}
