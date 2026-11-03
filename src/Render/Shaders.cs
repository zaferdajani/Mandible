namespace Mandible;

public static class Shaders
{
    static readonly System.Collections.Generic.Dictionary<string, Godot.Shader> _cache = new();

    /// One shared Shader per source: the renderer compiles every Shader object separately, so
    /// creating one per rock, leaf or crumb stalled the first frame for many seconds.
    static bool _globals;

    /// Runtime global: (nest x, nest z, nest ground height, 0) of the local colony. Shaders read it to know when
    /// a fragment is underground, where the sky must not light it. Must exist before any shader using it compiles.
    public static void EnsureGlobals()
    {
        if (_globals) return;
        _globals = true;
        Godot.RenderingServer.GlobalShaderParameterAdd("nest_info", Godot.RenderingServer.GlobalShaderParameterType.Vec4, new Godot.Vector4(-1000f, -1000f, 0f, 0f));
    }

    static Godot.Vector4 _nest = new(-1000f, -1000f, 0f, 0f);

    public static void SetNest(Godot.Vector2 pos, float groundY)
    {
        _nest = new Godot.Vector4(pos.X, pos.Y, groundY, _nest.W);
        Godot.RenderingServer.GlobalShaderParameterSet("nest_info", _nest);
    }

    /// How far the earth is opened for building (0..1): the nest's contents take more fill so they read through the roof.
    public static void SetNestOpen(float open)
    {
        if (Godot.Mathf.IsEqualApprox(open, _nest.W)) return;
        _nest.W = open;
        Godot.RenderingServer.GlobalShaderParameterSet("nest_info", _nest);
    }

    public static Godot.Shader Get(string code)
    {
        EnsureGlobals();
        if (!_cache.TryGetValue(code, out var s)) _cache[code] = s = new Godot.Shader { Code = code };
        return s;
    }

    /// How much sky ambient reaches a point: 1 on the surface, `UNDER_AMBIENT` in the dug nest (below the entrance
    /// and within its footprint), blended over a short distance so nothing pops at the hole.
    public const string Under = @"
global uniform vec4 nest_info;
float under_ambient(vec3 p) {
    float depth = nest_info.z - p.y;
    float near = 1.0 - smoothstep(26.0, 32.0, length(p.xz - nest_info.xy));
    // underground it is dark (0.09 of the sky's fill); with the earth opened for building, enough fill returns (0.6)
    // for the brood and the ants to read in the lamp light (nest_info.w = how open)
    return mix(1.0, mix(0.09, 0.6, nest_info.w), smoothstep(0.9, 1.6, depth) * near);
}
";

    const string Fow = @"
uniform float map_size = 320.0;
// the fog of war itself is FogOverlay's screen pass (true black over the unknown); materials no longer dim themselves
float fow(vec3 wp) { return 1.0; }
";

    public const string Ant = @"
shader_type spatial;
render_mode depth_draw_opaque;
" + Under + @"
uniform vec3 body_color : source_color = vec3(0.03, 0.026, 0.024);
uniform vec3 head_color : source_color = vec3(0.035, 0.03, 0.028);
uniform vec3 gaster_color : source_color = vec3(0.025, 0.022, 0.02);
uniform vec3 leg_color : source_color = vec3(0.06, 0.045, 0.035);
uniform float dead = 0.0;
uniform int pattern = 0;      // 0 ant, 1 ground beetle (grooved metallic shell), 2 wolf spider (striped, hairy), 3 wasp, 4 stag beetle
uniform float irid = 0.0;     // structural-colour sheen strength
uniform float hero = 0.0;     // metal-inlaid armour of hero ants
uniform vec3 accent : source_color = vec3(0.95, 0.68, 0.2); // crests, plate rims, inlay: the colony's metal, or a bug's own
uniform vec3 acid = vec3(0.0); // glowing acid sac (Acid Matron)
uniform float sculpture = 0.08; // the cuticle's micro-relief: fine imbricate on a worker, coarse rugose on the Warlord
uniform vec3 waist = vec3(0.0, 0.14, -0.105); // the gaster nods (and curls to spray) about this model-space point
// a body made from a real specimen scan (Antscan micro-CT): its sutures, punctures and setae sockets, baked
uniform sampler2D detail_normal : hint_normal, filter_linear_mipmap, repeat_disable;
uniform float has_detail = 0.0;
uniform vec3 neck = vec3(0.0, 0.14, 0.13);  // the head turns about this model-space point
varying float v_part;
varying float v_flash;
varying vec3 v_local;
varying float v_w;
varying float v_region;
varying float v_block;
varying vec2 v_uv;
varying float v_seed;
varying float v_sel;
varying float v_hov;
varying float v_ao;
varying float v_pale;
varying float v_etch;
varying float v_stage;
varying vec4 v_cus; // CUSTOM0: membrane, seta, textured

float hash3(vec3 p) { return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453); }
float hash1(float x) { return fract(sin(x * 127.1 + 311.7) * 43758.5453); }
float vnoise(vec3 p) {
    vec3 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(mix(hash3(i), hash3(i + vec3(1.0, 0.0, 0.0)), f.x), mix(hash3(i + vec3(0.0, 1.0, 0.0)), hash3(i + vec3(1.0, 1.0, 0.0)), f.x), f.y),
               mix(mix(hash3(i + vec3(0.0, 0.0, 1.0)), hash3(i + vec3(1.0, 0.0, 1.0)), f.x), mix(hash3(i + vec3(0.0, 1.0, 1.0)), hash3(i + vec3(1.0, 1.0, 1.0)), f.x), f.y), f.z);
}

// distance to the nearest of a jittered lattice of points: the punctures that pit an ant's cuticle
float worley(vec3 p) {
    vec3 i = floor(p), f = fract(p);
    float d = 9.0;
    for (int z = -1; z <= 1; z++) for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) {
        vec3 o = vec3(float(x), float(y), float(z));
        vec3 r = o + vec3(hash3(i + o), hash3(i + o + 1.7), hash3(i + o + 3.3)) - f;
        d = min(d, dot(r, r));
    }
    return sqrt(d);
}
vec3 rot_y(vec3 p, float a) { float c = cos(a), s = sin(a); return vec3(c * p.x + s * p.z, p.y, -s * p.x + c * p.z); }
vec3 rot_x(vec3 p, float a) { float c = cos(a), s = sin(a); return vec3(p.x, c * p.y - s * p.z, s * p.y + c * p.z); }
vec3 rot_z(vec3 p, float a) { float c = cos(a), s = sin(a); return vec3(c * p.x - s * p.y, s * p.x + c * p.y, p.z); }
float bit_of(float m, float b) { return mod(floor(m / exp2(b)), 2.0); }

