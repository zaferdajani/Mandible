using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The moment of mutation (2-4 s, never blinding): an energy burst of a violet-green shockwave ring on the ground, a
/// flash of radiating light, ground cracks that glow and fade, and leaf litter and grit flung outward. A general that
/// mutates or reaches a new insignia tier gets the burst around it; the queen's mutation adds a column of light that
/// rises from the nest into the sky (target M3, docs/art-targets/REVIEW-mutation-core.jpg). Light and particles only:
/// rings, cracks and the column are shaders on flat or open meshes, the debris is the shared particle system.
public sealed class MutationBurst
{
    // a soft, broken ring that races outward and thins as it goes, with a faint inner wash
    const string ShockShader = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
uniform float t = 0.0;
uniform float power = 1.0;
uniform vec3 col : source_color = vec3(0.6, 0.25, 1.0);
uniform vec3 hot : source_color = vec3(0.35, 1.0, 0.55);
void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float r = length(p);
    float a = atan(p.y, p.x);
    float front = 0.12 + 0.88 * (1.0 - pow(1.0 - t, 2.4));
    float w = 0.035 + 0.05 * t;
    float band = exp(-pow((r - front) / w, 2.0));
    float wake = smoothstep(front, front - 0.35, r) * smoothstep(0.0, front, r) * 0.18;
    float broken = 0.7 + 0.3 * sin(a * 13.0 + t * 7.0) * sin(a * 5.0 - t * 3.0);
    float fade = (1.0 - t) * (1.0 - t);
    ALBEDO = mix(col, hot, band * 0.6);
    ALPHA = clamp((band * broken + wake) * fade * (0.45 + 0.15 * power), 0.0, 1.0) * smoothstep(1.0, 0.94, r);
}
";

    // cracks in the soil (cell edges of a jittered grid) that light up as the wave passes and cool to nothing
    const string CrackShader = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
uniform float t = 0.0;
uniform float seed = 0.0;
uniform vec3 col : source_color = vec3(0.6, 0.25, 1.0);
uniform vec3 hot : source_color = vec3(0.4, 1.0, 0.6);
vec2 h2(vec2 p) { p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3))); return fract(sin(p + seed) * 43758.5453); }
void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float r = length(p);
    // stretch the cells along the radius so the cracks run outward from the centre
    float a = atan(p.y, p.x);
    vec2 q = vec2(a * 2.6, r * 7.0);
    vec2 g = floor(q), f = fract(q);
    float d1 = 9.0, d2 = 9.0;
    for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) {
        vec2 o = vec2(float(x), float(y));
        vec2 c = o + h2(g + o) - f;
        float d = dot(c, c);
        if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) { d2 = d; }
    }
    float edge = 1.0 - smoothstep(0.0, 0.07, sqrt(d2) - sqrt(d1));
    float reach = 0.15 + 0.85 * smoothstep(0.0, 0.3, t);
    float lit = smoothstep(reach, reach - 0.12, r) * smoothstep(1.0, 0.55, r);
    float glow = smoothstep(0.0, 0.08, t) * (1.0 - smoothstep(0.25, 1.0, t));
    float flicker = 0.85 + 0.15 * sin(TIME * 23.0 + r * 30.0);
    ALBEDO = mix(col, hot, 0.35 + 0.4 * (1.0 - r));
    ALPHA = clamp(edge * lit * glow * flicker * 0.75, 0.0, 1.0);
}
";

    // the column of light: an open cylinder, brightest in its core seen edge-on, streaks climbing it, soft at its foot
    // and fading into the sky; it shoots up, stands, and thins away
    const string BeamShader = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
