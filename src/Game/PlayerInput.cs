using System.Collections.Generic;
using Godot;

namespace Mandible;

public partial class PlayerInput : Node
{
    public GameSession Game;
    public readonly List<int> Selection = new();
    public bool NestSelected;
    enum Mode { None, AttackMove, Gather }
    Mode _mode;
    bool _leftDown;
    Vector2 _dragStart;
    readonly Dictionary<int, List<(int id, int serial)>> _groups = new();
    public Vector2? LastAlert;

    Sim Sim => Game.Sim;
    RtsCamera Cam => Game.Camera;

    public override void _UnhandledInput(InputEvent e)
    {
        if (Game.Hud == null || Game.Hud.Blocking)
        {
            if (e is InputEventKey k0 && k0.Pressed && !k0.Echo && k0.Keycode == Key.Escape) Game.Hud?.TogglePause();
            if (e is InputEventKey k1 && k1.Pressed && !k1.Echo && k1.Keycode == Key.F1) Game.Hud?.ToggleHelp();
            return;
        }
        if (e is InputEventMouseButton mb) Mouse(mb);
        else if (e is InputEventMouseMotion mm && _leftDown)
        {
            if (mm.Position.DistanceTo(_dragStart) > 6)
            {
                Game.Hud.Box.Active = true;
                Game.Hud.Box.Rect = new Rect2(_dragStart, mm.Position - _dragStart);
                Game.Hud.Box.QueueRedraw();
            }
        }
        else if (e is InputEventKey k && k.Pressed && !k.Echo) OnKey(k);
    }