void vertex() {
    float part = floor(COLOR.r * 255.0 + 0.5);
    float w = COLOR.g;
    float region = COLOR.b;
    // Per-instance data, packed by AntRenderer:
    //   x = seed (0..15) + 16 * pose (0 normal, 1 sentinel, 2 listening, 3 grooming) + gait fraction (distance-locked in the sim)
    //   y = yaw8 * 256 + pitch8 (where the head looks) + speed amplitude fraction
    //   z = flags (2 shell wall, 4 charge, 8 carrying, 16 selected, 32 hovered, 64 * hit flash 0..15)
    //       + 1024 * missing parts (bits: legs 0-5, antennae 6-7, head 8, thorax 9, gaster 10) + bite fraction
    //   w = 512 * evolution stage (0..3) + 64 * acid etching (0..7) + 4 * callow paleness (0..15) + one-shot (1 order acknowledged, 2 flinch, 3 greeting) + its progress fraction
    float xi = floor(INSTANCE_CUSTOM.x);
    float phase = fract(INSTANCE_CUSTOM.x) * TAU;
    float lost = floor(floor(INSTANCE_CUSTOM.z) / 1024.0);
    float legsLost = mod(lost, 64.0);
    float limp = step(0.5, legsLost);
    // a missing leg breaks the rhythm: the stride hitches on the short side and the body sags toward it
    float limpSide = step(0.5, mod(legsLost, 8.0)) - step(0.5, floor(legsLost / 8.0));
    phase += limp * 0.6 * sin(phase);
    float seed = mod(xi, 16.0);
    float pose = floor(xi / 16.0);
    float yi = floor(INSTANCE_CUSTOM.y);
    float amp = fract(INSTANCE_CUSTOM.y);
    float yaw = -((floor(yi / 256.0) / 255.0 - 0.5) * 2.4);  // + in the renderer = toward mesh -X
    float pitch = (mod(yi, 256.0) / 255.0 - 0.5) * 1.2;      // + = looking up
    float cz = INSTANCE_CUSTOM.z;
    float zi = floor(cz);
    float bite = fract(cz);
    zi -= lost * 1024.0;
    float hitq = floor(zi / 64.0); zi -= hitq * 64.0;
    float hov = step(32.0, zi); zi -= hov * 32.0;
    float sel = step(16.0, zi); zi -= sel * 16.0;
    float carry = step(8.0, zi); zi -= carry * 8.0;
    float charge = step(4.0, zi); zi -= charge * 4.0;
    float block = step(2.0, zi);
    float wi = floor(INSTANCE_CUSTOM.w);
    float stage = floor(wi / 512.0); wi -= stage * 512.0;
    v_stage = stage;
    float etch = floor(wi / 64.0); wi -= etch * 64.0;
    v_etch = etch / 7.0;
    float kind = mod(wi, 4.0);
    v_pale = floor(wi / 4.0) / 15.0; // a callow just out of its cocoon: pale until its cuticle tans
    float ot = fract(INSTANCE_CUSTOM.w);
    float ack = kind == 1.0 ? sin(ot * PI) : 0.0;
    float flinch = kind == 2.0 ? sin(ot * PI) : 0.0;
    float greet = kind == 3.0 ? sin(ot * PI) : 0.0;
    float id = seed * 1.7 + seed * seed * 0.31;      // a stable phase per ant (INSTANCE_ID pops when slots are reused)
    float idle = 1.0 - smoothstep(0.02, 0.15, amp);
    float moving = smoothstep(0.0, 0.08, amp);
    float alive = 1.0 - dead;
    bool leg = part >= 1.0 && part <= 6.0;
    // the head group turns as one about the neck; a spider's cephalothorax carries its legs, so only its face turns
    bool headGroup = (part == 12.0 && pattern != 2) || part == 9.0 || part == 10.0 || part == 7.0 || part == 8.0
                  || ((part == 11.0 || part == 14.0 || part == 15.0) && region < 0.25);
    vec2 piv = UV2;
    vec3 p = VERTEX;
    vec3 n = NORMAL;
    // weight: the body rolls with the stride and sways when standing
    float roll = (sin(phase) * 0.035 * moving + idle * sin(TIME * 0.9 + id) * 0.02 + limpSide * (0.05 + 0.04 * sin(phase) * moving)) * alive;
    if (leg) {
        float side = part <= 3.0 ? 1.0 : -1.0;
        float idx = mod(part - 1.0, 3.0);
        float grp = mod(idx + (side > 0.0 ? 0.0 : 1.0), 2.0);
        float s = fract((phase + grp * PI) / TAU);
        // planted tripod: 55% of the cycle the foot stands on the ground and sweeps back under the body,
        // 45% it swings forward with a lift. The gait phase is distance-locked, so feet do not slide.
        float A = 0.45;
        float swing, lift;
        if (s < 0.55) { swing = A * (1.0 - 2.0 * s / 0.55); lift = 0.0; }
        else { float t = (s - 0.55) / 0.45; float e = t * t * (3.0 - 2.0 * t); swing = A * (2.0 * e - 1.0); lift = sin(t * PI) * 0.09 * w; }
        vec3 q = p - vec3(piv.x, 0.0, piv.y);
        q = rot_y(q, swing * side);
        n = rot_y(n, swing * side);
        p = q + vec3(piv.x, 0.0, piv.y);
        p.y += lift * moving;
        // a sentinel lifts its front legs; a spider rears to lunge; a beetle behind its shell draws its legs in
        if (pose == 1.0 && idx < 0.5) p.y += 0.03 * w;
        if (pattern == 2 && idx < 0.5) p.y += charge * 0.14 * w;
        if (pattern == 1) p.xz = mix(p.xz, piv + (p.xz - piv) * 0.75, block * w * 0.6);
        // flinch: legs splay
        p.xz = mix(p.xz, piv + (p.xz - piv) * 1.12, flinch * w * 0.6);
        // dead: legs fold in toward the body
        p.xz = mix(p.xz, piv + (p.xz - piv) * 0.45, dead * w);
        p.y = mix(p.y, p.y + 0.12 * w, dead);
        // the roll reaches the hips, not the planted feet
        p = rot_z(p, roll * (1.0 - w));
    } else if (part == 7.0 || part == 8.0) {
        float side = part == 7.0 ? 1.0 : -1.0;
        // antennae: an idle sweep; still and forward when listening; a flick on an order; swept back on a strike or a flinch
        float a = (sin(TIME * 5.0 + id * 1.7 + side) * 0.22 + sin(TIME * 2.3 + id) * 0.12) * (1.0 - ack) * (pose == 2.0 ? 0.3 : 1.0);
        // grooming: the two antennae tap in turn, quick and small
        if (pose == 3.0) a = sin(TIME * 11.0 + side * 1.57 + id) * 0.3;
        float sweep = (bite * 0.5 + flinch * 0.8) * side;
        vec3 q = p - vec3(piv.x, 0.0, piv.y);
        q = rot_y(q, (a * w + sweep) * alive);
        p = q + vec3(piv.x, 0.0, piv.y);
        // the flick: up, then pointing at the order; a greeting raises and quivers them; listening holds them high
        p.y += w * alive * (ack * 0.06 + greet * (0.05 + 0.012 * sin(TIME * 40.0)) + (pose == 2.0 ? 0.035 : 0.0) + (pose == 3.0 ? 0.02 * max(0.0, sin(TIME * 11.0 + side * 1.57 + id)) : 0.0));
    } else if (part == 9.0 || part == 10.0) {
        float side = part == 9.0 ? 1.0 : -1.0;
        // jaws gape on the bite; held open on sentry; a slow gnash at rest; wide on a charge
        float open = bite * 0.55 + sin(TIME * 3.0 + id) * (0.04 + 0.06 * hero) + charge * 0.45
                   + (pose == 1.0 ? 0.3 : 0.0) + idle * 0.12 * max(0.0, sin(TIME * 2.6 + id * 2.0));
        if (carry > 0.5) open = 0.16; // keep a firm grip on food instead of chewing through it
        vec3 q = p - vec3(piv.x, 0.0, piv.y);
        q *= 1.0 + 0.16 * stage; // each moult grows heavier jaws
        q = rot_y(q, open * side);
        n = rot_y(n, open * side);
        p = q + vec3(piv.x, 0.0, piv.y);
    } else if (pattern == 3 && region > 0.75) {
        // the wasp's gaster curls down and forward to drive the sting home
        vec3 q = p - vec3(0.0, 0.2, -0.06);
        q = rot_x(q, bite * 0.9);
        n = rot_x(n, bite * 0.9);
        p = q + vec3(0.0, 0.2, -0.06);
        p.y += sin(TIME * 2.0 + id) * 0.004;
    } else {
        // body bob, a slight gaster pump, and the gaster nodding at the petiole with the stride
        p.y += sin(phase * 2.0) * 0.006 * amp;
        if (part == 13.0) {
            p.y += sin(TIME * 2.0 + id) * 0.004;
            if (pattern == 0) {
                vec3 q = p - waist;
                float g = sin(2.0 * phase - 0.6) * 0.05 * amp;
                // a formicine sprays from the acidopore: the gaster swings down and forward under the body on the shot
                if (length(acid) > 0.0) g -= bite * 1.25 * alive;
                q = rot_x(q, g); n = rot_x(n, g);
                p = q + waist;
            }
        }
        // a beetle hunkers behind its shell wall; an ant dips to acknowledge, crouches on a flinch, rises to listen
        p.y += (-block * 0.035 - ack * 0.02 - flinch * 0.03 + (pose == 2.0 ? 0.02 : 0.0)) * alive;
    }
    if (headGroup) {
        // the head turns about the neck toward whatever the ant minds; a hero nods; a strike lunges it forward
        vec3 q = p - neck;
        float lunge = (bite * (0.03 + 0.03 * hero) + charge * 0.05) * alive;
        q.z += lunge;
        float pt = (-pitch + sin(TIME * 1.4 + id) * 0.04 * hero - flinch * 0.3 + charge * 0.2 + bite * 0.1 + (pose == 3.0 ? 0.12 : 0.0)) * alive;
        q = rot_x(q, pt); n = rot_x(n, pt);
        q = rot_y(q, yaw * alive); n = rot_y(n, yaw * alive);
        p = q + neck;
    }
    if (!leg) { p = rot_z(p, roll); n = rot_z(n, roll); }
    // every ant its own size (feet stay on the ground: the scale is about the origin at foot level)
    p *= 0.93 + 0.14 * hash1(seed + 0.37);
    // severed parts: whatever is missing collapses to nothing (the pieces lying on the ground are drawn on their own)
    if (lost > 0.5) {
        float b = 9.0; // thorax and the waist
        if (part >= 1.0 && part <= 8.0) b = part - 1.0;
        else if (region < 0.25 && part >= 9.0) b = 8.0;
        else if (region > 0.75) b = 10.0;
        if (bit_of(lost, b) > 0.5) { p = vec3(0.0, 0.1, 0.0); n = vec3(0.0, 1.0, 0.0); }
    }
    v_part = part;
    v_flash = hitq / 15.0;
    v_local = VERTEX;
    v_w = w;
    v_region = region;
    v_block = block;
    v_uv = UV;
    v_seed = seed;
    v_sel = sel;
    v_hov = hov;
    v_ao = COLOR.a; // occlusion baked by MeshKit.BakeAO: legs under the body, the neck, the waist
    v_cus = CUSTOM0;
    VERTEX = p;
    NORMAL = n;
}

