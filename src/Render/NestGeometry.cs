using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Presentation-only horizontal colony. One distance field drives the connected excavation mesh,
/// the soil roof and hit testing. It never changes the simulation's graph, paths or random stream.
public sealed class NestGeometry
{
    public const float Depth = 2.4f;
    public const int MaxSegments = 32, Stride = 7;
    public readonly float[] Network = new float[MaxSegments * Stride];
    public int Count { get; private set; }
    public int Revision { get; private set; }
    Vector2 _min, _max;
    readonly float[] _next = new float[MaxSegments * Stride];

    public static float Progress(NestChamber ch) => ch.Built ? 1f : Mathf.Max(0.18f, Mathf.Round(ch.Progress * 32f) / 32f);

    public static (float w, float h, float d) RoomSize(NestChamber ch) => ch.Kind switch
    {
        ChamberKind.Royal => (3.4f + 0.15f * ch.Tier, 1.7f + 0.12f * ch.Tier, 2.8f + 0.12f * ch.Tier),
        ChamberKind.Brood => (2.35f, 1.2f, 2f),
        ChamberKind.Storage => (2.5f, 1.4f, 2.1f),
        _ => (2.15f + 0.12f * ch.Tier, 1.3f, 1.9f + 0.10f * ch.Tier),
    };

    public bool Refresh(IReadOnlyList<NestChamber> chambers)
    {
        int count = 0;
        void Add(Vector2 a, Vector2 b, float rx, float rz, float width)
        {
            if (count == MaxSegments) return;
            int k = count++ * Stride;
            _next[k] = a.X; _next[k + 1] = a.Y; _next[k + 2] = b.X; _next[k + 3] = b.Y;
            _next[k + 4] = rx; _next[k + 5] = rz; _next[k + 6] = width;
        }
        Add(Vector2.Zero, Vector2.Zero, 1.3f, 1.3f, 1.1f);
        foreach (var ch in chambers)
        {
            var parent = ch.Parent >= 0 && ch.Parent < chambers.Count ? chambers[ch.Parent].Offset : Vector2.Zero;
            var (w, _, d) = RoomSize(ch);
            float p = Progress(ch);
            Add(parent, ch.Offset, w * p, d * p, 0.7f + 0.4f * p);
        }
        bool changed = count != Count;
        for (int i = 0; i < count * Stride; i++) changed |= _next[i] != Network[i];
        if (!changed) return false;
        Count = count;
        System.Array.Copy(_next, Network, count * Stride);
        _min = new Vector2(-3, -3); _max = new Vector2(3, 3);
        for (int i = 0; i < Count; i++)
        {
            int k = i * Stride;
            var a = new Vector2(Network[k], Network[k + 1]);
            var b = new Vector2(Network[k + 2], Network[k + 3]);
            var r = new Vector2(Network[k + 4] + 2f, Network[k + 5] + 2f);
            _min = _min.Min(a - Vector2.One * 2).Min(b - r);
            _max = _max.Max(a + Vector2.One * 2).Max(b + r);
        }
        Revision++;
        return true;
    }

    static float Union(float a, float b)
    {
        float h = Mathf.Max(0.45f - Mathf.Abs(a - b), 0f) / 0.45f;
        return Mathf.Min(a, b) - h * h * 0.1125f;
    }

    // Keep these operations in sync with Shaders.NetworkFn. All coordinates are nest-local.
    public float Distance(Vector2 p)
    {
        float best = 100000f;
        for (int i = 0; i < Count; i++)
        {
            int k = i * Stride;
            var a = new Vector2(Network[k], Network[k + 1]);
            var b = new Vector2(Network[k + 2], Network[k + 3]);
            var ab = b - a;
            float t = Mathf.Clamp((p - a).Dot(ab) / Mathf.Max(ab.LengthSquared(), 0.0001f), 0, 1);
            float tube = (p - a - ab * t).Length() - Network[k + 6];
            var radii = new Vector2(Network[k + 4], Network[k + 5]);
            float room = ((p - b) / radii).Length() - 1f;
            room *= Mathf.Min(radii.X, radii.Y);
            best = Union(best, Union(tube, room));
        }
        return best + 0.10f * Mathf.Sin(p.X * 2.1f + Mathf.Sin(p.Y * 1.3f)) * Mathf.Sin(p.Y * 1.8f)
                    + 0.045f * Mathf.Sin(p.X * 5.7f + p.Y * 4.3f);
    }

    public static float Height(float distance) => Mathf.Lerp(-Depth, -0.18f, Mathf.SmoothStep(-0.72f, 0.22f, distance));

    /// A single welded heightfield: overlapping room/tunnel fields are unioned BEFORE triangulation,
    /// so there are no bowl walls blocking doorways, stacked floors or transform-scaled tube stubs.
    public ArrayMesh BuildMesh(float step = 0.24f)
    {
        int nx = Mathf.CeilToInt((_max.X - _min.X) / step), nz = Mathf.CeilToInt((_max.Y - _min.Y) / step);
        var mesh = new MeshKit();
        var distances = new float[(nx + 1) * (nz + 1)];
        for (int z = 0; z <= nz; z++)
            for (int x = 0; x <= nx; x++)
            {
                var p = _min + new Vector2(x * step, z * step);
                float d = Distance(p);
                distances[z * (nx + 1) + x] = d;
                mesh.Add(new Vector3(p.X, Height(d), p.Y), Vector3.Up, Colors.White, new Vector2(d, 0), Vector2.Zero);
            }
        for (int z = 0; z < nz; z++)
            for (int x = 0; x < nx; x++)
            {
                int a = z * (nx + 1) + x, b = a + 1, c = a + nx + 2, d = a + nx + 1;
                if (Mathf.Min(Mathf.Min(distances[a], distances[b]), Mathf.Min(distances[c], distances[d])) > 1.4f) continue;
                mesh.Quad(a, b, c, d);
            }
        mesh.RecalcNormals();
        return mesh.Commit();
    }
}
