using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Mandible;

/// Scripted screenshot tour for automated visual review:  -- --capture=<dir>
public static class Capture
{
    static Main _app;
    static string _dir;

    public static async Task Run(Main app, string dir)
    {
        _app = app;
        _dir = dir;
        DirAccess.MakeDirRecursiveAbsolute(dir);
        await Wait(6);
        await Shot("01_menu");

        app.StartBattle(Difficulty.Normal);
        await Wait(3);
        var s = app.Session;
        var nest = s.World.Nests[Config.Player].Pos;
        s.Camera.JumpTo(nest + new Vector2(6, -8), 58);
        await Wait(2);
        await Shot("02_nest");
        s.Camera.JumpTo(nest + new Vector2(5, -7), 18);
        await Wait(2);
        await Shot("03_closeup");

        await InputSelfTest(s);

        // let the economy run
        s.Autopilot(true);
        s.FastForward = 7;
        await Wait(22);
        s.FastForward = 0;
        s.Autopilot(false);
        Log(s, "after fast-forward");
        s.Camera.JumpTo(nest + new Vector2(30, -30), 110);
        s.ToggleTrails();
        await Wait(2.5f);
        await Shot("04_trails");
        s.ToggleTrails();

        // stage a battle in the middle of the yard
        var c = new Vector2(Config.MapSize, Config.MapSize) * 0.5f;
        var mine = new List<int>();
        var theirs = new List<int>();
        var rng = new Rng(5);
        for (int i = 0; i < 46; i++)
        {
            mine.Add(s.Sim.Spawn(Config.Player, i % 9 == 0 ? AntType.Major : AntType.Soldier, c + new Vector2(-26, 22) + rng.InDisk(9), -0.8f));
            theirs.Add(s.Sim.Spawn(Config.Enemy, i % 10 == 0 ? AntType.Major : AntType.Soldier, c + new Vector2(26, -22) + rng.InDisk(9), 2.3f));
        }
        s.Sim.CmdMove(mine, c + new Vector2(10, -8), true);
        s.Sim.CmdMove(theirs, c + new Vector2(-10, 8), true);
        s.Camera.JumpTo(c + new Vector2(-6, 6), 46);
        await Wait(4.5f);
        await Shot("05_battle");
        s.Camera.JumpTo(c + new Vector2(-3, 3), 22);
        await Wait(2f);
        await Shot("06_battle_close");
        s.Input.SelectArmy();
        s.Camera.JumpTo(c + new Vector2(-8, 8), 70);
        await Wait(2.5f);
        await Shot("07_selected");
        Log(s, "end");
        _app.GetTree().Quit();
    }

