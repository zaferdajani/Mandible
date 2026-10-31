namespace Mandible;

public static class Shaders
{
    static readonly System.Collections.Generic.Dictionary<string, Godot.Shader> _cache = new();

    /// One shared Shader per source: the renderer compiles every Shader object separately, so
    /// creating one per rock, leaf or crumb stalled the first frame for many seconds.
    public static Godot.Shader Get(string code)
    {
        if (!_cache.TryGetValue(code, out var s)) _cache[code] = s = new Godot.Shader { Code = code };
        return s;
    }

    const string Fow = @"
uniform sampler2D fow_tex : hint_default_white, filter_linear, repeat_disable;
uniform float map_size = 320.0;
float fow(vec3 wp) {
    vec2 f = texture(fow_tex, wp.xz / map_size).rg;
    return mix(0.10, mix(0.42, 1.0, f.r), f.g);
}
";

    public const string Ant = @"
shader_type spatial;
render_mode depth_draw_opaque;
uniform vec3 body_color : source_color = vec3(0.03, 0.026, 0.024);
uniform vec3 head_color : source_color = vec3(0.035, 0.03, 0.028);
uniform vec3 gaster_color : source_color = vec3(0.025, 0.022, 0.02);
uniform vec3 leg_color : source_color = vec3(0.06, 0.045, 0.035);
uniform float dead = 0.0;
uniform int pattern = 0;      // 0 ant, 1 beetle (grooved metallic shell), 2 wolf spider (striped, hairy)
uniform float irid = 0.0;     // structural-colour sheen strength
uniform float hero = 0.0;     // gold-inlaid armour of hero ants
uniform vec3 acid = vec3(0.0); // glowing acid sac (Acid Matron)
varying float v_part;
varying float v_flash;
varying vec3 v_local;
varying float v_w;

float hash3(vec3 p) { return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453); }

vec3 rot_y(vec3 p, float a) { float c = cos(a), s = sin(a); return vec3(c * p.x + s * p.z, p.y, -s * p.x + c * p.z); }
vec3 rot_x(vec3 p, float a) { float c = cos(a), s = sin(a); return vec3(p.x, c * p.y - s * p.z, s * p.y + c * p.z); }

void vertex() {
    float part = floor(COLOR.r * 255.0 + 0.5);
    float w = COLOR.g;
    float phase = INSTANCE_CUSTOM.x * TAU;
    float amp = INSTANCE_CUSTOM.y;
    float bite = INSTANCE_CUSTOM.z;
    vec2 piv = UV2;
    vec3 p = VERTEX;
    vec3 n = NORMAL;
    if (part >= 1.0 && part <= 6.0) {
        float side = part <= 3.0 ? 1.0 : -1.0;
        float idx = mod(part - 1.0, 3.0);
        float grp = mod(idx + (side > 0.0 ? 0.0 : 1.0), 2.0);
        float ph = phase + grp * PI;
        float swing = sin(ph) * 0.42 * amp;
        float lift = max(0.0, cos(ph)) * 0.09 * amp * w;
        vec3 q = p - vec3(piv.x, 0.0, piv.y);
        q = rot_y(q, swing * side);
        n = rot_y(n, swing * side);
        p = q + vec3(piv.x, 0.0, piv.y);
        p.y += lift;
        // dead: legs fold in toward the body
        p.xz = mix(p.xz, piv + (p.xz - piv) * 0.45, dead * w);
        p.y = mix(p.y, p.y + 0.12 * w, dead);
    } else if (part == 7.0 || part == 8.0) {
        float side = part == 7.0 ? 1.0 : -1.0;
        float a = sin(TIME * 5.0 + float(INSTANCE_ID) * 1.7 + side) * 0.22 + sin(TIME * 2.3 + float(INSTANCE_ID)) * 0.12;
        vec3 q = p - vec3(piv.x, 0.0, piv.y);
        q = rot_y(q, a * w * (1.0 - dead));
        p = q + vec3(piv.x, 0.0, piv.y);
    } else if (part == 9.0 || part == 10.0) {
        float side = part == 9.0 ? 1.0 : -1.0;
        float open = bite * 0.55 + sin(TIME * 3.0 + float(INSTANCE_ID)) * 0.04;
        vec3 q = p - vec3(piv.x, 0.0, piv.y);
        q = rot_y(q, open * side);
        p = q + vec3(piv.x, 0.0, piv.y);
    } else {
        // body bob and a slight gaster pump
        p.y += sin(phase * 2.0) * 0.006 * amp;
        if (part == 13.0) p.y += sin(TIME * 2.0 + float(INSTANCE_ID)) * 0.004;
    }
    v_part = part;
    v_flash = INSTANCE_CUSTOM.w;
    v_local = VERTEX;
    v_w = w;
    VERTEX = p;
    NORMAL = n;
}

