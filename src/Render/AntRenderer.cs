using System.Collections.Generic;
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
    readonly Batch[,] _live = new Batch[3, 13];
    readonly Batch[,] _dead = new Batch[3, 13];
    readonly Batch _crumbs, _rings, _bars;
    public Func<Vector2, bool> VisibleToPlayer = _ => true;
    public bool ShowAllBars;

    static readonly Color[] Body = { new(0.028f, 0.025f, 0.024f), new(0.42f, 0.11f, 0.04f) };
    static readonly Color[] HeadC = { new(0.034f, 0.03f, 0.028f), new(0.46f, 0.13f, 0.05f) };
    static readonly Color[] GasterC = { new(0.022f, 0.02f, 0.019f), new(0.10f, 0.035f, 0.025f) };
    static readonly Color[] LegC = { new(0.07f, 0.05f, 0.04f), new(0.48f, 0.18f, 0.08f) };

    // wild bugs: body, head, shell/abdomen, legs per species
    static readonly Dictionary<AntType, Color[]> BugColors = new()
    {
        [AntType.Beetle] = new[] { new Color(0.03f, 0.07f, 0.05f), new Color(0.02f, 0.05f, 0.04f), new Color(0.02f, 0.09f, 0.08f), new Color(0.03f, 0.04f, 0.03f) },
        [AntType.Spider] = new[] { new Color(0.34f, 0.24f, 0.14f), new Color(0.30f, 0.21f, 0.12f), new Color(0.31f, 0.22f, 0.13f), new Color(0.38f, 0.27f, 0.16f) },
        // stag beetle: mahogany wing cases, dark head, deep red-brown jaws (head colour x0.6 in the shader)
        [AntType.StagBeetle] = new[] { new Color(0.08f, 0.03f, 0.015f), new Color(0.42f, 0.11f, 0.04f), new Color(0.30f, 0.09f, 0.03f), new Color(0.07f, 0.03f, 0.015f) },
        [AntType.Wasp] = new[] { new Color(0.025f, 0.022f, 0.018f), new Color(0.85f, 0.58f, 0.04f), new Color(0.9f, 0.62f, 0.05f), new Color(0.72f, 0.43f, 0.06f) },
    };

    readonly Dictionary<(int, AntType), Batch> _wings = new();
    readonly Batch _cocoons, _webs, _blobs;

    readonly Batch _pips;
    readonly Node3D _labelRoot;
    readonly List<Label3D> _labels = new();

    public AntRenderer(Node3D parent)
    {
        var root = new Node3D { Name = "Ants" };
        parent.AddChild(root);
        var shader = new Shader { Code = Shaders.Ant };
        for (int team = 0; team < 3; team++)
            for (int t = 0; t < 13; t++)
            {
                if ((team == Config.Wild) != Config.IsBug((AntType)t)) continue;
                var mesh = AntMesh.Build((AntType)t);
                var mat = AntMat(shader, team, (AntType)t, false);
                var dmat = AntMat(shader, team, (AntType)t, true);
                _live[team, t] = new Batch(root, mesh, mat, true, $"Ants{team}_{t}");
                _dead[team, t] = new Batch(root, mesh, dmat, true, $"Dead{team}_{t}");
            }
        // translucent wings for every flyer, beating in their own batch
        var wingShader = new Shader { Code = Shaders.Wing };
        foreach (var (team, type) in new[] { (0, AntType.Flyer), (1, AntType.Flyer), (0, AntType.Marshal), (1, AntType.Marshal), (Config.Wild, AntType.Wasp) })
        {
            var wm = new ShaderMaterial { Shader = wingShader };
            if (type == AntType.Wasp) { wm.SetShaderParameter("tint", new Color(0.82f, 0.62f, 0.3f)); wm.SetShaderParameter("vein", new Color(0.35f, 0.2f, 0.05f)); }
            if (type == AntType.Marshal) { wm.SetShaderParameter("tint", new Color(0.95f, 0.85f, 0.6f)); wm.SetShaderParameter("vein", new Color(0.6f, 0.42f, 0.1f)); }
            _wings[(team, type)] = new Batch(root, AntMesh.Wings(type == AntType.Marshal ? AntType.Flyer : type), wm, false, $"Wings{team}_{type}");
            _wings[(team, type)].Node.SortingOffset = 5;
        }
        var silk = AntMesh.Silk();
        var silkMat = new ShaderMaterial { Shader = new Shader { Code = Shaders.Silk } };
        _cocoons = new Batch(root, silk, silkMat, true, "Cocoons");
        _blobs = new Batch(root, silk, silkMat, true, "WebShots");
        var webMat = new ShaderMaterial { Shader = new Shader { Code = Shaders.Web } };
        _webs = new Batch(root, new PlaneMesh { Size = Vector2.One }, webMat, false, "Webs");
        var pipMat = new ShaderMaterial { Shader = new Shader { Code = Shaders.Pip } };
        _pips = new Batch(root, new QuadMesh { Size = Vector2.One }, pipMat, false, "Pips");
        _pips.Node.SortingOffset = 101;
        _labelRoot = new Node3D { Name = "Labels" };
        root.AddChild(_labelRoot);
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

    static ShaderMaterial AntMat(Shader s, int team, AntType type, bool dead)
    {
        var m = new ShaderMaterial { Shader = s };
        if (team == Config.Wild)
        {
            var c = BugColors[type];
            m.SetShaderParameter("body_color", c[0]);
            m.SetShaderParameter("head_color", c[1]);
            m.SetShaderParameter("gaster_color", c[2]);
            m.SetShaderParameter("leg_color", c[3]);
            m.SetShaderParameter("pattern", type == AntType.Spider ? 2 : type == AntType.Wasp ? 3 : 1);
            m.SetShaderParameter("irid", type switch { AntType.Beetle => 0.9f, AntType.StagBeetle => 0.25f, _ => 0f });
        }
        else
        {
            m.SetShaderParameter("body_color", Body[team]);
            m.SetShaderParameter("head_color", HeadC[team]);
            m.SetShaderParameter("gaster_color", GasterC[team]);
            m.SetShaderParameter("leg_color", LegC[team]);
            if (Config.IsHero(type)) m.SetShaderParameter("hero", 1f);
            if (type == AntType.Matron || type == AntType.Gunner) m.SetShaderParameter("acid", type == AntType.Matron ? new Color(0.35f, 0.75f, 0.08f) : new Color(0.18f, 0.4f, 0.05f));
        }
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

    public void Update(Sim sim, Camera3D cam, float alpha = 1f)
    {
        foreach (var b in _live) b?.Begin();
        foreach (var b in _dead) b?.Begin();
        _crumbs.Begin(); _rings.Begin(); _bars.Begin(); _pips.Begin();
        foreach (var w in _wings.Values) w.Begin();
        _cocoons.Begin(); _webs.Begin(); _blobs.Begin();
        var hf = sim.W.HF;
        var camRight = cam.GlobalTransform.Basis.X.Normalized();
        var camPos = cam.GlobalPosition;
        int labelsUsed = 0;
        for (int i = 0; i < sim.High; i++)
        {
            ref var a = ref sim.Ants[i];
            if (!a.Alive) continue;
            if (a.Team != Local.Me && !VisibleToPlayer(a.Pos)) continue;
            var st = Config.Of(a.Type);
            st.Size = a.Size;
            bool flies = Config.Flies(a.Type);
            // blend between the last two simulation ticks so motion is smooth at any frame rate
            var ip = a.PrevPos.DistanceSquaredTo(a.Pos) > 36 ? a.Pos : a.PrevPos.Lerp(a.Pos, alpha);
            float ih = Mathf.LerpAngle(a.PrevHeading, a.Heading, alpha);
            float ialt = Mathf.Lerp(a.PrevAlt, a.Alt, alpha);
            var n = hf.Normal(ip.X, ip.Y);
            var up = flies ? Vector3.Up : (n + Vector3.Up * 0.6f).Normalized();
            var b = Orient(up, ih, st.Size);
            if (flies) b = new Basis(b.X.Normalized(), Mathf.Sin(sim.Time * 1.7f + i) * 0.06f + (a.Speed > 1 ? -0.12f : 0f)) * b; // pitch nose-down when cruising, gentle wobble
            float hover = flies ? Mathf.Sin(sim.Time * 3.1f + i * 0.7f) * 0.15f : 0f;
            var o = new Vector3(ip.X, hf.Sample(ip.X, ip.Y) + 0.02f + ialt + hover, ip.Y);
            float amp = Mathf.Clamp(a.Speed / Mathf.Max(0.1f, st.Speed), 0, 1);
            if (a.Root > 0) amp = 0.35f; // struggling in silk
            _live[a.Team, (int)a.Type].Add(b, o, new Color(a.Gait, amp, Mathf.Max(a.AttackAnim, a.Carrying ? 0.15f : 0f), a.HitFlash));
            if (flies && _wings.TryGetValue((a.Team, a.Type), out var wb))
                wb.Add(b, o, new Color(a.Type == AntType.Wasp ? 46f : 36f, a.Alt > 0.6f ? 1f : 0.2f, 0, 0));
            if (a.Root > 0)
                _cocoons.Add(Orient(up, ih, st.Size * 1.05f), o + up * (0.13f * st.Size), new Color(Mathf.Clamp(a.Root * 2, 0.3f, 0.95f), 0, 0, 0));
            if (a.Carrying)
            {
                var fwd = b.Z.Normalized();
                var co = o + fwd * 0.38f * st.Size + up * 0.11f * st.Size;
                _crumbs.Add(Basis.FromScale(Vector3.One * st.Size * 0.9f), co, new Color(0, 0, 0, 0));
            }
            if (a.Selected)
                _rings.Add(Basis.FromScale(Vector3.One * st.Size * 1.25f), o + up * 0.05f, new Color(1, 0, 0, 0));
            bool wild = a.Team == Config.Wild;
            float barY = st.Size * 0.55f + 0.5f;
            if (a.Selected || (ShowAllBars && a.DamagedT > -6) || (a.Type == AntType.Queen && a.Hp < a.MaxHp * 0.98f) || (wild && a.Hp < a.MaxHp * 0.99f))
            {
                var tc = Config.TeamUi(a.Team);
                float w = a.Type == AntType.Queen ? 5f : Mathf.Max(1.1f, st.Size * 1.0f);
                var bb = Basis.FromScale(new Vector3(w, a.Type == AntType.Queen ? 0.35f : 0.16f, 1));
                _bars.Add(bb, o + Vector3.Up * barY, new Color(a.Hp / a.MaxHp, tc.R, tc.G, tc.B));
            }
            // veteran chevrons: one gold pip per level above the first
            if (Config.IsWarrior(a.Type) && !Config.IsHero(a.Type) && a.SquadP1 == 0 && a.Level > 1)
            {
                float pip = Mathf.Clamp(0.3f + st.Size * 0.1f, 0.38f, 0.6f);
                var top = o + Vector3.Up * (barY + 0.45f);
                for (int p = 0; p < a.Level - 1; p++)
                {
                    var at = top + camRight * (p - (a.Level - 2) * 0.5f) * pip * 1.1f;
                    _pips.Add(Basis.FromScale(Vector3.One * pip), at, new Color(a.Team == Local.Me ? 1 : 0, 0, 0, 1));
                }
            }
            // wild bugs announce their level
            bool hero = Config.IsHero(a.Type);
            if ((wild || hero) && camPos.DistanceTo(o) < 190)
            {
                if (labelsUsed >= _labels.Count)
                {
                    var l = new Label3D
                    {
                        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, FontSize = 44, OutlineSize = 10,
                        PixelSize = 0.012f, Font = UiTheme.Title, OutlineModulate = new Color(0, 0, 0, 0.85f), RenderPriority = 10,
                    };
                    _labelRoot.AddChild(l);
                    _labels.Add(l);
                }
                var label = _labels[labelsUsed++];
                label.Visible = true;
                label.Text = hero ? (camPos.DistanceTo(o) < 70 ? $"{st.Name}  {a.Level}" : $"{a.Level}") : $"Lv {a.Level}";
                label.Modulate = hero ? (a.Team == Local.Me ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.4f, 0.3f))
                    : a.Level >= 4 ? new Color(1f, 0.45f, 0.35f) : a.Level >= 2 ? new Color(1f, 0.8f, 0.45f) : new Color(0.85f, 0.9f, 0.7f);
                float dist = camPos.DistanceTo(o);
                label.PixelSize = (hero ? 0.0045f : 0.008f) + dist * (hero ? 0.00005f : 0.0001f);
                label.GlobalPosition = o + Vector3.Up * (barY + (hero ? 0.7f : 0.9f));
            }
        }
        // one banner per squad: rank chevrons over the pack, and its strength when it has lost members
        foreach (var s in sim.Squads)
        {
            if (!s.Alive) continue;
            var c = Vector2.Zero;
            foreach (int m in s.Members) c += sim.Ants[m].PrevPos.Lerp(sim.Ants[m].Pos, alpha);
            c /= s.Count;
            if (s.Team != Local.Me && !VisibleToPlayer(c)) continue;
            float size = Config.Of(s.Type).Size;
            float alt = Config.CruiseAlt(s.Type);
            var top = new Vector3(c.X, hf.Sample(c.X, c.Y) + alt + size * 0.55f + 1.4f, c.Y);
            if (camPos.DistanceTo(top) > 200) continue;
            float pip = Mathf.Clamp(0.35f + size * 0.12f, 0.45f, 0.7f);
            for (int p = 0; p < s.Level - 1; p++)
                _pips.Add(Basis.FromScale(Vector3.One * pip), top + camRight * (p - (s.Level - 2) * 0.5f) * pip * 1.1f, new Color(s.Team == Local.Me ? 1 : 0, 0, 0, 1));
            if (s.Team == Local.Me && s.Count < s.Size)
            {
                if (labelsUsed >= _labels.Count)
                {
                    var nl = new Label3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, FontSize = 44, OutlineSize = 10, Font = UiTheme.Title, OutlineModulate = new Color(0, 0, 0, 0.85f), RenderPriority = 10 };
                    _labelRoot.AddChild(nl);
                    _labels.Add(nl);
                }
                var l = _labels[labelsUsed++];
                l.Visible = true;
                l.Text = $"{s.Count}/{s.Size}";
                l.Modulate = s.Count * 2 <= s.Size ? new Color(1f, 0.45f, 0.35f) : new Color(0.9f, 0.85f, 0.7f);
                l.PixelSize = 0.006f + camPos.DistanceTo(top) * 0.00006f;
                l.GlobalPosition = top + Vector3.Up * (s.Level > 1 ? 0.8f : 0.2f);
            }
        }
        for (int l = labelsUsed; l < _labels.Count; l++) _labels[l].Visible = false;
        foreach (var f in sim.W.Food)
        {
            if (f.Kind != FoodKind.Corpse || !f.Alive) continue;
            if (!VisibleToPlayer(f.Pos) && f.Team != Local.Me) continue;
            var st = Config.Of(f.AntType);
            var n = hf.Normal(f.Pos.X, f.Pos.Y);
            float s = st.Size * Mathf.Lerp(0.55f, 1f, f.Amount / f.Max);
            var b = Orient(n, f.Heading, s);
            b = new Basis(-b.X, -b.Y, b.Z);
            var o = new Vector3(f.Pos.X, hf.Sample(f.Pos.X, f.Pos.Y) + 0.24f * s, f.Pos.Y);
            _dead[f.Team, (int)f.AntType].Add(b, o, new Color(0.25f, 0, 0, 0));
        }
        foreach (var z in sim.Webs)
        {
            if (!VisibleToPlayer(z.Pos)) continue;
            float fade = Mathf.Clamp(z.Life / 1.2f, 0, 1) * Mathf.Clamp((z.Max - z.Life) * 5f + 0.2f, 0, 1);
            var wo = new Vector3(z.Pos.X, hf.Sample(z.Pos.X, z.Pos.Y) + 0.12f, z.Pos.Y);
            _webs.Add(new Basis(Vector3.Up, z.Pos.X * 1.7f) * Basis.FromScale(new Vector3(z.R * 2.3f, 1, z.R * 2.3f)), wo, new Color(fade, 0, 0, 0));
        }
        foreach (var (from, to, t) in sim.WebShots())
        {
            var p = from.Lerp(to, t);
            var wo = new Vector3(p.X, hf.Sample(p.X, p.Y) + 1.2f + 4f * t * (1 - t) * 3f, p.Y);
            _blobs.Add(Basis.FromScale(Vector3.One * 1.6f), wo, new Color(0.95f, 0, 0, 0));
        }
        foreach (var b in _live) b?.End();
        foreach (var b in _dead) b?.End();
        _crumbs.End(); _rings.End(); _bars.End(); _pips.End();
        foreach (var w in _wings.Values) w.End();
        _cocoons.End(); _webs.End(); _blobs.End();
    }
}
