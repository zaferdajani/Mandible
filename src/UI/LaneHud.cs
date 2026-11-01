using Godot;

namespace Mandible;

/// Lane War part of the HUD: your hero's card, ability, shop and the state of the war.
public partial class Hud
{
    Label _heroName, _heroStats, _ability, _respawn, _score;
    ProgressBar _heroHp, _heroXp;
    readonly Button[] _shop = new Button[4];
    AntPortrait _heroPortrait;

    void BuildLanePanel()
    {
        var p = new PanelContainer();
        UiTheme.Place(p, 1, 1, 1, 1, -452, -420, -12, -30);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 6);
        p.AddChild(v);

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 10);
        _heroPortrait = new AntPortrait { Type = Game.Heroes[Local.Me], Team = Local.Me, CustomMinimumSize = new Vector2(110, 96) };
        top.AddChild(_heroPortrait);
        var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _heroName = UiTheme.Text("", 18, UiTheme.Title, UiTheme.Gold);
        _heroStats = UiTheme.Text("", 13, null, UiTheme.Dim);
        _heroHp = new ProgressBar { CustomMinimumSize = new Vector2(0, 12), ShowPercentage = false, MaxValue = 1 };
        _heroXp = new ProgressBar { CustomMinimumSize = new Vector2(0, 6), ShowPercentage = false, MaxValue = 1, Modulate = new Color(0.6f, 0.8f, 1f) };
        info.AddChild(_heroName);
        info.AddChild(_heroHp);
        info.AddChild(_heroXp);
        info.AddChild(_heroStats);
        top.AddChild(info);
        v.AddChild(top);

        _ability = UiTheme.Text("", 15, UiTheme.Bold, UiTheme.Ink);
        var cast = new Button { CustomMinimumSize = new Vector2(0, 40), TooltipText = Lane.HeroCard(Game.Heroes[Local.Me]) };
        cast.AddChild(_ability);
        UiTheme.Place(_ability, 0, 0, 1, 1, 12, 0, -8, 0);
        _ability.VerticalAlignment = VerticalAlignment.Center;
        cast.Pressed += () => Game.TryAbility();
        v.AddChild(cast);

        v.AddChild(UiTheme.Text("SHOP  ·  paid in food: kill bounties and your foragers", 12, UiTheme.Bold, UiTheme.Dim));
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        for (int i = 0; i < 4; i++)
        {
            int item = i;
            var b = new Button { CustomMinimumSize = new Vector2(212, 44), TooltipText = Lane.ItemDesc[i], ClipText = true };
            b.AddThemeFontSizeOverride("font_size", 14);
            b.Pressed += () => Game.TryBuy(item);
            grid.AddChild(b);
            _shop[i] = b;
        }
        v.AddChild(grid);

        var cmds = new HBoxContainer();
        cmds.AddThemeConstantOverride("separation", 6);
        foreach (var (label, act) in new (string, System.Action)[]
        {
            ("Attack  A", () => Game.Input.BeginAttackMove()), ("Stop  S", () => Game.Input.Stop(false)),
            ("Hold  H", () => Game.Input.Stop(true)), ("Hero  Space", () => Game.Input.FocusHero()),
        })
        {
            var b = new Button { Text = label, CustomMinimumSize = new Vector2(103, 38) };
            b.AddThemeFontSizeOverride("font_size", 13);
            b.Pressed += () => { Game.Audio.Ui("ui_click"); act(); };
            cmds.AddChild(b);
        }
        v.AddChild(cmds);
        _root.AddChild(p);

        _respawn = UiTheme.Text("", 30, UiTheme.Title, new Color(1, 0.55f, 0.4f));
        _respawn.HorizontalAlignment = HorizontalAlignment.Center;
        _respawn.MouseFilter = Control.MouseFilterEnum.Ignore;
        UiTheme.Place(_respawn, 0.5f, 0.5f, 0.5f, 0.5f, -400, -120, 400, -70);
        _root.AddChild(_respawn);

        _score = UiTheme.Text("", 15, UiTheme.Bold, UiTheme.Ink);
        _score.MouseFilter = Control.MouseFilterEnum.Ignore;
        UiTheme.Place(_score, 0, 0, 0, 0, 22, 50, 1400, 72);
        _root.AddChild(_score);
    }

    const string LaneHint = "Right-click to move or attack  ·  Q ability  ·  1-4 shop  ·  Space find hero  ·  click anything to inspect it";

    void RefreshLanes()
    {
        var sim = Game.Sim;
        int me = Local.Me, foe = Local.Foe;
        int id = sim.HeroId[me];
        var type = sim.HeroType[me];
        if (id >= 0)
        {
            ref var h = ref sim.Ants[id];
            _heroName.Text = $"{Config.Of(type).Name.ToUpper()}  ·  LEVEL {h.Level}";
            _heroHp.Value = h.Hp / h.MaxHp;
            float next = Sim.NextXp(h);
            _heroXp.Value = next <= 0 ? 1 : h.Xp / next;
            _heroStats.Text = $"HP {(int)h.Hp} / {(int)h.MaxHp}  ·  Bite {Config.Of(type).Damage * h.DmgMul:0}  ·  Armour {Config.Of(type).Armor * 100:0}%  ·  Speed {Config.Of(type).Speed * h.SpeedMul:0.#}";
            _heroPortrait.TooltipText = Lane.HeroCard(type);
            _ability.Text = h.AbilityCd > 0 ? $"Q   {Config.AbilityName(type)}   ·   {Mathf.Ceil(h.AbilityCd):0}s" : $"Q   {Config.AbilityName(type)}   ·   READY";
            _ability.AddThemeColorOverride("font_color", h.AbilityCd > 0 ? UiTheme.Dim : new Color(0.6f, 1f, 0.55f));
            _respawn.Text = "";
        }
        else
        {
            _heroName.Text = $"{Config.Of(type).Name.ToUpper()}  ·  FALLEN";
            _heroHp.Value = 0;
            _heroStats.Text = "Returning to the mound...";
            _ability.Text = $"Q   {Config.AbilityName(type)}";
            _respawn.Text = $"Your {Config.Of(type).Name} returns in {Mathf.Ceil(sim.HeroRespawn[me]):0}s";
        }
        for (int i = 0; i < 4; i++)
        {
            int tier = sim.Items[me, i];
            bool maxed = i < 3 && tier >= Lane.MaxItemTier;
            string tierText = i == 3 ? sim.JellyCd[me] > 0 ? $"  ·  {Mathf.Ceil(sim.JellyCd[me]):0}s" : "" : maxed ? " (max)" : $" {Roman(tier + 1)}";
            _shop[i].Text = maxed ? $"{i + 1}  {Lane.ItemNames[i]}{tierText}" : $"{i + 1}  {Lane.ItemNames[i]}{tierText}  ·  {Lane.ItemCost(i, tier)}";
            _shop[i].Disabled = !sim.CanBuy(me, i);
        }
        float wave = Lane.WaveEvery - ((sim.Time - Lane.FirstWave) % Lane.WaveEvery + Lane.WaveEvery) % Lane.WaveEvery;
        if (sim.Time < Lane.FirstWave) wave = Lane.FirstWave - sim.Time;
        string shield = sim.QueenShielded(foe) ? "enemy queen shielded by her guardians" : "ENEMY QUEEN EXPOSED";
        string herd = sim.HerdHere
            ? sim.HerdTeam < 0 ? "Aphid herd: unclaimed" : $"Aphid herd: {(sim.HerdTeam == me ? "you" : "enemy")} milking {sim.HerdBar * 100:0}%"
            : sim.Time < Lane.StagWake ? $"Aphid herd in {Mathf.Ceil(sim.HerdNext - sim.Time):0}s" : "";
        string stag = sim.StagAwake
            ? sim.StagTeam < 0 ? "OLD STAG AWAKE" : $"Old Stag marches for {(sim.StagTeam == me ? "YOU" : "the enemy")}"
            : $"Old Stag in {Mathf.Ceil(sim.StagNext - sim.Time):0}s";
        _score.Text = $"Guardians  {sim.GuardiansLeft(me)} : {sim.GuardiansLeft(foe)}    Hero kills  {sim.HeroKills[me]} : {sim.HeroKills[foe]}    Next wave {wave:0}s    {shield}    {herd}    {stag}";
    }

    static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", _ => n.ToString() };
}
