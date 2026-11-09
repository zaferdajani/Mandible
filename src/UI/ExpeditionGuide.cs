using Godot;

namespace Mandible;

/// Presentation for the mission's authoritative objectives. No skip button can unlock
/// rooms or accidentally turn the introductory building lesson into an enemy raid.
public partial class ExpeditionGuide : PanelContainer
{
    public GameSession Game;
    Label _goal, _heading, _body, _list, _stake;
    StarList _stars;
    int _shown = -1;
    public override void _Ready()
    {
        Game.Hud.EpisodeCard = this;
        var m = Game.Sim.Mission;
        bool steps = m != null && m.Episode > 1;
        _desktopBottom = steps ? 520 : 384;
        UiTheme.Place(this, 1, 0, 1, 0, -445, 70, -16, _desktopBottom);
        var column = new VBoxContainer(); AddChild(column);
        column.AddChild(UiTheme.Text($"LEVEL {Levels.NumberOf(Game.Episode)} · {CampaignEpisodes.Name(Game.Episode).ToUpperInvariant()}", 12, UiTheme.Bold, UiTheme.Dim));
        // the main victory, always on top: every step below leads to it
        _goal = UiTheme.Text((steps ? "MAIN GOAL  ·  " : "") + m.Goal, 17, UiTheme.Title, steps ? new Color(1f, 0.55f, 0.4f) : UiTheme.Dim);
        _goal.AutowrapMode = TextServer.AutowrapMode.WordSmart; _goal.CustomMinimumSize = new Vector2(380, 0);
        column.AddChild(_goal);
        column.AddChild(new HSeparator());
        _heading = UiTheme.Text("", 21, UiTheme.Title, UiTheme.Gold); column.AddChild(_heading);
        _body = UiTheme.Text("", 15); _body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _body.CustomMinimumSize = new Vector2(380, 80); column.AddChild(_body);
        if (steps)
        {
            // the whole episode at a glance: done, current and still to come
            _list = UiTheme.Text("", 14, UiTheme.Bold, UiTheme.Ink); column.AddChild(_list);
            _stake = UiTheme.Text("", 13, null, UiTheme.Dim); _stake.AutowrapMode = TextServer.AutowrapMode.WordSmart; column.AddChild(_stake);
        }
        // the optional goals, live: lit while they still hold
        _stars = new StarList { Size_ = 8, FontSize = 13 };
        column.AddChild(_stars);
        if (steps) column.AddChild(new MutationStrip { Game = Game }); // shows itself from the Silk Trap on
        var row = new HBoxContainer(); column.AddChild(row);
        var finger = new Vector2(0, Settings.Mobile ? 46 : 0);
        var show = new Button { Text = "Show objective", CustomMinimumSize = finger }; row.AddChild(show);
        show.Pressed += () => Game.Camera.PanTo(Game.Sim.Mission.Marker);
        // the founding's numbers and its one button live in their own panel, top centre (FoundingPanel)
        if (m.Episode == 1) Game.Hud.AddPanel(new FoundingPanel { Game = Game });
    }
    /// Art captures (ArtValidation) render the world without the guide's arrows and rings.
    public static bool Silent;

