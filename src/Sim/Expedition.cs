using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

public enum ExpeditionStage { ClearTrail, EscortQueen, Provision, DigEntrance, RoyalChamber, Incubator, Store, RaiseWorkers, RaiseSoldiers, Bridge, Fragment, Complete }

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
    // the founding site's stock: what the founders have brought to the clearing, and where the hunt's remains lie
    public int StockFood, StockOrganic;
    public Vector2 RemainsPos;
    public bool RemainsLeft;
    public const int WorkerGoal = 4, SoldierGoal = 6;
    public static readonly Vector2 Start = new(48, 256), Ambush = new(88, 256), Clearing = new(144, 256);

    // walkable ground (Constrain, Contains) and the pond, straws and bridge: Bridge.cs

    public string Title => Episode > 1 ? StepTitle(Step) : Stage switch
    {
        ExpeditionStage.ClearTrail => "Clear the grass alley",
        ExpeditionStage.EscortQueen => "Lead the queen to food",
        ExpeditionStage.Provision => "Stock the founding site",
        ExpeditionStage.DigEntrance => "Choose your workers",
        ExpeditionStage.RoyalChamber => "A home for the queen",
        ExpeditionStage.Incubator => "Build your first incubator",
        ExpeditionStage.Store => "Store the harvest",
        ExpeditionStage.RaiseWorkers => "Grow the workforce",
        ExpeditionStage.RaiseSoldiers => "Raise a fighting squad",
        ExpeditionStage.Bridge => "Bridge the water together",
        ExpeditionStage.Fragment => GlandId < 0 ? "The mutated queen" : "The queen's gland",
        _ => "The first nest is thriving",
    };
    public string Instruction => Episode > 1 ? StepInstruction : Stage switch
    {
        ExpeditionStage.ClearTrail => "Press F2 to select Burr's soldiers. A ground beetle blocks the alley: right-click it, or press A and click beyond it. Its shell is hard, so attack together.",
        ExpeditionStage.EscortQueen => "With your attackers selected, right-click the food clearing. The queen and workers follow them along the protected trail.",
        ExpeditionStage.Provision => "A queen can only found her nest where the colony has stocked both food and remains: everything the tube held was lost in the fall. "
            + "The two bars at the top show what the site still needs. "
            + (RemainsLeft ? "Workers gather seeds by themselves; send some to the beetle's remains (F3, then right-click them)." : "Workers are gathering: wait for both bars to fill."),
        ExpeditionStage.DigEntrance => "The site is stocked. Press DIG THE NEST at the top (or Enter): the workers at the clearing dig the shaft and the royal chamber.",
        ExpeditionStage.RoyalChamber => "Workers are excavating. The queen will enter the royal chamber as soon as it is ready.",
        ExpeditionStage.Incubator => "Press N, choose Brood Chamber, then click beside the royal chamber. This incubator hatches eggs and adds 50 population capacity.",
        ExpeditionStage.Store => "Choose Storage and click beside a connected room. Workers gather seeds and insect protein and carry them home.",
        ExpeditionStage.RaiseWorkers => $"Hatch {WorkerGoal} new workers with Z or the Worker portrait. Hatched: {WorkersHatched}/{WorkerGoal}. New workers leave the incubator and gather food.",
        ExpeditionStage.RaiseSoldiers => $"Hatch one squad with X or the Soldier portrait. Hatched: {SoldiersHatched}/{SoldierGoal}. This episode ends when your colony can support its first army.",
        ExpeditionStage.Bridge => $"Across the pond to the east lie a dead queen and a second broken tube. Water stops every ant: build a bridge of straws. "
            + $"A straw is too long for one ant. Select two or more workers (F3) and right-click a straw: they carry it together, and more workers carry it faster. Straws laid: {StrawsLaid}/{StrawGoal}.",
        ExpeditionStage.Fragment => GlandId < 0
            ? "The bridge holds. On the island a queen of another strain still lives among her dead: wounded, but the glass mutated her, and her bite kills. Send Burr's soldiers across together (F2, then right-click her)."
            : "She is dead. Send workers to her body (F3, then right-click the glowing gland): they carve it out, and one carries it home to the queen.",
        _ => "Level complete. Sweet Rain is now open on the colony map; Skirmish lets you try the complete roster now.",
    };
    public Vector2 Marker => Episode > 1 ? MarkerPos : Stage switch
    {
        ExpeditionStage.ClearTrail => Ambush,
        ExpeditionStage.Provision when RemainsLeft => RemainsPos,
        ExpeditionStage.Bridge => NextStraw?.Pos ?? BridgeSite,
        ExpeditionStage.Fragment => QueenAt,
        _ => Clearing,
    };
    /// The straw the guide points at: one already moving, else the nearest one still on the shore.
    public Straw NextStraw
    {
        get
        {
            Straw best = null;
            foreach (var s in Straws) if (!s.Laid && (best == null || s.HoldCount > best.HoldCount || s.HoldCount == best.HoldCount && s.Pos.X > best.Pos.X)) best = s;
            return best;
        }
    }
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
        // the fall lost everything: the founders must stock the site with food and the hunt's remains (Config.FoundFood, FoundOrganic)
        c.Food = 0;
        c.Organic = 0;
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
        // a ground beetle blocks the alley: the first wild bug, and the colony's first fight (no red ants in Act I)
        {
            int id = Spawn(Config.Wild, AntType.Beetle, Expedition.Ambush, Mathf.Pi);
            Ants[id].State = AntState.Hold; Ants[id].Home = Expedition.Ambush;
            Mission.Guards.Add(id);
        }
        HideUnrevealedWildlife();
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
        if (Mission.Stage == ExpeditionStage.EscortQueen && queen.Pos.DistanceTo(Expedition.Clearing) < 7)
        {
            // the founders spread out to stock the site: seeds from the clearing, and the beetle's remains dragged in
            Mission.Stage = ExpeditionStage.Provision;
            for (int i = 0; i < High; i++) if (Ants[i].Alive && Ants[i].Team == 0 && Ants[i].Type == AntType.Worker) { Ants[i].State = AntState.Forage; Ants[i].Goal = Ants[i].Pos; }
        }
        if (Mission.Stage == ExpeditionStage.Provision)
        {
            Mission.StockFood = (int)c.Food; Mission.StockOrganic = (int)c.Organic;
            var remains = BestRemains(0, Expedition.Ambush, 40f);
            Mission.RemainsLeft = remains != null && !remains.Hauled;
            if (remains != null) Mission.RemainsPos = remains.Pos;
            // the founders fetch the beetle themselves while the site lacks remains: half the workers haul, half gather seeds
            // (a player who orders it sooner only speeds it up; nobody gets stuck on a step the game never explained)
            if (remains != null && c.Organic < Config.FoundOrganic && Ticks % 30 == 0)
            {
                int workers = 0, hauling = 0;
                var free = new List<int>();
                for (int i = 0; i < High; i++)
                {
                    ref var a = ref Ants[i];
                    if (!a.Alive || a.Team != 0 || a.Type != AntType.Worker) continue;
                    workers++;
                    if (a.Assigned == remains) hauling++;
                    else if (!a.Carrying && a.State is AntState.Forage or AntState.Idle) free.Add(i);
                }
                int want = (workers + 1) / 2 - hauling;
                if (want > 0 && free.Count > 0) CmdGather(free.GetRange(0, System.Math.Min(want, free.Count)), remains);
            }
            if (CanFound(0)) Mission.Stage = ExpeditionStage.DigEntrance;
        }
        if (Mission.Stage == ExpeditionStage.RoyalChamber && c.Founded && queen.Layer == NestLayer(0) && queen.Pos.DistanceTo(ChamberPos(0, Royal(0))) < 1.4f)
        {
            Mission.Stage = ExpeditionStage.Incubator;
            // The founders follow the queen through the entrance before returning to forage.
            for (int i = 0; i < High; i++) if (Ants[i].Alive && Ants[i].Team == 0 && Ants[i].Type == AntType.Worker)
            { Ants[i].State = AntState.Move; Ants[i].Goal = ChamberPos(0, Royal(0)); Ants[i].GoalLayer = (byte)NestLayer(0); }
        }
        if (Mission.Stage == ExpeditionStage.Incubator && ChamberCount(0, ChamberKind.Brood, true) > 0) Mission.Stage = ExpeditionStage.Store;
        if (Mission.Stage == ExpeditionStage.Store && ChamberCount(0, ChamberKind.Storage, true) > 0) Mission.Stage = ExpeditionStage.RaiseWorkers;
        // the soldier caste is Chapter 2's lesson: the founding ends with the first workers
        // then the teamwork lesson: a straw bridge to the island, and the dead queen's gland carried home
        if (Mission.Stage == ExpeditionStage.RaiseWorkers && Mission.WorkersHatched >= Expedition.WorkerGoal) BeginBridge();
        if (Mission.Stage == ExpeditionStage.Bridge) TickBridge(dt);
        if (Mission.Stage == ExpeditionStage.Fragment) TickFragment();
        if (Mission.Stage == ExpeditionStage.Fragment && MutagenById(Mission.GlandId) is { Home: true })
        { Mission.Stage = ExpeditionStage.Complete; Winner = 0; }
        if (c.Founded || Mission.EntranceOrdered || Mission.Stage == ExpeditionStage.Provision) return;
        // Founders stay behind the fighting party, rather than foraging into the ambush.
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (!a.Alive || a.Team != 0 || a.Type != AntType.Worker) continue;
            a.State = AntState.Move;
            a.Goal = Mission.Constrain(queen.Pos + new Vector2(-3 - i % 3, (i % 5 - 2) * 0.8f));
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
