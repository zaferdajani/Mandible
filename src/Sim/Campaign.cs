using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Names and story text of the four campaign episodes (shown by the menu, the briefing and the end card).
public static class CampaignEpisodes
{
    public const int Count = Levels.Count; // every level on the map; ids 1-4 are the episodes, 5-12 Levels.cs

    public static string Name(int ep) => Levels.Info(ep).Name;

    public static string Blurb(int ep) => Levels.Info(ep).Blurb;

    public static string[] Intro(int ep) => Levels.Intro(ep);

    public static string Outro(int ep) => Levels.Outro(ep);
}

public sealed partial class Expedition
{
    // ---------------------------------------------------------------- episodes 2-4
    public int Step;                        // the first unfinished objective
    public int Have, Need;                  // progress of that objective, for the guide panel
    public Vector2 MarkerPos;
    public string LossReason = "Your queen is dead. The colony scatters into the grass.";
    public readonly List<(int id, int serial)> Raiders = new(), HerdGuards = new(), Post = new();
    public int RaidsSent, RaidWaves;
    public float RaidAt = -1, RaidGap;
    public Vector2 Lair, PostPos;
    public float GatherBase;
    public float FlightAt;
    public int RaidersLeft, PostLeft, GuardsLeft;

    public const float StagHp = 2f;            // the campaign's Old Stag: half Lane War's boss health
    public const float PostHp = 0.45f, PostDmg = 2.2f; // campaign guardians against Lane War's (9-14x health, 3.2x bite)
    public const float MilkTime = 45f, HerdFood = 250f, Honeydew = 1f;
    public const float GatherGoal = 800f;      // carried home by foragers (honeydew does not count)
    public const float FlightTime = 1800f;     // the finale's nuptial flight leaves after 30 minutes
    public const float ScoutRange = 45f;       // close enough to the red thatch to count its guards
    public const float CorridorClear = 26f;    // the foragers' highway: no wild lairs this close to the line between the mounds

    public bool Confined => Episode == 1;
    public bool ShieldsRival => Episode is 4 or 11 or 12;
    public int Steps => IsLevel ? Objectives.Count : Episode switch { 1 => 0, 4 => 4, _ => 5 };
    /// Steps finished when the red queen fell (the rest became optional the moment she died).
    public int StepsDone;
    public bool Complete => Episode == 1 ? Stage == ExpeditionStage.Complete : Step >= Steps;

    /// The episode's main victory, always on show: every step leads here.
    public string Goal => IsLevel ? LevelGoal : Episode switch
    {
        1 => "Found the Glass Queen's nest. One day its army will march on the red queens.",
        2 => "Kill the young red queen in her royal chamber.",
        _ => "Kill the red queen in her royal chamber.",
    };

    // what the player may hatch, dig and research: Reveal (CampaignReveal.cs), one chapter at a time

    public string StepTitle(int i) => IsLevel ? LevelStepTitle(i) : (Episode, i) switch
    {
        (2, 0) => "Dig a store",
        (2, 1) => "Win the aphid herd",
        (2, 2) => "Fill the galleries",
        (2, 3) => "Survive the lair raid",
        (2, 4) => "Kill the young red queen",
        (3, 0) => "Scout the red mound",
        (3, 1) => "Hold your mound",
        (3, 2) => "Grow stronger",
        (3, 3) => "Break the outpost",
        (3, 4) => "Kill the red queen",
        (4, 0) => "Raise a hero",
        (4, 1) => "Rouse the Old Stag",
        (4, 2) => "Break the mound guardians",
        (4, 3) => "Kill the red queen",
        _ => "Episode complete",
    };

