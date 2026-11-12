using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Level 1's founding, shown where the eye goes first (top centre), from the moment the queen reaches the
/// clearing until she is inside: how much food and how many remains the site still needs, one big button to dig,
/// then the dig itself with the time left. The side card keeps the story; this keeps the numbers.
public partial class FoundingPanel : PanelContainer
{
    public GameSession Game;
    Label _title, _hint;
    ProgressBar _food, _remains, _dig;
    Label _foodText, _remainsText, _digText;
    Button _button;

    static readonly Color FoodFill = new(0.93f, 0.78f, 0.42f), RemainsFill = new(0.78f, 0.36f, 0.24f), DigFill = new(0.62f, 0.86f, 0.55f);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.05f, 0.04f, 0.03f, 0.94f), UiTheme.Gold, 2, 8, 14));
        UiTheme.Place(this, 0.5f, 0, 0.5f, 0, -300, 64, 300, 64);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        AddChild(v);
        _title = UiTheme.Text("", 22, UiTheme.Title, UiTheme.Gold);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(_title);
        (_food, _foodText) = Bar(v, FoodFill);
        (_remains, _remainsText) = Bar(v, RemainsFill);
        (_dig, _digText) = Bar(v, DigFill);
        _hint = UiTheme.Text("", 15, null, UiTheme.Ink);
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        _hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hint.CustomMinimumSize = new Vector2(560, 0);
        v.AddChild(_hint);
        _button = new Button { Text = Settings.Mobile ? "DIG THE NEST" : "DIG THE NEST   (Enter)", CustomMinimumSize = new Vector2(320, 58) };
        _button.AddThemeFontOverride("font", UiTheme.Title);
        _button.AddThemeFontSizeOverride("font_size", 24);
        _button.AddThemeColorOverride("font_color", new Color(0.08f, 0.05f, 0.02f));
        _button.AddThemeColorOverride("font_hover_color", new Color(0.08f, 0.05f, 0.02f));
        _button.AddThemeStyleboxOverride("normal", UiTheme.Box(UiTheme.Gold, new Color(1f, 0.9f, 0.6f), 2, 8, 10));
        _button.AddThemeStyleboxOverride("hover", UiTheme.Box(new Color(1f, 0.82f, 0.45f), new Color(1f, 0.95f, 0.75f), 2, 8, 10));
        _button.Pressed += Dig;
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddChild(_button);
        v.AddChild(row);
        Visible = false;
    }

    /// A labelled bar: dark track, bright fill, the numbers written on it in high-contrast white.
    static (ProgressBar, Label) Bar(Container parent, Color fill)
    {
        var bar = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(560, 30), MinValue = 0, MaxValue = 1 };
        bar.AddThemeStyleboxOverride("background", UiTheme.Box(new Color(0.12f, 0.09f, 0.06f), new Color(0.3f, 0.22f, 0.12f), 1, 5, 0));
        bar.AddThemeStyleboxOverride("fill", UiTheme.Box(fill, fill, 0, 5, 0));
        var text = UiTheme.Text("", 16, UiTheme.Bold, Colors.White);
        text.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
        text.AddThemeConstantOverride("outline_size", 6);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
        UiTheme.Place(text, 0, 0, 1, 1, 0, 0, 0, 0);
        bar.AddChild(text);
        parent.AddChild(bar);
        return (bar, text);
    }

    public override void _Process(double delta)
    {
        var m = Game.Sim.Mission;
        bool show = m is { Episode: 1 } && m.Stage is ExpeditionStage.Provision or ExpeditionStage.DigEntrance or ExpeditionStage.RoyalChamber or ExpeditionStage.Bridge;
        Visible = show;
        if (!show) return;
        var sim = Game.Sim;
        var c = sim.Col[Local.Me];
        bool stocking = m.Stage is ExpeditionStage.Provision or ExpeditionStage.DigEntrance;
        _food.Visible = _remains.Visible = stocking;
        _dig.Visible = m.Stage is ExpeditionStage.RoyalChamber or ExpeditionStage.Bridge;
        _button.Visible = m.Stage == ExpeditionStage.DigEntrance;
        if (stocking)
        {
            float food = Mathf.Min(c.Food, Config.FoundFood), org = Mathf.Min(c.Organic, Config.FoundOrganic);
            _food.Value = food / Config.FoundFood;
            _remains.Value = org / Config.FoundOrganic;
            _foodText.Text = $"FOOD  {food:0} / {Config.FoundFood:0}" + (food >= Config.FoundFood ? "   ✓" : "");
            _remainsText.Text = $"PROTEIN (REMAINS)  {org:0} / {Config.FoundOrganic:0}" + (org >= Config.FoundOrganic ? "   ✓" : "");
        }
        if (m.Stage == ExpeditionStage.Provision)
        {
            _title.Text = "STOCK THE NEST SITE";
            int need = Config.HaulNeed(AntType.Beetle);
            string drag = !m.RemainsLeft ? "" : m.RemainsCrew >= need ? $"\n{m.RemainsCrew} workers are dragging the beetle home: {m.RemainsToGo:0} paces to go"
                : m.RemainsCrew > 0 ? $"\n{m.RemainsCrew} of {need} workers on the beetle: it needs {need} to move" : "";
            _hint.Text = "A queen founds her nest only where food and protein are stored. " + m.StockNeeded + "." + drag + "\n"
                + (!m.RemainsLeft ? "Workers are gathering. When both bars are full, you can dig the nest."
                   : Settings.Touch ? "Workers fetch seeds themselves. Tap Workers, then tap the beetle: four or more drag it here whole."
                   : "Workers fetch seeds themselves. Send them to the beetle: Workers (F3), then right-click it. Four or more drag it here whole.");
        }
        else if (m.Stage == ExpeditionStage.DigEntrance)
        {
            _title.Text = "THE SITE IS READY";
            _hint.Text = "Press the button: the workers at the clearing dig the shaft and the royal chamber.";
        }
        else if (m.Stage == ExpeditionStage.Bridge)
        {
            int crew = m.NextStraw?.HoldCount ?? 0;
            _title.Text = "BRIDGE THE WATER";
            _dig.Value = (float)m.StrawsLaid / Expedition.StrawGoal;
            _digText.Text = $"STRAWS LAID  {m.StrawsLaid} / {Expedition.StrawGoal}" + (crew > 0 ? $"   ·   {crew} worker{(crew == 1 ? "" : "s")} on the straw" + (crew < Expedition.StrawCrew ? ": it needs two" : "") : "");
            _hint.Text = Settings.Touch
                ? "A straw is too long for one ant. Tap Workers, then tap a straw: two or more carry it, and more carry it faster."
                : "A straw is too long for one ant. Workers (F3), then right-click a straw: two or more carry it, and more carry it faster.";
        }
        else
        {
            var royal = sim.Royal(Local.Me);
            float p = royal?.Progress ?? 0;
            int diggers = royal?.Diggers ?? 0;
            float left = royal == null ? 0 : (royal.Work - royal.Done) / Mathf.Max(1, diggers);
            _title.Text = "DIGGING THE NEST";
            _dig.Value = p;
            _digText.Text = $"{p * 100:0}%   ·   {diggers} worker{(diggers == 1 ? "" : "s")} digging   ·   about {(int)left / 60}:{(int)left % 60:00} left";
            _hint.Text = "The queen walks down into her royal chamber as soon as it is dug.";
        }
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (!Visible || !_button.Visible) return;
        if (e is InputEventKey { Pressed: true, Echo: false } k && (k.Keycode == Key.Enter || k.Keycode == Key.KpEnter)) { Dig(); GetViewport().SetInputAsHandled(); }
    }

    /// Digs with the selected workers, or, when none are selected, with every worker at the clearing.
    void Dig()
    {
        var sim = Game.Sim;
        var diggers = new List<int>();
        foreach (int id in Game.Input.Selection) if (sim.Ants[id].Alive && sim.Ants[id].Type == AntType.Worker) diggers.Add(id);
        if (diggers.Count == 0)
            for (int i = 0; i < sim.High; i++)
                if (sim.Ants[i].Alive && sim.Ants[i].Team == Local.Me && sim.Ants[i].Type == AntType.Worker && sim.Ants[i].Pos.DistanceTo(Expedition.Clearing) < 30) diggers.Add(i);
        Game.Issue(Cmd.Units(CmdKind.Entrance, Local.Me, diggers, sim)); // an order like any other, so saved games replay it
        if (sim.Mission is { EntranceOrdered: true }) { Game.Audio.Ui("ui_queue"); Game.SetNestView(true); }
        else Game.Hud.Alert("No workers at the clearing yet: bring them to the nest site.");
    }
}
