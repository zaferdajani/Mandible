using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Minimal procedural mesh builder (positions, normals, vertex colors, two UV sets).
public sealed class MeshKit
{
    public readonly List<Vector3> V = new();
    public readonly List<Vector3> Nrm = new();
    public readonly List<Color> C = new();
    public readonly List<Vector2> UV = new();
    public readonly List<Vector2> UV2 = new();
    public readonly List<int> I = new();

    public int Add(Vector3 p, Vector3 n, Color c, Vector2 uv, Vector2 uv2)
    {
        V.Add(p); Nrm.Add(n); C.Add(c); UV.Add(uv); UV2.Add(uv2);
        return V.Count - 1;
    }

    public void Tri(int a, int b, int c) { I.Add(a); I.Add(b); I.Add(c); }
    public void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }

    /// Ellipsoid around `center`, oriented by `basis` (columns are local axes scaled by radii).
    public void Ellipsoid(Vector3 center, Basis basis, int seg, int rings, Color col, Vector2 uv2, Func<Vector3, float> displace = null)
    {
        int start = V.Count;
        var normalBasis = basis.Inverse().Transposed(); // ellipsoid normals use the inverse-transpose
        for (int r = 0; r <= rings; r++)
        {
            float v = (float)r / rings;
            float phi = v * Mathf.Pi;
            for (int s = 0; s <= seg; s++)
            {
                float u = (float)s / seg;
                float th = u * Mathf.Tau;
                var unit = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                float d = displace?.Invoke(unit) ?? 1f;
                Vector3 p = center + basis * (unit * d);
                Vector3 n = (normalBasis * unit).Normalized();
                Add(p, n, col, new Vector2(u, v), uv2);
            }
        }
        int row = seg + 1;
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < seg; s++)
            {
                int a = start + r * row + s;
                Quad(a, a + row, a + row + 1, a + 1); // Godot front faces are clockwise

            }
    }

    /// Tapered tube from a to b.
    public void Tube(Vector3 a, Vector3 b, float ra, float rb, int sides, Color ca, Color cb, Vector2 uv2, bool caps = true)
    {
        Vector3 axis = b - a;
        float len = axis.Length();
        if (len < 1e-5f) return;
        Vector3 w = axis / len;
        Vector3 up = Mathf.Abs(w.Y) < 0.95f ? Vector3.Up : Vector3.Right;
        Vector3 u = w.Cross(up).Normalized();
        Vector3 v = u.Cross(w).Normalized();
        int start = V.Count;
        for (int i = 0; i <= sides; i++)
        {
            float t = (float)i / sides * Mathf.Tau;
            Vector3 dir = u * Mathf.Cos(t) + v * Mathf.Sin(t);
            Add(a + dir * ra, dir, ca, new Vector2((float)i / sides, 0), uv2);
            Add(b + dir * rb, dir, cb, new Vector2((float)i / sides, 1), uv2);
        }
        for (int i = 0; i < sides; i++)
        {
            int k = start + i * 2;
            Quad(k, k + 2, k + 3, k + 1);
        }
        if (caps)
        {
            int ce = Add(b + w * rb * 0.6f, w, cb, new Vector2(0.5f, 1), uv2);
            for (int i = 0; i < sides; i++)
            {
                int k = start + i * 2 + 1;
                Tri(k, k + 2, ce);
            }
        }
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (3 * p1 - p0 - 3 * p2 + p3) * t3);
    }

    static float CatmullRom(float p0, float p1, float p2, float p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (3 * p1 - p0 - 3 * p2 + p3) * t3);
    }

    /// One continuous tube along a Catmull-Rom spine through `knots`: radius and limb weight are interpolated
    /// between knots, so a wider knot reads as a joint bulb and the whole limb bows instead of kinking. The rings
    /// share one surface, the normals follow the bend, and the end closes with a cone. For legs, antennae,
    /// mandibles and anything else that should look grown rather than assembled.
    public void Loft(Vector3[] knots, float[] radius, float[] weight, int part, float region, int sides, Vector2 uv2, int subdiv = 3, bool cap = true)
    {
        int n = knots.Length;
        var pts = new List<Vector3>();
        var rad = new List<float>();
        var wt = new List<float>();
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 p0 = knots[Math.Max(i - 1, 0)], p1 = knots[i], p2 = knots[i + 1], p3 = knots[Math.Min(i + 2, n - 1)];
            float r0 = radius[Math.Max(i - 1, 0)], r1 = radius[i], r2 = radius[i + 1], r3 = radius[Math.Min(i + 2, n - 1)];
            int steps = i == n - 2 ? subdiv + 1 : subdiv;
            for (int k = 0; k < steps; k++)
            {
                float t = (float)k / subdiv;
                pts.Add(CatmullRom(p0, p1, p2, p3, t));
                rad.Add(Mathf.Max(0.0005f, CatmullRom(r0, r1, r2, r3, t)));
                wt.Add(Mathf.Lerp(weight[i], weight[i + 1], t));
            }
        }
        int rows = pts.Count;
        int start = V.Count;
        Vector3 u = Vector3.Zero, tan = Vector3.Forward;
        for (int k = 0; k < rows; k++)
        {
            var d = k < rows - 1 ? pts[k + 1] - pts[k] : pts[k] - pts[k - 1];
            if (d.LengthSquared() > 1e-10f) tan = d.Normalized();
            if (k == 0)
            {
                Vector3 up = Mathf.Abs(tan.Y) < 0.95f ? Vector3.Up : Vector3.Right;
                u = tan.Cross(up).Normalized();
            }
            else u = (u - tan * u.Dot(tan)).Normalized(); // parallel transport: no twist along the limb
            Vector3 v = u.Cross(tan).Normalized();
            var col = new Color(part / 255f, wt[k], region, 1);
            for (int i = 0; i <= sides; i++)
            {
                float a = (float)i / sides * Mathf.Tau;
                Vector3 dir = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                Add(pts[k] + dir * rad[k], dir, col, new Vector2((float)i / sides, (float)k / (rows - 1)), uv2);
            }
        }
        int row = sides + 1;
        for (int k = 0; k < rows - 1; k++)
            for (int i = 0; i < sides; i++)
            {
                int a = start + k * row + i;
                int b = a + row;
                Quad(a, a + 1, b + 1, b);
            }
        if (cap)
        {
            var endCol = new Color(part / 255f, wt[rows - 1], region, 1);
            int ce = Add(pts[rows - 1] + tan * rad[rows - 1] * 0.6f, tan, endCol, new Vector2(0.5f, 1), uv2);
            int last = start + (rows - 1) * row;
            for (int i = 0; i < sides; i++) Tri(last + i, last + i + 1, ce);
        }
    }

    /// Smooth normals over the vertices and triangles added since (`vStart`, `iStart`): face normals are
    /// accumulated and shared between vertices at the same position, so a displaced ellipsoid's seam and
    /// poles close up and sculpted bumps shade as one surface.
    public void SmoothNormals(int vStart, int iStart)
    {
        var acc = new Dictionary<(int, int, int), Vector3>();
        (int, int, int) Key(Vector3 p) => ((int)Mathf.Round(p.X * 20000), (int)Mathf.Round(p.Y * 20000), (int)Mathf.Round(p.Z * 20000));
        for (int k = iStart; k < I.Count; k += 3)
        {
            int a = I[k], b = I[k + 1], c = I[k + 2];
            Vector3 n = (V[c] - V[a]).Cross(V[b] - V[a]);
            foreach (int idx in new[] { a, b, c })
            {
                var key = Key(V[idx]);
                acc[key] = acc.TryGetValue(key, out var cur) ? cur + n : n;
            }
        }
        for (int k = vStart; k < V.Count; k++)
            if (acc.TryGetValue(Key(V[k]), out var n) && n.LengthSquared() > 1e-12f) Nrm[k] = n.Normalized();
    }

    /// Bakes a cheap ambient-occlusion term into vertex colour alpha: every vertex is darkened by how close
    /// it sits under the big round masses of the body (`occluders`: centre and radius), facing them. It
    /// grounds legs under the thorax, the neck and the waist, and shows on every quality tier.
    public void BakeAO(IList<(Vector3 c, float r)> occluders, float strength = 1f)
    {
        for (int k = 0; k < V.Count; k++)
        {
            float occ = 0;
            foreach (var (c, r) in occluders)
            {
                var to = c - V[k];
                float d = to.Length() - r;
                if (d <= 0 || d > r * 1.6f) continue; // inside the mass itself: its own shading; far away: free
                float facing = Mathf.Clamp(Nrm[k].Dot(to / (d + r)) + 0.45f, 0, 1);
                float near = 1 - d / (r * 1.6f);
                occ += near * near * facing * 0.7f;
            }
            float ao = Mathf.Clamp(1 - occ * strength, 0.35f, 1f);
            var col = C[k];
            C[k] = new Color(col.R, col.G, col.B, ao);
        }
    }

    public void RecalcNormals()
    {
        var acc = new Vector3[V.Count];
        for (int k = 0; k < I.Count; k += 3)
        {
            int a = I[k], b = I[k + 1], c = I[k + 2];
            Vector3 n = (V[c] - V[a]).Cross(V[b] - V[a]);
            acc[a] += n; acc[b] += n; acc[c] += n;
        }
        for (int k = 0; k < V.Count; k++) Nrm[k] = acc[k].Normalized();
    }

    public ArrayMesh Commit(ArrayMesh into = null)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = V.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = Nrm.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = C.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = UV.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV2] = UV2.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = I.ToArray();
        var mesh = into ?? new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
