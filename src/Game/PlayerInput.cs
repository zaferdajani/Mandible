using System.Collections.Generic;
using Godot;

namespace Mandible;

public partial class PlayerInput : Node
{
    public GameSession Game;
    public readonly List<int> Selection = new();
    public bool NestSelected;
    public int Inspect = -1, InspectSerial;
    enum Mode { None, AttackMove, Gather }
    Mode _mode;
    bool _leftDown;
    Vector2 _dragStart;
    readonly Dictionary<int, List<(int id, int serial)>> _groups = new();
    public Vector2? LastAlert;

    Sim Sim => Game.Sim;
    RtsCamera Cam => Game.Camera;

    // touch: finger index -> screen position
    readonly System.Collections.Generic.Dictionary<int, Vector2> _touches = new();
    ulong _pressMs;

    /// Two fingers pan, pinch to zoom and twist to rotate. Returns true when the event was a multi-touch gesture.
    bool Touch(InputEvent e)
    {
        if (e is InputEventScreenTouch st)
        {
            if (st.Pressed) _touches[st.Index] = st.Position;
            else _touches.Remove(st.Index);
            if (_touches.Count >= 2)
            {
                // a second finger turns the gesture into camera control: cancel any box in progress
                _leftDown = false;
                Game.Hud.Box.Active = false;
                Game.Hud.Box.QueueRedraw();
            }
            return _touches.Count >= 2;
        }
        if (e is InputEventScreenDrag sd && _touches.ContainsKey(sd.Index))
        {
            if (_touches.Count < 2) { _touches[sd.Index] = sd.Position; return false; }
            var keys = new System.Collections.Generic.List<int>(_touches.Keys);
            keys.Sort();
            var a0 = _touches[keys[0]]; var b0 = _touches[keys[1]];
            _touches[sd.Index] = sd.Position;
            var a1 = _touches[keys[0]]; var b1 = _touches[keys[1]];
            Cam.TouchPan(((a1 + b1) - (a0 + b0)) * 0.5f);
            float d0 = a0.DistanceTo(b0), d1 = a1.DistanceTo(b1);
            if (d0 > 10 && d1 > 10) Cam.TargetDistance = Mathf.Clamp(Cam.TargetDistance * d0 / d1, RtsCamera.MinDist, RtsCamera.MaxDist);
            Cam.SetYaw(Cam.Yaw - Mathf.AngleDifference((b0 - a0).Angle(), (b1 - a1).Angle()), true);
            return true;
        }
        return false;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (Game.Hud != null && !Game.Hud.Blocking && Touch(e)) return;
        if (_touches.Count >= 2 && e is InputEventMouse) return; // ignore the emulated mouse while gesturing
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
                _pressMs = Time.GetTicksMsec();
                if (mb.DoubleClick)
                {
                    // on touch, double-tapping empty ground clears the selection
                    if (mb.Device == InputEvent.DeviceIdEmulation && Pick(mb.Position, true) < 0) { SetSelection(new List<int>()); _leftDown = false; }
                    else SelectSameType(mb.Position);
                }
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
                else if (!mb.DoubleClick)
                {
                    bool touch = mb.Device == InputEvent.DeviceIdEmulation;
                    bool hasOrders = Selection.Count > 0 || NestSelected;
                    if (touch && hasOrders && Pick(mb.Position, true) < 0 && Cam.ScreenToGround(mb.Position, out var tg))
                    {
                        // a finger has no right button: tap = smart order, long press = attack-move
                        bool longPress = Time.GetTicksMsec() - _pressMs > 450;
                        Issue(new Vector2(tg.X, tg.Z), mb.Position, longPress, true);
                    }
                    else ClickSelect(mb.Position, mb.ShiftPressed, mb.CtrlPressed);
                }
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
            case Godot.Key.F: Game.TryEnqueue(ProdKind.Flyer); return;
            case Godot.Key.D: Game.TryEnqueue(ProdKind.Gunner); return;
            case Godot.Key.Y: Game.TryEnqueue(ProdKind.Warlord); return;
            case Godot.Key.U: Game.TryEnqueue(ProdKind.Matron); return;
            case Godot.Key.I: Game.TryEnqueue(ProdKind.Marshal); return;
            case Godot.Key.Q: Game.TryResearch(Upgrade.Queen); return;
            case Godot.Key.W: Game.TryResearch(Upgrade.Wings); return;
            case Godot.Key.E: Game.TryResearch(Upgrade.Mandibles); return;
            case Godot.Key.R: Game.TryResearch(Upgrade.Chitin); return;
            case Godot.Key.T: Game.TryResearch(Upgrade.SilkCutters); return;
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
        Inspect = -1;
        if (ids.Count > 0) Game.Audio.Ui("ui_click", -6);
    }

