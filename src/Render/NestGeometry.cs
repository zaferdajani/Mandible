using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Presentation-only horizontal colony. One distance field drives the connected excavation mesh,
/// the soil roof and hit testing. It never changes the simulation's graph, paths or random stream.
public sealed class NestGeometry
{
    public const float Depth = 6.0f;      // floor below the surface: deep enough that the walls read as walls
    public const float Lip = 0.7f;        // the bank climbs from the floor's edge to the surface over this distance: steep, like a dug wall
    public const float HubDepth = 1.0f;   // the bowl under the entrance hole, where the galleries start
    public const float RampFrom = 1.4f, RampTo = 7.5f; // galleries dive from the hub's depth to full depth over this stretch

    /// How deep the floor lies at a nest-local point: the hub's bowl near the entrance, ramping down each gallery
    /// to the rooms' depth. Ants are drawn at this height, so one walks in through the hole and down the slope.
    public static float FloorDepth(Vector2 local) => Mathf.Lerp(HubDepth, Depth, Mathf.SmoothStep(RampFrom, RampTo, local.Length()));
    public const int MaxSegments = 32, Stride = 7;
    public readonly float[] Network = new float[MaxSegments * Stride];
    public int Count { get; private set; }
    public int Revision { get; private set; }
    Vector2 _min, _max;
    readonly float[] _next = new float[MaxSegments * Stride];

    public static float Progress(NestChamber ch) => ch.Built ? 1f : Mathf.Max(0.18f, Mathf.Round(ch.Progress * 32f) / 32f);

    public static (float w, float h, float d) RoomSize(NestChamber ch) => ch.Kind switch
    {
        ChamberKind.Royal => (3.3f + 0.12f * ch.Tier, 1.7f + 0.12f * ch.Tier, 2.6f + 0.10f * ch.Tier),
        ChamberKind.Brood => (2.8f, 1.2f, 2.2f),
        ChamberKind.Storage => (2.5f, 1.4f, 2.0f),
        ChamberKind.Nursery => (3.4f, 1.6f, 2.7f), // the royal larva, its bed and the heap of food the nurses pile
        _ => (2.4f + 0.10f * ch.Tier, 1.3f, 2.0f + 0.08f * ch.Tier),
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
        Add(Vector2.Zero, Vector2.Zero, 1.4f, 1.4f, 0.9f); // the hub bowl under the hole
        foreach (var ch in chambers)
        {
            var parent = ch.Parent >= 0 && ch.Parent < chambers.Count ? chambers[ch.Parent].Offset : Vector2.Zero;
            var (w, _, d) = RoomSize(ch);
            float p = Progress(ch);
            // The flat walking strip must contain the simulation's 0.9-unit
            // lateral allowance plus the worker and its load, before the banks rise.
            Add(parent, ch.Offset, w * p, d * p, 0.62f + 0.3f * p); // a corridor: just the sim's 0.9 lateral allowance, the lip takes the rest
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
            var r = new Vector2(Network[k + 4] + 6f, Network[k + 5] + 6f);
            _min = _min.Min(a - Vector2.One * 6).Min(b - r);
            _max = _max.Max(a + Vector2.One * 6).Max(b + r);
        }
        Revision++;
        return true;
    }

    static float Union(float a, float b)
    {
        float h = Mathf.Max(0.22f - Mathf.Abs(a - b), 0f) / 0.22f; // a tight blend: galleries meet the hub and the rooms with a shoulder, not a flare
        return Mathf.Min(a, b) - h * h * 0.055f;
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

    /// The bank's profile: flat floor to the cavity's edge, then a wall that rises steeply from the foot and eases
    /// into a sloped lip at the surface, like a pit dug in sand.
    public static float Height(float distance) => Height(distance, Depth);

    public static float Height(float distance, float floorDepth)
    {
        float t = Mathf.Clamp((distance + 0.45f) / Lip, 0f, 1f);
        return Mathf.Lerp(-floorDepth, -0.03f, Mathf.Pow(t, 1.7f));
    }

    /// A single welded heightfield: overlapping room/tunnel fields are unioned BEFORE triangulation,
    /// so there are no bowl walls blocking doorways, stacked floors or transform-scaled tube stubs.
    /// Height of the scanned wall relief at a world point (0..1), set by the renderer; null keeps the bank smooth.
    public static System.Func<Vector2, float> WallRelief;
    public const float ReliefDepth = 0.22f;

    public ArrayMesh BuildMesh(float step = 0.16f)
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
                float y = Height(d, FloorDepth(p));
                float bank = Mathf.SmoothStep(-0.5f, -0.3f, d) * (1 - Mathf.SmoothStep(0.05f, 0.2f, d)); // 0 on the floor and above the lip
                y += bank * (0.16f * Mathf.Sin(p.X * 4.3f + Mathf.Sin(p.Y * 3.1f) * 2) * Mathf.Sin(p.Y * 3.7f + p.X * 0.9f)
                             + 0.07f * Mathf.Sin(p.X * 11.3f + p.Y * 9.7f));
                // the rock face's own relief, pressed into the bank so its ledges and cracks catch the sun
                if (WallRelief != null) y -= bank * ReliefDepth * (WallRelief(p) - 0.5f);
                y = Mathf.Clamp(y, -Depth, -0.03f); // the bumps and relief never break the floor or the surface
                mesh.Add(new Vector3(p.X, y, p.Y), Vector3.Up, Colors.White, new Vector2(d, -FloorDepth(p)), Vector2.Zero);
            }
        for (int z = 0; z < nz; z++)
            for (int x = 0; x < nx; x++)
            {
                int a = z * (nx + 1) + x, b = a + 1, c = a + nx + 2, d = a + nx + 1;
                if (Mathf.Min(Mathf.Min(distances[a], distances[b]), Mathf.Min(distances[c], distances[d])) > Lip + 2.0f) continue; // the skirt reaches well under the terrain's translucent fringe, so no sky shows through it
                mesh.Quad(a, b, c, d);
            }
        mesh.RecalcNormals();
        return mesh.Commit();
    }
}
