using Godot;

namespace Mandible;

/// Completed campaign episodes, kept in user://campaign.cfg beside the settings. An episode is
/// playable once the one before it is complete; any playable episode can be replayed.
public static class CampaignProgress
{
    static string _path = "user://campaign.cfg";
    static readonly bool[] _done = new bool[CampaignEpisodes.Count + 1];
    static bool _loaded;

    /// Tests point the store at a scratch file so a player's real progress is never touched.
    public static void UsePath(string path) { _path = path; _loaded = false; }

    public static bool Completed(int ep) { Ensure(); return ep >= 1 && ep <= CampaignEpisodes.Count && _done[ep]; }
    public static bool Unlocked(int ep) => ep == 1 || ep >= 2 && ep <= CampaignEpisodes.Count && Completed(ep - 1);

    /// The episode "Continue" starts: the first playable one not yet complete, or 0 once all are done.
    public static int Next
    {
        get
        {
            for (int ep = 1; ep <= CampaignEpisodes.Count; ep++)
                if (Unlocked(ep) && !Completed(ep)) return ep;
            return 0;
        }
    }

    public static int CompletedCount { get { int n = 0; for (int ep = 1; ep <= CampaignEpisodes.Count; ep++) if (Completed(ep)) n++; return n; } }

    public static void Complete(int ep)
    {
        Ensure();
        if (ep < 1 || ep > CampaignEpisodes.Count || _done[ep]) return;
        _done[ep] = true;
        Save();
    }

    public static void Reset()
    {
        System.Array.Clear(_done);
        _loaded = true;
        Save();
    }

    static void Ensure() { if (!_loaded) Load(); }

    public static void Load()
    {
        _loaded = true;
        System.Array.Clear(_done);
        var cfg = new ConfigFile();
        if (cfg.Load(_path) != Error.Ok) return;
        for (int ep = 1; ep <= CampaignEpisodes.Count; ep++)
            _done[ep] = (bool)cfg.GetValue("campaign", $"episode{ep}", false);
    }

    public static void Save()
    {
        var cfg = new ConfigFile();
        for (int ep = 1; ep <= CampaignEpisodes.Count; ep++) cfg.SetValue("campaign", $"episode{ep}", _done[ep]);
        cfg.Save(_path);
    }
}