    public void Prune()
    {
        for (int i = Selection.Count - 1; i >= 0; i--)
            if (!Sim.Ants[Selection[i]].Alive || Sim.Ants[Selection[i]].Team != Local.Me) { Sim.Ants[Selection[i]].Selected = false; Selection.RemoveAt(i); }
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
            if (!a.Alive || (a.Team == Local.Me) != own) continue;
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
        if (Cam.ScreenToGround(screen, out var g) && new Vector2(g.X, g.Z).DistanceTo(Game.World.Nests[Local.Me].Pos) < 7)
        {
            SelectNest(false);
            return;
        }
        if (!shift) SetSelection(new List<int>());
        // clicking an enemy or wild bug shows its details
        int other = Pick(screen, false);
        if (other >= 0) { Inspect = other; InspectSerial = Sim.Ants[other].Serial; Game.Audio.Ui("ui_click", -8); }
    }

    void BoxSelect(Rect2 r, bool shift)
    {
        var fighters = new List<int>();
        var workers = new List<int>();
        for (int i = 0; i < Sim.High; i++)
        {
            ref var a = ref Sim.Ants[i];
            if (!a.Alive || a.Team != Local.Me || a.Type == AntType.Queen) continue;
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
            if (!a.Alive || a.Team != Local.Me || a.Type != type) continue;
            if (OnScreen(i, out var sp) && vp.HasPoint(sp)) ids.Add(i);
        }
        SetSelection(ids);
    }

    public void SelectArmy()
    {
        var ids = new List<int>();
        for (int i = 0; i < Sim.High; i++)
            if (Sim.Ants[i].Alive && Sim.Ants[i].Team == Local.Me && Config.IsWarrior(Sim.Ants[i].Type)) ids.Add(i);
        SetSelection(ids);
    }

    public void SelectWorkers()
    {
        var ids = new List<int>();
        for (int i = 0; i < Sim.High; i++)
            if (Sim.Ants[i].Alive && Sim.Ants[i].Team == Local.Me && Sim.Ants[i].Type == AntType.Worker) ids.Add(i);
        SetSelection(ids);
    }

    public void SelectNest(bool jump = true)
    {
        SetSelection(new List<int>());
        NestSelected = true;
        Game.Audio.Ui("ui_click", -4);
        if (jump) Cam.PanTo(Game.World.Nests[Local.Me].Pos);
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
        Game.Issue(Cmd.Units(hold ? CmdKind.Hold : CmdKind.Stop, Local.Me, Selection, Sim));
    }

    public void CommandAt(Vector2 world, bool attack) => Issue(world, null, attack);

    void Issue(Vector2 world, Vector2? screen, bool attack, bool rightClick = false)
    {
        Prune();
        if (NestSelected && Selection.Count == 0)
        {
            var c = Sim.Col[Local.Me];
            Game.Issue(new Cmd { Kind = CmdKind.Rally, Team = (byte)Local.Me, P = world });
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
            var ac = Cmd.Units(CmdKind.Attack, Local.Me, Selection, Sim);
            ac.Target = enemy; ac.TargetSerial = Sim.Ants[enemy].Serial;
            Game.Issue(ac);
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
                var gc = Cmd.Units(CmdKind.Gather, Local.Me, Selection, Sim);
                gc.FoodId = food.Id;
                Game.Issue(gc);
                Game.Effects.Marker(Game.World.HF.Point(food.Pos), new Color(0.75f, 1f, 0.35f));
                Game.Audio.Ui("ui_click");
                return;
            }
        }
        var mc = Cmd.Units(attack ? CmdKind.AttackMove : CmdKind.Move, Local.Me, Selection, Sim);
        mc.P = world;
        Game.Issue(mc);
        Game.Effects.Marker(Game.World.HF.Point(world), attack ? new Color(1, 0.35f, 0.2f) : new Color(0.5f, 1, 0.45f));
        Game.Audio.Ui("ui_click");
    }
}
