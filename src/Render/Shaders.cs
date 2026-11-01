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
uniform int pattern = 0;      // 0 ant, 1 ground beetle (grooved metallic shell), 2 wolf spider (striped, hairy), 3 wasp, 4 stag beetle
uniform float irid = 0.0;     // structural-colour sheen strength
uniform float hero = 0.0;     // metal-inlaid armour of hero ants
uniform vec3 accent : source_color = vec3(0.95, 0.68, 0.2); // crests, plate rims, inlay: the colony's metal, or a bug's own
uniform vec3 acid = vec3(0.0); // glowing acid sac (Acid Matron)
uniform vec3 eye_color : source_color = vec3(0.6, 0.32, 0.08); // the iris under the catchlight
uniform vec3 neck = vec3(0.0, 0.14, 0.13);  // the head turns about this model-space point
uniform float detail = 0.0;   // 1 in portraits and close-ups: compound-eye facets
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

float hash3(vec3 p) { return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453); }
float hash1(float x) { return fract(sin(x * 127.1 + 311.7) * 43758.5453); }

vec3 rot_y(vec3 p, float a) { float c = cos(a), s = sin(a); return vec3(c * p.x + s * p.z, p.y, -s * p.x + c * p.z); }
vec3 rot_x(vec3 p, float a) { float c = cos(a), s = sin(a); return vec3(p.x, c * p.y - s * p.z, s * p.y + c * p.z); }
vec3 rot_z(vec3 p, float a) { float c = cos(a), s = sin(a); return vec3(c * p.x - s * p.y, s * p.x + c * p.y, p.z); }

