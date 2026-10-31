using System;
using Godot;

namespace Mandible;

/// Growable MultiMesh whose instance data is written straight into a float buffer.
public sealed class Batch
{
    public readonly MultiMesh MM;
    public readonly MultiMeshInstance3D Node;
    float[] _buf = Array.Empty<float>();
    int _cap;
    public int Count;
    const int Stride = 16; // 12 transform + 4 custom

    public Batch(Node parent, Mesh mesh, Material mat, bool shadows = true, string name = "Batch")
    {
        MM = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = mesh };
        Node = new MultiMeshInstance3D
        {
            Multimesh = MM, MaterialOverride = mat, Name = name,
            CastShadow = shadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-20, -40, -20), new Vector3(Config.MapSize + 40, 160, Config.MapSize + 40)),
        };
        parent.AddChild(Node);
        Grow(64);
    }

    void Grow(int cap)
    {
        _cap = cap;
        MM.InstanceCount = cap;
        var nb = new float[cap * Stride];
        Array.Copy(_buf, nb, Math.Min(_buf.Length, nb.Length));
        _buf = nb;
    }

    public void Begin() { Count = 0; }

    public void Add(in Basis b, in Vector3 o, in Color c)
    {
        if (Count >= _cap) Grow(_cap * 2);
        int k = Count * Stride;
        var f = _buf;
        f[k + 0] = b.X.X; f[k + 1] = b.Y.X; f[k + 2] = b.Z.X; f[k + 3] = o.X;
        f[k + 4] = b.X.Y; f[k + 5] = b.Y.Y; f[k + 6] = b.Z.Y; f[k + 7] = o.Y;
        f[k + 8] = b.X.Z; f[k + 9] = b.Y.Z; f[k + 10] = b.Z.Z; f[k + 11] = o.Z;
        f[k + 12] = c.R; f[k + 13] = c.G; f[k + 14] = c.B; f[k + 15] = c.A;
        Count++;
    }

    public void End()
    {
        RenderingServer.MultimeshSetBuffer(MM.GetRid(), _buf);
        MM.VisibleInstanceCount = Count;
    }
}

public sealed class AntRenderer
{
    readonly Batch[,] _live = new Batch[2, 4];
    readonly Batch[,] _dead = new Batch[2, 4];
    readonly Batch _crumbs, _rings, _bars;
    public Func<Vector2, bool> VisibleToPlayer = _ => true;
    public bool ShowAllBars;

    static readonly Color[] Body = { new(0.028f, 0.025f, 0.024f), new(0.42f, 0.11f, 0.04f) };
    static readonly Color[] HeadC = { new(0.034f, 0.03f, 0.028f), new(0.46f, 0.13f, 0.05f) };
    static readonly Color[] GasterC = { new(0.022f, 0.02f, 0.019f), new(0.10f, 0.035f, 0.025f) };
    static readonly Color[] LegC = { new(0.07f, 0.05f, 0.04f), new(0.48f, 0.18f, 0.08f) };

    public AntRenderer(Node3D parent)
    {
        var root = new Node3D { Name = "Ants" };
        parent.AddChild(root);
        var shader = new Shader { Code = Shaders.Ant };
        for (int team = 0; team < 2; team++)
            for (int t = 0; t < 4; t++)
            {
                var mesh = AntMesh.Build((AntType)t);
                var mat = AntMat(shader, team, false);
                var dmat = AntMat(shader, team, true);
                _live[team, t] = new Batch(root, mesh, mat, true, $"Ants{team}_{t}");
                _dead[team, t] = new Batch(root, mesh, dmat, true, $"Dead{team}_{t}");
            }
        var crumbMat = new ShaderMaterial { Shader = new Shader { Code = Shaders.Food } };
        crumbMat.SetShaderParameter("kind", 0);
        _crumbs = new Batch(root, AntMesh.Crumb(), crumbMat, false, "Crumbs");

        var ringMat = new ShaderMaterial { Shader = new Shader { Code = Shaders.Ring } };
        ringMat.SetShaderParameter("color", new Color(0.55f, 1.0f, 0.45f, 0.95f));
        _rings = new Batch(root, new PlaneMesh { Size = Vector2.One }, ringMat, false, "Rings");
        var barMat = new ShaderMaterial { Shader = new Shader { Code = Shaders.Bar } };
        _bars = new Batch(root, new QuadMesh { Size = Vector2.One }, barMat, false, "Bars");
        _bars.Node.SortingOffset = 100;
    }

