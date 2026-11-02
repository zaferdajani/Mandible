using System.Collections.Generic;
using Godot;

namespace Mandible;

/// How the renderer draws an ant the sim has just hatched (see NestInterior.Callow).
public struct CallowPose
{
    public bool Active;               // position, heading and gait come from the emergence replay
    public int Layer, Pale, Kind;     // pale 0..15; kind: an Ant shader one-shot (2 splays the legs as they unfold)
    public float Rise, Scale, Lift, Amp, Gait, KindT;
}

/// Brood life in the player's own nest. The queen lays as the colony's real brood timer runs, the beds hold
/// what the production queue holds, nurse workers carry eggs and larvae along the real galleries and tend the
/// beds, and every ant the sim hatches below ground first splits its cocoon and walks out pale, retracing its
/// actual path a moment behind until it has caught up. Rendering only: nurses are not sim ants, and nothing
/// here writes to the simulation or draws from its random sequence.
public sealed partial class NestInterior
{
    sealed class Bed
    {
        public Surface Type;
        public Transform3D[] Slots, Contacts;  // room-local, filled from the middle of the bed outward
        public Vector2 Centre, Extent;         // the bed's floor ellipse, room-local
        public MultiMesh Items, Shadows;
        public float Shown = -1;
        public int Target, DrawnCount = -1, BusyStamp, DrawnBusy;
        public float DrawnFrac = -1;
        public readonly List<int> Busy = new(); // slots whose cocoon has split open
        public int Shown0 => Mathf.Max(0, Mathf.FloorToInt(Shown + 0.001f));
    }

    sealed class Nurse
    {
        public Vector2 Pos, Face;
        public float Heading, Gait, Amp, T0, Until;
        public int Seed, Room, Task, Carry, Dest = -1, DestBed, Shuffles;
        public Loose Target;          // the egg it is going for (null: a larva taken straight from a bed)
        public readonly List<Vector2> Path = new();
        public int Step;
    }
    const int TaskTend = 0, TaskToPick = 1, TaskPick = 2, TaskToDrop = 3, TaskDrop = 4, TaskToTend = 5;

    sealed class Loose
    {
        public Vector3 From;
        public Vector2 Pos;
        public float Yaw, Born, Gone = -1;
        public int Room = -1;          // -1: laid in the royal chamber, waiting for a nurse; else the nursery it was set down in
        public Nurse By;              // the nurse coming for it
        public bool Larva, Lifted;
    }

    struct Sample { public float T, H, G; public Vector2 P; public byte L; }
    sealed class Emergence
    {
        public int Id, Serial, Room;
        public float Start, Yaw, Size, WalkLen;
        public Vector3 Shell;
        public Bed Bed;                       // the cocoon bed it split in, and which slot
        public int Slot = -1;
        public Vector2 Exit;
        public readonly List<Sample> Trail = new();
        public int LastTick = -1, Cursor;
        public float RawGait;
        public bool Replayed, Split;
    }
    struct Fleck { public Vector3 P, V; public float Life; }

    const float Emerge = 1.7f, CallowWalk = 2.3f, CatchUp = 1.75f, Tanning = 7f, NurseSpeed = 2.4f;

    readonly List<Nurse> _nurses = new();
    readonly List<Loose> _loose = new();
    readonly Dictionary<int, Emergence> _hatch = new();
    readonly List<Fleck> _flecks = new();
    readonly Rng _broodRng = new(5150);
    readonly List<Room> _broodRooms = new();
    Batch _eggs, _larvae, _shells, _fleckBatch;
    ProdItem _layItem;
    float _layProgress, _layAcc = 0.55f, _lastLay = -10, _clock, _broodDt, _queenTail;
    bool _broodShown;

    // what the brood captures and checks look at
    internal int LaidWaiting { get { int n = 0; foreach (var e in _loose) if (e.Room == -1 && e.Gone < 0 && e.By == null) n++; return n; } }
    internal int NursesCarrying { get { int n = 0; foreach (var nu in _nurses) if (nu.Carry > 0) n++; return n; } }
    internal int NurseCount => _nurses.Count;
    internal float SinceLaid => _clock - _lastLay;
    internal Vector2 LastLaid;
    internal Vector2? CarryingEgg { get { foreach (var nu in _nurses) if (nu.Carry == 1 && nu.Task == TaskToDrop) return nu.Pos; return null; } }
    internal (Vector2 pos, float age)? Emerging
    {
        get
        {
            foreach (var e in _hatch.Values) if (_clock - e.Start < Emerge + 1f) return (new Vector2(e.Shell.X, e.Shell.Z), _clock - e.Start);
            return null;
        }
    }
    /// Eggs, larvae and cocoons the nurseries hold for the current queue.
    internal (int eggs, int larvae, int cocoons) BroodHeld
    {
        get
        {
            int e = 0, l = 0, c = 0;
            foreach (var room in _broodRooms) { e += room.Beds[0].Target; l += room.Beds[1].Target; c += room.Beds[2].Target; }
            return (e, l, c);
        }
    }

