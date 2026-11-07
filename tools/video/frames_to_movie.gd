## Plays a folder of numbered PNG frames full-window, one per rendered frame, so Godot's movie writer can encode
## them (Theora .ogv for the game's VideoStreamPlayer; no ffmpeg needed).
##   godot --path . --script res://tools/video/frames_to_movie.gd --write-movie out.ogv --fixed-fps 24 \
##         --resolution 320x320 -- <frames_dir>
extends SceneTree

var _files: PackedStringArray
var _dir := ""
var _i := 0
var _rect: TextureRect

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	_dir = args[0]
	var d := DirAccess.open(_dir)
	for f in d.get_files():
		if f.ends_with(".png"):
			_files.append(f)
	_files.sort()
	_rect = TextureRect.new()
	_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_rect.stretch_mode = TextureRect.STRETCH_SCALE
	_rect.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.add_child(_rect)
	print("frames: ", _files.size())

func _process(_delta: float) -> bool:
	if _i >= _files.size():
		return true
	var img := Image.load_from_file(_dir.path_join(_files[_i]))
	_rect.texture = ImageTexture.create_from_image(img)
	_i += 1
	return false
