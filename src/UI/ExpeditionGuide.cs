using Godot;

namespace Mandible;

/// Presentation for the mission's authoritative objectives. No skip button can unlock
/// rooms or accidentally turn the introductory building lesson into an enemy raid.
public partial class ExpeditionGuide : PanelContainer
{
    public GameSession Game;
    Label _heading, _body;
    Button _dig;
    ExpeditionStage _shown = (ExpeditionStage)(-1);
    public override void _Ready()
    {
        UiTheme.Place(this, 1, 0, 1, 0, -445, 70, -16, 290);
        var column = new VBoxContainer(); AddChild(column);
        column.AddChild(UiTheme.Text("EPISODE 1 · THE FIRST NEST", 12, UiTheme.Bold, UiTheme.Dim));
        _heading = UiTheme.Text("", 21, UiTheme.Title, UiTheme.Gold); column.AddChild(_heading);
        _body = UiTheme.Text("", 15); _body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _body.CustomMinimumSize = new Vector2(380, 80); column.AddChild(_body);
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
        _dig.Visible = m.Stage == ExpeditionStage.DigEntrance;
        if (_shown != m.Stage)
        {
            _shown = m.Stage;
            Game.Audio.Ui("research", -5);
            if (m.Stage == ExpeditionStage.Incubator) Game.SetNestView(true);
        }
    }
}
