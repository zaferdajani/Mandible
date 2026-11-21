using System.Threading.Tasks;
using Godot;

namespace Mandible;

/// `--death-capture=<dir>`: a beetle (and a soldier beside it) killed in front of the camera, photographed every tenth
/// of a second from just before the blow until the body has settled. One contact sheet, death.png, row by row in time,
/// to see that a body falls onto the ground and stays on it (owner, 2026-11-21: "the beetle flicks under the ground").
public static class DeathCapture
{
    public static async Task Run(Main app, string directory)
    {
        DirAccess.MakeDirRecursiveAbsolute(directory);
        app.StartSkirmish();
        var game = app.Session;
        game.Hud.Visible = false;
        game.Camera.UserControl = false;
        var sim = game.Sim;
        async Task Frame() => await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        for (int i = 0; i < 20; i++) await Frame();
        // beside the colony's own entrance, in plain sight: a ground beetle (the Level 1 kind) and a red soldier
        var at = sim.NestEntrance(0) + new Vector2(7, 5);
        for (int r = 0; r < 30 && sim.W.Blocked(at, 2.5f); r++) at += new Vector2(1.5f, 1);
        int beetle = sim.Spawn(Config.Wild, AntType.Beetle, at, 0.6f);
        int soldier = sim.Spawn(1, AntType.Soldier, at + new Vector2(3f, 1.4f), 2.2f);
        sim.Ants[beetle].State = AntState.Hold; sim.Ants[soldier].State = AntState.Hold;
        game.Camera.JumpTo(at + new Vector2(-0.6f, 0.3f), 9f);
        for (int i = 0; i < 40; i++) await Frame();
        var frames = new System.Collections.Generic.List<Image>();
        ulong next = Time.GetTicksMsec();
        for (int k = 0; k < 16; k++)
        {
            if (k == 2) { sim.Slay(beetle); sim.Slay(soldier); }
            while (Time.GetTicksMsec() < next) await Frame();
            next += k < 6 ? 60UL : 500UL;
            await Frame();
            var im = app.GetViewport().GetTexture().GetImage();
            im.Resize(im.GetWidth() / 2, im.GetHeight() / 2);
            frames.Add(im);
        }
        int w = frames[0].GetWidth(), h = frames[0].GetHeight();
        var sheet = Image.CreateEmpty(w * 4, h * 4, false, frames[0].GetFormat());
        for (int k = 0; k < frames.Count; k++) sheet.BlitRect(frames[k], new Rect2I(0, 0, w, h), new Vector2I(k % 4 * w, k / 4 * h));
        sheet.SavePng(directory.PathJoin("death.png"));
        GD.Print($"[death-capture] saved {frames.Count} frames");
        app.GetTree().Quit();
    }

    /// --dig-capture=<dir>: Level 1's tube dug open by six soldiers, a frame every 1.2 s (the plug and the sand carried
    /// out in their jaws, the diggers kept inside the gold ring). One contact sheet, dig.png.
    public static async Task Dig(Main app, string directory)
    {
        DirAccess.MakeDirRecursiveAbsolute(directory);
        app.StartEpisode(1);
        var game = app.Session;
        var sim = game.Sim;
        async Task Frame() => await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        for (int i = 0; i < 20; i++) await Frame();
        if (game.Hud.StoryOpen) game.Hud.Story.QueueFree();
        game.Hud.CloseBriefing();
        game.Hud.Visible = false;
        game.Camera.UserControl = false;
        foreach (int g in sim.Mission.Guards) sim.Slay(g);
        var crew = new System.Collections.Generic.List<int>();
        for (int k = 0; k < 6; k++) { int id = sim.Spawn(0, AntType.Soldier, Expedition.DigSite + new Vector2(4 + k % 3, (k / 3 - 0.5f) * 2.5f), 3.1f); crew.Add(id); }
        sim.Mission.Stage = ExpeditionStage.FreeQueen;
        sim.CmdMove(crew, Expedition.DigSite, false);
        game.SetPaused(false);
        game.Camera.JumpTo(Expedition.DigSite + new Vector2(0.6f, 0), 11f);
        var frames = new System.Collections.Generic.List<Image>();
        ulong next = Time.GetTicksMsec() + 1500;
        for (int k = 0; k < 16; k++)
        {
            while (Time.GetTicksMsec() < next) await Frame();
            next += 1200;
            await Frame();
            var im = app.GetViewport().GetTexture().GetImage();
            im.Resize(im.GetWidth() / 2, im.GetHeight() / 2);
            frames.Add(im);
            GD.Print($"[dig-capture] frame {k} dug {sim.Mission.Dug:0}% diggers {sim.Mission.Diggers} stage {sim.Mission.Stage}");
        }
        int w = frames[0].GetWidth(), h = frames[0].GetHeight();
        var sheet = Image.CreateEmpty(w * 4, h * 4, false, frames[0].GetFormat());
        for (int k = 0; k < frames.Count; k++) sheet.BlitRect(frames[k], new Rect2I(0, 0, w, h), new Vector2I(k % 4 * w, k / 4 * h));
        sheet.SavePng(directory.PathJoin("dig.png"));
        app.GetTree().Quit();
    }
}