    public override void _Process(double delta)
    {
        var m = Game.Sim.Mission;
        if (m == null) return;
        if (Silent) { if (_guiding) Game.Guide.Clear(); _guiding = false; return; }
        _heading.Text = m.Title;
        _body.Text = Settings.Mobile ? ForTouch(m.Instruction) : m.Instruction;
        Arrange();
        // the opening always points at what to do next: the live beetle, its remains, then the nest site
        bool guiding = false;
        if (m.Episode == 1)
        {
            int beetle = m.Guards.Count > 0 && Game.Sim.Ants[m.Guards[0]].Alive ? m.Guards[0] : -1;
            if (m.Stage == ExpeditionStage.ClearTrail && beetle >= 0)
            {
                guiding = true;
                Game.Guide.Show(Game.Sim.Ants[beetle].Pos, "GROUND BEETLE · ATTACK",
                    Settings.Mobile ? "Tap Army, then tap the beetle. Workers wait behind the queen until it is dead" : "Soldiers (F2), then right-click it. Workers wait behind the queen until it is dead");
            }
            else if (m.Stage == ExpeditionStage.Provision && m.RemainsLeft)
            {
                guiding = true;
                Game.Guide.Show(m.RemainsPos, "BEETLE REMAINS", Settings.Mobile ? "Tap Workers, then the remains: carry them to the clearing" : "Workers (F3), then right-click: carry them to the clearing");
            }
            else if (m.Stage == ExpeditionStage.DigEntrance)
            {
                guiding = true;
                Game.Guide.Show(Expedition.Clearing, "NEST SITE", ""); // the panel at the top carries the button and the words
            }
            else if (m.Stage == ExpeditionStage.Bridge && m.NextStraw is { } straw)
            {
                // a straw on the shore until a crew has one; then the water's edge where it is laid
                guiding = true;
                if (straw.HoldCount >= Expedition.StrawCrew)
                    Game.Guide.Show(Expedition.BridgeSite, $"BRIDGE · {m.StrawsLaid}/{Expedition.StrawGoal}", "Keep at least two workers on the straw; more carry it faster");
                else
                    Game.Guide.Show(straw.Pos, "STRAW · CARRY TOGETHER", Settings.Mobile ? "Tap Workers, then the straw: two or more carry it to the water" : "Two or more workers (F3), then right-click: they carry it to the water");
            }
            else if (m.Stage == ExpeditionStage.Fragment && Game.Sim.MutagenById(m.GlandId) is { Home: false } gland)
            {
                guiding = true;
                if (gland.Carried) Game.Guide.Show(Game.Sim.NestEntrance(Local.Me), "THE QUEEN", "The gland is heavy: its carrier walks slowly home");
                else Game.Guide.Show(Expedition.Island, "DEAD QUEEN · MUTAGEN", Settings.Mobile ? "Tap Workers, then the glowing gland on the island" : "Workers (F3), then right-click the glowing gland on the island");
            }
        }
        if (!guiding && _guiding) Game.Guide.Clear();
        _guiding = guiding;
        if (_list != null)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < m.Steps; i++)
                sb.Append(i < m.Step ? "✓  " : i == m.Step ? "▶  " : "○  ").Append(m.StepTitle(i)).Append(i < m.Steps - 1 ? "\n" : "");
            _list.Text = sb.ToString();
            _stake.Text = m.Stake(Game.Sim.Time);
        }
        if (Engine.GetProcessFrames() % 15 == 0) RefreshStars(m);
        int shown = m.Episode == 1 ? (int)m.Stage : m.Step;
        if (_shown != shown)
        {
            if (_shown >= 0 && m.Episode > 1 && !m.Complete) Game.Hud.Alert($"Objective: {m.Title}", UiTheme.Gold);
            _shown = shown;
            Game.Audio.Ui("research", -5);
            if (m.Episode == 1 && m.Stage == ExpeditionStage.Incubator) Game.SetNestView(true);
        }
    }

    /// The two optional goals with their live numbers: lit while still within reach.
    void RefreshStars(Expedition m)
    {
        var sim = Game.Sim;
        var rows = new System.Collections.Generic.List<(bool, string)>();
        foreach (var (goal, value, text) in m.Info.Stars)
        {
            float t = sim.Time;
            (bool on, string live) = goal switch
            {
                StarGoal.Time => (t <= value, $"{(int)t / 60}:{(int)t % 60:00} of {(int)value / 60}:{(int)value % 60:00}"),
                StarGoal.Lost => (sim.Col[0].Lost <= value, $"{sim.Col[0].Lost} lost of {value:0}"),
                StarGoal.QueenHp => (m.QueenLow >= value, $"lowest {m.QueenLow * 100:0}%"),
                StarGoal.Escort => (m.EscortId >= 0 && sim.Valid(m.EscortId, m.EscortSerial), ""),
                StarGoal.Stag => (sim.StagTeam == 0 && sim.Valid(sim.StagId, sim.StagSerial), sim.StagTeam == 0 ? "with you" : "not yet roused"),
                StarGoal.AllSteps => (true, $"{Mathf.Min(m.Step, m.Steps - 1)} of {m.Steps - 1}"),
                StarGoal.Master => (true, m.MastersSlain > 0 ? "slain" : "still at the hub"),
                StarGoal.LarvaSafe => (m.LarvaLow >= 0.99f, $"larva at {m.LarvaLow * 100:0}%"),
                _ => (true, ""),
            };
            rows.Add((on, live == "" ? text : $"{text}  ·  {live}"));
        }
        _stars.Set(rows);
    }

    /// On phones and tablets the card sits under the top bar, opposite the minimap, as large as fits.
    void Arrange()
    {
        var hud = Game.Hud.CommandHud;
        bool flexible = hud is { Flexible: true };
        if (flexible == _flexible && !flexible) return;
        _flexible = flexible;
        if (!flexible) { Scale = Vector2.One; UiTheme.Place(this, 1, 0, 1, 0, -445, 70, -16, _desktopBottom); return; }
        AnchorLeft = AnchorRight = AnchorTop = AnchorBottom = 0;
        Size = new Vector2(430, 0); // shrink to the content height
        Scale = Vector2.One * hud.GuideScale;
        var viewport = GetViewportRect().Size;
        Position = new Vector2(viewport.X - 12 - Size.X * hud.GuideScale, hud.TopEdge + 10);
    }
    bool _flexible, _guiding;
    float _desktopBottom;

    /// The objectives are written for mouse and keyboard; on a touch screen the same steps are taps.
    public static string ForTouch(string text)
    {
        foreach (var (mouse, touch) in TouchWords) text = text.Replace(mouse, touch);
        return text;
    }
    static readonly (string, string)[] TouchWords =
    {
        ("Press F2 to select Burr's soldiers. A ground beetle blocks the alley: right-click it, or press A and click beyond it.", "Tap Army to select Burr's soldiers. A ground beetle blocks the alley: tap it, or tap Attack and then the ground beyond it."),
        ("right-click the food clearing", "tap the food clearing"),
        ("Select two or more workers (F3) and right-click a straw", "Tap Workers, then tap a straw"),
        ("(F3, then right-click the glowing gland)", "(tap Workers, then tap the glowing gland)"),
        ("Press F3 to select workers, then click Dig entrance below.", "Tap Workers to select them, then tap Dig entrance below."),
        ("Press N, choose Brood Chamber, then click beside the royal chamber.", "Tap Build, choose Brood Chamber, then tap beside the royal chamber."),
        ("Choose Storage and click beside a connected room.", "Choose Storage and tap beside a connected room."),
        (" with Z or the Worker portrait", " with the Worker portrait"),
        (" with X or the Soldier portrait", " with the Soldier portrait"),
        ("Open the Chambers tab (N), choose Storage and click beside a connected room.", "Open the Chambers tab, choose Storage and tap beside a connected room."),
        ("Press F2 for your squads and attack it together", "Tap Army for your squads and tap it to attack together"),
        ("Hatch more workers (Z)", "Hatch more workers"),
        ("Hatch soldiers (X)", "Hatch soldiers"),
        ("Attack-move your army onto them.", "Tap Army, then Attack and the ground beyond them."),
        ("attack-move onto the red mound", "tap Attack and then the red mound"),
        ("raise the Warlord (Y)", "raise the Warlord"),
        // the level system's levels (Levels.cs)
        ("Open the Research tab and research Wings (W).", "Open the Research tab and research Wings."),
        ("Hatch flying ants (F) and acid gunners (D).", "Hatch flying ants and acid gunners."),
        ("(right-click it with workers selected)", "(tap Workers, then tap it)"),
        ("(F2, then right-click them)", "(tap Army, then tap them)"),
        ("(F2, right-click near her)", "(tap Army, then tap near her)"),
        ("Select the whole army (F2, F4 for flyers) and attack-move to the gold ring past the middle.", "Tap Army, then Attack and the gold ring past the middle."),
        ("Majors can hatch now (C)", "Majors can hatch now"),
        ("press X (or the Soldier card)", "tap the Soldier card"),
        // the briefing's lessons (Reveal.Systems)
        ("Hatch workers (Z)", "Hatch workers"),
        ("giant workers (C)", "giant workers"),
        ("the Warlord (Y)", "the Warlord"),
        ("flying ants (F) and acid gunners (D)", "flying ants and acid gunners"),
        ("attack-move onto them", "tap Attack and then the ground beyond them"),
        ("Hatch more (Z)", "Hatch more"),
        ("(Y for the Warlord)", "(the Warlord first)"),
        ("Open the Chambers tab (N), choose Storage and click beside a connected room. Every store", "Open the Chambers tab, choose Storage and tap beside a connected room. Every store"),
    };
}
