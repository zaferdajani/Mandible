# Runs a batch of headless AI-vs-AI matches (black garden ants vs red fire ants) on different maps
# and prints who won each. Usage: powershell -File tools/balance.ps1 [-Diff Normal] [-Games 6] [-Seconds 1500]
param([string]$Diff = "Normal", [int]$Games = 6, [int]$Seconds = 1500)
$root = Resolve-Path "$PSScriptRoot/.."
$godot = "D:\Tools\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
$tmp = Join-Path $env:TEMP "mandible-balance"
New-Item -ItemType Directory -Force $tmp | Out-Null
$procs = @()
for ($i = 1; $i -le $Games; $i++) {
    $seed = 1000 + $i * 7919
    $procs += Start-Process $godot -ArgumentList "--headless", "--path", "$root", "--", "--simtest=$Seconds,$Diff,$Diff,$seed" `
        -RedirectStandardOutput "$tmp/g$i.log" -RedirectStandardError "$tmp/g$i.err" -PassThru -WindowStyle Hidden
}
$procs | Wait-Process
$wins = @{ black = 0; red = 0; none = 0 }
for ($i = 1; $i -le $Games; $i++) {
    $end = Select-String -Path "$tmp/g$i.log" -Pattern "finished" | Select-Object -Last 1
    $last = Select-String -Path "$tmp/g$i.log" -Pattern "ants=" | Select-Object -Last 1
    $w = if ($end -and $end.Line -match "winner=(\w+)") { $Matches[1] } else { "crash" }
    if ($wins.ContainsKey($w)) { $wins[$w]++ }
    $t = if ($end -and $end.Line -match "t=([\d.]+)m") { $Matches[1] } else { "?" }
    "game $i  winner $w at ${t}m  |  " + ($last.Line -replace '^.*?\| ', '' -replace '\| tick.*', '' -replace ' pop \S+', '')
}
"black {0}  red {1}  draw {2}" -f $wins.black, $wins.red, $wins.none
