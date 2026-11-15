using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Every piece of silk on the surface, drawn from the modelled set `assets/silk/silk.gltf`
/// (`tools/blender/silk_models.py`, targets S1-S3 in `docs/art-targets/README-silk.md`):
/// the spiders' thrown and standing webs (`Sim.Webs`) as snare tents standing off the ground, the gob in flight,
/// the bands and tension lines on every creature held by silk (`Ant.Root`, `Ant.PinT`), the colonies' trap webs
/// (`Sim.Traps`) slung between grass stems, and the spinnery with its silk ants.
/// One silk shader for all of it: ropes, fibrous sheets that tear into holes as a web wears, and beads of glue.
public sealed class SilkWorks
{
    const string Scene = "res://assets/silk/silk.gltf";

    public const string SilkShader = @"
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_prepass_alpha;
varying float v_kind;
varying float v_integ;
varying vec3 v_lp;
uniform sampler2D fibre : source_color, repeat_enable, filter_linear_mipmap_anisotropic;
float h21(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vn(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y);
}
float thread(float x, float w) { return smoothstep(1.0 - w, 1.0, 1.0 - abs(sin(x))); }
void vertex() {
    v_kind = COLOR.a;
    v_integ = INSTANCE_CUSTOM.x;
    // something is struggling in it: the whole web shivers, most where it hangs free
    float sh = INSTANCE_CUSTOM.y;
    float free = smoothstep(0.0, 0.35, VERTEX.y);
    VERTEX.x += sh * 0.035 * sin(TIME * 41.0 + VERTEX.y * 9.0 + VERTEX.z * 6.0) * free;
    VERTEX.z += sh * 0.035 * cos(TIME * 37.0 + VERTEX.x * 7.0) * free;
    // and it breathes a little in the air always
    VERTEX.y += 0.012 * sin(TIME * 1.7 + VERTEX.x * 2.3 + VERTEX.z * 1.9) * free;
    v_lp = VERTEX;
}
void fragment() {
    vec3 col = COLOR.rgb;
    float alpha = 1.0;
    float rough = 0.5;
    float spec = 0.4;
    vec3 glow = vec3(0.0);
    if (v_kind < 0.25) {
        // a bead of glue: wet, bright, a pinpoint of the sun in it
        rough = 0.05; spec = 1.0;
        col = vec3(1.0, 0.9, 0.68);
        glow = vec3(0.75, 0.55, 0.22);
        alpha = step(0.12, v_integ);
    } else if (v_kind < 0.75) {
        // a sheet: real silk fibre (a photographed weave, assets/silk/silk_fibre.png) at two scales, thin in places,
        // torn open in others; the spinnery's walls (kind above 0.5) are woven thick
        vec2 uv = UV;
        float f1 = texture(fibre, uv * 0.42).r;
        float f2 = texture(fibre, uv.yx * 1.05 + 0.37).r;
        float fib = max(f1, f2 * 0.7);
        float dense = clamp((v_kind - 0.5) * 6.0, 0.0, 1.0);
        float dens = vn(uv * vec2(1.6, 2.4) + 4.0);
        float veil = mix(0.0, 0.18, smoothstep(0.35, 0.8, dens)) + dense * 0.4;
        float hole = vn(uv * vec2(2.6, 3.8) + 11.0) * 0.7 + vn(uv * vec2(8.0, 10.0)) * 0.3;
        float open = smoothstep(hole - 0.06, hole + 0.06, v_integ * 0.9 + 0.02);
        alpha = clamp(smoothstep(0.22, 0.75, fib) * 0.9 + veil, 0.0, 0.94) * open;
        // gauze is lit through, evenly and warm: most of its light is its own, so the sky's blue never tints it
        float glint = smoothstep(0.82, 0.97, f1) * step(0.6, h21(floor(uv * 90.0)));
        vec3 warm = col * vec3(0.7, 0.54, 0.32); // sunlit silk is honey-cream, never white
        glow = warm * (0.2 + 0.42 * fib) + vec3(0.6, 0.42, 0.16) * glint;
        col *= 0.3 + 0.3 * fib;
        spec = 0.0;
        rough = 1.0;
    } else {
        // a rope: twisted strands; the last of it goes when the web is nearly gone
        float tw = abs(sin(v_lp.x * 60.0 + v_lp.y * 85.0 + v_lp.z * 70.0));
        col *= (0.62 + 0.18 * tw) * vec3(1.0, 0.88, 0.66);
        glow = col * 0.12;
        spec = 0.15;
        alpha = step(h21(floor(v_lp.xz * 2.5)) * 0.3, v_integ);
    }
    ALBEDO = col;
    ROUGHNESS = rough;
    SPECULAR = spec;
    EMISSION = glow;
    BACKLIGHT = vec3(0.6, 0.45, 0.25);
    ALPHA = alpha;
}
";