void fragment() {
    // part ids arrive interpolated (12.9999...), so snap them before comparing
    float part = floor(v_part + 0.5);
    vec3 c = body_color;
    float rough = 0.34;
    if (part == 12.0 || part == 9.0 || part == 10.0) c = head_color;
    float cc = 0.7;
    float metal = 0.0;
    if (part == 13.0) {
        c = gaster_color;
        if (pattern == 1) {
            // elytra: fine longitudinal grooves and a raised suture
            float groove = smoothstep(0.30, 0.5, abs(fract(v_local.x * 34.0) - 0.5));
            c *= 0.55 + 0.45 * groove;
            float pits = step(0.82, hash3(floor(v_local * vec3(34.0, 60.0, 60.0))));
            c *= 1.0 - pits * 0.35 * (1.0 - groove);
        } else if (pattern == 2) {
            // wolf spider abdomen: dark cardiac mark and chevrons on a mottled ground
            float heart = 1.0 - smoothstep(0.025, 0.045, abs(v_local.x) - (v_local.z + 0.24) * 0.12);
            float chev = smoothstep(0.4, 0.5, fract((v_local.z + abs(v_local.x) * 1.4) * 14.0)) * step(v_local.z, -0.25);
            c = mix(c, c * 0.35, max(heart * step(-0.3, v_local.z), chev * 0.7));
        } else {
            // tergite bands
            float band = smoothstep(0.42, 0.5, fract(v_local.z * 18.0 + 0.3));
            c *= 1.0 - band * 0.35;
        }
    }
    if (pattern == 3) {
        // wasp: black thorax with yellow flecks, yellow face, banded abdomen
        if (part == 13.0) c = mix(body_color, gaster_color, step(0.45, fract(v_local.z * 9.0 + 0.15)));
        if (part == 0.0) c = mix(body_color, head_color, step(0.06, abs(v_local.x)) * step(0.1, v_local.z) * 0.9);
        if (part == 12.0) c = mix(head_color, body_color, smoothstep(0.31, 0.34, v_local.y));
    }
    if (pattern == 2 && part == 12.0) {
        // pale stripes either side of a dark median band
        float ax = abs(v_local.x);
        float pale = smoothstep(0.022, 0.032, ax) * (1.0 - smoothstep(0.06, 0.075, ax));
        c = mix(c * 0.55, c * 1.9, pale);
    }
    if (pattern == 1 && part == 0.0) {
        // pronotum: punctured, with a dark midline
        c *= 1.0 - (1.0 - smoothstep(0.004, 0.01, abs(v_local.x))) * 0.5;
    }
    if (part >= 1.0 && part <= 8.0) {
        c = leg_color; rough = 0.45; BACKLIGHT = leg_color * 0.6;
        if (pattern == 2) c *= 0.65 + 0.55 * step(0.5, fract(v_w * 4.0 + 0.2)); // banded legs
        if (pattern == 1) c *= 0.8 + 0.2 * step(0.85, v_w);
    }
    if (part == 9.0 || part == 10.0) c *= 0.6;
    if (part == 11.0) { c = vec3(0.01); rough = 0.08; }
    if (pattern == 2) {
        // velvet hair: matte, speckled, soft sheen
        float hair = hash3(floor(v_local * 320.0));
        c *= 0.8 + 0.4 * hair;
        if (part != 11.0) { rough = 0.78; cc = 0.0; }
    }
    if (pattern == 1 && part != 11.0) { rough = 0.22; cc = 0.85; metal = 0.35; }
    if (pattern == 3 && part != 11.0) { rough = 0.3; cc = 0.5; }
    if (hero > 0.0 && part != 11.0) {
        // gold filigree: bands on the abdomen, a crest on the head, edged mandibles
        float gband = part == 13.0 ? step(0.8, fract(v_local.z * 11.0 + 0.1)) : 0.0;
        float crest = part == 12.0 ? smoothstep(0.02, 0.0, abs(v_local.x)) * step(0.17, v_local.y) : 0.0;
        float jaw = (part == 9.0 || part == 10.0) ? step(0.75, v_w) : 0.0;
        float g = max(max(gband, crest), jaw) * hero;
        c = mix(c, vec3(0.95, 0.68, 0.2), g);
        metal = mix(metal, 0.9, g);
        rough = mix(rough, 0.2, g);
    }
    // structural colour: hue shifts with viewing angle, strongest at grazing angles
    float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.0);
    vec3 rainbow = 0.5 + 0.5 * cos(6.2831 * (fres * 0.9 + vec3(0.55, 0.75, 0.95)));
    c += rainbow * irid * (0.06 + 0.5 * fres) * (part >= 1.0 && part <= 10.0 ? 0.3 : 1.0);
    ALBEDO = c * (1.0 - dead * 0.35);
    ROUGHNESS = rough + dead * 0.3;
    METALLIC = metal;
    SPECULAR = 0.6;
    CLEARCOAT = cc;
    CLEARCOAT_ROUGHNESS = 0.18;
    RIM = pattern == 2 ? 0.6 : 0.35;
    RIM_TINT = 0.6;
    // brief warm flicker when bitten; kept subtle so big targets under constant attack keep their colour
    EMISSION = vec3(1.0, 0.3, 0.1) * v_flash * v_flash * (pattern == 0 ? 0.3 : 0.08);
    if (part == 13.0 && length(acid) > 0.0) {
        // translucent acid sac: smooth glossy green with a slow inner glow, gold bands still on top
        float pulse = 0.75 + 0.25 * sin(TIME * 2.2);
        float bands = hero * step(0.8, fract(v_local.z * 11.0 + 0.1));
        ALBEDO = mix(acid * 0.6, vec3(0.95, 0.68, 0.2), bands);
        EMISSION += acid * 0.22 * pulse * (1.0 - bands);
        CLEARCOAT = 1.0;
        CLEARCOAT_ROUGHNESS = 0.15;
        ROUGHNESS = 0.25;
        METALLIC = bands * 0.8;
        BACKLIGHT = vec3(0.0);
    }
}
";

    public static readonly string Terrain = @"
