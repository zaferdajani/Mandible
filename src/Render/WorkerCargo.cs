using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Actual carried food, attached to the worker's animated head. No independent
/// animation clock or world-space offset can let the load drift out of its jaws.
public static class WorkerCargo
{
    static readonly Dictionary<FoodKind, ArrayMesh> Meshes = new();
    static readonly Dictionary<FoodKind, ShaderMaterial> Materials = new();
    static string _shader;
    static string ShaderCode => _shader ??= Shaders.Ant[..Shaders.Ant.IndexOf("void fragment()", StringComparison.Ordinal)] + @"
uniform int food_kind = 0;
uniform vec3 piece_tint : source_color = vec3(0.03, 0.026, 0.024);
void fragment() {
    vec3 p=v_local*34.0;
    float n=hash3(floor(p));
    vec3 col=vec3(0.55,0.29,0.075); float rough=0.72;
    if(food_kind==1) { col=mix(vec3(0.25,0.012,0.019),vec3(0.58,0.035,0.045),n); rough=0.25; }
    else if(food_kind==2) { col=vec3(0.88,0.82,0.63)*(0.84+n*0.16); rough=0.18; }
    else if(food_kind==3) {
        float seam=exp(-pow((v_local.x+0.012)*170.0,2.0));
        col=vec3(0.43,0.25,0.055)*(1.0-seam*0.4); rough=0.46;
    }
    else if(food_kind==4) { col=mix(vec3(0.19,0.065,0.022),vec3(0.48,0.27,0.11),smoothstep(0.12,0.22,v_local.y)); rough=0.48; }
    else if(food_kind==7) {
        // a piece of a carcass: the chitin it came from, glossy where the cut is wet with hemolymph
        float wet=smoothstep(0.6,0.95,n)*0.35;
        col=mix(piece_tint*(0.82+0.36*n),vec3(0.55,0.38,0.09),wet); rough=mix(0.5,0.15,wet);
    }
    else if(food_kind>=5) {
        // brood in a nurse's jaws: a glossy cream egg, or a pale segmented larva
        col=food_kind==5 ? vec3(0.9,0.8,0.62) : vec3(0.86,0.74,0.53)*(0.9+0.1*sin(v_local.z*190.0));
        rough=food_kind==5 ? 0.22 : 0.34; BACKLIGHT=col*0.25;
    }
    else col*=0.76+0.30*n;
    ALBEDO=col; ROUGHNESS=rough; SPECULAR=0.35;
    AO=0.85; AO_LIGHT_AFFECT=0.25;
}
";

    public static ShaderMaterial Material(FoodKind kind)
    {
        if (Materials.TryGetValue(kind, out var material)) return material;
        Materials[kind] = material = Make((int)kind);
        return material;
    }

    static ShaderMaterial Make(int kind)
    {
        var material = new ShaderMaterial { Shader = Shaders.Get(ShaderCode) };
        material.SetShaderParameter("food_kind", kind);
        material.SetShaderParameter("neck", AntMesh.Neck[AntType.Worker]);
        return material;
    }

    // Brood a nurse carries uses the same grip: the load is weighted to the head, so it pitches and turns with it.
    static readonly ShaderMaterial[] BroodMaterials = new ShaderMaterial[2];
    static readonly ArrayMesh[] BroodMeshes = new ArrayMesh[2];

    public static ShaderMaterial BroodMaterial(bool larva) => BroodMaterials[larva ? 1 : 0] ??= Make(larva ? 6 : 5);

    public static ArrayMesh BroodMesh(bool larva)
    {
        int i = larva ? 1 : 0;
        if (BroodMeshes[i] != null) return BroodMeshes[i];
        var k = new MeshKit();
        var headPart = new Color(AntMesh.Head / 255f, 0, 0, 1);
        // the same size as the brood lying in the beds
        var centre = new Vector3(0, 0.15f, 0.5f);
        if (!larva)
            // held crosswise between the mandible tips, the way nurses pinch an egg
            k.Ellipsoid(centre, Basis.FromScale(new Vector3(0.26f, 0.17f, 0.165f)), 20, 14, headPart, Vector2.Zero, p => 1 + 0.1f * p.X);
        else
            // a grub gripped at its middle, both ends curling down
            for (int s = 0; s < 9; s++)
            {
                float t = s / 8f - 0.5f;
                k.Ellipsoid(centre + new Vector3(t * 0.6f, -0.17f * t * t * 4 + 0.03f, 0.06f * Mathf.Cos(t * 3)),
                    Basis.FromScale(Vector3.One * (0.15f - 0.06f * Mathf.Abs(t))), 12, 8, headPart, Vector2.Zero);
            }
        k.SmoothNormals(0, 0);
        return BroodMeshes[i] = k.Commit();
    }

    // Carcass pieces: three shapes (0 a head with its jaws, 1 a gaster, 2 a bundle of legs) in three chitins (0 black, 1 red, 2 bug).
    static readonly ArrayMesh[] PieceMeshes = new ArrayMesh[3];
    static readonly ShaderMaterial[] PieceMaterials = new ShaderMaterial[3];
    static readonly Color[] PieceTints = { new(0.032f, 0.028f, 0.026f), new(0.42f, 0.12f, 0.045f), new(0.05f, 0.055f, 0.035f) };

