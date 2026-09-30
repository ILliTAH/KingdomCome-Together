# Kingdom Come: Together 0.18.2 (Host World fork) - one installer for every kind of machine.
#
#   Setup.bat                 shows what was found on this machine and asks
#   Setup.bat -Mode gamepass  Xbox Game Pass player: install the mod and start playing
#   Setup.bat -Mode steam     Steam player: install the mod into the Modding Tools build
#   Setup.bat -Mode host      dedicated world host: relay + game + agent (Steam machine)
param(
    [ValidateSet('', 'gamepass', 'steam', 'host')] [string] $Mode = '',
    [Parameter(ValueFromRemainingArguments = $true)] $Rest
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $here 'KcdmpCommon.ps1')

$gamePassExe = Find-GamePassExe
$steamExe    = Find-ModdingToolsExe

function Install-SteamMod {
    if (-not $steamExe) {
        throw 'KCD2 Modding Tools was not found in any Steam library. Install it from your Steam library (free with the game) and start it once so Workspace Setup can finish.'
    }
    if (Test-GameRunning) { throw 'The game is running. Quit it first - it keeps the mod file open.' }
    $modDir = Get-SteamModDir $steamExe
    Install-Mod $modDir
    Write-Host "Mod installed to $modDir"
    if (-not (Test-Path -LiteralPath (Join-Path $env:LOCALAPPDATA 'KCDMP'))) {
        Write-Warning 'KCDMP 0.18.2 itself is not installed on this machine. Install KCDMP-Setup-0.18.2.exe from the original project first, then run this again.'
    } else {
        Write-Host 'Done. Play through the KCDMP Launcher as usual.'
    }
}

if (-not $Mode) {
    Write-Host ''
    Write-Host 'Kingdom Come: Together 0.18.2 - Host World fork'
    Write-Host '-----------------------------------------------'
    Write-Host ("  Xbox Game Pass game : " + $(if ($gamePassExe) { $gamePassExe } else { 'not found' }))
    Write-Host ("  Steam Modding Tools : " + $(if ($steamExe) { $steamExe } else { 'not found' }))
    Write-Host ''
    Write-Host '  1  Play on Xbox Game Pass          (installs the mod, starts the game and connects)'
    Write-Host '  2  Install the mod for Steam       (then play through the KCDMP Launcher as usual)'
    Write-Host '  3  Run this machine as WORLD HOST  (Steam machine: relay + game + agent)'
    Write-Host ''
    switch ((Read-Host 'Choose 1, 2 or 3').Trim()) {
        '1' { $Mode = 'gamepass' }
        '2' { $Mode = 'steam' }
        '3' { $Mode = 'host' }
        default { throw 'Nothing chosen.' }
    }
}

switch ($Mode) {
    'gamepass' { & (Join-Path $here 'Start-GamePass.ps1') @Rest }
    'steam'    { Install-SteamMod }
    'host'     { & (Join-Path $here 'Start-WorldHost.ps1') @Rest }
}