    void InitBrood()
    {
        bool shadows = Settings.Gfx >= Settings.Quality.Medium;
        _eggs = new Batch(_root, OrganicMesh(Surface.Egg), OrganicMat(Surface.Egg), shadows, "LooseEggs");
        _larvae = new Batch(_root, OrganicMesh(Surface.Larva), OrganicMat(Surface.Larva), shadows, "LooseLarvae");
        _shells = new Batch(_root, ShellHalf(), OrganicMat(Surface.Cocoon), shadows, "SplitCocoons");
        _fleckBatch = new Batch(_root, Grit(), MThread, false, "SilkFlecks");
    }

    /// One half of a cocoon split along its length, with a chewed, ragged rim.
    static ArrayMesh _shellHalf;
    static ArrayMesh ShellHalf() => _shellHalf ??= Build(k =>
    {
        const int rings = 22, sides = 12;
        // the silk wall has two faces: the felted outside, and the smooth inside the pupa lay against
        for (int side = 0; side < 2; side++)
        {
            int start = k.V.Count;
            float wall = side == 0 ? 1 : 0.93f;
            for (int j = 0; j <= rings; j++)
            {
                float v = j / (float)rings, z = -1 + 2 * v, r = Mathf.Sqrt(Mathf.Max(0.0004f, 1 - z * z));
                float jag = 0.16f * Mathf.Abs(Mathf.Sin(v * 37f)) + 0.07f * Mathf.Abs(Mathf.Sin(v * 91f + 1));
                for (int s = 0; s <= sides; s++)
                {
                    float phi = Mathf.Lerp(-Mathf.Pi / 2 + jag, Mathf.Pi / 2 - jag * 0.7f, s / (float)sides);
                    var p = new Vector3(Mathf.Cos(phi) * 0.17f * r, Mathf.Sin(phi) * 0.16f * r, z * 0.34f) * (1 + 0.014f * Mathf.Sin(z * 30 + phi * 4)) * wall;
                    k.Add(p, side == 0 ? p.Normalized() : -p.Normalized(), Colors.White, new Vector2(s / (float)sides, v), Vector2.Zero);
                }
            }
            for (int j = 0; j < rings; j++) for (int s = 0; s < sides; s++)
            {
                int a = start + j * (sides + 1) + s;
                // wound so each face is front-facing on the side its normal points to (cull_disabled lights back faces flipped)
                if (side == 0) k.Quad(a, a + sides + 1, a + sides + 2, a + 1);
                else k.Quad(a, a + 1, a + sides + 2, a + sides + 1);
            }
        }
    }, false);

