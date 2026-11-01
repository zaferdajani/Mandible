using Godot;

namespace Mandible;

public sealed partial class NestInterior
{
    static ArrayMesh _jaw, _shellPlate, _leafBlade, _perch;
    static readonly StandardMaterial3D DarkChitin = new() { AlbedoColor = new Color("171a1e"), Metallic = 0.32f, Roughness = 0.27f, ClearcoatEnabled = true };
    static readonly StandardMaterial3D BlueShell = new() { AlbedoColor = new Color("243742"), Metallic = 0.48f, Roughness = 0.3f, ClearcoatEnabled = true };
    static readonly StandardMaterial3D LeafGreen = new() { AlbedoColor = new Color("586a24"), Roughness = 0.8f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
    static readonly StandardMaterial3D GoldenSeed = new() { AlbedoColor = new Color("d7a34f"), Roughness = 0.43f };
    static readonly StandardMaterial3D BerryRed = new() { AlbedoColor = new Color("932b30"), Roughness = 0.22f, ClearcoatEnabled = true };
    static readonly StandardMaterial3D RootBrown = new() { AlbedoColor = new Color("9b7644"), Roughness = 0.85f };
    static ArrayMesh Jaw() => _jaw ??= Build(k =>
    {
        Vector3 P(float t) => new(Mathf.Sin(t * 2.3f) * 0.8f, 0.12f + Mathf.Sin(t * Mathf.Pi) * 0.12f, -0.9f + t * 1.8f);
        for (int i = 0; i < 16; i++)
        { float t = i / 16f, n = (i + 1) / 16f; k.Tube(P(t), P(n), 0.25f * (1 - t) + 0.015f, 0.25f * (1 - n) + 0.015f, 10, Colors.White, Colors.White, Vector2.Zero, true); }
        for (int i = 1; i <= 5; i++)
        { var p = P(i / 7f); k.Tube(p, p + new Vector3(-0.25f, 0, 0.06f), 0.08f, 0.01f, 7, Colors.White, Colors.White, Vector2.Zero, true); }
    });
    static ArrayMesh ShellPlate() => _shellPlate ??= Build(k => k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(0.65f, 0.28f, 0.82f)), 24, 14, Colors.White, Vector2.Zero, u => 1 + 0.05f * Mathf.Cos(u.Z * 22)));
    static ArrayMesh LeafBlade() => _leafBlade ??= Build(k => k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(0.19f, 0.035f, 0.65f)), 16, 8, Colors.White, Vector2.Zero));
    static ArrayMesh Perch() => _perch ??= Build(k => k.Tube(new Vector3(-0.8f, 0, -0.2f), new Vector3(0.8f, 0.18f, 0.2f), 0.07f, 0.025f, 8, Colors.White, Colors.White, Vector2.Zero, true));

    void FillIdentity(Room room, float density)
    {
        var ch = room.Ch; var node = room.Contents;
        var (w, _, d) = RoomSize(ch);
        var rng = new Rng((uint)(71 + ch.Id * 117 + room.Team * 9));
        int N(int n) => Mathf.Max(1, Mathf.RoundToInt(n * density));
        Basis Yaw(float scale = 1) => new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * Basis.FromScale(Vector3.One * scale);
        Vector3 Cluster(float x, float z, float spread, float lift = 0.17f)
        { var v = rng.InDisk(spread); return new Vector3(x + v.X, lift + rng.R(0, 0.14f), z + v.Y); }
        Vector3 Bed(int i, int count, float x, float z, float rx, float rz, float lift)
        {
            float angle = i * 2.39996f, radius = Mathf.Sqrt((i + 0.5f) / count);
            return new Vector3(x + Mathf.Cos(angle) * radius * rx, lift + rng.R(0, 0.055f), z + Mathf.Sin(angle) * radius * rz);
        }
        switch (ch.Kind)
        {
            case ChamberKind.Brood:
                Scatter(node, Egg(), MEgg, N(70), i => new Transform3D(Yaw(1.45f), Bed(i, N(70), 0.9f, -1.2f, 1.3f, 0.85f, 0.22f)));
                Scatter(node, Larva(), MLarva, N(34), i => new Transform3D(Yaw(1.35f), Bed(i, N(34), -1.7f, 0.15f, 0.8f, 1.3f, 0.17f)));
                Scatter(node, Cocoon(), MCocoon, N(20), i => new Transform3D(Yaw(1.5f), Bed(i, N(20), 1.15f, 1.2f, 1.25f, 0.75f, 0.22f)));
                break;
            case ChamberKind.Royal:
                Scatter(node, Egg(), MEgg, N(120), i =>
                { float t = i * 2.39996f, r = 1.5f + 1.7f * Mathf.Sqrt((i + 0.5f) / N(120)); return new Transform3D(Yaw(1.4f), new Vector3(Mathf.Cos(t) * r, 0.22f, Mathf.Sin(t) * r * 0.7f)); });
                Scatter(node, Drop(), MJelly, N(24), i =>
                { float t = i * 2.39996f; return new Transform3D(Yaw(rng.R(1.1f, 1.65f)), new Vector3(Mathf.Cos(t) * 3.5f, 0.25f, Mathf.Sin(t) * 2.5f)); });
                break;
            case ChamberKind.Storage:
                float fill = Mathf.Clamp(_g.Sim.Col[room.Team].Food / _g.Sim.Col[room.Team].FoodCap, 0.08f, 1);
                Scatter(node, Seed(), GoldenSeed, N((int)(55 * fill)), _ => new Transform3D(Yaw(1.45f), Cluster(0.8f, -0.55f, 0.85f)));
                Scatter(node, Drop(), BerryRed, N((int)(18 * fill)), _ => new Transform3D(Yaw(1.7f), Cluster(-0.7f, -0.65f, 0.6f)));
                Scatter(node, Seed(), GoldenSeed, N((int)(25 * fill)), _ => new Transform3D(Yaw(1.2f), Cluster(-0.8f, 0.65f, 0.6f)));
                Scatter(node, LeafBlade(), LeafGreen, N(9), _ => new Transform3D(Yaw(1.3f), Cluster(0.9f, 0.6f, 0.5f, 0.08f)));
                break;
            case ChamberKind.Forge:
                Scatter(node, Jaw(), DarkChitin, 2, i => new Transform3D(new Basis(Vector3.Up, i == 0 ? -0.8f : 2.4f) * Basis.FromScale(Vector3.One * 1.65f), new Vector3(i == 0 ? -1.3f : 1.2f, 0.1f, i == 0 ? -0.4f : 0.4f)));
                Scatter(node, Drop(), MJelly, N(16), _ => new Transform3D(Yaw(1.5f), Cluster(0, 0.8f, 0.75f)));
                Scatter(node, Grit(), MQuartz, N(12), _ => new Transform3D(Yaw(2), Cluster(0, -0.5f, 1.3f)));
                break;
            case ChamberKind.Armory:
                Scatter(node, ShellPlate(), BlueShell, 7, i => new Transform3D(new Basis(Vector3.Up, i < 4 ? -0.45f : 0.8f), new Vector3(i < 4 ? -0.8f : 0.7f, 0.18f + (i % 4) * 0.045f, (i % 4 - 1.5f) * 0.6f)));
                Scatter(node, Perch(), GoldenSeed, 9, i => new Transform3D(new Basis(Vector3.Up, 0.3f), new Vector3(0.7f, 0.47f, -0.9f + i * 0.22f)));
                break;
            case ChamberKind.Wings:
                Scatter(node, Perch(), RootBrown, N(14), _ => new Transform3D(Yaw(1.2f), Cluster(0, 0, 1.5f, 0.1f)));
                Scatter(node, Cocoon(), MCocoon, N(24), _ => new Transform3D(Yaw(1.4f), Cluster(rng.R(-1.2f, 1.2f), -0.8f, 0.4f)));
                Transform3D FlyerAt(int i) => new(new Basis(Vector3.Up, i * 1.6f) * Basis.FromScale(Vector3.One * 1.7f), new Vector3((i % 2 - 0.5f) * 1.6f, 0.2f, (i / 2 - 0.5f) * 1.5f));
                Scatter(node, AntMesh.Build(AntType.Flyer), DarkChitin, 4, FlyerAt);
                Scatter(node, AntMesh.Wings(AntType.Flyer), new ShaderMaterial { Shader = Shaders.Get(Shaders.Wing) }, 4, FlyerAt);
                break;
            case ChamberKind.Silk:
                Scatter(node, Perch(), RootBrown, 6, i => new Transform3D(new Basis(Vector3.Forward, Mathf.Pi / 2), new Vector3((i % 2 == 0 ? -1 : 1) * 1.4f, 0.7f, (i / 2 - 1) * 1.1f)));
                Scatter(node, Thread(), MThread, N(32), _ =>
                {
                    var a = new Vector3(-1.4f, rng.R(0.3f, 1.3f), rng.R(-1.2f, 1.2f)); var b = new Vector3(1.4f, rng.R(0.3f, 1.3f), rng.R(-1.2f, 1.2f)); var axis = b - a;
                    return new Transform3D(Basis.LookingAt(axis.Normalized(), Vector3.Up) * Basis.FromScale(new Vector3(1, 1, axis.Length())), (a + b) / 2);
                });
                Scatter(node, Cocoon(), MCocoon, N(12), i => new Transform3D(Yaw(2.2f), Cluster(i % 2 == 0 ? -1 : 1, 0.85f, 0.35f)));
                break;
        }
    }
}
