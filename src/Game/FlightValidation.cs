using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The owner's flight rules, checked headless: a wasp in the air is out of every ground ant's reach (acid gunners
/// included), flying ants reach it, a ground ant reaches it when it comes down to sting or when thrown silk pins it,
/// Web Throwing pins it; and no wasp starts near a nest in any mode, while the middle prize changes from map to map.
public static class FlightValidation
{
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    public static void Run(Main app)
    {
        var root = new Node3D(); app.AddChild(root);
        try
        {
            Reach(root);
            StartAreas(root);
        }
        finally { root.QueueFree(); }
        Campaign(app);
    }

    static Sim Quiet(Node3D root, uint seed = 20261031)
    {
        var s = new Sim(WorldBuilder.Build(root, seed));
        for (int i = 0; i < s.High; i++) if (s.Ants[i].Alive && s.Ants[i].Team == Config.Wild) s.Slay(i); // an empty yard for the duel
        s.W.Food.Clear();
        for (int i = 0; i < 2; i++) s.Tick(1f / 30);
        return s;
    }

    static void Run(Sim s, int frames) { for (int k = 0; k < frames; k++) s.Tick(1f / 30); }

    static void Reach(Node3D root)
    {
        // a wasp hanging in the air: ground ants cannot target it, bite it or be ordered onto it
        var s = Quiet(root);
        var p = new Vector2(160, 160);
        int wasp = s.Spawn(Config.Wild, AntType.Wasp, p, 0);
        s.Ants[wasp].State = AntState.Hold; s.Ants[wasp].Alt = Config.CruiseAlt(AntType.Wasp);
        int soldier = s.Spawn(0, AntType.Soldier, p + new Vector2(1, 0), 0);
        int gunner = s.Spawn(0, AntType.Gunner, p + new Vector2(-3, 0), 0);
        s.Ants[soldier].State = AntState.Hold; s.Ants[gunner].State = AntState.Hold;
        Check(s.Airborne(s.Ants[wasp]), "A hovering wasp is not airborne");
        Check(s.FindEnemy(s.Ants[soldier].Pos, 0, 12, false, 0, false) != wasp, "A ground ant found a wasp in the air");
        Check(s.FindEnemy(s.Ants[gunner].Pos, 0, 12, true, 0, false) != wasp, "An acid gunner found a wasp in the air");
        s.CmdAttack(new List<int> { soldier, gunner }, wasp);
        Check(s.Ants[soldier].Target != wasp && s.Ants[gunner].Target != wasp, "Ground ants were ordered onto a wasp in the air");
        float hp = s.Ants[wasp].Hp;
        Run(s, 150);
        Check(s.Ants[wasp].Alive && s.Ants[wasp].Hp >= hp, $"Ground ants hurt a wasp in the air ({hp:0} -> {s.Ants[wasp].Hp:0})");
        GD.Print("[flight-test] PASS ground ants and acid gunners cannot target, be ordered onto or hurt a wasp in the air");
        // a flying ant reaches it
        int flyer = s.Spawn(0, AntType.Flyer, p + new Vector2(0, 1), 0);
        s.CmdAttack(new List<int> { flyer }, wasp);
        Check(s.Ants[flyer].Target == wasp, "A flying ant was refused a wasp");
        Run(s, 240);
        Check(!s.Ants[wasp].Alive || s.Ants[wasp].Hp < hp, "A flying ant could not hurt a wasp");
        GD.Print("[flight-test] PASS flying ants reach and hurt wasps");
        // pinned by silk: every ant can bite it
        s = Quiet(root);
        wasp = s.Spawn(Config.Wild, AntType.Wasp, p, 0);
        s.Ants[wasp].State = AntState.Hold; s.Ants[wasp].PinT = 6f;
        soldier = s.Spawn(0, AntType.Soldier, p + new Vector2(1, 0), 0);
        Run(s, 20);
        Check(!s.Airborne(s.Ants[wasp]), "A pinned wasp is still airborne");
        s.CmdAttack(new List<int> { soldier }, wasp);
        hp = s.Ants[wasp].Hp;
        Run(s, 90);
        Check(!s.Ants[wasp].Alive || s.Ants[wasp].Hp < hp, "A ground ant could not hurt a pinned wasp");
        GD.Print("[flight-test] PASS a wasp pinned by silk falls within every ant's reach");
        // the sting: a wasp that comes down to sting can be bitten in that moment
        s = Quiet(root);
        wasp = s.Spawn(Config.Wild, AntType.Wasp, p, 0, 2);
        var guard = new List<int>();
        for (int k = 0; k < 6; k++) guard.Add(s.Spawn(0, AntType.Soldier, p + MathX.Dir(k) * 2.5f, 0));
        foreach (int g in guard) s.Ants[g].State = AntState.Hold;
        hp = s.Ants[wasp].Hp;
        s.CmdAttack(new List<int> { wasp }, guard[0]);
        float low = hp;
        for (int k = 0; k < 30 * 25 && s.Ants[wasp].Alive; k++) { s.Tick(1f / 30); low = Mathf.Min(low, s.Ants[wasp].Hp); } // a wild wasp mends once it stops fighting
        Check(!s.Ants[wasp].Alive || low < hp, "Soldiers never caught a wasp as it came down to sting");
        GD.Print($"[flight-test] PASS soldiers bite a wasp only when it comes down to sting (its health fell to {100 * low / hp:0}%)");
        // Web Throwing: a soldier of a colony with the mutation pins the wasp, which comes down
        s = Quiet(root);
        s.Col[0].Mutations |= (int)Mutation.WebThrowing; s.Col[0].WebRank = 1;
        wasp = s.Spawn(Config.Wild, AntType.Wasp, p, 0);
        s.Ants[wasp].State = AntState.Hold; s.Ants[wasp].Alt = Config.CruiseAlt(AntType.Wasp);
        soldier = s.Spawn(0, AntType.Soldier, p + new Vector2(4, 0), 0);
        s.Ants[soldier].State = AntState.Hold;
        bool pinned = false;
        for (int k = 0; k < 60 && !pinned; k++) { s.Tick(1f / 30); pinned = s.Ants[wasp].PinT > 0; }
        Check(pinned, "A web-throwing soldier did not pin a wasp in reach");
        Run(s, 20);
        Check(!s.Airborne(s.Ants[wasp]), "The pinned wasp did not come down");
        GD.Print($"[flight-test] PASS Web Throwing (rank 1) pins a wasp for {Mutations.PinTime(1):0.#} s within {Mutations.WebRange(1):0} units");
    }

    static void StartAreas(Node3D root)
    {
        var prizes = new HashSet<string>();
        foreach (uint seed in new uint[] { 20261031, 7, 99, 4242, 123456, 2026 })
        {
            var s = new Sim(WorldBuilder.Build(root, seed));
            float near = float.MaxValue;
            for (int i = 0; i < s.High; i++)
                if (s.Ants[i].Alive && Config.Predator(s.Ants[i].Type))
                    near = Mathf.Min(near, Mathf.Min(s.Ants[i].Pos.DistanceTo(s.W.Nests[0].Pos), s.Ants[i].Pos.DistanceTo(s.W.Nests[1].Pos)));
            Check(near >= Config.WaspFloor - 6, $"Seed {seed}: a wasp starts {near:0} from a nest (floor {Config.WaspFloor:0})");
            FoodSource prize = null;
            foreach (var f in s.W.Food) if (f.Pos.DistanceTo(new Vector2(Config.MapSize, Config.MapSize) * 0.5f) < 40 && (prize == null || f.Max > prize.Max)) prize = f;
            Check(prize != null, $"Seed {seed}: no prize in the middle");
            // fair as a whole: the middle's food, weighted by amount, lies as close to one nest as to the other
            float bias = 0, total = 0;
            foreach (var f in s.W.Food)
                if (f.Pos.DistanceTo(new Vector2(Config.MapSize, Config.MapSize) * 0.5f) < 40) { bias += f.Max * (f.Pos.DistanceTo(s.W.Nests[0].Pos) - f.Pos.DistanceTo(s.W.Nests[1].Pos)); total += f.Max; }
            Check(Mathf.Abs(bias / total) < 1f, $"Seed {seed}: the middle's food favours one nest by {bias / total:0.0}");
            prizes.Add($"{prize.Kind}@{(int)prize.Pos.X}");
        }
        Check(prizes.Count >= 3, $"The middle prize is the same on every map ({string.Join(", ", prizes)})");
        GD.Print($"[flight-test] PASS start areas: no wasp within {Config.WaspFloor - 6:0} of a nest on 6 maps; middle prizes vary ({string.Join(", ", prizes)}) and stay fair");
    }

    static void Campaign(Main app)
    {
        int checkedLevels = 0;
        for (int n = 2; n <= Levels.Count; n++)
        {
            int id = Levels.IdAt(n);
            var s = LevelValidation.Open(app, id, out _);
            var home = s.W.Nests[0].Pos;
            for (int i = 0; i < s.High; i++)
                if (s.Ants[i].Alive && s.Ants[i].Team == Config.Wild && Config.Predator(s.Ants[i].Type))
                {
                    Check(n >= Reveal.Creature(AntType.Wasp), $"Chapter {n}: a wasp before their chapter");
                    Check(s.Ants[i].Pos.DistanceTo(home) >= 100, $"Chapter {n}: a wasp starts {s.Ants[i].Pos.DistanceTo(home):0} from the nest");
                }
            LevelValidation.Close();
            checkedLevels++;
        }
        GD.Print($"[flight-test] PASS campaign: no wasp before Chapter {Reveal.Creature(AntType.Wasp)} and none within 100 of the nest in {checkedLevels} chapters");
    }
}
