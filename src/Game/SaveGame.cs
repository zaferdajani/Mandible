using System.Collections.Generic;
using System.IO;
using Godot;

namespace Mandible;

/// Wraps a single-player game's order channel and keeps every order with the tick it was given on.
/// The simulation is deterministic, so the match setup plus this log is the whole game: replaying it
/// rebuilds the exact same state (the AI is not logged, it decides again from the same state).
public sealed class RecordingOrders : IOrders
{
    readonly Sim _sim;
    readonly IOrders _inner;
    public readonly List<(int Tick, Cmd Cmd)> Log = new();

    public RecordingOrders(Sim sim, IOrders inner) { _sim = sim; _inner = inner; }

    public void Issue(Cmd c)
    {
        Log.Add((_sim.Ticks, c));
        _inner.Issue(c);
    }
}

/// Everything needed to rebuild a single-player game: how it was set up, and the orders given since.
public sealed class SaveData
{
    public const int FormatVersion = 1;
    public string Slot, Label;
    public long SavedAtUnix;
    public string GameVersion = Updater.Version;
    public GameMode Mode;
    public int Episode, Team, Ticks;
    public uint Seed, Checksum;
    public Difficulty Diff;
    public bool Skirmish, Fortified;
    public AntType Hero0, Hero1;
    public string Roster = "";   // the campaign colony that entered the level (persistent colony)
    public int Perks;
    public Vector2 CameraFocus;
    public float CameraDistance;
    public readonly List<(int Tick, Cmd Cmd)> Orders = new();

    public string When => System.DateTimeOffset.FromUnixTimeSeconds(SavedAtUnix).ToLocalTime().ToString("d MMM, HH:mm");
    public string Clock => $"{Ticks / 30 / 60}:{Ticks / 30 % 60:00}";
}

/// Save slots: "auto" (milestones only) and three manual slots.
public static class SaveGame
{
    const string Dir = "user://saves";
    public static readonly string[] Manual = { "1", "2", "3" };
    public const string Auto = "auto";

    static string PathOf(string slot) => $"{Dir}/slot_{slot}.sav";

    /// What the game's title looks like on a save card.
    public static string Describe(GameSession s) =>
        s.Campaign ? $"Level {Levels.NumberOf(s.Episode)} · {Levels.Info(s.Episode).Name}" :
        s.Skirmish ? "Skirmish" : s.Mode == GameMode.Lanes ? "Lane War" : $"Colony War · {s.Diff}";

    public static bool Write(GameSession s, string slot)
    {
        if (s.Recorder == null) return false;
        var d = new SaveData
        {
            Slot = slot, Label = Describe(s), SavedAtUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Mode = s.Mode, Episode = s.Episode, Team = Local.Me, Ticks = s.Sim.Ticks, Seed = s.Seed, Checksum = s.Sim.Checksum(),
            Diff = s.Diff, Skirmish = s.Skirmish, Fortified = s.Fortified, Hero0 = s.Heroes[0], Hero1 = s.Heroes[1],
            Roster = s.RosterAtStart ?? "", Perks = s.PerksAtStart,
            CameraFocus = s.Camera.Focus2D, CameraDistance = s.Camera.TargetDistance,
        };
        d.Orders.AddRange(s.Recorder.Log);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Dir));
        string tmp = ProjectSettings.GlobalizePath(PathOf(slot) + ".tmp");
        using (var fs = File.Create(tmp))
        using (var w = new BinaryWriter(fs))
        {
            w.Write("MANDIBLE-SAVE"); w.Write(SaveData.FormatVersion); w.Write(d.GameVersion);
            w.Write(d.Slot); w.Write(d.Label); w.Write(d.SavedAtUnix);
            w.Write((byte)d.Mode); w.Write(d.Episode); w.Write(d.Team); w.Write(d.Ticks); w.Write(d.Seed); w.Write(d.Checksum);
            w.Write((byte)d.Diff); w.Write(d.Skirmish); w.Write(d.Fortified); w.Write((byte)d.Hero0); w.Write((byte)d.Hero1);
            w.Write(d.Roster); w.Write(d.Perks);
            w.Write(d.CameraFocus.X); w.Write(d.CameraFocus.Y); w.Write(d.CameraDistance);
            w.Write(d.Orders.Count);
            foreach (var (tick, cmd) in d.Orders) { w.Write(tick); cmd.Write(w); }
        }
        // replace atomically, so a crash mid-save never leaves a broken slot
        string final = ProjectSettings.GlobalizePath(PathOf(slot));
        if (File.Exists(final)) File.Delete(final);
        File.Move(tmp, final);
        return true;
    }

    public static SaveData Read(string slot)
    {
        string path = ProjectSettings.GlobalizePath(PathOf(slot));
        if (!File.Exists(path)) return null;
        try
        {
            using var fs = File.OpenRead(path);
            using var r = new BinaryReader(fs);
            if (r.ReadString() != "MANDIBLE-SAVE" || r.ReadInt32() != SaveData.FormatVersion) return null;
            var d = new SaveData { GameVersion = r.ReadString(), Slot = r.ReadString(), Label = r.ReadString(), SavedAtUnix = r.ReadInt64() };
            d.Mode = (GameMode)r.ReadByte(); d.Episode = r.ReadInt32(); d.Team = r.ReadInt32(); d.Ticks = r.ReadInt32(); d.Seed = r.ReadUInt32(); d.Checksum = r.ReadUInt32();
            d.Diff = (Difficulty)r.ReadByte(); d.Skirmish = r.ReadBoolean(); d.Fortified = r.ReadBoolean(); d.Hero0 = (AntType)r.ReadByte(); d.Hero1 = (AntType)r.ReadByte();
            d.Roster = r.ReadString(); d.Perks = r.ReadInt32();
            d.CameraFocus = new Vector2(r.ReadSingle(), r.ReadSingle()); d.CameraDistance = r.ReadSingle();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) { int tick = r.ReadInt32(); d.Orders.Add((tick, Cmd.Read(r))); }
            return d;
        }
        catch (System.Exception e) { GD.PushWarning($"[save] slot {slot} unreadable: {e.Message}"); return null; }
    }

    /// Every readable save, newest first.
    public static List<SaveData> All()
    {
        var list = new List<SaveData>();
        foreach (var slot in new[] { Auto, Manual[0], Manual[1], Manual[2] }) { var d = Read(slot); if (d != null) list.Add(d); }
        list.Sort((a, b) => b.SavedAtUnix.CompareTo(a.SavedAtUnix));
        return list;
    }
}
