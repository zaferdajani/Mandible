using Godot;

namespace Mandible;

/// The fog of war as one screen pass over everything the world draws (soil, stones, ants, effects): ground no ant
/// has ever seen is solid black, ground seen before but out of sight now is dimmed, and what the colony sees is
/// clear. Each pixel's world position comes from the depth buffer, so the edge follows the ground, and a little
/// noise breaks the frontier into an irregular, feathered line a few ants wide.
public static class FogOverlay
{
    public const string Code = @"
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled, fog_disabled;
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform sampler2D fow_tex : hint_default_white, filter_linear, repeat_disable;
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
uniform float map_size = 320.0;
uniform float remembered = 0.42; // how bright ground seen before (but not now) stays
void vertex() { POSITION = vec4(VERTEX.xy * 2.0, 1.0, 1.0); }
void fragment() {
    float d = texture(depth_tex, SCREEN_UV).r;
    vec4 v = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, d, 1.0);
    v.xyz /= v.w;
    vec3 wp = (INV_VIEW_MATRIX * vec4(v.xyz, 1.0)).xyz;
    // break the frontier: two octaves of the yard's cell noise push the lookup a couple of units either way
    vec2 j = (texture(noise_b, wp.xz * 0.045).rg - 0.5) * 2.0 + (texture(noise_b, wp.xz * 0.17 + 0.37).rg - 0.5) * 0.5;
    vec2 f = texture(fow_tex, (wp.xz + j) / map_size).rg;
    float known = smoothstep(0.02, 0.95, f.g);
    float seen = smoothstep(0.05, 0.9, f.r);
    float lit = known * mix(remembered, 1.0, seen);
    ALBEDO = vec3(0.0);
    ALPHA = 1.0 - lit;
}
";

    /// Hangs the pass in front of the camera. Only for sessions that have a fog of war.
    public static MeshInstance3D Attach(Camera3D cam, World w)
    {
        var m = new ShaderMaterial { Shader = Shaders.Get(Code), RenderPriority = 127 };
        m.SetShaderParameter("noise_b", w.NoiseB);
        m.SetShaderParameter("map_size", Config.MapSize);
        w.RegisterFow(m);
        var mi = new MeshInstance3D
        {
            Name = "FogOverlay",
            Mesh = new QuadMesh { Size = Vector2.One },
            MaterialOverride = m,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-1e5f, -1e5f, -1e5f), new Vector3(2e5f, 2e5f, 2e5f)), // never culled
            Position = new Vector3(0, 0, -1),
        };
        cam.AddChild(mi);
        return mi;
    }
}