void fragment() {
    // part ids arrive interpolated (12.9999...), so snap them before comparing
    float part = floor(v_part + 0.5);
    float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.0);
    vec3 c = body_color;
    float rough = 0.6; // chitin: velvet with a soft sheen, not glass
    if (part == 12.0 || part == 9.0 || part == 10.0) c = head_color;
    float cc = 0.18;
    float metal = 0.0;
    vec3 eyeEm = vec3(0.0);
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
            if (pattern == 0) {
                // the tergites are modelled; honey glows through the abdomen when the sun is behind it
                BACKLIGHT = gaster_color * 1.5 + vec3(0.26, 0.13, 0.03);
            } else {
                // tergite bands: darker toward each seam
                float band = smoothstep(0.42, 0.5, fract(v_local.z * 18.0 + 0.3));
                c *= 1.0 - band * 0.35;
            }
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
    if (pattern == 4 && part == 13.0) {
        // stag elytra: broad shallow grooves, a pale polished margin and a dark suture
        float groove = smoothstep(0.25, 0.5, abs(fract(v_local.x * 22.0) - 0.5));
        c = gaster_color * (0.6 + 0.4 * groove);
        c = mix(c, gaster_color * 1.7, smoothstep(0.15, 0.185, abs(v_local.x)));
        c *= 1.0 - (1.0 - smoothstep(0.006, 0.014, abs(v_local.x))) * 0.6;
    }
    if (pattern == 4 && part == 12.0) c = head_color * (0.85 + 0.3 * smoothstep(0.0, 0.05, v_local.y - 0.16)); // polished crown
    if (part >= 1.0 && part <= 8.0) {
        c = leg_color; rough = 0.62; BACKLIGHT = leg_color * 0.6;
        if (pattern == 2) c *= 0.65 + 0.55 * step(0.5, fract(v_w * 4.0 + 0.2)); // banded legs
        if (pattern == 1 || pattern == 4) c *= 0.8 + 0.2 * step(0.85, v_w);
        if (part >= 7.0) BACKLIGHT += vec3(0.2, 0.1, 0.02) * v_w; // light through the antenna tips
    }
    if (part == 9.0 || part == 10.0) c *= 0.6;
    bool eye = part == 11.0 && pattern == 0 && v_region < 0.25;
    if (part == 11.0 && !eye) {
        // the hard black parts: a spider's fangs, a wasp's sting, a beetle's suture, a fire ant's sting
        c = vec3(0.012); rough = 0.3; cc = 0.0;
    }
    if (eye) {
        // a compound eye: hundreds of ommatidia, each a tiny lens. A hexagonal lattice over the eye's surface tilts
        // each facet's normal a little, so the eye breaks the light into a fine glitter instead of one smooth dome
        vec2 q = vec2(v_local.z * 1.1547, v_local.y + v_local.z * 0.57735) * 520.0;
        vec2 cell = floor(q), f = fract(q);
        vec2 ctr = (f.x + f.y < 1.0) ? vec2(0.33) : vec2(0.67);
        vec2 dd = f - ctr;
        float edge = smoothstep(0.18, 0.3, length(dd));
        vec3 tilt = (VIEW_MATRIX * MODEL_MATRIX * vec4(0.0, dd.y, dd.x, 0.0)).xyz;
        NORMAL = normalize(NORMAL + tilt * 0.35);
        // dark warm lenses: a metal-like reflection tinted brown, so the sky does not turn the eye blue
        c = mix(vec3(0.06, 0.04, 0.025), vec3(0.012, 0.008, 0.006), edge);
        rough = 0.2; cc = 0.0; metal = 0.55;
        // the spark of life: the sky caught in the eye, a small bright window that slides over the dome as the head
        // turns (the head group's rotation already moved NORMAL); dimmed underground, where only the lamps are
        vec3 r = reflect(-VIEW, NORMAL);
        vec3 sky = normalize((VIEW_MATRIX * vec4(0.35, 1.0, 0.25, 0.0)).xyz);
        float spark = smoothstep(0.986, 0.996, dot(r, sky));
        eyeEm += vec3(1.0, 0.93, 0.8) * spark * 1.6 * under_ambient((INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz);
    }
    if (pattern == 0 && !eye && part != 14.0 && part != 15.0) {
        // cuticle micro-relief: two octaves of smooth noise tilt the normal, finest on the legs, coarse on a rugose hero
        vec3 sp = v_local * (part >= 1.0 && part <= 8.0 ? 300.0 : 180.0);
        vec3 jit = vec3(vnoise(sp), vnoise(sp + 17.3), vnoise(sp + 41.9)) - 0.5
                 + 0.5 * (vec3(vnoise(sp * 2.7 + 5.1), vnoise(sp * 2.7 + 23.4), vnoise(sp * 2.7 + 61.2)) - 0.5);
        // (a scanned body carries the real relief in its normal map instead)
        bool scanned = has_detail > 0.5 && v_cus.z > 0.5;
        if (scanned) { NORMAL_MAP = texture(detail_normal, v_uv).rgb; NORMAL_MAP_DEPTH = 1.0; }
        else NORMAL = normalize(NORMAL + (VIEW_MATRIX * MODEL_MATRIX * vec4(jit, 0.0)).xyz * sculpture * 1.6);
        // punctures, each with a hair socket: only near the camera, where they are more than a pixel (and phones skip them)
        if (!scanned && -VERTEX.z < 18.0 && v_cus.x < 0.5) {
            float pit = 1.0 - smoothstep(0.1, 0.24, worley(v_local * (part >= 1.0 && part <= 8.0 ? 260.0 : 150.0)));
            c *= 1.0 - 0.45 * pit * (1.0 - smoothstep(10.0, 18.0, -VERTEX.z));
            rough += 0.12 * pit;
        }
        cc = 0.05; rough = max(rough, 0.66); // satin, not lacquer: the sheen comes from the setae and the sculpture
        // the pale seams between tergites and the stretched membrane of a replete
        float mem = clamp(v_cus.x, 0.0, 1.0);
        c = mix(c, mix(vec3(0.16, 0.1, 0.05), vec3(0.62, 0.36, 0.1), step(0.5, mem)), mem);
        rough = mix(rough, 0.18, mem); cc = mix(cc, 0.8, mem);
        BACKLIGHT = mix(BACKLIGHT, vec3(0.9, 0.5, 0.12), mem);
        // setae: fine golden hairs that catch the sun
        if (v_cus.y > 0.5) { c = vec3(0.4, 0.29, 0.15); rough = 0.4; BACKLIGHT = vec3(0.5, 0.35, 0.15); }
    }
    if (part == 14.0) {
        // crests, quills, nozzles, studs and spinnerets: the accent metal, brightest at the tips
        c = accent * (0.75 + 0.35 * v_w);
        metal = 0.85; rough = 0.33; cc = 0.5;
    }
    if (part == 15.0) {
        // armour plates: dark lacquered chitin with a rim of the accent metal along the edge
        float rim = smoothstep(0.3, 0.46, v_uv.y);
        c = mix(body_color * 0.9 + vec3(0.02), accent, rim);
        metal = mix(0.45, 0.9, rim); rough = mix(0.38, 0.3, rim); cc = 0.6;
    }
    if (pattern == 2) {
        // velvet hair: matte, speckled, soft sheen
        float hair = hash3(floor(v_local * 320.0));
        c *= 0.8 + 0.4 * hair;
        if (part != 11.0) { rough = 0.78; cc = 0.0; }
    } else if (part != 11.0 && part != 14.0 && part != 15.0) {
        // micro-hair on every other creature: a fine fuzz that catches light at grazing angles and breaks the plastic dome
        float hair = hash3(floor(v_local * 300.0));
        c += (c * 1.2 + vec3(0.02)) * pow(fres, 3.0) * (0.5 + 0.5 * hair);
        rough += (hair - 0.5) * 0.08;
    }
    if (part != 11.0) {
        // the fine grain of chitin, a speckle that reads in close-up and vanishes at distance
        if (pattern == 0 && part != 14.0 && part != 15.0) c *= 0.9 + 0.2 * hash3(floor(v_local * 230.0));
        // no two ants quite alike
        c *= 0.94 + 0.12 * hash1(v_seed + 1.3);
        rough += (hash1(v_seed + 2.9) - 0.5) * 0.12;
    }
    if ((pattern == 1 || pattern == 4) && part != 11.0 && part != 14.0 && part != 15.0) { rough = pattern == 4 ? 0.36 : 0.42; cc = 0.4; metal = 0.15; }
    if (pattern == 3 && part != 11.0 && part != 14.0) { rough = 0.48; cc = 0.25; }
    if (hero > 0.0 && pattern != 0 && part != 11.0 && part != 14.0 && part != 15.0) {
        // metal filigree in the colony's colour: bands on the abdomen, a crest line on the head, edged mandibles
        float gband = part == 13.0 ? step(0.74, fract(v_local.z * 11.0 + 0.1)) : 0.0;
        float crest = part == 12.0 ? smoothstep(0.02, 0.0, abs(v_local.x)) * step(0.17, v_local.y) : 0.0;
        float jaw = (part == 9.0 || part == 10.0) ? step(0.75, v_w) : 0.0;
        float g = max(max(gband, crest), jaw) * hero;
        c = mix(c, accent, g);
        metal = mix(metal, 0.9, g);
        rough = mix(rough, 0.38, g);
    }
    // structural colour: hue shifts with viewing angle, strongest at grazing angles
    vec3 rainbow = 0.5 + 0.5 * cos(6.2831 * (fres * 0.9 + vec3(0.55, 0.75, 0.95)));
    c += rainbow * irid * (0.03 + 0.22 * fres) * (part >= 1.0 && part <= 10.0 ? 0.3 : 1.0);
    if (v_stage > 0.5 && part != 11.0 && part != 14.0) {
        // an evolved fighter: darker, harder chitin, and the healed scars of the fights it fed on
        c *= 1.0 - 0.07 * v_stage;
        // thin healed scratches: a few short lines across the head, thorax and gaster, more with each stage
        float stroke = smoothstep(0.46, 0.5, abs(fract(v_local.x * 23.0 + v_local.z * 31.0 + v_seed * 0.37) - 0.5));
        float patch = step(0.86 - 0.05 * v_stage, hash3(floor(v_local * vec3(9.0, 40.0, 9.0))));
        float body = (part == 0.0 || part == 12.0 || part == 13.0) ? 1.0 : 0.0;
        c += vec3(0.05, 0.038, 0.026) * stroke * patch * body;
        rough += 0.05 * v_stage;
    }
    if (v_etch > 0.0 && part != 11.0 && part != 14.0) {
        // formic acid eats pits into the chitin: dull, rough, faintly yellowed craters
        float pit = step(1.0 - v_etch * 0.45, hash3(floor(v_local * 95.0)));
        c = mix(c, c * 0.45 + vec3(0.05, 0.045, 0.01), pit);
        rough = mix(rough, 0.95, pit); cc *= 1.0 - pit;
    }
    if (v_pale > 0.0 && part != 11.0) {
        // callow cuticle: soft translucent amber-cream that tans to the colony's colour within seconds
        vec3 callow = part == 13.0 ? vec3(0.46, 0.31, 0.16) : part >= 1.0 && part <= 8.0 ? vec3(0.56, 0.42, 0.25) : vec3(0.52, 0.36, 0.2);
        c = mix(c, callow, v_pale);
        rough = mix(rough, 0.5, v_pale); metal *= 1.0 - v_pale; cc *= 1.0 - v_pale * 0.6;
        BACKLIGHT = mix(BACKLIGHT, vec3(0.5, 0.34, 0.16), v_pale);
    }
    BACKLIGHT *= 1.0 - dead * 0.85; // dead tissue no longer glows through
    ALBEDO = c * (1.0 - dead * 0.35) * mix(0.72, 1.0, v_ao);
    AO = v_ao * under_ambient((INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz);
    AO_LIGHT_AFFECT = 0.35;
    ROUGHNESS = clamp(rough + dead * 0.3, 0.05, 1.0);
    METALLIC = metal;
    SPECULAR = pattern == 0 ? 0.14 : 0.32;
    CLEARCOAT = cc;
    CLEARCOAT_ROUGHNESS = 0.42;
    RIM = pattern == 2 ? 0.6 : 0.35;
    RIM_TINT = 0.6;
    // brief warm flicker when bitten; kept subtle so big targets under constant attack keep their colour
    EMISSION = vec3(1.0, 0.3, 0.1) * v_flash * v_flash * (pattern == 0 ? 0.3 : 0.08) + eyeEm;
    // a beetle behind its shell wall: the wing cases gleam hard along their edges
    if (v_block > 0.5 && part != 11.0) { EMISSION += accent * (0.08 + 0.35 * fres) * (0.7 + 0.3 * sin(TIME * 6.0)); ROUGHNESS = 0.25; }
    if (part == 13.0 && length(acid) > 0.0 && v_cus.x > 0.5) {
        // a replete's membrane: amber formic acid seen through taut cuticle, glowing where the sun comes through
        float pulse = 0.85 + 0.15 * sin(TIME * 2.2);
        // the acid inside is not even: darker where it is deep, paler veins where the membrane is stretched thinnest
        float mottle = vnoise(v_local * 38.0) * 0.6 + vnoise(v_local * 110.0) * 0.4;
        ALBEDO = mix(vec3(0.075, 0.03, 0.006), vec3(0.2, 0.09, 0.016), mottle) * (1.0 - dead * 0.35);
        EMISSION += vec3(0.16, 0.065, 0.01) * 0.05 * pulse * (1.0 - dead);
        BACKLIGHT = vec3(0.32, 0.15, 0.03) * (0.6 + 0.6 * mottle) * (1.0 - dead);
        CLEARCOAT = 1.0; CLEARCOAT_ROUGHNESS = 0.08; ROUGHNESS = 0.12; METALLIC = 0.0;
    }
}
";

    /// Same signed-distance union as NestGeometry: nest-local from.xy, to.xy, room radii.xy,
    /// gallery half-width. The roof follows the camera ray to the shared horizontal floor.
    public const string NetworkFn = @"
uniform float xray_on = 0.0;
uniform vec2 xray_center = vec2(-1000.0);
uniform float net_ground_y = 0.0;
uniform float net[224];
uniform int net_count = 0;
float nest_union(float a, float b) {
    float h = max(0.45 - abs(a - b), 0.0) / 0.45;
    return min(a, b) - h * h * 0.1125;
}
float nest_field(vec2 p) {
    float best = 100000.0;
    for (int i = 0; i < net_count; i++) {
        int k = i * 7;
        vec2 a = vec2(net[k], net[k + 1]);
        vec2 b = vec2(net[k + 2], net[k + 3]);
        vec2 radii = vec2(net[k + 4], net[k + 5]);
        vec2 ab = b - a;
        float t = clamp(dot(p - a, ab) / max(dot(ab, ab), 0.0001), 0.0, 1.0);
        float tube = length(p - a - ab * t) - net[k + 6];
        float room = (length((p - b) / radii) - 1.0) * min(radii.x, radii.y);
        best = nest_union(best, nest_union(tube, room));
    }
    return best + 0.10 * sin(p.x * 2.1 + sin(p.y * 1.3)) * sin(p.y * 1.8)
                + 0.045 * sin(p.x * 5.7 + p.y * 4.3);
}
float net_dist(vec3 p, vec3 cam) {
    if (net_count == 0 || distance(p.xz, xray_center) > 65.0) return 100000.0;
    vec3 dir = p - cam;
    vec2 shift = dir.y < -0.001 ? dir.xz / (-dir.y) : vec2(0.0);
    float sl = length(shift);
    if (sl > 6.0) shift *= 6.0 / sl;
    vec2 q = p.xz + shift * (p.y - net_ground_y) - xray_center; // the cut is at the surface: the pit's walls below are real geometry
    return nest_field(q);
}
float glass_at(vec3 p, vec3 cam) {
    return xray_on > 0.001 ? xray_on * (1.0 - smoothstep(-0.15, 0.85, net_dist(p, cam))) : 0.0;
}
";

    /// The terrain while the Glass Earth is open: the same shader, drawn in the transparent pass so the soil over
    /// the galleries thins to a translucent roof instead of vanishing. Swapped in only while the earth is opened.
    public static string TerrainRoof => _roof ??= Terrain.Replace("//ROOF_MODE", "render_mode depth_draw_never;").Replace("//ROOF_ALPHA", "ALPHA = 1.0 - glass * 0.92;");
    static string _roof;

    public static readonly string Terrain = @"
shader_type spatial;
//ROOF_MODE
" + Fow + @"
uniform sampler2D noise_a : filter_linear_mipmap, repeat_enable;
uniform sampler2D noise_b : filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D normal_a : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D normal_b : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D normal_c : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable; // the sand grains themselves
uniform sampler2D pher_tex : filter_linear, repeat_disable;
uniform float pher_view = 0.25;
uniform vec2 entrance_a = vec2(-1000.0);
uniform vec2 entrance_b = vec2(-1000.0);
uniform int detail = 1; // graphics tier: 0 phones (base and hollow sets only), 1 and up every ground set
uniform sampler2D g_base_d : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D g_base_n : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D g_base_a : filter_linear_mipmap, repeat_enable;
uniform sampler2D g_crest_d : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D g_crest_n : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D g_crest_a : filter_linear_mipmap, repeat_enable;
uniform sampler2D g_hollow_d : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D g_hollow_n : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D g_litter_d : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D g_litter_n : hint_normal, filter_linear_mipmap, repeat_enable;
// Glass Earth: while xray_on, the soil directly over the colony's galleries and rooms is cut away
varying vec3 wpos;
varying vec4 vcol;   // r baked shade, g excavated sand, b pine thatch, a 0.5 level / <0.5 crest / >0.5 hollow
varying vec3 wnrm;
void vertex() {
    wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    wnrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
    vcol = COLOR;
    TANGENT = normalize(vec3(1.0, 0.0, 0.0) - NORMAL * NORMAL.x);
    BINORMAL = normalize(cross(TANGENT, NORMAL));
}
" + NetworkFn + @"
void fragment() {
    vec2 uv = wpos.xz;
    // Actual small entrance holes remain readable with build mode both open and closed.
    vec2 mouth = distance(uv, entrance_a) < distance(uv, entrance_b) ? uv - entrance_a : uv - entrance_b;
    if (length(mouth) < 0.82 + 0.055 * sin(atan(mouth.y, mouth.x) * 7.0)) discard;
    // Glass Earth: the soil over the galleries thins to a translucent roof (the roof variant of this shader) and
    // carries the shadow of the hollow beneath it: a soft band along the cavity's edge, lighter over its middle
    float nd = xray_on > 0.001 ? net_dist(wpos, CAMERA_POSITION_WORLD) : 1e9;
    // the pit is open: no soil over it, a thin crumbling fringe at the lip, a shadow band just beyond
    float lip = 1.25;
    if (xray_on > 0.5 && nd < lip) discard;
    float glass = xray_on * (1.0 - smoothstep(lip, lip + 0.55, nd));
    float band = exp(-pow((nd - lip - 0.3) / 0.8, 2.0)) * xray_on;
    // relief: how far this point sits below its surroundings (+) or above them (-), and how steep it is
    float hollow = clamp((vcol.a - 0.5) * 2.0, -1.0, 1.0);
    float inHollow = smoothstep(0.05, 0.7, hollow);
    float onCrest = smoothstep(0.05, 0.7, -hollow);
    float steep = smoothstep(0.08, 0.35, 1.0 - wnrm.y);
    float n1 = texture(noise_a, uv * 0.0035).r;
    float n2 = texture(noise_a, uv * 0.017 + 0.3).r;
    float g2 = texture(noise_b, uv * 0.41 + 0.7).r;
    // photographed soil (CC0 ground sets, assets/nature/ground), blended by the lie of the land. The base is read
    // twice, at two scales and angles, and the two are mixed by slow noise so its tile never repeats visibly.
    vec2 ua = uv / 26.0;
    vec2 ub = mat2(vec2(0.80, 0.60), vec2(-0.60, 0.80)) * uv / 18.0 + 0.37;
    float mixAB = smoothstep(0.35, 0.65, n2);
    vec3 baseC = mix(texture(g_base_d, ua).rgb, texture(g_base_d, ub).rgb, mixAB);
    vec3 baseN = mix(texture(g_base_n, ua).rgb, texture(g_base_n, ub).rgb, mixAB);
    vec3 baseA = mix(texture(g_base_a, ua).rgb, texture(g_base_a, ub).rgb, mixAB);
    // where each layer lies, sharpened against the base's own height so stones stand proud of the fines
    float hBase = baseA.b;
    float edgeD = min(min(wpos.x, map_size - wpos.x), min(wpos.z, map_size - wpos.z));
    float wCrest = clamp(onCrest * 0.9 + steep * 0.6 + smoothstep(0.62, 0.8, n1) * 0.35, 0.0, 1.0);
    float wHollow = clamp(inHollow * 1.1 + smoothstep(0.7, 0.9, n1) * 0.3, 0.0, 1.0);
    float wLitter = clamp((1.0 - smoothstep(8.0, 30.0, edgeD)) * 0.9 + smoothstep(0.66, 0.82, n2) * 0.55 * (1.0 - inHollow), 0.0, 1.0);
    vec3 col = baseC; vec3 nmT = baseN; float rough = baseA.g; float occ = baseA.r;
    if (detail > 0) {
        vec2 uc = uv / 30.0 + 0.19;
        vec3 crestA = texture(g_crest_a, uc).rgb;
        float tC = smoothstep(0.0, 0.25, wCrest + crestA.b - hBase - 0.35);
        col = mix(col, texture(g_crest_d, uc).rgb, tC); nmT = mix(nmT, texture(g_crest_n, uc).rgb, tC);
        rough = mix(rough, crestA.g, tC); occ = mix(occ, crestA.r, tC);
    }
    vec2 uh = uv / 26.0 + 0.53;
    float tH = smoothstep(0.0, 0.3, wHollow - hBase * 0.5 - 0.15);
    col = mix(col, texture(g_hollow_d, uh).rgb * vec3(1.02, 0.98, 0.92), tH);
    nmT = mix(nmT, texture(g_hollow_n, uh).rgb, tH);
    if (detail > 0) {
        vec2 ul = uv / 40.0 + 0.71;
        float tL = smoothstep(0.0, 0.25, wLitter - hBase * 0.4 - 0.2);
        col = mix(col, texture(g_litter_d, ul).rgb, tL); nmT = mix(nmT, texture(g_litter_n, ul).rgb, tL);
    }
    col *= mix(1.0, occ, 0.8) * (0.9 + 0.2 * n1);
    // close to the camera, a macro layer: the pebbly ground set at a tenth of the base tile, so at ant framing the
    // soil shows individual grains, grit and pebbles; it fades out by ~30 units, where the base sets carry the look
    float camD = length(CAMERA_POSITION_WORLD - wpos);
    float macro = detail > 0 ? 1.0 - smoothstep(10.0, 30.0, camD) : 0.0;
    vec3 macroN = vec3(0.5, 0.5, 1.0);
    if (macro > 0.001) {
        vec2 um = mat2(vec2(0.6, 0.8), vec2(-0.8, 0.6)) * uv / 2.6 + 0.13;
        vec3 mc = texture(g_crest_d, um).rgb;
        float ml = dot(mc, vec3(0.3, 0.59, 0.11));
        // its own stones show through where they stand proud; elsewhere it only adds grain to the base colour
        float stones = smoothstep(0.42, 0.62, texture(g_crest_a, um).b) * (0.35 + 0.65 * wHollow);
        col = mix(col, col * clamp(0.5 + ml * 1.4, 0.65, 1.3), macro * 0.8);
        col = mix(col, mc * vec3(1.05, 0.96, 0.86), stones * macro * 0.7);
        macroN = texture(g_crest_n, um).rgb;
    }
    // the grains themselves: a fine octave of the photographed sand, in luminance only so the layer colours stay
    vec3 fineN = vec3(0.5, 0.5, 1.0);
    if (detail > 0) {
        vec2 uf = mat2(vec2(0.31, 0.95), vec2(-0.95, 0.31)) * uv / 6.5 + 0.61;
        float fl = dot(texture(g_base_d, uf).rgb, vec3(0.3, 0.59, 0.11));
        col *= clamp(0.45 + fl * 1.35, 0.6, 1.35);
        fineN = texture(g_base_n, uf).rgb;
    }
    float quartz = 0.0;
    // excavated sand around the black colony's nest: the same soil, sifted paler
    vec3 sand = baseC * vec3(1.18, 1.12, 1.02) * (0.85 + 0.3 * g2);
    col = mix(col, sand, vcol.g);
    // pine thatch mound of the red colony
    vec3 thatch = mix(vec3(0.20, 0.11, 0.05), vec3(0.36, 0.20, 0.09), g2) * (0.7 + 0.4 * n2);
    col = mix(col, thatch, vcol.b);
    col *= vcol.r;
    float wet = smoothstep(0.55, 0.85, n1) * (1.0 - onCrest * 0.6) + inHollow * 0.3;
    // the freshly cut soil at the opening's edge: darker and damper than the surface
    col *= 1.0 - 0.32 * band - 0.18 * glass; // the roof stays readable over the opened galleries
    col = mix(col, col * vec3(1.0, 0.9, 0.8), 0.5 * max(band, glass));
    ALBEDO = col;
    ROUGHNESS = clamp(mix(rough, rough * 0.8, clamp(wet, 0.0, 1.0)), 0.4, 1.0);
    SPECULAR = 0.3;
    // the photographed relief on top of the broad undulations of the land
    vec2 nm = (nmT.xy - 0.5) * (detail > 0 ? 1.0 : 0.7) + (fineN.xy - 0.5) * 0.9 + (macroN.xy - 0.5) * 1.3 * macro + (texture(normal_a, uv * 0.11).xy - 0.5) * 0.6;
    nm = mix(nm, nm * 0.3, vcol.b); // the thatch is needles, not sand
    NORMAL_MAP = normalize(vec3(nm, 0.5)) * 0.5 + 0.5;
    NORMAL_MAP_DEPTH = detail > 0 ? 1.4 : 1.0;
    float ph = texture(pher_tex, uv / map_size).r;
    float f = fow(wpos);
    ALBEDO = mix(ALBEDO, vec3(1.0, 0.72, 0.32), clamp(ph, 0.0, 1.0) * pher_view * 0.35) * f;
    EMISSION = vec3(1.0, 0.55, 0.12) * clamp(ph, 0.0, 1.5) * pher_view * 0.35 * step(0.5, f);
    //ROOF_ALPHA
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
    ROUGHNESS = 0.9 - b * 0.12;
    SPECULAR = 0.25;
    NORMAL_MAP = texture(normal_b, UV * 3.0).rgb;
    NORMAL_MAP_DEPTH = 0.8;
}
";

    /// Fallen leaves: real scanned leaves (ambientCG Leaf001-003, CC0) in one atlas, cut out by their own outline and
    /// dried from green through yellow to brown by age, keeping the photo's veins and blemishes.
    /// Cotton wool: a mass of fine strands, off-white and a little soiled where it touched the sand, with a soft
    /// fuzz at the silhouette and light passing through.
    public const string Cotton = @"
shader_type spatial;
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
varying vec3 lp;
void vertex() { lp = VERTEX; }
void fragment() {
    float f1 = texture(noise_b, lp.xy * vec2(7.0, 1.3) + lp.z * 0.7).r;
    float f2 = texture(noise_b, lp.zy * vec2(1.2, 8.0) + 0.37).r;
    float f3 = texture(noise_b, (lp.xz + lp.y) * vec2(11.0, 2.0) + 0.71).r;
    float strands = smoothstep(0.45, 0.85, max(max(f1, f2), f3));
    vec3 col = vec3(0.7, 0.67, 0.6) * (0.6 + 0.5 * strands);
    col = mix(col, vec3(0.56, 0.46, 0.34), smoothstep(-0.3, -1.1, lp.y) * 0.55);   // soiled underneath
    float fz = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.5);
    ALBEDO = col + fz * 0.1;
    ROUGHNESS = 1.0;
    SPECULAR = 0.15;
    BACKLIGHT = vec3(0.42, 0.38, 0.3);
    NORMAL_MAP = normalize(vec3((f1 - 0.5) * 0.9, (f3 - 0.5) * 0.9, 1.0)) * 0.5 + 0.5;
}
";

    /// The collector's paper label on the tube: the scanned label, lines along the tube, a little translucent paper.
    public const string TubeLabel = @"
