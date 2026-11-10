using Godot;

namespace Mandible;

/// Level 1's teamwork lesson, built against approved targets K1-K3: a shallow rain pond east of the clearing, tea-brown
/// and clear over its mud, with a low island of damp soil and pebbles. On the island lies a dead queen of another lab
/// strain, on her side with her legs curled, and the second broken tube half sunk in the mud beside her. The sim's
/// walkable ring and island (Expedition.Constrain) follow the same circles; the straws and the bridge are StrawProps.
public static partial class WorldBuilder
{
    public const float PondDepth = 1.1f, WaterDrop = 0.32f; // the basin's floor, and the water's surface, below the yard

    /// The pond's floor and banks and the island's low mound, cut into the yard before the soil settles.
    static void PondBasin(Heightfield hf)
    {
        var c = Expedition.Pond;
        float baseY = 0;
        for (int k = 0; k < 16; k++) baseY += hf.Sample(c.X + Mathf.Cos(k * 0.39f) * (Expedition.ShoreR + 2), c.Y + Mathf.Sin(k * 0.39f) * (Expedition.ShoreR + 2));
        baseY /= 16;
        var wobble = new FastNoiseLite { Seed = 913, Frequency = 0.35f };
        float reach = Expedition.ShoreR + 4;
        for (int z = (int)(c.Y - reach); z <= (int)(c.Y + reach); z++)
            for (int x = (int)(c.X - reach); x <= (int)(c.X + reach); x++)
            {
                int i = z * hf.N + x;
                if (i < 0 || i >= hf.H.Length) continue;
                var d = new Vector2(x, z) - c;
                float r = d.Length() + wobble.GetNoise2D(x, z) * 0.6f;
                float floor = baseY - PondDepth + wobble.GetNoise2D(x * 2.1f, z * 2.1f) * 0.12f;
                float island = baseY + 0.18f * (1 - Mathf.SmoothStep(0, Expedition.IslandR, r)) + wobble.GetNoise2D(x * 3.1f, z * 3.1f) * 0.22f; // lumpy, not a plate
                // island mound → its muddy flank into the water → the floor → the bank back up to the yard
                float h = r < Expedition.IslandR - 0.8f ? island
                    : r < Expedition.IslandR + 1.4f ? Mathf.Lerp(island, floor, Mathf.SmoothStep(Expedition.IslandR - 0.8f, Expedition.IslandR + 1.4f, r))
                    : r < Expedition.WaterR - 1.6f ? floor
                    : Mathf.Lerp(floor, hf.H[i], Mathf.SmoothStep(Expedition.WaterR - 1.6f, Expedition.WaterR + 1.2f, r));
                float blend = 1 - Mathf.SmoothStep(Expedition.ShoreR, reach, d.Length());
                hf.H[i] = Mathf.Lerp(hf.H[i], h, blend);
            }
        PondLevel = baseY - WaterDrop;
    }
    public static float PondLevel;

    /// The bridge's deck: ants crossing walk on the straws, not the pond floor. The ground mesh keeps the basin;
    /// only the walk height is raised, and nobody can stand there before the bridge is built.
    static void PondDeck(Heightfield hf)
    {
        var c = Expedition.Pond;
        for (int z = (int)c.Y - 2; z <= (int)c.Y + 2; z++)
            for (int x = (int)(c.X - Expedition.WaterR - 1); x <= (int)(c.X - Expedition.IslandR + 1); x++)
            {
                int i = z * hf.N + x;
                hf.H[i] = Mathf.Max(hf.H[i], PondLevel + StrawProps.Thick);
            }
    }

