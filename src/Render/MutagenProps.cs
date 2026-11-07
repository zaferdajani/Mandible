using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The mutation track's props, modelled in Blender (tools/blender/mutation_models.py) against approved targets H1-H3:
/// the dead queen wasp hanging in an orb web between two grass stems and a bramble twig, her glowing gland (in her
/// gaster, then in its carrier's jaws), and the royal nursery's bed of chewed fibre with the living gauge, a heap of
/// remains and seeds that grows as the nurses feed the larva. Rendering only: it reads the sim, never writes it.
public sealed class MutagenProps
{
    const string Root = "res://assets/mutation/";
    readonly GameSession _g;
    readonly Node3D _root = new() { Name = "MutagenProps" };
    readonly Dictionary<int, (Node3D web, Node3D wasp, Node3D gland, StandardMaterial3D glow)> _sites = new();
    Node3D _nurseryDressing;
    readonly List<Node3D> _heap = new();
    int _nurseryRoom = -1;
    float _t;

    public MutagenProps(GameSession g) { _g = g; g.AddChild(_root); }
    readonly Dictionary<int, StandardMaterial3D> _cores = new();
    public const uint SkinLayer = 1u << 10;

    /// Where a gland is drawn (its light follows it: up in the web, then in the carrier's jaws).
    public Vector3? GlandPoint(int id) => _sites.TryGetValue(id, out var s) && s.gland != null ? s.gland.GlobalPosition : null;

    // ---------------------------------------------------------------- materials
    const string SilkShader = @"
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_never, shadows_disabled;
void fragment() {
    // a strand is nearly invisible: it shows by the warm light running along it, never as a white wire
    ALBEDO = COLOR.rgb * vec3(1.0, 0.9, 0.72);
    ROUGHNESS = 0.25;
    SPECULAR = 0.8;
    float rim = pow(1.0 - clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0), 2.0);
    EMISSION = vec3(1.0, 0.82, 0.55) * (0.04 + 0.32 * rim);
    ALPHA = 0.22 + 0.42 * rim;
}
";

    const string DewShader = @"