shader_type spatial;
" + Fow + @"
uniform sampler2D noise_a : filter_linear_mipmap, repeat_enable;
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
uniform sampler2D normal_a : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D normal_b : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D pher_tex : filter_linear, repeat_disable;
uniform float pher_view = 0.25;
// Glass Earth: within xray_radius of xray_center the soil turns to glass (screen-door dither keeps it cheap and sort-free)
uniform vec2 xray_center = vec2(-1000.0);
uniform float xray_radius = 20.0;
uniform float xray_on = 0.0;
varying vec3 wpos;
varying vec3 vcol;
void vertex() {
    wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    vcol = COLOR.rgb;
}
float bayer(vec2 px) {
    ivec2 p = ivec2(mod(px, 4.0));
    int m[16] = int[16](0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5);
    return (float(m[p.y * 4 + p.x]) + 0.5) / 16.0;
}
void fragment() {
    vec2 uv = wpos.xz;
    float glass_d = distance(uv, xray_center);
    float glass = xray_on * (1.0 - smoothstep(xray_radius - 6.0, xray_radius, glass_d));
    if (glass > 0.001) {
        float keep = mix(1.0, 0.09, glass);
        if (bayer(FRAGCOORD.xy) > keep) discard;
    }
    float n1 = texture(noise_a, uv * 0.0035).r;
    float n2 = texture(noise_a, uv * 0.017 + 0.3).r;
    float g = texture(noise_b, uv * 0.09).r;
    float g2 = texture(noise_b, uv * 0.41 + 0.7).r;
    float grain = texture(noise_b, uv * 1.7).r;
    float grit = texture(noise_b, uv * 0.83 + 0.37).r;
    vec3 dry = vec3(0.36, 0.27, 0.19);
    vec3 damp = vec3(0.17, 0.12, 0.085);
    vec3 clay = vec3(0.34, 0.20, 0.12);
    vec3 col = mix(dry, damp, smoothstep(0.40, 0.80, n1));
    col = mix(col, clay, smoothstep(0.5, 0.8, n2) * 0.5);
    col *= 0.72 + 0.45 * g;
    col *= 0.8 + 0.3 * g2;
    // individual sand grains: bright quartz flecks and dark pores
    col *= 0.78 + 0.32 * grain;
    float quartz = smoothstep(0.80, 0.88, grit) * smoothstep(0.35, 0.7, g);
    col = mix(col, vec3(0.62, 0.57, 0.50), quartz * 0.6);
    float pore = smoothstep(0.72, 0.8, texture(noise_b, uv * 0.6 + 0.11).r);
    col = mix(col, vec3(0.06, 0.045, 0.035), pore * 0.35);
    float dark = smoothstep(0.7, 0.8, texture(noise_b, uv * 0.23).r);
    col = mix(col, vec3(0.1, 0.075, 0.05), dark * 0.35);
    // excavated sand around the black colony's nest
    vec3 sand = vec3(0.50, 0.40, 0.28) * (0.7 + 0.5 * g2) * (0.8 + 0.3 * grain);
    col = mix(col, sand, vcol.g);
    // pine thatch mound of the red colony
    vec3 thatch = mix(vec3(0.20, 0.11, 0.05), vec3(0.36, 0.20, 0.09), g) * (0.7 + 0.4 * g2);
    col = mix(col, thatch, vcol.b);
    col *= vcol.r;
    float wet = smoothstep(0.55, 0.85, n1);
    // the glass itself: a cool sheen with a bright rim where it meets the soil
    float rim = glass * smoothstep(xray_radius - 6.0, xray_radius - 1.5, glass_d);
    col = mix(col, col * 0.6 + vec3(0.22, 0.30, 0.34), glass * 0.55) + vec3(0.35, 0.45, 0.5) * rim * 0.6;
    ALBEDO = col;
    ROUGHNESS = mix(0.95, 0.72, wet);
    vec3 na = texture(normal_a, uv * 0.11).rgb;
    vec3 nb = texture(normal_b, uv * 0.47).rgb;
    NORMAL_MAP = normalize(vec3((na.xy + nb.xy) - 1.0, 1.0)) * 0.5 + 0.5;
    NORMAL_MAP_DEPTH = 1.4;
    float ph = texture(pher_tex, uv / map_size).r;
    float f = fow(wpos);
    ALBEDO = mix(ALBEDO, vec3(1.0, 0.72, 0.32), clamp(ph, 0.0, 1.0) * pher_view * 0.35) * f;
    EMISSION = vec3(1.0, 0.55, 0.12) * clamp(ph, 0.0, 1.5) * pher_view * 0.35 * step(0.5, f);
}
";

    public static readonly string Rock = @"
