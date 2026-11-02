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
        Game.Hud.EpisodeCard = this;
        UiTheme.Place(this, 1, 0, 1, 0, -445, 70, -16, 290);
        var column = new VBoxContainer(); AddChild(column);
        column.AddChild(UiTheme.Text("EPISODE 1 · THE FIRST NEST", 12, UiTheme.Bold, UiTheme.Dim));
        _heading = UiTheme.Text("", 21, UiTheme.Title, UiTheme.Gold); column.AddChild(_heading);
        _body = UiTheme.Text("", 15); _body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _body.CustomMinimumSize = new Vector2(380, 80); column.AddChild(_body);
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
        _dig.Visible = m.Stage == ExpeditionStage.DigEntrance;
        if (_shown != m.Stage)
        {
            _shown = m.Stage;
            Game.Audio.Ui("research", -5);
            if (m.Stage == ExpeditionStage.Incubator) Game.SetNestView(true);
        }
    }

    /// On phones and tablets the card sits under the top bar, opposite the minimap, as large as fits.
    void Arrange()
    {
        var hud = Game.Hud.CommandHud;
        bool flexible = hud is { Flexible: true };
        if (flexible == _flexible && !flexible) return;
        _flexible = flexible;
        if (!flexible) { Scale = Vector2.One; UiTheme.Place(this, 1, 0, 1, 0, -445, 70, -16, 290); return; }
        AnchorLeft = AnchorRight = AnchorTop = AnchorBottom = 0;
        Size = new Vector2(430, 0); // shrink to the content height
        Scale = Vector2.One * hud.GuideScale;
        var viewport = GetViewportRect().Size;
        Position = new Vector2(viewport.X - 12 - Size.X * hud.GuideScale, hud.TopEdge + 10);
    }
    bool _flexible;

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
    };
}
