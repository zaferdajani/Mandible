using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// --carrion-test (headless): the carrion rules on a bare skirmish sim. A wounded idle hero feasts and heals, a team
/// of workers drags a beetle home, fallen nestmates are carried to the midden under Honour and eaten under Recycle,
/// a nestmate who dies inside the nest is dealt with by the nurses, and carcasses rot.
public static class CarrionValidation
{
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    public static void Run(Main app)
    {
        try
        {
            var root = new Node3D(); app.AddChild(root);
            var sim = new Sim(WorldBuilder.Build(root, 20261031)); sim.BeginSkirmish();
            const float dt = 1f / 30;
            void Ticks(float seconds) { for (int k = 0; k < seconds * 30; k++) sim.Tick(dt); }
            var home = sim.NestEntrance(0);
            var outv = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - home).Normalized();
            var perp = new Vector2(-outv.Y, outv.X);
            Vector2 Clear(Vector2 p)
            {
                for (int r = 0; r < 40; r++) for (int k = 0; k < 8; k++) { var q = p + MathX.Dir(k * 0.785f) * r; if (!sim.W.Blocked(q, 4)) return q; }
                return p;
            }
            FoodSource Carcass(AntType type, int team, Vector2 at)
            {
                var st = Config.Of(type);
                var f = new FoodSource { Id = sim.W.NextFoodId++, Kind = FoodKind.Corpse, Pos = at, Amount = st.CorpseFood, Max = st.CorpseFood, Radius = st.Size * 0.45f, Team = team, AntType = type, Natural = true };
                sim.W.Food.Add(f);
                return f;
            }
            Ticks(2);

            // the feast
            var table = Clear(home + outv * 14 - perp * 20);
            var spider = Carcass(AntType.Spider, Config.Wild, table);
            int hero = sim.Spawn(0, AntType.Warlord, table + new Vector2(1.6f, 0.4f), 0);
            sim.Ants[hero].Hp = sim.Ants[hero].MaxHp * 0.45f;
            float hp0 = sim.Ants[hero].Hp, xp0 = sim.Ants[hero].Xp;
            bool fed = false;
            for (int k = 0; k < 6 * 30; k++) { sim.Tick(dt); fed |= sim.Ants[hero].Feasting; }
            ref var h = ref sim.Ants[hero];
            GD.Print($"[carrion-test] feast: hp {hp0:0} -> {h.Hp:0}, xp {xp0:0} -> {h.Xp:0}, mouthfuls {spider.Feasted}, state {h.State}, feasting {h.Feasting}, distance {h.Pos.DistanceTo(spider.Pos):0.0}");
            Check(fed && spider.Feasted >= 3 && h.Hp > hp0 && h.Xp == xp0 && h.Level == 1, "The idle, wounded general did not feast and mend (or eating levelled it)");

            // the drag
            var field = Clear(home + outv * 26 + perp * 10);
            var beetle = Carcass(AntType.Beetle, Config.Wild, field);
            var crew = new List<int>();
            for (int i = 0; i < sim.High && crew.Count < 8; i++)
                if (sim.Ants[i].Alive && sim.Ants[i].Team == 0 && sim.Ants[i].Type == AntType.Worker && sim.Ants[i].Layer == 0) crew.Add(i);
            foreach (int id in crew) sim.Ants[id].Pos = sim.Ants[id].PrevPos = field + MathX.Dir(id * 1.3f) * 3.5f;
            sim.CmdGather(crew, beetle);
            float d0 = beetle.Pos.DistanceTo(home);
            int most = 0;
            for (int k = 0; k < 8 * 30; k++) { sim.Tick(dt); most = Math.Max(most, beetle.HaulCount); }
            GD.Print($"[carrion-test] drag: beetle {d0:0.0} -> {beetle.Pos.DistanceTo(home):0.0} from the nest mouth, up to {most} workers gripping, hauled home {beetle.Hauled}");
            Check(most >= Config.HaulNeed(AntType.Beetle) && beetle.Hauled, "Workers did not drag the beetle home");

            // Honour: bodies to the midden
            var c = sim.Col[0];
            List<int> Doomed(int n)
            {
                var ids = new List<int>();
                for (int i = 0; i < sim.High && ids.Count < n; i++)
                    if (sim.Ants[i].Alive && sim.Ants[i].Team == 0 && sim.Ants[i].Type == AntType.Worker && sim.Ants[i].Layer == 0 && !sim.Ants[i].Carrying) ids.Add(i);
                return ids;
            }
            var yard = Clear(home + outv * 8 + perp * 4);
            for (int k = 0; k < 12; k++) sim.Spawn(0, AntType.Worker, yard + MathX.Dir(k * 2.4f) * 4f, k); // foragers of their own, free of other work
            foreach (int id in Doomed(4)) { sim.Ants[id].Pos = yard + MathX.Dir(id) * 1.5f; sim.Damage(id, 9999, -1); }
            float food0 = c.Food;
            Ticks(60);
            GD.Print($"[carrion-test] honour: buried {c.Buried}, resolve {c.MournT:0}s, midden {sim.Midden(0).DistanceTo(home):0.0} from the nest mouth");
            Check(c.Buried >= 3 && c.MournT > 0, "The dead were not carried to the midden");

            // Haul: the enemy dead are cut up and carried home into the organic reserve (recruited foragers stream out to them)
            sim.SetRemains(0, RemainsOrder.Haul);
            float org0 = c.Organic, hauled0 = c.OrganicGathered;
            var field2 = Clear(home + outv * 30 - perp * 8);
            for (int k = 0; k < 4; k++) { int e = sim.Spawn(1, AntType.Soldier, field2 + MathX.Dir(k * 1.6f) * 1.5f, 0); sim.Damage(e, 9999, -1); }
            int scout = sim.Spawn(0, AntType.Soldier, field2 + new Vector2(2, 0), 0); // one of ours has found them
            float value0 = 0; foreach (var f in sim.W.Food) if (f.Kind == FoodKind.Corpse && f.Team == 1 && f.Alive) value0 += Sim.RemainsValue(f);
            for (int k = 0; k < 90 * 30; k++)
            {
                sim.Tick(dt);
                if (k == 20 * 30)
                {
                    int carriers = 0, assigned = 0;
                    for (int i = 0; i < sim.High; i++)
                    {
                        ref var a = ref sim.Ants[i];
                        if (!a.Alive || a.Team != 0 || a.Type != AntType.Worker) continue;
                        if (a.Carrying && a.CarryKind == FoodKind.Corpse && a.CarryTeam == 1) carriers++;
                        if (a.Assigned != null && a.Assigned.Kind == FoodKind.Corpse) assigned++;
                    }
                    GD.Print($"[carrion-test] haul after 20s: {carriers} carrying enemy remains, {assigned} sent to remains, reserve {c.Organic:0.0}");
                }
            }
            float value1 = 0; foreach (var f in sim.W.Food) if (f.Kind == FoodKind.Corpse && f.Team == 1 && f.Alive) value1 += Sim.RemainsValue(f);
            GD.Print($"[carrion-test] haul: enemy remains {value0:0.0} -> {value1:0.0} organic on the ground, hauled +{c.OrganicGathered - hauled0:0.0}, reserve {org0:0.0} -> {c.Organic:0.0}");
            Check(c.OrganicGathered - hauled0 > value0 * 0.4f, "Enemy remains were not hauled into the organic reserve");

            // a nestmate who dies in the galleries
            int below = -1;
            for (int i = 0; i < sim.High; i++) if (sim.Ants[i].Alive && sim.Ants[i].Team == 0 && sim.Ants[i].Layer > 0 && sim.Ants[i].Type == AntType.Worker) { below = i; break; }
            if (below >= 0)
            {
                int buried = c.Buried;
                sim.Damage(below, 9999, -1);
                bool kept = sim.W.Food.Exists(f => f.Kind == FoodKind.Corpse && f.Layer > 0 && f.Alive);
                Ticks(Config.NestCarryOut + 1);
                GD.Print($"[carrion-test] nest death: carcass kept below {kept}, carried out to the midden {c.Buried > buried}");
                Check(kept && c.Buried > buried, "A nestmate who died below was not carried out");
            }

            // a kill alone makes no one stronger, and eating in the field only mends (docs/progression.md: levels come
            // from the evolve tree)
            var mess = Clear(home + outv * 20 + perp * 20);
            int fighter = sim.Spawn(0, AntType.Soldier, mess + new Vector2(1.6f, 0), 0);
            int victim = sim.Spawn(1, AntType.Soldier, mess + new Vector2(2.4f, 0), 0);
            sim.Ants[victim].Hp = 1;
            float hp1 = sim.Ants[fighter].MaxHp; int level1 = sim.Ants[fighter].Level;
            for (int k = 0; k < 4 * 30 && sim.Ants[victim].Alive; k++) sim.Tick(dt);
            Check(!sim.Ants[victim].Alive && sim.Ants[fighter].Level == level1 && sim.Ants[fighter].MaxHp == hp1, "A kill alone made the soldier stronger");
            sim.SetRemains(0, RemainsOrder.Leave); // the workers leave it for the army
            var prey = Carcass(AntType.Beetle, Config.Wild, mess);
            sim.Ants[fighter].Hp = hp1 * 0.4f;
            sim.CmdFeast(new List<int> { fighter }, prey);
            for (int k = 0; k < 60 * 30 && sim.Ants[fighter].Hp < hp1 * 0.999f; k++) sim.Tick(dt);
            ref var fa = ref sim.Ants[fighter];
            GD.Print($"[carrion-test] eating: soldier mended to {100 * fa.Hp / fa.MaxHp:0}%, max hp {hp1:0} -> {fa.MaxHp:0}, beetle left {Sim.RemainsValue(prey):0.0} organic");
            Check(fa.Hp >= hp1 * 0.99f && fa.MaxHp == hp1 && fa.Level == level1, "Eating a beetle must mend the soldier and nothing more");
            sim.SetRemains(0, RemainsOrder.Haul);

            // the queen ascends on organic matter, and larvae need some
            sim.Col[0].Organic = 0;
            sim.Col[0].Food = 5000;
            sim.Col[0].Researching = false;
            GD.Print($"[carrion-test] queen tier {sim.Col[0].T(Upgrade.Queen)}");
            sim.Col[0].Tier[(int)Upgrade.Queen] = 1;
            var rr = sim.CanResearch(0, Upgrade.Queen);
            var qr = sim.CanEnqueue(0, ProdKind.Major);
            GD.Print($"[carrion-test] costs: queen with no organic matter -> {rr}; a major squad -> {qr}");
            Check(rr == ResearchResult.NoOrganic, "The queen ascended without organic matter");
            sim.Col[0].Organic = 50;

            // body parts (docs/progression.md 1c): a mutation unit costs one part of its insect, carried home with the remains
            {
                var pc = sim.Col[0];
                pc.Queue.Clear();
                sim.GiveMutation(0, Mutation.Winged); sim.GiveMutation(0, Mutation.Web);
                var bare = (sim.CanEnqueue(0, ProdKind.WebAnt), sim.CanEnqueue(0, ProdKind.Flyer), sim.CanEnqueue(0, ProdKind.Soldier));
                Check(bare == (QueueResult.NoPart, QueueResult.NoPart, QueueResult.Ok), $"With no parts in store only the plain castes may be laid (web ant, winged ant, soldier: {bare})");
                var den = Clear(home + outv * 16 + perp * 12);
                int denSpider = sim.Spawn(Config.Wild, AntType.Spider, den, 0);
                sim.Damage(denSpider, 99999, -1);
                FoodSource body = null;
                foreach (var f in sim.W.Food) if (f.Kind == FoodKind.Corpse && f.AntType == AntType.Spider && f.Part != 0) body = f;
                Check(body != null && body.Part == (byte)Part.Gland + 1, "A dead spider did not keep its silk gland");
                int ticks = 0;
                for (; ticks < 150 * 30 && pc.Parts[(int)Part.Gland] == 0; ticks++) sim.Tick(dt);
                Check(pc.Parts[(int)Part.Gland] == 1 && body.Part == 0, "The workers did not bring the silk gland home with the remains");
                int squad = Sim.PartCount(ProdKind.WebAnt);
                Check(squad == Config.SquadSize(ProdKind.WebAnt) && sim.CanEnqueue(0, ProdKind.WebAnt) == QueueResult.NoPart, "One silk gland must not pay for a whole squad of web ants");
                pc.Parts[(int)Part.Gland] = squad + 1;
                Check(sim.CanEnqueue(0, ProdKind.WebAnt) == QueueResult.Ok && sim.CanEnqueue(0, ProdKind.Flyer) == QueueResult.NoPart, "Silk glands must open web ants and nothing else");
                sim.Enqueue(0, ProdKind.WebAnt);
                Check(pc.Parts[(int)Part.Gland] == 1 && sim.CanEnqueue(0, ProdKind.WebAnt) == QueueResult.NoPart, "A brood must cost one part for each ant");
                sim.CancelLast(0);
                Check(pc.Parts[(int)Part.Gland] == squad + 1, "A cancelled brood did not give its parts back");
                sim.GiveMutation(0, Mutation.Stinger);
                pc.Parts[(int)Part.Wings] = 8;
                Check(sim.CanEnqueue(0, ProdKind.Flyer) == QueueResult.NoPart, "With the wasp's core a winged ant must cost a sting, not wings");
                pc.Parts[(int)Part.Sting] = Sim.PartCount(ProdKind.Flyer);
                Check(sim.CanEnqueue(0, ProdKind.Flyer) == QueueResult.Ok, "Stings in store did not open a brood of stingers");
                GD.Print($"[carrion-test] parts: the spider's silk gland came home in {ticks / 30f:0}s; a brood costs one part for each ant, a cancelled brood gives them back, stingers cost stings");
            }

            // Devour the Fallen: only the fire ants' Warlord eats its own kind
            var pit = Clear(home + outv * 70 - perp * 30); // beyond the black colony's collection ring: its haulers would come for the red dead
            var redDead = Carcass(AntType.Soldier, 1, pit);
            int blackWarlord = sim.Spawn(0, AntType.Warlord, pit + new Vector2(1.4f, 0), 0);
            var blackDead = Carcass(AntType.Soldier, 0, pit + new Vector2(30, 0));
            Check(!sim.CanEat(sim.Ants[blackWarlord], blackDead), "The black Warlord may eat its own dead");
            Check(sim.CanEat(sim.Ants[blackWarlord], redDead), "The black Warlord may not eat the enemy's dead");
            sim.Ants[blackWarlord].Alive = false; sim.Col[0].Count[(int)AntType.Warlord]--;
            int redWarlord = sim.Spawn(1, AntType.Warlord, pit + new Vector2(1.4f, 0), 0);
            sim.Ants[redWarlord].Hp = sim.Ants[redWarlord].MaxHp * 0.5f;
            float xpR = sim.Ants[redWarlord].Xp, hpR = sim.Ants[redWarlord].Hp;
            Check(sim.Cannibal(sim.Ants[redWarlord]) && sim.CanEat(sim.Ants[redWarlord], redDead), "The fire-ant Warlord cannot devour its own fallen");
            sim.CmdFeast(new List<int> { redWarlord }, redDead);
            Ticks(6);
            GD.Print($"[carrion-test] devour: red Warlord xp {xpR:0} -> {sim.Ants[redWarlord].Xp:0}, hp {hpR:0} -> {sim.Ants[redWarlord].Hp:0}, its own dead left {redDead.Amount:0.0} loads");
            Check(sim.Ants[redWarlord].Xp == xpR && sim.Ants[redWarlord].Hp > hpR, "Devour the Fallen did not mend the Warlord (or eating taught it)");

            // rot
            var old = Carcass(AntType.Soldier, 1, Clear(home + outv * 30));
            Ticks(Config.CarcassFresh + Config.CarcassRot * 0.5f);
            GD.Print($"[carrion-test] rot: soldier carcass {old.Max:0} -> {old.Amount:0.0} loads after {Config.CarcassFresh + Config.CarcassRot * 0.5f:0}s");
            Check(old.Amount < old.Max * 0.6f, "Carcasses did not rot");
            GD.Print("[carrion-test] ALL PASS");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[carrion-test] FAIL " + e); app.GetTree().Quit(1); }
    }
}
