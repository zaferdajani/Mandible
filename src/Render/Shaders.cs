namespace Mandible;

public static class Shaders
{
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
varying float v_part;
varying float v_flash;
varying vec3 v_local;

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
    VERTEX = p;
    NORMAL = n;
}

void fragment() {
    vec3 c = body_color;
    float rough = 0.34;
    if (v_part == 12.0 || v_part == 9.0 || v_part == 10.0) c = head_color;
    if (v_part == 13.0) {
        c = gaster_color;
        // tergite bands
        float band = smoothstep(0.42, 0.5, fract(v_local.z * 18.0 + 0.3));
        c *= 1.0 - band * 0.35;
    }
    if (v_part >= 1.0 && v_part <= 8.0) { c = leg_color; rough = 0.45; BACKLIGHT = leg_color * 0.6; }
    if (v_part == 9.0 || v_part == 10.0) c *= 0.6;
    if (v_part == 11.0) { c = vec3(0.01); rough = 0.08; }
    ALBEDO = c * (1.0 - dead * 0.35);
    ROUGHNESS = rough + dead * 0.3;
    SPECULAR = 0.6;
    CLEARCOAT = 0.7;
    CLEARCOAT_ROUGHNESS = 0.18;
    RIM = 0.35;
    RIM_TINT = 0.6;
    EMISSION = vec3(1.0, 0.25, 0.08) * v_flash * 0.8;
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
varying vec3 wpos;
varying vec3 vcol;
void vertex() {
    wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    vcol = COLOR.rgb;
}
void fragment() {
    vec2 uv = wpos.xz;
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
    vec3 col = v_c.z < 0.5 ? vec3(0.62, 0.52, 0.40) : (v_c.z < 1.5 ? vec3(0.78, 0.82, 0.30) : vec3(1.0, 0.85, 0.5));
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
