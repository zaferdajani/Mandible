using System.Collections.Generic;
using Godot;

namespace Mandible;

/// PLACEHOLDER visuals for the mutation track, until the owner approves targets H1-H5 (docs/art-targets/
/// REVIEW-mutation.jpg) and the real assets replace them: a faint violet-green light that pulses where mutagen lies
/// (on the dead queen wasp's body, then in its carrier's jaws), the sixth-sense ripple over a source the colony feels
/// through the fog, and the light of the royal larva and its throbbing cocoon in the nursery. No geometry is made
/// here; the queen wasp's body is the game's own wasp mesh.
public sealed class MutagenGlow
{
    readonly GameSession _g;
    readonly Node3D _root = new() { Name = "MutagenGlow" };
    readonly Dictionary<int, (Node3D node, OmniLight3D light, MeshInstance3D body)> _sources = new();
    OmniLight3D _nursery;
    float _t;
    static readonly Color Glow = new(0.72f, 0.55f, 1f);
    static readonly Color Green = new(0.6f, 1f, 0.7f);

    public MutagenGlow(GameSession g) { _g = g; g.AddChild(_root); }

    // the sixth-sense ripple: a soft, thin, broken ring of warm violet light that swells and fades, never a hard disc
    const string RippleShader = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled;
uniform float t = 0.0;
uniform vec3 col : source_color = vec3(0.7, 0.55, 1.0);
void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float r = length(p);
    float a = atan(p.y, p.x);
    float band = exp(-pow((r - 0.85) * 14.0, 2.0));
    float broken = 0.55 + 0.45 * sin(a * 9.0 + t * 4.0) * sin(a * 4.0 - t * 2.0);
    ALBEDO = col;
    ALPHA = band * broken * (1.0 - t) * (1.0 - t) * 0.16;
}
";
    // the radiating core: a camera-facing halo of light (a hot heart, a soft corona and slow faint rays) over the gland,
    // sized and brightened by the core's strength so a weak core reads dim and a queen's core blazes. Light only: the
    // core's body (a faceted organic seed, target M2) waits for the owner's approval of docs/art-targets/REVIEW-mutation-core.jpg.
    const string HaloShader = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