shader_type spatial;
" + Fow + @"
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
uniform sampler2D normal_b : hint_normal, filter_linear_mipmap, repeat_enable;
varying vec3 wpos;
varying vec3 wnrm;
varying vec4 vcol;
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; wnrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz); vcol = COLOR; }
void fragment() {
    vec3 an = abs(wnrm);
    an /= (an.x + an.y + an.z);
    float a = texture(noise_b, wpos.yz * 0.35).r * an.x + texture(noise_b, wpos.xz * 0.35).r * an.y + texture(noise_b, wpos.xy * 0.35).r * an.z;
    float b = texture(noise_b, wpos.xz * 1.6 + wpos.y).r;
    vec3 base = mix(vec3(0.27, 0.26, 0.25), vec3(0.40, 0.35, 0.29), vcol.g);
    base = mix(base, vec3(0.33, 0.23, 0.16), vcol.b);
    vec3 col = base * (0.62 + 0.6 * a);
    // mineral veins and dark crevices
    col = mix(col, vec3(0.08, 0.075, 0.07), smoothstep(0.72, 0.8, b) * 0.7);
    col = mix(col, vec3(0.78, 0.76, 0.72), smoothstep(0.88, 0.92, b) * 0.45);
    float lichen = smoothstep(0.62, 0.7, texture(noise_b, wpos.xz * 0.9 + 3.0).r) * smoothstep(0.2, 0.8, wnrm.y);
    col = mix(col, vec3(0.55, 0.55, 0.38), lichen * 0.35);
    float moss = smoothstep(0.6, 0.95, wnrm.y) * smoothstep(0.55, 0.75, a) * vcol.a;
    col = mix(col, vec3(0.12, 0.20, 0.05), moss * 0.75);
    // dust settles on top
    col = mix(col, vec3(0.36, 0.28, 0.20), smoothstep(0.8, 1.0, wnrm.y) * 0.3);
    ALBEDO = col * vcol.r * fow(wpos);
    ROUGHNESS = 0.82 - b * 0.2;
    NORMAL_MAP = texture(normal_b, UV * 3.0).rgb;
    NORMAL_MAP_DEPTH = 0.8;
}
";

    public static readonly string Leaf = @"
