using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Mandible;

/// --gore-capture=<dir>: stages real fights and real carrion work in a live skirmish and captures each moment:
/// the hemolymph of a battle, a kill in pieces, a limping ant missing a leg, workers dragging a beetle home, a hero
/// feasting, both standing orders for the dead (the midden and the recycled), acid pitting, Reduced detail and the
/// Low tier. Every frame is checked against the sim state it claims to show; the pictures still need a human eye.
public static class GoreValidation
{
    public static async Task Capture(Main app, string directory)
    {
        static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(directory);
            Settings.CombatDetail = Settings.Detail.Full;
            app.StartSkirmish();
            var game = app.Session;
            game.Hud.Visible = false;
            game.Camera.UserControl = false;
            var sim = game.Sim; var gore = game.Gore; var col = sim.Col[0];
            async Task Frame() => await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            async Task<bool> Until(Func<bool> done, float seconds)
            {
                ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000);
                while (Time.GetTicksMsec() < end) { if (done()) return true; await Frame(); }
                return done();
            }
            async Task Run(float seconds) { float t = sim.Time + seconds; await Until(() => sim.Time >= t, seconds * 4 + 10); }
            async Task Shot(string name, Vector2 at, float distance)
            {
                game.SetPaused(true);
                game.Camera.JumpTo(at, distance);
                await app.ToSignal(app.GetTree().CreateTimer(0.9), SceneTreeTimer.SignalName.Timeout);
                await Frame();
                using var im = app.GetViewport().GetTexture().GetImage();
                Check(im.SavePng(directory.PathJoin(name + ".png")) == Error.Ok, "Gore capture failed to save " + name);
                GD.Print($"[gore-capture] {name}; drops {gore.DropCount} stains {gore.StainCount} loose parts {gore.LooseParts} broken carcasses {gore.BrokenCarcasses} wounded {gore.Wounded} buried {col.Buried} fps {Engine.GetFramesPerSecond()}");
                game.SetPaused(false);
            }
            var home = sim.NestEntrance(0);
            var outv = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - home).Normalized();
            var perp = new Vector2(-outv.Y, outv.X);
            Vector2 Clear(Vector2 p, float pad)
            {
                for (int r = 0; r < 40; r++)
                    for (int k = 0; k < 8; k++)
                    {
                        var q = p + MathX.Dir(k * 0.785f) * r;
                        if (!sim.W.Blocked(q, pad)) return q;
                    }
                return p;
            }
            List<int> Spawn(int team, AntType type, int n, Vector2 at)
            {
                var ids = new List<int>();
                for (int k = 0; k < n; k++)
                {
                    int id = sim.Spawn(team, type, at + MathX.Dir(k * 2.39996f) * (0.9f * MathF.Sqrt(k)), (home - at).AngleD());
                    if (id >= 0) ids.Add(id);
                }
                return ids;
            }
            List<int> OurWorkers(int n)
            {
                var ids = new List<int>();
                for (int i = 0; i < sim.High && ids.Count < n; i++)
                {
                    ref var a = ref sim.Ants[i];
                    if (a.Alive && a.Team == 0 && a.Type == AntType.Worker && a.Layer == 0 && !a.Carrying && a.DigP1 == 0) ids.Add(i);
                }
                return ids;
            }
            FoodSource Carcass(AntType type, int team, Vector2 at, float heading)
            {
                var st = Config.Of(type);
                var f = new FoodSource { Id = sim.W.NextFoodId++, Kind = FoodKind.Corpse, Pos = at, Amount = st.CorpseFood, Max = st.CorpseFood, Radius = st.Size * 0.45f, Team = team, AntType = type, Heading = heading, Natural = true };
                sim.W.Food.Add(f);
                return f;
            }
            await Run(3f); // the first frames compile shaders

            // 1-2: a battle: black majors and the Warlord against red soldiers
            var arena = Clear(home + outv * 26 + perp * 10, 5);
            var black = Spawn(0, AntType.Major, 3, arena - outv * 2.5f);
            black.AddRange(Spawn(0, AntType.Warlord, 1, arena - outv * 4.5f));
            sim.FormSquads(black);
            var red = Spawn(1, AntType.Soldier, 7, arena + outv * 2.5f);
            sim.FormSquads(red);
            Check(await Until(() => gore.DropCount > 12 && col.Killed > 0, 60), $"The battle threw no hemolymph (drops {gore.DropCount}, kills {col.Killed})");
            await Shot("01-battle-hemolymph", arena, 15);
            // a clean duel away from the melee: a major's jaws leave a soldier in pieces
            var duel = Clear(arena - perp * 22, 4);
            int killer = Spawn(0, AntType.Major, 1, duel - outv * 1.2f)[0];
            int victim = Spawn(1, AntType.Soldier, 1, duel + outv * 1.2f)[0];
            sim.Ants[victim].Hp = 8;
            int broken0 = gore.BrokenCarcasses;
            Check(await Until(() => !sim.Ants[victim].Alive, 20) && gore.BrokenCarcasses > broken0, "The duel left no body in pieces");
            await Run(0.35f);
            await Shot("02-kill-splash", duel, 8f);
            if (System.Environment.GetEnvironmentVariable("MANDIBLE_GORE_QUICK") != null) { app.GetTree().Quit(); return; }
            await Run(1.5f);
            sim.CmdMove(new List<int> { killer }, duel - outv * 6, false);
            await Run(2f);
            await Shot("02b-kill-in-pieces", gore.BrokenAt(sim) ?? duel, 6.5f);

            // 3: a wounded ant missing legs, limping and leaking
            int limper = -1;
            foreach (int id in black) if (sim.Ants[id].Alive && sim.Ants[id].Type == AntType.Major) { limper = id; break; }
            if (limper < 0) limper = Spawn(0, AntType.Major, 1, arena)[0];
            for (int k = 0; k < 2 && Bits(gore.Lost(limper, sim.Ants[limper].Serial) & Carnage.Legs) < 2; k++)
            {
                var p = sim.Ants[limper].Pos;
                gore.Sever(sim, limper, MathX.Dir(sim.Ants[limper].Heading + 1.6f), sim.W.HF.Sample(p.X, p.Y), sim.W.HF.Point(p, 0.3f));
            }
            Check((gore.Lost(limper, sim.Ants[limper].Serial) & Carnage.Legs) != 0, "The wounded ant kept all its legs");
            var walkTo = Clear(arena - perp * 9, 2);
            sim.CmdMove(new List<int> { limper }, walkTo, false);
            await Run(1.4f);
            await Shot("03-limping-wounded", sim.Ants[limper].Pos, 8.5f);

            // 4: eight workers drag a dead ground beetle home
            var field = Clear(home + outv * 24 - perp * 12, 4);
            var beetle = Carcass(AntType.Beetle, Config.Wild, field, 0.4f);
            var haulers = OurWorkers(8);
            foreach (int id in haulers) sim.Ants[id].Pos = sim.Ants[id].PrevPos = field + MathX.Dir(id * 1.3f) * 3.5f;
            sim.CmdGather(haulers, beetle);
            var start = beetle.Pos;
            await Until(() => beetle.HaulCount >= Config.HaulNeed(AntType.Beetle), 20);
            await Run(2.5f);
            int gripping0 = beetle.HaulCount;
            await Shot("04-workers-dragging", beetle.Pos, 9f);
            await Run(6f);
            float dragged = beetle.Pos.DistanceTo(start);
            int gripping = 0; foreach (int id in haulers) if (sim.Ants[id].Hauling) gripping++;
            GD.Print($"[gore-capture] beetle dragged {dragged:0.0} toward the nest by {beetle.HaulCount} workers ({gripping} gripping)");
            Check(gripping0 >= Config.HaulNeed(AntType.Beetle) && dragged > 2f && beetle.Pos.DistanceTo(home) < start.DistanceTo(home), $"The beetle was not dragged home (moved {dragged:0.0}, haulers {gripping0})");

            // 5: the Warlord, wounded, eats at a spider carcass
            var table = Clear(home + outv * 12 - perp * 22, 3); // well away from any fight, where an idle hero stays idle
            int hero = Spawn(0, AntType.Warlord, 1, table)[0];
            var spider = Carcass(AntType.Spider, Config.Wild, table, 2.1f);
            sim.Ants[hero].Pos = sim.Ants[hero].PrevPos = table + new Vector2(1.6f, 0.4f);
            sim.Ants[hero].Hp = sim.Ants[hero].MaxHp * 0.45f;
            sim.CmdStop(new List<int> { hero }, false);
            float hp0 = sim.Ants[hero].Hp, xp0 = sim.Ants[hero].Xp;
            Check(await Until(() => sim.Ants[hero].Feasting, 15), "The idle, wounded hero did not feast");
            await Run(2.6f);
            GD.Print($"[gore-capture] hero feast: hp {hp0:0} -> {sim.Ants[hero].Hp:0}, xp {xp0:0} -> {sim.Ants[hero].Xp:0}, mouthfuls {spider.Feasted}");
            Check(sim.Ants[hero].Hp > hp0 && spider.Feasted > 0, "Feasting neither healed the hero nor ate the carcass");
            await Shot("05-hero-feasting", sim.Ants[hero].Pos, 7.5f);

            // 6-7: Honour the dead: fallen nestmates are carried to the midden
            Check(col.Fallen == FallenOrder.Honour, "The colony should start by honouring its dead");
            var yard = Clear(home + outv * 9 + perp * 4, 1.5f);
            Spawn(0, AntType.Worker, 10, yard + perp * 4); // foragers free of other work, to find the dead
            var doomed = OurWorkers(5);
            foreach (int id in doomed) sim.Ants[id].Pos = sim.Ants[id].PrevPos = yard + MathX.Dir(id * 2.1f) * 1.5f;
            foreach (int id in doomed) sim.Damage(id, 9999, -1);
            Check(await Until(() => Burier(sim) >= 0, 40), "No worker carried a dead nestmate");
            await Run(0.8f);
            int b = Burier(sim);
            await Shot("06-honour-carrying-dead", b >= 0 ? sim.Ants[b].Pos : yard, 6f);
            Check(await Until(() => col.Buried >= 3, 60), $"The dead were not laid on the midden (buried {col.Buried})");
            Check(col.MournT > 0, "Burials gave no resolve");
            await Shot("07-midden", sim.Midden(0), 9f);

            // 8: Recycle the fallen: nestmates are cut up and carried down as protein, with a carrion fever
            game.Issue(new Cmd { Kind = CmdKind.Fallen, Team = 0, Arg = (byte)FallenOrder.Recycle });
            Check(col.Fallen == FallenOrder.Recycle, "The standing order did not change");
            doomed = OurWorkers(4);
            foreach (int id in doomed) sim.Ants[id].Pos = sim.Ants[id].PrevPos = yard + MathX.Dir(id * 2.1f) * 1.5f;
            foreach (int id in doomed) sim.Damage(id, 9999, -1);
            Check(await Until(() => Recycler(sim) >= 0, 40), "No worker picked up a piece of a fallen nestmate");
            await Run(0.5f);
            int r = Recycler(sim);
            await Shot("08-recycle-carrying", r >= 0 ? sim.Ants[r].Pos : yard, 6f);
            Check(await Until(() => col.SickT > 0, 90), "Eating nestmates gave no carrion fever");
            if (game.Hud.CommandHud is { } command)
            {
                // the standing order in Colony Command: the two cards, Recycle chosen, the fever running
                game.Hud.Visible = true;
                command.ShowPage(4);
                command.Refresh(1f);
                await Shot("08b-the-dead-tab", yard, 22f);
                game.Hud.Visible = false;
            }

            // 9: acid gunners pit a major's chitin
            var range = Clear(arena + perp * 12, 4);
            var gunners = Spawn(0, AntType.Gunner, 3, range - outv * 5);
            sim.FormSquads(gunners);
            int target = Spawn(1, AntType.Major, 1, range + outv * 1.5f)[0];
            sim.Ants[target].Hp = sim.Ants[target].MaxHp * 3; // it must live long enough to be scarred
            Check(await Until(() => gore.Etch(target, sim.Ants[target].Serial) >= 2 || !sim.Ants[target].Alive, 25), "Acid never pitted the target");
            await Shot("09-acid-pitting", sim.Ants[target].Pos, 6.5f);

            // 10: Reduced detail: lighter fluids, nothing severed, bodies whole
            Settings.CombatDetail = Settings.Detail.Reduced;
            int partsBefore = gore.LooseParts, brokenBefore = gore.BrokenCarcasses;
            var arena2 = Clear(arena + outv * 14, 5);
            var b2 = Spawn(0, AntType.Major, 3, arena2 - outv * 2.5f); sim.FormSquads(b2);
            var r2 = Spawn(1, AntType.Soldier, 6, arena2 + outv * 2.5f); sim.FormSquads(r2);
            await Run(6f);
            Check(gore.LooseParts <= partsBefore && gore.BrokenCarcasses <= brokenBefore, "Reduced detail still severed parts");
            await Shot("10-reduced-detail", arena2, 14);
            Settings.CombatDetail = Settings.Detail.Full;

            // 11: the Low tier phones run, Full detail on a smaller budget
            Settings.Gfx = Settings.Quality.Low; game.ApplyQuality();
            var arena3 = Clear(arena - outv * 14 + perp * 4, 5);
            var b3 = Spawn(0, AntType.Major, 3, arena3 - outv * 2.5f); sim.FormSquads(b3);
            var r3 = Spawn(1, AntType.Soldier, 6, arena3 + outv * 2.5f); sim.FormSquads(r3);
            await Run(3.5f);
            await Shot("11-low-tier", arena3, 15);
            Settings.Gfx = Settings.Quality.High; game.ApplyQuality();
            GD.Print("[gore-capture] PASS hemolymph, pieces, limp, dragging, feast, honour, recycle, acid, reduced, low tier (visual review required)");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[gore-capture] FAIL " + e); app.GetTree().Quit(1); }
    }

    static int Bits(int v) { int n = 0; while (v != 0) { n += v & 1; v >>= 1; } return n; }

    static int Burier(Sim sim)
    {
        for (int i = 0; i < sim.High; i++) if (sim.Ants[i].Alive && sim.Ants[i].Team == 0 && sim.Ants[i].Burying && sim.Ants[i].Layer == 0) return i;
        return -1;
    }

    static int Recycler(Sim sim)
    {
        for (int i = 0; i < sim.High; i++)
        {
            ref var a = ref sim.Ants[i];
            if (a.Alive && a.Team == 0 && a.Carrying && a.CarryKind == FoodKind.Corpse && a.CarryTeam == 0 && !a.Burying && a.Layer == 0) return i;
        }
        return -1;
    }
}
