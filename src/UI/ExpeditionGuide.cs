using Godot;

namespace Mandible;

/// Presentation for the mission's authoritative objectives. No skip button can unlock
/// rooms or accidentally turn the introductory building lesson into an enemy raid.
public partial class ExpeditionGuide : PanelContainer
{
    public GameSession Game;
    Label _goal, _heading, _body, _list, _stake;
    StarList _stars;
    Button _dig;
    int _shown = -1;
    public override void _Ready()
    {
        var m = Game.Sim.Mission;
        bool steps = m != null && m.Episode > 1;
        UiTheme.Place(this, 1, 0, 1, 0, -445, 70, -16, steps ? 520 : 384);
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
        var row = new HBoxContainer(); column.AddChild(row);
        var show = new Button { Text = "Show objective" }; row.AddChild(show);
        show.Pressed += () => Game.Camera.PanTo(Game.Sim.Mission.Marker);
        _dig = new Button { Text = "Dig entrance" }; row.AddChild(_dig);
        _dig.Pressed += () =>
        {
            if (Game.Sim.OrderEntrance(Local.Me, Game.Input.Selection)) { Game.Audio.Ui("ui_queue"); Game.SetNestView(true); }
            else Game.Hud.Alert("Select workers at the food clearing with F3 first.");
        };
    }
    public override void _Process(double delta)
    {
        var m = Game.Sim.Mission;
        if (m == null) return;
        _heading.Text = m.Title;
        _body.Text = m.Instruction;
        _dig.Visible = m.Episode == 1 && m.Stage == ExpeditionStage.DigEntrance;
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
                _ => (true, ""),
            };
            rows.Add((on, live == "" ? text : $"{text}  ·  {live}"));
        }
        _stars.Set(rows);
    }
}
