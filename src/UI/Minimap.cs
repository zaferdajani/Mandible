using Godot;

namespace Mandible;

public partial class Minimap : Control
{
    public GameSession Game;
    Image _img;
    ImageTexture _tex;
    byte[] _base, _px;
    int _res;
    float _acc = 1;
    bool _dragging;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
    }

    void Init()
    {
        var b = Game.World.MinimapBase;
        _res = b.GetWidth();
        var src = (Image)b.Duplicate();
        src.Convert(Image.Format.Rgba8);
        _base = src.GetData();
        _px = new byte[_base.Length];
        _img = Image.CreateFromData(_res, _res, false, Image.Format.Rgba8, _base);
        _tex = ImageTexture.CreateFromImage(_img);
    }

    public override void _Process(double delta)
    {
        if (Game?.Sim == null) return;
        if (_tex == null) Init();
        _acc += (float)delta;
        if (_acc > 0.2f) { _acc = 0; Compose(); }
        QueueRedraw();
    }

    void Compose()
    {
        var sim = Game.Sim;
        var fog = Game.Fog;
        float S = Config.MapSize;
        int fogN = fog?.N ?? 1;
        System.Buffer.BlockCopy(_base, 0, _px, 0, _base.Length);
        if (fog != null)
            for (int y = 0; y < _res; y++)
                for (int x = 0; x < _res; x++)
                {
                    int fx = x * fogN / _res, fz = y * fogN / _res;
                    float m = !fog.ExploredCell(fx, fz) ? 0.12f : Mathf.Lerp(0.5f, 1f, fog.VisibleValue(fx, fz));
                    int i = (y * _res + x) * 4;
                    _px[i] = (byte)(_px[i] * m); _px[i + 1] = (byte)(_px[i + 1] * m); _px[i + 2] = (byte)(_px[i + 2] * m);
                }
        foreach (var f in sim.W.Food)
        {
            if (!f.Alive || (fog != null && !fog.Explored(f.Pos))) continue;
            Dot(f.Pos, f.Kind == FoodKind.Corpse ? 1 : Mathf.Clamp((int)(f.Radius / S * _res), 1, 4), f.Kind == FoodKind.Corpse ? new Color(0.6f, 0.55f, 0.3f) : new Color(0.75f, 0.95f, 0.35f));
        }
        for (int i = 0; i < sim.High; i++)
        {
            ref var a = ref sim.Ants[i];
            if (!a.Alive) continue;
            if (a.Team != Local.Me && fog != null && !fog.Visible(a.Pos)) continue;
            int r = a.Type == AntType.Queen || a.Type == AntType.StagBeetle ? 3 : (a.Type == AntType.Major || Config.IsBug(a.Type) ? 2 : 1);
            Dot(a.Pos, r, Config.TeamUi(a.Team));
        }
        _img.SetData(_res, _res, false, Image.Format.Rgba8, _px);
        _tex.Update(_img);
    }

    void Dot(Vector2 p, int r, Color c)
    {
        int cx = (int)(p.X / Config.MapSize * _res), cy = (int)(p.Y / Config.MapSize * _res);
        byte R = (byte)(c.R * 255), G = (byte)(c.G * 255), B = (byte)(c.B * 255);
        for (int y = cy - r + 1; y < cy + r; y++)
            for (int x = cx - r + 1; x < cx + r; x++)
            {
                if (x < 0 || y < 0 || x >= _res || y >= _res) continue;
                int i = (y * _res + x) * 4;
                _px[i] = R; _px[i + 1] = G; _px[i + 2] = B;
            }
    }

    public override void _Draw()
    {
        if (_tex == null) return;
        DrawTextureRect(_tex, new Rect2(Vector2.Zero, Size), false);
        if (Game.Lanes)
        {
            // the three lanes, faintly
            float k = Size.X / Config.MapSize;
            foreach (var lane in Game.Sim.Lanes)
            {
                var line = new Vector2[lane.Length];
                for (int i = 0; i < lane.Length; i++) line[i] = lane[i] * k;
                DrawPolyline(line, new Color(0.95f, 0.85f, 0.55f, 0.28f), 3f, true);
            }
            var sim = Game.Sim;
            // the aphid herd while it is in the pasture, and the Old Stag's log (brighter while it is awake)
            if (sim.HerdHere)
            {
                var p = sim.Pasture * k;
                var c = sim.HerdTeam < 0 ? new Color(0.75f, 0.95f, 0.45f) : Config.TeamUi(sim.HerdTeam);
                DrawCircle(p, 4.5f, c);
                if (sim.HerdTeam >= 0) DrawArc(p, 7, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * sim.HerdBar, 24, c, 2, true);
            }
            var stagAt = sim.StagAwake ? sim.Ants[sim.StagId].Pos * k : sim.StagLair * k;
            var stagColor = !sim.StagAwake ? new Color(0.6f, 0.45f, 0.3f, 0.7f) : sim.StagTeam < 0 ? new Color(1f, 0.55f, 0.25f) : Config.TeamUi(sim.StagTeam);
            DrawArc(stagAt, sim.StagAwake ? 6 : 4, 0, Mathf.Tau, 16, stagColor, sim.StagAwake ? 3 : 1.5f, true);
        }
        // camera footprint
        var cam = Game.Camera;
        var vp = GetViewport().GetVisibleRect().Size;
        Vector2[] corners = { Vector2.Zero, new(vp.X, 0), vp, new(0, vp.Y) };
        var pts = new Vector2[5];
        for (int i = 0; i < 4; i++)
        {
            var from = cam.Cam.ProjectRayOrigin(corners[i]);
            var dir = cam.Cam.ProjectRayNormal(corners[i]);
            float t = dir.Y < -0.01f ? (from.Y - cam.Focus.Y) / -dir.Y : 400;
            var g = from + dir * Mathf.Min(t, 500);
            pts[i] = new Vector2(g.X, g.Z) / Config.MapSize * Size;
        }
        pts[4] = pts[0];
        DrawPolyline(pts, new Color(1, 1, 1, 0.85f), 1.5f, true);
        // nest markers
        foreach (var n in Game.World.Nests)
        {
            if (n.Team != Local.Me && Game.Fog != null && !Game.Fog.Explored(n.Pos)) continue;
            var p = n.Pos / Config.MapSize * Size;
            DrawArc(p, 7, 0, Mathf.Tau, 20, Config.TeamUi(n.Team), 2, true);
        }
        DrawRect(new Rect2(Vector2.Zero, Size), UiTheme.Bronze, false, 1);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb)
        {
            var world = mb.Position / Size * Config.MapSize;
            if (mb.ButtonIndex == MouseButton.Left)
            {
                _dragging = mb.Pressed;
                if (mb.Pressed) Game.Camera.PanTo(world);
            }
            else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed) Game.Input.CommandAt(world, Input.IsKeyPressed(Key.A));
            AcceptEvent();
        }
        else if (e is InputEventMouseMotion mm && _dragging)
        {
            Game.Camera.PanTo(mm.Position / Size * Config.MapSize);
            AcceptEvent();
        }
    }
}
