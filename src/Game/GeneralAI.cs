namespace Mandible;

/// The computer's generals: spend every skill point as it comes (ultimate first, then the four-rank skill, then the
/// signature), and in Lane War, where heroes never autocast, order each ready skill when the fight calls for it.
/// Everything goes through Cmds, so a saved game or a multiplayer peer replays the same choices.
public static class GeneralAI
{
    public static void Learn(Sim sim, int team, IOrders orders)
    {
        for (int i = 0; i < sim.High; i++)
        {
            ref var a = ref sim.Ants[i];
            if (!a.Alive || a.Team != team || !Config.IsHero(a.Type) || a.CharmP1 > 0 || a.SkPts == 0) continue;
            int slot = Sim.BestLearn(a);
            if (slot >= 0) orders.Issue(new Cmd { Kind = CmdKind.Learn, Team = (byte)team, Ids = new[] { i }, Serials = new[] { a.Serial }, Arg = (byte)slot });
        }
    }

    /// Lane War: ask for each learnt, ready skill; the sim fires it only when its own conditions find a worthy target.
    public static void CastReady(Sim sim, int team, int id, IOrders orders, bool fight)
    {
        if (id < 0 || !fight) return;
        ref var a = ref sim.Ants[id];
        if (!a.Alive || a.CastReq > 0) return;
        for (int s = 0; s < Skills.SlotCount(a); s++)
        {
            if (Sim.Rank(a, s) == 0 || Sim.SkillCd(a, s) > 0) continue;
            orders.Issue(new Cmd { Kind = CmdKind.Cast, Team = (byte)team, Ids = new[] { id }, Serials = new[] { a.Serial }, Arg = (byte)s });
            return;
        }
    }
}
