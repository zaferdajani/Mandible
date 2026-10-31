using Godot;

namespace Mandible;

public enum AntType { Worker = 0, Soldier = 1, Major = 2, Queen = 3 }

public enum AntState { Idle, Forage, Gather, Return, Move, AttackMove, Engage, Hold }

public enum ProdKind { Worker, Soldier, Major, Chamber }

public enum Difficulty { Easy, Normal, Hard }

public struct UnitStats
{
    public string Name;
    public float Hp, Damage, Cooldown, Speed, Reach, Aggro, Sight, Size, Cost, BuildTime, CorpseFood;
    public int Pop;
}

public static class Config
{
    public const float MapSize = 320f;
    public const int MaxAnts = 3000;
    public const int Player = 0;
    public const int Enemy = 1;

    public const float StartFood = 250f;
    public const int StartPopCap = 60;
    public const int ChamberPop = 50;
    public const int MaxPopCap = 460;
    public const float ChamberCost = 100f;
    public const float ChamberTime = 12f;
    public const int QueueLimit = 6;

    public static readonly UnitStats[] Stats =
    {
        new() { Name = "Worker", Hp = 24, Damage = 2.2f, Cooldown = 0.7f, Speed = 7.2f, Reach = 0.9f, Aggro = 4.5f, Sight = 11, Size = 1.0f, Cost = 15, BuildTime = 1.6f, CorpseFood = 3, Pop = 1 },
        new() { Name = "Soldier", Hp = 72, Damage = 6.5f, Cooldown = 0.6f, Speed = 7.6f, Reach = 1.25f, Aggro = 13, Sight = 13, Size = 1.45f, Cost = 35, BuildTime = 3f, CorpseFood = 6, Pop = 1 },
        new() { Name = "Major", Hp = 270, Damage = 19f, Cooldown = 0.9f, Speed = 5.2f, Reach = 1.8f, Aggro = 13, Sight = 14, Size = 2.15f, Cost = 100, BuildTime = 6.5f, CorpseFood = 18, Pop = 3 },
        new() { Name = "Queen", Hp = 3200, Damage = 30f, Cooldown = 0.7f, Speed = 0f, Reach = 3.2f, Aggro = 7, Sight = 30, Size = 3.4f, Cost = 0, BuildTime = 0, CorpseFood = 80, Pop = 0 },
    };

    public static UnitStats Of(AntType t) => Stats[(int)t];

    public static float ProdCost(ProdKind k) => k switch
    {
        ProdKind.Worker => Stats[0].Cost,
        ProdKind.Soldier => Stats[1].Cost,
        ProdKind.Major => Stats[2].Cost,
        _ => ChamberCost,
    };

    public static float ProdTime(ProdKind k) => k switch
    {
        ProdKind.Worker => Stats[0].BuildTime,
        ProdKind.Soldier => Stats[1].BuildTime,
        ProdKind.Major => Stats[2].BuildTime,
        _ => ChamberTime,
    };

    public static string ProdName(ProdKind k) => k == ProdKind.Chamber ? "Excavate Chamber" : Stats[(int)k].Name;

    public static readonly Color PlayerUi = new(1.0f, 0.78f, 0.29f);
    public static readonly Color EnemyUi = new(1.0f, 0.27f, 0.18f);
    public static Color TeamUi(int team) => team == Player ? PlayerUi : EnemyUi;
}

/// Small fast deterministic RNG (xorshift).
public sealed class Rng
{
    uint _s;
    public Rng(uint seed) { _s = seed == 0 ? 0x9E3779B9u : seed; }
    public uint NextU() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s; }
    public float F() => (NextU() & 0xFFFFFF) / 16777216f;
    public float R(float a, float b) => a + (b - a) * F();
    public int I(int a, int b) => a + (int)(NextU() % (uint)(b - a));
    public Vector2 InDisk(float r) { float a = R(0, Mathf.Tau); float d = Mathf.Sqrt(F()) * r; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d; }
}

public static class MathX
{
    public static float AngleDiff(float from, float to)
    {
        float d = (to - from) % Mathf.Tau;
        if (d > Mathf.Pi) d -= Mathf.Tau;
        if (d < -Mathf.Pi) d += Mathf.Tau;
        return d;
    }

    public static Vector2 Dir(float a) => new(Mathf.Cos(a), Mathf.Sin(a));
}
