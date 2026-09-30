# Shared by Start-GamePass.ps1 and Start-WorldHost.ps1: finding the
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
# Where Steam is installed: the per-user value first, then the machine-wide
# ones, which are all there is when Windows is logged in as a different
# account from the one that set Steam up -- the usual state of a server.
# A folder as Steam writes it (forward slashes, sometimes a trailing one). A
# drive root keeps its backslash: "D:" alone is "the current folder on D".
function ConvertTo-WindowsDir([string] $path) {
    $dir = $path.Replace('/', '\').TrimEnd('\')
    if ($dir -match '^[A-Za-z]:$') { $dir += '\' }
    return $dir
}

function Get-SteamRoot {
    $candidates = @(
        (Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath,
        (Get-ItemProperty -Path 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam' -ErrorAction SilentlyContinue).InstallPath,
        (Get-ItemProperty -Path 'HKLM:\SOFTWARE\Valve\Steam' -ErrorAction SilentlyContinue).InstallPath
    )
    foreach ($c in $candidates) {
        if (-not $c) { continue }
        $dir = ConvertTo-WindowsDir ([string]$c)
        if (Test-Path -LiteralPath $dir) { return $dir }
    }
    return $null
}

function Get-SteamLibraries {
    $steam = Get-SteamRoot
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
        # <game>\Bin\<config>\KingdomCome.exe is three folders down; going
        # no deeper keeps this to seconds on a full Steam library.
        foreach ($exe in Get-ChildItem -LiteralPath $common -Filter 'KingdomCome.exe' -Recurse -Depth 4 -ErrorAction SilentlyContinue) {
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

# The folder to start the Modding Tools game from: the one holding
# steam_appid.txt. Started outside Steam, the game reads its app id from the
# working directory; from the exe's own folder it never finds it, and saves
# that need DLC are greyed out (the stock launcher's GameRootOf, WO-31).
function Get-SteamWorkingDir([string] $exe) {
    $dir = Split-Path -Parent $exe
    $fallback = $dir
    for ($i = 0; $i -lt 4 -and $dir; $i++) {
        if (Test-Path -LiteralPath (Join-Path $dir 'steam_appid.txt')) { return $dir }
        $dir = Split-Path -Parent $dir
    }
    return $fallback
}

# --- the skip save -----------------------------------------------------------
# save\autosave018.whs is the save the original project publishes with 0.18.2:
# past the prologue, where co-op works. It is installed under its own name so
# that it never replaces a save of the same number.
$script:SkipSaveName = 'kcdmpskip'

function Get-SteamSaveRoot {
    $shell = Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders' -ErrorAction SilentlyContinue
    $saved = if ($shell) { $shell.'{4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4}' } else { $null }
    if (-not $saved) { $saved = Join-Path $env:USERPROFILE 'Saved Games' }
    return (Join-Path ([Environment]::ExpandEnvironmentVariables($saved)) 'kingdomcome2\saves')
}

# Puts the skip save into saves\playline<N>. Returns $true when it copied the
# file, $false when it was already there. The game lists its saves once, at
# startup, so a copy made while it runs is not loadable until it restarts.
function Install-SkipSave([int] $playline = 0) {
    if ($playline -lt 0 -or $playline -gt 4) { throw 'Playline must be 0 to 4: the game has five.' }
    $src = Join-Path $script:KcdmpRoot 'save\autosave018.whs'
    if (-not (Test-Path -LiteralPath $src)) { throw "The skip save is missing from this package: $src" }
    $dir = Join-Path (Get-SteamSaveRoot) "playline$playline"
    $dst = Join-Path $dir "$($script:SkipSaveName).whs"
    if ((Test-Path -LiteralPath $dst) -and
        ((Get-FileHash -LiteralPath $dst).Hash -eq (Get-FileHash -LiteralPath $src).Hash)) { return $false }
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Copy-Item -LiteralPath $src -Destination $dst -Force
    return $true
}

# --- loading the last save without a click -----------------------------------
# The game reads user.cfg next to KingdomCome.exe at startup ("Loading config
# file 'user.cfg'"), and wh_sys_AutoLoadLastSave = 1 in it makes the game load
# the newest save instead of waiting at the main menu. Observed on the Game
# Pass build, 2026-09-30. The same cvar on the command line (+wh_sys_...) is
# applied after the menu exists and does nothing.
#
# It stays in effect for every start of the game until it is switched off
# again, including starts that do not go through these scripts.
$script:AutoLoadNote = '-- Kingdom Come: Together: load the newest save at startup (Start-GamePass.bat -NoAutoLoad removes this)'
$script:AutoLoadLine = 'wh_sys_AutoLoadLastSave = 1'

# Only our two lines are added or removed (and any other line that sets this
# same cvar: there is one switch, and it is this one). Every other byte of the
# file is written back as it was read: a user.cfg in a legacy code page is read and
# written as Latin-1 (which maps each byte to itself), a UTF-16 one as UTF-16,
# and a byte-order mark stays where it is. A read-only file is left alone and
# the call throws.
function Set-AutoLoadLastSave([string] $exe, [bool] $enabled) {
    $cfg = Join-Path (Split-Path -Parent $exe) 'user.cfg'
    $bytes = [byte[]]@()
    if (Test-Path -LiteralPath $cfg) { $bytes = [IO.File]::ReadAllBytes($cfg) }

    $enc = [Text.Encoding]::GetEncoding(28591)   # Latin-1
    $bom = 0
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) { $enc = [Text.Encoding]::Unicode; $bom = 2 }
    elseif ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) { $enc = [Text.Encoding]::BigEndianUnicode; $bom = 2 }
    elseif ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { $bom = 3 }
    $old = $enc.GetString($bytes, $bom, $bytes.Length - $bom)

    $ours = '(?m)^(?:' + [regex]::Escape($script:AutoLoadNote) + '|[ \t]*wh_sys_AutoLoadLastSave[ \t]*=[^\r\n]*)(?:\r?\n|\r|$)'
    $new = [regex]::Replace($old, $ours, '')
    # At the top, not the end: a file whose last line has no line end would
    # need one added before ours, and taking ours out again would leave it.
    if ($enabled) { $new = $script:AutoLoadNote + "`r`n" + $script:AutoLoadLine + "`r`n" + $new }
    if ($new -ceq $old) { return }

    if ($new.Length -eq 0) { Remove-Item -LiteralPath $cfg; return }
    $out = New-Object IO.MemoryStream
    $out.Write($bytes, 0, $bom)
    $body = $enc.GetBytes($new)
    $out.Write($body, 0, $body.Length)
    [IO.File]::WriteAllBytes($cfg, $out.ToArray())
}

# --- this package -----------------------------------------------------------
# The agent and the relay sit beside these scripts in an installed copy
# (KingdomCome-Coop-Setup), and in their own folders in a hand-made one.
function Find-PackageExe([string] $name) {
    foreach ($dir in @($script:KcdmpRoot, (Join-Path $script:KcdmpRoot 'agent'), (Join-Path $script:KcdmpRoot 'relay'))) {
        $p = Join-Path $dir $name
        if (Test-Path -LiteralPath $p) { return $p }
    }
    throw "$name is missing from $($script:KcdmpRoot)."
}

# --- the game's log -----------------------------------------------------------
# True when the log was written since $since and holds $text (any case). The
# game keeps the file open, so it is read with full sharing. A log that is
# there but cannot be read answers $onError: which answer is the safe one is
# the caller's to say.
function Test-LogContains([string] $path, [string] $text, [datetime] $since, [bool] $onError = $false) {
    if (-not (Test-Path -LiteralPath $path)) { return $false }
    try {
        if ((Get-Item -LiteralPath $path).LastWriteTime -lt $since) { return $false }
        $fs = [IO.File]::Open($path, 'Open', 'Read', 'ReadWrite')
        try { $content = (New-Object IO.StreamReader($fs)).ReadToEnd() } finally { $fs.Dispose() }
        return $content.IndexOf($text, [StringComparison]::OrdinalIgnoreCase) -ge 0
    } catch { return $onError }
}

# A process of this name in this Windows session. Another account's Steam, or
# another account's game, is not ours to count on or to close.
function Get-SessionProcess([string] $name) {
    $session = (Get-Process -Id $PID).SessionId
    return @(Get-Process $name -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $session })
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
    # The mod's own folder holds the manifest and the pak and nothing else.
    # Anything more is a leftover -- an old developer deploy with the pak's
    # loose sources, which stops the game from starting ("114 tables are not
    # loaded") -- and the stock installer emptied the folder for that reason.
    if (Test-Path -LiteralPath $modDir) {
        Get-ChildItem -LiteralPath $modDir -Force | Where-Object { $_.Name -ne 'mod.manifest' -and $_.Name -ne 'Data' } |
            Remove-Item -Recurse -Force
        $data = Join-Path $modDir 'Data'
        if (Test-Path -LiteralPath $data) {
            Get-ChildItem -LiteralPath $data -Force | Where-Object { $_.Name -ne 'kdcmp.pak' } | Remove-Item -Recurse -Force
        }
    }
    New-Item -ItemType Directory -Force -Path (Join-Path $modDir 'Data') | Out-Null
    Copy-Item -LiteralPath (Join-Path $script:KcdmpRoot 'mod\mod.manifest') -Destination $modDir -Force
    Copy-Item -LiteralPath (Join-Path $script:KcdmpRoot 'mod\Data\kdcmp.pak') -Destination (Join-Path $modDir 'Data\kdcmp.pak') -Force
}
