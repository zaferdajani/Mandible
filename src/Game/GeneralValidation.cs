using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// --generaltest (headless): the generals' Warcraft-style progression on a bare skirmish sim. Each general eats its way
/// from level 1 to 10 (one skill point a level, gates at 1/3/5/7 and 6/8/10), learns every rank through Learn orders,
/// then casts every skill at every rank through Cast orders: Queen's Pheromone's victim count, tier and level caps and
/// its expiry; Brood Call's summons per rank, their hardening and fading; and each other skill's effect.
public static class GeneralValidation
{
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static Cmd Set(Cmd c, byte arg) { c.Arg = arg; return c; }

    public static void Run(Main app)
    {
        try
        {
            var root = new Node3D(); app.AddChild(root);
            var sim = new Sim(WorldBuilder.Build(root, 20261031)); sim.BeginSkirmish();
            const float dt = 1f / 30;
            void Ticks(float seconds) { for (int k = 0; k < seconds * 30; k++) sim.Tick(dt); }
            var home = sim.NestEntrance(0);
            var outv = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - home).Normalized();
            var perp = new Vector2(-outv.Y, outv.X);
            Vector2 Clear(Vector2 p)
            {
                for (int r = 0; r < 40; r++) for (int k = 0; k < 8; k++) { var q = p + MathX.Dir(k * 0.785f) * r; if (!sim.W.Blocked(q, 7)) return q; }
                return p;
            }
            var field = Clear(home + outv * 40 + perp * 12);
            Ticks(1);
            void Apply(Cmd c) => c.Apply(sim);
            Cmd Order(CmdKind k, int id, int slot, int target = -1, Vector2 at = default)
            {
                var c = new Cmd { Kind = k, Team = (byte)sim.Ants[id].Team, Ids = new[] { id }, Serials = new[] { sim.Ants[id].Serial }, Arg = (byte)slot, P = at };
                if (target >= 0) { c.Target = target; c.TargetSerial = sim.Ants[target].Serial; }
                return c;
            }
            // clears the test ground of everything but the general under test and the queens
            void Sweep(int keep)
            {
                for (int i = 0; i < sim.High; i++)
                {
                    ref var a = ref sim.Ants[i];
                    if (!a.Alive || i == keep || a.Type == AntType.Queen || a.Pos.DistanceTo(field) > 40) continue;
                    sim.Slay(i);
                }
                sim.Pools.Clear();
            }
            List<int> Spawn(int team, AntType t, int n, int level, Vector2 at)
            {
                var ids = new List<int>();
                for (int k = 0; k < n; k++)
                {
                    int id = sim.Spawn(team, t, at + MathX.Dir(k * 1.1f) * (0.8f + 0.5f * k), 0, level);
                    sim.Ants[id].State = AntState.Hold; sim.Ants[id].Anchor = sim.Ants[id].Pos;
                    ids.Add(id);
                }
                return ids;
            }

            foreach (var type in new[] { AntType.Warlord, AntType.Matron, AntType.Marshal })
            {
                string tag = Config.Of(type).Name;
                Sweep(-1);
                int g = sim.Spawn(0, type, field, 0);
                ref var a = ref sim.Ants[g];
                Check(a.Level == 1 && a.Sk0 == 1 && a.SkPts == 0, $"{tag}: a new general must know its signature at rank 1 with no spare point");
                // a general levels only by eating
                float organic = 0;
                int lastLevel = 1;
                while (a.Level < Config.HeroMaxLevel && organic < 1000)
                {
                    sim.TestFeed(g, 5); organic += 5;
                    if (a.Level != lastLevel)
                    {
                        Check(a.SkPts == a.Level - a.Sk0 - a.Sk1 - a.Sk2, $"{tag}: one skill point per level");
                        // spend as the player would, through Learn orders, each the moment it is earned
                        for (int s = Sim.BestLearn(a); s >= 0; s = Sim.BestLearn(a)) Apply(Order(CmdKind.Learn, g, s));
                        lastLevel = a.Level;
                    }
                }
                GD.Print($"[general-test] {tag}: level {a.Level} after {organic:0} organic; ranks Q{a.Sk0} W{a.Sk1} E{a.Sk2}, {a.SkPts} unspent");
                Check(a.Level == 10, $"{tag}: did not reach level 10");
                Check(organic >= 150 && organic <= 260, $"{tag}: the 10-level curve should cost about 210 organic, took {organic}");
                for (int s = 0; s < 3; s++) Check(Sim.Rank(a, s) == Skills.MaxRank(Skills.At(type, s)), $"{tag}: slot {s} not mastered");
                Check(a.SkPts == 0, $"{tag}: points left over");

                // the gates: no ultimate before level 6, no rank above what the level allows
                int fresh = sim.Spawn(0, type, field + perp * 6, 0, 5);
                Check(sim.Ants[fresh].SkPts == 4, $"{tag}: a level-5 general hatches with 4 points to spend");
                Apply(Order(CmdKind.Learn, fresh, 2));
                Check(sim.Ants[fresh].Sk2 == 0, $"{tag}: ultimate learnt before level 6");
                for (int k = 0; k < 4; k++) Apply(Order(CmdKind.Learn, fresh, 1));
                Check(sim.Ants[fresh].Sk1 == 3, $"{tag}: rank 4 must wait for level 7 (got {sim.Ants[fresh].Sk1})");
                sim.Slay(fresh);

                for (int slot = 0; slot < 3; slot++)
                {
                    var sk = Skills.At(type, slot);
                    for (int rank = 1; rank <= Skills.MaxRank(sk); rank++)
                    {
                        Sweep(g);
                        a.Pos = a.PrevPos = field; a.Hp = a.MaxHp; a.State = AntState.Hold; a.Target = -1; a.Alt = Config.CruiseAlt(type);
                        a.AbilityCd = a.CdW = a.CdE = 99; // only the skill under test may fire
                        sim.TestSetRank(g, slot, rank);
                        int r = rank - 1;
                        var front = field + outv * 5;
                        switch (sk)
                        {
                            case SkillId.QueensPheromone:
                            {
                                // a crowd of every tier at the level cap, and one soldier a level above it
                                var pool = new List<int>();
                                pool.AddRange(Spawn(1, AntType.Soldier, 6, Skills.CharmLevel[r], front));
                                pool.AddRange(Spawn(1, AntType.Major, 2, 1, front + perp * 1.5f));
                                pool.AddRange(Spawn(Config.Wild, AntType.Wasp, 1, 1, front - perp * 1.5f));
                                pool.AddRange(Spawn(1, AntType.Warlord, 1, 3, front + outv * 1.5f));
                                var over = Spawn(1, AntType.Soldier, 1, Math.Min(10, Skills.CharmLevel[r] + 1), front + outv * 0.5f);
                                if (Skills.CharmLevel[r] >= 10) { pool.AddRange(over); over.Clear(); } // the top rank has no level cap
                                foreach (int j in pool) sim.Ants[j].Alt = 0;
                                Apply(Order(CmdKind.Cast, g, slot, pool[0]));
                                Ticks(0.5f);
                                int taken = 0, maxTier = 0;
                                foreach (int j in pool) if (sim.Ants[j].Alive && sim.Ants[j].CharmP1 > 0) { taken++; maxTier = Math.Max(maxTier, Skills.ControlTier(sim.Ants[j].Type)); }
                                bool overTaken = over.Count > 0 && sim.Ants[over[0]].CharmP1 > 0;
                                GD.Print($"[general-test] {tag} Queen's Pheromone r{rank}: {taken} turned (cap {Skills.CharmCount[r]}), highest tier {maxTier} (cap {Skills.CharmTier[r]}), over-level taken {overTaken}");
                                Check(taken == Skills.CharmCount[r], $"Queen's Pheromone r{rank}: turned {taken}, expected {Skills.CharmCount[r]}");
                                Check(maxTier <= Skills.CharmTier[r] && !overTaken, $"Queen's Pheromone r{rank}: took an ant above its tier or level");
                                foreach (int j in pool) if (sim.Ants[j].Alive && sim.Ants[j].CharmP1 > 0) Check(sim.Ants[j].Team == 0, "a turned ant must fight for the caster");
                                if (rank == 4)
                                {
                                    // the top rank takes generals, for half the time
                                    var foe = Spawn(1, AntType.Matron, 1, 9, front + outv * 3);
                                    a.CdW = 0; Apply(Order(CmdKind.Cast, g, slot, foe[0])); Ticks(0.4f);
                                    Check(sim.Ants[foe[0]].CharmP1 == 2 && Mathf.Abs(sim.Ants[foe[0]].CharmT - Skills.CharmTime[r] * 0.5f) < 0.6f, "Queen's Pheromone r4: an enemy general must turn for half the time");
                                }
                                Ticks(Skills.CharmTime[r] + 0.5f);
                                foreach (int j in pool) if (sim.Ants[j].Alive) Check(sim.Ants[j].CharmP1 == 0 && sim.Ants[j].Team != 0, $"Queen's Pheromone r{rank}: the scent must wear off after {Skills.CharmTime[r]}s");
                                break;
                            }
                            case SkillId.BroodCall:
                            {
                                Spawn(1, AntType.Soldier, 3, 1, front + outv * 8);
                                int Brood() => sim.Col[0].Count[(int)AntType.Soldier] + sim.Col[0].Count[(int)AntType.Major] + sim.Col[0].Count[(int)AntType.Flyer] + sim.Col[0].Count[(int)AntType.Gunner];
                                int before = Brood();
                                Apply(Order(CmdKind.Cast, g, slot));
                                Ticks(0.4f);
                                var got = new Dictionary<AntType, int>();
                                var called = new List<int>();
                                for (int i = 0; i < sim.High; i++) if (sim.Ants[i].Alive && sim.Ants[i].Summoned) { called.Add(i); got[sim.Ants[i].Type] = got.GetValueOrDefault(sim.Ants[i].Type) + 1; }
                                var want = Skills.Brood(rank);
                                string seen = string.Join(", ", got);
                                GD.Print($"[general-test] {tag} Brood Call r{rank}: {seen}");
                                foreach (var (t, n) in want) Check(got.GetValueOrDefault(t) == n, $"Brood Call r{rank}: expected {n} {t}, got {got.GetValueOrDefault(t)}");
                                Check(got.Count == want.Length, $"Brood Call r{rank}: unexpected types {seen}");
                                Check(Brood() == before, "summons must not take brood room");
                                int lv0 = sim.Ants[called[0]].Level;
                                Ticks(Skills.HardenEvery * 2 + 0.2f);
                                int hard = 0; foreach (int j in called) if (sim.Ants[j].Alive && sim.Ants[j].Summoned) hard = Math.Max(hard, sim.Ants[j].Level);
                                Check(hard > lv0 || hard == 0, $"Brood Call r{rank}: callows must harden (level {lv0} -> {hard})");
                                Ticks(Skills.BroodLife[r]);
                                int left = 0; foreach (int j in called) if (sim.Ants[j].Alive && sim.Ants[j].Summoned) left++;
                                Check(left == 0, $"Brood Call r{rank}: {left} summons outlived their time");
                                Check(Brood() == before, "fading summons must not change the brood count");
                                break;
                            }
                            default:
                            {
                                Spawn(1, AntType.Soldier, 6, 1, front);
                                var big = Spawn(1, AntType.Major, 1, 1, front + perp);
                                var mates = Spawn(0, AntType.Soldier, 6, 1, field - outv * 2);
                                float hp0 = sim.Ants[big[0]].Hp;
                                int target = Skills.Aim(sk) == SkillAim.Unit ? big[0] : -1;
                                var at = Skills.Aim(sk) == SkillAim.Point ? front : Vector2.Zero;
                                Apply(Order(CmdKind.Cast, g, slot, target, at));
                                var kind = sk switch
                                {
                                    SkillId.WarCry => EvKind.WarCry, SkillId.MandibleFrenzy => EvKind.Frenzy, SkillId.AcidBurst => EvKind.AcidBurst,
                                    SkillId.CausticCloud => EvKind.Cloud, SkillId.DiveBomb => EvKind.DiveBomb, SkillId.TrailBlaze => EvKind.TrailBlaze, _ => EvKind.TalonDive,
                                };
                                bool fired = false;
                                for (int k = 0; k < 30 && !fired; k++) { sim.Tick(dt); foreach (var e in sim.Events) if (e.Kind == kind && e.Team == 0) fired = true; }
                                Check(fired, $"{tag} {Skills.Name(sk)} r{rank} did not fire");
                                Check(Sim.SkillCd(a, slot) > 0, $"{tag} {Skills.Name(sk)} r{rank}: no cooldown");
                                string fx = sk switch
                                {
                                    SkillId.MandibleFrenzy => $"frenzy {a.FrenzyT:0.0}s",
                                    SkillId.TrailBlaze => $"mates hasted {mates.FindAll(j => sim.Ants[j].HasteT > 0).Count}/6",
                                    SkillId.CausticCloud => $"cloud slow {(sim.Pools.Count > 0 ? sim.Pools[^1].Slow : 0):0.0}",
                                    SkillId.WarCry => $"mates buffed {mates.FindAll(j => sim.Ants[j].BuffT > 0).Count}/6",
                                    _ => $"major hp {hp0:0} -> {(sim.Ants[big[0]].Alive ? sim.Ants[big[0]].Hp : 0):0}",
                                };
                                GD.Print($"[general-test] {tag} {Skills.Name(sk)} r{rank}: fired, cd {Sim.SkillCd(a, slot):0.0}s, {fx}");
                                if (sk == SkillId.MandibleFrenzy) Check(Mathf.Abs(a.FrenzyT - Skills.FrenzyTime[r]) < 1.2f, "frenzy time");
                                if (sk == SkillId.TrailBlaze) Check(mates.FindAll(j => sim.Ants[j].HasteT > 0).Count == 6, "trail blaze must reach the nearby squad");
                                if (sk == SkillId.CausticCloud) Check(sim.Pools.Count > 0 && Mathf.IsEqualApprox(sim.Pools[^1].Slow, Skills.CloudSlow[r]), "caustic cloud slow");
                                if (sk == SkillId.TalonDive) Check(!sim.Ants[big[0]].Alive || sim.Ants[big[0]].Hp < hp0, "talon dive must strike the major");
                                break;
                            }
                        }
                    }
                }
                sim.Slay(g);
            }

            // auto-eat: a squad with it ON eats its kill and keeps the haulers off it; with it OFF the kill is left whole
            Sweep(-1);
            foreach (bool on in new[] { true, false })
            {
                var spot = Clear(home + outv * 75 + perp * (on ? -14 : 14));
                var squad = Spawn(0, AntType.Soldier, 6, 1, spot);
                sim.FormSquads(squad);
                foreach (int id in squad) Check(!sim.Ants[id].AutoEat, "fighters must start with auto-eat off");
                if (on) Apply(Set(Cmd.Units(CmdKind.AutoEat, 0, new List<int> { squad[0] }, sim), 1));
                foreach (int id in squad) Check(sim.Ants[id].AutoEat == on, $"auto-eat {(on ? "ON" : "OFF")} must reach the whole squad");
                foreach (int id in squad) sim.Ants[id].State = AntState.Idle;
                var prey = Spawn(1, AntType.Major, 1, 1, spot + outv * 1.5f)[0];
                float evo0 = 0; foreach (int id in squad) evo0 += sim.Ants[id].Evo;
                sim.Slay(prey, squad[0]);
                FoodSource carcass = null;
                foreach (var f in sim.W.Food) if (f.Kind == FoodKind.Corpse && f.Alive && f.Pos.DistanceTo(spot + outv * 1.5f) < 1f) carcass = f;
                Check(carcass != null, "the kill left no carcass");
                Ticks(8);
                float evo = 0; foreach (int id in squad) evo += sim.Ants[id].Evo + 24 * sim.Ants[id].Stage;
                bool queued = (carcass.Queued & 1) != 0;
                GD.Print($"[general-test] auto-eat {(on ? "ON" : "OFF")}: mouthfuls {carcass.Feasted}, claim {carcass.ClaimTeam}, squad evolution {evo0:0.0} -> {evo:0.0}, queued for haulers {queued}");
                if (on) Check(carcass.Feasted >= 3 && evo > evo0 && carcass.ClaimTeam == 1 && !queued, "auto-eat ON: the squad must eat its kill, and the haulers must leave it");
                else Check(carcass.Feasted == 0 && carcass.ClaimTeam == 0, "auto-eat OFF: the kill must be left for the workers");
                foreach (int id in squad) sim.Slay(id);
            }

            // collection: a carcass dropped near the nest, where no ant has seen it, is noticed and carried home; one an
            // auto-eat fighter has claimed is left alone
            {
                var c0 = sim.Col[0];
                sim.SetRemains(0, RemainsOrder.Haul);
                for (int k = 0; k < 14; k++) { int wid = sim.Spawn(0, AntType.Worker, home + MathX.Dir(k * 0.45f) * 4f, k * 0.45f); sim.Ants[wid].State = AntState.Forage; } // the sweeps above took the workforce
                float radius = sim.CollectRadius(0);
                FoodSource Drop(Vector2 at)
                {
                    var st = Config.Of(AntType.Beetle);
                    var f = new FoodSource { Id = sim.W.NextFoodId++, Kind = FoodKind.Corpse, Pos = at, Amount = st.CorpseFood, Max = st.CorpseFood, Radius = st.Size * 0.45f, Team = Config.Wild, AntType = AntType.Beetle, Natural = false };
                    sim.W.Food.Add(f);
                    return f;
                }
                var near = Drop(Clear(home + outv * (radius * 0.72f)));
                float nearAt = near.Pos.DistanceTo(home);
                var kept = Drop(Clear(home + outv * (radius * 0.5f) + perp * (radius * 0.5f)));
                kept.ClaimTeam = 1; kept.ClaimUntil = sim.Time + 60;
                float organic0 = c0.OrganicGathered;
                bool alert = false;
                for (int k = 0; k < 90 * 30 && c0.OrganicGathered < organic0 + 3; k++)
                {
                    sim.Tick(dt);
                    foreach (var e in sim.Events) if (e.Kind == EvKind.Collect && e.Team == 0 && e.FoodP1 == near.Id + 1) alert = true;
                }
                int wk = 0, forage = 0, surface = 0; for (int i = 0; i < sim.High; i++) { ref var x = ref sim.Ants[i]; if (x.Alive && x.Team == 0 && x.Type == AntType.Worker) { wk++; if (x.State == AntState.Forage) forage++; if (x.Layer == 0) surface++; } }
                GD.Print($"[general-test] collection workers {wk}, foraging {forage}, on the surface {surface}, remains order {c0.Remains}, founded {c0.Founded}");
                GD.Print($"[general-test] collection: radius {radius:0}, beetle dropped {nearAt:0} from the nest queued {alert}, organic brought home {c0.OrganicGathered - organic0:0.0}, claimed one queued {(kept.Queued & 1) != 0}, untouched {kept.Amount >= kept.Max - 0.01f}");
                Check(alert, "the workers never noticed the carcass near the nest");
                Check(c0.OrganicGathered >= organic0 + 3, "the workers did not bring the carcass home");
                Check((kept.Queued & 1) == 0, "a claimed carcass must be left to its eaters");
            }

            // every order above went through Learn and Cast: they must also survive the wire
            var probe = new Cmd { Kind = CmdKind.Cast, Team = 0, Ids = new[] { 7 }, Serials = new[] { 9 }, Arg = 2, Target = 3, TargetSerial = 4, P = new Vector2(1.5f, 2.5f) };
            var ms = new System.IO.MemoryStream(); var w = new System.IO.BinaryWriter(ms); probe.Write(w); ms.Position = 0;
            var back = Cmd.Read(new System.IO.BinaryReader(ms));
            Check(back.Kind == CmdKind.Cast && back.Arg == 2 && back.Target == 3 && back.P == probe.P, "Cast order does not round-trip");
            GD.Print("[general-test] ALL PASS");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[general-test] FAIL " + e); app.GetTree().Quit(1); }
    }

