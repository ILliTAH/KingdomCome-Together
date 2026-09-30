# Kingdom Come: Together 0.18.2 - dedicated WORLD HOST (Steam, Modding Tools build).
#
# STATUS: UNTESTED. Written on a Game Pass machine, which cannot run the
# Modding Tools build. The Lua it drives is unit-tested (tools\Test-GamePassLua.py);
# this script itself has never been run. HOST-WORLD-GUIDE.md lists what to
# verify, in order.
#
# KCD2 has no dedicated-server program: the host IS the game, and the game
# needs a GPU (on a software renderer the world runs 15-55 times too slow).
# What this does instead is run the game with nobody at it: no click to load
# the save, the window minimised, no pause when it is not in front.
#
# What it does:
#   1. installs the mod into <ModdingTools>\Mods\kdcmp and the skip save into
#      Saved Games (the game must be closed: it reads both only at startup),
#   2. starts the relay on this machine (unless -NoRelay),
#   3. starts the Modding Tools game and minimises its window,
#   4. loads the save through the debug API (:1403) and waits for the world,
#   5. injects the native plugin when it is in this folder (an installed copy
#      has it), so that damage and deaths from the players reach this world,
#   6. runs the agent as "[HOST] ..." with --world-host. The name is how every
#      other player's game knows to become a guest and not show this player;
#      --world-host makes the agent keep this game the host, and its player
#      following the guests, across save loads and game restarts.
[CmdletBinding()]
param(
    [string] $GameExe    = '',          # Modding Tools KingdomCome.exe; found through Steam when empty
    [int]    $RelayPort  = 7778,
    [string] $HostName   = '[HOST] world',
    [string] $Save       = 'kcdmpskip', # save to load, name without .whs; the default is the skip save in this package
    [int]    $Playline   = 0,           # saves\playline<N> the save is in (0 to 4)
    [switch] $NoAutoLoad,               # load a save yourself in the game instead
    [switch] $ShowWindow,               # leave the game window as it is instead of minimising it
    [int]    $MaxFps     = 30,          # frame cap while hosting; 0 leaves the game's own setting
    [string] $Window     = '',          # e.g. 640x360: windowed at that size (less GPU work); empty leaves video settings alone
    [switch] $NoRelay,                  # a relay is already running elsewhere / as a service
    [switch] $NoFollow,                 # do not move the host's player after the guests
    [switch] $NoInject,                 # do not inject the native plugin
    [int]    $LoadTimeoutMinutes = 15,
    [switch] $PauseOnError              # the launcher passes this
)
$ErrorActionPreference = 'Stop'
# Started from the launcher, this window is the only place an error shows, and
# it would close with the error in it.
trap {
    Write-Host ''
    Write-Host "ERROR: $_" -ForegroundColor Red
    if ($PauseOnError) { Read-Host 'Press Enter to close' | Out-Null }
    exit 1
}
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $here 'KcdmpCommon.ps1')
$api  = 'http://localhost:1403'

if (-not $HostName.StartsWith('[HOST]')) { throw 'HostName must start with "[HOST]" - the guests look for that prefix.' }
if ($Save -and $Save -notmatch '^\w+$') { throw 'Save is a file name without .whs, letters, digits and _ only.' }
if ($Window -and $Window -notmatch '^(\d+)x(\d+)$') { throw 'Window is WIDTHxHEIGHT, for example 640x360.' }

# A console command, as typed in the game's console.
function Invoke-GameConsole([string] $command) {
    $cmd = [Uri]::EscapeDataString($command)
    $null = Invoke-WebRequest -Uri "$api/api/System/Console/ExecuteString?command=$cmd" -UseBasicParsing -TimeoutSec 5
}

# The port, not a request: what the API answers before a world is loaded is
# not known, and an error status would read as "not up".
function Test-ApiUp {
    $c = [Net.Sockets.TcpClient]::new()
    try { return $c.ConnectAsync('127.0.0.1', 1403).Wait(500) } catch { return $false } finally { $c.Dispose() }
}

