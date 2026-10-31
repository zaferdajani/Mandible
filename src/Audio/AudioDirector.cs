using System.Collections.Generic;
using Godot;

namespace Mandible;

public partial class AudioDirector : Node
{
    readonly Dictionary<string, AudioStream[]> _banks = new();
    readonly List<AudioStreamPlayer3D> _pool3d = new();
    readonly List<AudioStreamPlayer> _poolUi = new();
    readonly Dictionary<string, double> _lastPlayed = new();
    AudioStreamPlayer _calm, _battle, _amb, _swarm;
    float _intensity, _swarmLevel, _musicFade = 1f;
    int _next3d, _nextUi;
    readonly RandomNumberGenerator _rng = new();
    public float MusicVolume = 0.8f, SfxVolume = 1f;

    public override void _Ready()
    {
        foreach (var bus in new[] { "Music", "Sfx", "Amb", "Ui" })
        {
            AudioServer.AddBus();
            int idx = AudioServer.BusCount - 1;
            AudioServer.SetBusName(idx, bus);
            AudioServer.SetBusSend(idx, "Master");
        }
        // gentle bus compression keeps big battles from clipping
        int sfx = AudioServer.GetBusIndex("Sfx");
        AudioServer.AddBusEffect(sfx, new AudioEffectCompressor { Threshold = -14, Ratio = 4, AttackUs = 5000, ReleaseMs = 120 });
        int master = AudioServer.GetBusIndex("Master");
        AudioServer.AddBusEffect(master, new AudioEffectHardLimiter { CeilingDb = -0.5f });

        Bank("bite", 5, "bite_");
        Bank("death", 3, "death_");
        foreach (var s in new[] { "pickup", "deliver", "spawn", "ui_click", "ui_hover", "ui_error", "ui_ready", "ui_queue", "alert", "victory", "defeat", "levelup", "web", "web_land", "sweep", "sting", "research" })
            Bank(s, 1, s);

        _calm = Loop("music_calm", "Music");
        _battle = Loop("music_battle", "Music");
        _amb = Loop("amb_yard", "Amb");
        _swarm = Loop("swarm", "Amb");
        _battle.VolumeDb = -60;
        _swarm.VolumeDb = -60;

        for (int i = 0; i < 28; i++)
        {
            var p = new AudioStreamPlayer3D
            {
                Bus = "Sfx", UnitSize = 28, MaxDistance = 320, AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Logarithmic,
                AttenuationFilterCutoffHz = 9000, AttenuationFilterDb = -10, PanningStrength = 0.9f,
            };
            AddChild(p);
            _pool3d.Add(p);
        }
        for (int i = 0; i < 6; i++)
        {
            var p = new AudioStreamPlayer { Bus = "Ui" };
            AddChild(p);
            _poolUi.Add(p);
        }
    }

    void Bank(string key, int count, string prefix)
    {
        var list = new List<AudioStream>();
        for (int i = 0; i < count; i++)
        {
            string path = count == 1 ? $"res://assets/audio/{prefix}.wav" : $"res://assets/audio/{prefix}{i}.wav";
            if (ResourceLoader.Exists(path)) list.Add(GD.Load<AudioStream>(path));
        }
        _banks[key] = list.ToArray();
    }

    AudioStreamPlayer Loop(string name, string bus)
    {
        var path = $"res://assets/audio/{name}.wav";
        var p = new AudioStreamPlayer { Bus = bus };
        AddChild(p);
        if (!ResourceLoader.Exists(path)) return p;
        var s = GD.Load<AudioStreamWav>(path);
        s.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        s.LoopBegin = 0;
        s.LoopEnd = (int)(s.GetLength() * s.MixRate);
        p.Stream = s;
        p.Play();
        return p;
    }

    bool Throttle(string key, double gap)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        if (_lastPlayed.TryGetValue(key, out var t) && now - t < gap) return false;
        _lastPlayed[key] = now;
        return true;
    }

    public void At(string key, Vector3 pos, float db = 0, float gap = 0.04f, float pitchVar = 0.12f, float pitch = 1f)
    {
        if (!_banks.TryGetValue(key, out var bank) || bank.Length == 0) return;
        if (!Throttle(key, gap)) return;
        var p = _pool3d[_next3d];
        _next3d = (_next3d + 1) % _pool3d.Count;
        p.Stream = bank[_rng.RandiRange(0, bank.Length - 1)];
        p.GlobalPosition = pos;
        p.VolumeDb = db + Mathf.LinearToDb(SfxVolume);
        p.PitchScale = pitch * (1 + _rng.RandfRange(-pitchVar, pitchVar));
        p.Play();
    }

    public void Ui(string key, float db = 0)
    {
        if (!_banks.TryGetValue(key, out var bank) || bank.Length == 0) return;
        if (!Throttle("ui_" + key, 0.03)) return;
        var p = _poolUi[_nextUi];
        _nextUi = (_nextUi + 1) % _poolUi.Count;
        p.Stream = bank[0];
        p.VolumeDb = db + Mathf.LinearToDb(SfxVolume);
        p.Play();
    }

    /// 0 = calm, 1 = full battle.
    public void SetIntensity(float v) => _intensity = Mathf.Clamp(v, 0, 1);
    public void SetSwarm(float v) => _swarmLevel = Mathf.Clamp(v, 0, 1);
    public void FadeMusic(float target) => _musicFade = target;

    float _calmMix = 1, _battleMix;
    float _musicGain = 1;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _calmMix = Mathf.MoveToward(_calmMix, 1 - _intensity * 0.85f, dt * 0.25f);
        _battleMix = Mathf.MoveToward(_battleMix, _intensity, dt * (_intensity > _battleMix ? 0.6f : 0.12f));
        _musicGain = Mathf.MoveToward(_musicGain, _musicFade, dt * 0.5f);
        float mv = MusicVolume * _musicGain;
        _calm.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.0001f, _calmMix * mv * 0.7f));
        _battle.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.0001f, _battleMix * mv * 0.85f));
        _amb.VolumeDb = Mathf.LinearToDb(0.75f);
        _swarm.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.0001f, _swarmLevel * 0.8f));
    }
}