void vertex() {
    float part = floor(COLOR.r * 255.0 + 0.5);
    float w = COLOR.g;
    float region = COLOR.b;
    // Per-instance data, packed by AntRenderer:
    //   x = seed (0..15) + 16 * pose (0 normal, 1 sentinel, 2 listening) + gait fraction (distance-locked in the sim)
    //   y = yaw8 * 256 + pitch8 (where the head looks) + speed amplitude fraction
    //   z = flags (2 shell wall, 4 charge, 8 carrying, 16 selected, 32 hovered, 64 * hit flash 0..15) + bite fraction
    //   w = one-shot (1 order acknowledged, 2 flinch, 3 greeting) + its progress fraction
    float xi = floor(INSTANCE_CUSTOM.x);
    float phase = fract(INSTANCE_CUSTOM.x) * TAU;
    float seed = mod(xi, 16.0);
    float pose = floor(xi / 16.0);
    float yi = floor(INSTANCE_CUSTOM.y);
    float amp = fract(INSTANCE_CUSTOM.y);
    float yaw = -((floor(yi / 256.0) / 255.0 - 0.5) * 2.4);  // + in the renderer = toward mesh -X
    float pitch = (mod(yi, 256.0) / 255.0 - 0.5) * 1.2;      // + = looking up
    float cz = INSTANCE_CUSTOM.z;
    float zi = floor(cz);
    float bite = fract(cz);
    float hitq = floor(zi / 64.0); zi -= hitq * 64.0;
    float hov = step(32.0, zi); zi -= hov * 32.0;
    float sel = step(16.0, zi); zi -= sel * 16.0;
    float carry = step(8.0, zi); zi -= carry * 8.0;
    float charge = step(4.0, zi); zi -= charge * 4.0;
    float block = step(2.0, zi);
    float kind = floor(INSTANCE_CUSTOM.w);
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
    float roll = (sin(phase) * 0.035 * moving + idle * sin(TIME * 0.9 + id) * 0.02) * alive;
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
        float sweep = (bite * 0.5 + flinch * 0.8) * side;
        vec3 q = p - vec3(piv.x, 0.0, piv.y);
        q = rot_y(q, (a * w + sweep) * alive);
        p = q + vec3(piv.x, 0.0, piv.y);
        // the flick: up, then pointing at the order; a greeting raises and quivers them; listening holds them high
        p.y += w * alive * (ack * 0.06 + greet * (0.05 + 0.012 * sin(TIME * 40.0)) + (pose == 2.0 ? 0.035 : 0.0));
    } else if (part == 9.0 || part == 10.0) {
        float side = part == 9.0 ? 1.0 : -1.0;
        // jaws gape on the bite; held open on sentry; a slow gnash at rest; wide on a charge
        float open = bite * 0.55 + sin(TIME * 3.0 + id) * (0.04 + 0.06 * hero) + charge * 0.45
                   + (pose == 1.0 ? 0.3 : 0.0) + idle * 0.12 * max(0.0, sin(TIME * 2.6 + id * 2.0));
        vec3 q = p - vec3(piv.x, 0.0, piv.y);
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
                vec3 q = p - vec3(0.0, 0.14, -0.105);
                float g = sin(2.0 * phase - 0.6) * 0.05 * amp;
                q = rot_x(q, g); n = rot_x(n, g);
                p = q + vec3(0.0, 0.14, -0.105);
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
        float pt = (-pitch + sin(TIME * 1.4 + id) * 0.04 * hero - flinch * 0.3 + charge * 0.2 + bite * 0.1) * alive;
        q = rot_x(q, pt); n = rot_x(n, pt);
        q = rot_y(q, yaw * alive); n = rot_y(n, yaw * alive);
        p = q + neck;
    }
    if (!leg) { p = rot_z(p, roll); n = rot_z(n, roll); }
    // every ant its own size (feet stay on the ground: the scale is about the origin at foot level)
    p *= 0.93 + 0.14 * hash1(seed + 0.37);
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
            // tergite bands: darker toward each seam, and the seam itself tilts the normal so it catches the sun
            float f = fract(v_local.z * 18.0 + 0.3);
            float band = smoothstep(0.42, 0.5, f);
            c *= 1.0 - band * 0.35;
            if (pattern == 0) {
                float seam = smoothstep(0.36, 0.46, f) - smoothstep(0.46, 0.56, f);
                vec3 fwd = normalize((VIEW_MATRIX * MODEL_MATRIX * vec4(0.0, 0.0, 1.0, 0.0)).xyz);
                NORMAL = normalize(NORMAL - fwd * seam * 0.35);
                // honey through the abdomen when the sun is behind it
                BACKLIGHT = gaster_color * 1.5 + vec3(0.26, 0.13, 0.03);
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
    if (part == 11.0) {
        // the eye: a two-tone iris with a camera-facing pseudopupil (compound-eye optics) and a catchlight that is
        // painted, not lit, so it stays in shadow; it brightens when the ant is selected or under the cursor
        vec3 nv = NORMAL;
        float pupil = smoothstep(0.76, 0.95, clamp(nv.z, 0.0, 1.0));
        float catch = pow(clamp(dot(nv, normalize(vec3(-0.45, 0.6, 1.0))), 0.0, 1.0), 48.0);
        float pulse = 0.85 + 0.15 * sin(TIME * 0.7 + v_seed);
        vec3 iris = eye_color * (1.0 + 0.7 * fres);
        float facet = detail * step(0.5, fract(v_local.x * 190.0 + v_local.z * 60.0)) * step(0.5, fract(v_local.y * 110.0 + v_local.x * 95.0));
        iris = mix(iris, iris * 0.7, facet);
        c = mix(iris, vec3(0.012), pupil);
        rough = 0.12; cc = 0.0;
        eyeEm = iris * 0.12 * pulse + vec3(1.0, 0.97, 0.9) * catch * (0.9 + 0.7 * v_sel + 0.9 * v_hov) + iris * 0.5 * v_sel * pulse;
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
        // no two ants quite alike
        c *= 0.94 + 0.12 * hash1(v_seed + 1.3);
        rough += (hash1(v_seed + 2.9) - 0.5) * 0.12;
    }
    if ((pattern == 1 || pattern == 4) && part != 11.0 && part != 14.0 && part != 15.0) { rough = pattern == 4 ? 0.36 : 0.42; cc = 0.4; metal = 0.15; }
    if (pattern == 3 && part != 11.0 && part != 14.0) { rough = 0.48; cc = 0.25; }
    if (hero > 0.0 && part != 11.0 && part != 14.0 && part != 15.0) {
        // metal filigree in the colony's colour: bands on the abdomen, a crest line on the head, edged mandibles
        float gband = part == 13.0 ? step(0.8, fract(v_local.z * 11.0 + 0.1)) : 0.0;
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
    ALBEDO = c * (1.0 - dead * 0.35) * mix(0.72, 1.0, v_ao);
    AO = v_ao;
    AO_LIGHT_AFFECT = 0.35;
    ROUGHNESS = clamp(rough + dead * 0.3, 0.05, 1.0);
    METALLIC = metal;
    SPECULAR = 0.32;
    CLEARCOAT = cc;
    CLEARCOAT_ROUGHNESS = 0.42;
    RIM = pattern == 2 ? 0.6 : 0.35;
    RIM_TINT = 0.6;
    // brief warm flicker when bitten; kept subtle so big targets under constant attack keep their colour
    EMISSION = vec3(1.0, 0.3, 0.1) * v_flash * v_flash * (pattern == 0 ? 0.3 : 0.08) + eyeEm;
    // a beetle behind its shell wall: the wing cases gleam hard along their edges
    if (v_block > 0.5 && part != 11.0) { EMISSION += accent * (0.08 + 0.35 * fres) * (0.7 + 0.3 * sin(TIME * 6.0)); ROUGHNESS = 0.25; }
    if (part == 13.0 && length(acid) > 0.0) {
        // translucent acid sac: smooth glossy green with a slow inner glow, gold bands still on top
        float pulse = 0.75 + 0.25 * sin(TIME * 2.2);
        float bands = hero * step(0.8, fract(v_local.z * 11.0 + 0.1));
        ALBEDO = mix(acid * 0.6, accent, bands);
        EMISSION += acid * 0.22 * pulse * (1.0 - bands);
        CLEARCOAT = 0.5;
        CLEARCOAT_ROUGHNESS = 0.35;
        ROUGHNESS = 0.42;
        METALLIC = bands * 0.8;
        BACKLIGHT = vec3(0.0);
    }
}
";

    /// The tunnel network, shared by the terrain cut, the glass pane and the ground litter: up to 32 galleries
    /// packed seven floats each (from.xy, to.xy, room radius at 'to', depth at 'from', depth at 'to'). A gallery
    /// is a capsule 1.5 wide sloping between its two depths; a room is a disc of its radius at its depth.
    /// net_dist follows the line of sight from the camera through the ground point down to each gallery's depth,
    /// so the window in the soil sits exactly over what is seen through it from any camera angle, instead of
    /// over the rooms' map positions (which drift out from under a ground-level cut as the camera tilts).
    public const string NetworkFn = @"
uniform float xray_on = 0.0;
uniform vec2 xray_center = vec2(-1000.0);
uniform float net[224];
uniform int net_count = 0;
float net_dist(vec3 p, vec3 cam) {
    vec3 dir = p - cam;
    // how far the line of sight drifts across the ground per unit of depth (clamped for a near-horizontal view)
    vec2 sh = dir.y < -0.001 ? dir.xz / (-dir.y) : vec2(0.0);
    float shl = length(sh);
    if (shl > 6.0) sh *= 6.0 / shl;
    // nothing of the network can be seen through ground this far from the mound: skip the loop
    if (distance(p.xz, xray_center) > 48.0 + 11.0 * min(shl, 6.0)) return 1e9;
    float best = 1e9;
    for (int i = 0; i < net_count; i++) {
        int k = i * 7;
        vec2 a = vec2(net[k], net[k + 1]);
        vec2 b = vec2(net[k + 2], net[k + 3]);
        float r = net[k + 4];
        float da = net[k + 5], db = net[k + 6];
        vec2 ab = b - a;
        float len2 = max(dot(ab, ab), 0.0001);
        for (int s = 0; s < 3; s++) {
            float f = float(s) * 0.5;
            vec2 q = p.xz + sh * mix(da, db, f);
            float t = clamp(dot(q - a, ab) / len2, 0.0, 1.0);
            best = min(best, length(q - (a + ab * t)) - 1.5);
            if (s == 2) best = min(best, length(q - b) - r);
        }
    }
    return best;
}
// how open the ground is at p (0 soil .. 1 glass); surface dressing over an opening is dropped with it
float glass_at(vec3 p, vec3 cam) { return xray_on > 0.001 ? xray_on * (1.0 - smoothstep(0.0, 1.0, net_dist(p, cam))) : 0.0; }
";

    /// The glass pane over an opened nest: a faint cool tint with a brighter rim, drawn after the opaque world.
    /// It only shows where the terrain has been cut away, so the ground around the tunnels stays ordinary soil.
    public static readonly string Glass = @"
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded;
uniform float strength = 1.0;
varying vec3 wpos;
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
" + NetworkFn + @"
void fragment() {
    if (strength < 0.001) discard;
    float nd = net_dist(wpos, CAMERA_POSITION_WORLD);
    float inside = 1.0 - smoothstep(0.4, 0.8, nd);        // the terrain shader discards soil up to about here
    float edge = smoothstep(0.22, 0.42, nd) * inside;     // the crumbling lip of the cut, right at the edge
    // Not glass. Blending happens in linear light, so over the dark pit even a few percent reads as a grey slab after
    // tonemapping: the opening itself is left completely clear and only the lip carries a thin skin of sunlit sand.
    ALBEDO = vec3(0.78, 0.62, 0.40);
    ALPHA = strength * 0.06 * edge;
}
";

    public static readonly string Terrain = @"
shader_type spatial;
" + Fow + @"
uniform sampler2D noise_a : filter_linear_mipmap, repeat_enable;
uniform sampler2D noise_b : filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D normal_a : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D normal_b : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D normal_c : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable; // the sand grains themselves
uniform sampler2D pher_tex : filter_linear, repeat_disable;
uniform float pher_view = 0.25;
uniform int detail = 1; // graphics tier: 0 plain soil (phones), 1 grain and clods, 2 a finer grain octave on top
// Glass Earth: while xray_on, the soil directly over the colony's galleries and rooms is cut away
varying vec3 wpos;
varying vec4 vcol;   // r baked shade, g excavated sand, b pine thatch, a 0.5 level / <0.5 crest / >0.5 hollow
varying vec3 wnrm;
void vertex() {
    wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    wnrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
    vcol = COLOR;
}
" + NetworkFn + @"
void fragment() {
    vec2 uv = wpos.xz;
    float glass = glass_at(wpos, CAMERA_POSITION_WORLD);
    // the soil over the tunnels is simply gone while the glass is up (a translucent pane is drawn in its place)
    if (glass > 0.55) discard;
    // relief: how far this point sits below its surroundings (+) or above them (-), and how steep it is
    float hollow = clamp((vcol.a - 0.5) * 2.0, -1.0, 1.0);
    float inHollow = smoothstep(0.05, 0.7, hollow);
    float onCrest = smoothstep(0.05, 0.7, -hollow);
    float steep = smoothstep(0.08, 0.35, 1.0 - wnrm.y);
    float n1 = texture(noise_a, uv * 0.0035).r;
    float n2 = texture(noise_a, uv * 0.017 + 0.3).r;
    float g = texture(noise_b, uv * 0.09).r;
    float g2 = texture(noise_b, uv * 0.41 + 0.7).r;
    float grain = texture(noise_b, uv * 1.7).r;
    float grain2 = detail > 0 ? texture(noise_b, uv * 4.3 + 0.23).r : 0.5;
    float grain3 = detail > 0 ? texture(noise_b, uv * 9.1 + 0.61).r : 0.5;
    float grit = texture(noise_b, uv * 0.83 + 0.37).r;
    vec3 dry = vec3(0.42, 0.315, 0.22);
    vec3 damp = vec3(0.19, 0.135, 0.09);
    vec3 clay = vec3(0.38, 0.23, 0.13);
    vec3 pale = vec3(0.58, 0.49, 0.37);   // sun-dried crests
    vec3 col = mix(dry, damp, smoothstep(0.45, 0.85, n1) * 0.8);
    col = mix(col, clay, smoothstep(0.5, 0.8, n2) * 0.5);
    // hollows hold the damp and the fines; crests dry pale; steep sides show the lighter subsoil
    col = mix(col, damp * 0.9, inHollow * 0.55);
    col = mix(col, pale, onCrest * 0.45 + steep * 0.25);
    col *= 0.72 + 0.45 * g;
    col *= 0.8 + 0.3 * g2;
    // individual sand grains: bright quartz flecks and dark pores (stronger in the pebble beds of the hollows)
    float gc = 1.0 + 0.5 * inHollow;
    col *= 1.0 - (0.26 - 0.32 * grain) * gc;
    col *= 0.86 + 0.28 * grain2;
    col *= 0.92 + 0.16 * grain3;
    float quartz = smoothstep(0.80, 0.88, grit) * smoothstep(0.35, 0.7, g) * (1.0 + 0.7 * onCrest);
    col = mix(col, vec3(0.64, 0.59, 0.52), clamp(quartz, 0.0, 1.0) * 0.6);
    // a bed of small stones where the fines have washed in
    float bed = step(0.74 - 0.08 * inHollow, g2) * inHollow * float(detail > 0);
    col = mix(col, vec3(0.38, 0.35, 0.31) * (0.7 + 0.5 * grain3), bed * 0.65);
    float pore = smoothstep(0.72, 0.8, texture(noise_b, uv * 0.6 + 0.11).r);
    col = mix(col, vec3(0.06, 0.045, 0.035), pore * 0.35);
    float dark = smoothstep(0.7, 0.8, texture(noise_b, uv * 0.23).r);
    col = mix(col, vec3(0.1, 0.075, 0.05), dark * 0.35);
    // excavated sand around the black colony's nest
    vec3 sand = vec3(0.50, 0.40, 0.28) * (0.7 + 0.5 * g2) * (0.8 + 0.3 * grain) * (0.82 + 0.36 * grain2) * (0.9 + 0.2 * grain3);
    sand = mix(sand, vec3(0.30, 0.22, 0.14), step(0.86, grain3) * 0.6);          // scattered dark grains
    sand = mix(sand, vec3(0.72, 0.66, 0.58), step(0.9, grain2) * smoothstep(0.5, 0.8, grain3)); // bright quartz specks
    col = mix(col, sand, vcol.g);
    // pine thatch mound of the red colony
    vec3 thatch = mix(vec3(0.20, 0.11, 0.05), vec3(0.36, 0.20, 0.09), g) * (0.7 + 0.4 * g2);
    col = mix(col, thatch, vcol.b);
    col *= vcol.r;
    float wet = smoothstep(0.55, 0.85, n1) * (1.0 - onCrest * 0.6) + inHollow * 0.3;
    // the freshly cut soil at the opening's edge: darker and damper than the surface
    col = mix(col, col * vec3(0.62, 0.50, 0.38), glass * 0.7);
    ALBEDO = col;
    ROUGHNESS = mix(0.96, 0.74, clamp(wet, 0.0, 1.0)) - quartz * 0.25;
    SPECULAR = 0.3;
    // the surface: broad undulations, clods, then the grains (two octaves at Ultra); plain soil keeps only the first two
    vec2 nm = (texture(normal_a, uv * 0.11).xy - 0.5) * 1.0 + (texture(normal_b, uv * 0.47).xy - 0.5) * 0.9;
    if (detail > 0) nm += (texture(normal_c, uv * 0.26).xy - 0.5) * (1.0 + 0.4 * inHollow);
    if (detail > 1) nm += (texture(normal_c, uv * 0.95 + 0.31).xy - 0.5) * 0.6;
    nm = mix(nm, nm * 0.3, vcol.b); // the thatch is needles, not sand
    NORMAL_MAP = normalize(vec3(nm, 0.5)) * 0.5 + 0.5;
    NORMAL_MAP_DEPTH = detail > 0 ? 1.5 : 1.1;
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
" + NetworkFn + @"
void fragment() {
    if (glass_at(wpos, CAMERA_POSITION_WORLD) > 0.55) discard; // pebbles do not hover over an opened gallery
    // weathered grit: warm greys and tans, never brighter than the sand they lie on
    vec3 a = mix(vec3(0.30, 0.23, 0.16), vec3(0.50, 0.41, 0.29), v_c.x);   // sand and ochre grit (the sun is strong: keep them under the sand's own albedo)
    a = mix(a, vec3(0.64, 0.57, 0.46), v_c.y * 0.5);                      // pale quartz
    a = mix(a, vec3(0.32, 0.20, 0.11), v_c.z);                            // rust-brown, never grey
    float n = texture(noise_b, wpos.xz * 2.5 + v_c.w * 9.0).r;
    a *= 0.72 + 0.42 * n;
    a *= mix(0.6, 1.0, smoothstep(-0.6, 0.6, v_n.y));
    ALBEDO = a * fow(wpos);
    ROUGHNESS = mix(0.9, 0.6, v_c.y);
    SPECULAR = 0.25;
}
";

    public static readonly string Needle = @"
shader_type spatial;
" + Fow + @"
varying vec3 wpos;
varying float v_tint;
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; v_tint = INSTANCE_CUSTOM.x; }
" + NetworkFn + @"
void fragment() {
    if (glass_at(wpos, CAMERA_POSITION_WORLD) > 0.55) discard; // straw and thatch do not hover over an opened gallery
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
    // kinds: 0 dust, 1 hemolymph, 2 gold sparks, 3 silk, 4 acid, 5 venom, 6 shell gleam, 7 scarlet sparks
    float k = v_c.z;
    vec3 col = k < 0.5 ? vec3(0.62, 0.52, 0.40) : k < 1.5 ? vec3(0.78, 0.82, 0.30) : k < 2.5 ? vec3(1.0, 0.85, 0.5) : k < 3.5 ? vec3(0.95, 0.95, 0.92)
             : k < 4.5 ? vec3(0.55, 0.98, 0.18) : k < 5.5 ? vec3(0.72, 0.38, 0.95) : k < 6.5 ? vec3(0.45, 0.95, 0.8) : vec3(1.0, 0.42, 0.22);
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
