using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

public enum ExpeditionStage { Alone, Gather, ClearTrail, FreeQueen, EscortQueen, Provision, DigEntrance, RoyalChamber, Incubator, Store, RaiseWorkers, RaiseSoldiers, Bridge, Fragment, Complete }

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
    public int RemainsCrew; // workers gripping the beetle right now (shown live; not part of the sim state)
    public float RemainsToGo; // how far the beetle still has to be dragged to the clearing
    public const int WorkerGoal = 4, SoldierGoal = 6;
    public static readonly Vector2 Start = new(48, 256), Ambush = new(88, 256), Clearing = new(144, 256);

    // The opening. The fall threw old Burr clear: he comes to alone at Wake, at the far end of the alley from the tube.
    // His soldiers lie stunned along it; a stunned ant wakes when a nestmate walks up to it. The queen and her workers
    // are alive inside the closed half of the broken tube, shut in: its mouth is stopped with the cotton plug and packed
    // sand. Nothing lies on them. The soldiers dig the mouth open from outside (many jaws dig far faster than one), and
    // the queen and the workers walk out.
    public static readonly Vector2 Wake = new(124, 256);
    internal static readonly Vector2[] SoldierSpots = { new(118.5f, 254.2f), new(114, 258.3f), new(110, 254.6f), new(106.5f, 257.6f), new(104, 255) };
    // the tube's closed half as the world builder lays it (BrokenTube.cs): its closed end, the way it points, its length
    public static readonly Vector2 TubeAt = new(33f, 253.6f);
    public const float TubeYaw = 0.18f, TubeLength = 12.5f, TubeBed = 4.6f; // TubeBed: where the colony's sand bed starts, past the water and its plug
    public static Vector2 TubeDir => DMath.Dir(TubeYaw);
    /// A point inside the tube: `along` its axis from the closed end, `side` across it.
    public static Vector2 InTube(float along, float side) { var d = TubeDir; return TubeAt + d * along + new Vector2(-d.Y, d.X) * side; }
    public static readonly Vector2 Mouth = InTube(TubeLength, 0), DigSite = InTube(TubeLength + 0.9f, 0), QueenCell = InTube(8.3f, 0);
    internal static readonly (float along, float side)[] WorkerCells = { (5.0f, -0.5f), (5.4f, 0.65f), (6.5f, -1.05f), (7.0f, 1.05f), (8.1f, -1.1f), (8.7f, 1.1f), (9.9f, -1.05f), (10.4f, 1.0f), (11.1f, -0.45f), (11.5f, 0.5f) };
    /// How far along the tube a point is if it is inside it (between the sand bed and a step past the mouth), else -1.
    public static float TubeAlong(Vector2 p)
    {
        var d = TubeDir; var q = p - TubeAt;
        float along = q.X * d.X + q.Y * d.Y, side = -q.X * d.Y + q.Y * d.X;
        return along >= TubeBed - 0.4f && along <= TubeLength + 1.6f && Mathf.Abs(side) <= 1.55f ? along : -1f;
    }
    public const float GateX = 102.5f;         // until Burr has his soldiers nobody goes nearer the beetle than this
    public const float WakeRadius = 4f, DigRadius = 5.5f;
    public const float DigAlone = 0.3f, DigEach = 0.9f; // percent a second: one ant alone, and each ant of a crew
    public int Strays, Joined;                 // the stunned soldiers on the trail, and how many have been woken
    public int Leader = -1;                    // old Burr: the others fall in behind him until the beetle is in sight
    public float Dug;                          // the queen's way out, 0..100
    public int Diggers;                        // ants at the dig right now (shown live)
    public bool QueenTrapped => Episode == 1 && Stage < ExpeditionStage.EscortQueen;
    /// The alley west of the gate is closed to the colony until the soldiers are gathered; and nobody walks through the
    /// stopped mouth of the tube until it is dug open.
    public Vector2 Gate(Vector2 p)
    {
        float min = Stage <= ExpeditionStage.Gather ? GateX : Stage <= ExpeditionStage.FreeQueen ? Mouth.X + 1.1f : float.MinValue;
        return p.X < min ? new Vector2(min, p.Y) : p;
    }
    public static float DigRate(int n) => n <= 0 ? 0 : n == 1 ? DigAlone : DigEach * n;
    /// The stunned nestmate the guide points at: the next one down the alley that can be reached now.
    public int NextStray(Sim s)
    {
        int best = -1; float bd = float.MaxValue;
        for (int i = 0; i < s.High; i++)
        {
            ref var a = ref s.Ants[i];
            if (!a.Alive || a.Team != 0 || !a.Dormant || a.Type != AntType.Soldier) continue;
            float d = Wake.X - a.Pos.X; // they lie in order along the alley, from Burr toward the tube
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    // walkable ground (Constrain, Contains) and the pond, straws and bridge: Bridge.cs

    public string Title => Episode > 1 ? StepTitle(Step) : Stage switch
    {
        ExpeditionStage.Alone => "Alone",
        ExpeditionStage.Gather => "Wake the colony",
        ExpeditionStage.ClearTrail => "Clear the grass alley",
        ExpeditionStage.FreeQueen => "Dig the tube open",
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
        ExpeditionStage.Alone => "The tube fell and broke, and old Burr was thrown clear. Click Burr to select him, then right-click the ground to walk. A soldier of his colony lies stunned just down the alley: walk Burr up to her.",
        ExpeditionStage.Gather => $"A stunned ant wakes when a nestmate walks up to it, and follows. Find the rest of Burr's soldiers along the alley. Woken: {Joined}/{SoldierSpots.Length}.",
        ExpeditionStage.ClearTrail => "Press F2 to select Burr's soldiers. A ground beetle blocks the alley: right-click it, or press A and click beyond it. Its shell is hard, so attack together.",
        ExpeditionStage.FreeQueen => "The queen and her workers are alive inside the broken tube, shut in: the cotton plug and packed sand stop its mouth. Click the tube's mouth and every ant comes to dig: "
            + $"they tear at it in turns. One ant alone is very slow; every extra pair of jaws digs faster. Dug open: {(int)Dug}%.",
        ExpeditionStage.EscortQueen => "With your attackers selected, right-click the food clearing. The queen and workers follow them along the protected trail.",
        ExpeditionStage.Provision => "Why wait? A queen won't found a nest until the site holds food and protein for her first brood, and the fall lost everything the tube held. "
            + StockNeeded + ". "
            + (RemainsLeft ? "Workers fetch seeds by themselves. The protein is the beetle Burr's soldiers killed: send workers to it (F3, then right-click the body). It is too heavy for one ant: four or more drag it home whole."
                           : "Workers are gathering: the nest can be dug as soon as both bars are full."),
        ExpeditionStage.DigEntrance => "The site is stocked. Press DIG THE NEST at the top (or Enter): the workers at the clearing dig the shaft and the royal chamber.",
        ExpeditionStage.RoyalChamber => "Workers are excavating. The queen will enter the royal chamber as soon as it is ready.",
        ExpeditionStage.Incubator => "Press N, choose Incubator, then click beside the royal chamber. This incubator hatches eggs and adds 50 population capacity.",
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
    public const float SeedFood = 6f; // what one seed brings in the campaign (Config.FoodValue)
    /// What the founding site still lacks, in plain words: "Still needed: 3 more seeds and the beetle's body".
    public string StockNeeded
    {
        get
        {
            int seeds = Mathf.CeilToInt(Mathf.Max(0, Config.FoundFood - StockFood) / SeedFood);
            float org = Mathf.Max(0, Config.FoundOrganic - StockOrganic);
            var parts = new List<string>();
            if (seeds > 0) parts.Add($"{seeds} more seed{(seeds == 1 ? "" : "s")}");
            if (org > 0) parts.Add(RemainsLeft ? "the beetle's body" : $"{Mathf.CeilToInt(org)} more remains");
            return parts.Count == 0 ? "The site is stocked" : "Still needed: " + string.Join(" and ", parts);
        }
    }
    public Vector2 Marker => Episode > 1 ? MarkerPos : Stage switch
    {
        ExpeditionStage.Alone or ExpeditionStage.Gather => Wake,
        ExpeditionStage.ClearTrail => Ambush,
        ExpeditionStage.FreeQueen => DigSite,
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
        int workers = 0;
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
            // the queen and her workers are inside the tube's closed half, on its sand bed, shut in behind the stopped mouth
            var cell = Expedition.WorkerCells[workers % Expedition.WorkerCells.Length];
            a.Pos = a.PrevPos = a.Type == AntType.Queen ? Expedition.QueenCell : Expedition.InTube(cell.along, cell.side);
            a.Heading = a.PrevHeading = a.Type == AntType.Queen ? Expedition.TubeYaw : Expedition.TubeYaw + (workers % 3 - 1) * 0.5f + (workers % 2) * DMath.Pi;
            if (a.Type != AntType.Queen) workers++;
            a.Anchor = a.Goal = a.Pos; a.State = AntState.Hold; a.Dormant = true;
        }
        Col[1].Alive = false; Col[1].Chambers.Clear();
        // old Burr, the tube's eldest soldier, comes to alone; the other soldiers lie stunned between him and the beetle
        Mission.Leader = Spawn(0, AntType.Soldier, Expedition.Wake, DMath.Pi);
        Ants[Mission.Leader].State = AntState.Hold;
        for (int i = 0; i < Expedition.SoldierSpots.Length; i++)
        {
            int id = Spawn(0, AntType.Soldier, Expedition.SoldierSpots[i], DMath.Tau * ((i * 61 + 13) % 100) / 100f);
            Ants[id].State = AntState.Hold; Ants[id].Dormant = true;
            Mission.Strays++;
        }
        c.PopCap = 0;
        for (int type = 0; type < c.Count.Length; type++) c.PopCap += c.Count[type] * Config.Stats[type].Pop;
        // the stunned are not yet the colony's to count: each joins it when it wakes (WakeStray)
        for (int i = 0; i < High; i++) if (Ants[i].Alive && Ants[i].Team == 0 && Ants[i].Dormant && Ants[i].Type != AntType.Queen) c.Count[(int)Ants[i].Type]--;
        // a ground beetle blocks the alley: the first wild bug, and the colony's first fight (no red ants in Act I)
        {
            int id = Spawn(Config.Wild, AntType.Beetle, Expedition.Ambush, 0); // it faces the colony coming down the alley
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
            c.Mutations = (int)(Mutation.Winged | Mutation.Stinger | Mutation.Web | Mutation.Armoured); // every core already taken
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
        if (Mission.Stage <= ExpeditionStage.FreeQueen) TickOpening(dt, ref queen);
        if (Mission.Stage == ExpeditionStage.EscortQueen && queen.Pos.DistanceTo(Expedition.Clearing) < 7)
        {
            // the founders spread out to stock the site: seeds from the clearing, and the beetle's remains dragged in
            Mission.Stage = ExpeditionStage.Provision;
            for (int i = 0; i < High; i++) if (Ants[i].Alive && Ants[i].Team == 0 && Ants[i].Type == AntType.Worker) { Ants[i].State = AntState.Forage; Ants[i].Goal = Ants[i].Pos; }
        }
        if (Mission.Stage == ExpeditionStage.Provision)
        {
            Mission.StockFood = (int)c.Food; Mission.StockOrganic = (int)c.Organic;
            var remains = BestRemains(0, Expedition.Ambush, 70f); // reaches the clearing: the beetle is tracked all the way home
            Mission.RemainsLeft = remains != null && !remains.Hauled;
            if (remains != null) { Mission.RemainsPos = remains.Pos; Mission.RemainsCrew = remains.HaulCount; Mission.RemainsToGo = Mathf.Max(0, remains.Pos.DistanceTo(Expedition.Clearing) - Config.HaulArrive - remains.Radius); }
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
        if (Mission.Stage == ExpeditionStage.Fragment && CoreById(Mission.GlandId) is { Home: true })
        { Mission.Stage = ExpeditionStage.Complete; Winner = 0; }
        if (c.Founded || Mission.EntranceOrdered || Mission.Stage == ExpeditionStage.Provision) return;
        // Founders stay behind the fighting party, rather than foraging into the ambush.
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (!a.Alive || a.Team != 0 || a.Type != AntType.Worker || a.Dormant) continue;
            a.State = AntState.Move;
            a.Goal = Mission.Constrain(queen.Pos + new Vector2(-3 - i % 3, (i % 5 - 2) * 0.8f));
            a.GoalLayer = 0;
        }
    }

    /// Level 1's opening: Burr alone, the stunned nestmates he wakes, the beetle, and the queen dug out of the tube.
    void TickOpening(float dt, ref Ant queen)
    {
        var m = Mission;
        if (Ticks % 30 == 0)
        {
            // nobody left standing to wake the rest or dig: nobody will open the tube
            bool anyone = false;
            for (int i = 0; i < High && !anyone; i++) anyone = Ants[i].Alive && Ants[i].Team == 0 && !Ants[i].Dormant;
            if (!anyone) { m.LossReason = "Every ant that could still stand is dead. Inside the stopped tube, the queen's call goes unanswered."; Winner = Config.Enemy; return; }
        }
        // a stunned ant wakes when an awake nestmate reaches it; a woken worker runs to its queen (the loop in TickExpedition)
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (!a.Alive || a.Team != 0 || !a.Dormant || a.Type != AntType.Soldier) continue; // those in the tube are shut in, not stunned
            bool near = false;
            for (int j = 0; j < High && !near; j++)
                near = j != i && Ants[j].Alive && Ants[j].Team == 0 && !Ants[j].Dormant && Ants[j].Pos.DistanceSquaredTo(a.Pos) < Expedition.WakeRadius * Expedition.WakeRadius;
            if (near) WakeStray(i);
        }
        if (m.Stage == ExpeditionStage.Alone && m.Joined > 0) m.Stage = ExpeditionStage.Gather;
        if (m.Stage == ExpeditionStage.Gather && m.NextStray(this) < 0) m.Stage = ExpeditionStage.ClearTrail;
        if (m.Stage <= ExpeditionStage.ClearTrail && Ticks % 15 == 0)
        {
            // the woken soldiers fall in behind Burr until the beetle is dead (an order of the player's own comes first)
            if (!Ants[m.Leader].Alive || Ants[m.Leader].Team != 0 || Ants[m.Leader].Type != AntType.Soldier)
                for (int i = 0; i < High; i++) if (Ants[i].Alive && Ants[i].Team == 0 && !Ants[i].Dormant && Ants[i].Type == AntType.Soldier) { m.Leader = i; break; }
            var lead = Ants[m.Leader].Pos;
            int k = 0;
            for (int i = 0; i < High; i++)
            {
                ref var a = ref Ants[i];
                if (i == m.Leader || !a.Alive || a.Team != 0 || a.Dormant || a.Type != AntType.Soldier) continue;
                k++;
                if (a.State is not (AntState.Idle or AntState.Hold) || a.Pos.DistanceSquaredTo(lead) < 36f) continue;
                a.State = AntState.Move; a.GoalLayer = 0;
                a.Goal = m.Constrain(m.Gate(lead + new Vector2(2.2f + 1.5f * (k / 2), (k % 2 == 0 ? 1 : -1) * (1.2f + 0.5f * k))));
            }
        }
        if (m.Stage == ExpeditionStage.ClearTrail && m.Guards.TrueForAll(id => !Ants[id].Alive)) m.Stage = ExpeditionStage.FreeQueen;
        if (m.Stage != ExpeditionStage.FreeQueen) return;
        // the dig: every awake ant at the tube's mouth that is not fighting works at the cotton and the packed sand
        int n = 0;
        float r2 = Expedition.DigRadius * Expedition.DigRadius;
        var site = Expedition.Mouth;
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (!a.Alive || a.Team != 0 || a.Dormant || a.State == AntState.Engage || a.Pos.DistanceSquaredTo(Expedition.DigSite) > r2) continue;
            n++;
            // each digger has a place in a fan around the plug and works in turns: it tears at the cotton and sand
            // for a few seconds, then backs out a few steps with its mouthful and comes in again
            int turn = (Ticks + i * 53) % 210;
            bool hauling = turn >= 150;
            float fan = ((i * 5) % 7 - 3) * 0.42f;
            var outward = (Expedition.DigSite - site).Normalized();
            var side = new Vector2(-outward.Y, outward.X);
            var slot = site + outward * (hauling ? 3.4f : 0.9f + (i % 2) * 0.55f) + side * fan * (hauling ? 2.2f : 1f);
            if (a.State is AntState.Idle or AntState.Hold or AntState.Move && a.Goal.DistanceSquaredTo(Expedition.DigSite) < r2)
            {
                if (a.Pos.DistanceSquaredTo(slot) > 0.2f) { a.State = AntState.Move; a.Goal = slot; a.GoalLayer = 0; }
            }
            if (a.Speed > 0.5f || hauling) continue;
            // jaws in the plug: it faces it and bites, each to its own beat
            a.Heading = (site - a.Pos).AngleD();
            if ((Ticks + i * 7) % 11 == 0) a.AttackAnim = 1f;
        }
        m.Diggers = n;
        m.Dug = Mathf.Min(100f, m.Dug + Expedition.DigRate(n) * dt);
        if (m.Dug < 100f) return;
        // the mouth is open: the queen and her workers walk out, and anyone still lying in the sand rises at her call
        queen.Dormant = false; queen.State = AntState.Idle;
        for (int i = 0; i < High; i++) if (Ants[i].Alive && Ants[i].Team == 0 && Ants[i].Dormant) WakeStray(i);
        m.Stage = ExpeditionStage.EscortQueen;
        Events.Add(new SimEvent { Kind = EvKind.QueenFreed, Pos = queen.Pos, Team = 0 });
    }

    void WakeStray(int i)
    {
        ref var a = ref Ants[i];
        a.Dormant = false;
        a.State = a.Type == AntType.Worker ? AntState.Move : AntState.Idle;
        a.Anchor = a.Goal = a.Pos; a.GoalLayer = 0;
        if (a.Type == AntType.Soldier) Mission.Joined++;
        Col[0].Count[(int)a.Type]++;
        Events.Add(new SimEvent { Kind = EvKind.Woke, Pos = a.Pos, Team = 0, Type = a.Type, HatchP1 = i + 1 });
    }

    /// Tests and captures that start after the opening: everyone is awake and the queen is out of the tube.
    public void SkipOpening()
    {
        if (Mission is not { Episode: 1 } m || !m.QueenTrapped) return;
        foreach (int g in m.Guards) Slay(g);
        for (int i = 0; i < High; i++)
        {
            if (!Ants[i].Alive || Ants[i].Team != 0 || !Ants[i].Dormant) continue;
            if (Ants[i].Type == AntType.Queen) { Ants[i].Dormant = false; Ants[i].State = AntState.Idle; } else WakeStray(i);
        }
        m.Dug = 100; m.Stage = ExpeditionStage.EscortQueen;
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
