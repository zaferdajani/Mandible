@echo off
rem Builds the game code (quick if nothing changed) and launches Mandible.
cd /d "%~dp0"
dotnet build -nologo -v q
if errorlevel 1 (
  echo Build failed.
  pause
  exit /b 1
)
start "" "D:\Tools\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path "%~dp0"
