# Plays every Lane War hero matchup on both sides of the map (6 games at once) and prints the winners.
# Usage: powershell -File tools/lanebalance.ps1 [-Diff Normal] [-Seconds 1800]
param([string]$Diff = "Normal", [int]$Seconds = 1800, [int]$Seed = 20261031)
$root = Resolve-Path "$PSScriptRoot/.."
$godot = "D:\Tools\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
$tmp = Join-Path $env:TEMP "mandible-lanes"
New-Item -ItemType Directory -Force $tmp | Out-Null
$pairs = @(("Warlord", "Matron"), ("Matron", "Warlord"), ("Warlord", "Marshal"), ("Marshal", "Warlord"), ("Matron", "Marshal"), ("Marshal", "Matron"))
$procs = @()
for ($i = 0; $i -lt $pairs.Count; $i++) {
    $a, $b = $pairs[$i]
    $procs += Start-Process $godot -ArgumentList "--headless", "--path", "$root", "--", "--lanetest=$Seconds,$Diff,$Diff,$Seed,$a,$b" `
        -RedirectStandardOutput "$tmp/l$i.log" -RedirectStandardError "$tmp/l$i.err" -PassThru -WindowStyle Hidden
}
$procs | Wait-Process
$wins = @{}
for ($i = 0; $i -lt $pairs.Count; $i++) {
    $a, $b = $pairs[$i]
    $end = Select-String -Path "$tmp/l$i.log" -Pattern "finished" | Select-Object -Last 1
    $w = if ($end -and $end.Line -match "winner=(\w+) ") { $Matches[1] } else { "crash" }
    $t = if ($end -and $end.Line -match "t=([\d.]+)m") { $Matches[1] } else { "?" }
    $hero = if ($w -eq "black") { $a } elseif ($w -eq "red") { $b } else { "none" }
    $wins[$hero] = 1 + [int]$wins[$hero]
    "black $a vs red $b  ->  $w ($hero) at ${t}m"
}
($wins.GetEnumerator() | ForEach-Object { "$($_.Key) $($_.Value)" }) -join "   "
