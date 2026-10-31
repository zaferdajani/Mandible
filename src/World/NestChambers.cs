using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Side chambers dug around a colony's mound as research completes: a small crater with
/// a telltale pile at its mouth (royal jelly, shed wings, mandible shards, chitin plates, silk).
public sealed class NestChambers
{
    readonly World _w;
    readonly Node3D _root;
    readonly Dictionary<(int team, Upgrade u, int tier), Node3D> _built = new();
    public readonly List<(int team, Node3D node, Vector2 pos)> All = new();

    public NestChambers(World w, Node3D parent)
    {
        _w = w;
        _root = new Node3D { Name = "Chambers" };
        parent.AddChild(_root);
    }

    static float Angle(Upgrade u, int tier) => u switch
    {
        Upgrade.Queen => tier == 2 ? 0.0f : 0.5f,
        Upgrade.Wings => 1.6f,
        Upgrade.Mandibles => 2.5f + 0.35f * (tier - 1),
        Upgrade.Chitin => 3.9f + 0.35f * (tier - 1),
        _ => 5.3f,
    };

    public Vector2 Build(int team, Upgrade u, int tier)
    {
        if (_built.ContainsKey((team, u, tier))) return Vector2.Zero;
        var nest = _w.Nests[team];
        // spread the chambers on the side facing away from the map centre
        var away = (nest.Pos - new Vector2(Config.MapSize, Config.MapSize) * 0.5f).Angle();
        var dir = MathX.Dir(away + (Angle(u, tier) - 3f) * 0.75f);
        var p = nest.Pos + dir * (nest.Radius + 6f);
        var hf = _w.HF;
        var rng = new Rng((uint)(team * 97 + (int)u * 13 + tier * 7 + 1));
        var node = new Node3D { Name = $"Chamber_{team}_{u}_{tier}" };
        _root.AddChild(node);
        float gy(Vector2 q) => hf.Sample(q.X, q.Y);

        // excavated rim of grains
        var grains = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = PebbleMesh() };
        grains.InstanceCount = 220;
        for (int i = 0; i < 220; i++)
        {
            float a = rng.R(0, Mathf.Tau), r = 1.4f + Mathf.Abs(rng.R(-1, 1) + rng.R(-1, 1)) * 1.3f;
            var q = p + MathX.Dir(a) * r;
            float s = rng.R(0.1f, 0.3f);
            grains.SetInstanceTransform(i, new Transform3D(new Basis(Vector3.Up, rng.R(0, 6)) * Basis.FromScale(Vector3.One * s), new Vector3(q.X, gy(q) + s * 0.25f, q.Y)));
            grains.SetInstanceCustomData(i, new Color(rng.R(0.5f, 1f), 0, rng.R(0, 0.3f), rng.F()));
        }
        var pebbleMat = new ShaderMaterial { Shader = Shaders.Get(Shaders.Pebble) };
        pebbleMat.SetShaderParameter("noise_b", _w.NoiseB);
        pebbleMat.SetShaderParameter("map_size", Config.MapSize);
        _w.RegisterFow(pebbleMat);
        node.AddChild(new MultiMeshInstance3D { Multimesh = grains, MaterialOverride = pebbleMat });

        // the dark mouth of the chamber
        var hole = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.75f, BottomRadius = 0.45f, Height = 0.08f, RadialSegments = 24 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.035f, 0.025f), Roughness = 1 },
            Position = new Vector3(p.X, gy(p) + 0.03f, p.Y),
        };
        node.AddChild(hole);

        // telltale pile
        Mesh item;
        Material mat;
        int count;
        float scale;
        switch (u)
        {
            case Upgrade.Queen:
                item = new SphereMesh { Radius = 0.5f, Height = 0.7f, RadialSegments = 16, Rings = 8 };
                mat = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.7f, 0.22f, 0.9f), Roughness = 0.04f, ClearcoatEnabled = true, Clearcoat = 1, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, RimEnabled = true, Rim = 0.4f, EmissionEnabled = true, Emission = new Color(0.25f, 0.15f, 0.02f) };
                count = tier == 3 ? 9 : 6; scale = 0.9f;
                break;
            case Upgrade.Wings:
                item = AntMesh.Wings(AntType.Flyer);
                var wm = new ShaderMaterial { Shader = Shaders.Get(Shaders.Wing) };
                mat = wm;
                count = 10; scale = 2.2f;
                break;
            case Upgrade.Mandibles:
                item = new CapsuleMesh { Radius = 0.08f, Height = 1.0f, RadialSegments = 8, Rings = 4 };
                mat = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.14f, 0.05f), Roughness = 0.2f, Metallic = 0.2f, ClearcoatEnabled = true, Clearcoat = 1 };
                count = 8 + tier * 3; scale = 1.1f;
                break;
            case Upgrade.Chitin:
                item = new SphereMesh { Radius = 0.5f, Height = 0.25f, RadialSegments = 14, Rings = 6 };
                mat = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.38f, 0.12f), Roughness = 0.15f, ClearcoatEnabled = true, Clearcoat = 1, Metallic = 0.1f, RimEnabled = true, Rim = 0.3f };
                count = 6 + tier * 2; scale = 0.9f;
                break;
            default:
                item = AntMesh.Silk();
                mat = new ShaderMaterial { Shader = Shaders.Get(Shaders.Silk) };
                count = 7; scale = 2.4f;
                break;
        }
        for (int i = 0; i < count; i++)
        {
            var q = p + MathX.Dir(rng.R(0, Mathf.Tau)) * rng.R(1.3f, 2.6f);
            var b = new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * new Basis(Vector3.Right, u == Upgrade.Mandibles ? Mathf.Pi / 2 : rng.R(-0.2f, 0.2f)) * Basis.FromScale(Vector3.One * scale * rng.R(0.7f, 1.2f));
            var mi = new MeshInstance3D { Mesh = item, MaterialOverride = mat, Transform = new Transform3D(b, new Vector3(q.X, gy(q) + 0.12f, q.Y)) };
            node.AddChild(mi);
        }
        // a name over each chamber, so the holes around the mound explain themselves
        node.AddChild(new Label3D
        {
            Text = u == Upgrade.Queen ? $"Royal Chamber · {Config.QueenTitles[System.Math.Min(5, tier)]}" : Config.ResearchName(u, tier), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
            FontSize = 44, OutlineSize = 12, PixelSize = 0.022f, Modulate = new Color(1f, 0.86f, 0.55f, 0.92f),
            OutlineModulate = new Color(0.05f, 0.03f, 0.02f, 0.85f), Font = UiTheme.Title,
            Position = new Vector3(p.X, gy(p) + 3.2f, p.Y),
        });
        _built[(team, u, tier)] = node;
        All.Add((team, node, p));
        return p;
    }

    static ArrayMesh _pebble;
    static ArrayMesh PebbleMesh()
    {
        if (_pebble != null) return _pebble;
        var k = new MeshKit();
        var n = new FastNoiseLite { Seed = 31, Frequency = 1.8f };
        k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(1, 0.65f, 0.85f)), 8, 6, new Color(1, 1, 1), Vector2.Zero, u => 1 + 0.25f * n.GetNoise3D(u.X, u.Y, u.Z));
        k.RecalcNormals();
        return _pebble = k.Commit();
    }

    /// Enemy chambers stay hidden until the area has been scouted.
    public void UpdateVisibility(FogOfWar fog)
    {
        if (fog == null) return;
        foreach (var (team, node, pos) in All)
            node.Visible = team == Local.Me || fog.Explored(pos);
    }
}