    string StepInstruction => IsLevel ? LevelStepInstruction : (Episode, Step) switch
    {
        (2, 0) => "Open the Chambers tab (N), choose Storage and click beside a connected room. A store holds 400 more food.",
        (2, 1) => GuardsLeft > 0
            ? "A ground beetle grazes beside the aphids on the clover stem. Press F2 for your squads and attack it together: alone, a soldier is crushed."
            : $"Keep at least three ants at the clover stem with no bugs nearby while they stroke the aphids for honeydew. Milked: {Have}%.",
        (2, 2) => $"Hatch more workers (Z) and let them forage. Tended aphids drip honeydew while two ants stay with them. Gathered: {Have}/{Need}.",
        (2, 3) => RaidsSent == 0
            ? $"Bugs are stirring in a lair to the east. Hatch soldiers (X) and gather them at the mound. They arrive in {Have}s."
            : $"The raiders are breaking for the shaft and your queen. Attack-move your army onto them. Raiders left: {Have}/{Need}.",
        (3, 0) => $"Send a squad down the foragers' highway toward the red thatch in the far corner until it sees their mound. Distance: {Have}.",
        (3, 1) => $"The fire ants answer with raids that go down your shaft for the queen. Keep your soldiers home and destroy every raider. Waves beaten: {Have}/{Need}.",
        (3, 2) => "Open the Research tab: feed the queen royal jelly to make her a Matriarch (majors unlock), or dig a Mandible Forge or a Chitin Armory.",
        (3, 3) => $"Mound guards grown into the ground hold the middle. They never move but hit hard and mend when left alone: strike with everything. Guards left: {Have}.",
        (2, 4) or (3, 4) => "Gather your army, attack-move onto the red mound and send everyone down the shaft: the queen waits in her royal chamber. She is huge and mends between fights, so keep fresh squads coming.",
        (4, 0) => "Open the Heroes tab and raise the Warlord (Y). Heroes level up quickly and fight with special moves.",
        (4, 1) => $"The Old Stag prowls by the great raspberry in the middle. Beat it with your whole army and it joins your colony. Its strength: {Have}%.",
        (4, 2) => $"While the guardians at the red entrance stand, their queen cannot be harmed. Lead the Old Stag and your army at them: its bite shatters mound guards. Left: {Have}.",
        (4, 3) => "The way is open. Send everything down the red shaft and kill the queen in her royal chamber before the princesses fly.",
        _ => "Episode complete.",
    };
}

