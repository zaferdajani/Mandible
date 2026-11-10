using System.Collections.Generic;
using Godot;

namespace Mandible;

/// What a colony's workers do with the remains of other creatures: carry them home as organic matter for the queen
/// and the brood, or leave them on the ground for the fighters to eat and evolve on. (Their own dead always go to the
/// midden: a colony gets nothing from its own race.)
public enum RemainsOrder : byte { Haul, Leave }

/// Carrion and organic matter. Every death leaves remains. Remains of another kind (wild bugs, the other colony) are
/// the only source of organic matter, the colony's evolving material: workers drag and carry them home to the reserve
/// (which pays for the queen's growth and part of every brood), and fighters and heroes evolve only by eating them.
/// Killing makes no one stronger. A colony's own dead are carried to the midden; only the fire-ant Warlord eats them.
public sealed partial class Sim
{
    /// Burial, dragging, morale and the midden run in skirmish, Lane War and multiplayer. The campaign's levels were
    /// tuned on a colony that eats its own dead and forages between fights, and keeps that.
    public bool CarrionRules => Mission == null;

    /// The organic economy: other creatures' remains go into the organic reserve, the queen ascends on it, every brood
    /// needs a little and the founders need it. Everywhere outside the campaign, and in the campaign from the chapter
    /// that introduces it (Reveal.Organic: the founding, Chapter 1).
    public bool OrganicRules => Mission == null || Mission.Organic;

    /// Evolution by eating: kills teach nothing, fighters grow only on remains. The campaign ranks its squads by kills
    /// until the chapter that introduces eating (Reveal.Evolution).
    public bool EatRules => Mission == null || Mission.Evolution;

    public void SetRemains(int team, RemainsOrder order)
    {
        if (team < 0 || team > 1) return;
        Col[team].Remains = order;
    }

    // ------------------------------------------------------------------ the API the campaign and the HUD build on
    /// Organic matter in this colony's reserve.
    public float Organic(int team) => team is >= 0 and <= 1 ? Col[team].Organic : 0;
    /// Organic matter a brood of this kind needs on top of its food (0 in the campaign until it opts in).
    public float OrganicCost(ProdKind k) => OrganicRules ? Config.BroodOrganic(k) : 0;
    /// Organic matter the queen's next ascension needs (0 in the campaign until it opts in).
    public float QueenOrganic(int team) => OrganicRules ? Config.QueenOrganic(Col[team].T(Upgrade.Queen) + 1) : 0;
    /// Whether the founders carry enough food and organic matter to found the nest (the royal chamber waits until they do).
    public bool CanFound(int team) => !OrganicRules || Col[team].Food >= Config.FoundFood && Col[team].Organic >= Config.FoundOrganic;
    /// Organic matter these remains still hold (what hauling them home gives, and what eating them teaches).
    public static float RemainsValue(FoodSource f) => f.Kind == FoodKind.Corpse && f.Alive ? f.Amount * Config.OrganicPerLoad(f.AntType) : 0;
    /// The fire ants' Warlord, alone among all ants, feeds on its own colony's dead.
    public bool Cannibal(in Ant a) => a.Team < 2 && a.Type == AntType.Warlord && Col[a.Team].Race == Race.RedFire;
    /// Remains this ant may eat: another kind's, or its own kind's when it is the cannibal.
    public bool CanEat(in Ant a, FoodSource f) => f.Kind == FoodKind.Corpse && f.Alive && f.Layer == a.Layer && (f.Team != a.Team || Cannibal(a));
    /// Remains this colony's workers may carry home as organic matter.
    public static bool Haulable(FoodSource f, int team) => f.Kind == FoodKind.Corpse && f.Team != team;

    /// The richest remains within reach of `from` that an ant of `team` could eat (cannibal: its own kind's too).
    public FoodSource BestRemains(int team, Vector2 from, float radius, bool cannibal = false, int layer = 0)
    {
        FoodSource best = null;
        float bestScore = 0;
        foreach (var f in W.Food)
        {
            if (f.Kind != FoodKind.Corpse || !f.Alive || f.Layer != layer || (f.Team == team && !cannibal)) continue;
            float d = f.Pos.DistanceTo(from);
            if (d > radius) continue;
            float score = RemainsValue(f) / (6f + d); // rich and near
            if (score > bestScore) { bestScore = score; best = f; }
        }
        return best;
    }

