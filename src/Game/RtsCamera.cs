using Godot;

namespace Mandible;

/// Classic RTS camera with a macro-lens feel: pitch flattens as you zoom in and the
/// depth of field tightens, so the backyard reads as a tiny world.
public partial class RtsCamera : Node3D
{
    public Camera3D Cam;
    public CameraAttributesPractical Attr;
    public Heightfield HF;
    public Vector3 Focus = new(80, 0, 240);
    public float Yaw = -Mathf.Pi * 0.25f;
    public float Distance = 70, TargetDistance = 70;
    public const float MinDist = 14, MaxDist = 175;
    public bool UserControl = true;
    public bool EdgeScroll = true;
    Vector3 _focusTarget;
    float _yawTarget;
    bool _rotating;

    public override void _Ready()
    {
        Cam = new Camera3D { Fov = 42, Near = 0.2f, Far = 1200, Current = true };
        Attr = new CameraAttributesPractical
        {
            DofBlurFarEnabled = true, DofBlurNearEnabled = true, DofBlurAmount = 0.06f,
        };
        Cam.Attributes = Attr;
        AddChild(Cam);
        _focusTarget = Focus;
        _yawTarget = Yaw;
    }

    public void JumpTo(Vector2 p, float? dist = null)
    {
        _focusTarget = new Vector3(p.X, HF?.Sample(p.X, p.Y) ?? 0, p.Y);
        Focus = _focusTarget;
        if (dist.HasValue) { TargetDistance = dist.Value; Distance = dist.Value; }
    }

    public void PanTo(Vector2 p) { _focusTarget = new Vector3(p.X, _focusTarget.Y, p.Y); }

    public void SetYaw(float y, bool snap) { _yawTarget = y; if (snap) Yaw = y; }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!UserControl) return;
        if (e is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed) TargetDistance = Mathf.Max(MinDist, TargetDistance * 0.88f);
            if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed) TargetDistance = Mathf.Min(MaxDist, TargetDistance * 1.13f);
            if (mb.ButtonIndex == MouseButton.Middle) _rotating = mb.Pressed;
        }
        else if (e is InputEventMouseMotion mm && _rotating)
        {
            _yawTarget -= mm.Relative.X * 0.006f;
            TargetDistance = Mathf.Clamp(TargetDistance * (1 + mm.Relative.Y * 0.004f), MinDist, MaxDist);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (UserControl)
        {
            var move = Vector2.Zero;
            if (Input.IsKeyPressed(Key.Up)) move.Y -= 1;
            if (Input.IsKeyPressed(Key.Down)) move.Y += 1;
            if (Input.IsKeyPressed(Key.Left)) move.X -= 1;
            if (Input.IsKeyPressed(Key.Right)) move.X += 1;
            if (Input.IsKeyPressed(Key.Q) && Input.IsKeyPressed(Key.Alt)) _yawTarget += dt * 1.5f;
            if (EdgeScroll && DisplayServer.WindowIsFocused())
            {
                var vp = GetViewport();
                var mp = vp.GetMousePosition();
                var size = vp.GetVisibleRect().Size;
                const float m = 6;
                if (mp.X >= 0 && mp.Y >= 0 && mp.X <= size.X && mp.Y <= size.Y)
                {
                    if (mp.X < m) move.X -= 1;
                    if (mp.X > size.X - m) move.X += 1;
                    if (mp.Y < m) move.Y -= 1;
                    if (mp.Y > size.Y - m) move.Y += 1;
                }
            }
            if (move != Vector2.Zero)
            {
                var fwd = new Vector3(-Mathf.Sin(Yaw), 0, -Mathf.Cos(Yaw));
                var right = new Vector3(Mathf.Cos(Yaw), 0, -Mathf.Sin(Yaw));
                var d = (right * move.X - fwd * move.Y).Normalized() * Distance * 1.1f * dt;
                _focusTarget += d;
            }
        }
        _focusTarget.X = Mathf.Clamp(_focusTarget.X, 0, Config.MapSize);
        _focusTarget.Z = Mathf.Clamp(_focusTarget.Z, 0, Config.MapSize);
        if (HF != null) _focusTarget.Y = Mathf.Lerp(_focusTarget.Y, HF.Sample(_focusTarget.X, _focusTarget.Z), Mathf.Min(1, dt * 4));
        float k = 1 - Mathf.Exp(-dt * 10);
        Focus = Focus.Lerp(_focusTarget, k);
        Distance = Mathf.Lerp(Distance, TargetDistance, 1 - Mathf.Exp(-dt * 8));
        Yaw = Mathf.LerpAngle(Yaw, _yawTarget, 1 - Mathf.Exp(-dt * 8));
        Apply();
    }

    public void Apply()
    {
        float t = Mathf.Clamp((Distance - MinDist) / (MaxDist - MinDist), 0, 1);
        float pitch = Mathf.Lerp(Mathf.DegToRad(30), Mathf.DegToRad(60), Mathf.Pow(t, 0.6f));
        var offset = new Vector3(Mathf.Sin(Yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(Yaw) * Mathf.Cos(pitch)) * Distance;
        var pos = Focus + offset;
        if (HF != null) pos.Y = Mathf.Max(pos.Y, HF.Sample(pos.X, pos.Z) + 3);
        Cam.GlobalTransform = new Transform3D(Basis.Identity, pos).LookingAt(Focus, Vector3.Up);
        // tilt-shift: sharp band at the focus, soft foreground and background
        float d = pos.DistanceTo(Focus);
        Attr.DofBlurFarDistance = d * 1.18f;
        Attr.DofBlurFarTransition = d * 0.9f;
        Attr.DofBlurNearDistance = d * 0.62f;
        Attr.DofBlurNearTransition = d * 0.35f;
        Attr.DofBlurAmount = Mathf.Lerp(0.09f, 0.035f, t);
    }

    public bool ScreenToGround(Vector2 screen, out Vector3 hit)
    {
        var from = Cam.ProjectRayOrigin(screen);
        var dir = Cam.ProjectRayNormal(screen);
        return HF.Raycast(from, dir, out hit);
    }
}
