using System.Collections.Generic;
using Godot;

namespace Mandible;

public sealed partial class NestInterior
{
    static readonly StandardMaterial3D RipeBerry = new() { AlbedoColor = new Color("791b22"), Roughness = 0.25f, ClearcoatEnabled = true };

    void FillIdentity(Room room, float density)
    {
        var ch = room.Ch; var node = room.Contents;
        var rng = new Rng((uint)(71 + ch.Id * 117 + room.Team * 9));
        int N(int n) => n <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(n * density));
        Basis Pose(float scale = 1) => Basis.FromEuler(new Vector3(rng.R(-0.15f, 0.15f), rng.R(0, Mathf.Tau), rng.R(-0.12f, 0.12f))) * Basis.FromScale(new Vector3(scale * rng.R(0.88f, 1.08f), scale, scale * rng.R(0.9f, 1.12f)));
        var placed = new List<Vector2>();
        Vector3 Bed(float x, float z, float rx, float rz, float gap, float height)
        {
            Vector2 p = default;
            for (int attempt = 0; attempt < 100; attempt++)
            {
                p = rng.InDisk(1) * new Vector2(rx, rz) + new Vector2(x, z);
                bool clear = true; foreach (var other in placed) if (p.DistanceSquaredTo(other) < gap * gap) { clear = false; break; }
                if (clear) break;
            }
            placed.Add(p);
            return new Vector3(p.X, height + rng.R(0, 0.065f), p.Y);
        }
        DressBanks(room, density);
        switch (ch.Kind)
        {
            case ChamberKind.Brood:
                // three beds whose slots are laid out here; how many hold brood follows the queue (BroodLife)
                Transform3D[] Slots(int n, float scale0, float scale1, float x, float z, float rx, float rz, float gap, float h)
                {
                    placed.Clear();
                    var slots = new Transform3D[n];
                    for (int i = 0; i < n; i++) slots[i] = new Transform3D(Pose(rng.R(scale0, scale1)), Bed(x, z, rx, rz, gap, h));
                    return slots;
                }
                room.Beds = new[]
                {
                    MakeBed(node, Surface.Egg, new Vector2(1.0f, -1.25f), new Vector2(1.65f, 0.95f), Slots(N(64), 1.25f, 1.65f, 1.0f, -1.25f, 1.65f, 0.95f, 0.32f, 0.23f)),
                    MakeBed(node, Surface.Larva, new Vector2(-1.7f, 0), new Vector2(0.85f, 1.65f), Slots(N(25), 1.45f, 1.8f, -1.7f, 0, 0.85f, 1.65f, 0.45f, 0.24f)),
                    MakeBed(node, Surface.Cocoon, new Vector2(1.2f, 1.3f), new Vector2(1.35f, 0.85f), Slots(N(18), 1.45f, 1.8f, 1.2f, 1.3f, 1.35f, 0.85f, 0.53f, 0.28f)),
                };
                break;
            case ChamberKind.Royal:
                // the apron behind her gaster stays bare: that is where each new egg is laid (BroodLife)
                var (tip, fwd) = GasterTip(room.Team);
                var apron = tip - fwd * 0.6f - _g.Sim.ChamberPos(room.Team, ch);
                // the nurses keep the eggs in a few heaps beside her, not a ring: piled high in the middle of each,
                // spilling at the edges, with bare floor between the heaps
                placed.Clear();
                var heaps = new (Vector2 c, float r)[3];
                float a0 = rng.R(0, Mathf.Tau);
                for (int h = 0; h < heaps.Length; h++)
                {
                    float a = a0 + h * Mathf.Tau / 3 + rng.R(-0.5f, 0.5f);
                    heaps[h] = (new Vector2(Mathf.Cos(a) * 2.35f, Mathf.Sin(a) * 1.75f), rng.R(0.6f, 1.05f));
                }
                OrganicScatter(node, Surface.Egg, N(135), i =>
                {
                    var (c, r) = heaps[i % heaps.Length];
                    Vector2 q;
                    int guard = 0;
                    bool clear;
                    do
                    {
                        q = c + new Vector2(rng.R(-1, 1) + rng.R(-1, 1), rng.R(-1, 1) + rng.R(-1, 1)) * 0.6f * r;
                        // each egg keeps a little space: a heap of separate eggs, not one lump
                        clear = true; foreach (var o in placed) if (o.DistanceSquaredTo(q) < 0.15f * 0.15f) { clear = false; break; }
                    }
                    while (guard++ < 40 && (!clear || new Vector2(q.X / 1.15f, q.Y / 1.3f).LengthSquared() < 1 || q.DistanceSquaredTo(apron) < 0.95f * 0.95f || new Vector2(q.X / 3.6f, q.Y / 2.6f).LengthSquared() > 1));
                    placed.Add(q);
                    float heap = Mathf.Max(0, 1 - q.DistanceTo(c) / (r * 1.3f)) * 0.18f; // a low heap, higher in the middle
                    return new Transform3D(Pose(rng.R(0.85f, 1.35f)), new Vector3(q.X, 0.2f + heap + rng.R(0, 0.04f), q.Y));
                });
                OrganicScatter(node, Surface.Resin, N(21), _ =>
                {
                    float angle = rng.R(0, Mathf.Tau), radius = rng.R(0.7f, 0.97f);
                    return new Transform3D(Pose(rng.R(0.85f, 1.4f)), new Vector3(Mathf.Cos(angle) * 3.8f * radius, 0.22f, Mathf.Sin(angle) * 2.65f * radius));
                });
                break;
            case ChamberKind.Storage:
                float fill = Mathf.Clamp(_g.Sim.Col[room.Team].Food / _g.Sim.Col[room.Team].FoodCap, 0, 1);
                OrganicScatter(node, Surface.Seed, N((int)(62 * fill)), _ => new Transform3D(Pose(rng.R(1.2f, 1.6f)), Bed(1, -0.8f, 1.1f, 0.9f, 0.28f, 0.18f)));
                placed.Clear();
                OrganicScatter(node, Surface.Seed, N((int)(40 * fill)), _ => new Transform3D(Pose(rng.R(0.95f, 1.3f)), Bed(-1.1f, 0.7f, 1.05f, 0.75f, 0.27f, 0.16f)));
                Scatter(node, OrganicMesh(Surface.Resin), RipeBerry, N((int)(19 * fill)), _ => new Transform3D(Pose(rng.R(1.4f, 2)), Bed(-1.15f, -1.1f, 0.8f, 0.7f, 0.4f, 0.34f)));
                OrganicScatter(node, Surface.Leaf, N((int)(7 * fill)), _ => new Transform3D(Pose(rng.R(1.1f, 1.5f)), Bed(1.3f, 0.85f, 0.7f, 0.6f, 0, 0.15f)));
                break;
            case ChamberKind.Forge:
                OrganicScatter(node, Surface.Jaw, 2, i => new Transform3D(new Basis(Vector3.Up, i == 0 ? -0.12f : Mathf.Pi + 0.15f) * Basis.FromScale(Vector3.One * 1.35f), new Vector3(i == 0 ? -1.0f : 1.1f, 0.29f, i == 0 ? -0.4f : 0.4f)));
                OrganicScatter(node, Surface.Resin, N(15), _ => new Transform3D(Pose(rng.R(0.9f, 1.5f)), Bed(0.2f, 0.5f, 0.7f, 1.5f, 0.3f, 0.24f)));
                OrganicScatter(node, Surface.Stone, 5, _ => new Transform3D(Pose(rng.R(2.5f, 4.5f)), Bed(0, -0.4f, 2.1f, 1.6f, 0.7f, 0.2f)));
                break;
            case ChamberKind.Armory:
                OrganicScatter(node, Surface.Shell, 12, i =>
                {
                    bool left = i < 5; int row = i % 5;
                    float x = i >= 10 ? (i - 10.5f) * 1.3f : left ? -1.35f : 1.35f;
                    float z = i >= 10 ? 1.9f : (row - 2) * 0.55f;
                    var basis = Basis.FromEuler(new Vector3(0.04f + row * 0.025f, i >= 10 ? i * 0.3f : left ? -0.4f : 0.35f, left ? -0.15f : 0.12f)) * Basis.FromScale(Vector3.One * (i >= 10 ? 0.82f : 1.16f + row * 0.025f));
                    return new Transform3D(basis, new Vector3(x, 0.08f + row * 0.07f, z));
                });
                break;
            case ChamberKind.Wings:
                OrganicScatter(node, Surface.Root, N(14), _ => new Transform3D(Pose(rng.R(0.85f, 1.7f)), Bed(0, 0, 2.4f, 1.8f, 0, 0.12f)));
                placed.Clear();
                OrganicScatter(node, Surface.Cocoon, N(20), _ => new Transform3D(Pose(rng.R(1.2f, 1.5f)), Bed(0, -0.8f, 2.3f, 1.2f, 0.4f, 0.27f)));
                var flyerMesh = AntMesh.Build(AntType.Flyer);
                var flyerMat = AntRenderer.AntMat(Shaders.Get(Shaders.Ant), room.Team, AntType.Flyer, false);
                var batch = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = flyerMesh, InstanceCount = 4 };
                Transform3D FlyerAt(int i) => new(new Basis(Vector3.Up, i * 1.6f) * Basis.FromScale(Vector3.One * 2.1f), new Vector3((i % 2 - 0.5f) * 2.2f, 0.18f, (i / 2 - 0.5f) * 1.8f));
                for (int i = 0; i < 4; i++) { batch.SetInstanceTransform(i, FlyerAt(i)); batch.SetInstanceCustomData(i, new Color(i, AntRenderer.NeutralLook, 0, 0)); }
                node.AddChild(new MultiMeshInstance3D { Multimesh = batch, MaterialOverride = flyerMat });
                Scatter(node, AntMesh.Wings(AntType.Flyer), new ShaderMaterial { Shader = Shaders.Get(Shaders.Wing) }, 4, FlyerAt);
                break;
            case ChamberKind.Silk:
                OrganicScatter(node, Surface.Root, 6, i => new Transform3D(new Basis(Vector3.Back, Mathf.Pi / 2) * Basis.FromScale(Vector3.One * 1.3f), new Vector3((i % 2 == 0 ? -1 : 1) * 1.9f, 1.0f, (i / 2 - 1) * 1.35f)));
                var web = new MeshKit();
                var posts = new Vector3[6];
                for (int i = 0; i < posts.Length; i++) posts[i] = new Vector3((i % 2 == 0 ? -1 : 1) * 1.9f, 1.75f + rng.R(-0.2f, 0.2f), (i / 2 - 1) * 1.35f);
                for (int i = 0; i < N(28); i++)
                {
                    var a = posts[i % 6] + Vector3.Down * rng.R(0, 1.05f);
                    var b = posts[(i * 5 + 3) % 6] + Vector3.Down * rng.R(0, 1.05f);
                    if (a.DistanceSquaredTo(b) < 1) b = posts[(i + 1) % 6];
                    web.Loft(new[] { a, (a + b) / 2 + Vector3.Down * 0.13f, b }, new[] { 0.009f, 0.007f, 0.009f }, new float[3], 0, 0, 5, Vector2.Zero, 5);
                }
                node.AddChild(new MeshInstance3D { Mesh = web.Commit(), MaterialOverride = MThread, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                OrganicScatter(node, Surface.Silk, 3, i => new Transform3D(Basis.FromEuler(new Vector3(0.2f, i * 1.8f, 0.45f)) * Basis.FromScale(Vector3.One * (i == 0 ? 1.15f : 0.9f)), new Vector3(i == 0 ? -1.7f : 1.15f + i * 0.15f, 0.5f, i == 0 ? 0.75f : i - 2)));
                break;
        }
    }
}
