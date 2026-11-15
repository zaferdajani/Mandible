using Godot;

namespace Mandible;

/// The scorpion (target P7-B, docs/art-targets/README-scorpion.md): the Stone Mother and her young are drawn from one
/// generated scan (assets/bugs/scorpion/scorpion.glb, tools/blender/scorpion_model.py) with its own colour map. A scan
/// has none of the per-vertex parts the Ant shader animates from, so this shader finds them by where a vertex lies:
/// the eight legs are what stands out sideways from the body, the tail what rises above the back, the claws what
/// reaches ahead of the legs. It reads the same per-instance data as the Ant shader (AntRenderer.Mind).
public static class ScorpionModel
{
    const string Path = "res://assets/bugs/scorpion/scorpion.glb";
    static ArrayMesh _mesh;
    static Texture2D _skin;
    static bool _tried;

    public static bool Is(AntType t) => t is AntType.Scorpion or AntType.Scorpling;

    public static ArrayMesh Mesh()
    {
        if (_tried) return _mesh;
        _tried = true;
        if (!ResourceLoader.Exists(Path)) return null;
        var node = GD.Load<PackedScene>(Path).Instantiate<Node3D>();
        foreach (var n in node.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)n;
            if (mi.Mesh is not ArrayMesh am) continue;
            _mesh = am;
            _skin = (mi.GetActiveMaterial(0) as BaseMaterial3D)?.AlbedoTexture;
            break;
        }
        node.Free();
        return _mesh;
    }

    public static ShaderMaterial Material(AntType type, bool dead)
    {
        Mesh();
        var m = new ShaderMaterial { Shader = Shaders.Get(Shader) };
        if (_skin != null) m.SetShaderParameter("skin", _skin);
        m.SetShaderParameter("young", type == AntType.Scorpling ? 1f : 0f);
        m.SetShaderParameter("dead", dead ? 1f : 0f);
        return m;
    }

    public const string Shader = @"
shader_type spatial;
render_mode depth_draw_opaque, cull_back;
uniform sampler2D skin : source_color, filter_linear_mipmap_anisotropic;
uniform float young = 0.0;  // a young scorpion: pale cream, as they ride on her back
uniform float dead = 0.0;
varying vec3 v_local;
varying float v_flash;
varying float v_sel;

vec3 rot_x(vec3 p, float a) { float c = cos(a), s = sin(a); return vec3(p.x, c * p.y - s * p.z, s * p.y + c * p.z); }
vec3 rot_y(vec3 p, float a) { float c = cos(a), s = sin(a); return vec3(c * p.x + s * p.z, p.y, -s * p.x + c * p.z); }

void vertex() {
    // the same per-instance packing as the Ant shader: x gait phase, y speed, z flags + bite, w one-shots
    float phase = fract(INSTANCE_CUSTOM.x) * TAU;
    float amp = fract(INSTANCE_CUSTOM.y) * (1.0 - dead);
    float flags = floor(INSTANCE_CUSTOM.z);
    float bite = fract(INSTANCE_CUSTOM.z) * (1.0 - dead);
    v_flash = mod(floor(flags / 64.0), 16.0) / 15.0;
    v_sel = mod(floor(flags / 16.0), 2.0) + 0.6 * mod(floor(flags / 32.0), 2.0);
    vec3 p = VERTEX;
    v_local = p;
    float side = sign(p.x);
    // the legs: everything that stands out sideways from the body, behind the claws. Four a side, walking in two
    // alternating sets; a foot swings forward through the air and pushes back on the ground
    float leg = smoothstep(0.1, 0.2, abs(p.x)) * (1.0 - smoothstep(0.19, 0.27, p.z)) * (1.0 - smoothstep(0.2, 0.28, p.y));
    float slot = floor((p.z + 0.2) / 0.1);
    float par = mod(slot + (side > 0.0 ? 0.0 : 1.0), 2.0);
    float ph = phase * 2.0 + par * PI;
    float reach = smoothstep(0.1, 0.4, abs(p.x));
    p.z += sin(ph) * 0.05 * leg * reach * amp;
    p.y += max(0.0, cos(ph)) * 0.035 * leg * reach * amp;
    p.x *= 1.0 - 0.35 * leg * dead;                                   // dead: the legs draw in
    // the tail: what rises above the back. It rocks as she walks and whips forward over her head when she strikes
    float tail = max(smoothstep(0.25, 0.33, p.y), 1.0 - smoothstep(-0.36, -0.3, p.z));
    vec3 hinge = vec3(0.0, 0.14, -0.3);
    float whip = sin(clamp(bite, 0.0, 1.0) * PI);
    float swing = -0.55 * whip + 0.04 * sin(phase) * amp + 0.9 * dead;
    p = mix(p, hinge + rot_x(p - hinge, swing), tail);
    // the claws: what reaches ahead of the legs. They open and close a little, and snap shut on a bite
    float claw = smoothstep(0.22, 0.32, p.z) * smoothstep(0.05, 0.14, abs(p.x));
    vec3 wrist = vec3(side * 0.16, p.y, 0.25);
    float grip = side * (0.1 * sin(TIME * 1.3 + side) * (1.0 - dead) - 0.3 * whip - 0.25 * dead);
    p = mix(p, wrist + rot_y(p - wrist, grip), claw);
    p.y += sin(phase * 2.0) * 0.006 * amp;
    p.y *= 1.0 - 0.25 * dead;
    VERTEX = p;
}

void fragment() {
    vec3 tex = texture(skin, UV).rgb;
    // the scan's colour map is lit flat and orange: bring it down to the target's dark chestnut, keep the pale young
    float lum = dot(tex, vec3(0.3, 0.55, 0.15));
    float pale = smoothstep(0.4, 0.62, min(tex.g, tex.b * 1.7));
    vec3 shell = vec3(lum * 0.9 + tex.r * 0.25, lum * 0.52 + tex.g * 0.12, lum * 0.26) * vec3(0.5, 0.44, 0.4); // chestnut: the red held down, no violet
    vec3 c = mix(shell, tex * vec3(0.92, 0.86, 0.72), pale);
    c = mix(c, vec3(lum) * vec3(0.95, 0.86, 0.66) + vec3(0.12, 0.1, 0.06), young * 0.85);
    c = mix(c, c * vec3(0.5, 0.46, 0.42), dead);
    float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
    c += vec3(0.1, 0.05, 0.015) * fres * (1.0 - pale);
    ALBEDO = mix(c, vec3(0.9, 0.7, 0.45), v_flash * 0.22);
    ROUGHNESS = mix(0.42, 0.65, pale);
    SPECULAR = 0.4;
    BACKLIGHT = vec3(0.2, 0.08, 0.02) * (1.0 - dead);
    EMISSION = vec3(1.0, 0.8, 0.4) * v_sel * fres * 0.25;
}
";
}
