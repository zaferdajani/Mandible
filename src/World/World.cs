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
    public int Team;
    public AntType AntType;
    public float Heading;
    public float Age;
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

public sealed class World
{
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
    public Texture2D NoiseA, NoiseB, NormalA, NormalB;
    public ShaderMaterial TerrainMaterial;

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