    /// Organic matter an ant still needs for its next stage (heroes: to their next level); 0 when it is complete.
    public float EvolutionNeed(in Ant a)
    {
        if (Config.IsHero(a.Type)) return a.Level >= Config.HeroMaxLevel ? 0 : Mathf.Max(0, (NextXp(a) - a.Xp) / Config.HeroXpPerOrganic);
        if (!Evolves(a)) return 0;
        return a.Stage >= Config.MaxStage ? 0 : Mathf.Max(0, Config.StageCost(a.Stage + 1) - a.Evo);
    }

    /// Progress toward the next stage or level, 0..1.
    public float EvolutionProgress(in Ant a)
    {
        if (Config.IsHero(a.Type))
        {
            if (a.Level >= Config.HeroMaxLevel) return 1;
            float lo = Config.XpFor(a.Type, a.Level), hi = NextXp(a);
            return hi > lo ? Mathf.Clamp((a.Xp - lo) / (hi - lo), 0, 1) : 1;
        }
        return a.Stage >= Config.MaxStage ? 1 : Mathf.Clamp(a.Evo / Config.StageCost(a.Stage + 1), 0, 1);
    }

    /// Colony fighters evolve by eating: soldiers, majors, flyers and gunners (heroes level instead; lane creeps and
    /// mound guardians are spent, not grown).
    public bool Evolves(in Ant a) => a.Team < 2 && Config.IsWarrior(a.Type) && !Config.IsHero(a.Type) && !a.Guardian && a.Lane == 0;
    public bool Eats(in Ant a) => a.Team < 2 && Config.IsWarrior(a.Type) && !a.Guardian && a.Lane == 0;