shader_type spatial;
render_mode cull_back;
uniform sampler2D label : source_color, filter_linear_mipmap_anisotropic, repeat_disable;
void fragment() {
    ALBEDO = texture(label, vec2(UV.y, 1.0 - UV.x)).rgb;
    ROUGHNESS = 0.85;
    SPECULAR = 0.25;
    BACKLIGHT = ALBEDO * 0.25;
}
";

    /// Food cut from a scan: up to three planes slice the piece and the faces they open are drawn as its inside
    /// (bread crumb, apple flesh, seed kernel). `bite` (0..1, how much has been eaten) pushes the cuts inward.
    /// outside: 0 the scan's own photo, 1 a striped sunflower seed shell, 2 a husked grass grain.
    public const string FoodCut = @"
shader_type spatial;
render_mode cull_disabled;
uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, hint_default_white;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap;
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
uniform vec4 cut0 = vec4(0.0, 1.0, 0.0, 1e6);
uniform vec4 cut1 = vec4(0.0, 1.0, 0.0, 1e6);
uniform vec4 cut2 = vec4(0.0, 1.0, 0.0, 1e6);
uniform float bite = 0.0;
uniform float bite_depth = 0.0;
uniform int inside = 0;
uniform int outside = 0;
varying vec3 lp;
void vertex() { lp = VERTEX; }
float plane(vec4 c) { return c.w > 1e5 ? -1e6 : dot(c.xyz, lp) - (c.w - bite * bite_depth); }
void fragment() {
    float d0 = plane(cut0), d1 = plane(cut1), d2 = plane(cut2);
    if (d0 > 0.0 || d1 > 0.0 || d2 > 0.0) discard;
    float n1 = texture(noise_b, lp.xz * 9.0 + lp.y * 3.1).g;
    float n2 = texture(noise_b, lp.zy * 23.0 + 0.41).g;
    if (FRONT_FACING) {
        if (outside == 0) {
            ALBEDO = texture(albedo_tex, UV).rgb;
            NORMAL_MAP = texture(normal_tex, UV).rgb;
            ROUGHNESS = 0.7;
        } else if (outside == 1) {
            // sunflower seed: black and cream stripes running its length, glossy shell
            float a = atan(lp.x, lp.y);
            // dark grey-black shell with thin pale stripes, as a sunflower seed has
            float stripe = smoothstep(0.82, 0.95, abs(sin(a * 5.0 + n1 * 1.2)));
            ALBEDO = mix(vec3(0.07, 0.065, 0.06), vec3(0.62, 0.58, 0.5), stripe * 0.75) * (0.85 + 0.25 * n2);
            ROUGHNESS = 0.35; CLEARCOAT = 0.4; CLEARCOAT_ROUGHNESS = 0.3;
        } else {
            ALBEDO = mix(vec3(0.55, 0.4, 0.2), vec3(0.78, 0.64, 0.4), n1) * (0.85 + 0.2 * n2);
            ROUGHNESS = 0.6;
        }
    } else {
        // the opened face: lit as the flat plane it is
        vec3 n = d0 >= d1 && d0 >= d2 ? cut0.xyz : d1 >= d2 ? cut1.xyz : cut2.xyz;
        NORMAL = normalize((VIEW_MATRIX * (MODEL_MATRIX * vec4(n, 0.0))).xyz);
        if (inside == 0) {
            // bread: soft cream crumb full of gas pockets
            float pore = smoothstep(0.55, 0.7, n2) + smoothstep(0.62, 0.75, n1) * 0.6;
            ALBEDO = vec3(0.86, 0.74, 0.52) * (1.0 - 0.45 * clamp(pore, 0.0, 1.0)) * (0.9 + 0.15 * n1);
            ROUGHNESS = 0.95;
        } else if (inside == 1) {
            // apple flesh, browning where it has been open longest
            ALBEDO = mix(vec3(0.95, 0.88, 0.64), vec3(0.72, 0.5, 0.24), clamp(0.25 + bite * 0.6 + (n1 - 0.5) * 0.4, 0.0, 1.0));
            ROUGHNESS = 0.45; SPECULAR = 0.6;
        } else if (inside == 2) {
            // berry pulp: wet, translucent, deep red
            ALBEDO = mix(vec3(0.45, 0.04, 0.05), vec3(0.85, 0.75, 0.68), smoothstep(0.6, 0.8, n2) * 0.4);
            ROUGHNESS = 0.15; SPECULAR = 0.7;
        } else {
            ALBEDO = vec3(0.88, 0.84, 0.72) * (0.9 + 0.15 * n1);
            ROUGHNESS = 0.6;
        }
    }
}
";

    /// A currant: glossy, translucent red skin with the dried calyx at its crown; `burst` splits it open.
    public const string Currant = @"
