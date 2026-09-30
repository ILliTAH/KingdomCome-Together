# Offline checks for the package scripts (package\*.ps1): they parse, and the
# helpers that do not need a game behave. Touches only a scratch folder in %TEMP%.
#   powershell -ExecutionPolicy Bypass -File tools\Test-PackageScripts.ps1
$ErrorActionPreference = 'Stop'
$pkg = Join-Path (Split-Path -Parent $PSScriptRoot) 'package'
$fail = 0
function Check($name, $cond, $detail = '') {
    if ($cond) { Write-Host "PASS $name" } else { Write-Host "FAIL $name  ($detail)"; $script:fail++ }
}

foreach ($f in Get-ChildItem $pkg -Filter *.ps1) {
    $errs = $null
    $null = [System.Management.Automation.Language.Parser]::ParseFile($f.FullName, [ref]$null, [ref]$errs)
    Check "$($f.Name) parses" ($errs.Count -eq 0) ($errs | ForEach-Object { $_.Message } | Select-Object -First 2)
}

# A copy of the package with the mod payload stubbed, so nothing real is touched.
$tmp = Join-Path $env:TEMP ("kcdmp-pstest-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
Copy-Item "$pkg\*" $tmp -Recurse
. (Join-Path $tmp 'KcdmpCommon.ps1')

# Every command a script calls exists: in the script, in KcdmpCommon.ps1, or in
# PowerShell. (A helper cut from the shared file once left Start-GamePass.ps1
# calling a function that was no longer there; nothing here ran far enough to
# notice.)
foreach ($f in Get-ChildItem $pkg -Filter *.ps1 | Where-Object { $_.Name -ne 'KcdmpCommon.ps1' }) {
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($f.FullName, [ref]$null, [ref]$null)
    $own = $ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true) | ForEach-Object { $_.Name }
    $missing = $ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.CommandAst] }, $true) |
        ForEach-Object { $_.GetCommandName() } |
        Where-Object { $_ -and $_ -match '^[A-Za-z]+-[A-Za-z]+$' -and ($own -notcontains $_) -and -not (Get-Command $_ -ErrorAction SilentlyContinue) } |
        Select-Object -Unique
    Check "$($f.Name) calls only commands that exist" (-not $missing) ($missing -join ', ')
}

# The mod: only the manifest and the pak, and "current" means the same pak.
New-Item -ItemType Directory -Force -Path (Join-Path $tmp 'mod\Data') | Out-Null
Set-Content (Join-Path $tmp 'mod\mod.manifest') '<manifest/>'
Set-Content (Join-Path $tmp 'mod\Data\kdcmp.pak') 'pak v2'
$modDir = Join-Path $tmp 'game\Mods\kdcmp'
Check 'mod: a game without it is not current' (-not (Test-ModCurrent $modDir))
Install-Mod $modDir
Check 'mod: installed is current' (Test-ModCurrent $modDir)
Check 'mod: the two files and nothing else' ((@(Get-ChildItem $modDir -Recurse -File | ForEach-Object { $_.Name } | Sort-Object) -join ',') -eq 'kdcmp.pak,mod.manifest')
Set-Content (Join-Path $modDir 'Data\kdcmp.pak') 'pak v1'
Check 'mod: an older pak is not current' (-not (Test-ModCurrent $modDir))
# Loose pak sources left in the mod folder stop the game from starting.
New-Item -ItemType Directory -Force -Path (Join-Path $modDir 'Data\Libs\Tables'), (Join-Path $tmp 'game\Mods\othermod') | Out-Null
Set-Content (Join-Path $modDir 'Data\Libs\Tables\loose.xml') ''
Set-Content (Join-Path $modDir 'notes.txt') ''
Set-Content (Join-Path $tmp 'game\Mods\othermod\other.pak') 'not ours'
Install-Mod $modDir
Check 'mod: leftovers in the mod folder are removed' ((@(Get-ChildItem $modDir -Recurse | ForEach-Object { $_.Name } | Sort-Object) -join ',') -eq 'Data,kdcmp.pak,mod.manifest')
Check 'mod: a neighbouring mod is not touched' ((Get-Content (Join-Path $tmp 'game\Mods\othermod\other.pak')) -eq 'not ours')
Check 'game running: answers without error' ((Test-GameRunning) -is [bool])

