using Godot;

namespace Mandible;

/// Presentation for the mission's authoritative objectives. No skip button can unlock
/// rooms or accidentally turn the introductory building lesson into an enemy raid.
public partial class ExpeditionGuide : PanelContainer
{
    public GameSession Game;
    Label _goal, _heading, _body, _list, _stake;
    Button _dig;
    int _shown = -1;
    public override void _Ready()
    {
        Game.Hud.EpisodeCard = this;
        var m = Game.Sim.Mission;
        bool steps = m != null && m.Episode > 1;
        _desktopBottom = steps ? 448 : 318;
        UiTheme.Place(this, 1, 0, 1, 0, -445, 70, -16, _desktopBottom);
        var column = new VBoxContainer(); AddChild(column);
        column.AddChild(UiTheme.Text($"EPISODE {Game.Episode} · {CampaignEpisodes.Name(Game.Episode).ToUpperInvariant()}", 12, UiTheme.Bold, UiTheme.Dim));
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
        var row = new HBoxContainer(); column.AddChild(row);
        var finger = new Vector2(0, Settings.Mobile ? 46 : 0);
        var show = new Button { Text = "Show objective", CustomMinimumSize = finger }; row.AddChild(show);
        show.Pressed += () => Game.Camera.PanTo(Game.Sim.Mission.Marker);
        _dig = new Button { Text = "Dig entrance", CustomMinimumSize = finger }; row.AddChild(_dig);
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
        _body.Text = Settings.Mobile ? ForTouch(m.Instruction) : m.Instruction;
        Arrange();
        _dig.Visible = m.Episode == 1 && m.Stage == ExpeditionStage.DigEntrance;
        if (_list != null)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < m.Steps; i++)
                sb.Append(i < m.Step ? "✓  " : i == m.Step ? "▶  " : "○  ").Append(m.StepTitle(i)).Append(i < m.Steps - 1 ? "\n" : "");
            _list.Text = sb.ToString();
            string stake = m.Step < m.Steps - 1 ? "The steps lead to the red queen; kill her any time to win. Lose if your queen dies" : "Lose if your queen dies";
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
    bool _flexible;
    float _desktopBottom;

    /// The objectives are written for mouse and keyboard; on a touch screen the same steps are taps.
    static string ForTouch(string text)
    {
        foreach (var (mouse, touch) in TouchWords) text = text.Replace(mouse, touch);
        return text;
    }
    static readonly (string, string)[] TouchWords =
    {
        ("Press F2 to select your attackers. Right-click the red ants ahead, or press A and click beyond them.", "Tap Army to select your attackers, then tap the red ants ahead, or tap Attack and then the ground beyond them."),
        ("right-click the food clearing", "tap the food clearing"),
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
    };
}