shader_type spatial;
render_mode cull_disabled;
" + Fow + @"
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
varying vec3 wpos;
varying vec4 vcol;
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; vcol = COLOR; }
void fragment() {
    float age = vcol.r;
    vec3 green = vec3(0.22, 0.34, 0.09);
    vec3 yellow = vec3(0.70, 0.52, 0.13);
    vec3 brown = vec3(0.40, 0.21, 0.08);
    vec3 col = age < 0.5 ? mix(green, yellow, age * 2.0) : mix(yellow, brown, (age - 0.5) * 2.0);
    float u = UV.x - 0.5;
    float v = UV.y;
    float rib = 1.0 - smoothstep(0.0, 0.012, abs(u));
    float lat = 1.0 - smoothstep(0.0, 0.03, abs(fract(v * 9.0 - abs(u) * 5.0) - 0.5) - 0.44);
    lat *= step(0.02, abs(u));
    float spots = smoothstep(0.62, 0.7, texture(noise_b, UV * vec2(2.0, 5.0) + vcol.g * 7.0).r);
    float n = texture(noise_b, UV * vec2(3.0, 8.0) + vcol.g * 3.0).r;
    col *= 0.82 + 0.3 * n;
    col = mix(col, col * 1.45 + 0.04, max(rib, lat * 0.6));
    col = mix(col, vec3(0.16, 0.09, 0.04), spots * (0.3 + age * 0.5));
    // dry, darker edges
    col = mix(col, brown * 0.7, smoothstep(0.38, 0.5, abs(u) / max(0.001, vcol.b)) * age);
    ALBEDO = col * fow(wpos);
    ROUGHNESS = mix(0.45, 0.85, age);
    SPECULAR = 0.35;
    BACKLIGHT = col * 0.5;
}
";

    public static readonly string Grass = @"
shader_type spatial;
render_mode cull_disabled;
" + Fow + @"
varying vec3 wpos;
varying float hgt;
void vertex() {
    vec3 world = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
    hgt = COLOR.g;
    float sway = sin(TIME * 1.1 + world.x * 0.04 + world.z * 0.03) * 0.6 + sin(TIME * 2.7 + world.z * 0.11) * 0.25;
    VERTEX.x += sway * hgt * hgt * 1.4;
    VERTEX.z += sway * hgt * hgt * 0.6;
    wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}
void fragment() {
    vec3 base = vec3(0.10, 0.17, 0.05);
    vec3 tip = mix(vec3(0.42, 0.55, 0.16), vec3(0.62, 0.58, 0.25), INSTANCE_CUSTOM_FRAG);
    vec3 col = mix(base, tip, hgt);
    float stripe = 0.9 + 0.1 * sin(UV.x * 40.0);
    ALBEDO = col * stripe * fow(wpos);
    ROUGHNESS = 0.55;
    SPECULAR = 0.4;
    BACKLIGHT = col * 0.7;
}
".Replace("INSTANCE_CUSTOM_FRAG", "v_dry").Replace("varying float hgt;", "varying float hgt;\nvarying float v_dry;").Replace("hgt = COLOR.g;", "hgt = COLOR.g;\n    v_dry = INSTANCE_CUSTOM.x;");

    public static readonly string Twig = @"
shader_type spatial;
" + Fow + @"
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
varying vec3 wpos;
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
    float grain = texture(noise_b, vec2(UV.x * 2.0, UV.y * 0.15)).r;
    float cracks = smoothstep(0.7, 0.78, texture(noise_b, vec2(UV.x * 5.0, UV.y * 0.6)).r);
    vec3 col = mix(vec3(0.30, 0.20, 0.12), vec3(0.52, 0.40, 0.28), grain);
    col = mix(col, vec3(0.12, 0.08, 0.05), cracks * 0.7);
    col = mix(col, vec3(0.55, 0.55, 0.45), smoothstep(0.8, 0.9, texture(noise_b, UV * vec2(3.0, 0.4)).r) * 0.3);
    ALBEDO = col * fow(wpos);
    ROUGHNESS = 0.85;
}
";

    public static readonly string Food = @"
