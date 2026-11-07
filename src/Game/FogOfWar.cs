using Godot;

namespace Mandible;

/// Player vision: R = currently visible (smoothed), G = explored (soft at the frontier, so the edge of the
/// unknown is a feathered band rather than a stamped circle). FogOverlay turns it into black over the screen.
public sealed class FogOfWar
{
    public const float Cell = 2f;
    public readonly int N;
    readonly float[] _vis, _target;
    readonly byte[] _explored, _pixels;
    readonly Image _img;
    public readonly ImageTexture Texture;
    float _acc;
    public bool RevealAll; // debug / screenshot tours

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
        return _explored[z * N + x] > 96;
    }

    public float VisibleValue(int x, int z) => _vis[z * N + x];
    public bool ExploredCell(int x, int z) => _explored[z * N + x] > 96;
    public float ExploredValue(int x, int z) => _explored[z * N + x] / 255f;

    /// Marks ground as already known (dim, not seen): a level's trail is known before anyone walks it.
    public void Know(System.Func<Vector2, float> along)
    {
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                float v = Mathf.Clamp(along(new Vector2((x + 0.5f) * Cell, (z + 0.5f) * Cell)), 0, 1);
                int i = z * N + x;
                _explored[i] = (byte)Mathf.Max(_explored[i], (int)(v * 255));
            }
    }

    public void Update(Sim sim, float dt)
    {
        _acc += dt;
        if (_acc >= 0.12f)
        {
            _acc = 0;
            System.Array.Clear(_target);
            if (RevealAll) { System.Array.Fill(_target, 1f); System.Array.Fill(_explored, (byte)255); }
            for (int i = 0; i < sim.High; i++)
            {
                ref var a = ref sim.Ants[i];
                if (!a.Alive || a.Team != Local.Me) continue;
                Stamp(a.Pos, Config.Of(a.Type).Sight);
            }
            foreach (var z in sim.Scouts) if (z.Team == Local.Me) Stamp(z.Pos, z.R + 3f); // Scout Swarm
            // level 1 starts with no nest: until the queen reaches the clearing only the trail is known
            if (sim.Mission is not { Episode: 1, Stage: <= ExpeditionStage.EscortQueen }) Stamp(sim.W.Nests[Local.Me].Pos, 34);
            // the opening's first target stays in sight so it can be seen, clicked and attacked from the start
            if (sim.Mission is { Episode: 1, Stage: ExpeditionStage.ClearTrail } m)
                foreach (int id in m.Guards) if (sim.Ants[id].Alive) Stamp(sim.Ants[id].Pos, 7);
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
                byte e = (byte)Mathf.Min(255, (int)(v * 2f * 255));
                if (e > _explored[i]) _explored[i] = e;
            }
        }
    }
}
