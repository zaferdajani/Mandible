using Godot;

namespace Mandible;

/// What a colony does with its own dead: real ants carry them to a refuse pile outside the nest
/// (necrophoresis), and many species eat dead nestmates when food runs short.
public enum FallenOrder : byte { Honour, Recycle }

/// Carrion: every death leaves a carcass that rots, is carved up and carried home a jawful at a time, dragged
/// whole by a team of workers when it is too big for one, buried on the midden or eaten, and feasted on by heroes.
public sealed partial class Sim
{
    /// The burial order, dragging and the morale it brings run in skirmish and multiplayer. The campaign's episodes
    /// were tuned, scripted fights and all, on a colony that simply ate its dead; they keep that until the new
    /// campaign opts in (heroes still feast and the dead still lie in pieces there).
    public bool CarrionRules => Mission == null;


    public void SetFallen(int team, FallenOrder order)
    {
        if (team < 0 || team > 1) return;
        Col[team].Fallen = order;
    }

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
            if (Mission?.Confined == true) p = Expedition.Constrain(p);
            if (W.Blocked(p, 2.5f)) continue;
            float clear = c.Founded ? p.DistanceTo(NestPoint(team, p)) : 99f;
            if (clear > bestClear + 0.5f) { best = p; bestClear = clear; }
        }
        c.MiddenPos = best;
        c.HasMidden = true;
        return best;
    }

    /// The fallen are part of what a colony eats only when it has said so; every other carcass is fair game.
    public bool Edible(FoodSource f, int team) => f.Kind != FoodKind.Corpse || f.Team != team || Col[team].Fallen == FallenOrder.Recycle || !CarrionRules;

    /// Bite multiplier from how the colony treats its dead: resolve while it keeps burying them. (Eating them costs brood instead.)
    public float MoraleMul(int team)
    {
        if (team > 1 || !CarrionRules) return 1f;
        var c = Col[team];
        return c.MournT > 0 ? Config.MournBite : 1f;
    }

    Vector2 NestPoint(int team, Vector2 p) { NearestOnNetwork(team, p, out var q, out _); return q; }

    void Mourn(Colony c, int bodies)
    {
        c.Buried += bodies;
        c.MournT = Mathf.Min(Config.MoraleMax, c.MournT + Config.MoraleTime * bodies);
    }

    void Sicken(Colony c) => c.SickT = Mathf.Min(Config.MoraleMax, c.SickT + Config.MoraleTime);

    /// A worker at a carcass: grips it to drag it home with its nestmates, picks up the whole body of a small fallen
    /// nestmate for the midden, or cuts off a jawful. Returns the way to walk (zero when it took its load).
    Vector2 AtCarcass(int i, ref Ant a, FoodSource f, UnitStats st, ref float speedMul)
    {
        var c = Col[a.Team];
        bool bury = f.Team == a.Team && c.Fallen == FallenOrder.Honour && CarrionRules;
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

    /// An idle hero stands over a carcass and eats: each mouthful heals it and teaches it a little, more from big prey
    /// and most from a rival hero. Returns true while it feasts (it then stays put).
    bool Feast(int i, ref Ant a, float dt, bool think, ref Vector2 desired)
    {
        bool hungry = a.Hp < a.MaxHp * 0.999f || a.Level < Config.HeroMaxLevel;
        if (!hungry) { a.FoodTarget = null; return false; }
        var f = a.FoodTarget;
        if (f != null && (!f.Alive || f.Kind != FoodKind.Corpse || f.Feasted >= Config.FeastLoads(f.AntType) || !Edible(f, a.Team))) f = a.FoodTarget = null;
        if (f == null && think)
        {
            float best = (a.Size * 0.5f + 3f) * (a.Size * 0.5f + 3f);
            foreach (var g in W.Food)
            {
                if (g.Kind != FoodKind.Corpse || !g.Alive || g.Layer != a.Layer || g.Feasted >= Config.FeastLoads(g.AntType) || !Edible(g, a.Team)) continue;
                float d = g.Pos.DistanceSquaredTo(a.Pos);
                if (d < best) { best = d; f = g; }
            }
            a.FoodTarget = f;
        }
        if (f == null) { a.FeastT = 0; return false; }
        var to = f.Pos - a.Pos;
        float reach = f.Radius + a.Size * 0.4f + 0.4f;
        if (to.Length() > reach)
        {
            if (a.State == AntState.Hold) { a.FoodTarget = null; return false; } // a hero on guard does not wander off to eat
            desired = to;
            a.Anchor = f.Pos;
            return false;
        }
        a.Feasting = true;
        a.Anchor = a.Pos;
        a.Heading += MathX.AngleDiff(a.Heading, to.AngleD()) * 0.2f;
        a.FeastT += dt;
        desired = Vector2.Zero;
        if (a.FeastT < Config.FeastBite) return true;
        a.FeastT = 0;
        f.Amount -= 1;
        f.Feasted++;
        a.AttackAnim = 1;
        a.Hp = Mathf.Min(a.MaxHp, a.Hp + a.MaxHp * Config.FeastHeal);
        GrantXp(i, Config.FeastXp(f.AntType) / Config.FeastLoads(f.AntType));
        if (f.Team == a.Team) Sicken(Col[a.Team]);
        Events.Add(new SimEvent { Kind = EvKind.Feast, Pos = f.Pos, To = a.Pos, Team = a.Team, Type = f.AntType, FoodP1 = f.Id + 1, Who = i + 1 });
        return true;
    }

    /// Carcasses rot, slide along behind the workers dragging them, and are buried or eaten when they get home.
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
        bool bury = f.Team == team && Col[team].Fallen == FallenOrder.Honour;
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
        if (Mission?.Confined == true) p = Expedition.Constrain(p);
        f.Pos = p;
        f.Heading += MathX.AngleDiff(f.Heading, dir.AngleD() + DMath.Pi) * Mathf.Min(1, dt * 1.5f); // it comes rear first, the jaws pulling at its head
    }

    /// A carcass inside a nest never reaches the surface foragers: the nurses deal with it where it lies.
    void InNest(FoodSource f, float dt)
    {
        int team = f.Layer - 1;
        var c = Col[team];
        if (!c.Alive) return;
        if (f.Team == team && c.Fallen == FallenOrder.Honour && CarrionRules)
        {
            // carried up and out to the midden
            if (f.Age < Config.NestCarryOut) return;
            f.Amount = 0;
            Mourn(c, 1);
            Events.Add(new SimEvent { Kind = EvKind.Bury, Pos = Midden(team), Team = team, Type = f.AntType });
            return;
        }
        float take = Mathf.Min(f.Amount, Config.NestEatRate * dt);
        f.Amount -= take;
        float gain = take * Config.FoodValue(FoodKind.Corpse, Mission != null) * c.FoodMult * (Config.Favoured(c.Race, FoodKind.Corpse) ? Config.FoodBonus(c.Race) : 1f);
        gain = Mathf.Min(gain, Mathf.Max(0, c.FoodCap - c.Food));
        c.Food += gain;
        c.Gathered += gain;
        c.GatheredBy[(int)FoodKind.Corpse] += gain;
        c.ProteinT = Mathf.Min(Config.EffectMax, c.ProteinT + Config.EffectPerDelivery * take);
        if (f.Team == team) c.SickT = Mathf.Min(Config.MoraleMax, c.SickT + Config.MoraleTime * take);
    }
}