shader_type spatial;
" + Fow + @"
uniform int kind = 0; // 0 crumb, 1 berry, 2 sugar, 3 seed
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
varying vec3 wpos;
varying vec3 lpos;
varying vec3 lnrm;
varying float vcol_r;
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; lpos = VERTEX; lnrm = NORMAL; vcol_r = COLOR.r; }
void fragment() {
    float n = texture(noise_b, lpos.xz * 0.9 + lpos.y * 0.3).r;
    float m = texture(noise_b, lpos.xy * 2.3).r;
    vec3 col; float rough = 0.8; float spec = 0.4; float cc = 0.0;
    if (kind == 0) {
        vec3 crust = mix(vec3(0.40, 0.19, 0.06), vec3(0.62, 0.36, 0.13), n);
        vec3 crumb = mix(vec3(0.70, 0.58, 0.38), vec3(0.80, 0.68, 0.48), m);
        float pores = smoothstep(0.6, 0.7, m);
        col = mix(crumb * (1.0 - pores * 0.45), crust, smoothstep(0.1, 0.6, lnrm.y + n * 0.4));
    } else if (kind == 1) {
        // raspberry drupelets: vertex red channel carries the per-drupelet tone, core is darker
        float tone = vcol_r;
        col = mix(vec3(0.16, 0.005, 0.02), vec3(0.52, 0.03, 0.08), tone) * (0.75 + 0.35 * n);
        // fine surface pitting breaks up the plastic look
        float pit = texture(noise_b, lpos.xz * 3.1 + lpos.y * 2.0).r;
        col *= 0.85 + 0.25 * pit;
        rough = 0.42 + 0.2 * pit; spec = 0.45; cc = 0.15;
        SSS_STRENGTH = 0.9;
        SSS_TRANSMITTANCE_COLOR = vec4(0.9, 0.1, 0.15, 1.0);
    } else if (kind == 2) {
        col = vec3(0.86, 0.85, 0.82) * (0.85 + 0.2 * n);
        rough = 0.25 + m * 0.3; spec = 0.8;
        SSS_STRENGTH = 0.4;
        EMISSION = vec3(1.0) * smoothstep(0.9, 0.93, texture(noise_b, lpos.xy * 6.0 + lpos.z).r) * 0.6;
    } else {
        float stripe = smoothstep(0.35, 0.5, abs(sin(atan(lpos.x, lpos.y) * 6.0 + n * 2.0)));
        col = mix(vec3(0.05, 0.045, 0.04), vec3(0.78, 0.74, 0.66), stripe * 0.85);
        rough = 0.4; spec = 0.5; cc = 0.4;
    }
    ALBEDO = col * fow(wpos);
    ROUGHNESS = rough;
    SPECULAR = spec;
    CLEARCOAT = cc;
    CLEARCOAT_ROUGHNESS = 0.2;
}
";

    public static readonly string Pebble = @"
shader_type spatial;
" + Fow + @"
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
varying vec3 wpos;
varying vec4 v_c;
varying vec3 v_n;
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; v_c = INSTANCE_CUSTOM; v_n = NORMAL; }
void fragment() {
    vec3 a = mix(vec3(0.22, 0.20, 0.18), vec3(0.38, 0.31, 0.23), v_c.x);
    a = mix(a, vec3(0.52, 0.49, 0.44), v_c.y * 0.5);
    a = mix(a, vec3(0.26, 0.15, 0.08), v_c.z);
    float n = texture(noise_b, wpos.xz * 2.5 + v_c.w * 9.0).r;
    a *= 0.7 + 0.5 * n;
    a *= mix(0.55, 1.05, smoothstep(-0.6, 0.6, v_n.y));
    ALBEDO = a * fow(wpos);
    ROUGHNESS = mix(0.85, 0.45, v_c.y);
}
";

    public static readonly string Needle = @"
shader_type spatial;
" + Fow + @"
varying vec3 wpos;
varying float v_tint;
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; v_tint = INSTANCE_CUSTOM.x; }
void fragment() {
    vec3 col = mix(vec3(0.32, 0.17, 0.07), vec3(0.62, 0.38, 0.16), v_tint);
    col *= 0.85 + 0.15 * sin(UV.y * 30.0);
    ALBEDO = col * fow(wpos);
    ROUGHNESS = 0.6;
}
";

    public const string Ring = @"
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;
uniform vec4 color : source_color = vec4(1.0, 0.8, 0.3, 1.0);
varying float v_alpha;
void vertex() { v_alpha = INSTANCE_CUSTOM.x; }
void fragment() {
    float r = length(UV - 0.5) * 2.0;
    float ring = smoothstep(0.72, 0.8, r) * (1.0 - smoothstep(0.9, 1.0, r));
    float fill = (1.0 - smoothstep(0.0, 0.8, r)) * 0.12;
    ALBEDO = color.rgb;
    ALPHA = (ring + fill) * color.a * v_alpha;
}
";

    public const string Bar = @"