function Test-WorldLoaded {
    try {
        $xml = (Invoke-WebRequest -Uri "$api/api/rpg/Calendar?depth=1" -UseBasicParsing -TimeoutSec 3).Content
        # Same read as the agent's IsGameReadyAsync: GameTime="<seconds>" above zero.
        return ($xml -match 'GameTime="([^"]+)"' -and [double]$Matches[1] -gt 0)
    } catch { return $false }
}

# Whether the game has taken the load command: it prints this line at once,
# long before the world is up. Sending the command again after that would load
# the save a second time, over a world that is already there or on its way.
function Test-LoadStarted {
    $log = Join-Path (Get-GameRoot $GameExe) 'kcd.log'
    if (-not (Test-Path -LiteralPath $log)) { return $true }      # cannot tell: do not risk a second load
    # A log that cannot be read right now counts as "started", for the same reason.
    $any = [datetime]::MinValue
    return (Test-LogContains $log 'Loading saved game' $any $true) -and (Test-LogContains $log "/$Save.whs" $any $true)
}

Add-Type -Namespace Kcdmp -Name Win32 -MemberDefinition '[DllImport("user32.dll")] public static extern bool ShowWindow(System.IntPtr hWnd, int nCmdShow);'
# Minimised is how the original project runs its own unattended hosts: the
# world keeps simulating at about 25 fps.
function Hide-GameWindow {
    if ($ShowWindow) { return }
    foreach ($p in Get-Process KingdomCome -ErrorAction SilentlyContinue) {
        if ($p.MainWindowHandle -ne [IntPtr]::Zero) { [void][Kcdmp.Win32]::ShowWindow($p.MainWindowHandle, 6) }   # SW_MINIMIZE
    }
}

if (-not $GameExe) { $GameExe = Find-ModdingToolsExe }
if (-not $GameExe -or -not (Test-Path -LiteralPath $GameExe)) {
    throw 'Could not find the KCD2 Modding Tools KingdomCome.exe. Pass it with -GameExe.'
}
Write-Host "Modding Tools game: $GameExe"

# 1. Mod and save (only while the game is closed - it reads both at startup).
$modDir = Get-SteamModDir $GameExe
$running = Test-GameRunning
$autoLoad = -not $NoAutoLoad -and $Save
if (-not $running) {
    Install-Mod $modDir
    Write-Host "Mod installed to $modDir"
    if ($autoLoad -and $Save -eq $script:SkipSaveName) {
        $null = Install-SkipSave $Playline
        Write-Host "Skip save: $(Join-Path (Get-SteamSaveRoot) "playline$Playline\$Save.whs")"
    }
} elseif (-not (Test-ModCurrent $modDir)) {
    throw 'The game is running with a different version of the mod. Quit it, then run this again.'
}
if ($autoLoad) {
    $savePath = Join-Path (Get-SteamSaveRoot) "playline$Playline\$Save.whs"
    if (-not (Test-Path -LiteralPath $savePath)) {
        Write-Warning "There is no save at $savePath, so nothing will be loaded automatically."
        $autoLoad = $false
    }
}

# 2. Relay.
if (-not $NoRelay) {
    if (Get-NetTCPConnection -LocalPort $RelayPort -State Listen -ErrorAction SilentlyContinue) {
        Write-Host "A relay is already listening on port $RelayPort."
    } else {
        Write-Host "Starting the relay on port $RelayPort..."
        $relayExe = Find-PackageExe 'KcdMpServer.exe'
        Start-Process -FilePath $relayExe -ArgumentList '--port', $RelayPort -WorkingDirectory (Split-Path -Parent $relayExe) -WindowStyle Minimized
    }
}