shader_type spatial;
render_mode blend_mix, cull_back, depth_draw_never, shadows_disabled;
void fragment() {
    // a dew bead: clear, a bright rim and a warm glint, the soil showing through
    float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
    ALBEDO = vec3(0.85, 0.82, 0.75);
    ROUGHNESS = 0.08;
    SPECULAR = 1.0;
    EMISSION = vec3(1.0, 0.9, 0.7) * pow(1.0 - facing, 3.0) * 0.5;
    ALPHA = 0.25 + 0.6 * pow(1.0 - facing, 2.0);
}
";

    const string WingShader = @"
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_never, shadows_disabled;
uniform sampler2D veins : source_color, filter_linear_mipmap;
void fragment() {
    vec4 v = texture(veins, UV);
    float fres = pow(1.0 - clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0), 2.0);
    vec3 film = 0.5 + 0.5 * cos(6.2831 * (fres * 1.2 + UV.x * 0.4 + vec3(0.0, 0.33, 0.67)));
    ALBEDO = mix(v.rgb + film * 0.12, v.rgb, v.a);
    ROUGHNESS = 0.12;
    SPECULAR = 0.8;
    ALPHA = clamp(v.a + fres * 0.2, 0.0, 0.92);
}
";

    static ShaderMaterial Mat(string code) => new() { Shader = Shaders.Get(code) };

    /// Vertex-coloured matter (chitin, stems, leaves, fibre, remains): the standard material, colours authored as sRGB.
    static StandardMaterial3D Matter(float roughness, bool twoSided = false) => new()
    {
        VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = roughness,
        CullMode = twoSided ? BaseMaterial3D.CullModeEnum.Disabled : BaseMaterial3D.CullModeEnum.Back,
    };

    static void Dress(Node n, System.Func<string, Material> pick)
    {
        foreach (var c in n.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)c;
            var m = pick(mi.Name.ToString().ToLowerInvariant());
            if (m != null) mi.MaterialOverride = m;
        }
    }

    static Node3D Load(string name) => ResourceLoader.Exists(Root + $"{name}/{name}.gltf") ? GD.Load<PackedScene>(Root + $"{name}/{name}.gltf").Instantiate<Node3D>() : null;

    // ---------------------------------------------------------------- the web and the queen wasp (H1), the gland (H2)
    (Node3D, Node3D, Node3D, StandardMaterial3D) MakeSite(MutagenSource m)
    {
        var web = Load("web");
        var wasp = Load("queen_wasp");
        var gland = Load("gland");
        if (web == null || wasp == null || gland == null) return (null, null, null, null);
        var silk = Mat(SilkShader);
        var dew = Mat(DewShader);
        var plain = Matter(0.75f);
        var leaves = Matter(0.6f, true);
        Dress(web, n => n.Contains("silk") || n.Contains("wrap") ? silk : n.Contains("dew") ? dew : n.Contains("leaves") ? leaves : plain);
        var body = Matter(0.42f);
        var wing = Mat(WingShader);
        if (ResourceLoader.Exists(Root + "queen_wasp/wing_veins.png")) wing.SetShaderParameter("veins", GD.Load<Texture2D>(Root + "queen_wasp/wing_veins.png"));
        var legs = Matter(0.55f);
        Dress(wasp, n => n.Contains("wing") ? wing : n.Contains("leg") ? legs : body);
        // the gland: wet amber flesh, a faint violet-green radiance inside that breathes (StandardMaterial3D: its
        // emission energy and colour are driven every frame)
        var glow = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.18f, Metallic = 0f, MetallicSpecular = 0.7f,
            BacklightEnabled = true, Backlight = new Color(0.7f, 0.4f, 0.15f),
        };
        var core = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.62f, 0.42f, 1f),
            EmissionEnabled = true, Emission = new Color(0.62f, 0.42f, 1f), EmissionEnergyMultiplier = 1f,
        };
        glow.AlbedoColor = new Color(1f, 0.78f, 0.45f); // wet amber skin; the light inside shows only through its veins
        Dress(gland, n => n.Contains("core") ? core : glow);
        // the skin lives on its own render layer, which the gland's own light skips: the light falls on the jaws and
        // the soil around it, never tinting the amber violet
        foreach (var c in gland.FindChildren("*", "MeshInstance3D", true, false)) if (!c.Name.ToString().Contains("core")) ((MeshInstance3D)c).Layers = SkinLayer;
        _cores[m.Id] = core;
        silk.RenderPriority = dew.RenderPriority = 10; // after the terrain, which draws in the transparent pass
        // the web faces the colony's nest, so the player sees it square on; the wasp hangs head-down in the tear
        var home = _g.Sim.NestEntrance(Local.Me);
        float face = Mathf.Atan2(home.X - m.Pos.X, home.Y - m.Pos.Y);
        web.Position = _g.World.HF.Point(m.Pos);
        web.Rotation = new Vector3(0, face, 0);
        wasp.Basis = Basis.FromEuler(new Vector3(-1.45f, 0.1f, -1.25f)).Scaled(Vector3.One * 1.9f); // broadside to the colony, dangling head-down: a queen twice a worker wasp's bulk
        wasp.Position = new Vector3(1.6f, 7.0f, 0.15f);
        web.AddChild(wasp);
        _root.AddChild(web);
        _root.AddChild(gland);
        return (web, wasp, gland, glow);
    }

    public void Update(float dt)
    {
        var sim = _g.Sim;
        _t += dt;
        float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 2.4f);
        foreach (var m in sim.Mutagens)
        {
            if (m.From != AntType.Wasp) continue;
            if (!_sites.TryGetValue(m.Id, out var s)) _sites[m.Id] = s = MakeSite(m);
            if (s.web == null) continue;
            bool seen = _g.Fog == null || _g.Fog.Visible(m.Pos) || m.Carried;
            s.web.Visible = _g.Fog == null || _g.Fog.Explored(new Vector2(s.web.Position.X, s.web.Position.Z));
            s.gland.Visible = !m.Home && seen;
            // a radiance inside the amber, never a lamp; from play zoom the sun would drown it, so it carries a little further there
            float zoom = Mathf.Clamp((_g.Camera.Distance - 14f) / 46f, 0, 1);
            
            // mostly violet, breathing toward green: nothing else in the woods is that colour, so it never reads as food
            var hue = new Color(0.62f, 0.42f, 1f).Lerp(new Color(0.55f, 1f, 0.62f), 0.25f + 0.25f * Mathf.Sin(_t * 0.9f));
            if (_cores.TryGetValue(m.Id, out var coreMat)) { coreMat.AlbedoColor = hue * (0.4f + 0.3f * pulse); coreMat.Emission = hue; coreMat.EmissionEnergyMultiplier = (0.15f + 0.25f * pulse) * (1 + 3f * zoom); } // never so bright it blooms to white
            // in her gaster until it is cut free; then in its carrier's jaws, or on the ground where it fell
            if (!m.Free)
            {
                s.gland.GlobalTransform = s.wasp.GlobalTransform * new Transform3D(Basis.Identity.Scaled(Vector3.One * 0.7f), new Vector3(-0.3f, 0.45f, -0.32f));
            }
            else
            {
                s.gland.GlobalTransform = new Transform3D(Basis.FromEuler(new Vector3(0, -m.Heading, 0)).Scaled(Vector3.One * 1.2f * (1 + 0.7f * zoom)), _g.World.HF.Point(m.Pos, m.Carried ? 0.5f : 0.25f));
            }
        }
        UpdateNursery();
    }

    // ---------------------------------------------------------------- the royal nursery (H3)
    void UpdateNursery()
    {
        var sim = _g.Sim;
        var room = sim.Nursery(Local.Me);
        if (room == null || _g.Interior == null) return;
        if (_nurseryDressing == null || _nurseryRoom != room.Id)
        {
            var node = _g.Interior.RoomNode(Local.Me, room.Id);
            if (node == null) return;
            var dressing = Load("nursery");
            if (dressing == null) return;
            var fibre = Matter(0.85f, true);
            var remains = Matter(0.45f);
            var clay = Matter(0.9f);
            var scrap = Mat(WingShader);
            if (ResourceLoader.Exists(Root + "queen_wasp/wing_veins.png")) scrap.SetShaderParameter("veins", GD.Load<Texture2D>(Root + "queen_wasp/wing_veins.png"));
            Dress(dressing, n => n.StartsWith("nursery_bed") ? fibre : n.StartsWith("nursery_ledge") ? clay : n.EndsWith("_wing") ? scrap : remains);
            // warm light falling from above into the nursery, as in the target
            dressing.AddChild(new OmniLight3D { Position = new Vector3(0, 2.8f, 0.4f), OmniRange = 6f, LightEnergy = 1.8f, LightColor = new Color(1f, 0.66f, 0.36f), ShadowEnabled = false });
            dressing.Scale = Vector3.One * 0.9f; // the nest view draws ants large; the bed and the heap match them
            node.AddChild(dressing);
            _nurseryDressing = dressing;
            _nurseryRoom = room.Id;
            _heap.Clear();
            foreach (var c in dressing.FindChildren("heap_*", "Node3D", true, false)) _heap.Add((Node3D)c);
            _heap.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        }
        // the living gauge: the heap grows with every load the nurses feed the larva, and is eaten down to nothing as
        // the cocoon spins
        var col = sim.Col[Local.Me];
        float fill = col.LarvaStage == 2 ? col.LarvaFed / Mathf.Max(1, col.LarvaNeed) : col.LarvaStage == 3 ? 1 : 0;
        int shown = Mathf.RoundToInt(fill * _heap.Count);
        for (int i = 0; i < _heap.Count; i++) _heap[i].Visible = i < shown;
    }
}