shader_type spatial;
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
uniform float tone = 1.0;
uniform float burst = 0.0;
varying vec3 lp;
void vertex() { lp = VERTEX; }
void fragment() {
    float n = texture(noise_b, lp.xz * 1.7 + lp.y).r;
    vec3 skin = mix(vec3(0.22, 0.015, 0.025), vec3(0.5, 0.05, 0.06), tone * (0.7 + 0.4 * n));
    // faint pale veins under the skin, as currants have
    float a = atan(lp.x, lp.z);
    skin = mix(skin, skin * 1.6 + vec3(0.06, 0.0, 0.0), smoothstep(0.96, 1.0, abs(sin(a * 5.0))) * 0.35 * (1.0 - abs(lp.y)));
    // the dried calyx: a dark star at the crown
    float crown = smoothstep(0.955, 0.985, lp.y) * (0.6 + 0.4 * smoothstep(0.2, 0.6, abs(sin(a * 2.5))));
    vec3 col = mix(skin, vec3(0.12, 0.07, 0.03), clamp(crown, 0.0, 1.0));
    // a burst berry: a split down one side, wet dark pulp showing
    float split = burst * smoothstep(0.08, 0.0, abs(lp.x - 0.15 * sin(lp.y * 6.0))) * step(0.0, lp.z);
    col = mix(col, vec3(0.2, 0.0, 0.02), split);
    ALBEDO = col;
    ROUGHNESS = 0.12 + 0.1 * n;
    SPECULAR = 0.6;
    CLEARCOAT = 0.35;
    CLEARCOAT_ROUGHNESS = 0.15;
    BACKLIGHT = vec3(0.45, 0.04, 0.03) * (1.0 - crown);
    RIM = 0.2;
}
";

    /// Bread crumbs: irregular broken chunks (the scanned stones' shapes, which are real broken forms) in bread:
    /// golden-brown crust on the faces that were the loaf's outside (up), soft porous crumb on the broken sides.
    public const string Crumb = @"
