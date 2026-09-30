# Builds release\KCDMP-<VERSION>-HostWorld.zip: one package for every machine -
# Setup.bat (Game Pass player / Steam player / dedicated world host), the
# self-contained agent and relay, and the mod.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$version = (Get-Content (Join-Path $root 'VERSION') -Raw).Trim()
$out = Join-Path $root "release\KCDMP-$version-HostWorld"

# relay.txt is this machine's own relay address: kept on disk, never shipped.
$relay = $null
if (Test-Path (Join-Path $out 'relay.txt')) { $relay = (Get-Content (Join-Path $out 'relay.txt') -Raw).Trim() }
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $out | Out-Null

foreach ($p in @(@('dotnet\KcdMp.Client\KcdMp.Client.csproj', 'agent'), @('dotnet\KcdMp.Server\KcdMp.Server.csproj', 'relay'))) {
    dotnet publish (Join-Path $root $p[0]) -c Release -r win-x64 --self-contained true -o (Join-Path $out $p[1])
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $($p[0])" }
}
New-Item -ItemType Directory -Force -Path (Join-Path $out 'mod\Data') | Out-Null
Copy-Item (Join-Path $root 'kdcmp\mod.manifest') (Join-Path $out 'mod')
Copy-Item (Join-Path $root 'kdcmp\Data\kdcmp.pak') (Join-Path $out 'mod\Data')
Copy-Item (Join-Path $root 'package\*') $out
Copy-Item (Join-Path $root 'docs\HOST-WORLD-GUIDE.md') $out
Copy-Item (Join-Path $root 'LICENSE') $out

Compress-Archive -Path (Join-Path $out '*') -DestinationPath "$out.zip" -Force
if ($relay) { Set-Content -LiteralPath (Join-Path $out 'relay.txt') -Value $relay -Encoding ASCII }
Write-Host "built $out.zip"
