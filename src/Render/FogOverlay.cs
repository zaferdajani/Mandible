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
uniform float remembered = 0.36; // how bright ground seen before (but not now) stays
uniform float xray_on = 0.0;       // build view: the yard's soil is a translucent roof (GameSession.UpdateGlass)
uniform float net_ground_y = 0.0;  // ...and the ground level at the nest
void vertex() { POSITION = vec4(VERTEX.xy * 2.0, 1.0, 1.0); }
void fragment() {
    float d = texture(depth_tex, SCREEN_UV).r;
    vec4 v = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, d, 1.0);
    v.xyz /= v.w;
    vec3 wp = (INV_VIEW_MATRIX * vec4(v.xyz, 1.0)).xyz;
    // the roof draws in the transparent pass and leaves no depth: through it the buffer holds the rooms below, or nothing
    // at all, which once put the whole yard off the map (all black). Read the fog where the view ray meets the surface.
    vec3 eye = INV_VIEW_MATRIX[3].xyz;
    if (xray_on > 0.001 && wp.y < net_ground_y - 0.3 && eye.y > net_ground_y)
        wp = eye + (wp - eye) * ((eye.y - net_ground_y) / max(eye.y - wp.y, 1e-4));
    // break the frontier: two octaves of the yard's cell noise push the lookup a couple of units either way
    vec2 j = (texture(noise_b, wp.xz * 0.045).rg - 0.5) * 2.0 + (texture(noise_b, wp.xz * 0.17 + 0.37).rg - 0.5) * 0.5;
    vec2 f = texture(fow_tex, (wp.xz + j) / map_size).rg;
    float known = smoothstep(0.02, 0.95, f.g);
    float seen = smoothstep(0.05, 0.9, f.r);
    float lit = known * mix(remembered, 1.0, seen);
    // ground seen before but out of sight now is a clearly separate tier: dimmed and drained of colour (a cool grey-brown
    // veil), so black (never seen), remembered and in sight read as three different things at a glance
    float remember = known * (1.0 - seen);
    ALBEDO = mix(vec3(0.0), vec3(0.09, 0.08, 0.075), remember / max(1.0 - lit, 0.001));
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
