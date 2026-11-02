using System;
using Godot;

namespace Mandible;

/// Level 1's first sight: the collector's glass test tube, burst on the stones where the alley begins. It is built
/// like the real thing: a thick-walled glass tube with a rounded closed end, a water reservoir held back by a
/// cotton plug, the colony's sand bed and a handwritten paper label; the open half lies apart with its own cotton
/// stopper; the break is jagged, shards of curved glass lie in the grit with sharp edges that catch the sun, damp
/// substrate has spilled out, and a few ants lie stunned or dead among them. Visual only: nothing here is an obstacle.
public static partial class WorldBuilder
{
    const float TubeR = 1.7f, TubeWall = 0.13f;

    static void BrokenTube(World w, Rng rng)
    {
        var hf = w.HF;
        var glass = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.86f, 0.95f, 0.93f, 0.07f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.01f, Metallic = 0f, MetallicSpecular = 1f, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            RefractionEnabled = true, RefractionScale = 0.03f, RimEnabled = true, Rim = 0.3f, RimTint = 0.2f,
        };
        var edgeGlass = (StandardMaterial3D)glass.Duplicate(); // the broken edges read faintly green, as thick glass does
        edgeGlass.AlbedoColor = new Color(0.78f, 0.9f, 0.84f, 0.2f);
        edgeGlass.Rim = 0.45f;

        // the closed half: lying on its side in the grass beside the alley, its broken mouth toward the path
        var tubeAt = new Vector2(33f, 253.6f);
        float yaw = 0.18f;
        var dir = MathX.Dir(yaw);
        float lenA = 12.5f;
        var a0 = tubeAt;
        var root = new Node3D { Position = new Vector3(a0.X, hf.Sample(a0.X, a0.Y) + TubeR * 0.92f, a0.Y), Rotation = new Vector3(0.03f, -yaw, 0.05f) };
        w.Root.AddChild(root);
        var jag = Jagged(rng, 9, 0.2f, 1.6f);
        root.AddChild(new MeshInstance3D { Mesh = Shell(a => 0, a => lenA - jag(a), closedStart: true, rimAtEnd: true), MaterialOverride = glass });
        root.AddChild(new MeshInstance3D { Mesh = Rim(a => lenA - jag(a)), MaterialOverride = edgeGlass });