    public static ShaderMaterial PieceMaterial(int tint)
    {
        if (PieceMaterials[tint] != null) return PieceMaterials[tint];
        var m = Make(7);
        m.SetShaderParameter("piece_tint", PieceTints[tint]);
        return PieceMaterials[tint] = m;
    }

    public static ArrayMesh PieceMesh(int shape)
    {
        if (PieceMeshes[shape] != null) return PieceMeshes[shape];
        var k = new MeshKit();
        var centre = new Vector3(0, 0.15f, 0.47f);
        var headPart = new Color(AntMesh.Head / 255f, 0, 0, 1);
        switch (shape)
        {
            case 0:
                // a severed head held by the neck: the heart-shaped capsule, two eyes, the dead jaws hanging open
                k.Ellipsoid(centre, Basis.FromScale(new Vector3(0.085f, 0.07f, 0.08f)), 14, 10, headPart, Vector2.Zero, p => 1 + 0.06f * Mathf.Abs(p.X) - 0.05f * Mathf.Max(0, -p.Z));
                for (int side = -1; side <= 1; side += 2)
                {
                    k.Ellipsoid(centre + new Vector3(side * 0.07f, 0.02f, 0.03f), Basis.FromScale(Vector3.One * 0.022f), 8, 6, headPart, Vector2.Zero);
                    var hinge = centre + new Vector3(side * 0.04f, -0.03f, 0.06f);
                    k.Tube(hinge, hinge + new Vector3(side * 0.035f, -0.01f, 0.07f), 0.014f, 0.003f, 5, headPart, headPart, Vector2.Zero);
                    var root = centre + new Vector3(side * 0.03f, 0.04f, 0.05f);
                    k.Tube(root, root + new Vector3(side * 0.1f, 0.05f, 0.08f), 0.008f, 0.004f, 4, headPart, headPart, Vector2.Zero);
                }
                break;
            case 1:
                // a gaster, its tergites still banded
                k.Ellipsoid(centre + new Vector3(0, -0.01f, 0.02f), new Basis(Vector3.Up, 1.3f) * Basis.FromScale(new Vector3(0.1f, 0.085f, 0.14f)), 16, 12, headPart, Vector2.Zero,
                    p => 1 + 0.04f * (1 - Mathf.Abs(Mathf.Sin(p.Z * 7.5f))));
                break;
            default:
                // three legs pulled off together, bent at the knee
                for (int l = -1; l <= 1; l++)
                {
                    var hip = centre + new Vector3(l * 0.03f, 0, -0.01f);
                    var knee = hip + new Vector3(l * 0.12f + 0.04f, 0.06f, 0.05f);
                    var foot = knee + new Vector3(l * 0.1f - 0.06f, -0.09f, 0.06f);
                    k.Tube(hip, knee, 0.016f, 0.012f, 5, headPart, headPart, Vector2.Zero);
                    k.Tube(knee, foot, 0.011f, 0.004f, 5, headPart, headPart, Vector2.Zero);
                }
                break;
        }
        k.SmoothNormals(0, 0);
        return PieceMeshes[shape] = k.Commit();
    }

    public static ArrayMesh Mesh(FoodKind kind)
    {
        if (Meshes.TryGetValue(kind, out var mesh)) return mesh;
        var k = new MeshKit();
        var centre = new Vector3(0, 0.145f, 0.445f);
        var headPart = new Color(AntMesh.Head / 255f, 0, 0, 1);
        switch (kind)
        {
            case FoodKind.Berry:
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 2.4f;
                    k.Ellipsoid(centre + new Vector3(Mathf.Cos(a) * 0.065f, i % 2 * 0.025f, Mathf.Sin(a) * 0.065f),
                        Basis.FromScale(new Vector3(0.075f, 0.075f, 0.08f)), 14, 10, headPart, Vector2.Zero);
                }
                break;
            case FoodKind.Sugar:
                k.Ellipsoid(centre, Basis.FromScale(new Vector3(0.13f, 0.105f, 0.14f)), 7, 5, headPart, Vector2.Zero,
                    p => 1 + 0.09f * Mathf.Sin(p.X * 15 + p.Z * 11));
                k.RecalcNormals();
                break;
            case FoodKind.Seed:
                k.Ellipsoid(centre, new Basis(Vector3.Up, 0.38f) * Basis.FromScale(new Vector3(0.105f, 0.09f, 0.19f)), 20, 14, headPart, Vector2.Zero,
                    p => 1 - 0.2f * p.Z - 0.08f * Mathf.Exp(-p.X * p.X * 100) * Mathf.Max(0, p.Y));
                break;
            case FoodKind.Corpse:
                k.Ellipsoid(centre, Basis.FromScale(new Vector3(0.14f, 0.09f, 0.17f)), 16, 12, headPart, Vector2.Zero,
                    p => 1 + 0.09f * Mathf.Sin(p.Z * 16) + 0.06f * Mathf.Sin(p.X * 11));
                break;
            default:
                k.Ellipsoid(centre, Basis.FromScale(new Vector3(0.14f, 0.105f, 0.15f)), 14, 10, headPart, Vector2.Zero,
                    p => 0.88f + 0.17f * Mathf.Abs(Mathf.Sin(p.X * 7 + p.Y * 5) * Mathf.Cos(p.Z * 6)));
                break;
        }
        k.SmoothNormals(0, 0);
        Meshes[kind] = mesh = k.Commit();
        return mesh;
    }
}