    // ------------------------------------------------------------------ the midden and morale
    /// The colony's refuse pile: on bare soil a little way out from the nest mouth, on the yard side (rooms are dug
    /// first on the far side), wherever it lies furthest from the galleries already dug.
    public Vector2 Midden(int team)
    {
        var c = Col[team];
        if (c.HasMidden) return c.MiddenPos;
        var nest = W.Nests[team].Pos;
        float toYard = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - nest).AngleD();
        var best = ClampMap(nest + MathX.Dir(toYard) * Config.MiddenDist);
        float bestClear = -1;
        for (int k = 0; k < 9; k++)
        {
            float off = (k + 1) / 2 * 0.35f * (k % 2 == 0 ? 1 : -1);
            var p = ClampMap(nest + MathX.Dir(toYard + off) * Config.MiddenDist);
            if (Mission?.Confined == true) p = Mission.Constrain(p);
            if (W.Blocked(p, 2.5f)) continue;
            float clear = c.Founded ? p.DistanceTo(NestPoint(team, p)) : 99f;
            if (clear > bestClear + 0.5f) { best = p; bestClear = clear; }
        }
        c.MiddenPos = best;
        c.HasMidden = true;
        return best;
    }

    /// Bite multiplier from how the colony treats its dead: resolve while it keeps burying them.
    public float MoraleMul(int team)
    {
        if (team > 1 || !CarrionRules) return 1f;
        return Col[team].MournT > 0 ? Config.MournBite : 1f;
    }

    Vector2 NestPoint(int team, Vector2 p) { NearestOnNetwork(team, p, out var q, out _); return q; }

    void Mourn(Colony c, int bodies)
    {
        c.Buried += bodies;
        c.MournT = Mathf.Min(Config.MoraleMax, c.MournT + Config.MoraleTime * bodies);
    }

    /// Remains carried home: organic matter in the reserve (fire ants, the predators, make 25% more of it), and the protein
    /// keeps the queen laying faster.
    void StoreRemains(Colony c, AntType from, float loads)
    {
        float gain = loads * Config.OrganicPerLoad(from) * c.FoodMult * (Config.Favoured(c.Race, FoodKind.Corpse) ? Config.PredatorOrganic : 1f);
        c.Organic += gain;
        c.OrganicGathered += gain;
        c.ProteinT = Mathf.Min(Config.EffectMax, c.ProteinT + Config.EffectPerDelivery * loads);
        if (Mission is { RemainsFeed: true })
        {
            float food = Mathf.Min(loads * Config.FoodValue(FoodKind.Corpse, true) * c.FoodMult, Mathf.Max(0, c.FoodCap - c.Food));
            c.Food += food; c.Gathered += food;
        }
    }

    // ------------------------------------------------------------------ workers at remains
    /// A worker at a carcass: grips it to drag it home with its nestmates, picks up the whole body of a small fallen
    /// nestmate for the midden, or cuts off a jawful. Returns the way to walk (zero when it took its load).
    Vector2 AtCarcass(int i, ref Ant a, FoodSource f, UnitStats st, ref float speedMul)
    {
        bool bury = f.Team == a.Team && CarrionRules;
        int need = CarrionRules ? Config.HaulNeed(f.AntType) : 0;
        // drag it whole: big enough to need a team, not yet home, and not already in the jaws of the other colony
        var dest = bury ? Midden(a.Team) : NestEntrance(a.Team);
        if (need > 0 && f.Layer == 0 && !f.Hauled && f.Pos.DistanceTo(dest) <= Config.HaulArrive + f.Radius) Arrive(f, a.Team, bury);
        if (!f.Alive) return Vector2.Zero;
        bool haul = need > 0 && f.Layer == 0 && !f.Hauled
                    && (f.HaulCount == 0 || f.HaulTeam == a.Team) && (a.Hauling || f.HaulCount < need + 2 || bury);
        if (haul)
        {
            a.Hauling = true;
            a.State = AntState.Gather;
            float slotA = (a.Serial * 2.39996f) % DMath.Tau;
            var slot = f.Pos + MathX.Dir(slotA) * (f.Radius + 0.45f);
            var to = slot - a.Pos;
            if (to.Length() < 1.1f && f.Haulers < need + 2 && (f.Haulers == 0 || f.HaulTeam == a.Team))
            {
                f.Haulers++;
                f.HaulTeam = a.Team;
                // keep pace with the load: its own speed when it is moving, plus whatever closes the gap to the grip
                bool moving = f.HaulCount >= need;
                float pace = (moving ? Config.HaulSpeed * Mathf.Min(1.3f, (float)f.HaulCount / need) : 0f) + to.Length() * 4f;
                speedMul = Mathf.Clamp(pace / st.Speed, 0f, 1f);
                return moving ? (dest - f.Pos).Normalized() * Mathf.Max(0.2f, 1.1f - to.Length()) + to : to;
            }
            return to;
        }
        a.Hauling = false;
        if (bury)
        {
            // a small nestmate is carried away whole
            f.Amount = 0;
            a.Burying = true;
        }
        else
        {
            f.Amount -= 1;
            ShrinkBlock(f);
        }
        a.Carrying = true;
        a.CarryKind = FoodKind.Corpse;
        a.CarryType = f.AntType;
        a.CarryTeam = f.Team;
        a.State = AntState.Return;
        a.FoodTarget = null;
        if (!bury && a.Assigned == null && f.Amount > 3) a.Assigned = f;
        Ph.Deposit(a.Team, a.Pos, 3f * TrailMul(a.Team));
        Events.Add(new SimEvent { Kind = EvKind.Pickup, Pos = f.Pos, Team = a.Team, Type = f.AntType, FoodP1 = f.Id + 1 });
        return Vector2.Zero;
    }

    /// A worker bringing a dead nestmate to the midden lays it down there.
    bool AtMidden(ref Ant a)
    {
        if (!a.Burying || a.Layer != 0 || a.Pos.DistanceTo(Midden(a.Team)) > 2f) return false;
        a.Carrying = false;
        a.Burying = false;
        Mourn(Col[a.Team], 1);
        Events.Add(new SimEvent { Kind = EvKind.Bury, Pos = a.Pos, Team = a.Team, Type = a.CarryType });
        a.Heading += DMath.Pi;
        a.State = AntState.Forage;
        return true;
    }

    // ------------------------------------------------------------------ eating and evolving
    /// The player (or the AI) sends fighters and heroes to eat these remains; whoever eats them evolves.
    public void CmdFeast(List<int> ids, FoodSource f)
    {
        foreach (int id in ids)
        {
            ref var a = ref Ants[id];
            if (!a.Alive || !Eats(a) || !CanEat(a, f)) continue;
            a.FoodTarget = f;
            a.FeastOrder = true;
            if (a.Team < 2) { f.ClaimTeam = a.Team + 1; f.ClaimUntil = Time + Config.EatClaim; } // our own haulers leave it to them
            a.Goal = f.Pos;
            a.GoalLayer = f.Layer;
            a.State = AntState.Move;
            a.Forced = false;
            a.Target = -1;
            a.Stuck = 0;
        }
    }

    /// A fighter or hero with nothing to fight eats at remains it stands by (or was sent to): each mouthful heals it
    /// and grows it, more from big prey and most from a rival hero. It stands still while it eats. Returns true while
    /// it feasts.
    bool Feast(int i, ref Ant a, float dt, bool think, ref Vector2 desired)
    {
        bool hero = Config.IsHero(a.Type);
        if (!EatRules && !hero) return false; // the campaign keeps its kill-ranked squads until eating is introduced
        bool growing = hero ? a.Level < Config.HeroMaxLevel : a.Stage < Config.MaxStage && Evolves(a);
        if (!growing && a.Hp >= a.MaxHp * 0.999f) { a.FoodTarget = null; a.FeastOrder = false; return false; }
        var f = a.FoodTarget;
        if (f != null && !CanEat(a, f)) { f = a.FoodTarget = null; a.FeastOrder = false; }
        if (f != null && !a.FeastOrder && !a.AutoEat) { f = a.FoodTarget = null; } // auto-eat turned off: the kill is left for the workers
        if (f == null && think && a.AutoEat)
        {
            f = BestRemains(a.Team, a.Pos, a.Size * 0.5f + 4f, Cannibal(a), a.Layer);
            a.FoodTarget = f;
        }
        if (f == null) { a.FeastT = 0; return false; }
        var to = f.Pos - a.Pos;
        float reach = f.Radius + a.Size * 0.4f + 0.4f;
        if (to.Length() > reach)
        {
            if (a.State == AntState.Hold && !a.FeastOrder) { a.FoodTarget = null; return false; } // a guard does not wander off to eat
            desired = to;
            a.Anchor = f.Pos;
            return false;
        }
        a.Feasting = true;
        a.Anchor = a.Pos;
        a.Heading += MathX.AngleDiff(a.Heading, to.AngleD()) * 0.2f;
        bool own = f.Team == a.Team; // the cannibal at its own dead tears into them faster
        a.FeastT += dt * (own ? Config.DevourPace : 1f);
        desired = Vector2.Zero;
        if (a.FeastT < Config.FeastBite) return true;
        a.FeastT = 0;
        f.Amount -= 1;
        f.Feasted++;
        f.LastFeast = Time;
        a.AttackAnim = 1;
        a.Hp = Mathf.Min(a.MaxHp, a.Hp + a.MaxHp * Config.FeastHeal * (own ? Config.DevourHeal : 1f));
        Nourish(i, ref a, Config.OrganicPerLoad(f.AntType));
        Col[a.Team].OrganicEaten += Config.OrganicPerLoad(f.AntType);
        Events.Add(new SimEvent { Kind = EvKind.Feast, Pos = f.Pos, To = a.Pos, Team = a.Team, Type = f.AntType, FoodP1 = f.Id + 1, Who = i + 1, Amount = own ? 1 : 0 });
        return true;
    }

    /// Organic matter eaten by one ant: a hero learns from it, a fighter evolves.
    void Nourish(int i, ref Ant a, float organic)
    {
        if (Config.IsHero(a.Type)) { GrantXp(i, organic * Config.HeroXpPerOrganic, true); return; }
        if (!Evolves(a) || a.Stage >= Config.MaxStage) return;
        if (Mission != null && a.Team == Config.Enemy) return; // the campaign's rival keeps the strength its level gives it
        a.Evo += organic * (a.Team == 0 && Has(0, Mutation.HungryGut) ? Mutations.GutMul : 1f);
        while (a.Stage < Config.MaxStage && a.Evo >= Config.StageCost(a.Stage + 1))
        {
            a.Evo -= Config.StageCost(a.Stage + 1);
            a.Stage++;
            ApplyLevel(ref a);
            a.Hp = Mathf.Min(a.MaxHp, a.Hp + a.MaxHp * 0.3f); // the moult heals
            Events.Add(new SimEvent { Kind = EvKind.Evolve, Pos = a.Pos, Team = a.Team, Type = a.Type, Tier = a.Stage, Who = i + 1 });
        }
        if (a.Stage >= Config.MaxStage) a.Evo = 0;
    }

    /// Fighters and heroes brought home are fed from the colony's reserve: the player chooses whether the organic matter
    /// grows the queen or the army. Only those standing at the nest (or inside it) are fed.
    public void FeedFromReserve(int team, List<int> ids)
    {
        if (!EatRules) return;
        var c = Col[team];
        var nest = NestEntrance(team);
        foreach (int id in ids)
        {
            ref var a = ref Ants[id];
            if (!a.Alive || a.Team != team || !Eats(a) || c.Organic < 0.5f) continue;
            if (a.Layer == 0 && a.Pos.DistanceTo(nest) > Config.ReinforceRadius) continue;
            float need = EvolutionNeed(a);
            if (need <= 0) continue;
            float take = Mathf.Min(need + 0.01f, c.Organic);
            c.Organic -= take;
            Nourish(id, ref a, take);
            Events.Add(new SimEvent { Kind = EvKind.Feast, Pos = a.Pos, To = a.Pos, Team = team, Type = a.Type, Who = id + 1, Amount = 2 });
        }
    }

    // ------------------------------------------------------------------ remains over time
    /// Carcasses rot, slide along behind the workers dragging them, and are buried or stored when they get home.
    void TickCarrion(float dt)
    {
        var list = W.Food;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var f = list[i];
            if (f.Kind != FoodKind.Corpse) continue;
            f.Age += dt;
            f.HaulCount = f.Haulers;
            f.Haulers = 0;
            if (f.Age > Config.CarcassFresh + Config.CarcassRot) f.Amount = 0;
            else if (f.Natural && f.Age > Config.CarcassFresh)
                f.Amount = Mathf.Min(f.Amount, f.Max * (1 - (f.Age - Config.CarcassFresh) / Config.CarcassRot)); // dries out, shrinks, crumbles
            if (f.Layer > 0 && f.Alive) InNest(f, dt);
            else if (f.Alive && f.HaulCount > 0) Drag(f, dt);
            if (!f.Alive) list.RemoveAt(i);
        }
        // the queen's own body: a trickle that keeps a colony that never hunts from stalling outright
        foreach (var c in Col)
            if (c.Alive && OrganicRules && c.Organic < Config.OrganicFloor && (Mission == null || c.Founded)) c.Organic += Config.OrganicTrickle * dt;
        if (CarrionRules && Ticks % 60 == 0) foreach (var c in Col) Recruit(c);
    }

    /// Prey recruitment and automatic collection. The colony notices fresh remains anywhere within its collection
    /// radius of the nest (it grows with the workforce), and further out wherever one of its ants can see them. It
    /// ranks them by value and distance and sends foragers to the best few that are short of carriers. Remains an
    /// auto-eat fighter has claimed, or that anyone is eating, are left alone. The computer colonies use the same rules.
    void Recruit(Colony c)
    {
        if (!c.Alive || c.Remains != RemainsOrder.Haul) return;
        int team = c.Team, bit = 1 << team;
        var nest = NestEntrance(team);
        int workers = 0;
        for (int i = 0; i < High; i++) if (Ants[i].Alive && Ants[i].Team == team && Ants[i].Type == AntType.Worker) workers++;
        float radius = Config.CollectRadius(workers);
        var queue = new List<(float score, FoodSource f)>();
        foreach (var f in W.Food)
        {
            if (f.Kind != FoodKind.Corpse || !f.Alive || f.Layer != 0 || f.Team == team) continue;
            bool claimed = f.ClaimTeam == team + 1 && f.ClaimUntil > Time;
            bool eaten = f.Feasted > 0 && Time - f.LastFeast < 3f;
            float d = f.Pos.DistanceTo(nest);
            float value = RemainsValue(f);
            bool known = d <= radius || d <= Config.RecruitRange && Seen(team, f.Pos);
            if (claimed || eaten || value < 1.5f || !known) { f.Queued &= ~bit; continue; }
            queue.Add((value / (20f + d), f));
        }
        if (queue.Count == 0) return;
        queue.Sort((x, y) => x.score != y.score ? y.score.CompareTo(x.score) : x.f.Id.CompareTo(y.f.Id));
        int cap = workers / 3; // the colony keeps most of its foragers on food
        int crewed = 0;
        for (int q = 0; q < queue.Count && crewed < Config.CollectAtOnce && cap > 0; q++)
        {
            var prey = queue[q].f;
            int already = 0;
            for (int i = 0; i < High; i++)
            {
                ref var a = ref Ants[i];
                if (a.Alive && a.Team == team && a.Type == AntType.Worker && (a.Assigned == prey || a.FoodTarget == prey)) already++;
            }
            cap -= already;
            int want = Mathf.Min(Mathf.CeilToInt(prey.Amount), Mathf.Max(Config.HaulNeed(prey.AntType) + 2, 3)) - already;
            int sent = 0;
            for (int i = 0; i < High && want > 0 && cap > 0; i++)
            {
                ref var a = ref Ants[i];
                if (!a.Alive || a.Team != team || a.Type != AntType.Worker || a.Layer != 0 || a.Carrying || a.DigP1 > 0) continue;
                if (a.State != AntState.Forage || a.Pos.DistanceTo(prey.Pos) > Config.RecruitRange) continue;
                a.Assigned = prey; a.FoodTarget = prey; a.State = AntState.Gather;
                want--; cap--; sent++;
            }
            if (sent > 0) crewed++; // remains already fully crewed do not hold up the next in the queue
            if ((prey.Queued & bit) == 0 && already + sent > 0)
            {
                prey.Queued |= bit;
                Events.Add(new SimEvent { Kind = EvKind.Collect, Pos = prey.Pos, Team = team, Type = prey.AntType, FoodP1 = prey.Id + 1 });
            }
        }
    }

    /// How far around its nest a colony notices fresh remains on its own (rendering shows it on the Remains page).
    public float CollectRadius(int team)
    {
        int workers = 0;
        for (int i = 0; i < High; i++) if (Ants[i].Alive && Ants[i].Team == team && Ants[i].Type == AntType.Worker) workers++;
        return Config.CollectRadius(workers);
    }

    /// CmdKind.AutoEat: these fighters and generals (and the rest of their squads) eat what they kill, or leave it.
    public void SetAutoEat(List<int> ids, bool on)
    {
        foreach (int id in ids)
        {
            ref var a = ref Ants[id];
            if (!a.Alive || !Eats(a)) continue;
            a.AutoEat = on;
            if (!on && !a.FeastOrder) { a.FoodTarget = null; a.Feasting = false; }
            if (a.SquadP1 <= 0) continue;
            var s = Squads[a.SquadP1 - 1];
            s.AutoEat = on;
            foreach (int m in s.Members) { Ants[m].AutoEat = on; if (!on && !Ants[m].FeastOrder) Ants[m].FoodTarget = null; }
        }
    }

    /// A kill by an auto-eat fighter: the remains are its (and its squad's) to eat, and the haulers keep off for a while.
    void ClaimKill(int killer, FoodSource f)
    {
        if (!EatRules || killer < 0 || !Ants[killer].Alive) return;
        ref var k = ref Ants[killer];
        if (!k.AutoEat || !Eats(k) || !CanEat(k, f)) return;
        bool Hungry(in Ant x) => EvolutionNeed(x) > 0 || x.Hp < x.MaxHp * 0.999f;
        int eaters = 0;
        if (Hungry(k) && k.FoodTarget is not { Alive: true }) { k.FoodTarget = f; eaters++; }
        if (k.SquadP1 > 0)
            foreach (int m in Squads[k.SquadP1 - 1].Members)
            {
                ref var b = ref Ants[m];
                if (m != killer && b.AutoEat && b.Layer == f.Layer && b.Pos.DistanceTo(f.Pos) < 15f && Hungry(b) && b.FoodTarget is not { Alive: true }) { b.FoodTarget = f; eaters++; }
            }
        if (eaters == 0) return;
        f.ClaimTeam = k.Team + 1;
        f.ClaimUntil = Time + Config.EatClaim;
    }

    /// Whether any ant of the colony stands close enough to these remains to have found them.
    bool Seen(int team, Vector2 p)
    {
        int r = (int)(Config.RecruitSight / SpatialHash.Cell) + 1;
        int cx = (int)(p.X / SpatialHash.Cell), cz = (int)(p.Y / SpatialHash.Cell);
        for (int z = cz - r; z <= cz + r; z++)
        {
            if (z < 0 || z >= Hash.N) continue;
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || x >= Hash.N) continue;
                for (int j = Hash.Head[z * Hash.N + x]; j >= 0; j = Hash.Next[j])
                    if (Ants[j].Alive && Ants[j].Team == team && Ants[j].Layer == 0 && Ants[j].Pos.DistanceSquaredTo(p) < Config.RecruitSight * Config.RecruitSight) return true;
            }
        }
        return false;
    }

    /// Dragged home: butchered at the nest mouth from now on, or laid on the midden.
    void Arrive(FoodSource f, int team, bool bury)
    {
        f.Hauled = true;
        if (!bury) return;
        Mourn(Col[team], 2);
        f.Amount = 0;
        Events.Add(new SimEvent { Kind = EvKind.Bury, Pos = f.Pos, Team = team, Type = f.AntType });
    }

    void Drag(FoodSource f, float dt)
    {
        int need = Config.HaulNeed(f.AntType);
        if (need == 0 || f.HaulCount < need || f.Hauled) return;
        int team = f.HaulTeam;
        bool bury = f.Team == team;
        var dest = bury ? Midden(team) : NestEntrance(team);
        var to = dest - f.Pos;
        float d = to.Length();
        if (d <= Config.HaulArrive + f.Radius) { Arrive(f, team, bury); return; }
        float speed = Config.HaulSpeed * Mathf.Min(1.3f, (float)f.HaulCount / need);
        var dir = to / d;
        // slide round rocks the way an ant would steer
        var ahead = f.Pos + dir * (f.Radius + 1.5f);
        foreach (var o in W.ObstaclesNear(ahead))
        {
            float rr = o.R + f.Radius + 0.4f;
            if (o.R <= 0 || ahead.DistanceSquaredTo(o.P) >= rr * rr) continue;
            var n = (f.Pos - o.P).Normalized();
            var tangent = new Vector2(-n.Y, n.X);
            if (tangent.Dot(dir) < 0) tangent = -tangent;
            dir = (tangent + n * 0.25f).Normalized();
            break;
        }
        var p = f.Pos + dir * Mathf.Min(speed * dt, d);
        foreach (var o in W.ObstaclesNear(p))
        {
            if (o.R <= 0) continue;
            float rr = o.R + f.Radius * 0.6f;
            var off = p - o.P;
            float l = off.Length();
            if (l < rr && l > 1e-4f) p = o.P + off / l * rr;
        }
        p = ClampMap(p);
        if (Mission?.Confined == true) p = Mission.Constrain(p);
        f.Pos = p;
        f.Heading += MathX.AngleDiff(f.Heading, dir.AngleD() + DMath.Pi) * Mathf.Min(1, dt * 1.5f); // it comes rear first, the jaws pulling at its head
    }

    /// A carcass inside a nest never reaches the surface foragers: the nurses deal with it where it lies. Their own dead go
    /// out to the midden; an invader's remains are cut up into the reserve.
    void InNest(FoodSource f, float dt)
    {
        int team = f.Layer - 1;
        var c = Col[team];
        if (!c.Alive) return;
        if (f.Team == team && CarrionRules)
        {
            if (f.Age < Config.NestCarryOut) return;
            f.Amount = 0;
            Mourn(c, 1);
            Events.Add(new SimEvent { Kind = EvKind.Bury, Pos = Midden(team), Team = team, Type = f.AntType });
            return;
        }
        float take = Mathf.Min(f.Amount, Config.NestEatRate * dt);
        f.Amount -= take;
        if (CarrionRules || OrganicRules && f.Team != team) { StoreRemains(c, f.AntType, take); return; }
        // the campaign's colony eats them as food
        float gain = take * Config.FoodValue(FoodKind.Corpse, Mission != null) * c.FoodMult * (Config.Favoured(c.Race, FoodKind.Corpse) ? Config.FoodBonus(c.Race) : 1f);
        gain = Mathf.Min(gain, Mathf.Max(0, c.FoodCap - c.Food));
        c.Food += gain;
        c.Gathered += gain;
        c.GatheredBy[(int)FoodKind.Corpse] += gain;
        c.ProteinT = Mathf.Min(Config.EffectMax, c.ProteinT + Config.EffectPerDelivery * take);
    }
}