    /// Drives real input events through Godot: box-select, right-click move, hotkeys.
    static async Task InputSelfTest(GameSession s)
    {
        var vp = _app.GetViewport().GetVisibleRect().Size;
        s.Camera.JumpTo(s.World.Nests[Config.Player].Pos, 60);
        await Wait(1);
        void Mouse(MouseButton b, bool pressed, Vector2 at)
        {
            var e = new InputEventMouseButton { ButtonIndex = b, Pressed = pressed, Position = at, GlobalPosition = at };
            Input.ParseInputEvent(e);
        }
        void Move(Vector2 at)
        {
            Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at, ButtonMask = MouseButtonMask.Left });
        }
        void Key(Key k)
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
        }
        // drag a box over the middle of the screen
        var a = vp * new Vector2(0.2f, 0.12f);
        var b = vp * new Vector2(0.8f, 0.68f);
        Mouse(MouseButton.Left, true, a);
        await Frames(2);
        Move((a + b) / 2);
        await Frames(2);
        Move(b);
        await Frames(2);
        Mouse(MouseButton.Left, false, b);
        await Frames(3);
        int selected = s.Input.Selection.Count;
        GD.Print($"[selftest] box-select picked {selected} ants  {(selected > 0 ? "PASS" : "FAIL")}");
        // right-click a point on the ground
        var target = vp * new Vector2(0.62f, 0.3f);
        Mouse(MouseButton.Right, true, target);
        await Frames(1);
        Mouse(MouseButton.Right, false, target);
        await Frames(3);
        int moving = 0;
        foreach (int id in s.Input.Selection) if (s.Sim.Ants[id].State is AntState.Move or AntState.Gather) moving++;
        GD.Print($"[selftest] right-click ordered {moving}/{selected} ants  {(moving > 0 && moving == selected ? "PASS" : "FAIL")}");
        // hotkeys: worker + soldier into the brood queue, Esc toggles pause
        int before = s.Sim.Col[Config.Player].Queue.Count;
        Key(Godot.Key.Z);
        Key(Godot.Key.X);
        await Frames(3);
        int after = s.Sim.Col[Config.Player].Queue.Count;
        GD.Print($"[selftest] Z/X queued {after - before} brood  {(after - before == 2 ? "PASS" : "FAIL")}");
        Key(Godot.Key.Escape);
        await Frames(3);
        bool paused = s.Hud.Blocking;
        Key(Godot.Key.Escape);
        await Frames(3);
        GD.Print($"[selftest] Esc pause/resume  {(paused && !s.Hud.Blocking ? "PASS" : "FAIL")}");
        Key(Godot.Key.F2);
        await Frames(2);
        GD.Print($"[selftest] F2 army select {s.Input.Selection.Count}  {(s.Input.Selection.Count == s.Sim.Col[0].Count[1] + s.Sim.Col[0].Count[2] ? "PASS" : "FAIL")}");
    }

    static async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await _app.ToSignal(_app.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// Headless AI-vs-AI match:  --headless -- --simtest=<seconds>
    public static void SimTest(Main app, float seconds, Difficulty a, Difficulty b)
    {
        var root = new Node3D();
        app.AddChild(root);
        var world = WorldBuilder.Build(root, 20261031);
        var sim = new Sim(world);
        var ais = new[] { new AIController(sim, 0, a), new AIController(sim, 1, b) };
        const float dt = 1f / 30f;
        var sw = new System.Diagnostics.Stopwatch();
        double worst = 0, total = 0;
        int ticks = 0, peak = 0;
        float nextLog = 60;
        while (sim.Time < seconds && sim.Winner < 0)
        {
            sw.Restart();
            sim.Tick(dt);
            foreach (var ai in ais) ai.Tick(dt);
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds;
            total += ms; ticks++;
            if (ms > worst) worst = ms;
            int alive = 0;
            for (int i = 0; i < sim.High; i++) if (sim.Ants[i].Alive) alive++;
            peak = System.Math.Max(peak, alive);
            if (sim.Time >= nextLog)
            {
                nextLog += 60;
                var p = sim.Col[0]; var e = sim.Col[1];
                GD.Print($"[simtest] t={sim.Time / 60:0}m ants={alive} | black {p.Count[0]}w {p.Count[1]}s {p.Count[2]}m pop {p.PopUsed}/{p.PopCap} food {p.Food:0} gathered {p.Gathered:0} killed {p.Killed} | red {e.Count[0]}w {e.Count[1]}s {e.Count[2]}m pop {e.PopUsed}/{e.PopCap} food {e.Food:0} gathered {e.Gathered:0} killed {e.Killed} | tick avg {total / ticks:0.00}ms worst {worst:0.0}ms");
                worst = 0;
            }
        }
        GD.Print($"[simtest] finished t={sim.Time / 60:0.0}m winner={(sim.Winner < 0 ? "none" : sim.Winner == 0 ? "black" : "red")} peak ants={peak} avg tick {total / ticks:0.00}ms");
        app.GetTree().Quit();
    }

    static void Log(GameSession s, string tag)
    {
        var p = s.Sim.Col[0];
        var e = s.Sim.Col[1];
        GD.Print($"[capture] {tag}: t={s.Sim.Time:0} fps={Engine.GetFramesPerSecond()} ants={p.Count[0]}w/{p.Count[1]}s/{p.Count[2]}m vs {e.Count[0]}w/{e.Count[1]}s/{e.Count[2]}m food={p.Food:0}/{e.Food:0} gathered={p.Gathered:0}/{e.Gathered:0}");
    }

    static async Task Wait(float sec)
    {
        await _app.ToSignal(_app.GetTree().CreateTimer(sec, true, false, true), SceneTreeTimer.SignalName.Timeout);
    }

    static async Task Shot(string name)
    {
        await _app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = _app.GetViewport().GetTexture().GetImage();
        string path = $"{_dir}/{name}.png";
        img.SavePng(path);
        GD.Print($"[capture] {path} fps={Engine.GetFramesPerSecond()}");
    }
}
