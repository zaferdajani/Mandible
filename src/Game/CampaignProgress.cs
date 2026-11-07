using Godot;

namespace Mandible;

/// The level map's saved progress, kept in user://campaign.cfg beside the settings: per level whether it is won,
/// the stars it earned (each kept on its own, so they can be collected over several replays) and the best time,
/// plus the colony colour and emblem the player chose. A level is playable once the one before it on the map is
/// won; any playable level can be replayed. Levels are keyed by their scenario id (Levels.Order maps them).
public static class CampaignProgress
{
    static string _path = "user://campaign.cfg";
    static readonly bool[] _done = new bool[Levels.Count + 1];
    static readonly int[] _stars = new int[Levels.Count + 1];
    static readonly float[] _best = new float[Levels.Count + 1];
    static readonly bool[] _story = new bool[Levels.Count + 1];
    static readonly string[] _roster = new string[Levels.Count + 1];   // the colony as it enters each chapter
    static int _colour, _emblem;
    static bool _loaded, _openModes;

    /// Tests point the store at a scratch file so a player's real progress is never touched.
    public static void UsePath(string path) { _path = path; _loaded = false; }

    static bool Valid(int id) => id >= 1 && id <= Levels.Count;
    public static bool Completed(int id) { Ensure(); return Valid(id) && _done[id]; }
    public static bool Unlocked(int id)
    {
        int n = Levels.NumberOf(id);
        // a chapter won before new chapters were added before it stays open
        return n == 1 || n > 1 && Completed(Levels.IdAt(n - 1)) || Completed(id);
    }
    /// The furthest chapter this save has opened.
    public static int Frontier { get { int n = 1; for (int k = 2; k <= Levels.Count; k++) if (Unlocked(Levels.IdAt(k))) n = k; return n; } }

    /// Whether the player has met a caste or bug, for the Field Guide: the campaign reveals them a chapter at a time
    /// (Reveal); a Skirmish or Lane War game shows them all.
    public static bool Met(AntType t) { Ensure(); return _openModes || Reveal.Creature(t) <= Frontier; }

    /// The red fire ants as a colony: met at the start of Act II, or in any Skirmish or Lane War.
    public static bool MetRedColony { get { Ensure(); return _openModes || Frontier >= Reveal.RedColony; } }

    /// A star unlock's name, kept secret while it would name a caste or hero the player has not met yet.
    public static string UnlockName(Levels.Unlock u)
    {
        var type = u.Perk switch { Perk.Matron => AntType.Matron, Perk.Marshal => AntType.Marshal, Perk.Gunner => AntType.Gunner, Perk.Flyer => AntType.Flyer, _ => AntType.Worker };
        return Met(type) ? u.Name : $"a {u.Kind.ToLowerInvariant()} not yet met";
    }

    /// Evolution by eating, met in its chapter (or any Skirmish or Lane War): the Field Guide's Feeding page.
    public static bool MetEvolution { get { Ensure(); return _openModes || Frontier >= Reveal.Evolution; } }

    /// The mutation track (mutagen, the sixth sense, mutations), met in the Silk Trap: the Field Guide's Mutation page.
    public static bool MetMutation { get { Ensure(); return Frontier >= Reveal.Mutagen; } }

    public static void MetEverything() { Ensure(); if (_openModes) return; _openModes = true; Save(); }

    /// The level's stars as bits: 1 the win, 2 and 4 its optional goals.
    public static int StarBits(int id) { Ensure(); return Valid(id) ? _stars[id] : 0; }
    public static int StarCount(int id) { int b = StarBits(id), n = 0; for (int i = 0; i < 3; i++) if ((b & 1 << i) != 0) n++; return n; }
    public static float Best(int id) { Ensure(); return Valid(id) ? _best[id] : 0; }
    public static int TotalStars { get { int n = 0; for (int id = 1; id <= Levels.Count; id++) n += StarCount(id); return n; } }
    public static Perk Perks => Levels.PerksFor(TotalStars);

    /// The level "Continue" starts: the first playable one on the map not yet won, or 0 once all are.
    public static int Next
    {
        get
        {
            for (int n = 1; n <= Levels.Count; n++)
            {
                int id = Levels.IdAt(n);
                if (Unlocked(id) && !Completed(id)) return id;
            }
            return 0;
        }
    }

    public static int CompletedCount { get { int n = 0; for (int id = 1; id <= Levels.Count; id++) if (Completed(id)) n++; return n; } }

    /// A win: the level is complete, any new stars join the ones it already had, and a faster time is kept.
    /// Returns the stars that are new (as bits).
    public static int Record(int id, int stars, float time)
    {
        Ensure();
        if (!Valid(id)) return 0;
        stars |= 1;
        int fresh = stars & ~_stars[id];
        _done[id] = true;
        _stars[id] |= stars;
        if (time > 0 && (_best[id] <= 0 || time < _best[id])) _best[id] = time;
        Save();
        return fresh;
    }

    public static void Complete(int id) => Record(id, 1, 0);

    /// The colony that starts a chapter: whoever walked out of the chapter before it. Null until the chapter before
    /// it has been won with this save (Chapter 1 always starts from the broken tube).
    public static ColonyRoster RosterFor(int id) { Ensure(); return Valid(id) ? ColonyRoster.Load(_roster[id]) : null; }

