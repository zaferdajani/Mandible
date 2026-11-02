using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

public enum ExpeditionStage { ClearTrail, EscortQueen, DigEntrance, RoyalChamber, Incubator, Store, RaiseWorkers, RaiseSoldiers, Complete }

/// An authored campaign episode. Its gates live in the simulation, so hotkeys and UI buttons
/// obey the same rules. Episode one is the confined founding lesson (Stage); episodes two to
/// four play on the open yard with an ordered list of objectives (Step, see Campaign.cs).
public sealed partial class Expedition
{
    public int Episode = 1;
    public ExpeditionStage Stage;
    public bool EntranceOrdered;
    public int WorkersHatched, SoldiersHatched;
    public readonly List<int> Guards = new();
    public const int WorkerGoal = 4, SoldierGoal = 6;
    public static readonly Vector2 Start = new(48, 256), Ambush = new(88, 256), Clearing = new(144, 256);

    public static Vector2 Constrain(Vector2 p)
    {
        var trail = new Vector2(Mathf.Clamp(p.X, 40, 144), Mathf.Clamp(p.Y, 250, 262));
        var d = p - Clearing;
        var clearing = Clearing + (d.Length() > 27 ? d.Normalized() * 27 : d);
        return p.DistanceSquaredTo(trail) < p.DistanceSquaredTo(clearing) ? trail : clearing;
    }
    public static bool Contains(Vector2 p, float margin = 0)
        => p.DistanceTo(Constrain(p)) <= margin + 0.01f;

    public string Title => Episode > 1 ? StepTitle(Step) : Stage switch
    {
        ExpeditionStage.ClearTrail => "Clear the grass alley",
        ExpeditionStage.EscortQueen => "Lead the queen to food",
        ExpeditionStage.DigEntrance => "Choose your workers",
        ExpeditionStage.RoyalChamber => "A home for the queen",
        ExpeditionStage.Incubator => "Build your first incubator",
        ExpeditionStage.Store => "Store the harvest",
        ExpeditionStage.RaiseWorkers => "Grow the workforce",
        ExpeditionStage.RaiseSoldiers => "Raise a fighting squad",
        _ => "The first nest is thriving",
    };
    public string Instruction => Episode > 1 ? StepInstruction : Stage switch
    {
        ExpeditionStage.ClearTrail => "Press F2 to select your attackers. Right-click the red ants ahead, or press A and click beyond them.",
        ExpeditionStage.EscortQueen => "With your attackers selected, right-click the food clearing. The queen and workers follow them along the protected trail.",
        ExpeditionStage.DigEntrance => "Press F3 to select workers, then click Dig entrance below. They will excavate the shaft and royal chamber at the food clearing.",
        ExpeditionStage.RoyalChamber => "Workers are excavating. The queen will enter the royal chamber as soon as it is ready.",
        ExpeditionStage.Incubator => "Press N, choose Brood Chamber, then click beside the royal chamber. This incubator hatches eggs and adds 50 population capacity.",
        ExpeditionStage.Store => "Choose Storage and click beside a connected room. Workers gather seeds and insect protein and carry them home.",
        ExpeditionStage.RaiseWorkers => $"Hatch {WorkerGoal} new workers with Z or the Worker portrait. Hatched: {WorkersHatched}/{WorkerGoal}. New workers leave the incubator and gather food.",
        ExpeditionStage.RaiseSoldiers => $"Hatch one squad with X or the Soldier portrait. Hatched: {SoldiersHatched}/{SoldierGoal}. This episode ends when your colony can support its first army.",
        _ => "Level complete. Sweet Rain is now open on the colony map; Skirmish lets you try the complete roster now.",
    };
    public Vector2 Marker => Episode > 1 ? MarkerPos : Stage == ExpeditionStage.ClearTrail ? Ambush : Clearing;
}

public sealed partial class Sim
{
    public Expedition Mission { get; private set; }
    public bool UnlockedSkirmish { get; private set; }

