# End-to-end test of the in-game updater, silently, on a throwaway copy of the Windows build:
# the copy pretends to be an old version, finds the latest GitHub release, downloads it, quits,
# the installer unpacks it over the copy and restarts it. Usage: powershell -File tools/updatetest.ps1
$build = "C:\Users\Drzaf\Documents\MandibleBuilds\Windows"
$t = Join-Path $env:TEMP "mandible-updatetest"
$log = "$env:APPDATA\Godot\app_userdata\Mandible\update\install.log"
Get-Process | Where-Object { $_.Path -like "$t*" } | Stop-Process -Force
Remove-Item $t -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $t | Out-Null
Copy-Item "$build\*" $t -Recurse
Remove-Item $log -ErrorAction SilentlyContinue
$before = Get-Item "$t\Mandible.exe"

$env:MANDIBLE_FAKE_VERSION = "0.3.0"
$env:MANDIBLE_AUTO_INSTALL = "1"
$env:MANDIBLE_RELAUNCH_ARGS = "--audio-driver Dummy --resolution 800x450"
$p = Start-Process "$t\Mandible.exe" -ArgumentList "--audio-driver", "Dummy", "--resolution", "800x450", "--", "--updatetest" `
    -RedirectStandardOutput "$t-run.log" -PassThru
$p | Wait-Process -Timeout 300 -ErrorAction SilentlyContinue
"game quit to install: $($p.HasExited)"
Remove-Item Env:MANDIBLE_FAKE_VERSION, Env:MANDIBLE_AUTO_INSTALL, Env:MANDIBLE_RELAUNCH_ARGS -ErrorAction SilentlyContinue

# wait for the installer to finish and the game to come back
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep 3
    $restarted = Get-Process Mandible -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$t*" }
    if ($restarted) { break }
}
Select-String -Path "$t-run.log" -Pattern "\[updater\]" | ForEach-Object { $_.Line }
"install.log: " + $(if (Test-Path $log) { Get-Content $log -Raw } else { "(none)" })
$after = Get-Item "$t\Mandible.exe"
"exe: $($before.Length) bytes, $($before.LastWriteTime)  ->  $($after.Length) bytes, $($after.LastWriteTime)"
"game restarted: $([bool]$restarted)"
$restarted | Stop-Process -Force