    /// A whole photographed web on one surface (the trap hammock, the snare dome): its own colour and its own gaps,
    /// lit through like gauze; it tears open from the thin places as the web wears.
    public const string CardShader = @"
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_prepass_alpha;
uniform sampler2D web : source_color, filter_linear_mipmap_anisotropic;
varying float v_integ;
float h21(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vn(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y);
}
void vertex() {
    v_integ = INSTANCE_CUSTOM.x;
    float sh = INSTANCE_CUSTOM.y;
    float free = smoothstep(0.0, 0.3, VERTEX.y);
    VERTEX.x += sh * 0.035 * sin(TIME * 41.0 + VERTEX.y * 9.0 + VERTEX.z * 6.0) * free;
    VERTEX.z += sh * 0.035 * cos(TIME * 37.0 + VERTEX.x * 7.0) * free;
    VERTEX.y += 0.012 * sin(TIME * 1.7 + VERTEX.x * 2.3 + VERTEX.z * 1.9) * free;
}
void fragment() {
    vec4 t = texture(web, UV);
    float hole = vn(UV * vec2(5.0, 4.0) + 3.0) * 0.7 + vn(UV * 13.0) * 0.3;
    float open = smoothstep(hole - 0.07, hole + 0.07, v_integ * 0.95 + 0.03);
    float lum = dot(t.rgb, vec3(0.3, 0.55, 0.15));
    vec3 col = mix(t.rgb, vec3(1.0, 0.9, 0.7) * lum * 1.15, 0.45); // honey-cream, not orange
    ALBEDO = col * 0.35;
    EMISSION = col * 0.5;
    ROUGHNESS = 0.6;
    SPECULAR = 0.1;
    BACKLIGHT = vec3(0.5, 0.38, 0.2);
    ALPHA = clamp(t.a * 1.5, 0.0, 1.0) * open;
}
";