    /// --general-capture=DIR (windowed, through godot-run.sh): real frames of the insignia at levels 1, 5 and 10, the
    /// skill bar and the skill tree, Queen's Pheromone turning an enemy squad, each Brood Call rank and the ultimates.
    public static async System.Threading.Tasks.Task Capture(Main app, string directory)
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(directory);
            app.StartSkirmish();
            var game = app.Session;
            game.Hud.Visible = false;
            game.Camera.UserControl = false;
            game.Fog.RevealAll = true;
            var sim = game.Sim;
            async System.Threading.Tasks.Task Frame() => await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            async System.Threading.Tasks.Task Run(float seconds)
            {
                float t = sim.Time + seconds; ulong end = Time.GetTicksMsec() + (ulong)(seconds * 4000 + 10000);
                while (sim.Time < t && Time.GetTicksMsec() < end) await Frame();
            }
            async System.Threading.Tasks.Task Shot(string name, Vector2 at, float distance, bool hud = false)
            {
                game.SetPaused(true);
                game.Hud.Visible = hud;
                game.Camera.JumpTo(at, distance);
                await app.ToSignal(app.GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
                await Frame();
                using var im = app.GetViewport().GetTexture().GetImage();
                Check(im.SavePng(directory.PathJoin(name + ".png")) == Error.Ok, "General capture failed to save " + name);
                GD.Print($"[general-capture] {name}; fps {Engine.GetFramesPerSecond()}");
                game.Hud.Visible = false;
                game.SetPaused(false);
            }
            void Select(List<int> ids)
            {
                foreach (int id in game.Input.Selection) if (sim.Ants[id].Alive) sim.Ants[id].Selected = false;
                game.Input.Selection.Clear();
                foreach (int id in ids) { game.Input.Selection.Add(id); sim.Ants[id].Selected = true; }
            }
            var home = sim.NestEntrance(0);
            var outv = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - home).Normalized();
            var perp = new Vector2(-outv.Y, outv.X);
            Vector2 Clear(Vector2 p, float pad)
            {
                for (int r = 0; r < 40; r++) for (int k = 0; k < 8; k++) { var q = p + MathX.Dir(k * 0.785f) * r; if (!sim.W.Blocked(q, pad)) return q; }
                return p;
            }
            List<int> Spawn(int team, AntType t, int n, Vector2 at, int level = 1)
            {
                var ids = new List<int>();
                for (int k = 0; k < n; k++)
                {
                    int id = sim.Spawn(team, t, at + MathX.Dir(k * 2.39996f) * (0.9f * MathF.Sqrt(k)), (home - at).AngleD(), level);
                    if (id >= 0) ids.Add(id);
                }
                if (Config.InSquads(t)) sim.FormSquads(ids);
                return ids;
            }
            int General(AntType t, Vector2 at, int level, bool learn = true)
            {
                int id = sim.Spawn(0, t, at, outv.AngleD(), level);
                if (learn) for (int s = Sim.BestLearn(sim.Ants[id]); s >= 0; s = Sim.BestLearn(sim.Ants[id])) sim.LearnSkill(id, s);
                sim.Ants[id].State = AntState.Hold; sim.Ants[id].Anchor = sim.Ants[id].Pos;
                sim.Ants[id].AutoMask = 0; // the capture decides when skills fire
                return id;
            }
            void Clean(Vector2 around, float r, int keep = -1)
            {
                for (int i = 0; i < sim.High; i++)
                    if (sim.Ants[i].Alive && i != keep && sim.Ants[i].Type != AntType.Queen && sim.Ants[i].Pos.DistanceTo(around) < r && sim.Ants[i].Type != AntType.Worker) sim.Slay(i);
            }
            await Run(3f); // the first frames compile shaders

