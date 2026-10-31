using Godot;

namespace Mandible;

/// Player vision: R = currently visible (smoothed), G = explored. Shared by every world shader.
public sealed class FogOfWar
{
    public const float Cell = 2f;
    public readonly int N;
    readonly float[] _vis, _target;
    readonly byte[] _explored, _pixels;
    readonly Image _img;
    public readonly ImageTexture Texture;
    float _acc;

    public FogOfWar(World w)
    {
        N = (int)(Config.MapSize / Cell);
        _vis = new float[N * N];
        _target = new float[N * N];
        _explored = new byte[N * N];
        _pixels = new byte[N * N * 2];
        _img = Image.CreateFromData(N, N, false, Image.Format.Rg8, _pixels);
        Texture = ImageTexture.CreateFromImage(_img);
        w.FowTexture = Texture;
        foreach (var m in w.FowMaterials) m.SetShaderParameter("fow_tex", Texture);
    }

    public bool Visible(Vector2 p)
    {
        int x = (int)(p.X / Cell), z = (int)(p.Y / Cell);
        if (x < 0 || z < 0 || x >= N || z >= N) return false;
        return _target[z * N + x] > 0.5f;
    }

    public bool Explored(Vector2 p)
    {
        int x = (int)(p.X / Cell), z = (int)(p.Y / Cell);
        if (x < 0 || z < 0 || x >= N || z >= N) return false;
        return _explored[z * N + x] > 0;
    }

    public float VisibleValue(int x, int z) => _vis[z * N + x];
    public bool ExploredCell(int x, int z) => _explored[z * N + x] > 0;

    public void Update(Sim sim, float dt)
    {
        _acc += dt;
        if (_acc >= 0.12f)
        {
            _acc = 0;
            System.Array.Clear(_target);
            for (int i = 0; i < sim.High; i++)
            {
                ref var a = ref sim.Ants[i];
                if (!a.Alive || a.Team != Config.Player) continue;
                Stamp(a.Pos, Config.Of(a.Type).Sight);
            }
            Stamp(sim.W.Nests[Config.Player].Pos, 34);
        }
        float k = Mathf.Min(1, dt * 5);
        for (int i = 0; i < _vis.Length; i++)
        {
            _vis[i] += (_target[i] - _vis[i]) * k;
            _pixels[i * 2] = (byte)(_vis[i] * 255);
            _pixels[i * 2 + 1] = _explored[i];
        }
        _img.SetData(N, N, false, Image.Format.Rg8, _pixels);
        Texture.Update(_img);
    }

    void Stamp(Vector2 p, float r)
    {
        int cr = (int)(r / Cell) + 1;
        int cx = (int)(p.X / Cell), cz = (int)(p.Y / Cell);
        float r2 = (r / Cell) * (r / Cell);
        for (int z = cz - cr; z <= cz + cr; z++)
        {
            if (z < 0 || z >= N) continue;
            for (int x = cx - cr; x <= cx + cr; x++)
            {
                if (x < 0 || x >= N) continue;
                float dx = x - cx, dz = z - cz;
                float d2 = dx * dx + dz * dz;
                if (d2 > r2) continue;
                int i = z * N + x;
                float v = Mathf.Clamp((1 - d2 / r2) * 3f, 0, 1);
                if (v > _target[i]) _target[i] = v;
                _explored[i] = 255;
            }
        }
    }
}