    void BeginExpedition()
    {
        Mission = new Expedition();
        var c = Col[0];
        c.Chambers.Clear();
        c.Food = 250;
        // The foe's founding party is absent in episode one. Remove it without a death
        // event, which would otherwise incorrectly award a queen-kill victory.
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (a.Team == 1) { if (a.Alive) Col[1].Count[(int)a.Type]--; a.Alive = false; continue; }
            if (a.Team != 0) continue;
            a.Pos = a.PrevPos = Expedition.Start + new Vector2((i % 4) * 1.4f, (i % 3 - 1) * 1.6f);
            a.Anchor = a.Goal = a.Pos; a.State = AntState.Hold;
        }
        Col[1].Alive = false; Col[1].Chambers.Clear();
        for (int i = 0; i < 6; i++)
        {
            int id = Spawn(0, AntType.Soldier, Expedition.Start + new Vector2(8 + i % 3 * 1.8f, (i / 3 - 0.5f) * 3), 0);
            Ants[id].State = AntState.Hold;
        }
        c.PopCap = 0;
        for (int type = 0; type < c.Count.Length; type++) c.PopCap += c.Count[type] * Config.Stats[type].Pop;
        for (int i = 0; i < 3; i++)
        {
            int id = Spawn(1, AntType.Soldier, Expedition.Ambush + new Vector2(i % 2 * 2, (i - 1) * 2), Mathf.Pi);
            Ants[id].State = AntState.Hold;
            Mission.Guards.Add(id);
        }
    }

    /// Starts fully developed colonies for a repeatable, unrestricted testing match.
    public void BeginSkirmish()
    {
        UnlockedSkirmish = true;
        foreach (var c in Col)
        {
            for (int u = 0; u < c.Tier.Length; u++) c.Tier[u] = Config.MaxTier((Upgrade)u);
            DevelopNest(c.Team);
            if (c.Chambers.Count > 7) c.Chambers.RemoveRange(7, c.Chambers.Count - 7);
            RecountCapacity(c);
            c.Food = c.FoodCap;
            ApplyLevel(ref Ants[c.QueenId]);
        }
    }

    public bool OrderEntrance(int team, IReadOnlyList<int> workers)
    {
        if (team != 0 || Mission?.Stage != ExpeditionStage.DigEntrance || Mission.EntranceOrdered) return false;
        bool hasWorker = false;
        foreach (int id in workers)
            if (id >= 0 && id < High && Ants[id].Alive && Ants[id].Team == team && Ants[id].Type == AntType.Worker && Ants[id].Pos.DistanceTo(Expedition.Clearing) < 30) hasWorker = true;
        if (!hasWorker) return false;
        Mission.EntranceOrdered = true;
        AddChamber(team, ChamberKind.Royal, FoundingWork, false);
        foreach (int id in workers)
            if (id >= 0 && id < High && Ants[id].Alive && Ants[id].Team == team && Ants[id].Type == AntType.Worker) Ants[id].State = AntState.Idle;
        Mission.Stage = ExpeditionStage.RoyalChamber;
        return true;
    }

    void TickExpedition(float dt)
    {
        if (Mission != null && Mission.Episode > 1 && Winner == Config.Player && !Mission.Complete) { Mission.StepsDone = Mission.Step; Mission.Step = Mission.Steps; }
        if (Mission != null) TrackStars();
        if (Mission == null || !Col[0].Alive || Winner >= 0) return;
        if (Mission.Episode > 1) { TickCampaign(dt); return; }
        var c = Col[0];
        ref var queen = ref Ants[c.QueenId];
        if (Mission.Stage == ExpeditionStage.ClearTrail && Mission.Guards.TrueForAll(id => !Ants[id].Alive)) Mission.Stage = ExpeditionStage.EscortQueen;
        if (Mission.Stage == ExpeditionStage.EscortQueen && queen.Pos.DistanceTo(Expedition.Clearing) < 7) Mission.Stage = ExpeditionStage.DigEntrance;
        if (Mission.Stage == ExpeditionStage.RoyalChamber && c.Founded && queen.Layer == NestLayer(0) && queen.Pos.DistanceTo(ChamberPos(0, Royal(0))) < 1.4f)
        {
            Mission.Stage = ExpeditionStage.Incubator;
            // The founders follow the queen through the entrance before returning to forage.
            for (int i = 0; i < High; i++) if (Ants[i].Alive && Ants[i].Team == 0 && Ants[i].Type == AntType.Worker)
            { Ants[i].State = AntState.Move; Ants[i].Goal = ChamberPos(0, Royal(0)); Ants[i].GoalLayer = (byte)NestLayer(0); }
        }
        if (Mission.Stage == ExpeditionStage.Incubator && ChamberCount(0, ChamberKind.Brood, true) > 0) Mission.Stage = ExpeditionStage.Store;
        if (Mission.Stage == ExpeditionStage.Store && ChamberCount(0, ChamberKind.Storage, true) > 0) Mission.Stage = ExpeditionStage.RaiseWorkers;
        if (Mission.Stage == ExpeditionStage.RaiseWorkers && Mission.WorkersHatched >= Expedition.WorkerGoal) Mission.Stage = ExpeditionStage.RaiseSoldiers;
        if (Mission.Stage == ExpeditionStage.RaiseSoldiers && Mission.SoldiersHatched >= Expedition.SoldierGoal)
        { Mission.Stage = ExpeditionStage.Complete; Winner = 0; }
        if (c.Founded || Mission.EntranceOrdered) return;
        // Founders stay behind the fighting party, rather than foraging into the ambush.
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (!a.Alive || a.Team != 0 || a.Type != AntType.Worker) continue;
            a.State = AntState.Move;
            a.Goal = Expedition.Constrain(queen.Pos + new Vector2(-3 - i % 3, (i % 5 - 2) * 0.8f));
            a.GoalLayer = 0;
        }
    }

    Vector2 ExpeditionQueenWalk(ref Ant queen)
    {
        if (Mission.Stage != ExpeditionStage.EscortQueen) return Vector2.Zero;
        float front = queen.Pos.X;
        for (int i = 0; i < High; i++) if (Ants[i].Alive && Ants[i].Team == 0 && Config.IsWarrior(Ants[i].Type)) front = Mathf.Max(front, Ants[i].Pos.X - 5);
        var to = new Vector2(Mathf.Min(Expedition.Clearing.X, front), Expedition.Clearing.Y) - queen.Pos;
        return to.Length() > 0.7f ? to : Vector2.Zero;
    }
}
