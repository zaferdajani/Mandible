using Godot;

namespace Mandible;

/// Builds one anatomically structured ant facing +Z with feet on y=0.
/// Vertex color R = part id / 255 (animated in the shader), G = weight along a limb,
/// UV2 = limb pivot (x,z) so the shader can swing legs, antennae and mandibles.
public static class AntMesh
{
    public const int Body = 0, Head = 12, Gaster = 13, Eye = 11;
    // legs 1..3 left (+x) front->rear, 4..6 right (-x); antennae 7,8; mandibles 9,10

    struct Shape
    {
        public float Head, Mandible, Gaster, Thorax, Leg, Seg;
    }

    public static ArrayMesh Build(AntType type)
    {
        Shape s = type switch
        {
            AntType.Soldier => new Shape { Head = 1.35f, Mandible = 1.45f, Gaster = 1.0f, Thorax = 1.05f, Leg = 1.0f, Seg = 14 },
            AntType.Major => new Shape { Head = 1.85f, Mandible = 1.7f, Gaster = 1.05f, Thorax = 1.1f, Leg = 0.95f, Seg = 16 },
            AntType.Queen => new Shape { Head = 1.15f, Mandible = 1.0f, Gaster = 1.55f, Thorax = 1.45f, Leg = 0.95f, Seg = 18 },
            _ => new Shape { Head = 1.0f, Mandible = 1.0f, Gaster = 1.0f, Thorax = 1.0f, Leg = 1.0f, Seg = 12 },
        };
        int seg = (int)s.Seg;
        var k = new MeshKit();
        Color Part(int id, float w = 0, float region = 0) => new(id / 255f, w, region, 1);

        float bodyY = 0.13f;
        // Gaster (abdomen)
        float gz = -0.30f - (s.Gaster - 1) * 0.12f;
        k.Ellipsoid(new Vector3(0, bodyY + 0.01f * s.Gaster, gz), Scaled(0.13f * s.Gaster, 0.11f * s.Gaster, 0.18f * s.Gaster, -0.18f),
            seg, seg - 3, Part(Gaster, 0, 1), Vector2.Zero);
        // Petiole node
        k.Ellipsoid(new Vector3(0, bodyY + 0.01f, -0.105f), Scaled(0.035f, 0.05f, 0.035f, 0), 8, 6, Part(Body, 0, 0.5f), Vector2.Zero);
        // Mesosoma (thorax), slightly humped
        float t = s.Thorax;
        k.Ellipsoid(new Vector3(0, bodyY + 0.01f, 0.02f), Scaled(0.058f * t, 0.06f * t, 0.125f * t, 0.12f), seg, seg - 4, Part(Body, 0, 0.5f), Vector2.Zero);
        k.Ellipsoid(new Vector3(0, bodyY + 0.045f * t, 0.075f), Scaled(0.05f * t, 0.04f * t, 0.06f * t, 0), 10, 6, Part(Body, 0, 0.5f), Vector2.Zero);
        // Head
        float h = s.Head;
        var headC = new Vector3(0, bodyY + 0.025f, 0.2f + 0.07f * (h - 1));
        k.Ellipsoid(headC, Scaled(0.085f * h, 0.07f * h, 0.085f * h, -0.15f), seg, seg - 3, Part(Head, 0, 0), Vector2.Zero);
        // Eyes
        for (int side = -1; side <= 1; side += 2)
            k.Ellipsoid(headC + new Vector3(side * 0.075f * h, 0.012f * h, 0.02f * h), Scaled(0.018f * h, 0.022f * h, 0.025f * h, 0), 8, 5, Part(Eye), Vector2.Zero);

        // Mandibles: two curved segments, pivot at the jaw hinge
        for (int side = -1; side <= 1; side += 2)
        {
            int id = side > 0 ? 9 : 10;
            float m = s.Mandible;
            var hinge = headC + new Vector3(side * 0.045f * h, -0.035f * h, 0.07f * h);
            var mid = hinge + new Vector3(side * 0.02f * m, -0.005f, 0.06f * m);
            var tip = mid + new Vector3(-side * 0.045f * m, -0.004f, 0.035f * m);
            var piv = new Vector2(hinge.X, hinge.Z);
            k.Tube(hinge, mid, 0.017f * m, 0.012f * m, 5, Part(id, 0.3f), Part(id, 0.7f), piv, false);
            k.Tube(mid, tip, 0.012f * m, 0.003f, 5, Part(id, 0.7f), Part(id, 1f), piv);
        }

        // Antennae: elbowed scape + funiculus with a club
        for (int side = -1; side <= 1; side += 2)
        {
            int id = side > 0 ? 7 : 8;
            var root = headC + new Vector3(side * 0.035f * h, 0.035f * h, 0.06f * h);
            var elbow = root + new Vector3(side * 0.09f, 0.1f, 0.02f);
            var tip = elbow + new Vector3(side * 0.05f, -0.02f, 0.2f);
            var piv = new Vector2(root.X, root.Z);
            k.Tube(root, elbow, 0.011f, 0.009f, 5, Part(id, 0.1f), Part(id, 0.5f), piv, false);
            k.Tube(elbow, tip, 0.008f, 0.014f, 5, Part(id, 0.5f), Part(id, 1f), piv);
        }

        // Legs: coxa -> femur (up/out) -> tibia/tarsus (down to ground)
        float[] hipZ = { 0.085f * t, 0.025f * t, -0.035f * t };
        float[] reach = { 0.17f, 0.02f, -0.2f };
        float L = s.Leg;
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 3; i++)
            {
                int id = (side > 0 ? 1 : 4) + i;
                var hip = new Vector3(side * 0.04f * t, bodyY - 0.02f, hipZ[i]);
                var knee = new Vector3(side * (0.17f + 0.02f * i) * L, bodyY + 0.07f * L, hipZ[i] + reach[i] * 0.45f * L);
                var ankle = new Vector3(side * (0.27f + 0.03f * i) * L, 0.03f, hipZ[i] + reach[i] * L);
                var foot = ankle + new Vector3(side * 0.035f, -0.03f, reach[i] * 0.15f);
                var piv = new Vector2(hip.X, hip.Z);
                k.Tube(hip, knee, 0.02f * t, 0.016f, 6, Part(id, 0f), Part(id, 0.45f), piv, false);
                k.Tube(knee, ankle, 0.014f, 0.009f, 6, Part(id, 0.45f), Part(id, 0.9f), piv, false);
                k.Tube(ankle, foot, 0.008f, 0.004f, 4, Part(id, 0.9f), Part(id, 1f), piv);
            }
        return k.Commit();
    }

    static Basis Scaled(float rx, float ry, float rz, float pitch)
    {
        var b = new Basis(Vector3.Right, pitch);
        return b * Basis.FromScale(new Vector3(rx, ry, rz));
    }

    /// Crumb carried in the mandibles.
    public static ArrayMesh Crumb()
    {
        var k = new MeshKit();
        var rng = new Rng(7);
        k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(0.09f, 0.07f, 0.08f)), 8, 6, new Color(1, 1, 1), Vector2.Zero,
            u => 0.8f + 0.35f * Mathf.Abs(Mathf.Sin(u.X * 7 + u.Y * 5) * Mathf.Cos(u.Z * 6)) + rng.R(-0.05f, 0.05f));
        k.RecalcNormals();
        return k.Commit();
    }
}
