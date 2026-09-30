# Run by the uninstaller, before the install folder is removed: undoes what
# this project put into the game itself. The game paths come from the
# launcher's settings.json beside this script.
#
#   always        the auto-load line in the Game Pass game's user.cfg -- it
#                 changes how the game starts, and after the uninstall there is
#                 no launcher left to switch it off
#   -RemoveMods   also the mod folders (Documents\kingdomcome_mods\kdcmp and
#                 <Modding Tools>\Mods\kdcmp)
#
# Left alone: the skip save (kcdmpskip.whs is a save like any other) and the
# firewall rule that blocks other PCs from port 4600 (removing it needs
# elevation, and it only blocks).
[CmdletBinding()]
param(
    [switch] $RemoveMods,
    [string] $SettingsPath = ''
)
$ErrorActionPreference = 'Continue'     # best effort: one failure must not stop the rest
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $here 'KcdmpCommon.ps1')

if (-not $SettingsPath) { $SettingsPath = Join-Path $here 'settings.json' }
$settings = $null
if (Test-Path -LiteralPath $SettingsPath) {
    try { $settings = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json } catch { }
}

$gamePass = if ($settings -and $settings.GamePassPath) { [string]$settings.GamePassPath } else { Find-GamePassExe }
if ($gamePass -and (Test-Path -LiteralPath $gamePass)) {
    try { Set-AutoLoadLastSave $gamePass $false } catch { Write-Warning "user.cfg beside the game was not changed: $($_.Exception.Message)" }
}

if ($RemoveMods -and -not (Test-GameRunning)) {
    $dirs = @(Get-GamePassModDir)
    $steam = if ($settings -and $settings.GamePath) { [string]$settings.GamePath } else { $null }
    if ($steam -and (Test-Path -LiteralPath $steam)) {
        try { $dirs += Get-SteamModDir $steam } catch { }
    }
    foreach ($dir in $dirs) {
        # Only a folder that is this mod (its manifest and its pak are there).
        # It is removed whole. A stock KCDMP installed beside this one uses the
        # same folder: its mod goes too, and its own installer puts it back.
        if ((Test-Path -LiteralPath (Join-Path $dir 'mod.manifest')) -and (Test-Path -LiteralPath (Join-Path $dir 'Data\kdcmp.pak'))) {
            Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
