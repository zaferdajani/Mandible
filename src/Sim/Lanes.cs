using Godot;

namespace Mandible;

public enum GameMode : byte { Colony, Lanes }

/// Lane War rules and numbers: three lanes, free creep waves, mound guardians as towers,
/// one hero per colony with a respawn timer and a shop.
public static class Lane
{
    public const float FirstWave = 10f, WaveEvery = 30f;
    public const float FountainRadius = 14f;
    public const int MaxItemTier = 3;
    public static readonly string[] ItemNames = { "Mandibles", "Carapace", "Swift Legs", "Royal Jelly" };
    public static readonly string[] ItemDesc =
    {
        "+18% bite damage per tier",
        "+20% health per tier",
        "+10% speed per tier",
        "Heals your hero 60% at once (any time, any number)",
    };

    /// Next tier's price; Royal Jelly is a consumable with a fixed price.
    public static int ItemCost(int item, int tier) => item == 3 ? 50 : tier switch { 0 => 90, 1 => 180, _ => 300 };

    public static float Respawn(int level) => 8f + 2.2f * level;

    /// Food paid to whoever lands the killing blow.
    public static float Bounty(in Ant t) => t.Guardian ? 120 : Config.IsHero(t.Type) ? 60 + 10 * t.Level : t.Type switch
    {
        AntType.Worker => 5, AntType.Soldier => 10, AntType.Gunner or AntType.Flyer => 14, AntType.Major => 22, AntType.Queen => 0,
        _ => 8 * t.Level, // wild bugs
    };

    public static string HeroBlurb(AntType t) => t switch
    {
        AntType.Warlord => "The plated brawler. Cleaving bites, the heaviest armour of the three, and a War Cry (Q) that staggers the enemy and drives your waves on.",
        AntType.Matron => "The ranged acid spitter. Her spray splashes over crowds; Acid Burst (Q) melts a cluster and leaves a venom pool nobody should stand in.",
        _ => "The winged commander. Flies over rocks and out of reach; Dive Bomb (Q) crushes and stuns a crowd and rallies every warrior who sees it.",
    };

    /// The full card: attack, defence and the special, each with its numbers.
    public static string HeroCard(AntType t) => $"ATTACK  {Config.AttackDesc(t)}\nDEFENCE  {Config.DefenceDesc(t)}\nSPECIAL (Q)  {Config.SpecialDesc(t)}";
}

public sealed partial class Sim
{
    public GameMode Mode;
    /// Waypoints of the three lanes, from the black nest (team 0) to the red nest (team 1).
    public readonly Vector2[][] Lanes = new Vector2[3][];
    public readonly AntType[] HeroType = { AntType.Warlord, AntType.Warlord };
    public readonly int[] HeroId = { -1, -1 }, HeroSerial = new int[2], HeroLevel = { 1, 1 };
    public readonly float[] HeroXp = new float[2], HeroRespawn = new float[2];
    public readonly int[,] Items = new int[2, 4];
    public readonly int[] HeroKills = new int[2];
    float _nextLaneWave = Lane.FirstWave;

    void BuildLanes()
    {
        var n0 = W.Nests[0].Pos;
        var n1 = W.Nests[1].Pos;
        // the two flank lanes turn at the corners the nests do not occupy
        // flank corners sit well inside the yard: the very corners are rock-choked
        var top = new Vector2(n0.X, n1.Y) + new Vector2(26, 26);
        var bot = new Vector2(n1.X, n0.Y) - new Vector2(26, 26);
        Lanes[0] = new[] { n0, n0.Lerp(top, 0.5f), top, top.Lerp(n1, 0.5f), n1 };
        Lanes[1] = new[] { n0, n0.Lerp(n1, 0.25f), n0.Lerp(n1, 0.5f), n0.Lerp(n1, 0.75f), n1 };
        Lanes[2] = new[] { n0, n0.Lerp(bot, 0.5f), bot, bot.Lerp(n1, 0.5f), n1 };
        // a waypoint inside a rock or the great raspberry could never be reached: slide it onto open ground
        foreach (var lane in Lanes)
            for (int i = 1; i < lane.Length - 1; i++) lane[i] = OpenGround(lane[i]);
    }

    Vector2 OpenGround(Vector2 p)
    {
        if (!W.Blocked(p, 2.5f)) return p;
        for (float r = 3; r < 30; r += 2)
            for (int k = 0; k < 16; k++)
            {
                var q = p + MathX.Dir(k * DMath.Tau / 16) * r;
                if (!W.Blocked(q, 2.5f)) return q;
            }
        return p;
    }

    /// Waypoint `step` of a lane as seen from `team`'s side (step 0 is its own nest).
    public Vector2 Waypoint(int team, int lane, int step) => Lanes[lane][team == 0 ? step : Lanes[lane].Length - 1 - step];

