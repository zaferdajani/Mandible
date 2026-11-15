using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// `--gait-capture=<abs dir>`: how every creature moves, in real frames and in numbers. Each one in turn is turned
/// about on the spot and then led round a curve. A walker gives a sheet of eight frames in sequence from a fixed
/// camera (`<creature>-turn.png`; a standing foot must stay on the same spot of ground from frame to frame), and the
/// self-test follows every standing foot over the ground: every leg must step, and a standing foot may drift only a
/// small part of a body length (more is allowed in the about-turn, where it swings round and breaks into a run at once). A flyer must lean into its turn, and nothing may be seen to turn faster than its
/// legs or wings can carry it. `MANDIBLE_GAIT_ONLY=Spider,Worker` limits the run.
public static class GaitCapture
{
    public static async System.Threading.Tasks.Task Run(Main app, string dir)
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(dir);
            async System.Threading.Tasks.Task Wait(float s) { await app.ToSignal(app.GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout); }
            async System.Threading.Tasks.Task Frame() { await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
            CampaignProgress.UsePath("user://campaign_gaitcap.cfg"); CampaignProgress.Reset();
            app.StartEpisode(Reveal.SilkTrapId);
            var gs = app.Session; gs.Hud.CloseBriefing();
            var sim = gs.Sim;
            await Wait(4f);
            gs.Ants.VisibleToPlayer = _ => true;
            gs.Fog.RevealAll = true;
            var home = sim.NestEntrance(0);
            var along = sim.Approach(0);
            var side = new Vector2(-along.Y, along.X);
            var stage = home + side * 24 + along * 8; // open ground beside the mound
            gs.Camera.PitchOverride = 68;
            string only = System.Environment.GetEnvironmentVariable("MANDIBLE_GAIT_ONLY");
            bool all = true;

            foreach (AntType type in Enum.GetValues<AntType>())
            {
                if ((int)type >= Config.TypeCount || (only != null && Array.IndexOf(only.Split(','), type.ToString()) < 0)) continue;
                // wild bugs come as borrowed minds (Queen's Pheromone), so they take orders and still wear their own bodies
                int id = sim.Spawn(0, type, stage, along.Angle());
                if (id < 0) continue;
                if (Config.IsBug(type)) { sim.Ants[id].CharmP1 = (byte)(Config.Wild + 1); sim.Ants[id].CharmT = 999; }
                float size = sim.Ants[id].Size;
                bool flies = Config.Flies(type);
                var legs = flies ? null : AntMesh.LegsOf(type, Config.IsBug(type) ? Config.Wild : 0);
                int n = legs?.Rest.Length ?? 0;
                var held = new Vector2[8]; var was = new bool[8]; var cyc = new float[8];
                var lifted = new HashSet<int>();
                float drift = 0, bank = 0, fastest = 0;
                bool planted = true;
                void Follow(float dt)
                {
                    fastest = Mathf.Max(fastest, gs.Ants.TurnSpeed(id));
                    bank = Mathf.Max(bank, Mathf.Abs(gs.Ants.BankOf(id)));
                    if (n > 0 && !gs.Ants.OnPlantedFeet(id)) planted = false;
                    for (int leg = 0; leg < n * 2; leg++)
                    {
                        var (at, down, cycle) = gs.Ants.Foot(id, legs, leg);
                        if (down && was[leg] && cycle >= cyc[leg]) drift = Mathf.Max(drift, at.DistanceTo(held[leg]));
                        else { held[leg] = at; if (was[leg] && !down) lifted.Add(leg); }
                        was[leg] = down; cyc[leg] = cycle;
                    }
                }
                bool founded = sim.Col[0].Founded; sim.Col[0].Founded = false;
                if (type != AntType.Queen) sim.CmdMove(new List<int> { id }, stage, false); // a worker would be off foraging
                await Wait(0.7f);
                gs.Camera.JumpTo(sim.Ants[id].Pos, 4.5f + size * 3.4f);
                await Wait(0.5f);
                gs.Camera.JumpTo(sim.Ants[id].Pos, 4.5f + size * 3.4f);
                await Frame();

                // the queen takes no move orders: once her colony is founded she walks herself to the nest entrance
                // (Sim.QueenWalk, the same Sim.Move as everything else). She is set down facing away from it
                void Order(Vector2 p) { if (type != AntType.Queen) sim.CmdMove(new List<int> { id }, p, false); }
                if (type == AntType.Queen) { sim.Ants[id].Heading = (stage - sim.W.Nests[0].Pos).Angle(); sim.Ants[id].PrevHeading = sim.Ants[id].Heading; sim.Col[0].Founded = true; }
                // 1. turned about on the spot: ordered to a point just behind it. Eight frames, every second one drawn
                Order(sim.Ants[id].Pos - MathX.Dir(sim.Ants[id].Heading) * (1.6f + size * 0.4f));
                var frames = new List<Image>();
                float clock = sim.Time, since = 0;
                for (int f = 0; f < 40; f++)
                {
                    await Frame();
                    since += sim.Time - clock; clock = sim.Time;
                    Follow(since); if (since > 0.01f) since = 0;
                    if (f >= 3 && (f - 3) % 2 == 0 && frames.Count < 8 && !flies) frames.Add(app.GetViewport().GetTexture().GetImage());
                }
                int turnSteps = lifted.Count;
                float turnDrift = drift;
                if (frames.Count > 0)
                {
                    int w = frames[0].GetWidth(), h = frames[0].GetHeight(), cw = Mathf.Min(w, 560), ch = Mathf.Min(h, 420);
                    using var sheet = Image.CreateEmpty(cw * 4, ch * 2, false, frames[0].GetFormat());
                    for (int k = 0; k < frames.Count; k++)
                    {
                        sheet.BlitRect(frames[k], new Rect2I((w - cw) / 2, (h - ch) / 2, cw, ch), new Vector2I(k % 4 * cw, k / 4 * ch));
                        frames[k].Dispose();
                    }
                    sheet.SavePng(dir.PathJoin(type.ToString().ToLowerInvariant() + "-turn.png"));
                }

                // 2. walking a curve: led round a circle
                lifted.Clear(); drift = 0; Array.Clear(was);
                var centre = sim.Ants[id].Pos + side * 6f;
                for (int f = 0; f < 95; f++)
                {
                    float ang = (sim.Ants[id].Pos - centre).Angle() + 0.7f;
                    Order(centre + MathX.Dir(ang) * 6f);
                    gs.Camera.JumpTo(sim.Ants[id].Pos); // the drawn size follows the zoom: hold the zoom still
                    await Frame();
                    since += sim.Time - clock; clock = sim.Time;
                    if (f >= 40) Follow(since);
                    if (since > 0.01f) since = 0;
                }
                bool ok = flies
                    ? bank > 0.04f && fastest < 8.5f
                    : n > 0 && planted && turnSteps == n * 2 && lifted.Count == n * 2 && turnDrift < 0.3f * size && drift < 0.12f * size && fastest < 10f;
                sim.Col[0].Founded = founded;
                all &= ok;
                GD.Print(flies
                    ? $"[selftest] gait {type}: flyer, leans {bank:0.00} rad into the turn, seen turning at most {fastest:0.0} rad/s  {(ok ? "PASS" : "FAIL")}"
                    : $"[selftest] gait {type}: {n * 2} legs{(planted ? "" : " NOT on planted feet")}, turn {turnSteps} stepped drift {turnDrift / size:0.000}, walk {lifted.Count} stepped drift {drift / size:0.000} body lengths, seen turning at most {fastest:0.0} rad/s  {(ok ? "PASS" : "FAIL")}");
                sim.Ants[id].Pos = home - along * 30 + side * (int)type * 3; sim.Ants[id].PrevPos = sim.Ants[id].Pos; // off the stage
                sim.CmdMove(new List<int> { id }, sim.Ants[id].Pos, false);
            }
            GD.Print("[gait-capture] " + (all ? "PASS" : "FAIL"));
            app.GetTree().Quit(all ? 0 : 1);
        }
        catch (Exception e) { GD.PushError("[gait-capture] FAIL " + e); app.GetTree().Quit(1); }
    }
}
