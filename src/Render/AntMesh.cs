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
        if (type == AntType.Beetle) return Beetle(false);
        if (type == AntType.StagBeetle) return Beetle(true);
        if (type == AntType.Spider) return Spider();
        if (type == AntType.Wasp) return Wasp();
        Shape s = type switch
        {
            AntType.Soldier => new Shape { Head = 1.35f, Mandible = 1.45f, Gaster = 1.0f, Thorax = 1.05f, Leg = 1.0f, Seg = 14 },
            AntType.Major => new Shape { Head = 1.85f, Mandible = 1.7f, Gaster = 1.05f, Thorax = 1.1f, Leg = 0.95f, Seg = 16 },
            // alate: bulky flight-muscle thorax, slender body
            AntType.Gunner => new Shape { Head = 1.1f, Mandible = 0.8f, Gaster = 1.4f, Thorax = 1.0f, Leg = 1.0f, Seg = 14 },
            AntType.Warlord => new Shape { Head = 1.75f, Mandible = 1.9f, Gaster = 1.1f, Thorax = 1.25f, Leg = 1.05f, Seg = 20 },
            AntType.Matron => new Shape { Head = 1.2f, Mandible = 1.0f, Gaster = 1.65f, Thorax = 1.2f, Leg = 1.0f, Seg = 20 },
            AntType.Marshal => new Shape { Head = 1.25f, Mandible = 1.45f, Gaster = 1.0f, Thorax = 1.5f, Leg = 0.95f, Seg = 18 },
            AntType.Flyer => new Shape { Head = 1.05f, Mandible = 1.15f, Gaster = 0.95f, Thorax = 1.35f, Leg = 0.95f, Seg = 14 },
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

    static Color Tag(int id, float w = 0, float region = 0) => new(id / 255f, w, region, 1);

    /// Ground beetle (domed elytra) or stag beetle (antler-like mandibles). Faces +Z, length ~1.
    static ArrayMesh Beetle(bool stag)
    {
        var k = new MeshKit();
        float y = 0.16f;
        // elytra: two wing cases meeting at a raised suture
        for (int side = -1; side <= 1; side += 2)
            k.Ellipsoid(new Vector3(side * 0.08f, y + 0.03f, -0.15f), Scaled(0.115f, 0.13f, 0.3f, -0.05f), 22, 16, Tag(Gaster, 0, 1), Vector2.Zero,
                u => 1f - 0.08f * Mathf.Max(0, -side * u.X)); // flatten the inner edge so the halves meet cleanly
        k.Ellipsoid(new Vector3(0, y + 0.145f, -0.15f), Scaled(0.012f, 0.025f, 0.28f, -0.05f), 6, 6, Tag(Eye), Vector2.Zero);
        // pronotum, a narrow waist, then the head
        k.Ellipsoid(new Vector3(0, y + 0.03f, 0.19f), Scaled(0.165f, 0.085f, 0.1f, 0.12f), 20, 12, Tag(Body, 0, 0.5f), Vector2.Zero,
            u => 1f + 0.08f * Mathf.Abs(u.X) * Mathf.Max(0, u.Y)); // squared-off shoulders
        k.Ellipsoid(new Vector3(0, y - 0.01f, 0.1f), Scaled(0.09f, 0.06f, 0.05f, 0), 12, 8, Tag(Body, 0, 0.5f), Vector2.Zero);
        var headC = new Vector3(0, y + 0.0f, 0.3f);
        k.Ellipsoid(headC, Scaled(stag ? 0.15f : 0.09f, stag ? 0.07f : 0.06f, stag ? 0.09f : 0.07f, 0), 16, 10, Tag(Head), Vector2.Zero);
        for (int side = -1; side <= 1; side += 2)
            k.Ellipsoid(headC + new Vector3(side * (stag ? 0.11f : 0.08f), 0.02f, 0.02f), Scaled(0.02f, 0.02f, 0.02f, 0), 6, 4, Tag(Eye), Vector2.Zero);
        // mandibles
        for (int side = -1; side <= 1; side += 2)
        {
            int id = side > 0 ? 9 : 10;
            var hinge = headC + new Vector3(side * 0.06f, -0.01f, 0.05f);
            var piv = new Vector2(hinge.X, hinge.Z);
            if (stag)
            {
                // great curved antler-jaws with a forked tip and inner teeth
                var a = hinge + new Vector3(side * 0.13f, 0.07f, 0.17f);
                var b = a + new Vector3(-side * 0.01f, 0.05f, 0.2f);
                var tip = b + new Vector3(-side * 0.12f, 0.01f, 0.08f);
                k.Ellipsoid(hinge, Scaled(0.05f, 0.045f, 0.05f, 0), 10, 8, Tag(id, 0.3f), piv);
                k.Tube(hinge, a, 0.048f, 0.042f, 10, Tag(id, 0.3f), Tag(id, 0.6f), piv, false);
                k.Ellipsoid(a, Scaled(0.042f, 0.042f, 0.042f, 0), 10, 8, Tag(id, 0.6f), piv);
                k.Tube(a, b, 0.042f, 0.032f, 10, Tag(id, 0.6f), Tag(id, 0.85f), piv, false);
                k.Ellipsoid(b, Scaled(0.032f, 0.032f, 0.032f, 0), 10, 8, Tag(id, 0.85f), piv);
                k.Tube(b, tip, 0.032f, 0.008f, 8, Tag(id, 0.85f), Tag(id, 1f), piv);
                k.Tube(b, b + new Vector3(side * 0.02f, 0.06f, 0.05f), 0.018f, 0.004f, 6, Tag(id, 0.9f), Tag(id, 1f), piv); // fork
                for (int tooth = 1; tooth <= 2; tooth++)
                {
                    var at = a.Lerp(b, tooth * 0.33f);
                    k.Tube(at, at + new Vector3(-side * 0.07f, -0.01f, 0.01f), 0.016f, 0.003f, 6, Tag(id, 0.7f), Tag(id, 0.75f), piv);
                }
            }
            else
            {
                var tip = hinge + new Vector3(-side * 0.01f, -0.01f, 0.08f);
                k.Tube(hinge, tip, 0.02f, 0.005f, 5, Tag(id, 0.3f), Tag(id, 1f), piv);
            }
        }
        // antennae: short, beaded
        for (int side = -1; side <= 1; side += 2)
        {
            int id = side > 0 ? 7 : 8;
            var root = headC + new Vector3(side * 0.07f, 0.02f, 0.04f);
            var tip = root + new Vector3(side * 0.13f, 0.03f, 0.12f);
            k.Tube(root, tip, 0.01f, 0.016f, 5, Tag(id, 0.1f), Tag(id, 1f), new Vector2(root.X, root.Z));
        }
        // legs: sturdy, spiny
        float[] hz = { 0.2f, 0.08f, -0.05f };
        float[] reach = { 0.18f, 0.0f, -0.22f };
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 3; i++)
            {
                int id = (side > 0 ? 1 : 4) + i;
                var hip = new Vector3(side * 0.1f, y - 0.04f, hz[i]);
                var knee = new Vector3(side * 0.3f, y + 0.08f, hz[i] + reach[i] * 0.4f);
                var ankle = new Vector3(side * 0.42f, 0.03f, hz[i] + reach[i] * 0.85f);
                var foot = ankle + new Vector3(side * 0.06f, -0.03f, reach[i] * 0.25f + 0.02f);
                var piv = new Vector2(hip.X, hip.Z);
                // stout femur, flattened spiny tibia, segmented tarsus
                k.Tube(hip, (hip + knee) * 0.5f + Vector3.Up * 0.015f, 0.034f, 0.042f, 8, Tag(id, 0f), Tag(id, 0.25f), piv, false);
                k.Tube((hip + knee) * 0.5f + Vector3.Up * 0.015f, knee, 0.042f, 0.03f, 8, Tag(id, 0.25f), Tag(id, 0.5f), piv, false);
                k.Tube(knee, ankle, 0.028f, 0.018f, 7, Tag(id, 0.5f), Tag(id, 0.85f), piv, false);
                for (int sp = 1; sp <= 3; sp++)
                {
                    var at = knee.Lerp(ankle, sp / 4f);
                    k.Tube(at, at + new Vector3(side * 0.035f, -0.02f, 0.01f), 0.007f, 0.001f, 4, Tag(id, 0.6f), Tag(id, 0.7f), piv);
                }
                var mid = ankle.Lerp(foot, 0.5f);
                k.Tube(ankle, mid, 0.014f, 0.012f, 5, Tag(id, 0.85f), Tag(id, 0.93f), piv, false);
                k.Tube(mid, foot, 0.012f, 0.005f, 5, Tag(id, 0.93f), Tag(id, 1f), piv);
            }
        return k.Commit();
    }

    /// Wolf spider: eight long legs, big abdomen, eye cluster. Faces +Z.
    static ArrayMesh Spider()
    {
        var k = new MeshKit();
        float y = 0.2f;
        k.Ellipsoid(new Vector3(0, y + 0.04f, -0.24f), Scaled(0.17f, 0.14f, 0.22f, -0.1f), 24, 16, Tag(Gaster, 0, 1), Vector2.Zero);
        k.Ellipsoid(new Vector3(0, y + 0.0f, -0.07f), Scaled(0.04f, 0.04f, 0.04f, 0), 8, 6, Tag(Body), Vector2.Zero); // pedicel
        for (int s = -1; s <= 1; s += 2) // spinnerets
            k.Tube(new Vector3(s * 0.02f, y - 0.02f, -0.44f), new Vector3(s * 0.03f, y - 0.05f, -0.48f), 0.014f, 0.006f, 5, Tag(Gaster, 0, 1), Tag(Gaster, 0, 1), Vector2.Zero);
        var ceph = new Vector3(0, y + 0.02f, 0.06f);
        k.Ellipsoid(ceph, Scaled(0.12f, 0.08f, 0.14f, 0.05f), 18, 10, Tag(Head), Vector2.Zero);
        // eyes: two big forward eyes, a row of small ones
        k.Ellipsoid(ceph + new Vector3(0.035f, 0.06f, 0.11f), Scaled(0.025f, 0.025f, 0.025f, 0), 8, 6, Tag(Eye), Vector2.Zero);
        k.Ellipsoid(ceph + new Vector3(-0.035f, 0.06f, 0.11f), Scaled(0.025f, 0.025f, 0.025f, 0), 8, 6, Tag(Eye), Vector2.Zero);
        for (int e = -2; e <= 1; e++)
            k.Ellipsoid(ceph + new Vector3((e + 0.5f) * 0.03f, 0.03f, 0.135f), Scaled(0.012f, 0.012f, 0.012f, 0), 6, 4, Tag(Eye), Vector2.Zero);
        // chelicerae and pedipalps
        for (int side = -1; side <= 1; side += 2)
        {
            var root = ceph + new Vector3(side * 0.03f, -0.02f, 0.13f);
            k.Tube(root, root + new Vector3(0, -0.07f, 0.03f), 0.025f, 0.012f, 6, Tag(side > 0 ? 9 : 10, 0.3f), Tag(side > 0 ? 9 : 10, 1f), new Vector2(root.X, root.Z));
            var palp = ceph + new Vector3(side * 0.07f, -0.01f, 0.12f);
            k.Tube(palp, palp + new Vector3(side * 0.06f, 0.02f, 0.1f), 0.018f, 0.014f, 5, Tag(side > 0 ? 7 : 8, 0.2f), Tag(side > 0 ? 7 : 8, 1f), new Vector2(palp.X, palp.Z));
        }
        // eight legs; the extra pair reuses the middle gait groups
        float[] hz = { 0.13f, 0.07f, 0.0f, -0.06f };
        float[] dir = { 0.5f, 0.15f, -0.2f, -0.55f };
        int[] ids = { 0, 1, 1, 2 };
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 4; i++)
            {
                int id = (side > 0 ? 1 : 4) + ids[i];
                var hip = new Vector3(side * 0.08f, y, hz[i]);
                var outDir = new Vector3(side * Mathf.Cos(dir[i] * 1.2f), 0, Mathf.Sin(dir[i] * 1.2f));
                var knee = hip + outDir * 0.28f + new Vector3(0, 0.2f, 0);
                var ankle = hip + outDir * 0.55f + new Vector3(0, 0.03f, 0);
                var foot = hip + outDir * 0.66f + new Vector3(0, -y, 0);
                var piv = new Vector2(hip.X, hip.Z);
                k.Ellipsoid(hip, Scaled(0.03f, 0.03f, 0.03f, 0), 8, 6, Tag(id, 0f), piv);
                k.Tube(hip, knee, 0.034f, 0.03f, 8, Tag(id, 0f), Tag(id, 0.45f), piv, false);
                k.Ellipsoid(knee, Scaled(0.032f, 0.032f, 0.032f, 0), 8, 6, Tag(id, 0.45f), piv);
                k.Tube(knee, ankle, 0.028f, 0.02f, 8, Tag(id, 0.45f), Tag(id, 0.85f), piv, false);
                k.Tube(ankle, foot, 0.018f, 0.007f, 6, Tag(id, 0.85f), Tag(id, 1f), piv);
                // bristles along the leg
                for (int sp = 1; sp <= 4; sp++)
                {
                    var at = knee.Lerp(ankle, sp / 5f);
                    k.Tube(at, at + outDir * 0.02f + new Vector3(0, 0.035f, 0), 0.005f, 0.001f, 3, Tag(id, 0.6f), Tag(id, 0.65f), piv);
                }
            }
        return k.Commit();
    }

    /// Hunting wasp: yellow-black banded, narrow waist, stinger, legs hanging in flight. Faces +Z.
    static ArrayMesh Wasp()
    {
        var k = new MeshKit();
        float y = 0.22f;
        // gaster: long tapering spindle, slightly drooped, ending in a stinger
        k.Ellipsoid(new Vector3(0, y - 0.02f, -0.27f), Scaled(0.1f, 0.095f, 0.22f, -0.22f), 22, 16, Tag(Gaster, 0, 1), Vector2.Zero,
            u => 1f - 0.35f * Mathf.Max(0, -u.Z) * Mathf.Max(0, -u.Z));
        k.Tube(new Vector3(0, y - 0.1f, -0.47f), new Vector3(0, y - 0.15f, -0.56f), 0.012f, 0.002f, 5, Tag(Eye), Tag(Eye), Vector2.Zero);
        // petiole: the famous wasp waist
        k.Tube(new Vector3(0, y + 0.01f, -0.04f), new Vector3(0, y, -0.08f), 0.018f, 0.02f, 6, Tag(Body), Tag(Body), Vector2.Zero, false);
        // thorax
        k.Ellipsoid(new Vector3(0, y + 0.03f, 0.05f), Scaled(0.085f, 0.085f, 0.11f, 0.05f), 18, 12, Tag(Body, 0, 0.5f), Vector2.Zero);
        // head with huge kidney-shaped compound eyes
        var headC = new Vector3(0, y + 0.03f, 0.2f);
        k.Ellipsoid(headC, Scaled(0.08f, 0.075f, 0.055f, 0.2f), 16, 10, Tag(Head), Vector2.Zero);
        for (int side = -1; side <= 1; side += 2)
        {
            k.Ellipsoid(headC + new Vector3(side * 0.058f, 0.012f, -0.005f), Scaled(0.03f, 0.055f, 0.04f, 0.2f), 10, 8, Tag(Eye), Vector2.Zero);
            int ma = side > 0 ? 9 : 10;
            var hinge = headC + new Vector3(side * 0.03f, -0.06f, 0.03f);
            k.Tube(hinge, hinge + new Vector3(-side * 0.025f, -0.03f, 0.03f), 0.014f, 0.004f, 5, Tag(ma, 0.3f), Tag(ma, 1f), new Vector2(hinge.X, hinge.Z));
            int an = side > 0 ? 7 : 8;
            var root = headC + new Vector3(side * 0.025f, 0.05f, 0.04f);
            var elbow = root + new Vector3(side * 0.03f, 0.08f, 0.06f);
            k.Tube(root, elbow, 0.01f, 0.009f, 5, Tag(an, 0.1f), Tag(an, 0.5f), new Vector2(root.X, root.Z), false);
            k.Tube(elbow, elbow + new Vector3(side * 0.07f, 0.0f, 0.13f), 0.009f, 0.007f, 5, Tag(an, 0.5f), Tag(an, 1f), new Vector2(root.X, root.Z));
        }
        // legs hang below the body in flight, hind pair trailing
        float[] hz = { 0.09f, 0.05f, 0.0f };
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 3; i++)
            {
                int id = (side > 0 ? 1 : 4) + i;
                var hip = new Vector3(side * 0.05f, y - 0.03f, hz[i]);
                var knee = hip + new Vector3(side * 0.1f, -0.06f, (i - 1) * 0.06f);
                var foot = knee + new Vector3(side * 0.02f, -0.14f, (i - 1) * 0.1f - 0.05f * i);
                var piv = new Vector2(hip.X, hip.Z);
                k.Tube(hip, knee, 0.018f, 0.016f, 6, Tag(id, 0f), Tag(id, 0.5f), piv, false);
                k.Tube(knee, foot, 0.014f, 0.006f, 6, Tag(id, 0.5f), Tag(id, 1f), piv);
            }
        return k.Commit();
    }

    /// Membranous wings (separate translucent batch). R = 1 left / 2 right, G = 0 hinge..1 tip,
    /// UV = venation coordinates, UV2 = hinge (x, y) for the flap rotation about the body axis.
    public static ArrayMesh Wings(AntType type)
    {
        var k = new MeshKit();
        bool wasp = type == AntType.Wasp;
        // (hinge z, length, width, sweep angle) for fore and hind wing
        var pairs = wasp
             ? new[] { (0.09f, 0.48f, 0.12f, 0.35f), (0.03f, 0.34f, 0.09f, 0.55f) }
            : new[] { (0.09f, 0.62f, 0.14f, 0.25f), (0.04f, 0.46f, 0.11f, 0.4f) };
        float hy = wasp ? 0.3f : 0.22f;
        foreach (var (hz, len, wid, sweep) in pairs)
            for (int side = -1; side <= 1; side += 2)
            {
                var col = new Color((side > 0 ? 1 : 2) / 255f, 0, 0, 1);
                var hinge = new Vector3(side * 0.05f, hy, hz);
                // wing points outward and back: direction in the XZ plane
                var dir = new Vector3(side * Mathf.Cos(sweep + 0.9f), 0.05f, -Mathf.Sin(sweep + 0.9f)).Normalized();
                var across = new Vector3(dir.Z, 0, -dir.X) * side;
                int segs = 10;
                int baseIdx = k.V.Count;
                for (int s = 0; s <= segs; s++)
                {
                    float t = (float)s / segs;
                    float w = wid * Mathf.Sin(Mathf.Pi * Mathf.Pow(t, 0.7f)) * (1 - 0.15f * t);
                    var c = hinge + dir * len * t;
                    var cc = new Color(col.R, t, hz + 0.5f, 1); // B carries hinge z for the spread rotation
                    k.Add(c + across * w * 0.35f, Vector3.Up, cc, new Vector2(t, 0.0f), new Vector2(hinge.X, hinge.Y));
                    k.Add(c - across * w * 0.65f, Vector3.Up, cc, new Vector2(t, 1.0f), new Vector2(hinge.X, hinge.Y));
                }
                for (int s = 0; s < segs; s++)
                {
                    int a = baseIdx + s * 2;
                    k.Quad(a, a + 2, a + 3, a + 1);
                }
            }
        return k.Commit();
    }

    /// Silk cocoon wrapped around a webbed ant, and the sticky web blob in flight.
    public static ArrayMesh Silk()
    {
        var k = new MeshKit();
        var n = new FastNoiseLite { Seed = 9, Frequency = 3f };
        k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(0.22f, 0.16f, 0.42f)), 16, 12, new Color(1, 1, 1), Vector2.Zero, u => 1 + 0.18f * n.GetNoise3D(u.X, u.Y, u.Z));
        k.RecalcNormals();
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
