using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Mandible;

public static class ColonyValidation
{
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static List<int> Units(Sim s, AntType type)
    { var ids = new List<int>(); for (int i = 0; i < s.High; i++) if (s.Ants[i].Alive && s.Ants[i].Team == 0 && s.Ants[i].Type == type) ids.Add(i); return ids; }
    static void Until(Sim sim, Func<bool> done, int seconds, string reason)
    {
        for (int t = 0; t < seconds * 30 && !done(); t++)
        {
            sim.Tick(1f / 30);
            Check(sim.Col[0].Alive, "Queen died during " + reason);
            for (int i = 0; i < sim.High; i++) if (sim.Ants[i].Alive && sim.Ants[i].Layer == 0)
                Check(Expedition.Contains(sim.Ants[i].Pos), "An ant escaped the confined trail");
        }
        Check(done(), $"Timed out: {reason}; stage {sim.Mission?.Stage}; queen {sim.Ants[sim.Col[0].QueenId].Pos}; food {sim.Col[0].Food}");
        GD.Print("[campaign-test] PASS " + reason);
    }
    public static void Campaign(Main app)
    {
        try
        {
            Settings.Gfx = Settings.Quality.Low;
            var root = new Node3D(); app.AddChild(root);
            var w = WorldBuilder.Build(root, 20261031, firstNest: true);
            var s = new Sim(w); var c = s.Col[0];
            Check(s.Mission != null && c.Chambers.Count == 0 && !c.Founded, "Mission did not start on the trail");
            Check(!s.OrderEntrance(0, Units(s, AntType.Worker)), "Entrance allowed before reaching clearing");
            for (int i = 0; i < 900; i++) s.Tick(1f / 30);
            Check(!c.Founded && s.Winner < 0, "Mission auto-founded or won without the player's orders");
            Check(s.CanResearch(0, Upgrade.Queen) == ResearchResult.Locked && s.CanEnqueue(0, ProdKind.Major) == QueueResult.Locked, "Episode technology gate failed");
            var army = Units(s, AntType.Soldier);
            s.CmdMove(army, Expedition.Ambush, true);
            Until(s, () => s.Mission.Stage == ExpeditionStage.EscortQueen, 120, "attackers clear the enemy patrol");
            s.CmdMove(Units(s, AntType.Soldier), Expedition.Clearing + new Vector2(8, 0), false);
            Until(s, () => s.Mission.Stage == ExpeditionStage.DigEntrance, 150, "queen and workers reach the food clearing");
            Check(!s.OrderEntrance(0, Units(s, AntType.Soldier)), "Soldiers could order the founding dig");
            Check(s.OrderEntrance(0, Units(s, AntType.Worker)), "Selected workers could not start the entrance");
            Check(!s.OrderEntrance(0, Units(s, AntType.Worker)), "Duplicate entrance order accepted");
            Until(s, () => s.Mission.Stage == ExpeditionStage.Incubator, 180, "workers dig and queen enters the royal chamber");
            Check(s.ChamberCount(0, ChamberKind.Brood, false) == 0 && s.ChamberCount(0, ChamberKind.Storage, false) == 0, "Founding gifted rooms instead of teaching construction");
            Check(s.CanEnqueue(0, ProdKind.Worker) == QueueResult.Locked, "Production allowed without an incubator");
            Check(s.CanDig(0, ChamberKind.Storage) != Sim.DigResult.Ok, "Storage allowed before incubator");
            int capacity = c.PopCap;
            Check(s.Dig(0, ChamberKind.Brood, s.DefaultSpot(0, ChamberKind.Brood)) == Sim.DigResult.Ok, "Cannot place incubator");
            Until(s, () => s.Mission.Stage == ExpeditionStage.Store, 180, "incubator completes");
            Check(c.PopCap == capacity + Config.ChamberPop, "Incubator did not increase population capacity");
            Check(s.Dig(0, ChamberKind.Storage, s.DefaultSpot(0, ChamberKind.Storage)) == Sim.DigResult.Ok, "Cannot place storage");
            Until(s, () => s.Mission.Stage == ExpeditionStage.RaiseWorkers, 180, "storage completes");
            for (int i = 0; i < Expedition.WorkerGoal; i++)
            {
                Until(s, () => s.CanEnqueue(0, ProdKind.Worker) == QueueResult.Ok, 120, "economy can pay for a worker");
                Check(s.Enqueue(0, ProdKind.Worker) == QueueResult.Ok, "Worker queue rejected");
            }
            Until(s, () => s.Mission.Stage == ExpeditionStage.RaiseSoldiers, 180, "four workers hatch inside the incubator");
            Until(s, () => s.CanEnqueue(0, ProdKind.Soldier) == QueueResult.Ok, 240, "foragers fund a fighting squad");
            Check(c.Gathered > 0, "No actual food was gathered");
            Check(s.Enqueue(0, ProdKind.Soldier) == QueueResult.Ok, "Soldier queue rejected");
            Until(s, () => s.Winner == 0, 180, "six soldiers hatch and complete episode one");
            Check(s.Mission.Stage == ExpeditionStage.Complete, "Wrong victory condition");
            capacity = c.PopCap;
            var expansionSpot = s.DefaultSpot(0, ChamberKind.Brood);
            var expansionResult = s.Dig(0, ChamberKind.Brood, expansionSpot);
            Check(expansionResult == Sim.DigResult.Ok, $"Cannot expand with a second incubator: {expansionResult}, spot {expansionSpot}, food {c.Food:0}");
            Until(s, () => s.ChamberCount(0, ChamberKind.Brood, true) == 2, 180, "second incubator completes");
            Check(c.PopCap == capacity + Config.ChamberPop, "Second incubator failed to add capacity");
            // A separate unrestricted simulation verifies the full roster without touching episode gates.
            var other = new Node3D(); app.AddChild(other);
            var sk = new Sim(WorldBuilder.Build(other, 20261031)); sk.BeginSkirmish();
            foreach (var kind in new[] { ProdKind.Worker, ProdKind.Soldier, ProdKind.Major, ProdKind.Flyer, ProdKind.Gunner, ProdKind.Warlord, ProdKind.Matron, ProdKind.Marshal })
                Check(sk.CanEnqueue(0, kind) == QueueResult.Ok, $"Skirmish caste locked: {kind}");
            Check(sk.Col[0].Chambers.Count == 7 && sk.Col[1].Chambers.Count == 7, "Skirmish lacks the seven chamber identities");
            int skirmishCapacity = sk.Col[0].PopCap;
            sk.Col[0].PopCap = Config.MaxPopCap;
            Check(sk.CanDig(0, ChamberKind.Brood) == Sim.DigResult.TooMany, "Colony can spend food on incubators beyond the population ceiling");
            sk.Col[0].PopCap = skirmishCapacity;
            Check(sk.Enqueue(0, ProdKind.Worker) == QueueResult.Ok, "Skirmish worker queue failed");
            int queen = sk.Col[0].QueenId; var queenPosition = sk.Ants[queen].Pos;
            sk.Ants[queen].Layer = 0; sk.Ants[queen].Pos = sk.W.Nests[0].Pos + new Vector2(50, 0);
            for (int i = 0; i < 30; i++) sk.Tick(1f / 30);
            Check(sk.Col[0].Queue[0].Progress == 0, "Eggs progressed while the queen was outside");
            sk.Ants[queen].Layer = (byte)Sim.NestLayer(0); sk.Ants[queen].Pos = queenPosition;
            sk.Tick(1f / 30); Check(sk.Col[0].Queue[0].Progress > 0, "Brood did not resume when queen returned");
            GD.Print($"[campaign-test] ALL PASS; mission {s.Time:0}s, gathered {c.Gathered:0}, workers {s.Mission.WorkersHatched}, soldiers {s.Mission.SoldiersHatched}; full skirmish roster unlocked");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[campaign-test] FAIL " + e); app.GetTree().Quit(1); }
    }

    public static async Task Capture(Main app, string directory)
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(directory);
            async Task Wait() { await app.ToSignal(app.GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout); await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
            async Task Shot(string name) { await Wait(); using var im = app.GetViewport().GetTexture().GetImage(); Check(im.SavePng(directory.PathJoin(name + ".png")) == Error.Ok, "Screenshot failed"); GD.Print("[command-capture] " + name); }
            app.StartSkirmish(); var g = app.Session;
            g.Sim.Tick(1f / 30); g.SetPaused(true); g.SetNestView(true);
            var home = g.World.Nests[0].Pos;
            g.Camera.SetYaw(0, true); g.Camera.NestView = true; g.Camera.JumpTo(home + new Vector2(0, 5), 39);
            g.Input.SelectedRoom = g.Sim.ChamberOf(0, ChamberKind.Brood).Id;
            g.Sim.Enqueue(0, ProdKind.Soldier); g.Sim.Enqueue(0, ProdKind.Worker);
            await Shot("01-skirmish-command");
            // Input events exercise the real GUI rather than invoking button handlers directly.
            async Task Click(float x, float y, MouseButton button = MouseButton.Left)
            {
                var size = app.GetTree().Root.Size;
                var p = new Vector2(x * size.X / 1672, y * size.Y / 941);
                Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p });
                Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = button, Pressed = true });
                await app.ToSignal(app.GetTree(), SceneTree.SignalName.ProcessFrame);
                Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = button, Pressed = false });
                await app.ToSignal(app.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            await Click(980, 701); await Shot("02-research-tab");
            await Click(1090, 701); await Shot("03-heroes-tab");
            await Click(850, 701);
            int queued = g.Sim.Col[0].Queue.Count;
            await Click(742, 775);
            Check(g.Sim.Col[0].Queue.Count == queued + 1, "Worker portrait did not enqueue through real mouse input");
            await Click(1598, 885);
            Check(g.Input.Selection.Count == g.Sim.Col[0].Count[(int)AntType.Worker], "Workers command did not select the workforce");
            // Placement and its preview use the same underground plane as chamber picking.
            var site = home + new Vector2(20, 2);
            Check(g.Sim.PlaceCheck(0, site) == Sim.DigResult.Ok, "Invalid placement fixture");
            var screen = g.Camera.Cam.UnprojectPosition(new Vector3(site.X, g.World.HF.Sample(home.X, home.Y) - NestGeometry.Depth, site.Y));
            var viewport = g.GetViewport().GetVisibleRect().Size;
            int rooms = g.Sim.Col[0].Chambers.Count;
            await Click(160, 125);
            await Click(screen.X * 1672 / viewport.X, screen.Y * 941 / viewport.Y);
            Check(g.Sim.Col[0].Chambers.Count == rooms + 1, "Room did not place through real mouse input");
            Check(g.Sim.ChamberPos(0, g.Sim.Col[0].Chambers[^1]).DistanceTo(site) < 0.1f, "Room placement drifted from the visible underground cursor");
            g.SetNestView(false); await Shot("04-surface-command");
            app.StartTutorial(); g = app.Session; g.Sim.Tick(1f / 30); g.SetPaused(true);
            await Shot("05-episode-opening");
            app.StartBattle(Difficulty.Normal, 0); g = app.Session; g.SetPaused(true);
            g.Sim.DevelopNest(0); g.Sim.Tick(1f / 30); g.Sim.Col[0].Food = g.Sim.Col[0].FoodCap;
            await Wait(); await Click(980, 701);
            float researchFood = g.Sim.Col[0].Food;
            await Click(742, 775);
            Check(g.Sim.Col[0].Researching, "Research portrait did not start an upgrade");
            await Wait(); await Click(742, 775, MouseButton.Right);
            Check(!g.Sim.Col[0].Researching && g.Sim.Col[0].Food == researchFood, "Research cancellation did not refund food");
            GD.Print("[command-capture] PASS live tabs, portrait queue, worker selection, underground placement, surface, episode opening and research start/cancel/refund");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[command-capture] FAIL " + e); app.GetTree().Quit(1); }
    }
}
