using System.Collections.Generic;
using Godot;

namespace Mandible;

public partial class PlayerInput : Node
{
    public GameSession Game;
    public readonly List<int> Selection = new();
    public bool NestSelected;
    public int Inspect = -1, InspectSerial;
    public FoodSource InspectFood;
    /// Build mode: the chamber (id) the player clicked under the floor, or -1.
    public int SelectedRoom = -1;
    MeshInstance3D _cue, _cueNode;
    enum Mode { None, AttackMove, Gather, Dig }
    ChamberKind _digKind;
    Upgrade? _digUpgrade;

    /// Placement, Settlers-style: the next click on the ground chooses where the room is dug. Glass Earth
    /// opens so the tunnels show; the room must lie within reach of one of them.
    public void BeginDig(ChamberKind k, Upgrade? upgrade)
    {
        _mode = Mode.Dig;
        _digKind = k;
        _digUpgrade = upgrade;
        if (!Game.NestView) Game.ToggleNestView();
        Input.SetDefaultCursorShape(Input.CursorShape.Cross);
        Game.Hud.Alert($"Click where to dig the {Sim.ChamberName(k).ToLower()}: within {Sim.MaxTunnel:0} of the entrance or another room (Esc cancels)", UiTheme.Gold);
    }

    /// Places the room being planned at a ground point.
    public void PlaceAt(Vector2 world)
    {
        bool ok = _digUpgrade.HasValue ? PlaceUpgrade(_digUpgrade.Value, world) : Game.TryDigAt(_digKind, world);
        if (ok) { _mode = Mode.None; Input.SetDefaultCursorShape(Input.CursorShape.Arrow); }
    }

    /// Opens or closes build mode (the hole, N, the Build button).
    public void ToggleBuild() => Game.SetNestView(!Game.NestView);

    /// Called by the session when build mode closes: nothing of it lingers.
    public void LeaveBuild()
    {
        SelectedRoom = -1;
        if (_mode == Mode.Dig) { _mode = Mode.None; Input.SetDefaultCursorShape(Input.CursorShape.Arrow); }
    }

    /// The chamber under a screen point (by its dug footprint), or -1.
    int PickRoom(Vector2 screen) => Game.NestView ? Game.Interior.PickRoom(screen) : -1;

    /// The placement cue: a ring the size of a room under the cursor, green where it may be dug and red where
    /// not, and a small gold ring on the node its gallery would start from.
    public override void _Process(double delta)
    {
        bool show = _mode == Mode.Dig && !Settings.Mobile && Game.Hud != null && !Game.Hud.Blocking;
        if (!show) { if (_cue != null) { _cue.Visible = false; _cueNode.Visible = false; } return; }
        if (_cue == null)
        {
            MeshInstance3D Ring(float size)
            {
                var m = new ShaderMaterial { Shader = Shaders.Get(Shaders.Cue) };
                var mi = new MeshInstance3D { Mesh = new PlaneMesh { Size = Vector2.One * size }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, SortingOffset = 60 };
                Game.AddChild(mi);
                return mi;
            }
            _cue = Ring(Sim.RoomSpacing);
            _cueNode = Ring(2.4f);
        }
        var mp = GetViewport().GetMousePosition();
        if (!Cam.ScreenToGround(mp, out var g)) { _cue.Visible = false; _cueNode.Visible = false; return; }
        var p = new Vector2(g.X, g.Z);
        bool ok = Sim.PlaceCheck(Local.Me, p) == Sim.DigResult.Ok;
        _cue.Visible = true;
        _cue.Position = Game.World.HF.Point(p, 0.12f);
        ((ShaderMaterial)_cue.MaterialOverride).SetShaderParameter("color", ok ? new Color(0.55f, 1f, 0.45f, 0.95f) : new Color(1f, 0.35f, 0.25f, 0.95f));
        int node = Sim.DigNodeFor(Local.Me, p);
        _cueNode.Visible = node != int.MinValue;
        if (node != int.MinValue)
        {
            _cueNode.Position = Game.World.HF.Point(Sim.NodePos(Local.Me, node), 0.12f);
            ((ShaderMaterial)_cueNode.MaterialOverride).SetShaderParameter("color", new Color(1f, 0.85f, 0.4f, 0.9f));
        }
    }