            // 01-03: the insignia, one general per tier, close, then the three side by side at play zoom
            var parade = Clear(home + outv * 24 + perp * 14, 9);
            int g1 = General(AntType.Warlord, parade - perp * 5, 1);
            int g5 = General(AntType.Warlord, parade, 5);
            int g10 = General(AntType.Warlord, parade + perp * 5, 10);
            await Run(1f);
            await Shot("01-insignia-level1", sim.Ants[g1].Pos, 12f);
            await Shot("02-insignia-level5", sim.Ants[g5].Pos, 12f);
            await Shot("03-insignia-level10", sim.Ants[g10].Pos, 12f);
            Select(new List<int> { g10 });
            await Shot("04-insignia-trio-play-zoom", parade, 24f);

            // 05-06: the skill bar for a selected general, then the tree as a level-up opens it
            Select(new List<int> { g10 });
            await Run(0.3f);
            await Shot("05-skill-bar", sim.Ants[g10].Pos, 16f, true);
            int fresh = General(AntType.Warlord, parade - outv * 6, 6, false);
            sim.LearnSkill(fresh, 1); sim.LearnSkill(fresh, 1);
            Select(new List<int> { fresh });
            game.Hud.SkillBar.OnLevelUp(fresh);
            await Run(0.3f);
            await Shot("06-skill-tree", sim.Ants[fresh].Pos, 16f, true);
            game.Hud.SkillBar.ToggleTree();
            foreach (int id in new[] { g1, g5, fresh }) sim.Slay(id);
            Select(new List<int>());

