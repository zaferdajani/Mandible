using System;
using Godot;

namespace Mandible;

/// The cinematic slots: an opening video before the first chapter and a reward video after every won chapter. Each
/// plays an Ogg Theora file if one is in the build (res://assets/video/) and is skipped cleanly when it is not.
/// Any key, click or tap skips a playing video. Files: `opening.ogv` and `reward-<chapter slug>.ogv`, for example
/// `reward-the-silk-trap.ogv` (Levels.Slug); the slug follows the chapter, not its place on the map, so a video
/// survives a change in the chapter order.
public partial class StoryVideo : Control
{
    public const string Folder = "res://assets/video/";
    public static string Opening => Folder + "opening.ogv";
    public static string Reward(int levelId) => Folder + $"reward-{Levels.Slug(levelId)}.ogv";

    public static bool Has(string path) => ResourceLoader.Exists(path) || FileAccess.FileExists(path);

    Action _done;
    VideoStreamPlayer _player;
    bool _closed;

    /// Plays the video over everything and calls `done` when it ends or is skipped; with no file (or in a headless
    /// test run) `done` runs at once.
    public static void Play(Node parent, string path, Action done)
    {
        VideoStream stream = null;
        if (Has(path) && DisplayServer.GetName() != "headless")
        {
            try { stream = GD.Load<VideoStream>(path); } catch (Exception e) { GD.PushWarning($"[video] {path}: {e.Message}"); }
        }
        if (stream == null) { done?.Invoke(); return; }
        var v = new StoryVideo { _done = done, MouseFilter = MouseFilterEnum.Stop, ProcessMode = ProcessModeEnum.Always };
        UiTheme.Place(v, 0, 0, 1, 1, 0, 0, 0, 0);
        v.AddChild(UiTheme.Place(new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore }, 0, 0, 1, 1, 0, 0, 0, 0));
        v._player = new VideoStreamPlayer { Stream = stream, Expand = true, Autoplay = true, MouseFilter = MouseFilterEnum.Ignore };
        UiTheme.Place(v._player, 0, 0, 1, 1, 0, 0, 0, 0);
        v._player.Finished += v.Close;
        v.AddChild(v._player);
        var hint = UiTheme.Text(Settings.Mobile ? "Tap to skip" : "Press any key to skip", 14, null, new Color(1, 1, 1, 0.45f));
        UiTheme.Place(hint, 1, 1, 1, 1, -260, -40, -20, -14);
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        v.AddChild(hint);
        parent.AddChild(v);
    }

    public override void _GuiInput(InputEvent e) { if (e is InputEventMouseButton { Pressed: true } or InputEventScreenTouch { Pressed: true }) Close(); }
    public override void _UnhandledInput(InputEvent e) { if (e is InputEventKey { Pressed: true }) { GetViewport().SetInputAsHandled(); Close(); } }

    void Close()
    {
        if (_closed) return;
        _closed = true;
        _player?.Stop();
        QueueFree();
        _done?.Invoke();
    }
}