    void Mouse(InputEventMouseButton mb)
    {
        if (mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                if (_mode != Mode.None)
                {
                    if (Cam.ScreenToGround(mb.Position, out var g))
                    {
                        if (_mode == Mode.AttackMove) Issue(new Vector2(g.X, g.Z), mb.Position, true);
                        else Issue(new Vector2(g.X, g.Z), mb.Position, false);
                    }
                    _mode = Mode.None;
                    Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
                    return;
                }
                _leftDown = true;
                _dragStart = mb.Position;
                if (mb.DoubleClick) SelectSameType(mb.Position);
            }
            else if (_leftDown)
            {
                _leftDown = false;
                var box = Game.Hud.Box;
                if (box.Active)
                {
                    box.Active = false;
                    box.QueueRedraw();
                    BoxSelect(box.Rect.Abs(), mb.ShiftPressed);
                }
                else if (!mb.DoubleClick) ClickSelect(mb.Position, mb.ShiftPressed, mb.CtrlPressed);
            }
        }
        else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            if (_mode != Mode.None) { _mode = Mode.None; Input.SetDefaultCursorShape(Input.CursorShape.Arrow); return; }
            if (Cam.ScreenToGround(mb.Position, out var g)) Issue(new Vector2(g.X, g.Z), mb.Position, false, true);
        }
    }

    void OnKey(InputEventKey k)
    {
        switch (k.Keycode)
        {
            case Godot.Key.Escape:
                if (_mode != Mode.None) { _mode = Mode.None; Input.SetDefaultCursorShape(Input.CursorShape.Arrow); }
                else Game.Hud.TogglePause();
                return;
            case Godot.Key.F1: Game.Hud.ToggleHelp(); return;
            case Godot.Key.A: BeginAttackMove(); return;
            case Godot.Key.S: Stop(false); return;
            case Godot.Key.H: Stop(true); return;
            case Godot.Key.G: BeginGather(); return;
            case Godot.Key.Z: Game.TryEnqueue(ProdKind.Worker); return;
            case Godot.Key.X: Game.TryEnqueue(ProdKind.Soldier); return;
            case Godot.Key.C: Game.TryEnqueue(ProdKind.Major); return;
            case Godot.Key.V: Game.TryEnqueue(ProdKind.Chamber); return;
            case Godot.Key.Tab: Game.ToggleTrails(); return;
            case Godot.Key.F2: SelectArmy(); return;
            case Godot.Key.F3: SelectWorkers(); return;
            case Godot.Key.Home: case Godot.Key.Backspace: SelectNest(); return;
            case Godot.Key.Space: if (LastAlert.HasValue) Cam.PanTo(LastAlert.Value); return;
            case Godot.Key.F11:
                DisplayServer.WindowSetMode(DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
                return;
        }
        int digit = (int)k.Keycode - (int)Godot.Key.Key0;
        if (digit >= 0 && digit <= 9)
        {
            if (k.CtrlPressed)
            {
                var list = new List<(int, int)>();
                foreach (int id in Selection) list.Add((id, Sim.Ants[id].Serial));
                _groups[digit] = list;
                Game.Hud.Alert($"Group {digit} set", UiTheme.Dim);
            }
            else if (_groups.TryGetValue(digit, out var g))
            {
                var ids = new List<int>();
                foreach (var (id, serial) in g) if (Sim.Valid(id, serial)) ids.Add(id);
                if (ids.Count == 0) return;
                bool again = ids.Count == Selection.Count && ids.TrueForAll(Selection.Contains);
                SetSelection(ids);
                if (again) Cam.PanTo(Sim.Ants[ids[0]].Pos);
            }
        }
    }

    // ------------------------------------------------------------- selection
    void SetSelection(List<int> ids, bool add = false)
    {
        if (!add)
        {
            foreach (int id in Selection) Sim.Ants[id].Selected = false;
            Selection.Clear();
        }
        foreach (int id in ids)
        {
            if (Selection.Contains(id)) continue;
            Selection.Add(id);
            Sim.Ants[id].Selected = true;
        }
        NestSelected = false;
        if (ids.Count > 0) Game.Audio.Ui("ui_click", -6);
    }

    public void Prune()
    {
        for (int i = Selection.Count - 1; i >= 0; i--)
            if (!Sim.Ants[Selection[i]].Alive || Sim.Ants[Selection[i]].Team != Config.Player) { Sim.Ants[Selection[i]].Selected = false; Selection.RemoveAt(i); }
    }

    bool OnScreen(int id, out Vector2 sp)
    {
        ref var a = ref Sim.Ants[id];
        var wp = new Vector3(a.Pos.X, a.Height + 0.2f * Config.Of(a.Type).Size, a.Pos.Y);
        sp = Vector2.Zero;
        if (Cam.Cam.IsPositionBehind(wp)) return false;
        sp = Cam.Cam.UnprojectPosition(wp);
        return true;
    }

    int Pick(Vector2 screen, bool own, float px = 22)
    {
        int best = -1;
        float bestD = px * px;
        for (int i = 0; i < Sim.High; i++)
        {
            ref var a = ref Sim.Ants[i];
            if (!a.Alive || (a.Team == Config.Player) != own) continue;
            if (!own && Game.Fog != null && !Game.Fog.Visible(a.Pos)) continue;
            if (!OnScreen(i, out var sp)) continue;
            float scale = Mathf.Clamp(60f / Cam.Distance, 0.6f, 3f) * Config.Of(a.Type).Size;
            float d = sp.DistanceSquaredTo(screen) / Mathf.Max(1, scale * 0.5f);
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    void ClickSelect(Vector2 screen, bool shift, bool ctrl)
    {
        int id = Pick(screen, true);
        if (id >= 0 && Sim.Ants[id].Type == AntType.Queen) { SelectNest(false); return; }
        if (id >= 0)
        {
            if (ctrl) { SelectSameType(screen); return; }
            if (shift && Selection.Contains(id)) { Selection.Remove(id); Sim.Ants[id].Selected = false; return; }
            SetSelection(new List<int> { id }, shift);
            return;
        }
        // nest click?
        if (Cam.ScreenToGround(screen, out var g) && new Vector2(g.X, g.Z).DistanceTo(Game.World.Nests[Config.Player].Pos) < 7)
        {
            SelectNest(false);
            return;
        }
        if (!shift) SetSelection(new List<int>());
    }

    void BoxSelect(Rect2 r, bool shift)
    {
        var fighters = new List<int>();
        var workers = new List<int>();
        for (int i = 0; i < Sim.High; i++)
        {
            ref var a = ref Sim.Ants[i];
            if (!a.Alive || a.Team != Config.Player || a.Type == AntType.Queen) continue;
            if (!OnScreen(i, out var sp) || !r.HasPoint(sp)) continue;
            (a.Type == AntType.Worker ? workers : fighters).Add(i);
        }
        // army first: a box over a mixed crowd picks the fighters
        SetSelection(fighters.Count > 0 ? fighters : workers, shift);
    }

    void SelectSameType(Vector2 screen)
    {
        int id = Pick(screen, true);
        if (id < 0) return;
        var type = Sim.Ants[id].Type;
        var vp = GetViewport().GetVisibleRect();
        var ids = new List<int>();
        for (int i = 0; i < Sim.High; i++)
        {
            ref var a = ref Sim.Ants[i];
            if (!a.Alive || a.Team != Config.Player || a.Type != type) continue;
            if (OnScreen(i, out var sp) && vp.HasPoint(sp)) ids.Add(i);
        }
        SetSelection(ids);
    }

    public void SelectArmy()
    {
        var ids = new List<int>();
        for (int i = 0; i < Sim.High; i++)
            if (Sim.Ants[i].Alive && Sim.Ants[i].Team == Config.Player && (Sim.Ants[i].Type == AntType.Soldier || Sim.Ants[i].Type == AntType.Major)) ids.Add(i);
        SetSelection(ids);
    }

    public void SelectWorkers()
    {
        var ids = new List<int>();
        for (int i = 0; i < Sim.High; i++)
            if (Sim.Ants[i].Alive && Sim.Ants[i].Team == Config.Player && Sim.Ants[i].Type == AntType.Worker) ids.Add(i);
        SetSelection(ids);
    }

    public void SelectNest(bool jump = true)
    {
        SetSelection(new List<int>());
        NestSelected = true;
        Game.Audio.Ui("ui_click", -4);
        if (jump) Cam.PanTo(Game.World.Nests[Config.Player].Pos);
    }

    // ------------------------------------------------------------- commands
    public void BeginAttackMove()
    {
        if (Selection.Count == 0) return;
        _mode = Mode.AttackMove;
        Input.SetDefaultCursorShape(Input.CursorShape.Cross);
        Game.Hud.Alert("Attack-move: choose a target", new Color(1, 0.55f, 0.4f));
    }

    public void BeginGather()
    {
        if (!Selection.Exists(i => Sim.Ants[i].Type == AntType.Worker)) return;
        _mode = Mode.Gather;
        Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);
        Game.Hud.Alert("Gather: choose food", new Color(0.75f, 0.95f, 0.4f));
    }

    public void Stop(bool hold)
    {
        Prune();
        if (Selection.Count == 0) return;
        Sim.CmdStop(Selection, hold);
    }

    public void CommandAt(Vector2 world, bool attack) => Issue(world, null, attack);

    void Issue(Vector2 world, Vector2? screen, bool attack, bool rightClick = false)
    {
        Prune();
        if (NestSelected && Selection.Count == 0)
        {
            var c = Sim.Col[Config.Player];
            c.Rally = world;
            c.HasRally = true;
            Game.Effects.Marker(Game.World.HF.Point(world), new Color(1, 0.8f, 0.3f));
            Game.Hud.Alert("Rally point set", UiTheme.Dim);
            Game.Audio.Ui("ui_click");
            return;
        }
        if (Selection.Count == 0) return;
        // enemy under the cursor?
        int enemy = screen.HasValue ? Pick(screen.Value, false, 26) : -1;
        if (enemy >= 0)
        {
            Sim.CmdAttack(Selection, enemy);
            Game.Effects.Marker(Game.World.HF.Point(Sim.Ants[enemy].Pos), new Color(1, 0.3f, 0.2f));
            Game.Audio.Ui("ui_click");
            return;
        }
        if (!attack)
        {
            FoodSource food = null;
            float best = 999;
            foreach (var f in Game.World.Food)
            {
                if (!f.Alive) continue;
                float d = f.Pos.DistanceTo(world) - f.Radius;
                if (d < 1.8f && d < best) { best = d; food = f; }
            }
            if (food != null && Selection.Exists(i => Sim.Ants[i].Type == AntType.Worker))
            {
                Sim.CmdGather(Selection, food);
                Game.Effects.Marker(Game.World.HF.Point(food.Pos), new Color(0.75f, 1f, 0.35f));
                Game.Audio.Ui("ui_click");
                return;
            }
        }
        Sim.CmdMove(Selection, world, attack);
        Game.Effects.Marker(Game.World.HF.Point(world), attack ? new Color(1, 0.35f, 0.2f) : new Color(0.5f, 1, 0.45f));
        Game.Audio.Ui("ui_click");
    }
}
