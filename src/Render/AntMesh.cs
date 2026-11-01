using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Builds one anatomically structured ant facing +Z with feet on y=0.
/// Vertex color R = part id / 255 (animated in the shader), G = weight along a limb,
/// UV2 = limb pivot (x,z) so the shader can swing legs, antennae and mandibles.
public static class AntMesh
{
    public const int Body = 0, Head = 12, Gaster = 13, Eye = 11;
    // legs 1..3 left (+x) front->rear, 4..6 right (-x); antennae 7,8; mandibles 9,10
    // 14 crest: spines, plumes, nozzles and studs in the accent colour (metal); 15 plate: armour with an accent rim.
    // Vertex colour B is the body region a part belongs to (0 head, 0.5 thorax, 1 gaster) so the shader moves it with that region.
    public const int Crest = 14, Plate = 15;

    /// Where each creature's head turns: the shader rotates the head group about this model-space point.
    public static readonly System.Collections.Generic.Dictionary<AntType, Vector3> Neck = new();
    /// Iris colour the shader paints under the catchlight: amber for garden ants, ember for fire ants, each bug its own.
    public static Color EyeColor(int team, AntType type) => type switch
    {
        AntType.Beetle => new Color(0.22f, 0.3f, 0.22f),
        AntType.Spider => new Color(0.5f, 0.42f, 0.26f),
        AntType.StagBeetle => new Color(0.36f, 0.12f, 0.05f),
        AntType.Wasp => new Color(0.45f, 0.3f, 0.08f),
        _ => team == 0 ? new Color(0.62f, 0.34f, 0.08f) : new Color(0.8f, 0.22f, 0.06f),
    };

    struct Shape
    {
        public float Head, Mandible, Gaster, Thorax, Leg, Seg;
    }