# The agent and the relay: beside the scripts (installed), or in their own folders.
$threw = $false; try { Find-PackageExe 'KcdMpClient.exe' } catch { $threw = $true }
Check 'package exe: a missing one is an error, not a wrong path' $threw
New-Item -ItemType Directory -Force -Path (Join-Path $tmp 'agent') | Out-Null
Set-Content (Join-Path $tmp 'agent\KcdMpClient.exe') ''
Check 'package exe: found in its own folder' ((Find-PackageExe 'KcdMpClient.exe') -like '*\agent\KcdMpClient.exe')
Set-Content (Join-Path $tmp 'KcdMpClient.exe') ''
Check 'package exe: beside the scripts wins' ((Find-PackageExe 'KcdMpClient.exe') -notlike '*\agent\*')

$root = Get-SteamSaveRoot
Check 'save root ends in kingdomcome2\saves' ($root -like '*\kingdomcome2\saves') $root

# Install-SkipSave against a scratch save root.
$saves = Join-Path $tmp 'saves'
function Get-SteamSaveRoot { return $saves }
Check 'skip save: first install copies' ((Install-SkipSave 0) -eq $true)
$dst = Join-Path $saves 'playline0\kcdmpskip.whs'
Check 'skip save: lands as kcdmpskip.whs in playline0' (Test-Path $dst)
Check 'skip save: same bytes' ((Get-FileHash $dst).Hash -eq (Get-FileHash "$pkg\save\autosave018.whs").Hash)
Check 'skip save: second install does nothing' ((Install-SkipSave 0) -eq $false)
Set-Content $dst 'an older copy'
Check 'skip save: a different file under our name is replaced' ((Install-SkipSave 0) -eq $true -and (Get-FileHash $dst).Hash -eq (Get-FileHash "$pkg\save\autosave018.whs").Hash)
Check 'skip save: nothing else is written to the playline' (@(Get-ChildItem (Join-Path $saves 'playline0')).Count -eq 1)
$threw = $false; try { Install-SkipSave 5 } catch { $threw = $true }
Check 'skip save: playline 5 is refused' $threw

# Working directory: the folder with steam_appid.txt, else the exe's folder.
$g = Join-Path $tmp 'game'
New-Item -ItemType Directory -Force -Path "$g\Bin\Win64MasterMasterSteamPGO", "$g\Data" | Out-Null
Set-Content "$g\Bin\Win64MasterMasterSteamPGO\KingdomCome.exe" ''
Check 'working dir: exe folder without steam_appid.txt' ((Get-SteamWorkingDir "$g\Bin\Win64MasterMasterSteamPGO\KingdomCome.exe") -eq "$g\Bin\Win64MasterMasterSteamPGO")
Set-Content "$g\steam_appid.txt" '1'
Check 'working dir: the folder with steam_appid.txt' ((Get-SteamWorkingDir "$g\Bin\Win64MasterMasterSteamPGO\KingdomCome.exe") -eq $g)
Check 'mod dir is <root>\Mods\kdcmp' ((Get-SteamModDir "$g\Bin\Win64MasterMasterSteamPGO\KingdomCome.exe") -eq "$g\Mods\kdcmp")

# Auto-load: one line in user.cfg next to the game, the player's own lines untouched.
$exe = "$g\Bin\Win64MasterMasterSteamPGO\KingdomCome.exe"
$cfg = "$g\Bin\Win64MasterMasterSteamPGO\user.cfg"
Set-AutoLoadLastSave $exe $true
Check 'auto-load: creates user.cfg with the cvar' ((Get-Content $cfg) -contains 'wh_sys_AutoLoadLastSave = 1')
$before = Get-Content $cfg -Raw
Set-AutoLoadLastSave $exe $true
Check 'auto-load: asking twice changes nothing' ((Get-Content $cfg -Raw) -eq $before)
Set-AutoLoadLastSave $exe $false
Check 'auto-load: off removes a user.cfg that held nothing else' (-not (Test-Path $cfg))
Set-Content $cfg @('r_Gamma = 1.1', 'wh_sys_AutoLoadLastSave=0', '-- mine')
Set-AutoLoadLastSave $exe $true
$lines = @(Get-Content $cfg)
Check 'auto-load: the player''s own lines stay' (($lines -contains 'r_Gamma = 1.1') -and ($lines -contains '-- mine')) ($lines -join ' | ')
Check 'auto-load: exactly one setting, and it is on' (@($lines | Where-Object { $_ -match 'wh_sys_AutoLoadLastSave' }).Count -eq 1 -and ($lines -contains 'wh_sys_AutoLoadLastSave = 1')) ($lines -join ' | ')
Set-AutoLoadLastSave $exe $false
$lines = @(Get-Content $cfg)
Check 'auto-load: off leaves only the player''s lines' (($lines -join '|') -eq 'r_Gamma = 1.1|-- mine') ($lines -join ' | ')

