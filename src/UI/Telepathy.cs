using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The Glass Queen speaks in the player's head: her moving face in a framed panel, her voice, the words typing out in
/// step with it, and a faint amber shimmer at the edges of the screen while she speaks. The script is
/// assets/voice/lines.tsv (persona and tone: docs/queen-persona.md); one voice file per line in assets/voice/<speaker>/.
/// A line without a voice file still shows its text, so subtitles are always on and nothing depends on audio.
///
/// What she says is driven by the campaign: Level 1's founding stages, every other level's opening and its win, and
/// short barks for the moments that matter (the nest attacked, the queen hurt, an enemy queen killed, a general born).
/// Volume follows the master volume (the Voice bus sends to Master). Skippable: click the panel or press Space.
public partial class Telepathy : Control
{
    public GameSession Game;

    sealed class Line { public string Id, Speaker, Text; }
    static Dictionary<string, Line> _script;

    readonly Queue<string> _queue = new();
    readonly HashSet<string> _said = new();
    readonly Dictionary<string, float> _cooldown = new();
    readonly Dictionary<string, float> _expires = new(); // barks are about now: one that waits too long is dropped
    Line _cur;
    float _curT, _curLen, _typeLen, _holdLeft;

    PanelContainer _panel;
    VideoStreamPlayer _face;
    TextureRect _still;
    Label _name, _text, _skip;
    ColorRect _edge;
    ShaderMaterial _edgeMat;
    AudioStreamPlayer _voice;
    VideoStream _idle, _speak;
    bool _speakingClip;
    float _edgeA, _panelA;

    // what has been said already about the mission
    bool _started;
    int _stageSeen = -1;
    float _remainsWait;
    bool _won;
    float _foodCheck;

    public const string VoiceBus = "Voice";

    public static string Name(string speaker) => speaker switch
    {
        "queen" => "THE GLASS QUEEN",
        "warlord" => "THE WARLORD · GENERAL",
        "matron" => "THE ACID MATRON · GENERAL",
        "marshal" => "THE SKY MARSHAL · GENERAL",
        _ => speaker.ToUpperInvariant(),
    };

    /// The script, read once: id -> speaker and text.
    public static IReadOnlyDictionary<string, string> Script()
    {
        Load();
        var d = new Dictionary<string, string>();
        foreach (var kv in _script) d[kv.Key] = kv.Value.Text;
        return d;
    }

    static void Load()
    {
        if (_script != null) return;
        _script = new Dictionary<string, Line>();
        using var f = FileAccess.Open("res://assets/voice/lines.tsv", FileAccess.ModeFlags.Read);
        if (f == null) { GD.PrintErr("[telepathy] lines.tsv missing"); return; }
        bool header = true;
        while (!f.EofReached())
        {
            var row = f.GetLine();
            if (header) { header = false; continue; }
            var p = row.Split('\t');
            if (p.Length < 4) continue;
            _script[p[0]] = new Line { Id = p[0], Speaker = p[1], Text = p[3] };
        }
    }

