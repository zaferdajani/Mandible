using System.Collections.Generic;
using Godot;

namespace Mandible;

/// A dry grass straw on the pond shore (level 1's teamwork lesson). It is far longer than an ant: it moves only while
/// at least Expedition.StrawCrew workers hold it, and every extra pair of jaws carries it faster. Laid at the water's
/// edge it becomes one plank of the bridge to the island.
public sealed class Straw
{
    public int Id;
    public Vector2 Pos;
    public float Heading;
    public bool Laid;
    public int Holders, HoldCount; // workers gripping it this tick / last tick (the count that moves it)
}

public sealed partial class Expedition
{
    // East of the clearing: a rain pond with a dirt island. On the island lie a dead queen of another lab strain and a
    // second broken tube; the first mutagen of the story is in her body.
    public static readonly Vector2 Pond = new(200, 256);
    public const float IslandR = 6f, WaterR = 14f, ShoreR = 20f;
    public const float StrawLength = 9f;       // spans the water between the shore and the island (WaterR - IslandR = 8)
    public const int StrawGoal = 5, StrawCrew = 2, StrawMaxCrew = 6;
    /// The near shore, where each straw is laid: the bridge runs from here straight east to the island.
    public static readonly Vector2 BridgeSite = new(Pond.X - WaterR - 0.5f, Pond.Y);
    public static readonly Vector2 Island = Pond;

    public readonly List<Straw> Straws = new();
    public int StrawsLaid, GlandId = -1;
    public bool Bridged => StrawsLaid >= StrawGoal;
    public Straw StrawById(int id) => id >= 0 && id < Straws.Count ? Straws[id] : null;

    /// A straw's pace with n workers holding it: none below the crew, then faster with every extra worker.
    public static float StrawSpeed(int n) => n < StrawCrew ? 0f : Config.HaulSpeed * Mathf.Min(1.6f, 0.5f + 0.25f * n);

    /// The walkable ground of the founding: the grass alley, the clearing, the path east to the pond, its shore and the
    /// island. The water blocks every ant until the bridge is built; then a straight corridor crosses it.
    public static Vector2 Constrain(Vector2 p, bool bridged)
    {
        Vector2 best = new(Mathf.Clamp(p.X, 40, 144), Mathf.Clamp(p.Y, 250, 262));
        float bd = p.DistanceSquaredTo(best);
        void Try(Vector2 q) { float d = p.DistanceSquaredTo(q); if (d < bd) { bd = d; best = q; } }
        var d0 = p - Clearing;
        Try(Clearing + (d0.Length() > 27 ? d0.Normalized() * 27 : d0));
        Try(new Vector2(Mathf.Clamp(p.X, 160, Pond.X - WaterR), Mathf.Clamp(p.Y, 248, 264)));
        var dp = p - Pond;
        float r = dp.Length();
        var dir = r > 0.001f ? dp / r : new Vector2(-1, 0);
        Try(Pond + dir * Mathf.Clamp(r, WaterR, ShoreR));
        Try(Pond + dir * Mathf.Min(r, IslandR));
        if (bridged) Try(new Vector2(Mathf.Clamp(p.X, Pond.X - WaterR - 1, Pond.X), Mathf.Clamp(p.Y, Pond.Y - 1f, Pond.Y + 1f)));
        return best;
    }
    public Vector2 Constrain(Vector2 p) => Constrain(p, Bridged);
    /// The whole founding ground, the bridge corridor included (the world builder keeps it free of rocks).
    public static bool Contains(Vector2 p, float margin = 0, bool bridged = true)
        => p.DistanceTo(Constrain(p, bridged)) <= margin + 0.01f;
    public static bool InWater(Vector2 p) { float r = p.DistanceTo(Pond); return r > IslandR && r < WaterR; }

    internal static readonly Vector2[] StrawSpots = { new(166, 252.5f), new(171, 259.5f), new(176, 253), new(168, 256.5f), new(179, 259) };
    internal static readonly float[] StrawTurns ={ 0.25f, -0.35f, 0.1f, 2.9f, -2.8f };
}

public sealed partial class Sim
{
    /// The workers take hold of a straw together; fighters sent with them walk beside it.
    public void CmdStraw(List<int> ids, Straw s)
    {
        if (s == null || s.Laid || Mission?.Stage != ExpeditionStage.Bridge) return;
        var others = new List<int>();
        foreach (int id in ids)
        {
            ref var a = ref Ants[id];
            if (!a.Alive) continue;
            if (a.Type != AntType.Worker) { others.Add(id); continue; }
            if (a.MutaP1 > 0) DropMutagen(id, ref a);
            a.StrawP1 = (byte)(s.Id + 1);
            a.Nurse = false; a.DigP1 = 0;
            a.Assigned = null; a.FoodTarget = null;
            a.Carrying = false; // it drops its crumb to take hold
            a.State = AntState.Move; a.Goal = s.Pos; a.GoalLayer = 0;
        }
        if (others.Count > 0) CmdMove(others, s.Pos, false);
    }