    static ShaderMaterial AntMat(Shader s, int team, bool dead)
    {
        var m = new ShaderMaterial { Shader = s };
        m.SetShaderParameter("body_color", Body[team]);
        m.SetShaderParameter("head_color", HeadC[team]);
        m.SetShaderParameter("gaster_color", GasterC[team]);
        m.SetShaderParameter("leg_color", LegC[team]);
        m.SetShaderParameter("dead", dead ? 1f : 0f);
        return m;
    }

    public static Basis Orient(Vector3 up, float heading, float scale)
    {
        var f = new Vector3(Mathf.Cos(heading), 0, Mathf.Sin(heading));
        var x = up.Cross(f).Normalized();
        f = x.Cross(up).Normalized();
        return new Basis(x * scale, up * scale, f * scale);
    }

    public void Update(Sim sim)
    {
        foreach (var b in _live) b.Begin();
        foreach (var b in _dead) b.Begin();
        _crumbs.Begin(); _rings.Begin(); _bars.Begin();
        var hf = sim.W.HF;
        for (int i = 0; i < sim.High; i++)
        {
            ref var a = ref sim.Ants[i];
            if (!a.Alive) continue;
            if (a.Team != Config.Player && !VisibleToPlayer(a.Pos)) continue;
            var st = Config.Of(a.Type);
            var n = hf.Normal(a.Pos.X, a.Pos.Y);
            var up = (n + Vector3.Up * 0.6f).Normalized();
            var b = Orient(up, a.Heading, st.Size);
            var o = new Vector3(a.Pos.X, a.Height + 0.02f, a.Pos.Y);
            float amp = Mathf.Clamp(a.Speed / Mathf.Max(0.1f, st.Speed), 0, 1);
            _live[a.Team, (int)a.Type].Add(b, o, new Color(a.Gait, amp, Mathf.Max(a.AttackAnim, a.Carrying ? 0.15f : 0f), a.HitFlash));
            if (a.Carrying)
            {
                var fwd = b.Z.Normalized();
                var co = o + fwd * 0.38f * st.Size + up * 0.11f * st.Size;
                _crumbs.Add(Basis.FromScale(Vector3.One * st.Size * 0.9f), co, new Color(0, 0, 0, 0));
            }
            if (a.Selected)
                _rings.Add(Basis.FromScale(Vector3.One * st.Size * 1.25f), o + up * 0.05f, new Color(1, 0, 0, 0));
            if (a.Selected || (ShowAllBars && a.DamagedT > -6) || (a.Type == AntType.Queen && a.Hp < st.Hp * 0.98f))
            {
                var tc = Config.TeamUi(a.Team);
                float w = a.Type == AntType.Queen ? 5f : Mathf.Max(1.1f, st.Size * 1.0f);
                var bb = Basis.FromScale(new Vector3(w, a.Type == AntType.Queen ? 0.35f : 0.16f, 1));
                _bars.Add(bb, o + Vector3.Up * (st.Size * 0.55f + 0.5f), new Color(a.Hp / st.Hp, tc.R, tc.G, tc.B));
            }
        }
        foreach (var f in sim.W.Food)
        {
            if (f.Kind != FoodKind.Corpse || !f.Alive) continue;
            if (!VisibleToPlayer(f.Pos) && f.Team != Config.Player) continue;
            var st = Config.Of(f.AntType);
            var n = hf.Normal(f.Pos.X, f.Pos.Y);
            float s = st.Size * Mathf.Lerp(0.55f, 1f, f.Amount / f.Max);
            var b = Orient(n, f.Heading, s);
            b = new Basis(-b.X, -b.Y, b.Z);
            var o = new Vector3(f.Pos.X, hf.Sample(f.Pos.X, f.Pos.Y) + 0.24f * s, f.Pos.Y);
            _dead[f.Team, (int)f.AntType].Add(b, o, new Color(0.25f, 0, 0, 0));
        }
        foreach (var b in _live) b.End();
        foreach (var b in _dead) b.End();
        _crumbs.End(); _rings.End(); _bars.End();
    }
}
