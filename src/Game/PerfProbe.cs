using System.Threading.Tasks;
using Godot;

namespace Mandible;

/// `--perf-probe`: the Low tier's frame rate on the tour's tier view, measured again with each art layer switched
/// off in turn (the fog pass, the nest lamps, the photoscan scatter, the grass, the leaves, the food piles), so a
/// regression on phones points at its cause. Prints `[perf-probe] <what>: <fps>`.
public static class PerfProbe
{
    public static async Task Run(Main app)
    {
        Settings.Gfx = Settings.Quality.Low;
        app.StartBattle(Difficulty.Normal);
        await app.ToSignal(app.GetTree().CreateTimer(3), SceneTreeTimer.SignalName.Timeout);
        var s = app.Session;
        var nest = s.World.Nests[Config.Player].Pos;
        var toC = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - nest).Normalized();
        s.Camera.JumpTo(nest + toC * 8, 34);
        s.Camera.SetYaw(-toC.Angle() + Mathf.Pi * 0.5f, true);
        async Task<float> Measure()
        {
            await app.ToSignal(app.GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);
            float sum = 0; int n = 0;
            ulong end = Time.GetTicksMsec() + 2000;
            while (Time.GetTicksMsec() < end) { await app.ToSignal(app.GetTree(), SceneTree.SignalName.ProcessFrame); sum += (float)Engine.GetFramesPerSecond(); n++; }
            return sum / n;
        }
        void Toggle(System.Func<Node, bool> which, bool on)
        {
            void Walk(Node n) { if (n is Node3D v && which(n)) v.Visible = on; foreach (var c in n.GetChildren()) Walk(c); }
            Walk(s);
        }
        GD.Print($"[perf-probe] all on: {await Measure():F1}");
        foreach (var (name, pick) in new (string, System.Func<Node, bool>)[]
        {
            ("fog overlay", n => n.Name == "FogOverlay"),
            ("nature scatter", n => n.Name.ToString().StartsWith("Nature_")),
            ("leaf litter", n => n.Name == "LeafLitter"),
            ("sticks", n => n.Name == "Stick"),
            ("food piles", n => n.Name.ToString().StartsWith("Food")),
            ("lamps", n => n is OmniLight3D),
            ("big leaves", n => n is MeshInstance3D mi && mi.MaterialOverride is ShaderMaterial sm && sm.Shader == Shaders.Get(Shaders.Leaf)),
        })
        {
            Toggle(pick, false);
            GD.Print($"[perf-probe] without {name}: {await Measure():F1}");
            Toggle(pick, true);
        }
        app.GetTree().Quit();
    }
}