        // inside: the water reservoir behind its cotton plug, then the colony's sand bed
        var water = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.75f, 0.72f, 0.38f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.0f, MetallicSpecular = 1, RefractionEnabled = true, RefractionScale = 0.06f };
        float inner = TubeR - TubeWall;
        root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = inner * 0.97f, BottomRadius = inner * 0.97f, Height = 3.2f, RadialSegments = 32 }, MaterialOverride = water, Position = new Vector3(1.3f, 0, 0), Rotation = new Vector3(0, 0, Mathf.Pi / 2) });
        var cotton = new ShaderMaterial { Shader = Shaders.Get(Shaders.Cotton) };
        cotton.SetShaderParameter("noise_b", w.NoiseB);
        root.AddChild(new MeshInstance3D { Mesh = Fluff(rng, inner * 0.98f, 1.5f), MaterialOverride = cotton, Position = new Vector3(3.6f, 0, 0) });
        // the colony's bed and the damp clumps it spilled: the same photographed soils as the yard
        var substrate = new StandardMaterial3D { AlbedoTexture = NatureAssets.Texture("ground/gravelly_sand_diff"), NormalEnabled = true, NormalTexture = NatureAssets.Texture("ground/gravelly_sand_nor"), Uv1Triplanar = true, Uv1Scale = new Vector3(0.12f, 0.12f, 0.12f), Roughness = 1 };
        var damp = new StandardMaterial3D { AlbedoTexture = NatureAssets.Texture("ground/brown_mud_dry_diff"), NormalEnabled = true, NormalTexture = NatureAssets.Texture("ground/brown_mud_dry_nor"), AlbedoColor = new Color(0.8f, 0.72f, 0.64f), Uv1Triplanar = true, Uv1Scale = new Vector3(0.2f, 0.2f, 0.2f), Roughness = 0.75f };
        root.AddChild(new MeshInstance3D { Mesh = SandBed(inner * 0.96f, lenA - 4.6f - 1.2f), MaterialOverride = substrate, Position = new Vector3(4.4f, 0, 0) });
        // the collector's handwritten label wrapped round the glass, its lines running along the tube
        var paper = new ShaderMaterial { Shader = Shaders.Get(Shaders.TubeLabel) };
        paper.SetShaderParameter("label", NatureAssets.Texture("tex/tube_label"));
        root.AddChild(new MeshInstance3D { Mesh = Band(TubeR + 0.012f, 2.0f, 0.1f, 2.0f), MaterialOverride = paper, Position = new Vector3(6.7f, 0, 0) });

        // the open half, rolled away down the bank, its own cotton stopper still in the mouth
        float lenB = 6.2f;
        var b0 = tubeAt + dir * (lenA + 3.2f) + new Vector2(0.4f, 6.8f);
        var root2 = new Node3D { Position = new Vector3(b0.X, hf.Sample(b0.X, b0.Y) + TubeR * 0.92f, b0.Y), Rotation = new Vector3(0, -yaw - 2.5f, 0.04f) };
        w.Root.AddChild(root2);
        var jag2 = Jagged(rng, 7, 0.1f, 1.3f);
        root2.AddChild(new MeshInstance3D { Mesh = Shell(a => jag2(a), a => lenB, closedStart: false, rimAtEnd: false), MaterialOverride = glass });
        root2.AddChild(new MeshInstance3D { Mesh = Rim(a => jag2(a)), MaterialOverride = edgeGlass });
        root2.AddChild(new MeshInstance3D { Mesh = Fluff(rng, inner * 0.98f, 1.9f), MaterialOverride = cotton, Position = new Vector3(lenB - 0.7f, 0, 0) });

        // shards: curved pieces of the wall, large and small, lying in the grit
        var mouth = tubeAt + dir * lenA;
        for (int i = 0; i < 34; i++)
        {
            float big = i < 6 ? rng.R(1.2f, 2.6f) : rng.R(0.25f, 1.0f);
            var p = mouth + dir * rng.R(-1.5f, 7f) + new Vector2(-dir.Y, dir.X) * rng.R(-4.5f, 4.5f);
            var shard = new MeshInstance3D { Mesh = Shard(rng, big), MaterialOverride = i % 3 == 0 ? edgeGlass : glass };
            shard.Position = new Vector3(p.X, hf.Sample(p.X, p.Y) + 0.03f, p.Y);
            // most lie flat on their curve, a few stand on edge in the grit
            shard.Rotation = rng.F() < 0.8f ? new Vector3(rng.R(-0.15f, 0.15f), rng.R(0, Mathf.Tau), rng.F() < 0.5f ? 0 : Mathf.Pi) : new Vector3(rng.R(0.9f, 1.3f), rng.R(0, Mathf.Tau), 0);
            w.Root.AddChild(shard);
        }
        // the spill: damp clumps of the colony's sand bed and fine grains fanned from the mouth
        for (int i = 0; i < 9; i++)
        {
            var p = mouth + dir * rng.R(-0.5f, 3.5f) + new Vector2(-dir.Y, dir.X) * rng.R(-1.6f, 1.6f);
            float r = rng.R(0.2f, 0.55f);
            w.Root.AddChild(new MeshInstance3D { Mesh = PebbleMesh(70 + i), MaterialOverride = damp, Position = new Vector3(p.X, hf.Sample(p.X, p.Y) + r * 0.15f, p.Y), Rotation = new Vector3(0, rng.R(0, 3), 0), Scale = new Vector3(r, r * 0.6f, r * rng.R(0.8f, 1.3f)) });
        }
        var spill = new NatureScatter.Dresser(w, rng);
        for (int i = 0; i < 260; i++)
        {
            float along = Mathf.Pow(rng.F(), 1.6f) * 7f - 0.4f, spread = 0.5f + along * 0.55f;
            var p = mouth + dir * along + new Vector2(-dir.Y, dir.X) * rng.R(-spread, spread) * Mathf.Sqrt(rng.F());
            spill.Place(spill.Pick(spill.Stones), p, rng.R(0.08f, 0.26f) * (along < 1.5f ? 1.2f : 1), 0.3f, 0.5f, Basis.Identity, rng.R(0, Mathf.Tau), rng.R(0.9f, 1.05f));
        }
        spill.Commit();

        // the fall cost the colony: a few workers lie on their backs among the glass
        var antMesh = AntMesh.Build(AntType.Worker);
        var fallen = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = antMesh, InstanceCount = 4 };
        var spots = new[] { mouth + dir * 1.8f + new Vector2(-dir.Y, dir.X) * 1.2f, mouth + dir * 3.6f - new Vector2(-dir.Y, dir.X) * 2.1f, mouth + dir * 0.6f - new Vector2(-dir.Y, dir.X) * 2.6f, mouth + dir * 5.2f + new Vector2(-dir.Y, dir.X) * 0.4f };
        for (int i = 0; i < 4; i++)
        {
            var p = spots[i];
            var basis = new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Forward, i == 3 ? 0.5f : Mathf.Pi) * Basis.FromScale(Vector3.One * 0.95f);
            fallen.SetInstanceTransform(i, new Transform3D(basis, new Vector3(p.X, hf.Sample(p.X, p.Y) + (i == 3 ? 0.15f : 0.32f), p.Y)));
            fallen.SetInstanceCustomData(i, new Color(i * 3 % 16, 128 * 256 + 128, 0, 0));
        }
        w.Root.AddChild(new MultiMeshInstance3D { Multimesh = fallen, MaterialOverride = AntRenderer.AntMat(Shaders.Get(Shaders.Ant), Config.Player, AntType.Worker, true), Name = "FallenAnts" });
    }

    /// A jagged break line: the tube's length lost at each angle around it, in sharp piecewise steps.
    static Func<float, float> Jagged(Rng rng, int teeth, float min, float max)
    {
        var v = new float[teeth];
        for (int i = 0; i < teeth; i++) v[i] = rng.R(min, max) * (i % 2 == 0 ? 1 : 0.45f);
        return a =>
        {
            float t = Mathf.PosMod(a / Mathf.Tau, 1) * teeth;
            int i = (int)t; float f = t - i;
            return Mathf.Lerp(v[i % teeth], v[(i + 1) % teeth], f);
        };
    }

    /// A thick glass tube along +X from x0(angle) to x1(angle): outer and inner walls, and a rounded closed start.
    static ArrayMesh Shell(Func<float, float> x0, Func<float, float> x1, bool closedStart, bool rimAtEnd)
    {
        var k = new MeshKit();
        const int seg = 56, rows = 20, capRows = 8;
        var white = new Color(1, 1, 1);
        foreach (int side in new[] { 1, -1 })
        {
            float r = side == 1 ? TubeR : TubeR - TubeWall;
            int start = k.V.Count;
            int rowCount = rows + 1 + (closedStart ? capRows : 0);
            for (int s = 0; s <= seg; s++)
            {
                float a = s * Mathf.Tau / seg;
                var radial = new Vector3(0, Mathf.Cos(a), Mathf.Sin(a));
                if (closedStart)
                    for (int c = capRows; c >= 1; c--)
                    {
                        // the hemispherical bottom of the tube
                        float phi = c * Mathf.Pi / 2 / capRows;
                        var n = new Vector3(-Mathf.Sin(phi), 0, 0) + radial * Mathf.Cos(phi);
                        k.Add(new Vector3(-Mathf.Sin(phi) * r, 0, 0) + radial * (Mathf.Cos(phi) * r), n * side, white, new Vector2(s / (float)seg, 0), Vector2.Zero);
                    }
                float a0 = x0(a), a1 = x1(a);
                for (int j = 0; j <= rows; j++)
                {
                    float x = Mathf.Lerp(a0, a1, j / (float)rows);
                    k.Add(new Vector3(x, 0, 0) + radial * r, radial * side, white, new Vector2(s / (float)seg, j / (float)rows), Vector2.Zero);
                }
            }
            for (int s = 0; s < seg; s++)
                for (int j = 0; j < rowCount - 1; j++)
                {
                    int i0 = start + s * rowCount + j, i1 = start + (s + 1) * rowCount + j;
                    if (side == 1) k.Quad(i0, i1, i1 + 1, i0 + 1); else k.Quad(i0, i0 + 1, i1 + 1, i1);
                }
        }
        return k.Commit();
    }

    /// The broken edge itself: the thickness of the glass along the jagged line, where it shows pale green.
    static ArrayMesh Rim(Func<float, float> x)
    {
        var k = new MeshKit();
        const int seg = 56;
        var white = new Color(1, 1, 1);
        for (int s = 0; s <= seg; s++)
        {
            float a = s * Mathf.Tau / seg;
            var radial = new Vector3(0, Mathf.Cos(a), Mathf.Sin(a));
            var p = new Vector3(x(a), 0, 0);
            k.Add(p + radial * TubeR, Vector3.Right, white, Vector2.Zero, Vector2.Zero);
            k.Add(p + radial * (TubeR - TubeWall), Vector3.Right, white, Vector2.Zero, Vector2.Zero);
        }
        for (int s = 0; s < seg; s++) k.Quad(s * 2, s * 2 + 2, s * 2 + 3, s * 2 + 1);
        return k.Commit();
    }

    /// A curved piece of the wall with a jagged outline, glass on both faces.
    static ArrayMesh Shard(Rng rng, float size)
    {
        var k = new MeshKit();
        var white = new Color(1, 1, 1);
        int n = rng.I(4, 7);
        var outline = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.Tau / n + rng.R(-0.3f, 0.3f);
            float r = size * 0.5f * rng.R(0.45f, 1.1f) * (i % 2 == 0 ? 1 : rng.R(0.4f, 0.8f)); // sharp points between short sides
            outline[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * rng.R(0.5f, 1f));
        }
        float bend = 1f / TubeR;
        Vector3 P(Vector2 q, float lift) => new(q.X, lift - bend * q.Y * q.Y * 0.5f, q.Y);
        foreach (float lift in new[] { TubeWall * 0.5f, -TubeWall * 0.5f })
        {
            int c = k.Add(P(Vector2.Zero, lift), Vector3.Up * Mathf.Sign(lift), white, Vector2.Zero, Vector2.Zero);
            int first = k.V.Count;
            foreach (var q in outline) k.Add(P(q, lift), Vector3.Up * Mathf.Sign(lift), white, Vector2.Zero, Vector2.Zero);
            for (int i = 0; i < n; i++) { if (lift > 0) k.Tri(c, first + (i + 1) % n, first + i); else k.Tri(c, first + i, first + (i + 1) % n); }
        }
        // the edges: thin facets of thick glass
        int top = 1, bot = n + 2;
        for (int i = 0; i < n; i++) k.Quad(top + i, top + (i + 1) % n, bot + (i + 1) % n, bot + i);
        return k.Commit();
    }

    /// A soft plug of cotton wool: a cylinder of fibrous lumps sized to the tube's bore.
    static ArrayMesh Fluff(Rng rng, float r, float len)
    {
        var k = new MeshKit();
        var white = new Color(1, 1, 1);
        var noise = new FastNoiseLite { Seed = rng.I(0, 9999), Frequency = 3.2f };
        var fine = new FastNoiseLite { Seed = rng.I(0, 9999), Frequency = 9f };
        k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(len * 0.5f, r, r)), 40, 24, white, Vector2.Zero, u => 1 + 0.12f * noise.GetNoise3D(u.X * 3, u.Y * 3, u.Z * 3) + 0.05f * fine.GetNoise3D(u.X * 3, u.Y * 3, u.Z * 3));
        k.RecalcNormals();
        for (int i = 0; i < k.Nrm.Count; i++) if (k.Nrm[i].Dot(k.V[i]) < 0) k.Nrm[i] = -k.Nrm[i];
        return k.Commit();
    }

    /// The colony's sand bed lying along the bottom of the tube: a flattened half-cylinder with an uneven top.
    static ArrayMesh SandBed(float r, float len)
    {
        var k = new MeshKit();
        var white = new Color(1, 1, 1);
        const int seg = 18, rows = 16;
        float fill = -r * 0.35f;   // the surface of the sand, below the tube's axis
        for (int j = 0; j <= rows; j++)
        {
            float x = len * j / rows;
            for (int s = 0; s <= seg; s++)
            {
                float a = Mathf.Pi + Mathf.Acos(Mathf.Clamp(-fill / r, -1, 1)) * (s / (float)seg * 2 - 1); // around the bottom arc
                var p = new Vector3(x, Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                if (p.Y > fill) p.Y = fill + 0.06f * Mathf.Sin(x * 3.1f + s);
                k.Add(p, new Vector3(0, 1, 0), white, new Vector2(x * 0.2f, s / (float)seg), Vector2.Zero);
            }
        }
        for (int j = 0; j < rows; j++)
            for (int s = 0; s < seg; s++)
            {
                int i0 = j * (seg + 1) + s, i1 = (j + 1) * (seg + 1) + s;
                k.Quad(i0, i0 + 1, i1 + 1, i1);
            }
        // the flat top across the arc
        k.RecalcNormals();
        return k.Commit();
    }

    /// A paper label: a band of the cylinder from angle a0 to a1, `width` long along the tube.
    static ArrayMesh Band(float r, float width, float a0, float a1)
    {
        var k = new MeshKit();
        var white = new Color(1, 1, 1);
        const int seg = 24;
        for (int s = 0; s <= seg; s++)
        {
            float a = Mathf.Lerp(a0, a1, s / (float)seg);
            var radial = new Vector3(0, Mathf.Cos(a), Mathf.Sin(a));
            k.Add(radial * r, radial, white, new Vector2(s / (float)seg, 0), Vector2.Zero);
            k.Add(new Vector3(width, 0, 0) + radial * r, radial, white, new Vector2(s / (float)seg, 1), Vector2.Zero);
        }
        for (int s = 0; s < seg; s++) k.Quad(s * 2, s * 2 + 1, s * 2 + 3, s * 2 + 2);
        return k.Commit();
    }
}
