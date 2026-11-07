using System.Collections.Generic;
using Godot;

namespace Mandible;

public sealed class Obstacle
{
    public Vector2 P;
    public float R;
    public float BaseR;
}

public enum FoodKind { Crumb = 0, Berry = 1, Sugar = 2, Seed = 3, Corpse = 4 }

public sealed class FoodSource
{
    public int Id;
    public FoodKind Kind;
    public Vector2 Pos;
    public float Amount, Max, Radius;
    public Node3D Node;
    public Obstacle Block;
    public Vector3 BaseScale = Vector3.One;
    // corpse data
    public int Team = Config.Wild; // whose body it was (a carcass with no owner counts as a wild bug)
    public AntType AntType;
    public float Heading;
    public float Age;
    // carcasses (Carrion.cs): where it lies, who drags it, what a feasting hero has taken from it
    public byte Layer;                 // 0 the surface, 1 + team: inside that colony's nest
    public bool Natural;               // left by a death, so it rots; the campaign's scripted carcass does not
    public int Haulers, HaulCount;     // workers gripping it this tick, and last tick's count
    public int HaulTeam;               // the colony whose workers have it in their jaws
    public bool Hauled;                // dragged all the way to its destination
    public int Feasted;                // loads a hero has eaten here
    // collection (Carrion.cs: Recruit): an auto-eat fighter's kill is kept for its eaters a while; workers skip it and
    // anything being eaten; Queued marks (one bit per colony) the remains its workers have been sent to bring home
    public int ClaimTeam;              // colony + 1 whose eaters have claimed it (0: none), until ClaimUntil
    public float ClaimUntil, LastFeast;
    public int Queued;
    public bool Alive => Amount > 0.01f;
}

/// A lair of wild bugs guarding a rich food spot.
public sealed class Camp
{
    public Vector2 Pos;
    public float Facing;
    public (AntType type, int level)[] Bugs;
}

public sealed class Nest
{
    public int Team;
    public Vector2 Pos;
    public float Radius;
}

public enum Scatter { Grass, Pebbles, Straw }

public sealed class World
{
    public bool FirstNest;
    public Heightfield HF;
    public readonly List<Obstacle> Obstacles = new();
    public readonly List<FoodSource> Food = new();
    public readonly Nest[] Nests = new Nest[2];
    public readonly List<Camp> Camps = new();
    public int NextFoodId;

    public FoodSource FoodById(int id)
    {
        foreach (var f in Food) if (f.Id == id) return f;
        return null;
    }
    public readonly List<ShaderMaterial> FowMaterials = new();
    public Node3D Root;
    public Image MinimapBase;
    public Texture2D NoiseA, NoiseB, NormalA, NormalB, NormalC;
    public ShaderMaterial TerrainMaterial;
    public readonly List<ShaderMaterial> TerrainMaterials = new(); // the yard and the skirt around it
    public readonly List<(MultiMeshInstance3D node, Scatter kind)> Scatter = new();

    /// Rendering-only knobs that follow the graphics tier: soil detail and which ground litter casts shadows.
    public void ApplyQuality()
    {
        foreach (var m in TerrainMaterials) m.SetShaderParameter("detail", Settings.TerrainDetail);
        foreach (var (node, kind) in Scatter)
        {
            bool on = kind switch { Mandible.Scatter.Grass => Settings.GrassShadows, Mandible.Scatter.Pebbles => Settings.PebbleShadows, _ => Settings.StrawShadows };
            node.CastShadow = on ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
        }
    }

    // coarse bucket grid for obstacle queries
    const float Bucket = 16f;
    List<Obstacle>[] _buckets;
    int _bn;

    public void IndexObstacles()
    {
        _bn = (int)(Config.MapSize / Bucket) + 1;
        _buckets = new List<Obstacle>[_bn * _bn];
        for (int i = 0; i < _buckets.Length; i++) _buckets[i] = new List<Obstacle>();
        foreach (var o in Obstacles) Bucketize(o);
    }

    void Bucketize(Obstacle o)
    {
        float r = o.R + 4f; // padding so ants near a bucket edge still see the rock
        int x0 = Mathf.Clamp((int)((o.P.X - r) / Bucket), 0, _bn - 1), x1 = Mathf.Clamp((int)((o.P.X + r) / Bucket), 0, _bn - 1);
        int z0 = Mathf.Clamp((int)((o.P.Y - r) / Bucket), 0, _bn - 1), z1 = Mathf.Clamp((int)((o.P.Y + r) / Bucket), 0, _bn - 1);
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
                _buckets[z * _bn + x].Add(o);
    }

    public void AddObstacle(Obstacle o)
    {
        Obstacles.Add(o);
        if (_buckets != null) Bucketize(o);
    }

    public List<Obstacle> ObstaclesNear(Vector2 p)
    {
        int x = Mathf.Clamp((int)(p.X / Bucket), 0, _bn - 1), z = Mathf.Clamp((int)(p.Y / Bucket), 0, _bn - 1);
        return _buckets[z * _bn + x];
    }

    public bool Blocked(Vector2 p, float pad)
    {
        foreach (var o in ObstaclesNear(p))
            if (p.DistanceSquaredTo(o.P) < (o.R + pad) * (o.R + pad)) return true;
        return false;
    }

    public Texture2D FowTexture;

    public void RegisterFow(ShaderMaterial m)
    {
        FowMaterials.Add(m);
        if (FowTexture != null) m.SetShaderParameter("fow_tex", FowTexture);
    }
}
