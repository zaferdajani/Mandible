using System.Collections.Generic;
using System.IO;
using Godot;

namespace Mandible;

public enum CmdKind : byte { Move, AttackMove, Attack, Gather, Stop, Hold, Enqueue, CancelLast, Research, CancelResearch, Rally, Reinforce, Ability, BuyItem, Dig }

/// One player order. In multiplayer these are the only thing sent over the network:
/// every machine applies the same orders on the same tick and so stays in sync.
public sealed class Cmd
{
    public CmdKind Kind;
    public byte Team;
    public int[] Ids = System.Array.Empty<int>();
    public int[] Serials = System.Array.Empty<int>();
    public Vector2 P;
    public int Target = -1, TargetSerial, FoodId = -1;
    public byte Arg;   // ProdKind, Upgrade, item or ChamberKind
    public byte Layer; // Move / AttackMove: 0 the surface, 1 + team inside that nest (garrison or raid)

    public static Cmd Units(CmdKind k, int team, List<int> ids, Sim sim)
    {
        var c = new Cmd { Kind = k, Team = (byte)team, Ids = ids.ToArray(), Serials = new int[ids.Count] };
        for (int i = 0; i < ids.Count; i++) c.Serials[i] = sim.Ants[ids[i]].Serial;
        return c;
    }

    public void Write(BinaryWriter w)
    {
        w.Write((byte)Kind); w.Write(Team);
        w.Write((ushort)Ids.Length);
        for (int i = 0; i < Ids.Length; i++) { w.Write((ushort)Ids[i]); w.Write(Serials[i]); }
        w.Write(P.X); w.Write(P.Y);
        w.Write(Target); w.Write(TargetSerial); w.Write(FoodId); w.Write(Arg); w.Write(Layer);
    }

    public static Cmd Read(BinaryReader r)
    {
        var c = new Cmd { Kind = (CmdKind)r.ReadByte(), Team = r.ReadByte() };
        int n = r.ReadUInt16();
        c.Ids = new int[n]; c.Serials = new int[n];
        for (int i = 0; i < n; i++) { c.Ids[i] = r.ReadUInt16(); c.Serials[i] = r.ReadInt32(); }
        c.P = new Vector2(r.ReadSingle(), r.ReadSingle());
        c.Target = r.ReadInt32(); c.TargetSerial = r.ReadInt32(); c.FoodId = r.ReadInt32(); c.Arg = r.ReadByte(); c.Layer = r.ReadByte();
        return c;
    }

    /// Executes the order. Only ants that still exist and belong to the issuing colony obey,
    /// so a stale or tampered order can never move someone else's ants.
    public void Apply(Sim sim)
    {
        var ids = new List<int>(Ids.Length);
        for (int i = 0; i < Ids.Length; i++)
            if (sim.Valid(Ids[i], Serials[i]) && sim.Ants[Ids[i]].Team == Team && (sim.Mode != GameMode.Lanes || Config.IsHero(sim.Ants[Ids[i]].Type)))
                ids.Add(Ids[i]); // in Lane War only the hero takes orders
        switch (Kind)
        {
            case CmdKind.Move: if (ids.Count > 0) sim.CmdMove(ids, P, false, Layer); break;
            case CmdKind.AttackMove: if (ids.Count > 0) sim.CmdMove(ids, P, true, Layer); break;
            case CmdKind.Dig: sim.Dig(Team, (ChamberKind)Arg, P); break;
            case CmdKind.Attack:
                if (ids.Count > 0 && sim.Valid(Target, TargetSerial) && sim.Ants[Target].Team != Team) sim.CmdAttack(ids, Target);
                break;
            case CmdKind.Gather:
            {
                var f = sim.W.FoodById(FoodId);
                if (ids.Count > 0 && f != null && f.Alive) sim.CmdGather(ids, f);
                break;
            }
            case CmdKind.Stop: if (ids.Count > 0) sim.CmdStop(ids, false); break;
            case CmdKind.Hold: if (ids.Count > 0) sim.CmdStop(ids, true); break;
            case CmdKind.Enqueue: sim.Enqueue(Team, (ProdKind)Arg); break;
            case CmdKind.CancelLast: sim.CancelLast(Team); break;
            case CmdKind.Research: sim.StartResearch(Team, (Upgrade)Arg, P == Vector2.Zero ? null : P); break;
            case CmdKind.CancelResearch: sim.CancelResearch(Team); break;
            case CmdKind.Reinforce: if (ids.Count > 0) sim.CmdReinforce(ids); break;
            case CmdKind.Ability: sim.CmdAbility(Team); break;
            case CmdKind.BuyItem: sim.BuyItem(Team, Arg); break;
            case CmdKind.Rally:
                sim.Col[Team].Rally = P;
                sim.Col[Team].HasRally = true;
                break;
        }
    }
}

/// Where orders go: straight into the simulation (single player) or into the lockstep queue.
public interface IOrders
{
    void Issue(Cmd c);
}

public sealed class DirectOrders : IOrders
{
    readonly Sim _sim;
    public DirectOrders(Sim sim) { _sim = sim; }
    public void Issue(Cmd c) => c.Apply(_sim);
}
