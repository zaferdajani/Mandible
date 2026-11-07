using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Instanced, volumetric chamber contents. Detail is local to the renderer: the sim
/// population, food, routes and random sequence are never changed by dressing.
public sealed partial class NestInterior
{
    enum Surface { Egg, Larva, Cocoon, Seed, Resin, Jaw, Shell, Root, Leaf, Stone, Silk }
    static readonly Dictionary<Surface, ShaderMaterial> OrganicMaterials = new();
    static readonly Dictionary<Surface, ArrayMesh> OrganicMeshes = new();
    const string OrganicShader = @"
shader_type spatial;
render_mode cull_disabled, ambient_light_disabled; // lit by the nest's lamps and its own warm fill only: the sky's blue never reaches down here
" + Shaders.Under + @"
uniform vec4 tint : source_color = vec4(1.0);
uniform int kind = 0;
varying vec3 lp;
varying vec3 vc;
varying float variation;
varying float wet; // a freshly laid egg glistens
void vertex() { lp = VERTEX; vc = COLOR.rgb; variation = INSTANCE_CUSTOM.x; wet = INSTANCE_CUSTOM.y; }
float hash(vec3 p) { return fract(sin(dot(p,vec3(127.1,311.7,74.7))) * 43758.5453); }
float noise(vec3 p) {
    vec3 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
    return mix(mix(mix(hash(i),hash(i+vec3(1,0,0)),f.x),mix(hash(i+vec3(0,1,0)),hash(i+vec3(1,1,0)),f.x),f.y),
               mix(mix(hash(i+vec3(0,0,1)),hash(i+vec3(1,0,1)),f.x),mix(hash(i+vec3(0,1,1)),hash(i+vec3(1,1,1)),f.x),f.y),f.z);
}
void fragment() {
    float n=noise(lp*26.0), fine=noise(lp*155.0);
    vec3 col=tint.rgb*(0.86+variation*0.19)*(0.92+n*0.12);
    float rough=0.38;
    SPECULAR=0.35;
    if(kind==0 || kind==1) {
        col*=mix(vec3(0.75,0.62,0.42),vec3(1.0),smoothstep(-0.12,0.13,lp.y));
        rough=kind==0 ? 0.24 : 0.35;
        BACKLIGHT=col*0.12;
        CLEARCOAT=mix(0.22,1.0,wet); CLEARCOAT_ROUGHNESS=mix(0.35,0.04,wet);
        rough=mix(rough,0.06,wet); col=mix(col,col*vec3(1.04,1.02,0.9),wet); EMISSION=col*0.3*wet;
    } else if(kind==2 || kind==10) {
        float fiber=sin(UV.x*420.0+sin(UV.y*38.0)*2.0)*sin(UV.y*330.0+UV.x*16.0);
        col*=0.89+0.09*fiber+0.15*fine; rough=0.83;
        if(kind==10) col*=mix(0.78,1.07,vc.r);
    } else if(kind==3) {
        float seam=exp(-pow((UV.x-0.25)*42.0,2.0));
        col*=mix(vec3(0.64,0.71,0.38),vec3(1.0,0.81,0.55),variation);
        col*=1.0-seam*0.32; col*=0.88+0.2*fine; rough=0.52;
    } else if(kind==4) {
        rough=0.12; SPECULAR=0.55; CLEARCOAT=0.7; CLEARCOAT_ROUGHNESS=0.1;
        col*=mix(0.44,1.0,smoothstep(-0.1,0.22,lp.y)); BACKLIGHT=col*0.3;
    } else if(kind==5 || kind==6) {
        col*=0.83+0.25*fine; rough=0.29+n*0.14;
        CLEARCOAT=0.5; CLEARCOAT_ROUGHNESS=0.26; METALLIC=0.12;
        if(kind==6) {
            col*=0.89+0.11*sin(lp.x*57.0+lp.z*9.0);
            col=mix(col,vec3(0.33,0.19,0.055),vc.r);
        }
    } else if(kind==7) {
        float bark=noise(vec3(lp.x*12.0,lp.y*80.0,lp.z*75.0));
        col*=0.64+0.55*bark; rough=0.92;
    } else if(kind==8) {
        float rib=exp(-abs(UV.x-0.5)*70.0);
        float veins=pow(0.5+0.5*cos(UV.y*80.0-abs(UV.x-0.5)*30.0),16.0);
        col=mix(col,col*1.5,rib*0.45+veins*0.17); rough=0.67; BACKLIGHT=col*0.2;
    } else { col*=0.66+0.5*n; rough=0.93; }
    ALBEDO=col; ROUGHNESS=rough;
    EMISSION+=col*vec3(1.0,0.8,0.58)*0.26; // the warm bounce of the lamps' light off sand: what the sky's blue fill was standing in for
    AO=(0.68+0.32*smoothstep(-0.2,0.16,lp.y))*under_ambient((INV_VIEW_MATRIX*vec4(VERTEX,1.0)).xyz); AO_LIGHT_AFFECT=0.4;
}
";
    static ShaderMaterial OrganicMat(Surface type)
    {
        if (OrganicMaterials.TryGetValue(type, out var material)) return material;
        string tint = type switch
        {
            Surface.Egg => "e7cfaa", Surface.Larva => "dcc093", Surface.Cocoon => "bb996b",
            Surface.Seed => "b99345", Surface.Resin => "bb7511", Surface.Jaw => "252023",
            Surface.Shell => "8a5a2a", Surface.Root => "8c643b", Surface.Leaf => "9c7a3a", // shed plates: amber-brown chitin; a dry leaf, not a green one
            Surface.Stone => "a58d6f", _ => "ddd0ad",
        };
        material = new ShaderMaterial { Shader = Shaders.Get(OrganicShader) };
        material.SetShaderParameter("kind", (int)type); material.SetShaderParameter("tint", new Color(tint));
        OrganicMaterials[type] = material;
        return material;
    }

