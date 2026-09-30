# Kingdom Come: Together 0.18.2 - Xbox Game Pass player (guest).
#
#   Start-GamePass.bat                          uses relay.txt next to this script
#   Start-GamePass.bat -RelayHost 1.2.3.4       or say where the relay is
#
# relay.txt holds one line, "host" or "host:port". It is created the first time
# you answer the prompt below, and is never part of the download.
param(
    [string] $RelayHost  = '',
    [int]    $RelayPort  = 0,
    [string] $PlayerName = $env:USERNAME,
    [string] $GameExe    = ''
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $here 'KcdmpCommon.ps1')

function Test-RemoteConsole {
    $c = [Net.Sockets.TcpClient]::new()
    try { return $c.ConnectAsync('127.0.0.1', 4600).Wait(300) } catch { return $false } finally { $c.Dispose() }
}

# 0. Which relay.
$relayFile = Join-Path $here 'relay.txt'
if (-not $RelayHost -and (Test-Path -LiteralPath $relayFile)) {
    $RelayHost = (Get-Content -LiteralPath $relayFile -TotalCount 1).Trim()
}
if (-not $RelayHost) {
    $RelayHost = (Read-Host 'Relay address (host or host:port)').Trim()
    if (-not $RelayHost) { throw 'No relay address given.' }
    Set-Content -LiteralPath $relayFile -Value $RelayHost -Encoding ASCII
}
if ($RelayHost -match '^(.+):(\d+)$') {
    $RelayHost = $Matches[1]
    if ($RelayPort -eq 0) { $RelayPort = [int]$Matches[2] }
}
if ($RelayPort -eq 0) { $RelayPort = 7778 }

# 1. One time: block other PCs from the game's RemoteConsole (port 4600 has no password).
$rule = 'KCDMP GamePass - block RemoteConsole 4600'
if (-not (Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue)) {
    Write-Host 'Adding a firewall rule that blocks other PCs from port 4600 (one time, needs admin)...'
    $cmd = "New-NetFirewallRule -DisplayName '$rule' -Direction Inbound -Protocol TCP -LocalPort 4600 -Action Block | Out-Null"
    try { Start-Process powershell -Verb RunAs -Wait -ArgumentList '-NoProfile', '-Command', $cmd } catch { }
    if (-not (Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue)) {
        Write-Warning 'Firewall rule not added: port 4600 stays reachable from your LAN while the game runs.'
    }
}

# 2. Mod, then the game with -devmode - unless it is already running with it.
$modDir = Get-GamePassModDir
if (-not (Test-GameRunning)) {
    Install-Mod $modDir

    $exe = $GameExe
    if (-not $exe) { $exe = Find-GamePassExe }
    if (-not $exe -or -not (Test-Path -LiteralPath $exe)) {
        throw 'Could not find the Game Pass KingdomCome.exe. Pass it with -GameExe "<...>\Content\KingdomCome.exe".'
    }
    Write-Host "Starting the game with -devmode: $exe"
    Start-Process -FilePath $exe -ArgumentList '-devmode' -WorkingDirectory (Split-Path $exe)
} elseif (-not (Test-RemoteConsole)) {
    throw 'The game is already running without -devmode. Quit it, then run this again.'
} elseif (-not (Test-ModCurrent $modDir)) {
    throw 'The game is running with a different version of the mod. Quit the game, then run this again so the mod can be updated.'
}

Write-Host 'Waiting for the game console on port 4600...'
$deadline = (Get-Date).AddMinutes(3)
while (-not (Test-RemoteConsole)) {
    if ((Get-Date) -gt $deadline) { throw 'Port 4600 never opened. Start the game through this script.' }
    Start-Sleep -Seconds 1
}

# 3. Run the agent. It connects to the relay once a save is loaded.
Write-Host "Load your save in the game. Connecting to ${RelayHost}:$RelayPort as $PlayerName."
& (Join-Path $here 'agent\KcdMpClient.exe') --host $RelayHost --port $RelayPort --name $PlayerName --transport remoteconsole
