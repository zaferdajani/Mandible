using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The photoscanned pieces in assets/nature (CC0, see docs/asset-credits.md): meshes cut to phone budgets by
/// tools/asset-process.gd, their textures, and what each one is (stone, rock, bark, twig, leaf, root, food).
public static class NatureAssets
{
    public sealed class Piece
    {
        public string File, Src, Kind;
        public Vector3 Size;       // metres, as scanned
        public Mesh Mesh;
        public Material Material;
        /// Turns the piece so its longest side lies along z, flat on the ground (branches and leaves were scanned upright).
        public Basis Lie = Basis.Identity;
        public float Longest => Mathf.Max(Size.X, Mathf.Max(Size.Y, Size.Z));
        public float Footprint => Mathf.Max(Size.X, Size.Z);
        /// The dead, flattened sprigs of the grass scan (its first three pieces).
        public bool Dead => File is "grass_bermuda_01_0.res" or "grass_bermuda_01_1.res" or "grass_bermuda_01_2.res";
    }

    static readonly Dictionary<string, Texture2D> _tex = new();
    static readonly Dictionary<string, Material> _mats = new();
    static Dictionary<string, List<Piece>> _byKind;

    public static Texture2D Texture(string name)
    {
        if (!_tex.TryGetValue(name, out var t))
            _tex[name] = t = ResourceLoader.Exists($"res://assets/nature/{name}.png") ? GD.Load<Texture2D>($"res://assets/nature/{name}.png") : GD.Load<Texture2D>($"res://assets/nature/{name}.jpg");
        return t;
    }

    /// A picture in assets/ui (the colony map painting).
    public static Texture2D UiTexture(string name)
    {
        if (!_tex.TryGetValue("ui/" + name, out var t)) _tex["ui/" + name] = t = GD.Load<Texture2D>($"res://assets/ui/{name}.jpg");
        return t;
    }

    public static List<Piece> Kind(string kind)
    {
        Load();
        return _byKind.TryGetValue(kind, out var l) ? l : new List<Piece>();
    }

    static void Load()
    {
        if (_byKind != null) return;
        _byKind = new();
        var json = Json.ParseString(FileAccess.GetFileAsString("res://assets/nature/meshes/pieces.json")).AsGodotArray();
        foreach (var v in json)
        {
            var d = v.AsGodotDictionary();
            var size = d["size"].AsGodotArray();
            var p = new Piece
            {
                File = (string)d["file"], Src = (string)d["src"], Kind = (string)d["kind"],
                Size = new Vector3((float)size[0], (float)size[1], (float)size[2]),
            };
            p.Mesh = GD.Load<Mesh>("res://assets/nature/meshes/" + p.File);
            p.Material = MaterialFor(p.Src);
            if (p.Kind is "twig" or "leaf" or "root" or "bark")
            {
                // the longest side goes along z, the shortest up
                if (p.Size.Y >= p.Size.X && p.Size.Y >= p.Size.Z) p.Lie = new Basis(Vector3.Right, Mathf.Pi / 2);
                else if (p.Size.X >= p.Size.Z) p.Lie = new Basis(Vector3.Up, Mathf.Pi / 2);
            }
            if (!_byKind.TryGetValue(p.Kind, out var list)) _byKind[p.Kind] = list = new List<Piece>();
            list.Add(p);
        }
    }

    /// Sources whose scans came out greyer than the yard's warm light: nudged toward the soil they lie in.
    static readonly Dictionary<string, Color> Warm = new()
    {
        ["rock_moss_set_01"] = new Color(1.08f, 0.97f, 0.84f), ["rock_moss_set_02"] = new Color(1.08f, 0.97f, 0.84f),
        ["boulder_01"] = new Color(1.1f, 0.98f, 0.82f), ["stone_01"] = new Color(1.06f, 0.97f, 0.86f),
        ["rock_07"] = new Color(1.08f, 0.98f, 0.84f), ["rock_09"] = new Color(1.08f, 0.98f, 0.84f),
        ["dry_branches_medium_01"] = new Color(1.12f, 0.95f, 0.78f), ["bark_debris_01"] = new Color(1.05f, 0.93f, 0.8f),
    };

    public static Material MaterialFor(string src)
    {
        if (_mats.TryGetValue(src, out var m)) return m;
        var s = new StandardMaterial3D
        {
            AlbedoTexture = Texture($"tex/{src}_diff"),
            AlbedoColor = Warm.TryGetValue(src, out var c) ? c : Colors.White,
            VertexColorUseAsAlbedo = true, // MultiMesh instance colour: each stone a little different
            NormalEnabled = true, NormalTexture = Texture($"tex/{src}_nor"),
            RoughnessTexture = Texture($"tex/{src}_arm"), RoughnessTextureChannel = BaseMaterial3D.TextureChannel.Green,
            AOEnabled = true, AOTexture = Texture($"tex/{src}_arm"), AOTextureChannel = BaseMaterial3D.TextureChannel.Red,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        if (src.StartsWith("grass"))
        {
            // photographed blades on cards: cut out by the atlas alpha, seen from both sides, lit through like leaves
            s.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
            s.AlphaScissorThreshold = 0.5f;
            s.AlphaAntialiasingMode = BaseMaterial3D.AlphaAntiAliasing.AlphaToCoverage;
            s.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            s.BacklightEnabled = true;
            s.Backlight = new Color(0.35f, 0.3f, 0.12f);
        }
        _mats[src] = s;
        return s;
    }
}
