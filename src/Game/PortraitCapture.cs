using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Mandible;

/// `--portrait-capture=<abs dir>`: the selection panel's living portraits for review.
/// `sheet.png`: every creature three times (idle, answering a call, badly hurt), straight from the portrait viewport.
/// `hud-<who>.png`: the real panel in a skirmish with the queen (nothing selected), a worker, a soldier, a general
/// and a wild spider in it. Self-tests: every portrait draws its creature, moves between frames, and the panel
/// shows the highest rank of a mixed selection.
public static class PortraitCapture
{
    public static async Task Run(Main app, string dir)
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(dir);
            async Task Frames(int n) { for (int i = 0; i < n; i++) await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
            async Task Wait(float s) { await app.ToSignal(app.GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout); await Frames(1); }
            bool ok = true;
            void Check(bool pass, string what) { GD.Print($"[portrait-capture] {what}  {(pass ? "PASS" : "FAIL")}"); ok &= pass; }

            // ---- the sheet: the portrait on its own, driven by hand
            var layer = new CanvasLayer { Layer = 90 };
            app.AddChild(layer);
            var p = new LivePortrait { Position = new Vector2(8, 8), Size = new Vector2(208, 256) };
            layer.AddChild(p);
            await Frames(2);
            (int team, AntType type)[] who =
            {
                (0, AntType.Queen), (0, AntType.Worker), (0, AntType.Soldier), (0, AntType.Major), (0, AntType.Flyer), (0, AntType.Gunner),
                (0, AntType.Warlord), (0, AntType.Matron), (0, AntType.Marshal), (0, AntType.WebAnt), (0, AntType.Armoured),
                (Config.Wild, AntType.Beetle), (Config.Wild, AntType.Spider), (Config.Wild, AntType.StagBeetle), (Config.Wild, AntType.Wasp),
                (1, AntType.Queen), (1, AntType.Soldier), (1, AntType.Warlord),
            };
            const int W = 208, H = 256, PerRow = 2;
            var sheet = Image.CreateEmpty(W * 3 * PerRow, H * ((who.Length + PerRow - 1) / PerRow), false, Image.Format.Rgb8);
            int still = 0, blank = 0;
            for (int i = 0; i < who.Length; i++)
            {
                var (team, type) = who[i];
                var s = new LivePortrait.Subject { Team = team, Type = type, Seed = 5, Health = 1, Voice = -1, Stage = Config.IsHero(type) ? 3 : 0 };
                async Task<Image> Take(int col, float seconds)
                {
                    for (float t = 0; t < seconds; t += 1f / 30) { p.Show(s); p.Advance(1f / 30); await Frames(1); }
                    await Frames(2);
                    var im = p.Snapshot(); im.Convert(Image.Format.Rgb8);
                    sheet.BlitRect(im, new Rect2I(0, 0, W, H), new Vector2I((i % PerRow * 3 + col) * W, i / PerRow * H));
                    return im;
                }
                { var mesh = AntMesh.Build(type, false, team, team == Config.Wild ? null : AntMesh.Detail.Near); var bx = mesh.GetAabb(); GD.Print($"[portrait-dims] {team} {type} box {bx.Position} .. {bx.End} neck {(team == Config.Wild ? AntMesh.Neck.GetValueOrDefault(type) : AntMesh.NeckOf(type, team))} waist {AntMesh.WaistOf(type, team)} tip {AntMesh.GasterTip(type, team)}"); }
                var a = await Take(0, 0.5f);
                // answering: the queen speaks, the others call
                if (type == AntType.Queen) s.Speaking = true; else p.React(type is AntType.Soldier or AntType.Major, 0.6f);
                var b = await Take(1, 0.3f);
                s.Speaking = false; s.Health = 0.18f; s.Lost = type == AntType.Worker ? 64 : 0; // the worker has lost an antenna
                await Take(2, 1.3f);
                double diff = 0, lum = 0;
                for (int y = 0; y < H; y += 4) for (int x = 0; x < W; x += 4) { var ca = a.GetPixel(x, y); var cb = b.GetPixel(x, y); diff += Mathf.Abs(ca.R - cb.R) + Mathf.Abs(ca.G - cb.G); lum += ca.R + ca.G + ca.B; }
                int n = (H / 4) * (W / 4);
                if (diff / n < 0.002) { still++; GD.Print($"[portrait-capture] {type} (team {team}) did not move: {diff / n:0.0000}"); }
                if (lum / n < 0.08) { blank++; GD.Print($"[portrait-capture] {type} (team {team}) is blank"); }
            }
            sheet.SavePng(dir.PathJoin("sheet.png"));
            Check(blank == 0, $"{who.Length} portraits drawn, {blank} blank");
            Check(still == 0, $"{who.Length - still}/{who.Length} portraits move when they answer");
            layer.QueueFree();

            // ---- the real panel
            app.StartSkirmish(); var g = app.Session;
            for (int i = 0; i < 3; i++) g.Sim.Tick(1f / 30);
            g.SetNestView(false); g.Input.SelectedRoom = -1;
            var home = g.World.Nests[0].Pos;
            g.Camera.JumpTo(home + new Vector2(0, 4), 40);
            await Wait(4f);
            async Task Hud(string name)
            {
                await Wait(1.2f);
                using var im = app.GetViewport().GetTexture().GetImage();
                im.SavePng(dir.PathJoin($"hud-{name}.png"));
                // and the panel alone, enlarged, for a close look
                var r = g.Hud.CommandHud.Live.GetGlobalRect();
                var sc = app.GetViewport().GetScreenTransform();
                var tl = sc * r.Position; var br = sc * r.End;
                float k = (float)im.GetWidth() / app.GetTree().Root.Size.X;
                var crop = im.GetRegion(new Rect2I((int)(tl.X * k) - 12, (int)(tl.Y * k) - 12, (int)((br.X - tl.X) * k * 2.8f), (int)((br.Y - tl.Y) * k) + 24));
                crop.Resize(crop.GetWidth() * 2, crop.GetHeight() * 2, Image.Interpolation.Lanczos);
                crop.SavePng(dir.PathJoin($"panel-{name}.png"));
                GD.Print($"[portrait-capture] {name}: live {g.Hud.CommandHud.Live.Visible}");
            }
            var live = g.Hud.CommandHud.Live;
            void Select(params int[] ids) { foreach (int id in g.Input.Selection) g.Sim.Ants[id].Selected = false; g.Input.Selection.Clear(); g.Input.Inspect = -1; foreach (int id in ids) { g.Input.Selection.Add(id); g.Sim.Ants[id].Selected = true; } }
            int Make(int team, AntType t, Vector2 off, int level = 1) => g.Sim.Spawn(team, t, home + off, 0, level);

            Select();
            await Hud("queen");
            Check(live.Visible && live.Shown == (0, AntType.Queen), "nothing selected: the queen");
            int worker = Make(0, AntType.Worker, new Vector2(3, 7));
            Select(worker);
            await Hud("worker");
            Check(live.Shown == (0, AntType.Worker), "a worker selected: the worker");
            int soldier = Make(0, AntType.Soldier, new Vector2(5, 7));
            Select(worker, soldier);
            await Hud("soldier");
            Check(live.Shown == (0, AntType.Soldier), "worker + soldier selected: the soldier (highest rank)");
            int warlord = Make(0, AntType.Warlord, new Vector2(0, 8), 6);
            Select(soldier, warlord, worker);
            await Hud("general");
            Check(live.Shown == (0, AntType.Warlord), "soldier + Warlord + worker selected: the Warlord");
            int spider = Make(Config.Wild, AntType.Spider, new Vector2(-6, 9));
            Select(); g.Input.Inspect = spider; g.Input.InspectSerial = g.Sim.Ants[spider].Serial;
            await Hud("spider");
            Check(live.Shown == (Config.Wild, AntType.Spider), "a wild spider inspected: the spider");
            // hurt: the same soldier at a fifth of her health
            g.Sim.Ants[soldier].Hp = g.Sim.Ants[soldier].MaxHp * 0.2f;
            Select(soldier);
            await Hud("soldier-hurt");
            // off: the still card is back
            Settings.LivePortrait = false;
            await Wait(0.3f);
            Check(!live.Visible, "switched off: the still card shows");
            Settings.LivePortrait = true;

            if (OS.GetCmdlineUserArgs().Contains("--no-perf")) { GD.Print($"[portrait-capture] {(ok ? "PASS" : "FAIL")}"); app.GetTree().Quit(); return; }
            // ---- what it costs: the same view, vsync off, the portrait switched on and off in turn
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled); Engine.MaxFps = 0;
            Select();
            foreach (var q in new[] { Settings.Quality.Low, Settings.Quality.High })
            {
                Settings.Gfx = q; g.ApplyQuality();
                await Wait(1.5f);
                double on = 0, off = 0;
                for (int round = 0; round < 6; round++)
                {
                    Settings.LivePortrait = round % 2 == 0;
                    await Wait(0.4f);
                    ulong t0 = Time.GetTicksUsec(); int f0 = Engine.GetFramesDrawn();
                    await Wait(2f);
                    double ms = (Time.GetTicksUsec() - t0) / 1000.0 / Math.Max(1, Engine.GetFramesDrawn() - f0);
                    if (round % 2 == 0) on += ms / 3; else off += ms / 3;
                }
                Settings.LivePortrait = true;
                GD.Print($"[perf] live portrait, tier {q}: on {on:0.00} ms ({1000 / on:0} fps), off {off:0.00} ms ({1000 / off:0} fps), cost {on - off:0.00} ms");
            }
            GD.Print($"[portrait-capture] {(ok ? "PASS" : "FAIL")}");
        }
        catch (Exception e) { GD.PrintErr("[portrait-capture] FAIL " + e); }
        app.GetTree().Quit();
    }
}