shader_type spatial;
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
uniform float seed = 0.0;
varying vec3 lp;
varying vec3 ln;
void vertex() { lp = VERTEX; ln = NORMAL; }
float tri(vec3 p, vec3 an, float s, float o) {
    return texture(noise_b, p.yz * s + o).r * an.x + texture(noise_b, p.xz * s + o).r * an.y + texture(noise_b, p.xy * s + o).r * an.z;
}
void fragment() {
    vec3 an = abs(ln); an /= an.x + an.y + an.z;
    float n = tri(lp, an, 9.0, seed);
    float pore = tri(lp, an, 41.0, seed + 0.37);
    float crust = smoothstep(0.05, 0.55, ln.y + (n - 0.5) * 0.8);
    vec3 crumbC = vec3(0.78, 0.62, 0.38) * (1.0 - 0.6 * smoothstep(0.5, 0.68, pore)) * (0.85 + 0.15 * n);
    vec3 crustC = mix(vec3(0.33, 0.14, 0.04), vec3(0.62, 0.33, 0.1), n) * (0.85 + 0.25 * pore);
    ALBEDO = mix(crumbC, crustC, crust);
    ROUGHNESS = mix(0.95, 0.5, crust);
    SPECULAR = mix(0.2, 0.45, crust);
    NORMAL_MAP = normalize(vec3((pore - 0.5) * (1.0 - crust) * 1.6, (n - 0.5) * 0.6, 1.0)) * 0.5 + 0.5;
}
";

    /// A sunflower seed modelled in Blender: UV.x runs around the shell, UV.y along it. Dark grey-black with thin
    /// pale stripes along its length, a little glossy; the split halves show the cream kernel inside.
    public const string Seed = @"
shader_type spatial;
render_mode cull_disabled;
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
uniform float seed = 0.0;
void fragment() {
    float n = texture(noise_b, UV * vec2(3.0, 9.0) + seed).r;
    if (FRONT_FACING) {
        float stripes = smoothstep(0.82, 0.94, abs(sin((UV.x * 5.0 + n * 0.25) * 3.14159)));
        float tip = smoothstep(0.85, 1.0, UV.y);
        vec3 shell = mix(vec3(0.05, 0.048, 0.045), vec3(0.6, 0.57, 0.5), stripes * (1.0 - tip * 0.6));
        ALBEDO = shell * (0.85 + 0.3 * n);
        ROUGHNESS = 0.5 + 0.2 * n;
        SPECULAR = 0.35;
        CLEARCOAT = 0.25; CLEARCOAT_ROUGHNESS = 0.4;
    } else {
        ALBEDO = vec3(0.86, 0.8, 0.66) * (0.85 + 0.2 * n);
        ROUGHNESS = 0.7;
    }
}
";

    /// A currant modelled in Blender: vertex colour 1 is skin, 0 the dried calyx and stalk. Glossy, light passing
    /// through the skin, pale veins, each berry its own red.
    public const string BerrySkin = @"
shader_type spatial;
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
uniform float tone = 1.0;
varying float skin;
varying vec3 lp;
void vertex() { skin = COLOR.r; lp = VERTEX; }
void fragment() {
    float n = texture(noise_b, UV * 4.0 + tone).r;
    vec3 red = mix(vec3(0.08, 0.005, 0.015), vec3(0.24, 0.018, 0.03), clamp(tone * (0.65 + 0.45 * n), 0.0, 1.0));
    float vein = smoothstep(0.93, 1.0, abs(sin(atan(lp.x, lp.z) * 5.0))) * 0.25;
    vec3 col = mix(red, red * 1.7 + vec3(0.05, 0.0, 0.0), vein);
    vec3 dry = vec3(0.16, 0.1, 0.05) * (0.8 + 0.4 * n);
    ALBEDO = mix(dry, col, skin);
    ROUGHNESS = mix(0.85, 0.16 + 0.1 * n, skin);
    SPECULAR = mix(0.2, 0.55, skin);
    CLEARCOAT = 0.4 * skin; CLEARCOAT_ROUGHNESS = 0.12;
    BACKLIGHT = vec3(0.22, 0.02, 0.02) * skin;
}
";

    public static readonly string Leaf = @"
shader_type spatial;
render_mode cull_disabled;
" + Fow + @"
uniform sampler2D leaf_tex : source_color, filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D leaf_nor : hint_normal, filter_linear_mipmap, repeat_disable;
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
varying vec3 wpos;
varying vec4 vcol;
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; vcol = COLOR; }
float h11(float x) { return fract(sin(x * 91.3458 + 17.17) * 47453.5453); }
void fragment() {
    // the photographed leaf (atlas), dried by age, with PR #11's per-leaf variation and matte rules: the seed in the
    // vertex colour shifts each leaf's age and drying colours, so no two neighbours match
    float h1 = h11(vcol.g * 313.0), h2 = h11(vcol.g * 571.0 + 3.0), h3 = h11(vcol.g * 977.0 + 7.0);
    float age = clamp(vcol.r + (h1 - 0.5) * 0.3, 0.0, 1.0);
    // where each species sits in the atlas (x, y, w, h); the stem end of the mesh (v = 0) is the photo's bottom
    vec4 r = vcol.b < 0.25 ? vec4(0.0, 0.1943, 0.3333, 0.6104) : vcol.b < 0.75 ? vec4(0.3684, 0.0, 0.2622, 0.999) : vec4(0.6667, 0.1807, 0.3333, 0.6387);
    vec2 t = r.xy + vec2(UV.x, 1.0 - UV.y) * r.zw;
    vec4 photo = texture(leaf_tex, t);
    if (photo.a < 0.5) discard;
    float lum = dot(photo.rgb, vec3(0.3, 0.59, 0.11));
    vec3 yellow = mix(vec3(1.6, 1.1, 0.3), vec3(1.85, 1.3, 0.42), h3) * lum * 0.6;
    vec3 ochre = mix(vec3(1.35, 0.8, 0.22), vec3(1.5, 0.92, 0.3), h2) * lum * 0.55;
    vec3 brown = mix(vec3(0.95, 0.5, 0.2), vec3(1.2, 0.68, 0.28), h1) * lum * 0.5;
    vec3 col = age < 0.35 ? mix(photo.rgb, yellow, age / 0.35) : age < 0.7 ? mix(yellow, ochre, (age - 0.35) / 0.35) : mix(ochre, brown, (age - 0.7) / 0.3);
    // decay: dark blotches that spread with age, and dry edges
    float n = texture(noise_b, UV * vec2(3.0, 8.0) + vcol.g * 3.0).r;
    float blotch = smoothstep(0.58, 0.72, texture(noise_b, UV * vec2(2.0, 5.0) + vcol.g * 7.0).r);
    float fine = smoothstep(0.66, 0.78, texture(noise_b, UV * vec2(9.0, 22.0) + vcol.g * 11.0).r);
    float decay = clamp(blotch * (0.3 + age * 0.7) * (0.6 + 0.8 * h3) + fine * age * 0.5, 0.0, 1.0);
    col = mix(col, vec3(0.13, 0.07, 0.03), decay * 0.7);
    col = mix(col, brown * 0.62, smoothstep(0.32, 0.5, abs(UV.x - 0.5)) * age * 0.5);
    ALBEDO = clamp(col, 0.03, 0.72);
    NORMAL_MAP = texture(leaf_nor, t).rgb;
    // matte (the lighting audit: a dry leaf never below 0.8; a fresh one may be a little smoother), grain breaks the highlight
    ROUGHNESS = clamp(mix(0.7, 0.9, age) + (n - 0.5) * 0.16 + decay * 0.05, 0.6, 1.0);
    SPECULAR = 0.2;
    METALLIC = 0.0;
    BACKLIGHT = col * (0.55 - 0.35 * age);
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
" + Under + Fow + @"
uniform int kind = 0; // 0 crumb, 1 berry, 2 sugar, 3 seed
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
varying vec3 wpos;
varying vec3 lpos;
varying vec3 lnrm;
varying float vcol_r;
varying float v_h;
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; lpos = VERTEX; lnrm = NORMAL; vcol_r = COLOR.r; v_h = fract(sin(dot(MODEL_MATRIX[3].xz, vec2(12.9898, 78.233))) * 43758.5453); }
void fragment() {
    float n = texture(noise_b, lpos.xz * 0.9 + lpos.y * 0.3).r;
    float m = texture(noise_b, lpos.xy * 2.3).r;
    vec3 col; float rough = 0.8; float spec = 0.4; float cc = 0.0;
    if (kind == 0) {
        vec3 crust = mix(vec3(0.40, 0.19, 0.06), vec3(0.62, 0.36, 0.13), n);
        vec3 crumb = mix(vec3(0.70, 0.58, 0.38), vec3(0.80, 0.68, 0.48), m);
        float pores = smoothstep(0.6, 0.7, m);
        col = mix(crumb * (1.0 - pores * 0.45), crust, smoothstep(0.1, 0.6, lnrm.y + n * 0.4));
        col *= 0.84 + 0.32 * v_h;                           // each scrap its own tone
        col = mix(col, col * vec3(1.1, 0.95, 0.8), fract(v_h * 7.0) * 0.5);
        rough = clamp(0.88 + (n - 0.5) * 0.16 + (v_h - 0.5) * 0.1, 0.78, 0.97); spec = 0.22;
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
        rough = clamp(0.8 + (n - 0.5) * 0.12 + (v_h - 0.5) * 0.08, 0.75, 0.9); spec = 0.28; // dry seed husk: matte, no varnish
        col *= 0.88 + 0.24 * v_h;
    }
    ALBEDO = col * fow(wpos);
    ROUGHNESS = rough;
    SPECULAR = spec;
    CLEARCOAT = cc;
    CLEARCOAT_ROUGHNESS = 0.2;
    AO = under_ambient(wpos);
}
";

    public static readonly string Pebble = @"