    /// `far`: the distant-crowd build of a colony caste, with half the rings, four-sided limbs and no setae,
    /// drawn by the renderer once an ant is far enough from the camera that the detail would be sub-pixel.
    public static ArrayMesh Build(AntType type, bool far = false)
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
            AntType.Matron => new Shape { Head = 1.2f, Mandible = 1.0f, Gaster = 1.85f, Thorax = 1.2f, Leg = 1.0f, Seg = 20 },
            AntType.Marshal => new Shape { Head = 1.25f, Mandible = 1.45f, Gaster = 1.0f, Thorax = 1.5f, Leg = 0.95f, Seg = 18 },
            AntType.Flyer => new Shape { Head = 1.05f, Mandible = 1.15f, Gaster = 0.95f, Thorax = 1.35f, Leg = 0.95f, Seg = 14 },
            AntType.Queen => new Shape { Head = 1.15f, Mandible = 1.0f, Gaster = 1.55f, Thorax = 1.45f, Leg = 0.95f, Seg = 18 },
            _ => new Shape { Head = 1.0f, Mandible = 1.0f, Gaster = 1.0f, Thorax = 1.0f, Leg = 1.0f, Seg = 12 },
        };
        int seg = far ? System.Math.Max(8, (int)s.Seg / 2) : (int)s.Seg;
        int sides = far ? 4 : 6, sub = far ? 1 : 2;
        var k = new MeshKit();
        Color Part(int id, float w = 0, float region = 0) => new(id / 255f, w, region, 1);

        float bodyY = 0.13f;
        // Gaster (abdomen): four tergites that telescope, each rear rim riding over the segment behind it,
        // smooth-shaded so the lips read as plates rather than facets
        float gz = -0.30f - (s.Gaster - 1) * 0.12f;
        int gv = k.V.Count, gi = k.I.Count;
        k.Ellipsoid(new Vector3(0, bodyY + 0.01f * s.Gaster, gz), Scaled(0.13f * s.Gaster, 0.11f * s.Gaster, 0.18f * s.Gaster, -0.18f),
            seg + 6, seg + 2, Part(Gaster, 0, 1), Vector2.Zero, u =>
            {
                float along = (u.Z + 1f) * 2f;                 // 0 at the tip, 4 at the waist
                float f = along - Mathf.Floor(along);
                float top = Mathf.SmoothStep(-0.6f, 0.1f, u.Y); // the plates are on the back; the belly stays smooth
                float lip = (1 - f) * (1 - f) * top * (u.Z < 0.85f && u.Z > -0.9f ? 1 : 0);
                return 1 + 0.045f * lip;
            });
        k.SmoothNormals(gv, gi);
        // Petiole node
        k.Ellipsoid(new Vector3(0, bodyY + 0.01f, -0.105f), Scaled(0.035f, 0.05f, 0.035f, 0), 8, 6, Part(Body, 0, 0.5f), Vector2.Zero);
        // Mesosoma (thorax), slightly humped
        float t = s.Thorax;
        k.Ellipsoid(new Vector3(0, bodyY + 0.01f, 0.02f), Scaled(0.058f * t, 0.06f * t, 0.125f * t, 0.12f), seg, seg - 4, Part(Body, 0, 0.5f), Vector2.Zero);
        k.Ellipsoid(new Vector3(0, bodyY + 0.045f * t, 0.075f), Scaled(0.05f * t, 0.04f * t, 0.06f * t, 0), 10, 6, Part(Body, 0, 0.5f), Vector2.Zero);
        // Head
        float h = s.Head;
        var headC = new Vector3(0, bodyY + 0.025f, 0.2f + 0.07f * (h - 1));
        Neck[type] = headC + new Vector3(0, -0.02f, -0.075f * h);
        // the head is a face: a brow that hoods the eyes, sockets the eyes sit in, a flattened clypeus below
        var eyeDir = new Vector3(0.74f, 0.3f, 0.6f).Normalized();
        int hv = k.V.Count, hi = k.I.Count;
        k.Ellipsoid(headC, Scaled(0.09f * h, 0.074f * h, 0.085f * h, -0.15f), seg + 4, seg, Part(Head, 0, 0), Vector2.Zero, u =>
        {
            float d = Mathf.Max(u.Dot(eyeDir), u.Dot(new Vector3(-eyeDir.X, eyeDir.Y, eyeDir.Z)));
            float socket = 0.11f * Mathf.Exp(-Mathf.Pow((1 - d) * 11f, 2));
            float brow = 0.05f * Mathf.SmoothStep(0.2f, 0.5f, u.Y) * Mathf.SmoothStep(0.0f, 0.45f, u.Z) * (1 - Mathf.SmoothStep(0.75f, 0.95f, u.Y));
            float clypeus = -0.045f * Mathf.SmoothStep(0.45f, 0.85f, u.Z) * Mathf.SmoothStep(-0.1f, -0.5f, u.Y);
            // the heart shape of a real ant head: broad across the upper cheeks, a notch at the back of the crown
            float cheeks = 0.05f * Mathf.SmoothStep(0.45f, 0.9f, Mathf.Abs(u.X)) * Mathf.SmoothStep(-0.4f, 0.3f, u.Y) * (1 - Mathf.SmoothStep(0.3f, 0.8f, u.Z));
            float notch = -0.06f * Mathf.SmoothStep(0.3f, 0.9f, -u.Z) * Mathf.SmoothStep(0.2f, 0.7f, u.Y) * (1 - Mathf.SmoothStep(0.05f, 0.35f, Mathf.Abs(u.X)));
            return 1 + brow - socket + clypeus + cheeks + notch;
        });
        k.SmoothNormals(hv, hi);
        // Eyes: big enough to read at RTS zoom, converged forward so both catch the camera from the three-quarter view
        for (int side = -1; side <= 1; side += 2)
            k.Ellipsoid(headC + new Vector3(side * 0.066f * h, 0.026f * h, 0.05f * h), new Basis(Vector3.Up, -side * 0.35f) * Scaled(0.027f * h, 0.029f * h, 0.025f * h, -0.1f), far ? 6 : 12, far ? 4 : 8, Part(Eye), Vector2.Zero);

        // Mandibles: two curved segments, pivot at the jaw hinge
        for (int side = -1; side <= 1; side += 2)
        {
            int id = side > 0 ? 9 : 10;
            float m = s.Mandible;
            var hinge = headC + new Vector3(side * 0.045f * h, -0.035f * h, 0.07f * h);
            var mid = hinge + new Vector3(side * 0.02f * m, -0.005f, 0.06f * m);
            var tip = mid + new Vector3(-side * 0.045f * m, -0.004f, 0.035f * m);
            var piv = new Vector2(hinge.X, hinge.Z);
            k.Loft(new[] { hinge, mid, tip }, new[] { 0.018f * m, 0.012f * m, 0.0025f }, new[] { 0.3f, 0.7f, 1f }, id, 0, far ? 4 : 6, piv, far ? 2 : 3);
            // every caste's jaws carry teeth along the cutting edge (the Warlord adds its own bigger set)
            if (!far && type != AntType.Warlord)
                for (int tooth = 1; tooth <= 2; tooth++)
                {
                    var at = mid.Lerp(tip, tooth * 0.3f);
                    k.Tube(at, at + new Vector3(-side * 0.014f * m, -0.003f, 0.006f), 0.005f * m, 0.001f, 4, Part(id, 0.8f), Part(id, 0.95f), piv);
                }
        }

        // Antennae: elbowed scape + funiculus with a club
        for (int side = -1; side <= 1; side += 2)
        {
            int id = side > 0 ? 7 : 8;
            var root = headC + new Vector3(side * 0.035f * h, 0.035f * h, 0.06f * h);
            var elbow = root + new Vector3(side * 0.09f, 0.1f, 0.02f);
            var tip = elbow + new Vector3(side * 0.05f, -0.02f, 0.2f);
            var piv = new Vector2(root.X, root.Z);
            // scape to the elbow, then a funiculus of beads that bends gently toward the tip and ends in a club
            var knots = new List<Vector3> { root, root.Lerp(elbow, 0.5f) + new Vector3(0, 0.005f, 0), elbow };
            var radii = new List<float> { 0.011f, 0.009f, 0.012f };
            var wts = new List<float> { 0.1f, 0.3f, 0.5f };
            for (int b = 1; b <= 5; b++)
            {
                float bt = b / 5f;
                knots.Add(elbow.Lerp(tip, bt) + new Vector3(side * 0.012f, 0.03f, 0) * Mathf.Sin(bt * Mathf.Pi));
                radii.Add(b == 5 ? 0.013f : b % 2 == 0 ? 0.0105f : 0.0075f);
                wts.Add(0.5f + 0.5f * bt);
            }
            knots.Add(tip + new Vector3(side * 0.01f, -0.01f, 0.035f));
            radii.Add(0.003f);
            wts.Add(1f);
            k.Loft(knots.ToArray(), radii.ToArray(), wts.ToArray(), id, 0, far ? 3 : 5, piv, far ? 1 : 2);
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
                // coxa swells at the hip, the femur bows up, a knee bulb, the tibia tapers to an ankle bulb, a whip of tarsus
                var femur = hip.Lerp(knee, 0.5f) + new Vector3(side * 0.01f, 0.012f, 0);
                var tibia = knee.Lerp(ankle, 0.5f) + new Vector3(side * 0.012f, -0.004f, 0);
                var tarsus = ankle.Lerp(foot, 0.5f) + new Vector3(0, -0.006f, 0);
                k.Loft(new[] { hip, femur, knee, tibia, ankle, tarsus, foot },
                    new[] { 0.021f * t, 0.016f, 0.019f, 0.011f, 0.012f, 0.0065f, 0.0025f },
                    new[] { 0f, 0.25f, 0.45f, 0.7f, 0.9f, 0.95f, 1f }, id, 0, sides, piv, sub);
                // a pair of pretarsal claws, splayed, hooking the ground
                if (!far)
                    for (int cl = -1; cl <= 1; cl += 2)
                        k.Tube(foot, foot + new Vector3(side * 0.004f + cl * 0.006f, -0.012f, reach[i] * 0.05f + 0.01f), 0.0028f, 0.0006f, 3, Part(id, 1f), Part(id, 1f), piv);
            }
        if (type == AntType.Queen && !far)
        {
            // the scars where her wings were shed: two dark pads either side of the flight thorax
            for (int side = -1; side <= 1; side += 2)
                k.Ellipsoid(new Vector3(side * 0.045f * t, bodyY + 0.055f * t, 0.065f), Scaled(0.03f, 0.012f, 0.045f, 0.2f), 10, 5, Part(Plate, 0, 0.5f), Vector2.Zero,
                    u => u.Y < -0.1f ? 0.4f : 1f);
        }
        if (Config.IsHero(type))
        {
            HeroRegalia(k, type, s, headC, bodyY, gz);
            if (far) goto bake;
            // a brush of setae over the gaster and thorax: close-up texture a hero earns
            var hair = new Rng((uint)type * 31 + 5);
            for (int i = 0; i < 28; i++)
            {
                bool onGaster = i < 20;
                float ux = hair.R(-0.8f, 0.8f), uz = hair.R(-0.75f, 0.75f);
                float uy = Mathf.Sqrt(Mathf.Max(0.05f, 1 - ux * ux - uz * uz));
                var at = onGaster
                    ? new Vector3(ux * 0.125f * s.Gaster, bodyY + 0.01f + uy * 0.105f * s.Gaster, gz + uz * 0.17f * s.Gaster)
                    : new Vector3(ux * 0.05f * s.Thorax, bodyY + 0.01f + uy * 0.058f * s.Thorax, 0.02f + uz * 0.1f * s.Thorax);
                var dir = new Vector3(ux * 0.6f, 1f, -0.5f).Normalized() * (onGaster ? 0.045f : 0.03f);
                k.Tube(at, at + dir, 0.0032f, 0.0006f, 3, Part(onGaster ? Gaster : Body, 0, onGaster ? 1 : 0.5f), Part(onGaster ? Gaster : Body, 0, onGaster ? 1 : 0.5f), Vector2.Zero);
            }
        }
        bake:
        // baked occlusion under the three masses of the body
        k.BakeAO(new (Vector3, float)[]
        {
            (new Vector3(0, bodyY + 0.01f * s.Gaster, gz), 0.14f * s.Gaster),
            (new Vector3(0, bodyY + 0.01f, 0.02f), 0.085f * s.Thorax),
            (headC, 0.08f * s.Head),
        });
        return k.Commit();
    }

    /// What makes a hero read as a hero at RTS zoom: a silhouette no ordinary caste has, in metal.
    static void HeroRegalia(MeshKit k, AntType type, Shape s, Vector3 headC, float bodyY, float gz)
    {
        float h = s.Head, t = s.Thorax, g = s.Gaster;
        switch (type)
        {
            case AntType.Warlord:
            {
                // a crest of five spikes down the crown, swept back
                for (int i = 0; i < 5; i++)
                {
                    float f = i / 4f;
                    var root = headC + new Vector3(0, 0.055f * h + 0.01f * (1 - f), (0.05f - 0.1f * f) * h);
                    var tip = root + new Vector3(0, 0.055f + 0.02f * (1 - Mathf.Abs(f - 0.5f) * 2), -0.04f);
                    k.Tube(root, tip, 0.012f, 0.002f, 5, Tag(Crest, 0.2f, 0), Tag(Crest, 1f, 0), Vector2.Zero);
                }
                // pronotal shoulder spines
                for (int side = -1; side <= 1; side += 2)
                {
                    var root = new Vector3(side * 0.04f * t, bodyY + 0.055f * t, 0.065f);
                    var tip = root + new Vector3(side * 0.07f, 0.075f, -0.07f);
                    k.Tube(root, tip, 0.016f, 0.003f, 6, Tag(Crest, 0.2f, 0.5f), Tag(Crest, 1f, 0.5f), Vector2.Zero);
                }
                // three overlapping armour plates ride the gaster, each with a metal rim
                for (int i = 0; i < 3; i++)
                {
                    float z = gz + (0.11f - 0.1f * i) * g;
                    k.Ellipsoid(new Vector3(0, bodyY + 0.05f * g + 0.01f * (1 - i * 0.3f), z), Scaled(0.135f * g, 0.045f * g, 0.075f * g, -0.25f), 16, 6, Tag(Plate, 0, 1), Vector2.Zero,
                        u => u.Y < 0 ? 0.35f : 1f); // only the upper half shows
                }
                // serrated mandibles: teeth along the inner edge
                for (int side = -1; side <= 1; side += 2)
                {
                    int id = side > 0 ? 9 : 10;
                    float m = s.Mandible;
                    var hinge = headC + new Vector3(side * 0.045f * h, -0.035f * h, 0.07f * h);
                    var mid = hinge + new Vector3(side * 0.02f * m, -0.005f, 0.06f * m);
                    var piv = new Vector2(hinge.X, hinge.Z);
                    for (int tooth = 1; tooth <= 3; tooth++)
                    {
                        var at = hinge.Lerp(mid, tooth / 3.5f);
                        k.Tube(at, at + new Vector3(-side * 0.022f, -0.004f, 0.012f), 0.007f, 0.001f, 4, Tag(id, 0.8f), Tag(id, 1f), piv);
                    }
                }
                break;
            }
            case AntType.Matron:
            {
                // acidopore: the spray nozzle at the tip of the gaster, ringed in metal
                var tipC = new Vector3(0, bodyY - 0.005f, gz - 0.175f * g);
                k.Tube(tipC + new Vector3(0, 0, 0.03f), tipC + new Vector3(0, -0.02f, -0.075f), 0.03f, 0.012f, 8, Tag(Crest, 0.1f, 1), Tag(Crest, 0.9f, 1), Vector2.Zero, false);
                k.Tube(tipC + new Vector3(0, -0.02f, -0.075f), tipC + new Vector3(0, -0.03f, -0.105f), 0.012f, 0.007f, 6, Tag(Eye, 0, 1), Tag(Eye, 0, 1), Vector2.Zero);
                // studded gland line along each flank of the sac
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 4; i++)
                    {
                        float z = gz + (0.1f - 0.065f * i) * g;
                        k.Ellipsoid(new Vector3(side * 0.12f * g, bodyY + 0.025f * g, z), Scaled(0.016f, 0.016f, 0.016f, 0), 6, 4, Tag(Crest, 0, 1), Vector2.Zero);
                    }
                // a radial ruff of gold points behind the head, like a sun collar, over a plate base
                k.Ellipsoid(new Vector3(0, bodyY + 0.035f, 0.125f), Scaled(0.1f, 0.018f, 0.04f, 0.35f), 16, 6, Tag(Plate, 0, 0.5f), Vector2.Zero,
                    u => u.Y < -0.2f ? 0.3f : 1f);
                for (int r = 0; r < 11; r++)
                {
                    float ang = Mathf.Pi * (0.08f + 0.84f * r / 10f); // a fan over the top half
                    var dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang) * 0.9f, -0.35f).Normalized();
                    var root = new Vector3(0, bodyY + 0.035f, 0.125f) + dir * 0.045f;
                    k.Tube(root, root + dir * 0.055f, 0.011f, 0.0015f, 4, Tag(Crest, 0.2f, 0.5f), Tag(Crest, 1f, 0.5f), Vector2.Zero);
                }
                // a single tall crown spike
                k.Tube(headC + new Vector3(0, 0.06f * h, -0.01f), headC + new Vector3(0, 0.2f * h, -0.07f), 0.014f, 0.002f, 6, Tag(Crest, 0.2f, 0), Tag(Crest, 1f, 0), Vector2.Zero);
                break;
            }
            case AntType.Marshal:
            {
                // a plume of three backswept quills on the flight thorax
                for (int i = -1; i <= 1; i++)
                {
                    var root = new Vector3(i * 0.018f, bodyY + 0.085f * t, 0.075f - Mathf.Abs(i) * 0.015f);
                    var tip = root + new Vector3(i * 0.03f, 0.1f - Mathf.Abs(i) * 0.02f, -0.13f);
                    k.Tube(root, tip, 0.011f, 0.002f, 5, Tag(Crest, 0.2f, 0.5f), Tag(Crest, 1f, 0.5f), Vector2.Zero);
                }
                // gold epaulettes capping the wing roots, ribbed like a drum
                for (int side = -1; side <= 1; side += 2)
                    k.Ellipsoid(new Vector3(side * 0.055f, bodyY + 0.07f, 0.075f), Scaled(0.038f, 0.022f, 0.048f, 0), 12, 5, Tag(Crest, 0.6f, 0.5f), Vector2.Zero,
                        u => (u.Y < -0.1f ? 0.4f : 1f) * (1 + 0.04f * Mathf.Sin(u.Z * 18f)));
                // twin streamers trailing from the gaster tip, like a marshal's pennants
                for (int side = -1; side <= 1; side += 2)
                {
                    var root = new Vector3(side * 0.012f, bodyY + 0.005f, gz - 0.17f * g);
                    var tip = root + new Vector3(side * 0.035f, 0.025f, -0.2f);
                    k.Tube(root, tip, 0.008f, 0.002f, 4, Tag(Crest, 0.2f, 1), Tag(Crest, 1f, 1), Vector2.Zero);
                }
                // a brow ridge over the eyes
                k.Ellipsoid(headC + new Vector3(0, 0.05f * h, 0.03f * h), Scaled(0.075f * h, 0.012f * h, 0.03f * h, 0.3f), 12, 4, Tag(Plate, 0, 0), Vector2.Zero);
                break;
            }
        }
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
        Neck[stag ? AntType.StagBeetle : AntType.Beetle] = headC + new Vector3(0, 0, -0.07f);
        k.Ellipsoid(headC, Scaled(stag ? 0.17f : 0.095f, stag ? 0.08f : 0.062f, stag ? 0.1f : 0.075f, 0), 16, 10, Tag(Head), Vector2.Zero);
        for (int side = -1; side <= 1; side += 2)
            k.Ellipsoid(headC + new Vector3(side * (stag ? 0.125f : 0.082f), 0.024f, 0.03f), Scaled(0.03f, 0.03f, 0.028f, 0), 10, 7, Tag(Eye), Vector2.Zero);
        if (stag)
        {
            // a raised head shield and a pronotal crest: the lord of the yard wears its armour on top
            k.Ellipsoid(headC + new Vector3(0, 0.055f, -0.01f), Scaled(0.15f, 0.022f, 0.085f, 0.1f), 16, 5, Tag(Plate, 0, 0), Vector2.Zero, u => u.Y < -0.2f ? 0.3f : 1f);
            k.Ellipsoid(new Vector3(0, y + 0.1f, 0.19f), Scaled(0.145f, 0.025f, 0.085f, 0.12f), 16, 5, Tag(Plate, 0, 0.5f), Vector2.Zero, u => u.Y < -0.2f ? 0.3f : 1f);
            for (int side = -1; side <= 1; side += 2) // horn-like corners on the pronotum
                k.Tube(new Vector3(side * 0.12f, y + 0.09f, 0.22f), new Vector3(side * 0.17f, y + 0.14f, 0.26f), 0.018f, 0.003f, 5, Tag(Crest, 0.2f, 0.5f), Tag(Crest, 1f, 0.5f), Vector2.Zero);
        }
        else
        {
            // flared pronotum margins, the ground beetle's shield
            for (int side = -1; side <= 1; side += 2)
                k.Ellipsoid(new Vector3(side * 0.15f, y + 0.035f, 0.19f), Scaled(0.03f, 0.015f, 0.095f, 0.12f), 10, 5, Tag(Plate, 0, 0.5f), Vector2.Zero);
        }
        // mandibles
        for (int side = -1; side <= 1; side += 2)
        {
            int id = side > 0 ? 9 : 10;
            var hinge = headC + new Vector3(side * 0.06f, -0.01f, 0.05f);
            var piv = new Vector2(hinge.X, hinge.Z);
            if (stag)
            {
                // great curved antler-jaws: a quarter of the body length, forked, with inner teeth
                var a = hinge + new Vector3(side * 0.15f, 0.08f, 0.2f);
                var b = a + new Vector3(-side * 0.02f, 0.06f, 0.24f);
                var tip = b + new Vector3(-side * 0.15f, 0.01f, 0.09f);
                k.Ellipsoid(hinge, Scaled(0.055f, 0.05f, 0.055f, 0), 10, 8, Tag(id, 0.3f), piv);
                k.Tube(hinge, a, 0.054f, 0.046f, 10, Tag(id, 0.3f), Tag(id, 0.6f), piv, false);
                k.Ellipsoid(a, Scaled(0.046f, 0.046f, 0.046f, 0), 10, 8, Tag(id, 0.6f), piv);
                k.Tube(a, b, 0.046f, 0.034f, 10, Tag(id, 0.6f), Tag(id, 0.85f), piv, false);
                k.Ellipsoid(b, Scaled(0.034f, 0.034f, 0.034f, 0), 10, 8, Tag(id, 0.85f), piv);
                k.Tube(b, tip, 0.034f, 0.008f, 8, Tag(id, 0.85f), Tag(id, 1f), piv);
                k.Tube(b, b + new Vector3(side * 0.025f, 0.075f, 0.06f), 0.02f, 0.004f, 6, Tag(id, 0.9f), Tag(id, 1f), piv); // fork
                for (int tooth = 1; tooth <= 3; tooth++)
                {
                    var at = a.Lerp(b, tooth * 0.25f);
                    k.Tube(at, at + new Vector3(-side * 0.08f, -0.01f, 0.01f), 0.018f, 0.003f, 6, Tag(id, 0.7f), Tag(id, 0.75f), piv);
                }
            }
            else
            {
                // sickle jaws that cross at the tips
                var mid = hinge + new Vector3(side * 0.015f, -0.005f, 0.06f);
                var tip = mid + new Vector3(-side * 0.045f, -0.01f, 0.05f);
                k.Tube(hinge, mid, 0.026f, 0.018f, 6, Tag(id, 0.3f), Tag(id, 0.7f), piv, false);
                k.Tube(mid, tip, 0.018f, 0.004f, 6, Tag(id, 0.7f), Tag(id, 1f), piv);
                var at = hinge.Lerp(mid, 0.5f);
                k.Tube(at, at + new Vector3(-side * 0.03f, -0.005f, 0.01f), 0.009f, 0.002f, 4, Tag(id, 0.6f), Tag(id, 0.7f), piv);
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
        // legs: sturdy, spiny; the stag's thicker still
        float lm = stag ? 1.2f : 1f;
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
                // stout femur, flattened spiny tibia, segmented tarsus: one bowed limb with a bulb at every joint
                k.Loft(new[] { hip, (hip + knee) * 0.5f + Vector3.Up * 0.02f, knee, knee.Lerp(ankle, 0.5f) + new Vector3(side * 0.01f, 0, 0), ankle },
                    new[] { 0.034f * lm, 0.043f * lm, 0.032f * lm, 0.024f * lm, 0.019f * lm },
                    new[] { 0f, 0.25f, 0.5f, 0.7f, 0.85f }, id, 0, 8, piv, 2, false);
                for (int sp = 1; sp <= 3; sp++)
                {
                    var at = knee.Lerp(ankle, sp / 4f);
                    k.Tube(at, at + new Vector3(side * 0.035f, -0.02f, 0.01f), 0.007f, 0.001f, 4, Tag(id, 0.6f), Tag(id, 0.7f), piv);
                }
                var mid = ankle.Lerp(foot, 0.5f);
                k.Tube(ankle, mid, 0.014f, 0.012f, 5, Tag(id, 0.85f), Tag(id, 0.93f), piv, false);
                k.Tube(mid, foot, 0.012f, 0.005f, 5, Tag(id, 0.93f), Tag(id, 1f), piv);
            }
        k.BakeAO(new (Vector3, float)[] { (new Vector3(0, y + 0.03f, -0.15f), 0.2f), (new Vector3(0, y + 0.03f, 0.19f), 0.12f), (headC, stag ? 0.12f : 0.085f) });
        return k.Commit();
    }

    /// Wolf spider: eight long legs, big abdomen, eye cluster. Faces +Z.
    static ArrayMesh Spider()
    {
        var k = new MeshKit();
        float y = 0.2f;
        k.Ellipsoid(new Vector3(0, y + 0.04f, -0.24f), Scaled(0.17f, 0.14f, 0.22f, -0.1f), 24, 16, Tag(Gaster, 0, 1), Vector2.Zero);
        k.Ellipsoid(new Vector3(0, y + 0.0f, -0.07f), Scaled(0.04f, 0.04f, 0.04f, 0), 8, 6, Tag(Body), Vector2.Zero); // pedicel
        // spinnerets: two pairs of pale tubes at the tip of the abdomen
        for (int s = -1; s <= 1; s += 2)
        {
            k.Tube(new Vector3(s * 0.025f, y - 0.01f, -0.44f), new Vector3(s * 0.045f, y - 0.05f, -0.51f), 0.018f, 0.007f, 6, Tag(Crest, 0.2f, 1), Tag(Crest, 1f, 1), Vector2.Zero);
            k.Tube(new Vector3(s * 0.012f, y - 0.03f, -0.44f), new Vector3(s * 0.015f, y - 0.07f, -0.49f), 0.013f, 0.005f, 5, Tag(Crest, 0.2f, 1), Tag(Crest, 1f, 1), Vector2.Zero);
        }
        // a brush of bristles over the abdomen
        var hairRng = new Rng(17);
        for (int i = 0; i < 26; i++)
        {
            float u = hairRng.R(-1f, 1f), v = hairRng.R(0.1f, 1f);
            var at = new Vector3(u * 0.15f, y + 0.04f + v * 0.12f, -0.24f + hairRng.R(-0.2f, 0.2f));
            k.Tube(at, at + new Vector3(u * 0.03f, 0.04f, -0.01f), 0.004f, 0.001f, 3, Tag(Gaster, 0, 1), Tag(Gaster, 0, 1), Vector2.Zero);
        }
        var ceph = new Vector3(0, y + 0.02f, 0.06f);
        Neck[AntType.Spider] = ceph + new Vector3(0, 0, 0.06f);
        k.Ellipsoid(ceph, Scaled(0.12f, 0.085f, 0.14f, 0.05f), 18, 10, Tag(Head), Vector2.Zero);
        for (int i = 0; i < 18; i++)
        {
            float u = hairRng.R(-1f, 1f), v = hairRng.R(0.2f, 1f), zz = hairRng.R(-0.8f, 0.6f);
            var at = ceph + new Vector3(u * 0.11f, v * 0.08f, zz * 0.13f);
            k.Tube(at, at + new Vector3(u * 0.02f, 0.035f, -0.01f), 0.0035f, 0.0008f, 3, Tag(Head), Tag(Head), Vector2.Zero);
        }
        // eyes: two big forward eyes, a row of four small ones below, and the two large rear eyes on top
        k.Ellipsoid(ceph + new Vector3(0.035f, 0.06f, 0.11f), Scaled(0.03f, 0.03f, 0.03f, 0), 8, 6, Tag(Eye), Vector2.Zero);
        k.Ellipsoid(ceph + new Vector3(-0.035f, 0.06f, 0.11f), Scaled(0.03f, 0.03f, 0.03f, 0), 8, 6, Tag(Eye), Vector2.Zero);
        for (int e = -2; e <= 1; e++)
            k.Ellipsoid(ceph + new Vector3((e + 0.5f) * 0.03f, 0.03f, 0.135f), Scaled(0.012f, 0.012f, 0.012f, 0), 6, 4, Tag(Eye), Vector2.Zero);
        for (int side = -1; side <= 1; side += 2)
            k.Ellipsoid(ceph + new Vector3(side * 0.06f, 0.08f, 0.06f), Scaled(0.02f, 0.02f, 0.02f, 0), 6, 4, Tag(Eye), Vector2.Zero);
        // chelicerae with the fangs folded under them, and the pedipalps
        for (int side = -1; side <= 1; side += 2)
        {
            int id = side > 0 ? 9 : 10;
            var root = ceph + new Vector3(side * 0.032f, -0.02f, 0.13f);
            var piv = new Vector2(root.X, root.Z);
            var end = root + new Vector3(0, -0.085f, 0.035f);
            k.Tube(root, end, 0.03f, 0.022f, 7, Tag(id, 0.3f), Tag(id, 0.8f), piv, false);
            k.Ellipsoid(end, Scaled(0.022f, 0.02f, 0.022f, 0), 7, 5, Tag(id, 0.8f), piv);
            k.Tube(end, end + new Vector3(-side * 0.03f, -0.035f, 0.015f), 0.011f, 0.002f, 5, Tag(Eye, 0, 0), Tag(Eye, 0, 0), piv); // fang
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
                // bristles along the leg, femur and tibia both
                for (int sp = 1; sp <= 4; sp++)
                {
                    var at = knee.Lerp(ankle, sp / 5f);
                    k.Tube(at, at + outDir * 0.02f + new Vector3(0, 0.035f, 0), 0.005f, 0.001f, 3, Tag(id, 0.6f), Tag(id, 0.65f), piv);
                    var fa = hip.Lerp(knee, sp / 5f);
                    k.Tube(fa, fa + outDir * 0.015f + new Vector3(0, 0.04f, 0), 0.005f, 0.001f, 3, Tag(id, 0.2f), Tag(id, 0.3f), piv);
                }
            }
        k.BakeAO(new (Vector3, float)[] { (new Vector3(0, y + 0.04f, -0.24f), 0.18f), (ceph, 0.12f) });
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
        // the sting: a long dark lance that curls under with the gaster when it strikes
        k.Tube(new Vector3(0, y - 0.1f, -0.47f), new Vector3(0, y - 0.17f, -0.62f), 0.014f, 0.002f, 5, Tag(Eye, 0, 1), Tag(Eye, 0, 1), Vector2.Zero);
        // petiole: the famous wasp waist
        k.Tube(new Vector3(0, y + 0.01f, -0.03f), new Vector3(0, y, -0.09f), 0.016f, 0.02f, 6, Tag(Body), Tag(Body), Vector2.Zero, false);
        // thorax, with the scutellum ridge
        k.Ellipsoid(new Vector3(0, y + 0.03f, 0.05f), Scaled(0.085f, 0.085f, 0.11f, 0.05f), 18, 12, Tag(Body, 0, 0.5f), Vector2.Zero);
        k.Ellipsoid(new Vector3(0, y + 0.1f, 0.0f), Scaled(0.05f, 0.015f, 0.04f, 0.2f), 10, 4, Tag(Crest, 0, 0.5f), Vector2.Zero, u => u.Y < -0.2f ? 0.3f : 1f);
        // head with huge kidney-shaped compound eyes and three ocelli on the crown
        var headC = new Vector3(0, y + 0.03f, 0.2f);
        Neck[AntType.Wasp] = headC + new Vector3(0, -0.01f, -0.05f);
        k.Ellipsoid(headC, Scaled(0.085f, 0.08f, 0.058f, 0.2f), 16, 10, Tag(Head), Vector2.Zero);
        for (int o = -1; o <= 1; o++)
            k.Ellipsoid(headC + new Vector3(o * 0.02f, 0.072f - Mathf.Abs(o) * 0.008f, -0.01f + Mathf.Abs(o) * 0.012f), Scaled(0.009f, 0.009f, 0.009f, 0), 6, 4, Tag(Eye), Vector2.Zero);
        for (int side = -1; side <= 1; side += 2)
        {
            k.Ellipsoid(headC + new Vector3(side * 0.06f, 0.012f, -0.005f), Scaled(0.032f, 0.06f, 0.042f, 0.2f), 10, 8, Tag(Eye), Vector2.Zero);
            int ma = side > 0 ? 9 : 10;
            var hinge = headC + new Vector3(side * 0.032f, -0.06f, 0.03f);
            k.Tube(hinge, hinge + new Vector3(-side * 0.03f, -0.035f, 0.04f), 0.018f, 0.004f, 5, Tag(ma, 0.3f), Tag(ma, 1f), new Vector2(hinge.X, hinge.Z));
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
                k.Loft(new[] { hip, hip.Lerp(knee, 0.5f) + new Vector3(side * 0.008f, 0.01f, 0), knee, knee.Lerp(foot, 0.5f) + new Vector3(side * 0.006f, 0, 0), foot },
                    new[] { 0.019f, 0.016f, 0.018f, 0.011f, 0.005f }, new[] { 0f, 0.25f, 0.5f, 0.8f, 1f }, id, 0, 6, piv, 2);
                k.Tube(foot, foot + new Vector3(side * 0.012f, -0.02f, 0.015f), 0.005f, 0.001f, 4, Tag(id, 1f), Tag(id, 1f), piv); // tarsal claw
            }
        k.BakeAO(new (Vector3, float)[] { (new Vector3(0, y - 0.02f, -0.27f), 0.14f), (new Vector3(0, y + 0.03f, 0.05f), 0.095f), (headC, 0.075f) });
        return k.Commit();
    }

    /// Membranous wings (separate translucent batch). R = 1 left / 2 right, G = 0 hinge..1 tip,
    /// UV = venation coordinates, UV2 = hinge (x, y) for the flap rotation about the body axis.
    public static ArrayMesh Wings(AntType type)
    {
        var k = new MeshKit();
        bool wasp = type == AntType.Wasp;
        // (hinge z, length, width, sweep angle) for fore and hind wing; the marshal's reach a third further
        var pairs = wasp
             ? new[] { (0.09f, 0.48f, 0.12f, 0.35f), (0.03f, 0.34f, 0.09f, 0.55f) }
            : type == AntType.Marshal
             ? new[] { (0.09f, 0.82f, 0.17f, 0.2f), (0.04f, 0.6f, 0.13f, 0.36f) }
            : new[] { (0.09f, 0.62f, 0.14f, 0.25f), (0.04f, 0.46f, 0.11f, 0.4f) };
        float hy = wasp ? 0.3f : type == AntType.Marshal ? 0.25f : 0.22f;
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