    public bool NearLane(Vector2 p, float r)
    {
        foreach (var lane in Lanes)
            for (int i = 0; i + 1 < lane.Length; i++)
                if (DistToSegment(p, lane[i], lane[i + 1]) < r) return true;
        return false;
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Mathf.Clamp((p - a).Dot(ab) / Mathf.Max(1e-4f, ab.LengthSquared()), 0, 1);
        return p.DistanceTo(a + ab * t);
    }

    /// Point a fraction of the way along a lane (by length) from `team`'s nest.
    public Vector2 AlongLane(int team, int lane, float frac)
    {
        float total = 0;
        for (int i = 0; i < 4; i++) total += Waypoint(team, lane, i).DistanceTo(Waypoint(team, lane, i + 1));
        float want = total * frac;
        for (int i = 0; i < 4; i++)
        {
            var a = Waypoint(team, lane, i);
            var b = Waypoint(team, lane, i + 1);
            float seg = a.DistanceTo(b);
            if (want <= seg) return a.Lerp(b, want / seg);
            want -= seg;
        }
        return Waypoint(team, lane, 4);
    }

    void SetupLaneColony(Colony c)
    {
        int team = c.Team;
        var nest = W.Nests[team].Pos;
        var toCenter = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - nest).Normalized();
        // two guardians on every lane, and two at the mound itself
        for (int lane = 0; lane < 3; lane++)
        {
            SpawnGuardian(team, AlongLane(team, lane, 0.36f));
            SpawnGuardian(team, AlongLane(team, lane, 0.2f));
        }
        var side = new Vector2(-toCenter.Y, toCenter.X);
        SpawnGuardian(team, nest + toCenter * 9 + side * 6);
        SpawnGuardian(team, nest + toCenter * 9 - side * 6);
        SpawnHero(team);
    }

    void SpawnGuardian(int team, Vector2 p)
    {
        var toCenter = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - p).Normalized();
        int id = Spawn(team, AntType.Major, p, toCenter.AngleD());
        if (id < 0) return;
        ref var a = ref Ants[id];
        a.Guardian = true;
        ApplyLevel(ref a);
        a.Hp = a.MaxHp;
        a.State = AntState.Hold;
    }

    void SpawnHero(int team)
    {
        var nest = W.Nests[team].Pos;
        var toCenter = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - nest).Normalized();
        int id = Spawn(team, HeroType[team], nest + toCenter * 6, toCenter.AngleD(), HeroLevel[team]);
        if (id < 0) return;
        ref var a = ref Ants[id];
        a.Xp = HeroXp[team];
        a.Manual = true;
        a.State = AntState.Idle;
        HeroId[team] = id;
        HeroSerial[team] = a.Serial;
        Events.Add(new SimEvent { Kind = EvKind.ProdDone, Pos = a.Pos, Team = team, Type = a.Type });
    }

    void ApplyLaneStats(ref Ant a)
    {
        if (a.Guardian)
        {
            a.MaxHp *= 9f;
            a.DmgMul *= 3.2f;
            a.Size *= 1.6f;
            a.SpeedMul = 0;
        }
        if (Config.IsHero(a.Type))
        {
            // lane duel balance: the brawler is toned down, the spitter and the flyer toughened up
            float hp = a.Type switch { AntType.Warlord => 0.85f, AntType.Matron => 1.45f, _ => 1.15f };
            float dmg = a.Type switch { AntType.Warlord => 0.9f, AntType.Matron => 1.5f, _ => 0.9f };
            a.MaxHp *= hp;
            a.DmgMul *= dmg;
            a.MaxHp *= 1 + 0.2f * Items[a.Team, 1];
            a.DmgMul *= 1 + 0.18f * Items[a.Team, 0];
            a.SpeedMul *= 1 + 0.1f * Items[a.Team, 2];
        }
    }

    /// A lane creep reached its waypoint: march on to the next one.
    bool NextLaneStep(ref Ant a)
    {
        if (a.LaneStep >= 4) return false;
        a.LaneStep++;
        a.Stuck = 0;
        a.Goal = Waypoint(a.Team, a.Lane - 1, a.LaneStep);
        return true;
    }

    /// Lane creeps never stall: one already nearer the following waypoint, or wedged against
    /// rocks for a few seconds, moves on to the next waypoint.
    void UnstickCreep(ref Ant a, float dt)
    {
        if (a.LaneStep >= 4) return;
        var next = Waypoint(a.Team, a.Lane - 1, a.LaneStep + 1);
        if (a.Pos.DistanceTo(next) < a.Goal.DistanceTo(next)) { NextLaneStep(ref a); return; }
        // time spent marching toward this waypoint (fights do not count): a jammed creep gives up on it
        a.Stuck += dt;
        if (a.Stuck > 24f) NextLaneStep(ref a);
    }

    void TickLanes(float dt)
    {
        for (int team = 0; team < 2; team++)
        {
            var c = Col[team];
            if (!c.Alive) continue;
            if (HeroId[team] < 0)
            {
                HeroRespawn[team] -= dt;
                if (HeroRespawn[team] <= 0) SpawnHero(team);
            }
            else
            {
                // the healing pool at the mound
                ref var h = ref Ants[HeroId[team]];
                if (h.Alive && h.Pos.DistanceTo(W.Nests[team].Pos) < Lane.FountainRadius) h.Hp = Mathf.Min(h.MaxHp, h.Hp + h.MaxHp * 0.1f * dt);
            }
        }
        if (Time < _nextLaneWave) return;
        _nextLaneWave += Lane.WaveEvery;
        for (int team = 0; team < 2; team++)
        {
            if (!Col[team].Alive) continue;
            for (int lane = 0; lane < 3; lane++)
            {
                // garden ants win by numbers, fire ants by venom
                SpawnCreep(team, lane, AntType.Soldier, Col[team].Race == Race.BlackGarden ? 5 : 4);
                if (Time > 300) SpawnCreep(team, lane, AntType.Gunner, 1);
                if (Time > 600) SpawnCreep(team, lane, AntType.Major, 1);
                if (Time > 900) SpawnCreep(team, lane, AntType.Major, 2); // elite waves: the war must end
            }
        }
    }

    void SpawnCreep(int team, int lane, AntType type, int n)
    {
        var nest = W.Nests[team].Pos;
        var dir = (Waypoint(team, lane, 1) - nest).Normalized();
        int level = System.Math.Min(6, 1 + (int)(Time / 300)); // waves hatch stronger as the war drags on
        for (int k = 0; k < n; k++)
        {
            int id = Spawn(team, type, nest + dir * 7 + Rng.InDisk(2.5f), dir.AngleD(), level);
            if (id < 0) return;
            ref var a = ref Ants[id];
            a.Lane = (byte)(lane + 1);
            a.LaneStep = 1;
            a.Goal = Waypoint(team, lane, 1);
            a.State = AntState.AttackMove;
        }
    }

    void LaneDeath(int id, int killer)
    {
        ref var t = ref Ants[id];
        if (killer >= 0 && Ants[killer].Team < 2 && Ants[killer].Team != t.Team) Col[Ants[killer].Team].Food += Lane.Bounty(t);
        if (t.Team < 2 && id == HeroId[t.Team])
        {
            HeroLevel[t.Team] = t.Level;
            HeroXp[t.Team] = t.Xp;
            HeroRespawn[t.Team] = Lane.Respawn(t.Level);
            HeroId[t.Team] = -1;
            if (killer >= 0 && Ants[killer].Team < 2) HeroKills[Ants[killer].Team]++;
        }
    }

    /// Creeps that break into a mound whose guardians have fallen go straight for the queen.
    bool LaneSiege(ref Ant a)
    {
        int foe = 1 - a.Team;
        var c = Col[foe];
        if (!c.Alive || a.Pos.DistanceTo(W.Nests[foe].Pos) > 18 || QueenShielded(foe)) return false;
        Engage(ref a, c.QueenId, AntState.AttackMove);
        a.Forced = true;
        return true;
    }

    /// Lane War: a queen cannot be harmed while either guardian of her mound still stands.
    public bool QueenShielded(int team)
    {
        if (Mode != GameMode.Lanes || team > 1) return false;
        var nest = W.Nests[team].Pos;
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (a.Alive && a.Guardian && a.Team == team && a.Pos.DistanceTo(nest) < 16) return true;
        }
        return false;
    }

    public int GuardiansLeft(int team)
    {
        int n = 0;
        for (int i = 0; i < High; i++) if (Ants[i].Alive && Ants[i].Guardian && Ants[i].Team == team) n++;
        return n;
    }

    /// Buys the next tier of a hero upgrade (Royal Jelly heals instead). Returns false if not allowed.
    public bool BuyItem(int team, int item)
    {
        if (!CanBuy(team, item)) return false;
        var c = Col[team];
        c.Food -= Lane.ItemCost(item, Items[team, item]);
        if (item == 3)
        {
            ref var h = ref Ants[HeroId[team]];
            h.Hp = Mathf.Min(h.MaxHp, h.Hp + h.MaxHp * 0.6f);
            return true;
        }
        Items[team, item]++;
        if (HeroId[team] >= 0) ApplyLevel(ref Ants[HeroId[team]]);
        return true;
    }

    public bool CanBuy(int team, int item)
    {
        if (Mode != GameMode.Lanes || item < 0 || item > 3 || !Col[team].Alive) return false;
        if (item < 3 && Items[team, item] >= Lane.MaxItemTier) return false;
        if (item == 3 && (HeroId[team] < 0 || Ants[HeroId[team]].Hp >= Ants[HeroId[team]].MaxHp)) return false;
        return Col[team].Food >= Lane.ItemCost(item, Items[team, item]);
    }

    /// Ask the hero to use its ability as soon as it has a target.
    public void CmdAbility(int team)
    {
        int id = HeroId[team];
        if (id >= 0 && Ants[id].Alive) Ants[id].CastReq = 2f;
    }
}