shader_type spatial;
" + Fow + @"
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
varying vec3 wpos;
varying vec4 v_c;
varying vec3 v_n;
varying float v_h;
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; v_c = INSTANCE_CUSTOM; v_n = NORMAL; v_h = fract(sin(dot(MODEL_MATRIX[3].xz, vec2(12.9898, 78.233))) * 43758.5453); }
" + NetworkFn + @"
void fragment() {
    // over an opening the litter thins out with the roof: each pebble sinks away at its own moment, most of them before the middle clears
    if (glass_at(wpos, CAMERA_POSITION_WORLD) > 0.2 + 0.6 * v_h) discard;
    // weathered grit: warm greys and tans, never brighter than the sand they lie on
    vec3 a = mix(vec3(0.30, 0.23, 0.16), vec3(0.50, 0.41, 0.29), v_c.x);   // sand and ochre grit (the sun is strong: keep them under the sand's own albedo)
    a = mix(a, vec3(0.64, 0.57, 0.46), v_c.y * 0.5);                      // pale quartz
    a = mix(a, vec3(0.32, 0.20, 0.11), v_c.z);                            // rust-brown, never grey
    float n = texture(noise_b, wpos.xz * 2.5 + v_c.w * 9.0).r;
    a *= 0.72 + 0.42 * n;
    a *= mix(0.6, 1.0, smoothstep(-0.6, 0.6, v_n.y));
    a *= 0.88 + 0.24 * v_h;                                               // no two stones alike
    ALBEDO = a * fow(wpos);
    ROUGHNESS = clamp(mix(0.92, 0.8, v_c.y) + (n - 0.5) * 0.16 + (v_h - 0.5) * 0.08, 0.75, 0.97);
    SPECULAR = 0.25;
}
";

    public static readonly string Needle = @"
shader_type spatial;
" + Fow + @"
varying vec3 wpos;
varying float v_tint;
varying float v_h;
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; v_tint = INSTANCE_CUSTOM.x; v_h = fract(sin(dot(MODEL_MATRIX[3].xz, vec2(12.9898, 78.233))) * 43758.5453); }
" + NetworkFn + @"
void fragment() {
    if (glass_at(wpos, CAMERA_POSITION_WORLD) > 0.2 + 0.6 * v_h) discard; // straw and thatch thin out with the roof
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

    /// A hero's aura: a soft additive glow that slowly swirls. On the ground it is a disc with a faint rim; with
    /// billboard = 1 it is a halo facing the camera. Per instance: rgb = colour, a = strength.
    public const string Aura = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform float billboard = 0.0;
varying vec4 v_c;
void vertex() {
    v_c = INSTANCE_CUSTOM;
    if (billboard > 0.5) {
        float s = length(MODEL_MATRIX[0].xyz);
        MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * s, INV_VIEW_MATRIX[1] * s, INV_VIEW_MATRIX[2] * s, MODEL_MATRIX[3]);
    }
}
void fragment() {
    vec2 p = UV - 0.5;
    float r = length(p) * 2.0;
    float a = atan(p.y, p.x);
    // two slow counter-turning lobes so the light seems to drift and blend rather than sit still
    float swirl = 0.72 + 0.28 * sin(a * 3.0 + TIME * 1.1 - r * 5.0) * sin(a * 2.0 - TIME * 0.7 + r * 3.0);
    float glow = pow(max(0.0, 1.0 - r), 2.4) * swirl;
    float rim = (1.0 - billboard) * smoothstep(0.6, 0.82, r) * (1.0 - smoothstep(0.82, 1.0, r)) * 0.35 * swirl;
    ALBEDO = v_c.rgb;
    ALPHA = clamp((glow + rim) * v_c.a, 0.0, 1.0);
}
";

    public const string Bar = @"