    public override void _Ready()
    {
        Load();
        ProcessMode = ProcessModeEnum.Always; // she finishes her sentence over the pause and end screens
        MouseFilter = MouseFilterEnum.Ignore;
        UiTheme.Place(this, 0, 0, 1, 1, 0, 0, 0, 0);
        EnsureBus();

        // the psychic shimmer: a warm glow that breathes at the screen edges while she speaks
        _edgeMat = new ShaderMaterial { Shader = new Shader { Code = EdgeShader } };
        _edge = new ColorRect { MouseFilter = MouseFilterEnum.Ignore, Material = _edgeMat, Color = Colors.White };
        AddChild(_edge);
        UiTheme.Place(_edge, 0, 0, 1, 1, 0, 0, 0, 0);

        bool mobile = Settings.Mobile;
        int face = mobile ? 112 : 136;
        _panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        var box = UiTheme.Box(new Color(0.05f, 0.035f, 0.025f, 0.9f), new Color(0.92f, 0.62f, 0.25f, 0.85f), 2, 10, 10);
        _panel.AddThemeStyleboxOverride("panel", box);
        AddChild(_panel);
        UiTheme.Place(_panel, 0, 0, 0, 0, 16, mobile ? 92 : 112, mobile ? 470 : 596, (mobile ? 92 : 112) + face + 22);
        _panel.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true } || e is InputEventScreenTouch { Pressed: true }) Skip();
        };

        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 14);
        _panel.AddChild(row);
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(face, face), MouseFilter = MouseFilterEnum.Ignore };
        frame.AddThemeStyleboxOverride("panel", UiTheme.Box(Colors.Black, new Color(0.75f, 0.5f, 0.2f), 1, 6, 0));
        row.AddChild(frame);
        _still = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/voice/queen/face.jpg"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore,
        };
        frame.AddChild(_still);
        _idle = LoadVideo("res://assets/voice/queen/face-idle.ogv");
        _speak = LoadVideo("res://assets/voice/queen/face-speak.ogv");
        if (_idle != null)
        {
            _face = new VideoStreamPlayer { Stream = _idle, Expand = true, Loop = true, Autoplay = false, MouseFilter = MouseFilterEnum.Ignore, Bus = "Ui", VolumeDb = -80 };
            frame.AddChild(_face);
        }

        var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        row.AddChild(col);
        var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; col.AddChild(top);
        _name = UiTheme.Text("THE GLASS QUEEN", 13, UiTheme.Title, new Color(1f, 0.72f, 0.36f));
        _name.SizeFlagsHorizontal = SizeFlags.ExpandFill; top.AddChild(_name);
        _skip = UiTheme.Text(mobile ? "tap to skip" : "click or Space to skip", 11, null, UiTheme.Dim); top.AddChild(_skip);
        _text = UiTheme.Text("", mobile ? 15 : 17, UiTheme.Body, new Color(1f, 0.93f, 0.8f));
        _text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _text.SizeFlagsVertical = SizeFlags.ExpandFill;
        _text.AddThemeColorOverride("font_shadow_color", new Color(0.9f, 0.45f, 0.1f, 0.35f));
        _text.AddThemeConstantOverride("shadow_offset_x", 0); _text.AddThemeConstantOverride("shadow_offset_y", 0);
        _text.AddThemeConstantOverride("shadow_outline_size", 6);
        col.AddChild(_text);

        _voice = new AudioStreamPlayer { Bus = VoiceBus };
        AddChild(_voice);
        _panel.Modulate = new Color(1, 1, 1, 0);
        _panel.Visible = false;
    }

    static VideoStream LoadVideo(string path)
    {
        if (!ResourceLoader.Exists(path) && !FileAccess.FileExists(path)) return null;
        return new VideoStreamTheora { File = path };
    }

    static void EnsureBus()
    {
        if (AudioServer.GetBusIndex(VoiceBus) >= 0) return;
        AudioServer.AddBus();
        int i = AudioServer.BusCount - 1;
        AudioServer.SetBusName(i, VoiceBus);
        AudioServer.SetBusSend(i, "Master");
        AudioServer.SetBusVolumeDb(i, 1.5f);
    }

    /// Queues a line by id; `once` lines are never repeated in a level.
    public void Say(string id, bool once = true, bool urgent = false)
    {
        if (_script == null || !_script.ContainsKey(id)) return;
        if (once && !_said.Add(id)) return;
        if (urgent) { var rest = _queue.ToArray(); _queue.Clear(); _queue.Enqueue(id); foreach (var r in rest) _queue.Enqueue(r); }
        else _queue.Enqueue(id);
    }

    /// A bark: said at most once per `gap` seconds, dropped if she is already busy with two lines.
    public void Bark(string id, float gap)
    {
        float now = Time.GetTicksMsec() / 1000f;
        if (_cooldown.TryGetValue(id, out var t) && now < t) return;
        if (_queue.Count >= 2) return;
        _cooldown[id] = now + gap;
        _expires[id] = now + 8f;
        Say(id, once: false);
    }

    public bool Speaking => _cur != null;
    public string Current => _cur?.Id;

    public void Skip()
    {
        if (_cur == null) return;
        _voice.Stop();
        Finish();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_cur != null && e is InputEventKey { Pressed: true, Keycode: Key.Space })
        {
            Skip();
            GetViewport().SetInputAsHandled();
        }
    }

    void Start(string id)
    {
        _cur = _script[id];
        _curT = 0;
        _name.Text = Name(_cur.Speaker);
        _text.Text = _cur.Text;
        _text.VisibleRatio = 0;
        var path = $"res://assets/voice/{_cur.Speaker}/{id}.wav";
        AudioStream s = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
        // reading pace when there is no voice: a calm 26 characters a second
        _curLen = s != null ? (float)s.GetLength() : 0.6f + _cur.Text.Length / 26f;
        _typeLen = Mathf.Max(0.5f, _curLen - (s != null ? 0.9f : 0.6f)); // the voice's tail rings on after the last word
        _holdLeft = 1.6f + _cur.Text.Length / 60f;   // the text stays a moment to be read
        if (s != null) { _voice.Stream = s; _voice.Play(); }
        bool queen = _cur.Speaker == "queen";
        _still.Modulate = queen ? Colors.White : new Color(0.35f, 0.3f, 0.26f);
        SetClip(queen);
        Game.Audio?.FadeMusic(0.45f);
        _panel.Visible = true;
        GD.Print($"[telepathy] {id}{(s == null ? " (text only)" : $" {_curLen:0.0}s")}: {_cur.Text}");
    }

    void Finish()
    {
        _text.VisibleRatio = 1;
        _cur = null;
        SetClip(false);
        if (_queue.Count == 0) Game.Audio?.FadeMusic(1f);
    }

    void SetClip(bool speaking)
    {
        if (_face == null || _speakingClip == speaking && _face.IsPlaying()) return;
        _speakingClip = speaking;
        _face.Stream = speaking && _speak != null ? _speak : _idle;
        _face.Play();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Watch(dt);
        bool blocked = Game.Hud != null && Game.Hud.Blocking && Game.Sim.Winner < 0;
        if (_cur == null && _holdLeft <= 0 && _queue.Count > 0 && !blocked)
        {
            var next = _queue.Dequeue();
            if (_expires.TryGetValue(next, out var until) && Time.GetTicksMsec() / 1000f > until)
            {
                GD.Print($"[telepathy] {next} dropped: the moment passed");
                if (_queue.Count == 0) Game.Audio?.FadeMusic(1f);
            }
            else Start(next);
        }
        if (_cur != null)
        {
            _curT += dt;
            _text.VisibleRatio = Mathf.Clamp(_curT / _typeLen, 0, 1);
            if (_curT >= _curLen && !_voice.Playing) Finish();
        }
        else if (_holdLeft > 0)
        {
            _holdLeft -= dt;
            if (_queue.Count > 0 && !blocked) _holdLeft = Mathf.Min(_holdLeft, 0.35f); // the next line follows quickly
        }
        bool show = _cur != null || _holdLeft > 0;
        _panelA = Mathf.MoveToward(_panelA, show ? 1 : 0, dt / (show ? 0.25f : 0.6f));
        _panel.Modulate = new Color(1, 1, 1, _panelA);
        _panel.Visible = _panelA > 0.01f;
        if (_face != null) _face.Paused = !_panel.Visible;
        // the shimmer swells with her voice and settles while the words stay
        float target = _cur != null ? (_cur.Speaker == "queen" ? 0.6f : 0.25f) : 0f;
        _edgeA = Mathf.MoveToward(_edgeA, target, dt / (target > _edgeA ? 0.25f : 0.8f));
        _edgeMat.SetShaderParameter("strength", _edgeA);
        _edge.Visible = _edgeA > 0.005f;
    }

    // ---------------------------------------------------------------- what the campaign makes her say
    void Watch(float dt)
    {
        var m = Game.Sim.Mission;
        if (m == null || Game.Restoring) return;
        if (!_started)
        {
            if (Game.Hud.Blocking) return; // after the story and the briefing
            _started = true;
            bool fresh = Game.Sim.Time < 3f;
            if (m.Episode == 1) { _stageSeen = fresh ? -1 : (int)m.Stage; if (fresh) Say("l1_wake"); }
            else if (fresh) Say($"open_{m.Episode}");
        }
        if (m.Episode == 1)
        {
            int st = (int)m.Stage;
            if (st != _stageSeen)
            {
                _stageSeen = st;
                // a stage the player finished before she got to it is not worth saying any more: only the newest stage's line waits
                var keep = new List<string>(_queue);
                _queue.Clear();
                foreach (var q in keep) if (q == "l1_wake" || !q.StartsWith("l1_")) _queue.Enqueue(q);
                Say(m.Stage switch
                {
                    ExpeditionStage.ClearTrail => "l1_beetle",
                    ExpeditionStage.EscortQueen => "l1_beetle_dead",
                    ExpeditionStage.Provision => "l1_clearing",
                    ExpeditionStage.DigEntrance => "l1_stocked",
                    ExpeditionStage.RoyalChamber => "l1_digging",
                    ExpeditionStage.Incubator => "l1_royal",
                    ExpeditionStage.Store => "l1_incubator",
                    ExpeditionStage.RaiseWorkers => "l1_store",
                    ExpeditionStage.RaiseSoldiers => "l1_workers",
                    ExpeditionStage.Bridge => "l1_bridge",
                    ExpeditionStage.Fragment => "l1_island",
                    ExpeditionStage.Complete => "l1_taste",
                    _ => "l1_done",
                });
            }
            // the remains are the one step players miss: remind them once if they lie untouched
            if (m.Stage == ExpeditionStage.Provision && m.RemainsLeft && (_remainsWait += dt) > 30f) Say("l1_remains");
        }
        if (!_won && Game.Sim.Winner == Local.Me)
        {
            _won = true;
            if (m.Episode != 1) Say($"close_{m.Episode}", urgent: true); else Say("l1_done"); // after her taste of the gland
        }
        if ((_foodCheck -= dt) <= 0)
        {
            _foodCheck = 5f;
            var c = Game.Sim.Col[Local.Me];
            if (c.Founded && c.Food < 3 && Game.Sim.Time > 120) Bark("bark_low_food", 150);
        }
    }

    /// Called by GameSession for every simulation event.
    public void OnEvent(in SimEvent e)
    {
        var s = Game.Sim;
        if (s.Mission == null || Game.Restoring) return;
        switch (e.Kind)
        {
            case EvKind.UnderAttack when e.Team == Local.Me:
            {
                var c = s.Col[Local.Me];
                bool home = c.QueenId >= 0 && s.Ants[c.QueenId].Pos.DistanceTo(e.Pos) < 30;
                Bark(home ? "bark_attack" : "bark_attack_far", 50);
                break;
            }
            case EvKind.QueenHit when e.Team == Local.Me:
            {
                ref var q = ref s.Ants[s.Col[Local.Me].QueenId];
                float f = q.MaxHp > 0 ? q.Hp / q.MaxHp : 1;
                if (f < 0.3f) { if (_said.Add("bark_queen_low")) Say("bark_queen_low", once: false, urgent: true); }
                else if (f < 0.7f) Bark("bark_queen_hurt", 60);
                break;
            }
            case EvKind.QueenDead when e.Team != Local.Me && e.Team >= 0 && e.Team < 2:
                Say("bark_queen_kill");
                break;
            case EvKind.Spawn when e.Team == Local.Me && Config.IsHero(e.Type):
                Say("bark_general", once: false);
                Say(e.Type switch { AntType.Warlord => "gen_warlord_born", AntType.Matron => "gen_matron_born", _ => "gen_marshal_born" });
                break;
        }
    }

    const string EdgeShader = @"
shader_type canvas_item;
uniform float strength = 0.0;
void fragment() {
    vec2 d = min(UV, 1.0 - UV);
    float e = min(d.x * 0.62, d.y);
    float edge = 1.0 - smoothstep(0.0, 0.17, e);
    float wave = 0.5 + 0.5 * sin(TIME * 2.1 + (UV.x * 1.3 + UV.y) * 13.0) * sin(TIME * 0.9 - UV.y * 8.0 + UV.x * 3.0);
    float a = pow(edge, 1.6) * strength * (0.55 + 0.45 * wave);
    COLOR = vec4(mix(vec3(1.0, 0.55, 0.16), vec3(1.0, 0.8, 0.45), wave), a);
}";
}
