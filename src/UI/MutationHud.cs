using Godot;

namespace Mandible;

/// The mutation track on the objective card (campaign, from the Silk Trap on): mutagen in the queen's reserve with its
/// own icon, the colony's level and instability, the mutation card the queen may take, and the royal larva's gauge.
public partial class MutationStrip : VBoxContainer
{
    public GameSession Game;
    Label _counts, _gaugeText;
    ProgressBar _gauge;
    Button _card;
    Mutation _offer;

    public static Texture2D Icon => _icon ??= ResourceLoader.Exists("res://assets/ui/mutagen.png") ? GD.Load<Texture2D>("res://assets/ui/mutagen.png") : null;
    static Texture2D _icon;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 4);
        AddChild(new HSeparator());
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        if (Icon != null) row.AddChild(new TextureRect { Texture = Icon, CustomMinimumSize = new Vector2(30, 30), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered });
        _counts = UiTheme.Text("", 14, UiTheme.Bold, new Color(0.86f, 0.78f, 1f));
        _counts.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(_counts);
        AddChild(row);
        _card = new Button { CustomMinimumSize = new Vector2(0, Settings.Mobile ? 52 : 40), Visible = false };
        _card.AddThemeColorOverride("font_color", new Color(0.92f, 0.86f, 1f));
        _card.Pressed += () =>
        {
            int bit = 0; while (bit < 31 && 1 << bit != (int)_offer) bit++;
            Game.Orders.Issue(new Cmd { Kind = CmdKind.Mutate, Team = (byte)Local.Me, Arg = (byte)bit });
            Game.Audio.Ui("research");
        };
        AddChild(_card);
        _gaugeText = UiTheme.Text("", 13, UiTheme.Bold, UiTheme.Gold);
        AddChild(_gaugeText);
        _gauge = new ProgressBar { CustomMinimumSize = new Vector2(0, 12), ShowPercentage = false, MaxValue = 1 };
        _gauge.AddThemeStyleboxOverride("background", UiTheme.Box(new Color(0.12f, 0.08f, 0.05f, 0.9f), new Color(UiTheme.Bronze, 0.6f), 1, 3, 0));
        _gauge.AddThemeStyleboxOverride("fill", UiTheme.Box(new Color(0.78f, 0.62f, 0.95f), new Color(0.95f, 0.85f, 0.55f), 0, 3, 0));
        AddChild(_gauge);
    }

    public override void _Process(double delta)
    {
        var sim = Game.Sim;
        var m = sim.Mission;
        Visible = m != null && m.Chapter >= Reveal.Mutagen;
        if (!Visible) return;
        var c = sim.Col[0];
        _counts.Text = $"MUTAGEN  {c.Mutagen}   ·   COLONY LEVEL {c.Level}   ·   POWER +{c.Power * Mathf.RoundToInt(Mutations.PowerHp * 100)}%   ·   INSTABILITY {c.Instability}";
        _counts.TooltipText = "Mutagen: carried home from glowing bodies; the queen consumes it to mutate the colony.\nColony level: every queen or nest master defeated; more room and tougher fighters.\nPower: the cores the queen consumed make every ant of the colony tougher and harder-biting (a stronger core, more power).\nInstability: every mutation toughens the woods' predators and rival strains.";
        _offer = m.Fixed;
        bool can = _offer != Mutation.None && (c.Mutations & (int)_offer) == 0;
        _card.Visible = can;
        if (can)
        {
            _card.Disabled = c.Mutagen < 1;
            _card.Text = c.Mutagen < 1 ? $"{Mutations.Name(_offer)}  ·  needs mutagen in the reserve" : $"FEED THE QUEEN  ·  {Mutations.Name(_offer)}";
            _card.TooltipText = Mutations.Text(_offer);
        }
        bool larva = c.LarvaStage > 0;
        _gauge.Visible = _gaugeText.Visible = larva;
        if (!larva) return;
        string hero = Config.Of(c.LarvaHero).Name;
        if (c.LarvaStage == 1) { _gaugeText.Text = $"ROYAL LARVA ({hero})  ·  waiting for its nursery"; _gauge.Value = 0; }
        else if (c.LarvaStage == 2) { _gaugeText.Text = $"ROYAL LARVA ({hero})  ·  fed {(int)c.LarvaFed} / {(int)c.LarvaNeed} organic  ·  health {c.LarvaHp * 100:0}%"; _gauge.Value = c.LarvaFed / Mathf.Max(1, c.LarvaNeed); }
        else { _gaugeText.Text = $"COCOON  ·  bursts in {Mathf.Ceil(c.CocoonT):0}s  ·  health {c.LarvaHp * 100:0}%"; _gauge.Value = 1 - c.CocoonT / Mathf.Max(1, c.CocoonTime); }
    }
}
