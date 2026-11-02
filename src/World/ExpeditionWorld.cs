using Godot;

namespace Mandible;

public static partial class WorldBuilder
{
    static void BuildExpedition(World w, Rng rng)
    {
        // Banks visibly delimit the same six-unit corridor used by movement constraints.
        var stone = new StandardMaterial3D { AlbedoColor = new Color(0.43f, 0.35f, 0.23f), Roughness = 1 };
        var grass = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.39f, 0.13f), Roughness = 0.9f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        for (int x = 39; x < 120; x += 3)
            foreach (int side in new[] { -1, 1 })
            {
                var p = new Vector2(x, 256 + side * (7.8f + rng.R(0, 1.1f)));
                w.Root.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 1.5f, Height = 2.1f, RadialSegments = 7, Rings = 4 }, MaterialOverride = stone, Position = w.HF.Point(p, 0.1f), Scale = new Vector3(1.2f, 0.85f, 1), Rotation = new Vector3(rng.R(0, 1), rng.R(0, 6), 0) });
                for (int b = 0; b < 4; b++)
                    w.Root.AddChild(new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(0.65f, 4 + b * 0.3f, 0.08f) }, MaterialOverride = grass, Position = w.HF.Point(p + new Vector2(b * 0.4f, side * 1.2f), 1.7f), Rotation = new Vector3(side * 0.15f, b * 1.7f, 0.15f) });
            }
        Spawn(w, rng, FoodKind.Seed, Expedition.Clearing + new Vector2(12, -9), 1000, 2.2f);
        Spawn(w, rng, FoodKind.Seed, Expedition.Clearing + new Vector2(15, 5), 800, 1.8f);
        Spawn(w, rng, FoodKind.Berry, Expedition.Clearing + new Vector2(9, 14), 700, 2);
        Spawn(w, rng, FoodKind.Sugar, Expedition.Clearing + new Vector2(-10, -12), 500, 1.4f);
        w.Food.Add(new FoodSource { Id = w.NextFoodId++, Kind = FoodKind.Corpse, Pos = Expedition.Clearing + new Vector2(16, -1), Amount = 900, Max = 900, Radius = 2.3f, Team = Config.Wild, AntType = AntType.Beetle, Heading = 0.7f });
    }
}