    /// Brood beds whose contents follow the colony: slots are laid out once, how many show is decided every frame.
    Bed MakeBed(Node parent, Surface type, Vector2 centre, Vector2 extent, Transform3D[] slots)
    {
        System.Array.Sort(slots, (a, b) => ((new Vector2(a.Origin.X, a.Origin.Z) - centre) / extent).LengthSquared()
            .CompareTo(((new Vector2(b.Origin.X, b.Origin.Z) - centre) / extent).LengthSquared()));
        var mesh = OrganicMesh(type); var aabb = mesh.GetAabb();
        var bed = new Bed { Type = type, Slots = slots, Centre = centre, Extent = extent, Contacts = new Transform3D[slots.Length] };
        bed.Items = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = mesh, InstanceCount = slots.Length, VisibleInstanceCount = 0 };
        bed.Shadows = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = ContactPlane, InstanceCount = slots.Length, VisibleInstanceCount = 0 };
        for (int i = 0; i < slots.Length; i++)
        {
            var t = slots[i];
            bed.Items.SetInstanceTransform(i, t);
            bed.Items.SetInstanceCustomData(i, new Color(Mathf.PosMod(i * 0.618f, 1), 0, 0));
            float sx = t.Basis.X.Length(), sz = t.Basis.Z.Length();
            var basis = new Basis(Vector3.Up, Mathf.Atan2(t.Basis.Z.X, t.Basis.Z.Z)) * Basis.FromScale(new Vector3(Mathf.Max(0.1f, aabb.Size.X * sx * 1.65f), 1, Mathf.Max(0.1f, aabb.Size.Z * sz * 1.5f)));
            bed.Contacts[i] = new Transform3D(basis, new Vector3(t.Origin.X, 0.015f + i * 0.000003f, t.Origin.Z));
            bed.Shadows.SetInstanceTransform(i, bed.Contacts[i]);
        }
        parent.AddChild(new MultiMeshInstance3D { Multimesh = bed.Shadows, MaterialOverride = Contact, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        parent.AddChild(new MultiMeshInstance3D { Multimesh = bed.Items, MaterialOverride = OrganicMat(type), CastShadow = Settings.Gfx >= Settings.Quality.Medium ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off });
        return bed;
    }

    static void DrawBed(Bed bed, float dt)
    {
        int target = Mathf.Clamp(bed.Target, 0, bed.Slots.Length);
        bed.Shown = bed.Shown < 0 ? target : Mathf.MoveToward(bed.Shown, target, dt * 5f);
        int count = Mathf.CeilToInt(bed.Shown - 0.001f);
        float frac = bed.Shown - Mathf.Floor(bed.Shown + 0.001f);
        if (count == bed.DrawnCount && Mathf.Abs(frac - bed.DrawnFrac) < 0.02f && bed.BusyStamp == bed.DrawnBusy) return;
        // the newest item swells in as it is laid or grows; the last of a stage shrinks away;
        // a cocoon that has split is the husk's (DrawEmergences) until the nurses clear it
        int from = bed.BusyStamp != bed.DrawnBusy ? 0 : Mathf.Max(0, Mathf.Min(count, bed.DrawnCount) - 1);
        bed.DrawnBusy = bed.BusyStamp;
        for (int i = from; i < count; i++)
        {
            float s = bed.Busy.Contains(i) ? 0.001f : i == count - 1 && frac > 0.001f ? Mathf.Max(0.05f, frac) : 1;
            var t = bed.Slots[i];
            bed.Items.SetInstanceTransform(i, new Transform3D(t.Basis.Scaled(Vector3.One * s), t.Origin));
            bed.Shadows.SetInstanceTransform(i, new Transform3D(bed.Contacts[i].Basis.Scaled(new Vector3(s, 1, s)), bed.Contacts[i].Origin));
        }
        bed.Items.VisibleInstanceCount = count;
        bed.Shadows.VisibleInstanceCount = count;
        bed.DrawnCount = count; bed.DrawnFrac = frac;
    }

    float FloorY => Ground(Local.Me) - NestGeometry.Depth + 0.025f;
    Vector2 RoomCentre(Room r) => _g.Sim.ChamberPos(Local.Me, r.Ch);
    float RoomScale(Room r) => r.Contents.Scale.X;
    Vector2 BedWorld(Room r, Bed b, Vector2 local) => RoomCentre(r) + local * RoomScale(r);

    /// A point just off a bed's rim at `angle`, kept on the room floor.
    Vector2 BedEdge(Room r, Bed b, float angle, float margin)
    {
        var local = b.Centre + new Vector2(Mathf.Cos(angle) * (b.Extent.X + margin), Mathf.Sin(angle) * (b.Extent.Y + margin));
        var (w, _, d) = RoomSize(r.Ch);
        var q = local / new Vector2(w * 0.82f, d * 0.8f);
        if (q.LengthSquared() > 1) local /= q.Length();
        return BedWorld(r, b, local);
    }

    Room RoomOf(int id) => _rooms.TryGetValue((Local.Me, id), out var r) ? r : null;

    /// Chooses which bed each queued clutch lies in. The brood being raised now (the head of the queue) is spread
    /// through every nursery as larvae, then cocoons; the clutches still waiting are eggs, one nursery each.
    void BroodTargets(Colony col)
    {
        int n = _broodRooms.Count;
        if (n == 0) return;
        int baseline = Mathf.Clamp(col.Count[(int)AntType.Worker] / 6, 1, 4);
        foreach (var room in _broodRooms)
        {
            room.Beds[0].Target = baseline + 1; room.Beds[1].Target = baseline; room.Beds[2].Target = baseline / 2;
        }
        int k = 0;
        foreach (var item in col.Queue)
        {
            if (item.Kind == ProdKind.Chamber) continue;
            int ants = Config.SquadSize(item.Kind);
            if (k == 0)
            {
                float p = Mathf.Clamp(item.Progress / Mathf.Max(0.01f, item.Time), 0, 1);
                float larva = Smooth(0.08f, 0.3f, p) * (1 - Smooth(0.62f, 0.82f, p)), pupa = Smooth(0.55f, 0.78f, p);
                foreach (var room in _broodRooms)
                {
                    room.Beds[0].Target += Mathf.RoundToInt((ants + 1) * (1 - Smooth(0.05f, 0.3f, p)));
                    room.Beds[1].Target += Mathf.RoundToInt((ants * 2 + 1) * larva);
                    room.Beds[2].Target += Mathf.RoundToInt(ants * pupa);
                }
            }
            else _broodRooms[(k - 1) % n].Beds[0].Target += ants * 2 + 1;
            k++;
        }
    }

    static float Smooth(float a, float b, float x) { float t = Mathf.Clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); }

    void UpdateBrood(float alpha)
    {
        var sim = _g.Sim;
        var col = sim.Col[Local.Me];
        float now = sim.Time - (1 - alpha) / 30f;
        _broodDt = Mathf.Clamp(now - _clock, 0, 0.1f);
        _clock = now;
        _broodShown = _open > 0.001f && col.Founded;
        _broodRooms.Clear();
        foreach (var ch in col.Chambers)
            if (ch.Kind == ChamberKind.Brood && ch.Built && RoomOf(ch.Id) is { Beds: not null } r) _broodRooms.Add(r);
        TrackLaying(col);
        RecordTrails(sim);
        _eggs.Begin(); _larvae.Begin(); _shells.Begin(); _fleckBatch.Begin();
        if (_broodShown)
        {
            BroodTargets(col);
            foreach (var room in _broodRooms) foreach (var bed in room.Beds) DrawBed(bed, _broodDt);
            UpdateNurses(col);
            DrawLoose();
            DrawEmergences();
        }
        _eggs.End(); _larvae.End(); _shells.End(); _fleckBatch.End();
    }

    // ------------------------------------------------------------------ the queen lays
    /// The queen lays one egg for every ant the head of the queue will become, spread over its real brood timer:
    /// she stops when production stops (no food queued, population cap, queen out of her chamber).
    void TrackLaying(Colony col)
    {
        ProdItem head = col.Queue.Count > 0 ? col.Queue[0] : null;
        if (head == null || head.Kind == ProdKind.Chamber) { _layItem = head; return; }
        if (head == _layItem)
        {
            float dp = head.Progress - _layProgress;
            if (dp > 0) _layAcc += dp / Mathf.Max(0.01f, head.Time) * Config.SquadSize(head.Kind);
        }
        _layItem = head; _layProgress = head.Progress;
        for (int i = 0; _layAcc >= 1 && i < 2; i++) { _layAcc -= 1; Lay(col); }
        _layAcc = Mathf.Min(_layAcc, 1.5f);
    }

    /// Where the queen's gaster ends, and the way she faces. The royal egg pile leaves this apron clear for new eggs.
    (Vector2 tip, Vector2 fwd) GasterTip(int team)
    {
        var sim = _g.Sim; int id = sim.Col[team].QueenId;
        if (_queenTail <= 0) _queenTail = -AntMesh.Build(AntType.Queen).GetAabb().Position.Z;
        if (id < 0) return (sim.ChamberPos(team, sim.Royal(team)), Vector2.Right);
        ref var q = ref sim.Ants[id];
        var fwd = MathX.Dir(q.Heading);
        return (q.Pos - fwd * (_queenTail * q.Size * 0.65f * 0.92f), fwd);
    }

    void Lay(Colony col)
    {
        var sim = _g.Sim;
        if (col.QueenId < 0 || !sim.Ants[col.QueenId].Alive || sim.Ants[col.QueenId].Layer != Sim.NestLayer(Local.Me)) return;
        var (tip, fwd) = GasterTip(Local.Me);
        float size = sim.Ants[col.QueenId].Size * 0.65f;
        var side = new Vector2(-fwd.Y, fwd.X) * _broodRng.R(-0.35f, 0.35f);
        _lastLay = _clock; LastLaid = tip;
        _loose.Add(new Loose
        {
            From = new Vector3(tip.X, FloorY + 0.3f * size, tip.Y),
            Pos = tip - fwd * _broodRng.R(0.5f, 0.75f) + side, Yaw = _broodRng.R(0, Mathf.Tau), Born = _clock,
        });
        _loose.RemoveAll(e => e.Gone >= 0 && _clock - e.Gone > 0.5f); // also while the nest is closed and nothing is drawn
        // too many waiting: the oldest joins the queen's egg pile
        int waiting = 0;
        for (int i = _loose.Count - 1; i >= 0; i--)
            if (_loose[i].Room == -1 && _loose[i].Gone < 0 && _loose[i].By == null && ++waiting > 4) { _loose[i].Gone = _clock; break; }
    }

    /// 0..1 while the queen's gaster presses an egg out.
    public float LayPulse(int team)
    {
        float t = _clock - _lastLay;
        return team != Local.Me || t < 0 || t > 0.7f ? 0 : Mathf.Sin(t / 0.7f * Mathf.Pi);
    }

    void DrawLoose()
    {
        float y = FloorY;
        for (int i = _loose.Count - 1; i >= 0; i--)
        {
            var e = _loose[i];
            float age = _clock - e.Born;
            if (e.Room >= 0 && age > 40 && e.Gone < 0) e.Gone = _clock; // tucked into the bed
            float shrink = e.Gone < 0 ? 1 : 1 - Mathf.Clamp((_clock - e.Gone) / 0.5f, 0, 1);
            if (shrink <= 0 || e.Lifted) { _loose.RemoveAt(i); continue; } // gone, or in a nurse's jaws
            float scale = (e.Larva ? 1.5f : 1.35f) * shrink * (e.Room < 0 ? Smooth(0, 0.3f, age) : 1);
            var rest = new Vector3(e.Pos.X, y + (e.Larva ? 0.17f : 0.2f), e.Pos.Y);
            var p = rest;
            if (e.Room < 0 && age < 0.45f)
            {
                // squeezed from the tip of the gaster, it drops and rolls clear of her
                float u = age / 0.45f;
                p = e.From.Lerp(new Vector3(e.From.X, rest.Y, e.From.Z), u * u).Lerp(rest, Smooth(0.4f, 1, u));
            }
            var basis = new Basis(Vector3.Up, e.Yaw) * new Basis(Vector3.Right, e.Room < 0 ? 0.25f : 0.1f) * Basis.FromScale(Vector3.One * scale);
            (e.Larva ? _larvae : _eggs).Add(basis, p, new Color(Mathf.PosMod(e.Yaw, 1), e.Room == -1 ? 1 - Smooth(5, 14, age) : 0, 0, 0)); // wet and glistening when new
        }
    }

    // ------------------------------------------------------------------ nurses
    void UpdateNurses(Colony col)
    {
        int rooms = _broodRooms.Count;
        bool low = Settings.Gfx == Settings.Quality.Low;
        int want = rooms == 0 ? 0 : Mathf.Min(low ? Mathf.Min(2, rooms) : Mathf.Min(7, rooms * 2 + 1), col.Count[(int)AntType.Worker] / 2);
        while (_nurses.Count > want)
        {
            var gone = _nurses[^1];
            foreach (var e in _loose) if (e.By == gone) e.By = null;
            _nurses.RemoveAt(_nurses.Count - 1);
        }
        while (_nurses.Count < want)
        {
            var room = _broodRooms[_nurses.Count % rooms];
            var bed = room.Beds[_nurses.Count % 3];
            float a = _broodRng.R(0, Mathf.Tau);
            var n = new Nurse { Seed = _broodRng.I(0, 16), Room = room.Ch.Id, Pos = BedEdge(room, bed, a, 0.35f), Task = TaskTend, T0 = _clock, Until = _clock + _broodRng.R(0.5f, 4f) };
            n.Face = BedWorld(room, bed, bed.Centre);
            n.Heading = (n.Face - n.Pos).Angle();
            _nurses.Add(n);
        }
        foreach (var n in _nurses) StepNurse(n);
    }

    void StepNurse(Nurse n)
    {
        float dt = _broodDt;
        switch (n.Task)
        {
            case TaskToPick: case TaskToDrop: case TaskToTend:
                if (Walk(n, dt, n.Carry > 0 ? NurseSpeed * 0.85f : NurseSpeed)) break;
                n.T0 = _clock;
                n.Task = n.Task == TaskToPick ? TaskPick : n.Task == TaskToDrop ? TaskDrop : TaskTend;
                if (n.Task == TaskTend) n.Until = _clock + _broodRng.R(2.2f, 4.5f);
                break;
            case TaskPick:
                TurnTo(n, dt);
                if (n.Target != null && n.Target.Gone >= 0) { n.Target = null; n.Task = TaskTend; n.Until = _clock + 1; break; } // it went to the pile
                if (n.Carry == 0 && _clock - n.T0 > 0.45f)
                {
                    n.Carry = n.Target == null || n.Target.Larva ? 2 : 1;
                    if (n.Target != null) n.Target.Lifted = true; // off the floor and into the jaws
                    n.Target = null;
                }
                if (_clock - n.T0 > 0.85f) GoDeliver(n);
                break;
            case TaskDrop:
                TurnTo(n, dt);
                if (n.Carry > 0 && _clock - n.T0 > 0.4f)
                {
                    var fwd = MathX.Dir(n.Heading);
                    _loose.Add(new Loose { Pos = n.Pos + fwd * 0.5f, Yaw = n.Heading + _broodRng.R(-0.4f, 0.4f), Born = _clock, Room = n.Dest, Larva = n.Carry == 2 });
                    int inRoom = 0;
                    for (int i = _loose.Count - 1; i >= 0; i--) if (_loose[i].Room == n.Dest && _loose[i].Gone < 0 && ++inRoom > 4) { _loose[i].Gone = _clock; break; }
                    n.Carry = 0;
                }
                if (_clock - n.T0 > 0.8f) { n.Task = TaskTend; n.T0 = _clock; n.Until = _clock + _broodRng.R(2f, 4f); n.Shuffles = 1; }
                break;
            default:
                TurnTo(n, dt);
                if (_clock >= n.Until) NextTask(n);
                break;
        }
    }

    /// What a nurse does next: fetch an egg the queen has just laid, move a larva, or tend another bed.
    void NextTask(Nurse n)
    {
        var royal = _g.Sim.Royal(Local.Me);
        for (int i = 0; i < _loose.Count; i++)
        {
            var e = _loose[i];
            if (e.Room != -1 || e.Gone >= 0 || e.By != null || _clock - e.Born < 0.6f || royal == null) continue;
            e.By = n; n.Target = e;
            // land it in the nursery with the fewest loose eggs
            Room best = _broodRooms[0]; int fewest = int.MaxValue;
            foreach (var r in _broodRooms) { int c = 0; foreach (var l in _loose) if (l.Room == r.Ch.Id && l.Gone < 0) c++; if (c < fewest) { fewest = c; best = r; } }
            n.Dest = best.Ch.Id; n.DestBed = 0;
            var away = (e.Pos - _g.Sim.ChamberPos(Local.Me, royal)).Normalized();
            Route(n, royal.Id, e.Pos + away * 0.48f);
            n.Face = e.Pos; n.Task = TaskToPick;
            return;
        }
        var room = _broodRooms[_broodRng.I(0, _broodRooms.Count)];
        bool stay = n.Shuffles > 0 && RoomOf(n.Room) is { Beds: not null } here && _broodRooms.Contains(here);
        if (stay) room = RoomOf(n.Room);
        if (stay || _broodRng.F() < 0.45f)
        {
            // shuffle along the rim of a bed and keep tending: antennae on the brood, mouthparts feeding the larvae
            int b = _broodRng.F() < 0.55f ? 1 : _broodRng.I(0, 3);
            var bed = room.Beds[b];
            if (bed.Shown0 == 0) bed = room.Beds[0];
            Route(n, room.Ch.Id, BedEdge(room, bed, _broodRng.R(0, Mathf.Tau), 0.32f));
            n.Face = BedWorld(room, bed, bed.Centre); n.Task = TaskToTend; n.Shuffles = Mathf.Max(0, n.Shuffles - 1);
            return;
        }
        // sort the brood: a larva carried from one nursery's bed to another's
        var from = room.Beds[1];
        if (from.Shown0 > 0)
        {
            float angle = _broodRng.R(0, Mathf.Tau);
            n.Target = null; // lifted straight off the bed's rim
            var target = _broodRooms[_broodRng.I(0, _broodRooms.Count)];
            n.Dest = target.Ch.Id; n.DestBed = _broodRng.F() < 0.6f ? 1 : 2;
            Route(n, room.Ch.Id, BedEdge(room, from, angle, 0.34f));
            n.Face = BedEdge(room, from, angle, -0.25f); n.Task = TaskToPick;
            return;
        }
        n.Task = TaskTend; n.T0 = _clock; n.Until = _clock + 2;
    }

    void GoDeliver(Nurse n)
    {
        var room = RoomOf(n.Dest);
        if (room?.Beds == null) { n.Carry = 0; n.Task = TaskTend; n.Until = _clock + 1; return; }
        var bed = room.Beds[n.DestBed];
        Route(n, room.Ch.Id, BedEdge(room, bed, _broodRng.R(0, Mathf.Tau), 0.38f));
        n.Face = BedWorld(room, bed, bed.Centre);
        n.Task = TaskToDrop;
    }

    /// The way through the real galleries: out to the middle of this room, node by node along the sim's own
    /// routing, into the destination room and on to the spot. The queen's body is walked around, not through.
    void Route(Nurse n, int toRoom, Vector2 to)
    {
        var sim = _g.Sim;
        n.Path.Clear(); n.Step = 0;
        if (n.Room != toRoom)
        {
            var goal = sim.ChamberPos(Local.Me, sim.Col[Local.Me].Chambers[toRoom]);
            var p = sim.ChamberPos(Local.Me, sim.Col[Local.Me].Chambers[n.Room]);
            AddWaypoint(n, p);
            for (int i = 0; i < 16 && p.DistanceSquaredTo(goal) > 0.01f; i++)
            {
                var step = sim.RouteStep(Local.Me, p, goal);
                if (step.DistanceSquaredTo(p) < 0.0001f) break;
                AddWaypoint(n, step);
                p = step;
            }
            n.Room = toRoom;
        }
        n.Path.Add(to);
    }

    void AddWaypoint(Nurse n, Vector2 p)
    {
        var sim = _g.Sim; var col = sim.Col[Local.Me];
        var royal = sim.Royal(Local.Me);
        if (royal != null && col.QueenId >= 0 && p.DistanceSquaredTo(sim.ChamberPos(Local.Me, royal)) < 0.01f)
        {
            var fwd = MathX.Dir(sim.Ants[col.QueenId].Heading);
            p += new Vector2(-fwd.Y, fwd.X) * (n.Seed % 2 == 0 ? 2.5f : -2.5f);
        }
        n.Path.Add(p);
    }

    /// Moves along the path; false once it has arrived.
    static bool Walk(Nurse n, float dt, float speed)
    {
        n.Amp = 0;
        if (n.Step >= n.Path.Count) return false;
        var d = n.Path[n.Step] - n.Pos;
        float len = d.Length();
        if (len < 0.04f) { n.Step++; return n.Step < n.Path.Count; }
        float diff = MathX.AngleDiff(n.Heading, d.Angle());
        n.Heading += Mathf.Clamp(diff, -7f * dt, 7f * dt);
        float step = Mathf.Min(len, speed * Mathf.Clamp(1 - Mathf.Abs(diff) / 1.8f, 0.2f, 1) * dt);
        n.Pos += d / len * step;
        n.Gait += step / 0.55f;
        n.Amp = dt > 0 ? step / dt / 7.2f : 0;
        if (len - step < 0.04f) n.Step++;
        return true;
    }

    static void TurnTo(Nurse n, float dt)
    {
        var d = n.Face - n.Pos;
        if (d.LengthSquared() < 0.01f) return;
        float diff = MathX.AngleDiff(n.Heading, d.Angle());
        float turn = Mathf.Clamp(diff, -4f * dt, 4f * dt);
        n.Heading += turn;
        n.Gait += Mathf.Abs(turn) * 0.4f; // turning on the spot moves the feet
        n.Amp = Mathf.Abs(turn) > 0.002f ? 0.12f : 0;
    }

    internal void DrawNurses(AntRenderer r)
    {
        if (!_broodShown) return;
        float y = Ground(Local.Me) - NestGeometry.Depth + 0.02f;
        foreach (var n in _nurses)
        {
            float t = _clock - n.T0;
            Color data;
            switch (n.Task)
            {
                case TaskTend:
                    // head down over the brood, antennae tapping, mouthparts working
                    data = AntRenderer.Pack(n.Seed, 3, n.Gait, 0.45f * Mathf.Sin(t * 0.6f + n.Seed), -0.48f + 0.09f * Mathf.Sin(t * 2.1f + n.Seed), n.Amp, n.Carry > 0,
                        0.32f * Mathf.Max(0, Mathf.Sin(t * 5.2f + n.Seed)));
                    break;
                case TaskPick: case TaskDrop:
                    {
                        float u = Mathf.Clamp(t / 0.85f, 0, 1);
                        data = AntRenderer.Pack(n.Seed, 0, n.Gait, 0, -0.6f * Mathf.Sin(u * Mathf.Pi), n.Amp, n.Carry > 0, n.Task == TaskPick && n.Carry == 0 ? u * 1.3f : 0.15f);
                        break;
                    }
                default:
                    data = AntRenderer.Pack(n.Seed, 0, n.Gait, 0, n.Carry > 0 ? 0.05f : -0.08f, n.Amp, n.Carry > 0, 0);
                    break;
            }
            r.AddWorker(Local.Me, AntRenderer.Orient(Vector3.Up, n.Heading, 1f), new Vector3(n.Pos.X, y, n.Pos.Y), data, n.Carry);
        }
    }

    // ------------------------------------------------------------------ hatching
    /// The sim hatched an ant in a nursery: it splits one of that bed's cocoons and climbs out.
    public void Hatched(in SimEvent e)
    {
        var sim = _g.Sim;
        int id = e.HatchP1 - 1;
        if (e.Team != Local.Me || id < 0 || id >= sim.High || !sim.Ants[id].Alive || sim.Ants[id].Layer == 0) return;
        ref var a = ref sim.Ants[id];
        // the nursery it hatched in, and a cocoon there
        Room room = null; float best = float.MaxValue;
        foreach (var ch in sim.Col[Local.Me].Chambers)
        {
            if (ch.Kind != ChamberKind.Brood || !ch.Built) continue;
            float d = sim.ChamberPos(Local.Me, ch).DistanceSquaredTo(e.To);
            if (d < best) { best = d; room = RoomOf(ch.Id); }
        }
        float size = a.Size;
        var shell = new Vector3(e.To.X, FloorY + 0.26f * size, e.To.Y);
        float yaw = _broodRng.R(0, Mathf.Tau);
        Bed cocoons = room?.Beds?[2];
        int slot = -1;
        if (cocoons != null)
            for (int s = cocoons.Shown0 - 1; s >= 0 && slot < 0; s--) if (!cocoons.Busy.Contains(s)) slot = s;
        if (slot >= 0)
        {
            // one of the bed's own cocoons splits; it is the husk's until the nurses clear it away
            var t = cocoons.Slots[slot];
            var p = BedWorld(room, cocoons, new Vector2(t.Origin.X, t.Origin.Z));
            shell = new Vector3(p.X, shell.Y, p.Y);
            yaw = Mathf.Atan2(t.Basis.Z.X, t.Basis.Z.Z);
            cocoons.Busy.Add(slot); cocoons.BusyStamp++;
        }
        var fwd = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
        var em = new Emergence
        {
            Id = id, Serial = a.Serial, Room = room?.Ch.Id ?? -1, Start = sim.Time - 1f / 30f, Yaw = yaw, Size = size, Shell = shell,
            Exit = new Vector2(shell.X, shell.Z) + fwd * 0.62f * size, RawGait = a.Gait, Bed = slot >= 0 ? cocoons : null, Slot = slot,
        };
        em.Trail.Add(new Sample { T = sim.Time, P = a.Pos, H = a.Heading, G = 0, L = a.Layer });
        em.LastTick = sim.Ticks;
        em.WalkLen = em.Exit.DistanceTo(a.Pos);
        _hatch[id] = em;
    }

    void RecordTrails(Sim sim)
    {
        if (_hatch.Count == 0) return;
        List<int> done = null;
        foreach (var e in _hatch.Values)
        {
            ref var a = ref sim.Ants[e.Id];
            if (!a.Alive || a.Serial != e.Serial || _clock - e.Start > Emerge + Tanning + 30)
            {
                if (e.Slot >= 0) { e.Bed.Busy.Remove(e.Slot); e.Bed.BusyStamp++; }
                (done ??= new()).Add(e.Id); continue;
            }
            if (e.Replayed || sim.Ticks == e.LastTick) continue;
            var last = e.Trail[^1];
            float g = last.G + Mathf.PosMod(a.Gait - e.RawGait, 1);
            e.RawGait = a.Gait; e.LastTick = sim.Ticks;
            e.Trail.Add(new Sample { T = sim.Time, P = a.Pos, H = a.Heading, G = g, L = a.Layer });
        }
        if (done != null) foreach (int id in done) _hatch.Remove(id);
    }

    /// The pose of an ant that hatched moments ago, or default when the renderer should draw it as the sim has it.
    public CallowPose Callow(int i, in Ant a, ref Vector2 ip, ref float ih)
    {
        if (_hatch.Count == 0 || !_hatch.TryGetValue(i, out var e) || e.Serial != a.Serial) return default;
        float age = _clock - e.Start;
        var pose = new CallowPose { Pale = Mathf.RoundToInt(15 * (1 - Smooth(Emerge, Emerge + Tanning, age))) };
        if (e.Replayed) return pose;
        pose.Active = true; pose.Scale = 1; pose.Layer = e.Trail[0].L;
        float s = e.Size;
        if (age < Emerge)
        {
            float u = Mathf.Max(0, age) / Emerge, out_ = Smooth(0.32f, 1, u);
            var fwd = new Vector2(Mathf.Sin(e.Yaw), Mathf.Cos(e.Yaw));
            var shell = new Vector2(e.Shell.X, e.Shell.Z);
            ip = shell - fwd * 0.12f * s + (e.Exit - shell + fwd * 0.12f * s) * out_;
            ih = fwd.Angle();
            pose.Rise = 0.45f * Smooth(0.28f, 0.55f, u) * (1 - Smooth(0.62f, 1, u));
            pose.Scale = Mathf.Lerp(0.86f, 1, Smooth(0.3f, 1, u));
            pose.Lift = (e.Shell.Y - FloorY - 0.2f * s) * (1 - Smooth(0.3f, 0.85f, u));
            pose.Amp = 0.3f * Smooth(0.35f, 0.5f, u);
            pose.Gait = Mathf.PosMod(age * 1.6f, 1);
            pose.Kind = 2; pose.KindT = Smooth(0.3f, 1, u); // the legs splay as they unfold
            return pose;
        }
        float walk = e.WalkLen / CallowWalk;
        var t0 = e.Trail[0];
        if (age < Emerge + walk)
        {
            float d = (age - Emerge) * CallowWalk;
            ip = e.Exit.Lerp(t0.P, e.WalkLen < 0.01f ? 1 : d / e.WalkLen);
            float toward = (t0.P - e.Exit).LengthSquared() > 0.01f ? (t0.P - e.Exit).Angle() : new Vector2(Mathf.Sin(e.Yaw), Mathf.Cos(e.Yaw)).Angle();
            ih = Mathf.LerpAngle(new Vector2(Mathf.Sin(e.Yaw), Mathf.Cos(e.Yaw)).Angle(), toward, Smooth(0, 0.35f, age - Emerge));
            pose.Amp = CallowWalk / 7.2f;
            pose.Gait = Mathf.PosMod(d / (0.55f * s), 1);
            return pose;
        }
        float tau = t0.T + (age - Emerge - walk) * CatchUp;
        var lastS = e.Trail[^1];
        if (tau >= _clock || tau >= lastS.T) { e.Replayed = true; pose.Active = false; return pose; }
        while (e.Cursor < e.Trail.Count - 2 && e.Trail[e.Cursor + 1].T <= tau) e.Cursor++;
        var p0 = e.Trail[e.Cursor]; var p1 = e.Trail[e.Cursor + 1];
        float k = Mathf.Clamp((tau - p0.T) / Mathf.Max(0.0001f, p1.T - p0.T), 0, 1);
        ip = p0.P.Lerp(p1.P, k);
        float walkHeading = (t0.P - e.Exit).LengthSquared() > 0.01f ? (t0.P - e.Exit).Angle() : p0.H;
        ih = Mathf.LerpAngle(walkHeading, Mathf.LerpAngle(p0.H, p1.H, k), Smooth(0, 0.3f, tau - t0.T));
        pose.Layer = k < 0.5f ? p0.L : p1.L;
        pose.Gait = Mathf.PosMod(Mathf.Lerp(p0.G, p1.G, k), 1);
        pose.Amp = Mathf.Clamp(p0.P.DistanceTo(p1.P) / Mathf.Max(0.0001f, p1.T - p0.T) * CatchUp / Mathf.Max(0.1f, Config.Of(a.Type).Speed), 0, 1);
        return pose;
    }

    void DrawEmergences()
    {
        bool particles = Settings.Gfx >= Settings.Quality.Medium;
        foreach (var e in _hatch.Values)
        {
            float age = _clock - e.Start;
            if (age > Emerge + 12)
            {
                if (e.Slot >= 0) { e.Bed.Busy.Remove(e.Slot); e.Bed.BusyStamp++; e.Slot = -1; }
                continue;
            }
            float u = Mathf.Clamp(age / Emerge, 0, 1);
            // the nurses chew it open along its length and the halves fall apart; later they clear the husk away
            float open = 1.45f * Smooth(0.08f, 0.45f, u);
            float keep = 1 - Smooth(Emerge + 10.5f, Emerge + 12, age);
            var frame = new Transform3D(new Basis(Vector3.Up, e.Yaw) * Basis.FromScale(Vector3.One * 1.62f * e.Size * keep), e.Shell);
            // each half rolls onto its back, hollow side up, and the two come apart
            float apart = 0.07f * open;
            var a = new Transform3D(new Basis(Vector3.Back, -open), new Vector3(apart, 0, 0));
            var b = new Transform3D(new Basis(Vector3.Back, open), new Vector3(-apart, 0, 0)) * new Transform3D(new Basis(Vector3.Back, Mathf.Pi), Vector3.Zero);
            var ta = frame * a; var tb = frame * b;
            _shells.Add(ta.Basis, ta.Origin, new Color(0.3f, 0, 0, 0));
            _shells.Add(tb.Basis, tb.Origin, new Color(0.7f, 0, 0, 0));
            if (!e.Split && u > 0.15f)
            {
                e.Split = true;
                if (particles)
                    for (int i = 0; i < 9; i++)
                        _flecks.Add(new Fleck { P = e.Shell + Vector3.Up * 0.15f * e.Size, V = new Vector3(_broodRng.R(-0.9f, 0.9f), _broodRng.R(0.6f, 1.5f), _broodRng.R(-0.9f, 0.9f)), Life = _broodRng.R(1.6f, 2.8f) });
            }
        }
        float floor = FloorY + 0.02f;
        for (int i = _flecks.Count - 1; i >= 0; i--)
        {
            var f = _flecks[i];
            f.Life -= _broodDt;
            if (f.Life <= 0) { _flecks[i] = _flecks[^1]; _flecks.RemoveAt(_flecks.Count - 1); continue; }
            f.V.Y -= 3.2f * _broodDt; f.V *= 1 - _broodDt * 1.8f; // silk drifts more than it falls
            f.P += f.V * _broodDt;
            if (f.P.Y < floor) { f.P.Y = floor; f.V = Vector3.Zero; }
            _flecks[i] = f;
            _fleckBatch.Add(Basis.FromScale(Vector3.One * 0.32f * Mathf.Min(1, f.Life)), f.P, default);
        }
    }
}