shader_type spatial;
render_mode unshaded, depth_test_disabled, cull_disabled, shadows_disabled;
uniform float evo = 0.0; // 1: the evolution bar under the health bar
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
    if (evo > 0.5) hpc = mix(vec3(0.62, 0.3, 0.07), vec3(1.0, 0.68, 0.22), v_hp); // deep amber filling to gold
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
    // kinds: 0 dust, 1 hemolymph, 2 gold sparks, 3 silk, 4 acid, 5 venom, 6 shell gleam, 7 scarlet sparks,
    // 8 amber ant hemolymph, 9 cloudy spider hemolymph, 10 acid vapour
    float k = v_c.z;
    vec3 col = k < 0.5 ? vec3(0.62, 0.52, 0.40) : k < 1.5 ? vec3(0.78, 0.82, 0.30) : k < 2.5 ? vec3(1.0, 0.85, 0.5) : k < 3.5 ? vec3(0.95, 0.95, 0.92)
             : k < 4.5 ? vec3(0.55, 0.98, 0.18) : k < 5.5 ? vec3(0.72, 0.38, 0.95) : k < 6.5 ? vec3(0.45, 0.95, 0.8) : k < 7.5 ? vec3(1.0, 0.42, 0.22)
             : k < 8.5 ? vec3(0.86, 0.62, 0.16) : k < 9.5 ? vec3(0.82, 0.8, 0.6) : vec3(0.86, 0.92, 0.72);
    ALBEDO = col;
    ALPHA = a * v_c.x;
}
";

    /// A venom pool left by an Acid Burst: a seething green slick that eats at the ground, fading with its life.
    public const string Pool = @"
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, shadows_disabled;
varying float v_a;
varying float v_seed;
float hash2(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }
void vertex() { v_a = INSTANCE_CUSTOM.x; v_seed = INSTANCE_CUSTOM.y; }
void fragment() {
    vec2 p = UV - 0.5;
    float ang = atan(p.y, p.x);
    float r = length(p) * 2.0 + 0.12 * sin(ang * 5.0 + v_seed * 7.0) + 0.06 * sin(ang * 11.0 - v_seed * 3.0);
    float body = 1.0 - smoothstep(0.78, 1.0, r);
    // bubbles rise and burst across the slick
    vec2 cell = floor((p + v_seed) * 16.0);
    float h = hash2(cell);
    vec2 cp = fract((p + v_seed) * 16.0) - 0.5;
    float bt = fract(TIME * (0.6 + h) + h * 3.0);
    float bub = (1.0 - smoothstep(0.0, 0.22 + 0.2 * bt, length(cp))) * (1.0 - bt) * step(0.35, h);
    float rim = smoothstep(0.55, 0.85, r) * body;
    vec3 col = mix(vec3(0.28, 0.62, 0.08), vec3(0.62, 0.98, 0.22), rim + bub);
    ALBEDO = col;
    EMISSION = col * (0.25 + 0.5 * bub + 0.2 * rim);
    ROUGHNESS = 0.15;
    SPECULAR = 0.7;
    ALPHA = body * v_a * (0.62 + 0.3 * bub);
}
";

    /// Hemolymph soaking into the soil: glossy and bright the moment it lands, then drunk by the earth into a dark
    /// amber blot that fades over a minute. x alpha, y seed, z kind (0 ant amber, 1 bug yellow-green, 2 spider, 3 acid burn), w wetness.
    public const string Stain = @"
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, shadows_disabled;
varying vec4 v_c;
float hash2(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }
void vertex() {
    v_c = INSTANCE_CUSTOM;
    // a hair toward the eye along the view ray, so it never flickers into the soil (bodies lying in it stay in front)
    vec4 vp = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
    vp.xyz -= normalize(vp.xyz) * 0.04;
    POSITION = PROJECTION_MATRIX * vp;
}
void fragment() {
    vec2 p = UV - 0.5;
    float seed = v_c.y;
    float ang = atan(p.y, p.x);
    float r = length(p) * 2.0 + 0.16 * sin(ang * 3.0 + seed * 11.0) + 0.08 * sin(ang * 7.0 - seed * 5.0);
    float body = 1.0 - smoothstep(0.55, 0.75, r);
    // satellite droplets thrown out round the blot
    vec2 q = p * 7.0 + seed * 13.0;
    float sat = (1.0 - smoothstep(0.12, 0.3, length(fract(q) - 0.5))) * step(0.72, hash2(floor(q))) * (1.0 - smoothstep(0.6, 0.95, length(p) * 2.0));
    float a = max(body, sat);
    // the rim dries darker: a coffee-ring of pigment where the film thinned
    float rim = smoothstep(0.35, 0.65, r) * body;
    float k = v_c.z;
    // soil soaked dark and saturated: the hue of the fluid shows in the darkened earth, not as paint on top of it
    vec3 col = k < 0.5 ? vec3(0.15, 0.085, 0.025) : k < 1.5 ? vec3(0.11, 0.11, 0.03) : k < 2.5 ? vec3(0.19, 0.16, 0.1) : vec3(0.07, 0.09, 0.015);
    vec3 fresh = k < 0.5 ? vec3(0.3, 0.17, 0.03) : k < 1.5 ? vec3(0.25, 0.27, 0.05) : k < 2.5 ? vec3(0.38, 0.35, 0.22) : vec3(0.18, 0.3, 0.03);
    float wet = v_c.w;
    ALBEDO = mix(col * (1.0 - 0.35 * rim), fresh, wet * (1.0 - 0.5 * rim));
    ROUGHNESS = mix(0.85, 0.3, wet); // a soft sheen, not a mirror: a glint off a film this thin would flare white
    SPECULAR = mix(0.2, 0.42, wet);
    ALPHA = a * v_c.x * mix(0.66, 0.55, wet) * (0.75 + 0.25 * rim); // a thin wet film at first, then soaked-in earth
}
";

    /// Build mode: the footprint of the room being placed (green where it may go, red where it may not) and a
    /// smaller ring on the node its gallery would be dug from.
    public const string Cue = @"
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;
uniform vec4 color : source_color = vec4(0.5, 1.0, 0.4, 1.0);
void fragment() {
    float r = length(UV - 0.5) * 2.0;
    float ring = smoothstep(0.84, 0.9, r) * (1.0 - smoothstep(0.95, 1.0, r));
    float fill = (1.0 - smoothstep(0.0, 0.9, r)) * 0.14;
    ALBEDO = color.rgb;
    ALPHA = (ring + fill) * color.a;
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
    /// Every dug surface of a nest: the pit walls, the shaft, the galleries, the room walls and floors. The soil
    /// is read by world height below the entrance (`ground_y`): a dark organic topsoil with roots and leaf
    /// litter, an ochre loam set with pebbles, and a pale loose sandy subsoil underneath, the boundaries wavy.
    /// The walls carry the scallops of the diggers' jaws (`grooves`), the floors are trodden smooth. Cavity
    /// surfaces take no sun and no sky ambient (see NestLights): they are lit by the warm lamps and a faint fill
    /// (`ambient_scale`); the sand around the mouth throws a little warm light into the hole (`bounce`). CULL is replaced per use (back faces for the cavities, which
    /// are seen from inside, both for the bowl and the mouth).
    public static readonly string Earth = @"
shader_type spatial;
render_mode cull_CULL;
uniform sampler2D noise_b : filter_linear_mipmap, repeat_enable;
uniform sampler2D normal_c : hint_normal, filter_linear_mipmap, repeat_enable;
uniform float ground_y = 0.0;   // world height of the nest entrance
uniform sampler2D soil_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float shade = 1.0;      // how much of the sun this surface can see
uniform float bounce = 0.5;     // warm light thrown in from the sunlit sand above
uniform bool cavity_surface = false;
uniform bool flat_lighting = false; // Low tier: stable directional light without screen-space buffers
uniform float ambient_scale = 0.1;  // cavity walls: share of the sky's fill that reaches underground (the sky ambient is switched off there)
uniform vec4 lamps[8];              // Low tier: nearest lamps (xyz world, w range) painted as pools, as there are no real shaded lights

uniform float grooves = 1.0;    // claw marks of the digging (0 on trodden floors)
uniform int detail = 1;         // graphics tier: 0 plain layers, 1 pebbles, roots and grooves, 2 finer grain
varying vec3 wpos;
varying vec3 wnrm;
void vertex() {
    wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    wnrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
    // Excavation UV.x stores a distance and UV.y is constant; it is not a valid
    // tangent parameterization. Supply a stable basis for the triplanar normal map.
    vec3 tangent_axis = abs(NORMAL.x) > 0.95 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
    TANGENT = normalize(tangent_axis - NORMAL * dot(NORMAL, tangent_axis));
    BINORMAL = normalize(cross(NORMAL, TANGENT));
}
float tri(sampler2D t, vec3 p, vec3 an, float s, float o) {
    return texture(t, p.yz * s + o).r * an.x + texture(t, p.xz * s + o).r * an.y + texture(t, p.xy * s + o).r * an.z;
}
void fragment() {
    vec3 an = abs(wnrm);
    an /= an.x + an.y + an.z;
    float depth = ground_y - wpos.y;
    float g = tri(noise_b, wpos, an, 0.35, 0.0);     // broad mottling
    float g2 = tri(noise_b, wpos, an, 1.6, 0.4);     // grain
    float g3 = detail > 0 ? tri(noise_b, wpos, an, 0.9, 0.7) : 0.5;  // pebbles
    // the three layers, their boundaries wandering with the mottling
    vec3 top = vec3(0.17, 0.105, 0.06);
    vec3 loam = vec3(0.38, 0.25, 0.14);
    vec3 sand = vec3(0.38, 0.31, 0.22);
    float t1 = smoothstep(0.6, 1.5, depth + (g - 0.5) * 1.0);
    float t2 = smoothstep(3.1, 4.3, depth + (g - 0.5) * 1.4);
    vec3 col = mix(mix(top, loam, t1), sand, t2);
    if (detail > 0) {
        // roots and leaf fibres in the topsoil, thinning out through the loam
        float fib = smoothstep(0.76, 0.82, tri(noise_b, wpos * vec3(1.0, 0.25, 1.0), an, 2.4, 0.13));
        col = mix(col, vec3(0.80, 0.68, 0.48), fib * (1.0 - t2) * (1.0 - 0.6 * t1) * 0.8);
        // pebbles set in the loam, fine gravel in the sand
        float peb = smoothstep(0.78, 0.86, g3);
        vec3 stone = mix(vec3(0.66, 0.62, 0.55), vec3(0.52, 0.46, 0.40), g2);
        col = mix(col, stone, peb * (t1 * (1.0 - t2) * 0.95 + t2 * 0.45));
        float grav = smoothstep(0.7, 0.8, g2) * t2;
        col = mix(col, vec3(0.70, 0.64, 0.54), grav * 0.5);
    }
    // the scallops left by the diggers' jaws: short arcs across the wall, catching the light along their rims
    float scal = 0.5 + 0.5 * sin(dot(wpos, vec3(5.0, 19.0, 4.0)) + g * 6.0 + g2 * 2.0);
    float claw = grooves * (1.0 - t2 * 0.5) * float(detail > 0);
    col *= 1.0 - claw * 0.14 * scal;
    col *= 0.72 + 0.44 * g2;
    if (detail > 1) col *= 0.92 + 0.16 * tri(noise_b, wpos, an, 4.5, 0.9);
    // a trodden floor darkens toward the foot of the wall, so the cavity reads as a depression
    if (grooves < 0.01) { float rr = length(UV - 0.5) * 2.0; col *= 1.0 - 0.42 * smoothstep(0.5, 1.0, rr); }
    if (cavity_surface) {
        // Triplanar photographic soil keeps grain continuous across the floor and banks.
        vec3 weights = pow(an, vec3(4.0)); weights /= max(dot(weights, vec3(1.0)), 0.001);
        vec3 tx = texture(soil_albedo, wpos.yz * 0.17).rgb;
        vec3 ty = texture(soil_albedo, wpos.xz * 0.17).rgb;
        vec3 tz = texture(soil_albedo, wpos.xy * 0.17).rgb;
        col = tx * weights.x + ty * weights.y + tz * weights.z;
        col = mix(vec3(dot(col, vec3(0.2126,0.7152,0.0722))), col, 0.8) * vec3(0.92,0.84,0.76);
        col *= mix(0.88, 1.08, smoothstep(-1.4, 0.3, UV.x));
        col *= 0.87 + 0.20 * g;
        // the walls show the soil in its layers (dark topsoil, ochre loam, pale sand), the floor stays trodden sand
        vec3 layer = mix(mix(vec3(0.46, 0.30, 0.17), vec3(1.0, 0.70, 0.40), t1), vec3(1.05, 0.90, 0.66), t2);
        float wall = 1.0 - an.y;
        col *= mix(vec3(1.0), layer, 0.8 * wall);
        float crevice = exp(-pow((UV.x + 0.48) / 0.4, 2.0));
        col *= 1.0 - 0.32 * crevice;
        if (flat_lighting) col *= 0.45 + 0.55 * max(dot(normalize(wnrm), normalize(vec3(-0.6,0.8,0.4))), 0.0);
        AO = 1.0 - 0.45 * crevice;
        AO_LIGHT_AFFECT = 0.5;
    }
    ALBEDO = col * shade;
    ROUGHNESS = 0.96 - t2 * 0.08;
    SPECULAR = 0.2;
    vec2 nm = (texture(normal_c, (an.y > 0.5 ? wpos.xz : an.x > an.z ? wpos.yz : wpos.xy) * 0.7).xy - 0.5) * (detail > 0 ? 1.0 : 0.6);
    NORMAL_MAP = normalize(vec3(nm, 0.6)) * 0.5 + 0.5;
    NORMAL_MAP_DEPTH = 1.0 + claw * 0.6 * scal;
    if (cavity_surface && !flat_lighting) {
        vec2 suv = (an.y > 0.5 ? wpos.xz : an.x > an.z ? wpos.yz : wpos.xy) * 0.17;
        vec2 px = 1.0 / vec2(textureSize(soil_albedo, 0));
        float l = dot(texture(soil_albedo, suv - vec2(px.x, 0.0)).rgb, vec3(0.3333));
        float r = dot(texture(soil_albedo, suv + vec2(px.x, 0.0)).rgb, vec3(0.3333));
        float d = dot(texture(soil_albedo, suv - vec2(0.0, px.y)).rgb, vec3(0.3333));
        float u = dot(texture(soil_albedo, suv + vec2(0.0, px.y)).rgb, vec3(0.3333));
        NORMAL_MAP = normalize(vec3((l-r) * 3.2, (d-u) * 3.2, 1.0)) * 0.5 + 0.5;
        NORMAL_MAP_DEPTH = 0.8;
    }
    EMISSION = col * vec3(0.72, 0.50, 0.28) * bounce * 0.55; // warm bounce fills the shadows that the blue sky would otherwise tint
    if (cavity_surface) {
        // the sky ambient is off down here: a faint warm fill keeps the blacks readable
        EMISSION += col * vec3(1.0, 0.78, 0.55) * ambient_scale * 0.6;
        if (flat_lighting) {
            float pool = 0.0;
            for (int i = 0; i < 8; i++) {
                float r = max(lamps[i].w, 0.001);
                float f = clamp(1.0 - length(wpos - lamps[i].xyz) / r, 0.0, 1.0);
                pool += f * f * step(0.01, lamps[i].w);
            }
            EMISSION += col * vec3(1.0, 0.72, 0.45) * pool * 1.5;
        }
    }
}
";

    /// A shaft of sunlight falling into the opened earth: a cone of warm haze, strongest along its edges where
    /// the eye looks through the most of it, fading toward the floor. Drawn only while the glass is up.
    public const string Shaft = @"
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;
uniform float strength = 0.0;
uniform vec3 color : source_color = vec3(1.0, 0.74, 0.4);
varying float v_along;
void vertex() { v_along = UV.y; }
void fragment() {
    float edge = pow(1.0 - abs(dot(normalize(NORMAL), normalize(VIEW))), 1.6);
    float fall = (1.0 - v_along) * (1.0 - v_along);
    ALBEDO = color * strength * fall * (0.006 + 0.045 * edge);
    ALPHA = strength;
}
";
}
