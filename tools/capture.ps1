# Builds the game and runs the scripted screenshot tour.
# Usage: powershell -File tools/capture.ps1 <output-dir>
param([string]$Out = "$PSScriptRoot/../captures")
$root = Resolve-Path "$PSScriptRoot/.."
$godot = "D:\Tools\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
dotnet build "$root" -nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) { dotnet build "$root" -nologo -v q; exit 1 }
New-Item -ItemType Directory -Force $Out | Out-Null
& $godot --path "$root" --audio-driver Dummy -- "--capture=$Out" *> "$Out/run.log"  # silent: tests never make noise
Select-String -Path "$Out/run.log" -Pattern "selftest\]|bugshoot\]|ERROR|Exception" | ForEach-Object { $_.Line }