            // 07-08: Queen's Pheromone (rank 4) turns an enemy squad and a major on their own line
            var arena = Clear(home + outv * 44 - perp * 18, 12);
            Clean(arena, 30);
            int warlord = General(AntType.Warlord, arena - outv * 6, 10);
            var foes = Spawn(1, AntType.Soldier, 12, arena + outv * 2);
            foes.AddRange(Spawn(1, AntType.Major, 2, arena + outv * 4 + perp * 2));
            foreach (int id in foes) { sim.Ants[id].State = AntState.AttackMove; sim.Ants[id].Goal = sim.Ants[warlord].Pos; }
            await Run(1.2f);
            new Cmd { Kind = CmdKind.Cast, Team = 0, Ids = new[] { warlord }, Serials = new[] { sim.Ants[warlord].Serial }, Arg = 1, Target = foes[12], TargetSerial = sim.Ants[foes[12]].Serial }.Apply(sim);
            await Run(1.0f);
            int turned = foes.FindAll(id => sim.Ants[id].Alive && sim.Ants[id].CharmP1 > 0).Count;
            Check(turned == Skills.CharmCount[3], $"Pheromone capture: {turned} turned");
            await Shot("07-queens-pheromone", arena, 15f);
            await Run(2.5f);
            await Shot("08-pheromone-turned-fighting", arena + outv * 2, 10f);
            Clean(arena, 30);
            sim.Slay(g10);