    /// A chapter was won: its survivors walk into the next chapter. Replaying an earlier chapter never rewrites the
    /// colony of a chapter already won, so the save's frontier is the only place the colony moves forward.
    /// A lost chapter records nothing: the retry starts from the same colony.
    public static void CarryRoster(int id, ColonyRoster survivors)
    {
        Ensure();
        int next = Levels.IdAt(Levels.NumberOf(id) + 1);
        if (next <= 0 || survivors == null || Completed(next)) return;
        _roster[next] = survivors.Save();
        Save();
    }

    /// The result card's mutation choice: the colony walking into the next chapter takes it (one mutagen). Like the
    /// roster itself, a chapter already won keeps the colony it began with.
    public static bool ChooseMutation(int id, ColonyRoster survivors, Mutation m)
    {
        if (survivors == null || !survivors.Take(m)) return false;
        CarryRoster(id, survivors);
        return true;
    }

    /// The comic beat before a level plays once by itself; the map replays it on request.
    public static bool StorySeen(int id) { Ensure(); return Valid(id) && _story[id]; }
    public static void SeeStory(int id) { Ensure(); if (!Valid(id) || _story[id]) return; _story[id] = true; Save(); }

    // ------------------------------------------------------------------ the colony's look
    public static int Colour { get { Ensure(); return _colour; } }
    public static int Emblem { get { Ensure(); return _emblem; } }
    public static bool ColourOpen(int c) => c == 0 || System.Array.Exists(Levels.Unlocks, u => u.Colour == c && TotalStars >= u.Stars);
    public static bool EmblemOpen(int e) => e == 0 || System.Array.Exists(Levels.Unlocks, u => u.Emblem == e && TotalStars >= u.Stars);
    public static void Choose(int colour, int emblem)
    {
        Ensure();
        if (colour >= 0 && colour < Levels.Colours.Length && ColourOpen(colour)) _colour = colour;
        if (emblem >= 0 && emblem < Levels.EmblemNames.Length && EmblemOpen(emblem)) _emblem = emblem;
        Save();
    }

    /// The black colony wears the chosen colour on its crests, its selection rings and the minimap.
    public static void ApplyLook(bool playerIsBlack)
    {
        var c = playerIsBlack ? Levels.Colours[Colour] : Levels.Colours[0];
        AntRenderer.Accent[0] = c;
        Config.PlayerUi = playerIsBlack && Colour != 0 ? c.Lightened(0.15f) : Config.DefaultPlayerUi;
    }

    public static void Reset()
    {
        System.Array.Clear(_done); System.Array.Clear(_stars); System.Array.Clear(_best); System.Array.Clear(_story); System.Array.Clear(_roster);
        _colour = _emblem = 0; _openModes = false;
        _loaded = true;
        Save();
    }

    static void Ensure() { if (!_loaded) Load(); }

    public static void Load()
    {
        _loaded = true;
        System.Array.Clear(_done); System.Array.Clear(_stars); System.Array.Clear(_best); System.Array.Clear(_story); System.Array.Clear(_roster);
        _colour = _emblem = 0; _openModes = false;
        var cfg = new ConfigFile();
        if (cfg.Load(_path) != Error.Ok) return;
        for (int id = 1; id <= Levels.Count; id++)
        {
            // progress saved before the level map (0.10 / PR #6): "episodeN" in [campaign]
            bool old = id <= 4 && (bool)cfg.GetValue("campaign", $"episode{id}", false);
            _done[id] = (bool)cfg.GetValue("levels", $"level{id}", old);
            _stars[id] = (int)cfg.GetValue("levels", $"stars{id}", _done[id] ? 1 : 0);
            _best[id] = (float)cfg.GetValue("levels", $"best{id}", 0f);
            _story[id] = (bool)cfg.GetValue("story", $"seen{id}", false);
            _roster[id] = (string)cfg.GetValue("colony", $"enter{id}", "");
        }
        _colour = (int)cfg.GetValue("colony", "colour", 0);
        _emblem = (int)cfg.GetValue("colony", "emblem", 0);
        _openModes = (bool)cfg.GetValue("guide", "open_modes", false);
        if (!ColourOpen(_colour)) _colour = 0;
        if (!EmblemOpen(_emblem)) _emblem = 0;
    }

    public static void Save()
    {
        var cfg = new ConfigFile();
        for (int id = 1; id <= Levels.Count; id++)
        {
            cfg.SetValue("levels", $"level{id}", _done[id]);
            cfg.SetValue("levels", $"stars{id}", _stars[id]);
            cfg.SetValue("levels", $"best{id}", _best[id]);
            cfg.SetValue("story", $"seen{id}", _story[id]);
            if (!string.IsNullOrEmpty(_roster[id])) cfg.SetValue("colony", $"enter{id}", _roster[id]);
        }
        cfg.SetValue("colony", "colour", _colour);
        cfg.SetValue("colony", "emblem", _emblem);
        if (_openModes) cfg.SetValue("guide", "open_modes", true);
        cfg.Save(_path);
    }
}