    /// A worker on a straw: walks to its own grip along the back half, holds on, and keeps pace with the load.
    Vector2 StrawLogic(ref Ant a, UnitStats st, ref float speedMul)
    {
        var s = Mission?.StrawById(a.StrawP1 - 1);
        if (s == null || s.Laid) { a.StrawP1 = 0; a.State = AntState.Forage; a.Goal = a.Pos; return Vector2.Zero; }
        a.State = AntState.Move;
        a.GoalLayer = 0;
        var along = MathX.Dir(s.Heading);
        var side = new Vector2(-along.Y, along.X);
        // grips spread along the trailing half, alternating sides, so the front end can reach out over the water
        float t = (a.Serial * 0.618034f) % 1f;
        var slot = s.Pos + along * (0.5f - t * Expedition.StrawLength * 0.45f) + side * (a.Serial % 2 == 0 ? 0.45f : -0.45f);
        var to = slot - a.Pos;
        a.Goal = slot;
        if (a.Layer != 0 || to.Length() > 1.1f || s.Holders >= Expedition.StrawMaxCrew) return to;
        s.Holders++;
        bool moving = s.HoldCount >= Expedition.StrawCrew;
        float pace = Expedition.StrawSpeed(s.HoldCount) + to.Length() * 4f;
        speedMul = Mathf.Clamp(pace / st.Speed, 0f, 1f);
        a.Heading += MathX.AngleDiff(a.Heading, along.AngleD()) * 0.2f;
        return moving ? (Expedition.BridgeSite - s.Pos).Normalized() * Mathf.Max(0.2f, 1.1f - to.Length()) + to : to;
    }

    void BeginBridge()
    {
        var m = Mission;
        m.Stage = ExpeditionStage.Bridge;
        m.Straws.Clear();
        for (int i = 0; i < Expedition.StrawGoal; i++)
            m.Straws.Add(new Straw { Id = i, Pos = Expedition.StrawSpots[i], Heading = Expedition.StrawTurns[i] });
    }

    /// The plank: straight across the water, side by side with the ones already laid.
    void Lay(Straw s)
    {
        var m = Mission;
        s.Laid = true;
        s.Heading = 0;
        s.Pos = new Vector2(Expedition.Pond.X - (Expedition.WaterR + Expedition.IslandR) * 0.5f, Expedition.Pond.Y + (m.StrawsLaid - 2) * 0.45f);
        m.StrawsLaid++;
        Events.Add(new SimEvent { Kind = EvKind.StrawLaid, Pos = s.Pos, Team = 0, Tier = m.StrawsLaid });
    }

    /// Art capture only (ArtValidation, targets K1-K3): jumps level 1 to its bridge with `laid` planks down and sends
    /// `crew` workers, set down beside it, to the next straw (or across to the gland once the bridge is whole).
    public void ArtBridge(int laid, int crew)
    {
        BeginBridge();
        for (int k = 0; k < laid && k < Mission.Straws.Count; k++) Lay(Mission.Straws[k]);
        if (Mission.Bridged) TickBridge(0);
        var ids = new List<int>();
        for (int i = 0; i < High && ids.Count < crew; i++) if (Ants[i].Alive && Ants[i].Team == 0 && Ants[i].Type == AntType.Worker) ids.Add(i);
        Straw next = null; // the farthest straw from the water, so the frame catches the crew on the way
        foreach (var s in Mission.Straws) if (!s.Laid && (next == null || s.Pos.X < next.Pos.X)) next = s;
        foreach (int id in ids)
        {
            ref var a = ref Ants[id];
            var p = next != null ? next.Pos + new Vector2(Rng.R(-3.5f, 0.5f), Rng.R(-1.2f, 1.2f)) : Expedition.BridgeSite + new Vector2(Rng.R(-4f, 3f), Rng.R(-0.6f, 0.6f));
            a.Pos = a.PrevPos = a.Anchor = a.Goal = Mission.Constrain(p);
            a.Layer = 0;
        }
        if (next != null) CmdStraw(ids, next);
        else CmdExtract(ids, MutagenById(Mission.GlandId));
    }

    /// Straws held by a full crew slide toward the bridge site; one that arrives is laid as the next plank.
    void TickBridge(float dt)
    {
        var m = Mission;
        foreach (var s in m.Straws)
        {
            if (s.Laid) continue;
            s.HoldCount = s.Holders;
            s.Holders = 0;
            float speed = Expedition.StrawSpeed(s.HoldCount);
            if (speed <= 0) continue;
            var to = Expedition.BridgeSite - s.Pos;
            float d = to.Length(), step = speed * dt;
            s.Heading += MathX.AngleDiff(s.Heading, to.AngleD()) * Mathf.Min(1f, 1.5f * dt);
            if (d > 1.2f + step) { s.Pos += to / d * step; continue; }
            Lay(s);
        }
        if (!m.Bridged) return;
        // the bridge holds: the dead queen's gland waits on the island (a story beat, no mutagen in the reserve)
        m.Stage = ExpeditionStage.Fragment;
        var g = AddMutagen(Expedition.Island + new Vector2(1.2f, 0.6f), "the dead queen's mutagen gland", AntType.Queen, Mutations.ExtractWork, 0);
        m.GlandId = g.Id;
    }
}
