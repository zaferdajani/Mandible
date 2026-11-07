using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace Mandible;

/// One ant the campaign remembers by name or rank: a tube survivor, a veteran, a hero.
public sealed class Veteran
{
    public AntType Type;
    public int Level = 1, Kills, Joined, Stage;  // Stage: evolution by eating (from Reveal.Evolution)
    public string Name = "";
    public string Title => Name != "" ? Name : $"a {Config.Of(Type).Name.ToLowerInvariant()}";
    public string Line => $"{(Name != "" ? Name : Config.Of(Type).Name)} · {Config.Of(Type).Name} Lv {Level} · {Kills} kills";
}

/// The colony carried from one chapter to the next: whoever survived, the nest it dug and the queen's rank. Losses
/// are permanent. It is applied before the first tick of a chapter (the campaign is single-player), so the
/// simulation stays deterministic.
public sealed class ColonyRoster
{
    public int QueenTier = 1;
    public readonly List<ChamberKind> Chambers = new();
    public readonly int[] Plain = new int[13];          // unnamed rank-1 ants by type
    public readonly List<Veteran> Veterans = new();     // veterans, heroes and named ants
    // the mutation track: mutations taken (Mutation bits), mutagen in the reserve, the colony's level and instability
    public int Mutations, Mutagen, Level = 1, Instability, WebRank;
    public int Total { get { int n = Veterans.Count; foreach (int p in Plain) n += p; return n; } }

    public string Save()
    {
        var sb = new StringBuilder();
        sb.Append(QueenTier).Append('|');
        foreach (var k in Chambers) sb.Append((int)k).Append(',');
        sb.Append('|');
        for (int t = 0; t < Plain.Length; t++) sb.Append(Plain[t]).Append(',');
        sb.Append('|');
        foreach (var v in Veterans) sb.Append((int)v.Type).Append(':').Append(v.Level).Append(':').Append(v.Kills).Append(':').Append(v.Joined).Append(':').Append(v.Name.Replace(";", "").Replace(":", "")).Append(':').Append(v.Stage).Append(';');
        sb.Append('|').Append(Mutations).Append(',').Append(Mutagen).Append(',').Append(Level).Append(',').Append(Instability).Append(',').Append(WebRank);
        return sb.ToString();
    }

    public static ColonyRoster Load(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        var parts = s.Split('|');
        if (parts.Length < 4) return null;
        var r = new ColonyRoster { QueenTier = int.Parse(parts[0]) };
        foreach (var c in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries)) r.Chambers.Add((ChamberKind)int.Parse(c));
        var plain = parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries);
        for (int t = 0; t < Math.Min(plain.Length, r.Plain.Length); t++) r.Plain[t] = int.Parse(plain[t]);
        foreach (var v in parts[3].Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = v.Split(':');
            r.Veterans.Add(new Veteran { Type = (AntType)int.Parse(f[0]), Level = int.Parse(f[1]), Kills = int.Parse(f[2]), Joined = int.Parse(f[3]), Name = f.Length > 4 ? f[4] : "", Stage = f.Length > 5 ? int.Parse(f[5]) : 0 });
        }
        if (parts.Length > 4)
        {
            // saves from before the mutation track have no fifth part: no mutations, level 1
            var mu = parts[4].Split(',');
            if (mu.Length >= 4) { r.Mutations = int.Parse(mu[0]); r.Mutagen = int.Parse(mu[1]); r.Level = int.Parse(mu[2]); r.Instability = int.Parse(mu[3]); }
            if (mu.Length >= 5) r.WebRank = int.Parse(mu[4]);
        }
        return r;
    }

    /// The colony takes a mutation between chapters (the result card's choice): the queen consumes one mutagen.
    public bool Take(Mutation m)
    {
        bool rankUp = m == Mutation.WebThrowing && (Mutations & (int)m) != 0 && WebRank < 3;
        if (Mutagen < 1 || (Mutations & (int)m) != 0 && !rankUp) return false;
        Mutagen--;
        Mutations |= (int)m;
        Instability++;
        if (m == Mutation.WebThrowing) WebRank = System.Math.Max(1, WebRank + (rankUp ? 1 : 0));
        return true;
    }
}

public sealed partial class Expedition
{
    /// The roster this chapter began with, and which of its veterans walk the yard as which ant.
    public ColonyRoster Roster;
    public readonly Dictionary<int, (int serial, Veteran vet)> Tracked = new();
    public readonly List<Veteran> Fallen = new();
    public int Reinforcements;
}

public sealed partial class Sim
{
    /// Names the colony gives the veterans it comes to rely on (in the order they earn them).
    static readonly string[] VeteranNames =
    {
        "Sorrel", "Flint", "Bramble", "Tansy", "Rook", "Cobble", "Wren", "Thorn", "Sedge", "Burdock", "Quill", "Bracken",
        "Nettle", "Grist", "Clove", "Teasel", "Pip", "Sloe", "Hazel", "Rush",
    };

