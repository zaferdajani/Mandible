using Godot;

namespace Mandible;

/// Presentation for the mission's authoritative objectives. No skip button can unlock
/// rooms or accidentally turn the introductory building lesson into an enemy raid.
public partial class ExpeditionGuide : PanelContainer
{
    public GameSession Game;
    Label _heading, _body, _list, _stake;
    Button _dig;
    int _shown = -1;
    public override void _Ready()
    {
        var m = Game.Sim.Mission;
        bool steps = m != null && m.Episode > 1;
        UiTheme.Place(this, 1, 0, 1, 0, -445, 70, -16, steps ? 392 : 290);
        var column = new VBoxContainer(); AddChild(column);
        column.AddChild(UiTheme.Text($"EPISODE {Game.Episode} · {CampaignEpisodes.Name(Game.Episode).ToUpperInvariant()}", 12, UiTheme.Bold, UiTheme.Dim));
        _heading = UiTheme.Text("", 21, UiTheme.Title, UiTheme.Gold); column.AddChild(_heading);
        _body = UiTheme.Text("", 15); _body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _body.CustomMinimumSize = new Vector2(380, 80); column.AddChild(_body);
        if (steps)
        {
            // the whole episode at a glance: done, current and still to come
            _list = UiTheme.Text("", 14, UiTheme.Bold, UiTheme.Ink); column.AddChild(_list);
            _stake = UiTheme.Text("", 13, null, UiTheme.Dim); _stake.AutowrapMode = TextServer.AutowrapMode.WordSmart; column.AddChild(_stake);
        }
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
            string stake = "Lose if your queen dies";
            if (m.FlightAt > 0)
            {
                int left = Mathf.Max(0, (int)(m.FlightAt - Game.Sim.Time));
                stake += $" or the princesses fly: {left / 60}:{left % 60:00} left";
            }
            _stake.Text = stake + ".";
        }
        int shown = m.Episode == 1 ? (int)m.Stage : m.Step;
        if (_shown != shown)
        {
            if (_shown >= 0 && m.Episode > 1 && !m.Complete) Game.Hud.Alert($"Objective: {m.Title}", UiTheme.Gold);
            _shown = shown;
            Game.Audio.Ui("research", -5);
            if (m.Episode == 1 && m.Stage == ExpeditionStage.Incubator) Game.SetNestView(true);
        }
    }
}