shader_type spatial;
render_mode unshaded, depth_test_disabled, cull_disabled, shadows_disabled;
varying float v_hp;
varying vec3 v_team;
void vertex() {
    v_hp = INSTANCE_CUSTOM.x;
    v_team = INSTANCE_CUSTOM.yzw;
    float width = length(MODEL_MATRIX[0].xyz);
    float hgt = length(MODEL_MATRIX[1].xyz);
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * width, INV_VIEW_MATRIX[1] * hgt, INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}
void fragment() {
    float edge = step(UV.x, 0.03) + step(0.97, UV.x) + step(UV.y, 0.15) + step(0.85, UV.y);
    vec3 hpc = mix(vec3(0.95, 0.18, 0.1), mix(vec3(0.95, 0.8, 0.15), vec3(0.3, 0.9, 0.25), smoothstep(0.5, 0.8, v_hp)), smoothstep(0.15, 0.5, v_hp));
    vec3 col = UV.x < v_hp ? hpc : vec3(0.06, 0.05, 0.04);
    col = mix(col, v_team * 0.35, clamp(edge, 0.0, 1.0));
    ALBEDO = col;
}
";

    public const string Wing = @"
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_never, shadows_disabled;
uniform vec3 tint : source_color = vec3(0.85, 0.88, 0.9);
uniform vec3 vein : source_color = vec3(0.12, 0.08, 0.04);
varying vec2 v_uv;
varying float v_fly;
vec3 rot_y(vec3 p, float a) { float c = cos(a), s = sin(a); return vec3(c * p.x + s * p.z, p.y, -s * p.x + c * p.z); }
vec3 rot_z(vec3 p, float a) { float c = cos(a), s = sin(a); return vec3(c * p.x - s * p.y, s * p.x + c * p.y, p.z); }
void vertex() {
    float side = floor(COLOR.r * 255.0 + 0.5) < 1.5 ? 1.0 : -1.0;
    float fly = INSTANCE_CUSTOM.y;
    float phase = TIME * INSTANCE_CUSTOM.x + float(INSTANCE_ID) * 1.37;
    vec3 hinge = vec3(UV2.x, UV2.y, COLOR.b - 0.5);
    vec3 p = VERTEX - hinge;
    vec3 n = NORMAL;
    // in flight the wings swing out from the folded pose and beat about the body axis
    p = rot_y(p, -side * fly * 0.95); n = rot_y(n, -side * fly * 0.95);
    float beat = fly * (sin(phase) * 0.8 + 0.2);
    p = rot_z(p, side * beat); n = rot_z(n, side * beat);
    VERTEX = p + hinge;
    NORMAL = n;
    v_uv = UV;
    v_fly = fly;
}
void fragment() {
    float along = abs(fract(v_uv.y * 3.0 + v_uv.x * 0.6) - 0.5);
    float cross = abs(fract(v_uv.x * 4.0) - 0.5);
    float veins = max(1.0 - smoothstep(0.0, 0.04, 0.5 - along), (1.0 - smoothstep(0.0, 0.03, 0.5 - cross)) * step(0.15, v_uv.x));
    float edge = 1.0 - smoothstep(0.0, 0.08, min(v_uv.y, 1.0 - v_uv.y));
    veins = max(veins, edge * 0.8);
    float fres = pow(1.0 - clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0), 2.0);
    vec3 film = 0.5 + 0.5 * cos(6.2831 * (fres * 1.3 + v_uv.x * 0.3 + vec3(0.0, 0.33, 0.67)));
    ALBEDO = mix(tint + film * 0.25, vein, veins);
    ROUGHNESS = 0.15;
    SPECULAR = 0.8;
    // a beating wing is a blur
    ALPHA = mix(0.22, 0.8, veins) * (1.0 - v_fly * 0.45) + fres * 0.15;
}
";

    public const string Web = @"
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, shadows_disabled;
varying float v_a;
void vertex() { v_a = INSTANCE_CUSTOM.x; }
void fragment() {
    vec2 p = UV - 0.5;
    float r = length(p) * 2.0;
    float ang = atan(p.y, p.x);
    float spokes = 1.0 - smoothstep(0.0, 0.04, abs(sin(ang * 7.0 + r * 0.6)) * max(r, 0.08));
    float q = fract(r * 7.0 + sin(ang * 7.0) * 0.06);
    float rings = 1.0 - smoothstep(0.0, 0.07, min(q, 1.0 - q));
    float fade = 1.0 - smoothstep(0.75, 1.0, r);
    float threads = max(spokes, rings * 0.85) * fade;
    ALBEDO = vec3(0.93, 0.93, 0.9);
    EMISSION = vec3(0.12);
    ROUGHNESS = 0.3;
    ALPHA = (threads * 0.9 + fade * 0.1) * v_a;
}
";

    public const string Silk = @"