    sealed class Bat
    {
        readonly MultiMesh _mm;
        int _n;
        public Bat(Node3D root, Mesh mesh, Material mat, int cap, string name)
        {
            _mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = mesh, InstanceCount = cap, VisibleInstanceCount = 0 };
            root.AddChild(new MultiMeshInstance3D
            {
                Name = name, Multimesh = _mm, MaterialOverride = mat,
                CustomAabb = new Aabb(new Vector3(-50, -20, -50), new Vector3(Config.MapSize + 100, 80, Config.MapSize + 100)),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            });
        }
        public void Begin() => _n = 0;
        public void Add(in Basis b, in Vector3 o, float integrity, float shake)
        {
            if (_n >= _mm.InstanceCount) return;
            _mm.SetInstanceTransform(_n, new Transform3D(b, o));
            _mm.SetInstanceCustomData(_n, new Color(integrity, shake, 0, 0));
            _n++;
        }
        public void End() => _mm.VisibleInstanceCount = _n;
    }

    readonly GameSession _g;
    readonly Node3D _root = new() { Name = "SilkWorks" };
    readonly bool _ok;
    readonly Bat[] _tents = new Bat[3], _tentStems = new Bat[3], _tentCards = new Bat[3], _hammockCards = new Bat[2], _strands = new Bat[3], _hammocks = new Bat[2], _hammockStems = new Bat[2];
    readonly Bat _wrap, _shot;
    static readonly string[] WorkName = { "spinnery", "post", "tower", "trapnest" }; // by SilkKind
    readonly Bat[] _work = new Bat[4], _workStems = new Bat[4];
    readonly List<Vector3>[] _perch = { new(), new(), new(), new() }; // where the silk ants stand on each structure (model space)
    MeshInstance3D _reach, _spot;
    // the spinnery lifted to 3D from the approved concept (assets/silk/spinnery.glb), when it is there
    Bat _spinScan;
    float _scanScale = 1;
    Vector3 _scanOff;
    const string SpinScan = "res://assets/silk/spinnery.glb";
    const float SpinHeight = 6.2f;
    StandardMaterial3D _spotMat;
    /// The structure the player is about to place: drawn as a pale ghost where the pointer is, with the ground the
    /// silk ants can reach ringed round the mound.
    public (bool on, bool trap, SilkKind kind, Vector2 pos, bool ok) Ghost;
    readonly List<(Vector3 from, Vector3 to, float t)> _flings = new();
    float _clock;

    public SilkWorks(GameSession g)
    {
        _g = g;
        g.AddChild(_root);
        if (!ResourceLoader.Exists(Scene)) return;
        var scene = GD.Load<PackedScene>(Scene).Instantiate<Node3D>();
        var meshes = new Dictionary<string, Mesh>();
        foreach (var n in scene.FindChildren("*", "MeshInstance3D", true, false)) if (n is MeshInstance3D mi) meshes[mi.Name] = mi.Mesh;
        foreach (var n in scene.FindChildren("perch_*", "Node3D", true, false))
            if (n is Node3D p3 && n is not MeshInstance3D)
                for (int k = 0; k < 4; k++) if (p3.Name.ToString().StartsWith("perch_" + WorkName[k] + "_")) _perch[k].Add(p3.Position);
        scene.QueueFree();
        var silk = new ShaderMaterial { Shader = Shaders.Get(SilkShader) };
        if (ResourceLoader.Exists("res://assets/silk/silk_fibre.png")) silk.SetShaderParameter("fibre", GD.Load<Texture2D>("res://assets/silk/silk_fibre.png"));
        var stem = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.7f, CullMode = BaseMaterial3D.CullModeEnum.Disabled, BacklightEnabled = true, Backlight = new Color(0.12f, 0.16f, 0.04f) };
        Bat B(string name, Material m, int cap) => new(_root, meshes[name], m, cap, name);
        ShaderMaterial Card(string tex)
        {
            var m = new ShaderMaterial { Shader = Shaders.Get(CardShader) };
            if (ResourceLoader.Exists(tex)) m.SetShaderParameter("web", GD.Load<Texture2D>(tex));
            return m;
        }
        var snareCard = Card("res://assets/silk/web_snare.png");
        var trapCard = Card("res://assets/silk/web_trap.png");
        for (int k = 0; k < 3; k++) _tentCards[k] = B($"tent_{"abc"[k]}_card", snareCard, 48);
        for (int k = 0; k < 2; k++) _hammockCards[k] = B($"hammock_{"ab"[k]}_card", trapCard, 16);
        for (int k = 0; k < 3; k++) { _tents[k] = B($"tent_{"abc"[k]}_silk", silk, 48); _tentStems[k] = B($"tent_{"abc"[k]}_stem", stem, 48); _strands[k] = B($"strand_{"abc"[k]}_silk", silk, 512); }
        for (int k = 0; k < 2; k++) { _hammocks[k] = B($"hammock_{"ab"[k]}_silk", silk, 16); _hammockStems[k] = B($"hammock_{"ab"[k]}_stem", stem, 16); }
        _wrap = B("wrap_silk", silk, 256);
        _shot = B("shot_silk", silk, 32);
        for (int k = 0; k < 4; k++) { _work[k] = B(WorkName[k] + "_silk", silk, 12); _workStems[k] = B(WorkName[k] + "_stem", stem, 12); }
        if (ResourceLoader.Exists(SpinScan))
        {
            var scan = GD.Load<PackedScene>(SpinScan).Instantiate<Node3D>();
            foreach (var n in scan.FindChildren("*", "MeshInstance3D", true, false))
                if (n is MeshInstance3D mi && mi.Mesh != null && _spinScan == null)
                {
                    var box = mi.Mesh.GetAabb();
                    _scanScale = SpinHeight / Mathf.Max(box.Size.Y, 0.001f);
                    _scanOff = new Vector3(-(box.Position.X + box.Size.X / 2), -box.Position.Y, -(box.Position.Z + box.Size.Z / 2));
                    // the scan's own texture, warmed and dulled: sunlit silk is honey-cream and soft, never white plastic
                    Material scanMat = null;
                    if (mi.Mesh.SurfaceGetMaterial(0) is BaseMaterial3D own)
                    {
                        var m = (BaseMaterial3D)own.Duplicate();
                        m.AlbedoColor = new Color(0.88f, 0.83f, 0.72f);
                        m.Roughness = 0.9f; m.Metallic = 0; m.MetallicSpecular = 0.1f;
                        m.BacklightEnabled = true; m.Backlight = new Color(0.2f, 0.15f, 0.08f);
                        scanMat = m;
                    }
                    _spinScan = new Bat(_root, mi.Mesh, scanMat, 12, "spinnery_scan");
                }
            scan.QueueFree();
        }
        // placement markers: the reach of the silk ants round the mound, and the spot under the ghost
        var ringMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1f, 0.82f, 0.4f, 0.55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, NoDepthTest = true };
        _reach = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = Config.SilkReach - 0.25f, OuterRadius = Config.SilkReach, Rings = 96, RingSegments = 4 }, MaterialOverride = ringMat, Scale = new Vector3(1, 0.02f, 1), Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _spotMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, NoDepthTest = true };
        _spot = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 2.3f, OuterRadius = 2.7f, Rings = 40, RingSegments = 4 }, MaterialOverride = _spotMat, Scale = new Vector3(1, 0.02f, 1), Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _root.AddChild(_reach); _root.AddChild(_spot);
        _ok = true;
    }

    static float Hash(Vector2 p) => Mathf.PosMod(Mathf.Sin(p.X * 12.9898f + p.Y * 78.233f) * 43758.547f, 1f);

    /// A thread flung by a web-throwing soldier at a flyer: it hangs in the air for a moment between them.
    public void Fling(Vector3 from, Vector3 to) { if (_ok && _flings.Count < 24) _flings.Add((from, to, 0)); }

    /// One taut strand from a to b (the strand mesh runs from its origin one unit along -Z).
    void Strand(int variant, Vector3 a, Vector3 b, float thick, float integrity, float shake)
    {
        var d = b - a;
        float len = d.Length();
        if (len < 0.05f) return;
        var z = -d / len;
        var x = (Mathf.Abs(z.Y) > 0.95f ? Vector3.Right : Vector3.Up).Cross(z).Normalized();
        var y = z.Cross(x);
        _strands[variant % 3].Add(new Basis(x * thick, y * thick, z * len), a, integrity, shake);
    }

    public void Update(float dt)
    {
        if (!_ok) return;
        _clock += dt;
        var sim = _g.Sim;
        var hf = _g.World.HF;
        var seen = _g.Ants.VisibleToPlayer;
        foreach (var b in _tents) b.Begin();
        foreach (var b in _tentStems) b.Begin();
        foreach (var b in _tentCards) b.Begin();
        foreach (var b in _hammockCards) b.Begin();
        foreach (var b in _strands) b.Begin();
        foreach (var b in _hammocks) b.Begin();
        foreach (var b in _hammockStems) b.Begin();
        _wrap.Begin(); _shot.Begin(); _spinScan?.Begin();
        for (int k = 0; k < 4; k++) { _work[k].Begin(); _workStems[k].Begin(); }

        // the spiders' webs: thrown ones and the standing ones round a lair
        foreach (var z in sim.Webs)
        {
            if (!seen(z.Pos)) continue;
            float h = Hash(z.Pos);
            float integ = Mathf.Clamp(z.Life / 1.5f, 0, 1) * Mathf.Clamp((z.Max - z.Life) * 3f + 0.25f, 0, 1);
            float rise = Mathf.Clamp((z.Max - z.Life) * 4f + 0.3f, 0.3f, 1f); // it unrolls as it lands
            var tb = new Basis(Vector3.Up, h * Mathf.Tau) * Basis.FromScale(new Vector3(z.R * 1.05f, z.R * 1.05f * rise, z.R * 1.05f));
            _tents[(int)(h * 3) % 3].Add(tb, hf.Point(z.Pos, 0.02f), integ, 0.12f);
            _tentStems[(int)(h * 3) % 3].Add(tb, hf.Point(z.Pos, 0.02f), 1, 0);
            _tentCards[(int)(h * 3) % 3].Add(tb, hf.Point(z.Pos, 0.02f), integ, 0.12f);
        }

        // the gob in flight
        foreach (var (from, to, t) in sim.WebShots())
        {
            var p = from.Lerp(to, t);
            if (!seen(p)) continue;
            var wo = new Vector3(p.X, hf.Sample(p.X, p.Y) + 1.2f + 12f * t * (1 - t), p.Y);
            var dir = new Vector3(to.X - from.X, 12f * (1 - 2 * t), to.Y - from.Y).Normalized();
            _shot.Add(Basis.LookingAt(dir, Vector3.Up) * Basis.FromScale(Vector3.One * 1.5f) * new Basis(Vector3.Forward, _clock * 9f), wo, 1, 0.5f);
        }

        // everything held by silk: bands over its body, tension lines to the ground around it
        for (int i = 0; i < sim.High; i++)
        {
            ref var a = ref sim.Ants[i];
            if (!a.Alive || a.Layer != 0 || (a.Root <= 0 && a.PinT <= 0)) continue;
            if (a.Team != Local.Me && !seen(a.Pos)) continue;
            float left = Mathf.Max(a.Root, a.PinT);
            float integ = Mathf.Clamp(left * 2.5f, 0, 1);
            float gy = hf.Sample(a.Pos.X, a.Pos.Y);
            var o = new Vector3(a.Pos.X, gy + a.Alt, a.Pos.Y);
            _wrap.Add(AntRenderer.Orient(Vector3.Up, a.Heading, a.Size * 1.2f), o, integ, 1f);
            var back = o + Vector3.Up * 0.3f * a.Size;
            int lines = a.PinT > 0 ? 6 : 5;
            for (int k = 0; k < lines; k++)
            {
                float ang = (k + Hash(new Vector2(i, k))) * Mathf.Tau / lines;
                float r = a.Size * (1.0f + 0.7f * Hash(new Vector2(k, i * 3)));
                var at = new Vector2(a.Pos.X + Mathf.Cos(ang) * r, a.Pos.Y + Mathf.Sin(ang) * r);
                var side = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * 0.16f * a.Size;
                Strand(i + k, back + side, hf.Point(at, 0.02f), 1f + 0.25f * a.Size, integ, 1f);
            }
        }

        // threads flung at flyers by web-throwing soldiers
        for (int k = _flings.Count - 1; k >= 0; k--)
        {
            var f = _flings[k];
            f.t += dt;
            if (f.t > 0.9f) { _flings.RemoveAt(k); continue; }
            _flings[k] = f;
            float reach = Mathf.Clamp(f.t / 0.12f, 0, 1);
            Strand(k, f.from, f.from.Lerp(f.to, reach), 1.6f, Mathf.Clamp((0.9f - f.t) * 3f, 0, 1), 0.6f);
        }

        // the colonies' trap webs: a hammock across the approach, slung between its stems
        for (int k = 0; k < sim.Traps.Count; k++)
        {
            var z = sim.Traps[k];
            if (z.Team != Local.Me && !seen(z.Pos)) continue;
            var owner = sim.WorkById(z.Work);
            var along = z.Pos - (owner?.Pos ?? sim.NestEntrance(z.Team));
            float yaw = Mathf.Atan2(-along.X, -along.Y) + (Hash(z.Pos) - 0.5f) * 0.5f;
            int v = Hash(z.Pos * 1.7f) < 0.5f ? 0 : 1;
            float grow = Mathf.Clamp(z.Age / 1.6f, 0, 1);
            float integ = grow * (0.4f + 0.2f * z.Charges);
            float shake = Mathf.Exp(-z.CatchT * 1.2f);
            var o = hf.Point(z.Pos, 0.02f);
            var b = new Basis(Vector3.Up, yaw) * Basis.FromScale(Vector3.One * z.R);
            _hammocks[v].Add(b, o, integ, shake);
            _hammockCards[v].Add(b, o, integ, shake);
            _hammockStems[v].Add(b, o, 1, 0);
        }

        // the silk structures: spinneries, web posts, web towers, trap nests
        foreach (var w in sim.Works)
        {
            if (w.Team != Local.Me && !seen(w.Pos)) continue;
            var o = hf.Point(w.Pos, 0.02f);
            var b = new Basis(Vector3.Up, Hash(w.Pos) * Mathf.Tau);
            float shake = (!w.Built && w.WeaverIn) || w.Upgrading ? 0.3f : 0.04f;
            float integ = Mathf.Clamp(w.Done, 0.05f, 1f);
            if (w.Upgrading)
            {
                // the post is woven over into what it becomes
                _work[(int)w.UpTo].Add(b, o, Mathf.Clamp(w.UpDone, 0.05f, 1f), shake);
                _workStems[(int)w.UpTo].Add(b * Basis.FromScale(new Vector3(1, Mathf.Clamp(w.UpDone * 1.5f, 0.1f, 1f), 1)), o, 1, 0);
                integ = 1 - w.UpDone;
            }
            if (w.Kind == SilkKind.Spinnery && _spinScan != null)
            {
                // the scanned pavilion rises as it is woven
                float grow = _scanScale * Mathf.Lerp(0.25f, 1f, Mathf.SmoothStep(0, 1, w.Done));
                var sb = b * Basis.FromScale(Vector3.One * grow);
                _spinScan.Add(sb, o + sb * _scanOff, 1, 0);
                continue;
            }
            _work[(int)w.Kind].Add(b, o, integ, shake);
            _workStems[(int)w.Kind].Add(b, o, 1, 0);
        }

        // the ghost of what is about to be placed
        _reach.Visible = Ghost.on && !Ghost.trap;
        _spot.Visible = Ghost.on;
        if (Ghost.on)
        {
            var o = hf.Point(Ghost.pos, 0.06f);
            _reach.Position = hf.Point(sim.NestEntrance(Local.Me), 0.1f);
            _spot.Position = o;
            _spotMat.AlbedoColor = Ghost.ok ? new Color(0.75f, 1f, 0.45f, 0.85f) : new Color(1f, 0.35f, 0.2f, 0.85f);
            if (Ghost.trap) { _hammocks[0].Add(Basis.FromScale(Vector3.One * Config.TrapRadius), o, Ghost.ok ? 0.5f : 0.25f, 0); _hammockCards[0].Add(Basis.FromScale(Vector3.One * Config.TrapRadius), o, Ghost.ok ? 0.6f : 0.3f, 0); }
            else
            {
                var b = new Basis(Vector3.Up, Hash(Ghost.pos) * Mathf.Tau);
                if (Ghost.kind == SilkKind.Spinnery && _spinScan != null) { var sb = b * Basis.FromScale(Vector3.One * _scanScale); _spinScan.Add(sb, o + sb * _scanOff, 1, 0); }
                else { _work[(int)Ghost.kind].Add(b, o, Ghost.ok ? 0.55f : 0.25f, 0); _workStems[(int)Ghost.kind].Add(b, o, 1, 0); }
            }
        }

        foreach (var x in _tents) x.End();
        foreach (var x in _tentStems) x.End();
        foreach (var x in _tentCards) x.End();
        foreach (var x in _hammockCards) x.End();
        foreach (var x in _strands) x.End();
        foreach (var x in _hammocks) x.End();
        foreach (var x in _hammockStems) x.End();
        _wrap.End(); _shot.End(); _spinScan?.End();
        for (int k = 0; k < 4; k++) { _work[k].End(); _workStems[k].End(); }
    }

    static readonly List<Vector3> ScanPerch = new() { new Vector3(2.3f, 0, 0.6f), new Vector3(-1.9f, 0, 1.4f), new Vector3(0.4f, 0, -2.4f) };

    /// The silk ants that live on each spinnery, drawn into the ant batches (called from AntRenderer's frame).
    internal void DrawAnts(AntRenderer r)
    {
        if (!_ok) return;
        var sim = _g.Sim;
        foreach (var w in sim.Works)
        {
            if (w.Team != Local.Me && !r.VisibleToPlayer(w.Pos)) continue;
            var o = _g.World.HF.Point(w.Pos, 0.02f);
            float yaw = Hash(w.Pos) * Mathf.Tau;
            var b = new Basis(Vector3.Up, yaw);
            var perch = w.Kind == SilkKind.Spinnery && _spinScan != null ? ScanPerch : _perch[(int)w.Kind];
            int ants = w.Built ? perch.Count : w.WeaverIn ? System.Math.Min(1, perch.Count) : 0;
            for (int k = 0; k < ants; k++)
            {
                var at = perch[k];
                float heading = Mathf.Atan2(at.Z, at.X) + (at.Y > 3f ? 1f : 0f); // facing out
                float t = _clock * 0.5f + k * 1.7f + w.Id;
                var data = AntRenderer.Pack(k * 5 + w.Id, 3, t * 0.2f, 0.4f * Mathf.Sin(t + k), -0.1f + 0.06f * Mathf.Sin(t * 2.3f), 0.25f, false, 0.2f * Mathf.Max(0, Mathf.Sin(t * 4f)));
                r.AddWorker(w.Team, AntRenderer.Orient(Vector3.Up, heading - yaw, 1.45f), o + b * at, data, 0);
            }
        }
    }
}
