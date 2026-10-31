using System;
using Godot;

namespace Mandible;

/// Walkable ground: base soil, nest mounds and fallen leaves all live in one grid so ants,
/// props and the terrain mesh agree on where the surface is.
public sealed class Heightfield
{
    public readonly int N;          // vertices per side
    public readonly float Cell;     // world units per cell
    public readonly float[] H;
    public readonly float[] Mound;  // 0..1 excavated soil / thatch mask (terrain tint)
    public readonly float[] Leaf;   // 0..1 covered by a leaf
    public float[] Ground;          // bare soil surface (terrain mesh), before leaves/twigs raise the walkable height

    public void SnapshotGround() { Ground = (float[])H.Clone(); }

    public Heightfield(float size, float cell)
    {
        Cell = cell;
        N = (int)(size / cell) + 1;
        H = new float[N * N];
        Mound = new float[N * N];
        Leaf = new float[N * N];
    }

    public float WorldOf(int i) => i * Cell;

    public float Sample(float x, float z) => Bilinear(H, x, z);
    public float SampleBase(float x, float z) => Bilinear(H, x, z);

    public float Bilinear(float[] a, float x, float z)
    {
        float fx = Mathf.Clamp(x / Cell, 0, N - 1.001f);
        float fz = Mathf.Clamp(z / Cell, 0, N - 1.001f);
        int ix = (int)fx, iz = (int)fz;
        float tx = fx - ix, tz = fz - iz;
        int i = iz * N + ix;
        float a0 = a[i] + (a[i + 1] - a[i]) * tx;
        float a1 = a[i + N] + (a[i + N + 1] - a[i + N]) * tx;
        return a0 + (a1 - a0) * tz;
    }

    public Vector3 Normal(float x, float z)
    {
        float e = Cell;
        float hl = Sample(x - e, z), hr = Sample(x + e, z);
        float hd = Sample(x, z - e), hu = Sample(x, z + e);
        return new Vector3(hl - hr, 2 * e, hd - hu).Normalized();
    }

    public Vector3 Point(Vector2 p, float lift = 0) => new(p.X, Sample(p.X, p.Y) + lift, p.Y);

    /// Ray-march the camera ray onto the surface.
    public bool Raycast(Vector3 from, Vector3 dir, out Vector3 hit)
    {
        hit = Vector3.Zero;
        if (dir.Y > -0.001f) return false;
        float t = 0, step = 1.5f;
        Vector3 prev = from;
        for (int k = 0; k < 600; k++)
        {
            t += step;
            Vector3 p = from + dir * t;
            if (p.Y <= Sample(p.X, p.Z))
            {
                // refine
                float lo = t - step, hi = t;
                for (int r = 0; r < 12; r++)
                {
                    float mid = (lo + hi) * 0.5f;
                    Vector3 m = from + dir * mid;
                    if (m.Y <= Sample(m.X, m.Z)) hi = mid; else lo = mid;
                }
                hit = from + dir * hi;
                return true;
            }
            prev = p;
            if (p.Y < -50) break;
        }
        return false;
    }

    public void ForEachInRadius(float cx, float cz, float r, Action<int, float> fn)
    {
        int x0 = Math.Max(0, (int)((cx - r) / Cell)), x1 = Math.Min(N - 1, (int)((cx + r) / Cell) + 1);
        int z0 = Math.Max(0, (int)((cz - r) / Cell)), z1 = Math.Min(N - 1, (int)((cz + r) / Cell) + 1);
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x * Cell - cx, dz = z * Cell - cz;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d <= r) fn(z * N + x, d);
            }
    }
}