public sealed partial class Sim
{
    /// Sets up a campaign episode after Episode 1 on the open yard (call once, right after construction).
    public void BeginEpisode(int episode)
    {
        Mission = new Expedition { Episode = episode };
        var m = Mission;
        var home = W.Nests[0].Pos;
        var foe = W.Nests[1].Pos;
        var toFoe = (foe - home).Normalized();
        var side = new Vector2(toFoe.Y, -toFoe.X);
        var player = Col[0];
        if (episode >= 5) BeginLevel(episode);
        else if (episode == 2)
        {
            ClearCorridor(home, foe, -1); // first: the herd's own beetle grazes beside the highway
            SettleColony(0, brood: 1, stores: 0);
            player.Food = 200;
            Muster(0, AntType.Worker, 2, home);
            Muster(0, AntType.Soldier, 12, home + toFoe * 9);
            Pasture = OpenGround(home + toFoe * 38 + side * 14);
            HerdHere = true; HerdTeam = -1; HerdBar = 0;
            for (int i = 0; i < 1; i++)
            {
                int id = Spawn(Config.Wild, AntType.Beetle, Pasture + toFoe * 4, (home - Pasture).AngleD());
                Ants[id].State = AntState.Idle; Ants[id].Home = Pasture;
                m.HerdGuards.Add((id, Ants[id].Serial));
            }
            m.Lair = OpenGround(home + toFoe * 100 - side * 40);
            m.RaidWaves = 1;
            // a young red queen has just dug in across the yard: a few workers and one squad
            SettleColony(1, brood: 1, stores: 0);
            Col[1].Food = 100;
            Muster(1, AntType.Soldier, 6, foe - toFoe * 9);
        }
        else if (episode == 3)
        {
            SettleColony(0, brood: 2, stores: 1);
            player.Food = 400;
            Muster(0, AntType.Worker, 8, home);
            Muster(0, AntType.Soldier, 12, home + toFoe * 9);
            SettleColony(1, brood: 2, stores: 1);
            Col[1].Food = 300;
            Muster(1, AntType.Worker, 10, foe);
            Muster(1, AntType.Soldier, 12, foe - toFoe * 9);
            m.PostPos = OutpostSite(home, foe, side);
            m.Post.Add(Track(SpawnGuardian(1, m.PostPos, AntType.Major)));
            m.Post.Add(Track(SpawnGuardian(1, m.PostPos + toFoe * 3.2f + side * 2.5f, AntType.Gunner)));
            m.Post.Add(Track(SpawnGuardian(1, m.PostPos + toFoe * 3.2f - side * 2.5f, AntType.Gunner)));
            m.RaidWaves = 2; m.RaidGap = 50;
            ClearCorridor(home, foe, -1);
        }
        else
        {
            player.Tier[(int)Upgrade.Queen] = 2;
            SettleColony(0, brood: 3, stores: 2);
            player.Food = 700;
            Muster(0, AntType.Worker, 20, home);
            Muster(0, AntType.Soldier, 24, home + toFoe * 9);
            Muster(0, AntType.Major, 3, home + toFoe * 12);
            Col[1].Tier[(int)Upgrade.Queen] = 2;
            SettleColony(1, brood: 3, stores: 2);
            Col[1].Food = 500;
            Muster(1, AntType.Worker, 20, foe);
            Muster(1, AntType.Soldier, 12, foe - toFoe * 9);
            Muster(1, AntType.Major, 3, foe - toFoe * 12);
            SpawnNestGuardians(1);
            // the stag lord by the great raspberry becomes the Old Stag: tougher, slower to mend, roused when beaten
            var centre = new Vector2(Config.MapSize, Config.MapSize) * 0.5f;
            int stag = -1;
            for (int i = 0; i < High; i++)
                if (Ants[i].Alive && Ants[i].Team == Config.Wild && Ants[i].Type == AntType.StagBeetle && (stag < 0 || Ants[i].Pos.DistanceTo(centre) < Ants[stag].Pos.DistanceTo(centre))) stag = i;
            if (stag < 0) { stag = Spawn(Config.Wild, AntType.StagBeetle, OpenGround(centre + new Vector2(8, 8)), 0); Ants[stag].State = AntState.Idle; }
            ref var s = ref Ants[stag];
            s.Level = 2; s.Xp = Config.XpFor(s.Type, 2); s.Boss = true;
            ApplyLevel(ref s); s.Hp = s.MaxHp;
            StagId = stag; StagSerial = s.Serial; StagTeam = -1; StagLair = s.Home;
            ClearCorridor(home, foe, stag);
            m.FlightAt = Expedition.FlightTime;
            m.RaidWaves = 4; m.RaidGap = 240; m.RaidAt = 480;
        }
        HideUnrevealedWildlife();
        foreach (var c in Col) { c.PopUsed = 0; for (int t = 0; t < c.Count.Length; t++) c.PopUsed += c.Count[t] * Config.Stats[t].Pop; }
        m.GatherBase = player.Gathered;
        UpdateCampaign();
    }

    /// The colonies' foragers have worn a highway between the mounds: no wild lair sits on it (except `keep`).
    void ClearCorridor(Vector2 home, Vector2 foe, int keep)
    {
        for (int i = 0; i < High; i++)
        {
            ref var b = ref Ants[i];
            if (!b.Alive || b.Team != Config.Wild || i == keep) continue;
            float along = Mathf.Clamp((b.Home - home).Dot(foe - home) / (foe - home).LengthSquared(), 0, 1);
            if (b.Home.DistanceTo(home.Lerp(foe, along)) > Expedition.CorridorClear) continue;
            b.Alive = false; WildCount--; _free.Push(i);
        }
    }

