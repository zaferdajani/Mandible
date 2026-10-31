using Godot;

namespace Mandible;

/// Food-trail pheromone per colony. Returning foragers lay it, searching foragers follow it.
public sealed class Pheromones
{
    public const float Cell = 2f;
    public readonly int N;
    public readonly float[][] Grid = new float[2][];
    int _ticks;

    public Pheromones()
    {
        N = (int)(Config.MapSize / Cell);
        Grid[0] = new float[N * N];
        Grid[1] = new float[N * N];
    }

    public void Deposit(int team, Vector2 p, float amount)
    {
        int x = (int)(p.X / Cell), z = (int)(p.Y / Cell);
        if (x < 0 || z < 0 || x >= N || z >= N) return;
        ref float v = ref Grid[team][z * N + x];
        v = Mathf.Min(v + amount, 12f);
    }

    public float Sample(int team, Vector2 p)
    {
        int x = (int)(p.X / Cell), z = (int)(p.Y / Cell);
        if (x < 0 || z < 0 || x >= N || z >= N) return 0;
        return Grid[team][z * N + x];
    }

    public void Update(float dt)
    {
        // fixed 30 Hz ticks: decay every 8 ticks by exp(-(8/30)/55), written as a literal so every platform agrees
        if (++_ticks % 8 != 0) return;
        const float k = 0.99516326f;
        for (int t = 0; t < 2; t++)
        {
            var g = Grid[t];
            for (int i = 0; i < g.Length; i++)
            {
                float v = g[i] * k - 0.0015f;
                g[i] = v > 0 ? v : 0;
            }
        }
    }

    public void Clear(int team, Vector2 p, float r)
    {
        int r2 = (int)(r / Cell) + 1;
        int cx = (int)(p.X / Cell), cz = (int)(p.Y / Cell);
        for (int z = cz - r2; z <= cz + r2; z++)
            for (int x = cx - r2; x <= cx + r2; x++)
                if (x >= 0 && z >= 0 && x < N && z < N) Grid[team][z * N + x] *= 0.3f;
    }
}

/// Uniform grid of ant indices, rebuilt every tick.
public sealed class SpatialHash
{
    public const float Cell = 4f;
    public readonly int N;
    public readonly int[] Head;
    public int[] Next;

    public SpatialHash(int capacity)
    {
        N = (int)(Config.MapSize / Cell) + 1;
        Head = new int[N * N];
        Next = new int[capacity];
    }

    public void Clear() { System.Array.Fill(Head, -1); }

    public int CellOf(Vector2 p)
    {
        int x = Mathf.Clamp((int)(p.X / Cell), 0, N - 1), z = Mathf.Clamp((int)(p.Y / Cell), 0, N - 1);
        return z * N + x;
    }

    public void Insert(int id, Vector2 p)
    {
        int c = CellOf(p);
        Next[id] = Head[c];
        Head[c] = id;
    }
}