    static void BuildPond(World w, Rng rng)
    {
        var hf = w.HF;
        var c = Expedition.Pond;
        // the water: one sheet at the pond's level, coloured by how deep the mud lies under it (Shaders.Pond)
        var water = new ShaderMaterial { Shader = Shaders.Get(Shaders.Pond) };
        water.SetShaderParameter("ripple_a", w.NormalA);
        water.SetShaderParameter("ripple_b", w.NormalB);
        water.SetShaderParameter("centre", c);
        water.SetShaderParameter("island_r", Expedition.IslandR - 0.4f);
        water.SetShaderParameter("shore_r", Expedition.WaterR + 1.4f);
        w.Root.AddChild(new MeshInstance3D
        {
            Name = "Pond", MaterialOverride = water, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new PlaneMesh { Size = Vector2.One * (Expedition.WaterR + 2) * 2, SubdivideWidth = 24, SubdivideDepth = 24 },
            Position = new Vector3(c.X, PondLevel, c.Y),
        });

        var dress = new NatureScatter.Dresser(w, rng);
        // the island: damp soil, pebbles of every size half sunk in it, a few clumps at the waterline
        // the island and the waterline are heaped with damp clods (a generated scan, kind "clod"), half sunk, clustered
        var clods = NatureAssets.Kind("clod");
        for (int i = 0; clods.Count > 0 && i < 95; i++)
        {
            bool shore = i >= 50;
            var p = shore ? c + MathX.Dir(rng.R(0, Mathf.Tau)) * (Expedition.WaterR + rng.R(-0.8f, 1.6f)) : c + rng.InDisk(Expedition.IslandR + 0.5f);
            if (!shore && p.DistanceTo(c + new Vector2(-0.2f, 0.6f)) < 2.2f) continue; // the queen's own ground
            if (shore && Mathf.Abs(p.Y - c.Y) < 2f && p.X < c.X) continue;              // the bridge's landing
            dress.Place(dress.Pick(clods), p, rng.R(0.25f, 0.95f) * (rng.F() < 0.15f ? 1.6f : 1), rng.R(0.15f, 0.45f), 0.5f, Basis.Identity, rng.R(0, Mathf.Tau), rng.R(0.8f, 1.1f));
        }
        // crumb soil: the island and the waterline are covered in small aggregates from every clod scan, densest at the water
        for (int i = 0; clods.Count > 0 && i < 320; i++)
        {
            bool shore = i >= 170;
            var p = shore ? c + MathX.Dir(rng.R(0, Mathf.Tau)) * (Expedition.WaterR - 0.4f + Mathf.Pow(rng.F(), 1.6f) * 4.5f) : c + rng.InDisk(Expedition.IslandR + 0.6f);
            if (!shore && p.DistanceTo(c + new Vector2(-0.2f, 0.6f)) < 1.4f) continue;
            if (shore && Mathf.Abs(p.Y - c.Y) < 1.6f && p.X < c.X) continue;
            dress.Place(dress.Pick(clods), p, rng.R(0.09f, 0.32f), rng.R(0.25f, 0.55f), 0.6f, Basis.Identity, rng.R(0, Mathf.Tau), rng.R(0.75f, 1.05f));
        }
        for (int i = 0; i < 50; i++)
        {
            var p = c + rng.InDisk(Expedition.IslandR + 0.6f);
            float r = p.DistanceTo(c);
            float s = rng.F() < 0.12f ? rng.R(0.35f, 0.7f) : rng.R(0.05f, 0.22f); // clods and grit, a few real pebbles
            if (r < 2.6f && s > 0.3f) continue; // the queen's bed stays clear of big stones
            dress.Stone(p, s * 2f, rng.R(0.25f, 0.6f));
        }
        // the shore: grit and pebbles thick at the waterline and thinning up the bank, dry grass leaning over it
        for (int i = 0; i < 230; i++)
        {
            float a = rng.R(0, Mathf.Tau), r = Expedition.WaterR + 0.6f + Mathf.Pow(rng.F(), 1.8f) * 5.5f;
            var p = c + MathX.Dir(a) * r;
            if (Mathf.Abs(p.Y - c.Y) < 2.5f && p.X < c.X) continue; // the bridge's landing stays open
            dress.Stone(p, rng.R(0.06f, 0.32f) * (r < Expedition.WaterR + 2 ? 1.3f : 1), rng.R(0.2f, 0.6f));
        }
        for (int k = 0; k < 9; k++)
        {
            float a = rng.R(0, Mathf.Tau);
            var p = c + MathX.Dir(a) * rng.R(Expedition.WaterR + 3.5f, Expedition.ShoreR + 3);
            if (Mathf.Abs(p.Y - c.Y) < 7f && p.X < c.X) continue; // nothing tall between the path and the water
            int n = rng.I(5, 14);
            for (int i = 0; i < n; i++) dress.Sprig(p + rng.InDisk(1.6f), rng.R(4f, 9f), rng.R(0.6f, 1f));
        }
        dress.Commit();
        // loose straws the wind left round the far shore and on the bank, beside the five the colony can carry
        for (int i = 0; i < 7; i++)
        {
            var straw = StrawProps.Make(i, false);
            if (straw == null) break;
            float a = rng.R(-2.2f, 2.2f) + (i % 2 == 0 ? 0 : Mathf.Pi * 0.5f);
            var p = c + MathX.Dir(a) * rng.R(Expedition.WaterR + 2.5f, Expedition.ShoreR + 4);
            if (p.X < c.X - 8 && Mathf.Abs(p.Y - c.Y) < 9) continue; // the carryable ones lie on the near path
            straw.Rotation = new Vector3(0, rng.R(0, Mathf.Tau), 0);
            straw.Position = new Vector3(p.X, hf.Sample(p.X, p.Y) - 0.08f, p.Y);
            w.Root.AddChild(straw);
        }

        // the second tube: its open half stuck in the island's mud at a slant, glinting, the break toward the water
        var tubeScan = NatureAssets.Kind("tube");
        if (tubeScan.Count > 0)
        {
            var glass = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.86f, 0.95f, 0.93f, 0.08f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.01f, MetallicSpecular = 1f, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                RimEnabled = true, Rim = 0.35f, RimTint = 0.2f, // no screen refraction: it smeared at the frame's edge
            };
            var t = tubeScan[0];
            float sr = TubeR * 1.6f / Mathf.Max(t.Size.X, t.Size.Z), sl = 7.5f / t.Size.Y;
            var at = c + new Vector2(2.6f, -1.8f);
            var root = new Node3D { Position = new Vector3(at.X, hf.Sample(at.X, at.Y) - 0.35f, at.Y), Rotation = new Vector3(0.0f, -0.6f, 0.32f) };
            root.AddChild(new MeshInstance3D { Mesh = t.Mesh, MaterialOverride = glass, Basis = new Basis(new Vector3(0, -sr, 0), new Vector3(sl, 0, 0), new Vector3(0, 0, sr)) });
            w.Root.AddChild(root);
            var shards = NatureAssets.Kind("shard");
            for (int i = 0; i < 7 && shards.Count > 0; i++)
            {
                var piece = shards[i % shards.Count];
                var p = at + rng.InDisk(2.4f);
                if (p.DistanceTo(c) > Expedition.IslandR) continue;
                w.Root.AddChild(new MeshInstance3D
                {
                    Mesh = piece.Mesh, MaterialOverride = glass, Scale = Vector3.One * (rng.R(0.3f, 0.9f) * 2f / piece.Longest),
                    Position = new Vector3(p.X, hf.Sample(p.X, p.Y) + 0.02f, p.Y), Rotation = new Vector3(rng.R(-0.15f, 0.15f), rng.R(0, Mathf.Tau), rng.F() < 0.5f ? 0 : Mathf.Pi),
                });
            }
        }