    /// The middle ground's outpost: about a third of the way to the red mound, clear of rocks and of every wild lair.
    Vector2 OutpostSite(Vector2 home, Vector2 foe, Vector2 side)
    {
        foreach (float along in new[] { 0.36f, 0.32f, 0.4f, 0.28f, 0.44f })
            foreach (float off in new[] { 18f, 26f, 10f, 34f, -18f, -26f })
            {
                var p = OpenGround(home.Lerp(foe, along) + side * off);
                bool clear = true;
                foreach (var camp in W.Camps) if (camp.Pos.DistanceTo(p) < 30) { clear = false; break; }
                if (clear) return p;
            }
        return OpenGround(home.Lerp(foe, 0.36f) + side * 18);
    }

    /// The campaign's rival lives on a leaner diet than a Colony War opponent: it grows and guards, it does not swamp.
    public static float RivalFoodMult(int episode) => Levels.Info(episode).RivalFood;

    (int, int) Track(int id) => (id, id >= 0 ? Ants[id].Serial : 0);
    int Living(List<(int id, int serial)> list) { int n = 0; foreach (var (id, serial) in list) if (Valid(id, serial)) n++; return n; }

    /// A colony that has already dug in: the royal chamber, `brood` incubators and `stores` stores, the queen below.
    void SettleColony(int team, int brood, int stores)
    {
        var c = Col[team];
        c.Chambers[0].Done = c.Chambers[0].Work; c.Chambers[0].Built = true;
        c.Chambers[0].Tier = c.T(Upgrade.Queen);
        c.Founded = true;
        for (int i = 0; i < brood; i++) AddChamber(team, ChamberKind.Brood, BroodWork, true);
        for (int i = 0; i < stores; i++) AddChamber(team, ChamberKind.Storage, StorageWork, true);
        RecountCapacity(c);
        ref var q = ref Ants[c.QueenId];
        ApplyLevel(ref q); q.Hp = q.MaxHp;
        q.Layer = (byte)NestLayer(team);
        q.Pos = q.PrevPos = q.Anchor = ChamberPos(team, Royal(team));
    }

