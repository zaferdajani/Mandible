using System.Collections.Generic;
using Godot;

namespace Mandible;

/// CPU particles (dust, hemolymph, crumbs) and command markers, drawn with one MultiMesh.
public sealed class Effects
{
    struct P { public Vector3 Pos, Vel; public float Life, Max, Size, Kind, Grav; }
    readonly List<P> _ps = new();
    readonly Batch _batch;
    readonly Rng _rng = new(99);
    readonly MeshInstance3D[] _markers = new MeshInstance3D[4];
    readonly float[] _markerT = new float[4];
    int _nextMarker;
    readonly Heightfield _hf;

    public Effects(Node3D parent, Heightfield hf)
    {
        _hf = hf;
        var mat = new ShaderMaterial { Shader = new Shader { Code = Shaders.Particle } };
        _batch = new Batch(parent, new QuadMesh { Size = Vector2.One }, mat, false, "Particles");
        var shader = new Shader { Code = Shaders.Marker };
        for (int i = 0; i < _markers.Length; i++)
        {
            var m = new ShaderMaterial { Shader = shader };
            _markers[i] = new MeshInstance3D { Mesh = new PlaneMesh { Size = Vector2.One * 4 }, MaterialOverride = m, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            parent.AddChild(_markers[i]);
            _markerT[i] = 1;
        }
    }

    public void Burst(Vector3 at, int count, float kind, float speed, float size, float life)
    {
        if (_ps.Count > 2500) return;
        for (int i = 0; i < count; i++)
        {
            var d = new Vector3(_rng.R(-1, 1), _rng.R(0.3f, 1.2f), _rng.R(-1, 1)).Normalized();
            _ps.Add(new P { Pos = at, Vel = d * speed * _rng.R(0.4f, 1f), Life = 0, Max = life * _rng.R(0.6f, 1.2f), Size = size * _rng.R(0.6f, 1.3f), Kind = kind, Grav = kind < 0.5f ? 0.6f : 9f });
        }
    }

    public void Marker(Vector3 at, Color c)
    {
        var m = _markers[_nextMarker];
        _markerT[_nextMarker] = 0;
        m.Visible = true;
        m.GlobalPosition = at + Vector3.Up * 0.15f;
        ((ShaderMaterial)m.MaterialOverride).SetShaderParameter("color", c);
        _nextMarker = (_nextMarker + 1) % _markers.Length;
    }

    public void Update(float dt)
    {
        for (int i = 0; i < _markers.Length; i++)
        {
            if (_markerT[i] >= 1) { _markers[i].Visible = false; continue; }
            _markerT[i] = Mathf.Min(1, _markerT[i] + dt * 1.8f);
            ((ShaderMaterial)_markers[i].MaterialOverride).SetShaderParameter("t", _markerT[i]);
        }
        _batch.Begin();
        for (int i = _ps.Count - 1; i >= 0; i--)
        {
            var p = _ps[i];
            p.Life += dt;
            if (p.Life >= p.Max) { _ps[i] = _ps[^1]; _ps.RemoveAt(_ps.Count - 1); continue; }
            p.Vel.Y -= p.Grav * dt;
            p.Vel *= 1 - dt * (p.Kind < 0.5f ? 2.5f : 0.5f);
            p.Pos += p.Vel * dt;
            float g = _hf.Sample(p.Pos.X, p.Pos.Z) + 0.05f;
            if (p.Pos.Y < g) { p.Pos.Y = g; p.Vel *= 0.3f; }
            _ps[i] = p;
            float t = p.Life / p.Max;
            float alpha = (p.Kind < 0.5f ? 0.55f : 0.9f) * (1 - t) * Mathf.Min(1, p.Life * 12);
            float size = p.Size * (p.Kind < 0.5f ? 1 + t * 1.8f : 1);
            _batch.Add(Basis.Identity, p.Pos, new Color(alpha, size, p.Kind, 0));
        }
        _batch.End();
    }
}