# 3. Steam, then the game.
#
# The game is started directly, not through Steam, and needs the Steam client
# up and logged in at that moment: without it the game logs "SteamApi_Init
# failed" and stops at a "License not verified / No SteamApps" box with the
# debug API down. (The original project hit the same; the first real host run
# of this script did too.) So Steam is started here when it is not running,
# and a game that could not reach it is closed and started again -- Steam
# takes a while to log in -- before this gives up and says what to do.
$steamFailed = 'The game could not reach Steam ("License not verified"). On this machine, in this Windows account: start Steam, log in ' +
    'with an account that owns Kingdom Come: Deliverance II and wait for the Library, then run this again. If Steam was already up and ' +
    'logged in, the usual cause is that the same Steam account is playing on another machine (not checked by this script).'

# $true when Steam is running in this session afterwards, or was started.
function Start-SteamClient {
    if (Get-SessionProcess 'steam') { return $true }
    $root = Get-SteamRoot
    $exe = if ($root) { Join-Path $root 'steam.exe' } else { $null }
    if (-not $exe -or -not (Test-Path -LiteralPath $exe)) {
        Write-Warning 'Steam is not running in this Windows account and steam.exe was not found. Start Steam and log in; the game needs it.'
        return $false
    }
    Write-Host 'Steam is not running in this Windows account: starting it and giving it 45 seconds to log in...'
    try { Start-Process -FilePath $exe -ArgumentList '-silent' }
    catch { Write-Warning "Steam could not be started: $($_.Exception.Message)"; return $false }
    Start-Sleep -Seconds 45
    return $true
}

# The game this script started, and nothing else of that name.
function Stop-HostGame {
    # Best effort throughout: a process that is already gone, or cannot be
    # asked when it started, must not stop the script.
    try { if ($game -and -not $game.HasExited) { Stop-Process -Id $game.Id -Force -ErrorAction Stop } } catch { }
    foreach ($p in Get-SessionProcess 'KingdomCome') {
        try { if ($p.StartTime -ge $startedAt.AddSeconds(-5)) { Stop-Process -Id $p.Id -Force -ErrorAction Stop } } catch { }
    }
}

function Start-HostGame {
    Write-Host 'Starting the Modding Tools game...'
    return Start-Process -FilePath $GameExe -WorkingDirectory (Get-SteamWorkingDir $GameExe) -PassThru
}

# What the game writes to its log when it cannot reach Steam, as the original
# project recorded it. Only that: a phrase guessed from the dialog could match
# a healthy log and have a working game closed. If this build words it
# differently, nothing is closed and the wait below ends with a message that
# names the dialog.
function Test-SteamRefused { return (Test-LogContains $gameLog 'SteamApi_Init failed' $startedAt) }

$gameLog = Join-Path (Get-GameRoot $GameExe) 'kcd.log'
$game = $null
$startedAt = Get-Date
if (-not $running) {
    if (-not (Test-Path -LiteralPath (Join-Path (Get-SteamWorkingDir $GameExe) 'steam_appid.txt'))) {
        Write-Warning 'steam_appid.txt was not found above the game: started outside Steam, the game may not know which Steam app it is.'
    }
    $steamUp = Start-SteamClient
    $startedAt = Get-Date
    $game = Start-HostGame
}
Write-Host 'Waiting for the game''s debug API on port 1403...'
$attempt = 1
$deadline = (Get-Date).AddMinutes(5)
while (-not (Test-ApiUp)) {
    if ((Get-Date) -gt $deadline) {
        # Left open, the game would make the next run skip everything above.
        if ($game) { Stop-HostGame }
        throw 'Port 1403 never answered. If the game showed "License not verified", Steam was not running or not logged in (in this Windows account) with an account that owns the game; otherwise check that this is the Modding Tools build. Close the game before running this again.'
    }
    if ($game -and (Test-SteamRefused)) {
        Stop-HostGame
        # No Steam here and none that could be started: another try changes nothing.
        if ($attempt -ge 3 -or -not $steamUp) { throw $steamFailed }
        $attempt++
        Write-Warning "The game could not reach Steam. Closing it and trying again in 30 seconds (attempt $attempt of 3)..."
        Start-Sleep -Seconds 30
        $steamUp = Start-SteamClient
        $startedAt = Get-Date
        $game = Start-HostGame
        $deadline = (Get-Date).AddMinutes(5)
    }
    Start-Sleep -Seconds 2
}
try { Invoke-GameConsole 'wh_ui_PauseGameOnFocusLoss 0' } catch { Write-Warning "Could not turn off pause-on-focus-loss: $($_.Exception.Message)" }
Hide-GameWindow