    /// Extra ants that start the episode beside the mound; warriors are formed into squads.
    List<int> Muster(int team, AntType type, int n, Vector2 at)
    {
        var ids = new List<int>();
        var face = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - at).AngleD();
        for (int i = 0; i < n; i++)
        {
            int id = Spawn(team, type, OpenGround(at + Rng.InDisk(type == AntType.Worker ? 5 : 3.5f)), face);
            if (id < 0) break;
            if (type != AntType.Worker) Ants[id].State = AntState.Idle;
            ids.Add(id);
        }
        FormSquads(ids);
        return ids;
    }

    void TickCampaign(float dt)
    {
        var m = Mission;
        if (m.IsLevel) { TickLevel(dt); return; }
        if (m.Episode == 2 && HerdHere) TickHerd(dt);
        // the finale's rival stops raiding once its mound guardians fall: everything stays home to guard the queen
        bool raiding = m.Episode == 2 || Col[1].Alive && (m.Episode != 4 || GuardiansLeft(1) > 0);
        if (m.RaidAt >= 0 && Time >= m.RaidAt && m.RaidsSent < m.RaidWaves && raiding)
        {
            LaunchRaid(m.RaidsSent++);
            m.RaidAt = m.RaidsSent < m.RaidWaves ? Time + m.RaidGap : -1;
        }
        if (m.Episode == 4 && Col[1].Alive && Time >= m.FlightAt)
        {
            m.LossReason = "The red princesses took their nuptial flight. Red queens will rule every corner of the garden.";
            Winner = Config.Enemy;
            return;
        }
        UpdateCampaign();
        while (m.Step < m.Steps && StepDone(m.Step))
        {
            m.Step++;
            if (m.Episode == 2 && m.Step == 3) m.RaidAt = Time + 40; // the raid is sighted: a little time to muster
            if (m.Episode == 3 && m.Step == 1) m.RaidAt = Time + 6;   // the scout was seen
            UpdateCampaign();
        }
    }

    bool StepDone(int step)
    {
        var m = Mission;
        var c = Col[0];
        switch (m.Episode, step)
        {
            case (2, 0): return ChamberCount(0, ChamberKind.Storage, true) > 0;
            case (2, 1): return HerdTeam == 0;
            case (2, 2): return c.Gathered - m.GatherBase >= Expedition.GatherGoal;
            case (2, 3): return m.RaidsSent == m.RaidWaves && m.RaidersLeft == 0;
            case (2, 4): case (3, 4): return !Col[1].Alive;
            case (3, 0): return Scouted();
            case (3, 1): return m.RaidsSent == m.RaidWaves && m.RaidersLeft == 0;
            case (3, 2): return c.T(Upgrade.Queen) >= 2 || c.T(Upgrade.Mandibles) >= 1 || c.T(Upgrade.Chitin) >= 1;
            case (3, 3): return m.PostLeft == 0;
            case (4, 0): return c.Count[(int)AntType.Warlord] + c.Count[(int)AntType.Matron] + c.Count[(int)AntType.Marshal] > 0;
            case (4, 1): return StagTeam == 0;
            case (4, 2): return GuardiansLeft(1) == 0;
            case (4, 3): return !Col[1].Alive;
        }
        return false;
    }

    bool Scouted()
    {
        var mound = W.Nests[1].Pos;
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (a.Alive && a.Team == 0 && a.Layer == 0 && a.Type != AntType.Queen && a.Pos.DistanceTo(mound) < Expedition.ScoutRange) return true;
        }
        return false;
    }

    /// Refreshes the numbers and the map marker the guide panel shows for the current objective.
    void UpdateCampaign()
    {
        var m = Mission;
        if (m.IsLevel) { UpdateLevel(); return; }
        var home = W.Nests[0].Pos;
        m.RaidersLeft = Living(m.Raiders);
        m.PostLeft = Living(m.Post);
        m.GuardsLeft = Living(m.HerdGuards);
        m.Have = m.Need = 0;
        m.MarkerPos = home;
        switch (m.Episode, m.Step)
        {
            case (2, 1): m.MarkerPos = Pasture; m.Have = (int)(HerdBar * 100); break;
            case (2, 2): m.Have = (int)(Col[0].Gathered - m.GatherBase); m.Need = (int)Expedition.GatherGoal; break;
            case (2, 3):
                if (m.RaidsSent == 0) { m.Have = m.RaidAt < 0 ? 0 : (int)Mathf.Max(0, m.RaidAt - Time); m.MarkerPos = m.Lair; }
                else { m.Have = m.RaidersLeft; m.Need = m.Raiders.Count; m.MarkerPos = RaidFront(home); }
                break;
            case (3, 0):
                m.MarkerPos = W.Nests[1].Pos;
                float best = float.MaxValue;
                for (int i = 0; i < High; i++)
                    if (Ants[i].Alive && Ants[i].Team == 0 && Ants[i].Layer == 0 && Ants[i].Type != AntType.Queen) best = Mathf.Min(best, Ants[i].Pos.DistanceTo(m.MarkerPos));
                m.Have = best == float.MaxValue ? 0 : (int)best;
                break;
            case (3, 1):
                int beaten = m.RaidsSent - (m.RaidersLeft > 0 ? 1 : 0);
                m.Have = Mathf.Max(0, beaten); m.Need = m.RaidWaves;
                if (m.RaidersLeft > 0) m.MarkerPos = RaidFront(home);
                break;
            case (3, 3): m.MarkerPos = m.PostPos; m.Have = m.PostLeft; break;
            case (4, 1):
                if (Valid(StagId, StagSerial)) { m.MarkerPos = Ants[StagId].Pos; m.Have = (int)(100 * Ants[StagId].Hp / Ants[StagId].MaxHp); }
                else m.MarkerPos = StagLair;
                break;
            case (4, 2): m.MarkerPos = W.Nests[1].Pos; m.Have = GuardiansLeft(1); break;
            case (4, 3): case (2, 4): case (3, 4): m.MarkerPos = W.Nests[1].Pos; break;
        }
    }

    /// Where the nearest living raider is (for the marker), or home if none.
    Vector2 RaidFront(Vector2 home)
    {
        var at = home;
        float best = float.MaxValue;
        foreach (var (id, serial) in Mission.Raiders)
            if (Valid(id, serial) && Ants[id].Pos.DistanceTo(home) < best) { best = Ants[id].Pos.DistanceTo(home); at = Ants[id].Pos; }
        return at;
    }

    /// Episode 2's aphid herd: three ants with no bugs nearby milk it; once won, two tenders keep honeydew dripping.
    void TickHerd(float dt)
    {
        int mine = 0, wild = 0;
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (!a.Alive || a.Layer != 0) continue;
            float d = a.Pos.DistanceTo(Pasture);
            if (a.Team == 0 && d < Lane.PastureRadius) mine++;
            else if (a.Team == Config.Wild && d < Lane.PastureRadius + 4) wild++;
        }
        var c = Col[0];
        if (HerdTeam != 0)
        {
            if (mine < 3 || wild > 0) return;
            HerdBar = Mathf.Min(1, HerdBar + dt / Expedition.MilkTime);
            if (HerdBar < 1) return;
            HerdTeam = 0;
            c.Food = Mathf.Min(c.FoodCap, c.Food + Expedition.HerdFood);
            Events.Add(new SimEvent { Kind = EvKind.HerdMilked, Pos = Pasture, Team = 0 });
        }
        else if (mine >= 2 && wild == 0 && c.Food < c.FoodCap)
        {
            float drip = Mathf.Min(Expedition.Honeydew * dt, c.FoodCap - c.Food);
            c.Food += drip;
        }
    }

    /// Sends a scripted raid at the player's mound: wild bugs from their lair in Episode 2, red fire ants later.
    void LaunchRaid(int wave)
    {
        var m = Mission;
        var home = W.Nests[0].Pos;
        void Raider(int team, AntType type, Vector2 from, int level = 1)
        {
            int id = Spawn(team, type, OpenGround(from + Rng.InDisk(4)), (home - from).AngleD(), level);
            if (id < 0) return;
            ref var a = ref Ants[id];
            a.Raider = team == Config.Wild;
            if (a.Raider) a.Home = home;
            a.State = AntState.AttackMove; a.Goal = a.Anchor = home; a.GoalLayer = 0;
            m.Raiders.Add((id, a.Serial));
        }
        if (m.Episode == 2)
        {
            Raider(Config.Wild, AntType.Beetle, m.Lair, 2);
            Raider(Config.Wild, AntType.Beetle, m.Lair, 2);
            Raider(Config.Wild, AntType.Beetle, m.Lair);
            Raider(Config.Wild, AntType.Spider, m.Lair, 2);
        }
        else
        {
            var foe = W.Nests[1].Pos;
            var from = foe + (home - foe).Normalized() * 12;
            int soldiers = m.Episode == 3 ? 10 + 4 * wave : 6 + 3 * wave;
            int majors = m.Episode == 3 ? 1 + wave : wave;
            for (int i = 0; i < soldiers; i++) Raider(1, AntType.Soldier, from);
            for (int i = 0; i < majors; i++) Raider(1, AntType.Major, from);
        }
        Events.Add(new SimEvent { Kind = EvKind.UnderAttack, Pos = home, Team = 0 });
        UpdateCampaign();
    }
}