uniform float t = 0.0;
uniform float height = 90.0;
uniform float gain = 1.0;
uniform vec3 col : source_color = vec3(0.5, 0.22, 0.95);
uniform vec3 hot : source_color = vec3(0.32, 0.95, 0.5);
varying float v_h;
varying float v_a;
void vertex() { v_h = VERTEX.y / height + 0.5; v_a = atan(VERTEX.z, VERTEX.x); }
void fragment() {
    float facing = abs(dot(NORMAL, VIEW));
    float core = pow(facing, 2.5);
    float rise = smoothstep(0.0, 0.22, t);              // the head of the column climbs in under a second
    float top = smoothstep(rise + 0.02, rise - 0.15, v_h);
    float foot = smoothstep(0.0, 0.04, v_h);
    float sky = 1.0 - smoothstep(0.35, 1.0, v_h) * 0.85;
    float streak = 0.6 + 0.4 * sin(v_a * 7.0 + v_h * 40.0 - TIME * 9.0) * sin(v_a * 3.0 - v_h * 13.0 - TIME * 5.0);
    float life = smoothstep(0.0, 0.06, t) * (1.0 - smoothstep(0.55, 1.0, t));
    ALBEDO = mix(col, hot, clamp(pow(core, 3.0) * 0.8 + 0.15 * sin(TIME * 2.0 + v_h * 6.0), 0.0, 1.0));
    ALPHA = clamp(core * streak * top * foot * sky * life * gain, 0.0, 1.0);
}
";

    sealed class Fx
    {
        public Node3D Root;
        public MeshInstance3D Ring, Ring2, Cracks, Beam, Glow;
        public OmniLight3D Light;
        public float T, Dur, Power, Peak;
        public Vector3 At;
        public bool Queen, Ground;
    }

    readonly Node3D _root = new() { Name = "MutationBursts" };
    readonly List<Fx> _fx = new();
    readonly Effects _effects;
    readonly Rng _rng = new(4711);

    public MutationBurst(Node3D parent, Effects effects) { _effects = effects; parent.AddChild(_root); }

    public int Active => _fx.Count;

    static MeshInstance3D Plane(Node3D parent, string shader, float size, int priority)
    {
        var m = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = Vector2.One * size },
            MaterialOverride = new ShaderMaterial { Shader = Shaders.Get(shader), RenderPriority = priority },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(m);
        return m;
    }

    /// A burst at `at` (ground point). `power` is 1-3 (the core's strength; a general's tier). `ground` false for a
    /// burst in the nest, where no leaves lie and the particles would find the surface above.
    public void Burst(Vector3 at, float power, bool queen, bool ground)
    {
        if (_fx.Count >= 6) return;
        power = Mathf.Clamp(power, 1, 3);
        var fx = new Fx { At = at, Power = power, Queen = queen, Ground = ground, Dur = queen ? 3.8f : 2.6f, Root = new Node3D() };
        _root.AddChild(fx.Root);
        fx.Root.Position = at + Vector3.Up * 0.12f;
        float reach = (queen ? 16f : 7f) + 2.5f * power;
        fx.Ring = Plane(fx.Root, ShockShader, reach * 2, 22);
        fx.Ring2 = Plane(fx.Root, ShockShader, reach * 1.3f, 22);
        fx.Cracks = Plane(fx.Root, CrackShader, reach * 1.1f, 21);
        fx.Cracks.Position = Vector3.Up * 0.03f;
        ((ShaderMaterial)fx.Cracks.MaterialOverride).SetShaderParameter("seed", _rng.R(0, 50));
        ((ShaderMaterial)fx.Ring.MaterialOverride).SetShaderParameter("power", queen ? power + 1.5f : power);
        ((ShaderMaterial)fx.Ring2.MaterialOverride).SetShaderParameter("power", power * 0.6f);
        // the flash: a warm-violet light that swells and decays; capped so it never whites the screen out
        fx.Peak = (queen ? 5f : 2.6f) * (0.7f + 0.15f * power);
        fx.Light = new OmniLight3D { OmniRange = reach * 0.9f, LightEnergy = 0, ShadowEnabled = false, LightColor = new Color(0.74f, 0.56f, 1f) };
        fx.Light.Position = Vector3.Up * 2.2f;
        fx.Root.AddChild(fx.Light);
        if (queen)
        {
            const float h = 90f;
            var cyl = new CylinderMesh { TopRadius = 2.2f, BottomRadius = 2.8f, Height = h, CapTop = false, CapBottom = false, RadialSegments = 24, Rings = 1 };
            fx.Beam = new MeshInstance3D { Mesh = cyl, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, MaterialOverride = new ShaderMaterial { Shader = Shaders.Get(BeamShader), RenderPriority = 23 } };
            ((ShaderMaterial)fx.Beam.MaterialOverride).SetShaderParameter("height", h);
            ((ShaderMaterial)fx.Beam.MaterialOverride).SetShaderParameter("gain", 0.9f);
            fx.Beam.Position = Vector3.Up * (h / 2);
            fx.Root.AddChild(fx.Beam);
            // a wider, fainter sheath round the core column
            var sheath = new CylinderMesh { TopRadius = 5.5f, BottomRadius = 6.5f, Height = h, CapTop = false, CapBottom = false, RadialSegments = 24, Rings = 1 };
            fx.Glow = new MeshInstance3D { Mesh = sheath, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, MaterialOverride = new ShaderMaterial { Shader = Shaders.Get(BeamShader), RenderPriority = 23 } };
            ((ShaderMaterial)fx.Glow.MaterialOverride).SetShaderParameter("height", h);
            ((ShaderMaterial)fx.Glow.MaterialOverride).SetShaderParameter("gain", 0.28f);
            fx.Glow.Position = Vector3.Up * (h / 2);
            fx.Root.AddChild(fx.Glow);
        }
        if (ground && _effects != null)
        {
            // leaf litter and grass clippings flung outward along the ground, grit kicked up, sparks of mutagen rising
            _effects.Radial(at + Vector3.Up * 0.3f, (int)(18 + 10 * power) * (queen ? 2 : 1), 12, reach * 0.9f, 0.32f, 1.6f, 0.25f);
            _effects.Radial(at + Vector3.Up * 0.2f, (int)(16 + 8 * power), 0, reach * 0.7f, 0.6f, 1.2f, 0.1f);
        }
        _effects?.Burst(at + Vector3.Up * 0.6f, (int)(14 + 8 * power) * (queen ? 2 : 1), 11, queen ? 7f : 4.5f, 0.22f, 1.6f);
        _fx.Add(fx);
    }

    public void Update(float dt)
    {
        for (int i = _fx.Count - 1; i >= 0; i--)
        {
            var fx = _fx[i];
            fx.T += dt;
            float k = fx.T / fx.Dur;
            if (k >= 1) { fx.Root.QueueFree(); _fx.RemoveAt(i); continue; }
            float ringK = Mathf.Min(1, fx.T / (fx.Queen ? 1.6f : 1.2f));
            ((ShaderMaterial)fx.Ring.MaterialOverride).SetShaderParameter("t", ringK);
            ((ShaderMaterial)fx.Ring2.MaterialOverride).SetShaderParameter("t", Mathf.Clamp((fx.T - 0.35f) / 1.2f, 0, 1));
            ((ShaderMaterial)fx.Cracks.MaterialOverride).SetShaderParameter("t", k);
            // light: up in 0.12 s, down over the rest
            float env = fx.T < 0.12f ? fx.T / 0.12f : Mathf.Pow(1 - (fx.T - 0.12f) / (fx.Dur - 0.12f), 2.2f);
            fx.Light.LightEnergy = fx.Peak * env;
            if (fx.Beam != null)
            {
                ((ShaderMaterial)fx.Beam.MaterialOverride).SetShaderParameter("t", k);
                ((ShaderMaterial)fx.Glow.MaterialOverride).SetShaderParameter("t", k);
                // the column pulls in a little as it fades
                float w = 1f - 0.35f * Mathf.Max(0, k - 0.5f) * 2f;
                fx.Beam.Scale = new Vector3(w, 1, w);
                fx.Glow.Scale = new Vector3(w, 1, w);
                // motes climbing the column
                if (fx.T < fx.Dur * 0.7f && _rng.R(0, 1) < dt * 30) _effects?.Rise(fx.At + new Vector3(_rng.R(-2f, 2f), _rng.R(1, 12), _rng.R(-2f, 2f)), 11, _rng.R(6, 14), 0.3f, 1.4f);
            }
        }
    }
}