            // 09-13: Brood Call at every rank, then the hardened brood of rank 4
            var nursery = Clear(home + outv * 30 - perp * 30, 10);
            int matron = General(AntType.Matron, nursery, 10);
            for (int rank = 1; rank <= 4; rank++)
            {
                Clean(nursery, 25, matron);
                sim.TestSetRank(matron, 1, rank);
                new Cmd { Kind = CmdKind.Cast, Team = 0, Ids = new[] { matron }, Serials = new[] { sim.Ants[matron].Serial }, Arg = 1 }.Apply(sim);
                await Run(0.9f);
                await Shot($"{8 + rank:00}-brood-call-rank{rank}", sim.Ants[matron].Pos, 10f + 1.5f * rank);
            }
            await Run(Skills.HardenEvery * 3);
            await Shot("13-brood-hardened", sim.Ants[matron].Pos, 12f);

            // 14-15: the Matron's and the Marshal's ultimates on a crowd
            Clean(nursery, 25, matron);
            var crowd = Spawn(1, AntType.Soldier, 12, nursery + outv * 7);
            new Cmd { Kind = CmdKind.Cast, Team = 0, Ids = new[] { matron }, Serials = new[] { sim.Ants[matron].Serial }, Arg = 2, P = nursery + outv * 7 }.Apply(sim);
            await Run(1.2f);
            await Shot("14-caustic-cloud", nursery + outv * 5, 14f);
            int marshal = General(AntType.Marshal, nursery - perp * 6, 10);
            var prey = Spawn(1, AntType.Major, 1, nursery + outv * 9 - perp * 6);
            new Cmd { Kind = CmdKind.Cast, Team = 0, Ids = new[] { marshal }, Serials = new[] { sim.Ants[marshal].Serial }, Arg = 1 }.Apply(sim);
            await Run(0.3f);
            new Cmd { Kind = CmdKind.Cast, Team = 0, Ids = new[] { marshal }, Serials = new[] { sim.Ants[marshal].Serial }, Arg = 2, Target = prey[0], TargetSerial = sim.Ants[prey[0]].Serial }.Apply(sim);
            await Run(0.12f);
            await Shot("15-talon-dive", sim.Ants[marshal].Pos, 14f);

