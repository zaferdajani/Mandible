using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// For `--startup-probe --probe-found` only: plays Level 1 forward to a dug nest with an incubator and a store, so a
/// probe shot can look at the build view. The stocks are handed over; nothing here runs in a real game.
public static class ProbeFound
{
    public static void Run(Sim s)
    {
        if (s.Mission is not { Episode: 1 } m) return;
        void Until(Func<bool> done, int seconds) { for (int t = 0; t < seconds * 30 && !done(); t++) s.Tick(1f / 30); }
        List<int> U(AntType type) { var ids = new List<int>(); for (int i = 0; i < s.High; i++) if (s.Ants[i].Alive && s.Ants[i].Team == 0 && s.Ants[i].Type == type && !s.Ants[i].Dormant) ids.Add(i); return ids; }
        s.CmdMove(U(AntType.Soldier), Expedition.Clearing + new Vector2(8, 0), false);
        Until(() => m.Stage >= ExpeditionStage.Provision, 150);
        s.Col[0].Food = 300; s.Col[0].Organic = Config.FoundOrganic + 5; s.Col[0].OrganicGathered = Config.FoundOrganic + 5;
        Until(() => m.Stage >= ExpeditionStage.DigEntrance, 60);
        s.OrderEntrance(0, U(AntType.Worker));
        Until(() => m.Stage >= ExpeditionStage.Incubator, 180);
        s.Col[0].Food = 300;
        s.Dig(0, ChamberKind.Brood, s.DefaultSpot(0, ChamberKind.Brood));
        Until(() => m.Stage >= ExpeditionStage.Store, 180);
        s.Col[0].Food = 300;
        s.Dig(0, ChamberKind.Storage, s.DefaultSpot(0, ChamberKind.Storage));
        Until(() => m.Stage >= ExpeditionStage.RaiseWorkers, 180);
        GD.Print($"[probe] Level 1 played forward to {m.Stage}");
    }
}
