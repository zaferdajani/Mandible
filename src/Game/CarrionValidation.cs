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
            Check(fed && spider.Feasted >= 3 && h.Hp > hp0 && h.Xp > xp0, "The idle, wounded hero did not feast");

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

            // Recycle: bodies eaten, carrion fever
            sim.SetFallen(0, FallenOrder.Recycle);
            float corpse0 = c.GatheredBy[(int)FoodKind.Corpse];
            foreach (int id in Doomed(4)) { sim.Ants[id].Pos = yard + MathX.Dir(id) * 1.5f; sim.Damage(id, 9999, -1); }
            float loads0 = 0; foreach (var f in sim.W.Food) if (f.Kind == FoodKind.Corpse && f.Team == 0 && f.Alive) loads0 += f.Amount;
            bool fever = false;
            for (int k = 0; k < 90 * 30; k++)
            {
                sim.Tick(dt); fever |= c.SickT > 0;
                if (k == 15 * 30)
                {
                    int bodies = 0, carriers = 0;
                    foreach (var f in sim.W.Food) if (f.Kind == FoodKind.Corpse && f.Team == 0 && f.Alive) bodies++;
                    for (int i = 0; i < sim.High; i++) if (sim.Ants[i].Alive && sim.Ants[i].Carrying && sim.Ants[i].CarryKind == FoodKind.Corpse && sim.Ants[i].CarryTeam == 0) carriers++;
                    GD.Print($"[carrion-test] recycle after {k / 30}s: {bodies} bodies lying, amounts {string.Join(",", sim.W.Food.FindAll(f => f.Kind == FoodKind.Corpse && f.Team == 0).ConvertAll(f => $"{f.Amount:0.0}@{f.Pos.DistanceTo(yard):0}/L{f.Layer}"))}, {carriers} carrying nestmates, food {c.Food:0}/{c.FoodCap:0}");
                }
            }
            float left = 0; foreach (var f in sim.W.Food) if (f.Kind == FoodKind.Corpse && f.Team == 0 && f.Alive) left += f.Amount;
            GD.Print($"[carrion-test] recycle: fever seen {fever}, nestmate loads left {left:0} of {loads0:0}, buried still {c.Buried} (protein credited +{c.GatheredBy[(int)FoodKind.Corpse] - corpse0:0}; full stores eat the surplus)");
            Check(fever && left < loads0 * 0.5f, "Recycled nestmates were not eaten");

            // a nestmate who dies in the galleries
            sim.SetFallen(0, FallenOrder.Honour);
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