shader_type spatial;
render_mode blend_mix, cull_back, depth_draw_opaque;
varying vec3 lpos;
varying float v_a;
float hash(vec3 p) { return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453); }
void vertex() { lpos = VERTEX; v_a = INSTANCE_CUSTOM.x; }
void fragment() {
    float strands = abs(sin(lpos.z * 90.0 + lpos.x * 40.0 + sin(lpos.y * 70.0) * 2.0));
    float fuzz = hash(floor(lpos * 300.0));
    ALBEDO = vec3(0.9, 0.9, 0.86) * (0.8 + 0.2 * strands) * (0.9 + 0.1 * fuzz);
    ROUGHNESS = 0.55;
    BACKLIGHT = vec3(0.6);
    ALPHA = v_a * (0.75 + 0.25 * strands);
}
";

    public const string Pip = @"
shader_type spatial;
render_mode unshaded, depth_test_disabled, cull_disabled, shadows_disabled, blend_mix;
varying float v_own;
void vertex() {
    v_own = INSTANCE_CUSTOM.x;
    float s = length(MODEL_MATRIX[0].xyz);
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * s, INV_VIEW_MATRIX[1] * s, INV_VIEW_MATRIX[2] * s, MODEL_MATRIX[3]);
}
void fragment() {
    vec2 p = UV - 0.5;
    // military chevron: a thick 'V' pointing down, with a dark outline
    float d = abs(abs(p.x) * 0.9 - p.y - 0.05) - 0.11;   // distance to the V band
    d = max(d, abs(p.x) - 0.42);
    float outer = 1.0 - smoothstep(0.0, 0.03, d - 0.05);
    float inner = 1.0 - smoothstep(0.0, 0.03, d);
    float shine = smoothstep(0.1, -0.2, p.y) * 0.25;
    vec3 gold = vec3(0.85, 0.52, 0.08) + shine;
    vec3 red = vec3(0.75, 0.16, 0.08) + shine;
    vec3 fill = mix(red, gold, v_own);
    ALBEDO = mix(vec3(0.08, 0.04, 0.01), fill, inner);
    ALPHA = outer;
}
";

    public const string Particle = @"
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;
varying vec4 v_c;
void vertex() {
    v_c = INSTANCE_CUSTOM;
    float s = INSTANCE_CUSTOM.y;
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * s, INV_VIEW_MATRIX[1] * s, INV_VIEW_MATRIX[2] * s, MODEL_MATRIX[3]);
}
void fragment() {
    float r = length(UV - 0.5) * 2.0;
    float a = (1.0 - smoothstep(0.2, 1.0, r));
    vec3 col = v_c.z < 0.5 ? vec3(0.62, 0.52, 0.40) : (v_c.z < 1.5 ? vec3(0.78, 0.82, 0.30) : (v_c.z < 2.5 ? vec3(1.0, 0.85, 0.5) : vec3(0.95, 0.95, 0.92)));
    ALBEDO = col;
    ALPHA = a * v_c.x;
}
";

    public const string Marker = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
uniform vec4 color : source_color = vec4(0.4, 1.0, 0.4, 1.0);
uniform float t = 0.0;
void fragment() {
    float r = length(UV - 0.5) * 2.0;
    float rr = mix(0.3, 1.0, t);
    float ring = smoothstep(rr - 0.18, rr - 0.05, r) * (1.0 - smoothstep(rr - 0.05, rr, r));
    ALBEDO = color.rgb;
    ALPHA = ring * (1.0 - t) * color.a;
}
";
}