    /// Builds the chapter's colony from the saved roster, before the first tick. Chapter 1 (the broken tube) always
    /// starts from the tube's survivors, with old Burr named among them. The level's own muster sets the floor: no
    /// carried colony is ever weaker than 60% of it (fresh brood reinforces the gaps), so no save reaches a dead end;
    /// a strong colony carries at most half again as many.
    public void ApplyRoster(ColonyRoster roster, int chapter)
    {
        var m = Mission;
        if (m == null) return;
        var home = W.Nests[0].Pos;
        if (roster == null)
        {
            if (chapter == 1)
            {
                // the tube's eldest soldier: the one nearest the queen
                int burr = -1;
                for (int i = 0; i < High; i++)
                    if (Ants[i].Alive && Ants[i].Team == 0 && Ants[i].Type == AntType.Soldier && (burr < 0 || Ants[i].Pos.DistanceTo(Ants[Col[0].QueenId].Pos) < Ants[burr].Pos.DistanceTo(Ants[Col[0].QueenId].Pos))) burr = i;
                if (burr >= 0) m.Tracked[burr] = (Ants[burr].Serial, new Veteran { Type = AntType.Soldier, Level = 1, Joined = 1, Name = "Burr" });
            }
            m.Roster = new ColonyRoster();
            ApplyMutationState(null, chapter);
            return;
        }
        m.Roster = roster;
        var c = Col[0];
        // what the level musters for this chapter, by caste: the floor and ceiling for the carried colony
        var design = new int[13];
        var levelAnts = new List<int>();
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (!a.Alive || a.Team != 0 || a.Type == AntType.Queen || a.Guardian || i == m.EscortId) continue;
            design[(int)a.Type]++; levelAnts.Add(i);
        }
        var toCentre = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - home).Normalized();
        Vector2 Muster(AntType t) => OpenGround(home + toCentre * (t == AntType.Worker ? 0 : t == AntType.Major ? 12 : 9) + Rng.InDisk(t == AntType.Worker ? 5 : 3.5f));
        // keep the level's ants where they stand (for ambush positions and episode scripts) and swap their identity
        var have = new int[13];
        foreach (var v in roster.Veterans) have[(int)v.Type]++;
        for (int t = 0; t < 13; t++) have[t] += roster.Plain[t];
        var plainLeft = (int[])roster.Plain.Clone();
        var vets = new List<Veteran>(roster.Veterans);
        // heroes and veterans first, strongest first, so a ceiling trims the rank and file
        vets.Sort((x, y) => y.Level.CompareTo(x.Level));
        var placed = new int[13];
        var byLevel = new Dictionary<(AntType, int), List<int>>();
        var fresh = new List<int>();
        int Cap(int t) => design[t] == 0 ? 12 : design[t] * 3 / 2 + 6;
        int Floor(int t) => (design[t] * 6 + 9) / 10;
        foreach (int id in levelAnts) { if (Ants[id].SquadP1 > 0) Squads[Ants[id].SquadP1 - 1].Members.Remove(id); Remove(id); }
        foreach (var v in vets)
        {
            int t = (int)v.Type;
            if (placed[t] >= Cap(t)) continue;
            if (Config.IsHero(v.Type) && c.Count[t] > 0) continue;
            int id = Spawn(0, v.Type, Muster(v.Type), toCentre.AngleD(), v.Level);
            if (id < 0) continue;
            if (v.Stage > 0) { Ants[id].Stage = Math.Min(v.Stage, Config.MaxStage); ApplyLevel(ref Ants[id]); Ants[id].Hp = Ants[id].MaxHp; }
            Ants[id].State = v.Type == AntType.Worker ? AntState.Forage : AntState.Idle;
            m.Tracked[id] = (Ants[id].Serial, v);
            placed[t]++;
            if (Config.InSquads(v.Type)) { if (!byLevel.TryGetValue((v.Type, v.Level), out var l)) byLevel[(v.Type, v.Level)] = l = new List<int>(); l.Add(id); }
        }
        for (int t = 0; t < 13; t++)
        {
            var type = (AntType)t;
            if (type == AntType.Queen) continue;
            int want = Math.Min(Cap(t), placed[t] + plainLeft[t]);
            int floor = Floor(t);
            if (want < floor) { m.Reinforcements += floor - want; want = floor; }
            for (int k = placed[t]; k < want; k++)
            {
                int id = Spawn(0, type, Muster(type), toCentre.AngleD());
                if (id < 0) break;
                Ants[id].State = type == AntType.Worker ? AntState.Forage : AntState.Idle;
                fresh.Add(id);
            }
        }
        // squads of the same rank stay together; new brood forms its own
        foreach (var ((type, level), ids) in byLevel)
        {
            Squad s = null;
            foreach (int id in ids)
            {
                if (s == null || s.Count >= s.Size) { s = NewSquad(0, type); s.Level = level; s.Xp = Config.SquadXp(level); }
                Join(s, id);
            }
        }
        FormSquads(fresh);
        // the queen keeps her rank, the nest keeps its chambers
        if (roster.QueenTier > c.T(Upgrade.Queen))
        {
            c.Tier[(int)Upgrade.Queen] = roster.QueenTier;
            c.Chambers[0].Tier = roster.QueenTier;
            ApplyLevel(ref Ants[c.QueenId]); Ants[c.QueenId].Hp = Ants[c.QueenId].MaxHp;
        }
        if (c.Founded)
        {
            var want = new Dictionary<ChamberKind, int>();
            foreach (var k in roster.Chambers) want[k] = want.GetValueOrDefault(k) + 1;
            foreach (var (kind, n) in want)
            {
                if (kind == ChamberKind.Royal) continue;
                for (int i = ChamberCount(0, kind, true); i < n && c.PopCap + Config.ChamberPop <= Config.MaxPopCap; i++)
                    AddChamber(0, kind, kind == ChamberKind.Storage ? StorageWork : BroodWork, true);
            }
            RecountCapacity(c);
        }
        c.PopUsed = 0; for (int t = 0; t < c.Count.Length; t++) c.PopUsed += c.Count[t] * Config.Stats[t].Pop;
        ApplyMutationState(roster, chapter);
    }

    /// Quietly takes an ant off the board (no death event, no corpse): used only while setting up a chapter.
    void Remove(int id)
    {
        ref var a = ref Ants[id];
        if (!a.Alive) return;
        a.Alive = false;
        if (a.Team < 2) Col[a.Team].Count[(int)a.Type]--;
        a.SquadP1 = 0;
        _free.Push(id);
    }

    /// Called from Kill: a tracked veteran's death is remembered; a tracked killer's tally grows.
    void RosterKill(int victim, int killer)
    {
        var m = Mission;
        if (m == null || m.Tracked.Count == 0) return;
        if (killer >= 0 && m.Tracked.TryGetValue(killer, out var k) && k.serial == Ants[killer].Serial) k.vet.Kills++;
        if (m.Tracked.TryGetValue(victim, out var v) && v.serial == Ants[victim].Serial) { m.Fallen.Add(v.vet); m.Tracked.Remove(victim); }
    }

    /// Tests: kills an ant as a fight would (death event, corpse, roster bookkeeping).
    public void Slay(int id, int killer = -1) { if (id >= 0 && id < High && Ants[id].Alive) Kill(id, killer); }

    /// The colony that walks out of a won chapter: every survivor, the chambers it holds and the queen's rank.
    /// Ants of rank 2 and above are remembered as veterans; heroes and rank 3 earn names.
    public ColonyRoster HarvestRoster(int chapter)
    {
        var m = Mission;
        var c0 = Col[0];
        var r = new ColonyRoster { QueenTier = c0.T(Upgrade.Queen), Mutations = c0.Mutations, Mutagen = c0.Mutagen, Level = c0.Level, Instability = c0.Instability, WebRank = c0.WebRank };
        // a dead queen's body holds mutagen, and every queen or nest master the colony defeats raises its level
        if (m != null && Winner == Config.Player && !Col[1].Alive && m.EndsWithQueen && chapter >= Reveal.FirstQueenKill) { r.Mutagen++; r.Level++; }
        if (m != null) r.Level += m.MastersSlain;
        foreach (var ch in Col[0].Chambers) if (ch.Built) r.Chambers.Add(ch.Kind);
        int named = 0;
        if (m?.Roster != null) foreach (var v in m.Roster.Veterans) if (v.Name != "" && Array.IndexOf(VeteranNames, v.Name) >= 0) named++;
        foreach (var (_, (_, v)) in m?.Tracked ?? new()) if (v.Name != "" && Array.IndexOf(VeteranNames, v.Name) >= 0) named++;
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (!a.Alive || a.Team != 0 || a.Type == AntType.Queen || a.Guardian) continue;
            if (m != null && m.Tracked.TryGetValue(i, out var t) && t.serial == a.Serial)
            {
                t.vet.Level = a.Level; t.vet.Type = a.Type; t.vet.Stage = a.Stage;
                r.Veterans.Add(t.vet);
                continue;
            }
            bool hero = Config.IsHero(a.Type);
            if (a.Level >= 2 || a.Stage >= 1 || hero)
            {
                // heroes come out of the royal cocoon: the first is Hemlock (old Burr stays the soldier he always was)
                string name = hero ? a.Type switch { AntType.Matron => "Vetch", AntType.Marshal => "Kestrel", _ => "Hemlock" } : a.Level >= 3 || a.Stage >= 2 ? VeteranNames[named++ % VeteranNames.Length] : "";
                if (hero && m != null && m.Fallen.Exists(f => f.Name == name)) name = ""; // the named hero fell; this one is new
                var v = new Veteran { Type = a.Type, Level = a.Level, Joined = chapter, Name = name, Stage = a.Stage };
                r.Veterans.Add(v);
            }
            else r.Plain[(int)a.Type]++;
        }
        return r;
    }
}