    internal static void ValidateInteriorMaterials()
    {
        foreach (var entry in OrganicMaterials)
            if (entry.Value.Shader.GetShaderUniformList().Count < 2)
                throw new InvalidOperationException($"Chamber material failed to compile: {entry.Key}");
        if (OrganicMaterials.Count != Enum.GetValues<Surface>().Length)
            throw new InvalidOperationException("Not every chamber material was exercised");
        var soil = GD.Load<Texture2D>("res://assets/materials/nest-soil-albedo.png");
        if (soil == null || soil.GetWidth() < 512)
            throw new InvalidOperationException("Nest soil texture is missing or invalid");
    }

    static ArrayMesh OrganicMesh(Surface type)
    {
        if (OrganicMeshes.TryGetValue(type, out var mesh)) return mesh;
        var k = new MeshKit();
        switch (type)
        {
            case Surface.Egg:
                k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(0.13f, 0.14f, 0.21f)), 24, 16, Colors.White, Vector2.Zero,
                    p => 1 - 0.12f * p.Z + 0.025f * p.X * p.Z);
                break;
            case Surface.Larva:
                // A continuous curled grub with constricted segments, head and rounded tail.
                const int rings = 30, sides = 16;
                for (int j = 0; j <= rings; j++)
                {
                    float t = j / (float)rings, a = -0.85f + t * 1.7f;
                    var centre = new Vector3(0.28f * Mathf.Cos(a) - 0.19f, 0.025f * Mathf.Sin(t * Mathf.Pi), 0.34f * Mathf.Sin(a));
                    var side = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    float radius = (0.08f + 0.055f * Mathf.Sin(t * Mathf.Pi)) * (0.96f + 0.04f * Mathf.Cos(t * Mathf.Pi * 16));
                    radius *= Mathf.Pow(Mathf.Max(0.015f, Mathf.Sin(t * Mathf.Pi)), 0.22f);
                    for (int s = 0; s <= sides; s++)
                    {
                        float phi = s * Mathf.Tau / sides; var n = Vector3.Up * Mathf.Cos(phi) + side * Mathf.Sin(phi);
                        k.Add(centre + n * radius, n, Colors.White, new Vector2(s / (float)sides, t), Vector2.Zero);
                    }
                }
                for (int j = 0; j < rings; j++) for (int s = 0; s < sides; s++)
                { int a = j * (sides + 1) + s; k.Quad(a, a + 1, a + sides + 2, a + sides + 1); }
                break;
            case Surface.Cocoon:
                k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(0.17f, 0.16f, 0.34f)), 30, 18, Colors.White, Vector2.Zero,
                    p => 1 + 0.014f * Mathf.Sin(p.Z * 90 + p.X * 11));
                break;
            case Surface.Silk:
                k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(0.5f, 0.42f, 0.65f)), 30, 18, Colors.Black, Vector2.Zero);
                // Wound fibres change the silhouette and catch light at every zoom.
                Vector3 Winding(float t)
                {
                    float z = -0.61f + t * 1.22f, a = t * Mathf.Tau * 32;
                    float radius = Mathf.Sqrt(Mathf.Max(0.01f, 1 - z * z / (0.65f * 0.65f)));
                    return new Vector3(Mathf.Cos(a) * (0.50f * radius + 0.012f), Mathf.Sin(a) * (0.42f * radius + 0.012f), z);
                }
                for (int i = 0; i < 1024; i++)
                    k.Tube(Winding(i / 1024f), Winding((i + 1) / 1024f), 0.012f, 0.012f, 5, Colors.White, Colors.White, Vector2.Zero, false);
                break;
            case Surface.Seed:
                k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(0.17f, 0.11f, 0.28f)), 22, 14, Colors.White, Vector2.Zero,
                    p => 1 - 0.14f * p.Z - 0.07f * Mathf.Exp(-p.X * p.X * 180) * Mathf.Max(0, p.Y));
                break;
            case Surface.Resin:
                k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(0.20f, 0.22f, 0.19f)), 24, 16, Colors.White, Vector2.Zero,
                    p => 1 - 0.24f * Mathf.Max(0, p.Y) + 0.06f * Mathf.Sin(p.X * 8 + p.Z * 5));
                break;
            case Surface.Jaw:
                k.Loft(new[] { new Vector3(0.1f, 0, -1.12f), new Vector3(-0.65f, 0.10f, -0.75f), new Vector3(-0.87f, 0.16f, 0), new Vector3(-0.5f, 0.12f, 0.75f), new Vector3(0.48f, 0.04f, 1.03f) },
                    new[] { 0.28f, 0.29f, 0.24f, 0.16f, 0.008f }, new float[5], 0, 0, 20, Vector2.Zero, 8);
                for (int i = 0; i < 6; i++)
                {
                    float t = i / 5f, a = -0.9f + t * 1.7f;
                    var p = new Vector3(-0.72f * Mathf.Cos(a), 0.14f, Mathf.Sin(a) * 0.9f);
                    var tip = p + new Vector3((0.53f - t * 0.16f) * Mathf.Cos(a), 0, -0.11f);
                    k.Loft(new[] { p, p.Lerp(tip, 0.55f) + new Vector3(0, 0.025f, 0.05f), tip }, new[] { 0.13f - t * 0.025f, 0.085f, 0.002f }, new float[3], 0, 0, 12, Vector2.Zero, 4);
                }
                for (int i = 0; i < k.V.Count; i++) k.V[i] *= new Vector3(1, 0.65f, 1);
                break;
            case Surface.Shell:
                // Shed chitin segments: open arches overlap into two ribbed carapaces.
                const int seg = 36, rows = 14;
                Vector3 Scute(float u, float v)
                {
                    float angle = (u - 0.5f) * Mathf.Pi * 0.92f;
                    return new Vector3(Mathf.Sin(angle) * (0.94f - 0.12f * v),
                        Mathf.Cos(angle) * (0.60f - 0.06f * v) + 0.025f * Mathf.Sin(angle * 7),
                        (v - 0.5f) * 0.98f + 0.13f * Mathf.Cos(angle) * Mathf.Cos(v * Mathf.Pi));
                }
                for (int r = 0; r <= rows; r++) for (int s = 0; s <= seg; s++)
                {
                    float u = s / (float)seg, v = r / (float)rows;
                    k.Add(Scute(u, v), Vector3.Up, Colors.Black, new Vector2(u, v), Vector2.Zero);
                }
                for (int r = 0; r < rows; r++) for (int s = 0; s < seg; s++)
                { int a = r * (seg + 1) + s; k.Quad(a, a + 1, a + seg + 2, a + seg + 1); }
                for (int edge = 0; edge <= 1; edge++) for (int i = 0; i < seg; i++)
                {
                    k.Tube(Scute(i / (float)seg, edge), Scute((i + 1) / (float)seg, edge), 0.023f, 0.023f, 8, Colors.White, Colors.White, Vector2.Zero, false);
                }
                break;
            case Surface.Root:
                k.Loft(new[] { new Vector3(-0.9f, 0, -0.12f), new Vector3(-0.3f, 0.07f, 0.08f), new Vector3(0.35f, 0.1f, -0.08f), new Vector3(0.9f, 0.16f, 0.11f) },
                    new[] { 0.07f, 0.06f, 0.045f, 0.016f }, new float[4], 0, 0, 9, Vector2.Zero, 4);
                break;
            case Surface.Leaf:
                for (int j = 0; j <= 20; j++) for (int side = -1; side <= 1; side++)
                {
                    float t = j / 20f, width = Mathf.Sin(t * Mathf.Pi) * 0.36f * (1 + 0.05f * Mathf.Sin(j * 4));
                    k.Add(new Vector3(width * side, 0.09f * Mathf.Sin(t * Mathf.Pi) - 0.08f * Mathf.Abs(side), t * 1.65f - 0.8f), Vector3.Up, Colors.White, new Vector2((side + 1) * 0.5f, t), Vector2.Zero);
                }
                for (int j = 0; j < 20; j++) for (int s = 0; s < 2; s++) { int a = j * 3 + s; k.Quad(a, a + 1, a + 4, a + 3); }
                break;
            default:
                k.Ellipsoid(Vector3.Zero, Basis.FromScale(new Vector3(0.12f, 0.07f, 0.10f)), 10, 7, Colors.White, Vector2.Zero,
                    p => 1 + 0.19f * Mathf.Sin(p.X * 11 + p.Z * 6) * Mathf.Cos(p.Y * 8));
                break;
        }
        k.SmoothNormals(0, 0);
        OrganicMeshes[type] = mesh = k.Commit();
        return mesh;
    }

    static readonly PlaneMesh ContactPlane = new() { Size = Vector2.One };
    static ShaderMaterial _contact;
    static ShaderMaterial Contact => _contact ??= new ShaderMaterial { Shader = Shaders.Get(@"
shader_type spatial;
render_mode unshaded, depth_draw_never, cull_disabled;
void fragment() {
    float r=length((UV-0.5)*2.0);
    ALBEDO=vec3(0.055,0.028,0.009);
    ALPHA=(1.0-smoothstep(0.05,1.0,r))*0.37;
}
") };

    static void OrganicScatter(Node parent, Surface type, int count, Func<int, Transform3D> place, bool contacts = true)
    {
        if (count <= 0) return;
        var mesh = OrganicMesh(type); var aabb = mesh.GetAabb();
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = mesh, InstanceCount = count };
        var shadow = contacts ? new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = ContactPlane, InstanceCount = count } : null;
        for (int i = 0; i < count; i++)
        {
            var transform = place(i);
            mm.SetInstanceTransform(i, transform); mm.SetInstanceCustomData(i, new Color(Mathf.PosMod(i * 0.618f, 1), 0, 0));
            if (shadow == null) continue;
            float sx = transform.Basis.X.Length(), sz = transform.Basis.Z.Length();
            var basis = new Basis(Vector3.Up, Mathf.Atan2(transform.Basis.Z.X, transform.Basis.Z.Z));
            basis *= Basis.FromScale(new Vector3(Mathf.Max(0.1f, aabb.Size.X * sx * 1.65f), 1, Mathf.Max(0.1f, aabb.Size.Z * sz * 1.5f)));
            shadow.SetInstanceTransform(i, new Transform3D(basis, new Vector3(transform.Origin.X, 0.015f + i * 0.000003f, transform.Origin.Z)));
        }
        if (shadow != null) parent.AddChild(new MultiMeshInstance3D { Multimesh = shadow, MaterialOverride = Contact, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        parent.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = OrganicMat(type), CastShadow = Settings.Gfx >= Settings.Quality.Medium ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off });
    }

    void DressBanks(Room room, float density)
    {
        var rng = new Rng((uint)(room.Ch.Id * 313 + 924));
        var (w, _, d) = RoomSize(room.Ch); var mesh = new MeshKit();
        var geometry = _geometry[room.Team]; geometry.Refresh(_g.Sim.Col[room.Team].Chambers);
        for (int i = 0; i < 11 * density; i++)
        {
            float angle = i * 2.39996f + rng.R(-0.12f, 0.12f); var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var edge = dir * new Vector2(w, d);
            if (geometry.Distance(room.Ch.Offset + edge) < -0.45f) continue; // keep tunnel mouths clear
            var start = new Vector3(edge.X, 1.7f + rng.R(0, 0.6f), edge.Y);
            var end = new Vector3(edge.X * 0.72f + rng.R(-0.4f, 0.4f), 0.08f, edge.Y * 0.72f);
            var middle = start.Lerp(end, 0.55f) + new Vector3(rng.R(-0.25f, 0.25f), -0.4f, rng.R(-0.25f, 0.25f));
            mesh.Loft(new[] { start, middle, end }, new[] { rng.R(0.04f, 0.08f), 0.04f, 0.009f }, new float[3], 0, 0, 8, Vector2.Zero, 5);
            for (int b = 0; b < 3; b++)
            {
                var tip = end + new Vector3(rng.R(-0.6f, 0.6f), 0.01f, rng.R(-0.6f, 0.6f));
                mesh.Loft(new[] { middle, end, tip }, new[] { 0.026f, 0.015f, 0.002f }, new float[3], 0, 0, 6, Vector2.Zero, 4);
            }
        }
        if (mesh.V.Count > 0) { mesh.SmoothNormals(0, 0); room.Contents.AddChild(new MeshInstance3D { Mesh = mesh.Commit(), MaterialOverride = OrganicMat(Surface.Root) }); }
        OrganicScatter(room.Contents, Surface.Stone, (int)(95 * density), i =>
        {
            var p = rng.InDisk(1); p *= new Vector2(w * 0.79f, d * 0.76f);
            float scale = rng.R(0.25f, 0.8f);
            return new Transform3D(new Basis(Vector3.Up, rng.R(0, Mathf.Tau)) * Basis.FromScale(Vector3.One * scale), new Vector3(p.X, 0.04f, p.Y));
        }, false);
    }
}