# 4. The save. wh_sys_LoadGame works from the main menu; sending it again
#    while a world is up would load it a second time, so it goes out once and
#    a second time only after four minutes of nothing (a cold load takes about
#    one).
if (Test-WorldLoaded) {
    Write-Host 'A world is already loaded.'
} else {
    if ($autoLoad) {
        Start-Sleep -Seconds 10          # the API answers a little before the main menu does
        Write-Host "Loading save '$Save' from playline $Playline..."
        try { Invoke-GameConsole "wh_sys_LoadGame $Playline $Save" }
        catch { Write-Warning "The load command was not accepted ($($_.Exception.Message)); it is sent once more if nothing loads." }
    } else {
        Write-Host 'Load a save in the game.'
    }
    $started  = Get-Date
    $retried  = $false
    $deadline = $started.AddMinutes($LoadTimeoutMinutes)
    while (-not (Test-WorldLoaded)) {
        if ((Get-Date) -gt $deadline) { throw 'No save was loaded in time (the debug API on :1403 never reported a world clock).' }
        if ($autoLoad -and -not $retried -and ((Get-Date) - $started).TotalMinutes -ge 4 -and -not (Test-LoadStarted)) {
            $retried = $true
            Write-Warning "The save has not loaded. Trying once more; if nothing happens, load one by hand in the game (the window is minimised)."
            try { Invoke-GameConsole "wh_sys_LoadGame $Playline $Save" } catch { }
        }
        Start-Sleep -Seconds 3
    }
}
Write-Host 'World loaded.'
Hide-GameWindow                          # a load can bring the window back to the front

# Less work for a machine nobody is looking at.
try {
    if ($MaxFps -gt 0) { Invoke-GameConsole "sys_MaxFPS $MaxFps" }
    if ($Window -match '^(\d+)x(\d+)$') {
        Invoke-GameConsole 'r_Fullscreen 0'
        Invoke-GameConsole "r_Width $($Matches[1])"
        Invoke-GameConsole "r_Height $($Matches[2])"
        Hide-GameWindow
    }
} catch { Write-Warning "Could not apply the low-load settings: $($_.Exception.Message)" }

# 5. The native plugin, where this package has it. Without it this game cannot
#    apply the damage and deaths the players report. It needs a world that is
#    ticking, which is why it goes in here and not earlier.
$injector = Join-Path $here 'KCDMP_LauncherInjector.exe'
$plugin   = Join-Path $here 'KCDMP.dll'
if (-not $NoInject -and (Test-Path -LiteralPath $injector) -and (Test-Path -LiteralPath $plugin)) {
    $game = Get-Process KingdomCome -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($game) {
        & $injector --pid $game.Id --dll $plugin
        if ($LASTEXITCODE -eq 0) { Write-Host 'Native plugin injected.' }
        else { Write-Warning "The native plugin was not injected (exit code $LASTEXITCODE): damage and deaths will not reach this world." }
    }
}

# 6. Agent. --hosting only changes the Discord text; voice and Discord are off
#    because nobody is sitting at this machine.
$agentArgs = @('--host', '127.0.0.1', '--port', $RelayPort, '--name', $HostName,
               '--world-host', '--hosting', '--no-voice', '--no-discord')
if ($NoFollow) { $agentArgs += '--no-world-follow' }
Write-Host "This game is the world host. Connecting to the relay as '$HostName'."
& (Find-PackageExe 'KcdMpClient.exe') @agentArgs
# The agent's own errors (a relay on another protocol version, say) are an exit
# code, not a PowerShell error: without this the window closed on them.
if ($LASTEXITCODE) { throw "The agent stopped with exit code $LASTEXITCODE. Its last lines above say why." }
