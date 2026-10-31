using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The guided first battle: one short goal at a time, with what to press and why it matters.
/// Each step completes itself the moment the player has done it; the red colony stays peaceful
/// until the lesson is over.
public partial class Tutorial : Control
{
    public GameSession Game;
    public Action OnFinished;

    sealed class Step
    {
        public string Title, Do, Why;
        public Func<bool> Done;
        public Func<Vector2?> Where; // a place to point at, if any
        public Action Begin;
    }

    readonly List<Step> _steps = new();
    int _i = -1;
    float _check, _doneT = -1;
    Label _head, _title, _do, _why;
    Button _show;
    Label3D _marker;
    // snapshots taken when a step begins
    Vector3 _camAt;
    float _camDist;
    int _startCount, _startQueue, _startWild, _base;

    public int StepIndex => _i;
    public string StepTitle => _i >= 0 && _i < _steps.Count ? _steps[_i].Title : "";

    Sim Sim => Game.Sim;
    Colony Me => Game.Sim.Col[Local.Me];

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        UiTheme.Place(this, 0, 0, 0, 0, 16, 104, 476, 420);
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.07f, 0.055f, 0.04f, 0.93f), new Color(UiTheme.Gold, 0.75f), 2, 10, 14));
        UiTheme.Place(p, 0, 0, 1, 0, 0, 0, 0, 0);
        AddChild(p);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 6);
        p.AddChild(v);
        _head = UiTheme.Text("", 12, UiTheme.Bold, UiTheme.Dim);
        _title = UiTheme.Text("", 21, UiTheme.Title, UiTheme.Gold);
        _do = UiTheme.Text("", 16, UiTheme.Bold, UiTheme.Ink);
        _why = UiTheme.Text("", 14, null, new Color(0.72f, 0.85f, 0.62f));
        foreach (var l in new[] { _do, _why }) { l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.CustomMinimumSize = new Vector2(420, 0); }
        v.AddChild(_head);
        v.AddChild(_title);
        v.AddChild(_do);
        v.AddChild(_why);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        _show = Btn("Show me", () => { var w = _steps[_i].Where?.Invoke(); if (w.HasValue) Game.Camera.PanTo(w.Value); });
        row.AddChild(_show);
        row.AddChild(Btn("Skip step", () => Next()));
        row.AddChild(Btn("End tutorial", Finish));
        v.AddChild(row);

        _marker = new Label3D
        {
            Text = "▼", FontSize = 96, OutlineSize = 18, PixelSize = 0.04f, NoDepthTest = true,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Modulate = new Color(1f, 0.82f, 0.3f), Visible = false,
        };
        Game.AddChild(_marker);

        Build();
        Next();
    }

    Button Btn(string text, Action act)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, 34) };
        b.AddThemeFontSizeOverride("font_size", 13);
        b.Pressed += () => { Game.Audio.Ui("ui_click"); act(); };
        return b;
    }

    void Build()
    {
        var nest = Game.World.Nests[Local.Me].Pos;
        var toCenter = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - nest).Normalized();
        Add("Look around",
            "Move the camera with the arrow keys (or push the mouse to the screen edge) and zoom with the mouse wheel. Hold the middle mouse button to turn.",
            "You command from above. Ants are tiny, so zoom in to see them and zoom out to see the whole yard.",
            () => Game.Camera.Focus.DistanceTo(_camAt) > 15 || Mathf.Abs(Game.Camera.Distance - _camDist) > 15,
            begin: () => { _camAt = Game.Camera.Focus; _camDist = Game.Camera.Distance; });
        Add("Select your soldiers",
            "Drag a box with the left mouse button around the soldiers standing in front of your nest.",
            "Soldiers are your fighters. They hatch in squads of six that rank up together.",
            () => CountSelected(AntType.Soldier) >= 3, () => nest + toCenter * 8);
        Add("Move them",
            "With the soldiers selected, right-click on open ground to send them there.",
            "Right-click is your main order: move, attack an enemy, or (with workers) collect food.",
            () => Game.Input.Moves + Game.Input.AttackMoves > _base, () => nest + toCenter * 26,
            begin: () => _base = Game.Input.Moves + Game.Input.AttackMoves);
        Add("Save a group",
            "Press Ctrl + 1 to save the selected soldiers as group 1. Then click empty ground to deselect, and press 1 to select them again instantly.",
            "Groups let you grab your army in a single key press in the middle of a fight. The group buttons appear above the selection panel.",
            () => Game.Input.Recalls > _base,
            begin: () => _base = Game.Input.Recalls);
        Add("See how ants find food",
            "Press Tab to show your colony's scent trails, then press Tab again to hide them.",
            "Workers forage on their own: scouts wander, and the one that finds food lays a scent trail home that others follow. You never have to order them to eat.",
            () => Game.TrailToggles >= _base + 2,
            begin: () => _base = Game.TrailToggles);
        Add("Inspect some food",
            "Left-click any food pile (a crumb, seed or berry) to see what it is, how much is left and what it does for your colony.",
            "Each food has a different effect: sugar speeds workers, fruit heals, insect protein makes the queen lay faster. Your race also prefers some foods.",
            () => Game.Input.InspectFood != null, () => NearestFood(nest));
        Add("Hatch a worker",
            "Press Z (or click the first brood button) to have the queen lay a worker for 15 food.",
            "More workers bring more food, and food pays for everything else.",
            () => Me.Count[(int)AntType.Worker] > _startCount || Me.Queue.Exists(q => q.Kind == ProdKind.Worker),
            begin: () => _startCount = Me.Count[(int)AntType.Worker]);
        Add("Hatch a squad",
            "Press X to hatch a squad of six soldiers.",
            "Your army grows a squad at a time. Watch the brood count in the top bar: when it is full, press V to dig a new chamber for more brood.",
            () => Me.Queue.Exists(q => q.Kind == ProdKind.Soldier) || Me.Count[(int)AntType.Soldier] > _startCount,
            begin: () => _startCount = Me.Count[(int)AntType.Soldier]);
        Add("Raise your queen",
            "Press Q to feed the queen royal jelly. She becomes a Matriarch.",
            "Queen levels unlock new castes (majors, gunners, flyers) and heroes. Every finished upgrade digs a chamber beside the nest: those holes with a name over them.",
            () => Me.Researching || Me.T(Upgrade.Queen) >= 2, () => nest);
        Add("Clear a wild lair",
            "Press F2 to select your whole army, then press A and click the marked lair. Your ants attack everything on the way.",
            "Wild bugs guard rich food all over the yard. Killing them ranks up your squads and leaves a carcass your workers carry home as protein.",
            () => Sim.WildCount < _startWild, () => NearestCamp(nest),
            begin: () => _startWild = Sim.WildCount);
        Add("Heal and refill",
            "Select a squad that lost members and press B. It walks home, heals and re-hatches its fallen (for food).",
            "Veteran squads keep their rank when refilled, so bring them home instead of throwing them away.",
            () => Sim.Squads.Exists(s => s.Team == Local.Me && s.Recalled) || Sim.Time - _stepStart > 40);
        Add("Win the war",
            "You know the basics. Build up, raise your queen, hatch heroes, and destroy the enemy queen across the yard. The red colony now starts its attacks.",
            "Press F1 any time for help. The main menu's Field Guide explains every caste, bug and food.",
            () => false);
    }

    float _stepStart;

    void Add(string title, string doText, string why, Func<bool> done, Func<Vector2?> where = null, Action begin = null)
        => _steps.Add(new Step { Title = title, Do = doText, Why = why, Done = done, Where = where, Begin = begin });

    void Next()
    {
        _i++;
        _doneT = -1;
        if (_i >= _steps.Count) { Finish(); return; }
        var s = _steps[_i];
        _stepStart = Sim.Time;
        s.Begin?.Invoke();
        _head.Text = $"TUTORIAL  ·  STEP {_i + 1} OF {_steps.Count}";
        _title.Text = s.Title;
        _do.Text = s.Do;
        _why.Text = "Why: " + s.Why;
        _show.Visible = s.Where != null;
        if (_i == _steps.Count - 1) OnFinished?.Invoke(); // last card: the war is on
    }

    void Finish()
    {
        _marker.QueueFree();
        OnFinished?.Invoke();
        QueueFree();
    }

    public override void _Process(double delta)
    {
        if (_i < 0 || _i >= _steps.Count) return;
        var s = _steps[_i];
        var where = s.Where?.Invoke();
        _marker.Visible = where.HasValue;
        if (where.HasValue)
        {
            var p = where.Value;
            float bob = Mathf.Sin(Time.GetTicksMsec() * 0.005f) * 0.6f;
            _marker.Position = new Vector3(p.X, Game.World.HF.Sample(p.X, p.Y) + 7f + bob, p.Y);
        }
        if (_doneT >= 0)
        {
            _doneT -= (float)delta;
            if (_doneT < 0) Next();
            return;
        }
        _check -= (float)delta;
        if (_check > 0) return;
        _check = 0.25f;
        if (s.Done())
        {
            _doneT = 1.2f;
            _title.Text = "✓  " + s.Title;
            Game.Audio.Ui("research", -4);
        }
    }

    int CountSelected(AntType t)
    {
        int n = 0;
        foreach (int id in Game.Input.Selection) if (Sim.Ants[id].Alive && Sim.Ants[id].Type == t) n++;
        return n;
    }

    Vector2? NearestFood(Vector2 from)
    {
        FoodSource best = null;
        foreach (var f in Game.World.Food)
            if (f.Alive && f.Kind != FoodKind.Corpse && (best == null || f.Pos.DistanceTo(from) < best.Pos.DistanceTo(from)) && f.Pos.DistanceTo(from) > 12) best = f;
        return best?.Pos;
    }

    Vector2? NearestCamp(Vector2 from)
    {
        Vector2? best = null;
        for (int i = 0; i < Sim.High; i++)
        {
            ref var a = ref Sim.Ants[i];
            if (!a.Alive || a.Team != Config.Wild) continue;
            if (best == null || a.Home.DistanceTo(from) < best.Value.DistanceTo(from)) best = a.Home;
        }
        return best;
    }
}
