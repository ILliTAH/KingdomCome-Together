# Shared by Setup.ps1, Start-GamePass.ps1 and Start-WorldHost.ps1: finding the
# game on this machine and putting the mod where that build loads mods from.

$script:KcdmpRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

# --- Xbox Game Pass ---------------------------------------------------------
# Each drive the Xbox app installs to has a .GamingRoot file naming its games
# folder: "RGBX", a version, then the folder name in UTF-16.
function Find-GamePassExe {
    foreach ($drive in [IO.DriveInfo]::GetDrives() | Where-Object { $_.DriveType -eq 'Fixed' -and $_.IsReady }) {
        $marker = Join-Path $drive.RootDirectory.FullName '.GamingRoot'
        if (-not (Test-Path -LiteralPath $marker)) { continue }
        $bytes = [IO.File]::ReadAllBytes($marker)
        if ($bytes.Length -le 8) { continue }
        $folder = [Text.Encoding]::Unicode.GetString($bytes, 8, $bytes.Length - 8).Trim([char]0)
        $exe = Join-Path $drive.RootDirectory.FullName (Join-Path $folder 'Kingdom Come- Deliverance II\Content\KingdomCome.exe')
        if (Test-Path -LiteralPath $exe) { return $exe }
    }
    $pkg = Get-AppxPackage -Name 'DeepSilver.77536C3FE941' -ErrorAction SilentlyContinue
    if ($pkg) { return (Join-Path $pkg.InstallLocation 'KingdomCome.exe') }
    return $null
}

function Get-GamePassModDir {
    return (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'kingdomcome_mods\kdcmp')
}

# --- Steam, Modding Tools build ---------------------------------------------
function Get-SteamLibraries {
    $steam = (Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    if (-not $steam) { return @() }
    $libs = @($steam)
    $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
    if (Test-Path -LiteralPath $vdf) {
        foreach ($m in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s+"([^"]+)"')) {
            $libs += $m.Groups[1].Value.Replace('\\', '\')
        }
    }
    return $libs | Select-Object -Unique
}

# The Modding Tools build is the one with Framework.dll and CrySystem.dll next
# to KingdomCome.exe; the retail game has neither (docs\LAUNCHING.md).
function Find-ModdingToolsExe {
    foreach ($lib in Get-SteamLibraries) {
        $common = Join-Path $lib 'steamapps\common'
        if (-not (Test-Path -LiteralPath $common)) { continue }
        foreach ($exe in Get-ChildItem -LiteralPath $common -Filter 'KingdomCome.exe' -Recurse -ErrorAction SilentlyContinue) {
            if ((Test-Path -LiteralPath (Join-Path $exe.DirectoryName 'Framework.dll')) -and
                (Test-Path -LiteralPath (Join-Path $exe.DirectoryName 'CrySystem.dll'))) {
                return $exe.FullName
            }
        }
    }
    return $null
}

# The game root is the folder that holds Data\ (and Mods\): walk up from the exe.
function Get-GameRoot([string] $exe) {
    $dir = Split-Path -Parent $exe
    while ($dir -and -not (Test-Path -LiteralPath (Join-Path $dir 'Data'))) { $dir = Split-Path -Parent $dir }
    if (-not $dir) { throw "Could not find the game root above $exe (no Data folder)." }
    return $dir
}

function Get-SteamModDir([string] $moddingToolsExe) {
    return (Join-Path (Get-GameRoot $moddingToolsExe) 'Mods\kdcmp')
}

# --- the mod ----------------------------------------------------------------
function Test-GameRunning { return [bool](Get-Process KingdomCome -ErrorAction SilentlyContinue) }

# True when $modDir already holds this package's pak.
function Test-ModCurrent([string] $modDir) {
    $src = Join-Path $script:KcdmpRoot 'mod\Data\kdcmp.pak'
    $dst = Join-Path $modDir 'Data\kdcmp.pak'
    return (Test-Path -LiteralPath $dst) -and
           ((Get-FileHash -LiteralPath $dst).Hash -eq (Get-FileHash -LiteralPath $src).Hash)
}

# The game keeps the pak open and only reads it at startup, so this is for a
# closed game; callers check Test-GameRunning first.
function Install-Mod([string] $modDir) {
    New-Item -ItemType Directory -Force -Path (Join-Path $modDir 'Data') | Out-Null
    Copy-Item -LiteralPath (Join-Path $script:KcdmpRoot 'mod\mod.manifest') -Destination $modDir -Force
    Copy-Item -LiteralPath (Join-Path $script:KcdmpRoot 'mod\Data\kdcmp.pak') -Destination (Join-Path $modDir 'Data\kdcmp.pak') -Force
}