            // 16-18: auto-eat. A squad with it on (the drops over them, the lit button), then eating its kill.
            var mess = Clear(home + outv * 30 + perp * 22, 8);
            Clean(mess, 20);
            var eaters = Spawn(0, AntType.Soldier, 6, mess);
            var leavers = Spawn(0, AntType.Soldier, 6, mess + perp * 6);
            var c = Cmd.Units(CmdKind.AutoEat, 0, new List<int> { eaters[0] }, sim); c.Arg = 1; c.Apply(sim);
            Select(eaters);
            await Run(0.4f);
            await Shot("16-auto-eat-on", mess + perp * 3, 15f, true);
            var meal = Spawn(1, AntType.Major, 1, mess + outv * 1.5f);
            sim.Slay(meal[0], eaters[0]);
            await Run(3f);
            Select(new List<int>());
            await Shot("17-auto-eat-feasting", mess + outv * 1.5f, 12f);

            // 18: the Remains page shows the collection ring; a carcass inside it is queued and fetched
            var drop = Clear(home + outv * 24 - perp * 10, 4);
            var bst = Config.Of(AntType.Beetle);
            sim.W.Food.Add(new FoodSource { Id = sim.W.NextFoodId++, Kind = FoodKind.Corpse, Pos = drop, Amount = bst.CorpseFood, Max = bst.CorpseFood, Radius = bst.Size * 0.45f, Team = Config.Wild, AntType = AntType.Beetle, Natural = true });
            game.Hud.CommandHud?.ShowPage(4);
            await Run(4f);
            await Shot("18-collection-radius", home + outv * 14, 60f, true);
            await Shot("19-carcass-queued", drop, 14f);
            GD.Print("[general-capture] done");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[general-capture] FAIL " + e); app.GetTree().Quit(1); }
    }
}