# The player's own bytes come back exactly, whatever the file is encoded in.
function Test-RoundTrip([string] $name, [byte[]] $original) {
    [IO.File]::WriteAllBytes($cfg, $original)
    Set-AutoLoadLastSave $exe $true
    $on = [IO.File]::ReadAllBytes($cfg)
    Set-AutoLoadLastSave $exe $false
    $off = [IO.File]::ReadAllBytes($cfg)
    Check "auto-load: $name - on adds to the file" ($on.Length -gt $original.Length)
    Check "auto-load: $name - on then off gives the same bytes back" ([Convert]::ToBase64String($off) -eq [Convert]::ToBase64String($original)) "$($off.Length) bytes, was $($original.Length)"
}
$crlf = [byte[]](13, 10)
Test-RoundTrip 'Thai in cp874'   ([byte[]](0x2D, 0x2D, 0x20, 0xA1, 0xD2, 0xC3) + $crlf + [Text.Encoding]::ASCII.GetBytes('r_Gamma = 1.1') + $crlf)
Test-RoundTrip 'UTF-8 with a BOM' ([byte[]](0xEF, 0xBB, 0xBF) + [Text.Encoding]::UTF8.GetBytes("r_Gamma = 1.1`r`n"))
Test-RoundTrip 'UTF-16'          ([byte[]](0xFF, 0xFE) + [Text.Encoding]::Unicode.GetBytes("r_Gamma = 1.1`r`n"))
Test-RoundTrip 'Unix line ends'  ([Text.Encoding]::ASCII.GetBytes("r_Gamma = 1.1`n-- mine`n"))
Test-RoundTrip 'no final line end' ([Text.Encoding]::ASCII.GetBytes('r_Gamma = 1.1'))
[IO.File]::WriteAllBytes($cfg, [Text.Encoding]::ASCII.GetBytes('r_Gamma = 1.1'))
Set-AutoLoadLastSave $exe $true
$lines = @(Get-Content $cfg)
Check 'auto-load: our lines are whole lines, the player''s last line is untouched' ($lines.Count -eq 3 -and $lines[1] -eq 'wh_sys_AutoLoadLastSave = 1' -and $lines[2] -eq 'r_Gamma = 1.1') ($lines -join ' | ')
[IO.File]::WriteAllBytes($cfg, [byte[]](0xFF, 0xFE) + [Text.Encoding]::Unicode.GetBytes("wh_sys_AutoLoadLastSave = 0`r`n"))
Set-AutoLoadLastSave $exe $true
$b = [IO.File]::ReadAllBytes($cfg)
Check 'auto-load: a UTF-16 file stays UTF-16' ($b[0] -eq 0xFF -and $b[1] -eq 0xFE -and ([Text.Encoding]::Unicode.GetString($b, 2, $b.Length - 2)).Contains('wh_sys_AutoLoadLastSave = 1'))

# A read-only user.cfg is the player's decision: refused, not overridden.
Remove-Item $cfg
Set-AutoLoadLastSave $exe $true
Set-ItemProperty $cfg -Name IsReadOnly -Value $true
$threw = $false; try { Set-AutoLoadLastSave $exe $false } catch { $threw = $true }
Check 'auto-load: a read-only user.cfg is not deleted' ($threw -and (Test-Path $cfg))
Set-Content (Join-Path (Split-Path $cfg) 'x') ''   # (keeps the folder non-empty either way)
Set-ItemProperty $cfg -Name IsReadOnly -Value $false

Remove-Item $tmp -Recurse -Force
if ($fail) { Write-Host "$fail FAILED"; exit 1 } else { Write-Host 'ALL PASS' }
