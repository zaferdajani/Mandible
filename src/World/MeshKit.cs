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