uniform float t = 0.0;
uniform float strength = 1.0;
uniform float patch_ = 0.0;   // 1 while the core is still in the carcass: the ruptured patch's veins radiate round it
uniform sampler2D veins : source_color, filter_linear_mipmap;
uniform vec3 col : source_color = vec3(0.72, 0.55, 1.0);
uniform vec3 hot : source_color = vec3(0.7, 1.0, 0.75);
void vertex() {
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
    MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(vec4(length(MODEL_MATRIX[0].xyz), 0, 0, 0), vec4(0, length(MODEL_MATRIX[1].xyz), 0, 0), vec4(0, 0, length(MODEL_MATRIX[2].xyz), 0), vec4(0, 0, 0, 1));
}
void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float r = length(p);
    float a = atan(p.y, p.x);
    float beat = 0.5 + 0.5 * sin(t * (2.0 + strength));
    float heart = exp(-r * r * 60.0) * (0.8 + 0.4 * beat);
    float corona = exp(-r * r * 7.0) * (0.25 + 0.2 * beat);
    float rays = pow(max(0.0, sin(a * 6.0 + t * 0.6) * sin(a * 3.0 - t * 0.4)), 6.0) * exp(-r * 3.2) * 0.35 * strength / 3.0;
    vec3 c = mix(col, hot, clamp(heart * 1.4 + 0.3 * sin(t * 0.9), 0.0, 1.0));
    // the veins: the generated vein map, slowly turning, throbbing outward from the centre with the beat
    float ang = t * 0.05;
    vec2 q = mat2(vec2(cos(ang), -sin(ang)), vec2(sin(ang), cos(ang))) * p * (1.15 - 0.25 * patch_);
    vec4 v = texture(veins, q * 0.5 + 0.5);
    float wave = 0.6 + 0.4 * sin(r * 9.0 - t * (2.0 + strength));
    float vein = v.a * wave * patch_ * (0.45 + 0.25 * strength);
    ALBEDO = mix(c, v.rgb, clamp(vein * 1.5, 0.0, 1.0));
    ALPHA = clamp((heart + corona + rays) * (0.35 + 0.22 * strength) * (1.0 - 0.4 * patch_) + vein, 0.0, 1.0) * smoothstep(1.0, 0.7, r);
}
";
    readonly Dictionary<int, MeshInstance3D> _halos = new();
    MeshInstance3D Halo(int id)
    {
        if (_halos.TryGetValue(id, out var h)) return h;
        var mat = new ShaderMaterial { Shader = Shaders.Get(HaloShader), RenderPriority = 21 };
        if (ResourceLoader.Exists(VeinPath)) mat.SetShaderParameter("veins", GD.Load<Texture2D>(VeinPath));
        h = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _root.AddChild(h);
        return _halos[id] = h;
    }

    // ---- target M1: the ruptured patch. Glowing veins (a generated vein map, assets/mutation/core/veins.png) projected
    // down onto the body and the soil round the gland while it is still in the carcass; their reach and fire follow
    // the core's strength. Target M2: once carved free the gland is the radiating core, a generated faceted organic
    // seed (assets/mutation/core/core.glb, Meshy image-to-3D from the approved target), its heart and veins glowing.
    const string CorePath = "res://assets/mutation/core/core.glb", VeinPath = "res://assets/mutation/core/veins.png";
    readonly Dictionary<int, Decal> _veins = new();
    readonly Dictionary<int, (Node3D node, StandardMaterial3D mat)> _coreMeshes = new();
    float _coreScale = -1;

    Decal Veins(int id)
    {
        if (_veins.TryGetValue(id, out var d)) return d;
        if (!ResourceLoader.Exists(VeinPath)) return _veins[id] = null;
        var tex = GD.Load<Texture2D>(VeinPath);
        d = new Decal { TextureAlbedo = tex, TextureEmission = tex, Modulate = new Color(1, 1, 1), AlbedoMix = 0.35f, UpperFade = 0.3f, LowerFade = 0.3f, CullMask = ~0u };
        _root.AddChild(d);
        return _veins[id] = d;
    }

    (Node3D node, StandardMaterial3D mat) CoreMesh(int id)
    {
        if (_coreMeshes.TryGetValue(id, out var c)) return c;
        if (!ResourceLoader.Exists(CorePath)) return _coreMeshes[id] = (null, null);
        var node = GD.Load<PackedScene>(CorePath).Instantiate<Node3D>();
        StandardMaterial3D mat = null;
        foreach (var n in node.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)n;
            var src = mi.GetActiveMaterial(0) as StandardMaterial3D;
            // the scan's own colours, made to glow from within: bright texels (the heart, the veins) emit most
            mat ??= new StandardMaterial3D
            {
                AlbedoTexture = src?.AlbedoTexture, Roughness = 0.25f, MetallicSpecular = 0.7f,
                EmissionEnabled = true, EmissionTexture = src?.AlbedoTexture, EmissionOperator = BaseMaterial3D.EmissionOperatorEnum.Multiply,
                RimEnabled = true, Rim = 0.6f, RimTint = 0.8f,
            };
            mi.MaterialOverride = mat;
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            mi.Layers = MutagenProps.SkinLayer; // the core's own light falls on the jaws and soil, not back on itself
            if (_coreScale < 0) { var aabb = mi.GetAabb(); _coreScale = 1f / Mathf.Max(0.001f, aabb.GetLongestAxisSize()); }
        }
        _root.AddChild(node);
        return _coreMeshes[id] = (node, mat);
    }

    /// How hard a core of this strength radiates (1 dim, 2 strong, 3 a queen's blaze).
    public static float Radiance(int strength) => strength switch { <= 1 => 0.55f, 2 => 1f, _ => 1.6f };

    readonly Dictionary<int, MeshInstance3D> _rings = new();
    MeshInstance3D Ring(int id)
    {
        if (_rings.TryGetValue(id, out var r)) return r;
        var mat = new ShaderMaterial { Shader = Shaders.Get(RippleShader), RenderPriority = 20 };
        r = new MeshInstance3D { Mesh = new PlaneMesh { Size = Vector2.One }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _root.AddChild(r);
        return _rings[id] = r;
    }

    public void Update(float dt)
    {
        var sim = _g.Sim;
        _t += dt;
        foreach (var m in sim.Mutagens)
        {
            // a stronger core beats faster and harder
            float pulse = 0.5f + 0.5f * Mathf.Sin(_t * (1.8f + 0.6f * m.Strength) + m.Id);
            float rad = Radiance(m.Strength);
            if (!_sources.TryGetValue(m.Id, out var s))
            {
                var node = new Node3D();
                var light = new OmniLight3D { OmniRange = 3.2f, LightEnergy = 0.8f, ShadowEnabled = false, LightColor = Glow, LightCullMask = ~MutagenProps.SkinLayer };
                node.AddChild(light);
                MeshInstance3D body = null;
                if (m.From is AntType.Wasp && !ResourceLoader.Exists("res://assets/mutation/queen_wasp/queen_wasp.gltf")) // the modelled wasp: MutagenProps
                {
                    body = new MeshInstance3D { Mesh = AntMesh.Build(AntType.Wasp), MaterialOverride = AntRenderer.AntMat(Shaders.Get(Shaders.Ant), Config.Wild, AntType.Wasp, true), Scale = Vector3.One * Config.Of(AntType.Wasp).Size * 1.6f };
                    node.AddChild(body);
                }
                _root.AddChild(node);
                _sources[m.Id] = s = (node, light, body);
            }
            // a radiating core shows wherever the colony sees it or feels it (the sixth sense), as its gland prop does
            bool seen = _g.Fog == null || _g.Fog.Visible(m.Pos) || m.Carried || sim.Senses(Local.Me, m) || _g.Fog.Explored(m.Pos);
            s.node.Visible = !m.Home && seen;
            if (m.Home) continue;
            // never inside a mesh: a light at zero distance blows its neighbours out to white
            s.node.Position = (_g.Props?.GlandPoint(m.Id) ?? _g.World.HF.Point(m.Pos, 0.2f)) + Vector3.Up * (m.Free ? 0.9f : 1.3f);
            s.node.Rotation = new Vector3(0, -m.Heading, 0);
            if (s.body != null) s.body.Visible = !m.Free;
            // a subtle radiance, never a lamp: it breathes slowly between violet and green
            s.light.LightColor = Glow.Lerp(Green, 0.5f + 0.5f * Mathf.Sin(_t * 0.9f));
            // in the web it lights the silk around her; in a carrier's jaws only the ants' heads and the soil beside it
            // from play zoom the sun drowns a small light: the pool on the soil grows as the camera pulls back
            float zoom = Mathf.Clamp((_g.Camera.Distance - 14f) / 46f, 0, 1);
            s.light.OmniRange = (m.Free ? 2.4f : 2.6f) * (0.75f + 0.25f * rad) * (1 + 0.9f * zoom);
            s.light.LightEnergy = (0.45f + 0.55f * pulse) * rad * (1 + 2.5f * zoom); // a pool of light on the carrier's head and the soil
            // the core's halo: on the body it marks the ruptured patch; carved out it radiates in the carrier's jaws,
            // and it grows with the camera's distance so the core stays readable from play zoom
            var veins = Veins(m.Id);
            if (veins != null)
            {
                // the ruptured patch: veins spread over the body while the gland is in it, brighter and wider for a stronger core
                veins.Visible = s.node.Visible && !m.Free;
                float span = (1.6f + 0.8f * m.Strength) * (1 + 0.5f * zoom);
                veins.Size = new Vector3(span, 3.5f, span);
                veins.Position = s.node.Position + Vector3.Down * 0.3f; // centred on the gland: on the island queen's body, or round the wasp up in her web
                veins.RotationDegrees = new Vector3(0, m.Id * 47f, 0);
                veins.EmissionEnergy = (0.5f + 0.9f * pulse) * rad * (1 + 1.5f * zoom);
            }
            var cm = CoreMesh(m.Id);
            if (cm.node != null)
            {
                cm.node.Visible = s.node.Visible && m.Free;
                if (m.Free)
                {
                    // seed-sized in the jaws, swelling a little with the camera's distance so it reads at play zoom
                    float size = (0.42f + 0.07f * m.Strength) * (1 + 1.8f * zoom) * _coreScale;
                    var at = _g.World.HF.Point(m.Pos, m.Carried ? 0.55f : 0.3f);
                    cm.node.GlobalTransform = new Transform3D(Basis.FromEuler(new Vector3(0, -m.Heading + Mathf.Pi / 2, 0.25f)).Scaled(Vector3.One * size), at);
                    cm.mat.EmissionEnergyMultiplier = (0.8f + 1.4f * pulse) * rad;
                }
            }
            var halo = Halo(m.Id);
            halo.Visible = s.node.Visible;
            halo.Position = s.node.Position + Vector3.Down * (m.Free ? 0.55f : 1.25f);
            // drawn just in front of the body toward the eye, so the gaster or the jaws never hide the light inside them
            var eye = _g.GetViewport().GetCamera3D();
            if (eye != null) halo.Position += (eye.GlobalPosition - halo.Position).Normalized() * (m.Free ? 0.4f : 0.9f);
            halo.Scale = Vector3.One * (m.Free ? 1.3f : 2.4f + 0.8f * m.Strength) * (0.7f + 0.3f * rad) * (1 + 2.4f * zoom); // never a lost dot at play zoom
            var hm = (ShaderMaterial)halo.MaterialOverride;
            hm.SetShaderParameter("patch_", m.Free ? 0f : 1f);
            hm.SetShaderParameter("t", _t + m.Id);
            hm.SetShaderParameter("strength", (float)m.Strength); // a violet-green pool on the soil round the carrier: the gland is findable from play zoom
            // the sixth sense: a faint ring of disturbed air spreads from a source the colony feels, even through the fog
            // from play zoom a carried gland keeps a faint, slow ripple under it, so the player can follow it across the yard
            bool sensed = (!m.Carried || zoom > 0.5f) && sim.Senses(Local.Me, m);
            var ring = Ring(m.Id);
            ring.Visible = sensed;
            if (sensed)
            {
                float k = (_t % 2.6f) / 2.6f;
                ring.Position = _g.World.HF.Point(m.Pos, 0.15f);
                ring.Scale = Vector3.One * (m.Carried ? 1.5f + 5f * k : 2f + 16f * k);
                ((ShaderMaterial)ring.MaterialOverride).SetShaderParameter("t", k);
            }
        }
        float pulseN = 0.5f + 0.5f * Mathf.Sin(_t * 2.4f);
        // the royal larva glows in its nursery; the cocoon throbs harder, faster as it nears bursting
        var c = sim.Col[Local.Me];
        var room = sim.Nursery(Local.Me);
        bool lit = room != null && c.LarvaStage >= 2;
        if (lit && _nursery == null) { _nursery = new OmniLight3D { OmniRange = 3.6f, ShadowEnabled = false }; _root.AddChild(_nursery); }
        if (_nursery == null) return;
        _nursery.Visible = lit && _g.NestView;
        if (!lit) return;
        _nursery.Position = _g.World.HF.Point(sim.NestEntrance(Local.Me)) + new Vector3(room.Offset.X, -NestGeometry.Depth + 1.8f, room.Offset.Y);
        float beat = c.LarvaStage == 3 ? 0.5f + 0.5f * Mathf.Sin(_t * (3f + 6f * (1 - c.CocoonT / Mathf.Max(1, c.CocoonTime)))) : pulseN;
        _nursery.LightColor = c.LarvaStage == 3 ? new Color(1f, 0.72f, 0.42f).Lerp(Glow, 0.3f) : Glow.Lerp(Green, 0.4f);
        _nursery.LightEnergy = (c.LarvaStage == 3 ? 0.6f : 0.2f) + beat * (c.LarvaStage == 3 ? 0.6f : 0.15f);
    }
}