    bool PlaceUpgrade(Upgrade u, Vector2 world)
    {
        if (Sim.PlaceCheck(Local.Me, world) != Sim.DigResult.Ok) { Game.Hud.Alert(Sim.DigMessage(Sim.PlaceCheck(Local.Me, world), Sim.KindOf(u)), new Color(1, 0.5f, 0.35f)); Game.Audio.Ui("ui_error"); return false; }
        Game.TryResearch(u, world);
        return true;
    }
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
                        if (_mode == Mode.Dig) { PlaceAt(new Vector2(g.X, g.Z)); return; }
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
            if (Game.NestView && Selection.Count == 0 && Pick(mb.Position, false, 26) < 0) { Game.SetNestView(false); return; }
            if (Cam.ScreenToGround(mb.Position, out var g)) Issue(new Vector2(g.X, g.Z), mb.Position, false, true);
        }
    }

    /// Hotkeys go by the key's position on the keyboard, not the letter it types, so they work on any
    /// layout (Arabic, French AZERTY, ...). Synthetic events without a physical key fall back to the letter.
    static Godot.Key HotKey(InputEventKey k) => k.PhysicalKeycode != Godot.Key.None ? k.PhysicalKeycode : k.Keycode;

    /// Lane War keys: Q ability, 1-4 shop, Space/F2 back to the hero; colony controls do nothing.
    bool LaneKey(InputEventKey k)
    {
        int item = HotKey(k) switch { Godot.Key.Key1 => 0, Godot.Key.Key2 => 1, Godot.Key.Key3 => 2, Godot.Key.Key4 => 3, _ => -1 };
        if (item >= 0) { Game.TryBuy(item); return true; }
        switch (HotKey(k))
        {
            case Godot.Key.Q: Game.TryAbility(); return true;
            case Godot.Key.Space: case Godot.Key.F2: FocusHero(); return true;
            case Godot.Key.Z: case Godot.Key.X: case Godot.Key.C: case Godot.Key.V: case Godot.Key.F: case Godot.Key.B: case Godot.Key.D:
            case Godot.Key.Y: case Godot.Key.U: case Godot.Key.I: case Godot.Key.W: case Godot.Key.E: case Godot.Key.R: case Godot.Key.T:
            case Godot.Key.F3: case Godot.Key.G:
                return true;
        }
        return false;
    }

    /// Selects your hero and brings the camera to it.
    public void FocusHero()
    {
        int id = Sim.HeroId[Local.Me];
        if (id < 0) return;
        SetSelection(new List<int> { id });
        Cam.PanTo(Sim.Ants[id].Pos);
    }

    void OnKey(InputEventKey k)
    {
        if (Game.Lanes && LaneKey(k)) return;
        switch (HotKey(k))
        {
            case Godot.Key.Escape:
                if (_mode != Mode.None) { _mode = Mode.None; Input.SetDefaultCursorShape(Input.CursorShape.Arrow); }
                else if (Game.NestView) Game.SetNestView(false);
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
            case Godot.Key.V: Game.TryDig(ChamberKind.Brood); return;
            case Godot.Key.L: Game.TryDig(ChamberKind.Storage); return;
            case Godot.Key.N: ToggleBuild(); return;
            case Godot.Key.F: Game.TryEnqueue(ProdKind.Flyer); return;
            case Godot.Key.B: Reinforce(); return;
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
                Settings.Fullscreen = DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Fullscreen;
                Settings.ApplyDisplay(save: true);
                return;
        }
        // control groups: the number row or the numpad; Ctrl (or Cmd on a Mac) saves, the number alone recalls
        int digit = (int)HotKey(k) - (int)Godot.Key.Key0;
        if (digit < 0 || digit > 9) digit = (int)HotKey(k) - (int)Godot.Key.Kp0;
        if (digit >= 0 && digit <= 9)
        {
            if (k.CtrlPressed || k.MetaPressed) SaveGroup(digit);
            else RecallGroup(digit);
        }
    }

    public void SaveGroup(int digit)
    {
        if (Selection.Count == 0) { Game.Hud.Alert("Select some ants first, then press Ctrl + a number", UiTheme.Dim); return; }
        var list = new List<(int, int)>();
        foreach (int id in Selection) list.Add((id, Sim.Ants[id].Serial));
        _groups[digit] = list;
        Game.Hud.Alert($"Group {digit} saved: {list.Count} ants.  Press {digit} to select them again", UiTheme.Dim);
        Game.Audio.Ui("ui_queue", -6);
    }

    /// Selects a saved group; pressing it again centres the camera on it.
    public void RecallGroup(int digit)
    {
        if (!_groups.TryGetValue(digit, out var g)) return;
        var ids = new List<int>();
        foreach (var (id, serial) in g) if (Sim.Valid(id, serial)) ids.Add(id);
        if (ids.Count == 0) return;
        bool again = ids.Count == Selection.Count && ids.TrueForAll(Selection.Contains);
        SetSelection(ids);
        Recalls++;
        if (again) Cam.PanTo(Sim.Ants[ids[0]].Pos);
    }

    public int Recalls, AttackMoves, Moves; // the tutorial watches these

    /// Saved groups and how many of their ants are still alive, for the HUD.
    public IEnumerable<(int digit, int alive)> Groups()
    {
        for (int d = 1; d <= 10; d++)
        {
            int digit = d % 10;
            if (!_groups.TryGetValue(digit, out var g)) continue;
            int n = 0;
            foreach (var (id, serial) in g) if (Sim.Valid(id, serial)) n++;
            if (n > 0) yield return (digit, n);
        }
    }

    // ------------------------------------------------------------- selection
    /// Squads move as one: selecting any member selects the whole pack.
    List<int> WithSquads(List<int> ids)
    {
        var all = new List<int>(ids);
        foreach (int id in ids)
        {
            var s = Sim.SquadOf(id);
            if (s != null) foreach (int m in s.Members) if (!all.Contains(m)) all.Add(m);
        }
        return all;
    }

    public void Reinforce()
    {
        Prune();
        if (Selection.Count == 0) return;
        Game.Issue(Cmd.Units(CmdKind.Reinforce, Local.Me, Selection, Sim));
        Game.Ants.Ack(Selection, Game.World.Nests[Local.Me].Pos);
        Game.Hud.Alert("Squads returning to the nest to heal and reinforce", UiTheme.Dim);
        Game.Audio.Ui("ui_click");
    }

    void SetSelection(List<int> ids, bool add = false)
    {
        ids = WithSquads(ids);
        if (Game.Lanes) ids = ids.FindAll(id => Config.IsHero(Sim.Ants[id].Type)); // only the hero takes orders
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
        SelectedRoom = -1;
        Inspect = -1;
        InspectFood = null;
        if (ids.Count > 0) Game.Audio.Ui("ui_click", -6);
    }

    public void Prune()
    {
        for (int i = Selection.Count - 1; i >= 0; i--)
            if (!Sim.Ants[Selection[i]].Alive || Sim.Ants[Selection[i]].Team != Local.Me) { Sim.Ants[Selection[i]].Selected = false; Selection.RemoveAt(i); }
        // Lane War: you always steer your hero, including the moment it respawns
        if (Game.Lanes && Selection.Count == 0 && Inspect < 0 && InspectFood == null && Sim.HeroId[Local.Me] >= 0)
        {
            int hero = Sim.HeroId[Local.Me];
            Selection.Add(hero);
            Sim.Ants[hero].Selected = true;
        }
    }

    bool OnScreen(int id, out Vector2 sp)
    {
        ref var a = ref Sim.Ants[id];
        sp = Vector2.Zero;
        if (a.Layer > 0 && (!Game.NestView || a.Layer != Sim.NestLayer(Local.Me))) return false;
        var nest = a.Layer > 0 ? Game.World.Nests[a.Layer - 1].Pos : Vector2.Zero;
        float height = a.Layer > 0 ? Game.World.HF.Sample(nest.X, nest.Y) - NestGeometry.Depth : a.Height;
        var wp = new Vector3(a.Pos.X, height + 0.2f * Config.Of(a.Type).Size, a.Pos.Y);
        if (Cam.Cam.IsPositionBehind(wp)) return false;
        sp = Cam.Cam.UnprojectPosition(wp);
        return true;
    }

    /// Every frame: which of your ants is under the cursor, and where the cursor meets the ground, so the
    /// renderer can make that ant listen. Render-side only.
    public void UpdateHover()
    {
        if (Settings.Mobile) { Game.Ants.Hovered = -1; Game.Ants.HasCursor = false; return; }
        var mp = GetViewport().GetMousePosition();
        Game.Ants.Hovered = Pick(mp, true, 18);
        Game.Ants.HasCursor = Cam.ScreenToGround(mp, out var g);
        Game.Ants.Cursor = g;
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
        // Build mode selects the visible room even when its queen/props occupy the centre.
        if (Game.NestView && Cam.ScreenToGround(screen, out var surface))
        {
            if (new Vector2(surface.X, surface.Z).DistanceTo(Game.World.Nests[Local.Me].Pos) < 1.5f)
            { ToggleBuild(); return; }
            int room = PickRoom(screen);
            if (room >= 0) { SetSelection(new List<int>()); SelectedRoom = room; Game.Audio.Ui("ui_click", -6); return; }
        }
        int id = Pick(screen, true);
        if (id >= 0 && Sim.Ants[id].Type == AntType.Queen) { SelectNest(false); return; }
        if (id >= 0)
        {
            if (ctrl) { SelectSameType(screen); return; }
            if (shift && Selection.Contains(id)) { Selection.Remove(id); Sim.Ants[id].Selected = false; return; }
            SetSelection(new List<int> { id }, shift);
            return;
        }
        // the entrance hole itself opens (and closes) build mode; the mound around it selects the nest
        if (Cam.ScreenToGround(screen, out var g))
        {
            float dn = new Vector2(g.X, g.Z).DistanceTo(Game.World.Nests[Local.Me].Pos);
            if (dn < 2.6f && !Game.Lanes) { ToggleBuild(); return; }
            int room = PickRoom(screen);
            if (room >= 0) { SetSelection(new List<int>()); SelectedRoom = room; Game.Audio.Ui("ui_click", -6); return; }
            if (dn < 7) { SelectNest(false); return; }
        }
        if (!shift) SetSelection(new List<int>());
        // clicking an enemy or wild bug shows its details
        int other = Pick(screen, false);
        if (other >= 0) { Inspect = other; InspectSerial = Sim.Ants[other].Serial; Game.Audio.Ui("ui_click", -8); return; }
        // clicking food shows what it is, how much is left and what it does for the colony
        var food = PickFood(screen);
        if (food != null) { InspectFood = food; Game.Audio.Ui("ui_click", -8); }
    }

    /// The visible food pile under the cursor, if any.
    public FoodSource PickFood(Vector2 screen)
    {
        if (!Cam.ScreenToGround(screen, out var g)) return null;
        var p = new Vector2(g.X, g.Z);
        FoodSource best = null;
        float bestD = float.MaxValue;
        foreach (var f in Game.World.Food)
        {
            if (!f.Alive || (Game.Fog != null && !Game.Fog.Visible(f.Pos))) continue;
            float d = f.Pos.DistanceTo(p) - f.Radius;
            if (d < 1.5f && d < bestD) { bestD = d; best = f; }
        }
        return best;
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

    public void ClearSelection() => SetSelection(new List<int>());

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
        Game.Ants.Ack(Selection, null);
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
        if (attack) AttackMoves++; else Moves++;
        // enemy under the cursor?
        int enemy = screen.HasValue ? Pick(screen.Value, false, 26) : -1;
        if (enemy >= 0)
        {
            var ac = Cmd.Units(CmdKind.Attack, Local.Me, Selection, Sim);
            ac.Target = enemy; ac.TargetSerial = Sim.Ants[enemy].Serial;
            Game.Issue(ac);
            Game.Ants.Ack(Selection, Sim.Ants[enemy].Pos);
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
                Game.Ants.Ack(Selection, food.Pos);
                Game.Effects.Marker(Game.World.HF.Point(food.Pos), new Color(0.75f, 1f, 0.35f));
                Game.Audio.Ui("ui_click");
                return;
            }
        }
        var mc = Cmd.Units(attack ? CmdKind.AttackMove : CmdKind.Move, Local.Me, Selection, Sim);
        mc.P = world;
        // right-clicking your own nest with warriors sends them below to garrison the royal chamber
        var nest = Game.World.Nests[Local.Me];
        if (!attack && Sim.Col[Local.Me].Founded && world.DistanceTo(nest.Pos) < nest.Radius && Selection.Exists(i => Config.IsWarrior(Sim.Ants[i].Type)))
        {
            mc.Layer = (byte)Sim.NestLayer(Local.Me);
            mc.P = Sim.ChamberPos(Local.Me, Sim.Royal(Local.Me));
            Game.Hud.Alert("Garrison: the warriors go below to guard the queen (press N to watch)", UiTheme.Dim);
        }
        Game.Issue(mc);
        Game.Ants.Ack(Selection, world);
        Game.Effects.Marker(Game.World.HF.Point(world), attack ? new Color(1, 0.35f, 0.2f) : new Color(0.5f, 1, 0.45f));
        Game.Audio.Ui("ui_click");
    }
}