        // her children: the island queen's pale workers lie dead around her, on their backs and sides, legs drawn in
        // (she lives: the sim spawns her when the bridge lesson starts, Sim.BeginBridge)
        const int Fallen = 9;
        var fallen = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = AntMesh.Build(AntType.Worker), InstanceCount = Fallen };
        for (int i = 0; i < Fallen; i++)
        {
            float a = i * 2.39996f + rng.R(-0.4f, 0.4f), r = rng.R(1.8f, Expedition.IslandR - 0.6f);
            var p = c + new Vector2(-0.2f, 0.6f) + MathX.Dir(a) * r;
            if (p.DistanceTo(c) > Expedition.IslandR - 0.4f) p = c + (p - c).Normalized() * (Expedition.IslandR - 0.5f);
            bool back = rng.F() < 0.6f;
            var up = back ? Vector3.Down : new Vector3(rng.R(-1, 1), 0.3f, rng.R(-1, 1)).Normalized();
            var basis = back ? new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Forward, Mathf.Pi) * Basis.FromScale(Vector3.One * 0.95f)
                             : AntRenderer.Orient(up, rng.R(0, Mathf.Tau), 0.95f);
            fallen.SetInstanceTransform(i, new Transform3D(basis, new Vector3(p.X, hf.Sample(p.X, p.Y) + (back ? 0.32f : 0.2f), p.Y)));
            fallen.SetInstanceCustomData(i, new Color(i * 5 % 16, 128 * 256 + 128, 1024 * (i % 3 == 0 ? 1 : 0), 24)); // some lost a leg; pale
        }
        w.Root.AddChild(new MultiMeshInstance3D { Multimesh = fallen, MaterialOverride = AntRenderer.AntMat(Shaders.Get(Shaders.Ant), Config.Wild, AntType.Worker, true), Name = "IslandDead" });
    }

    /// The waterline's wet soil: darker in a band round the shore and the island (through the terrain's AO).
    static void PondWet(Heightfield hf, float[] ao)
    {
        var c = Expedition.Pond;
        for (int z = (int)c.Y - 18; z <= (int)c.Y + 18; z++)
            for (int x = (int)c.X - 18; x <= (int)c.X + 18; x++)
            {
                float r = new Vector2(x, z).DistanceTo(c);
                float shore = 1 - Mathf.SmoothStep(0.4f, 2.4f, Mathf.Abs(r - (Expedition.WaterR - 0.6f)));
                float isle = 1 - Mathf.SmoothStep(0.3f, 1.6f, Mathf.Abs(r - (Expedition.IslandR + 0.2f)));
                ao[z * hf.N + x] *= 1 - 0.38f * Mathf.Max(shore, isle);
            }
    }

    /// No grass, leaf or litter grows in the water (the island and the shore are dressed by BuildPond).
    public static bool InPond(Vector2 p, float margin = 0)
        => p.DistanceTo(Expedition.Pond) < Expedition.WaterR + margin;
}
