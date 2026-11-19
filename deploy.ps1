# Dev-only: copy into BepInEx\plugins. Do NOT use for normal play — it fights NOMM.
# Prefer: .\pack-nomm.ps1 then NOMM Import of handout\DarkSkies-FactionGate-*.zip
param(
  [switch]$ForceDev
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not $ForceDev) {
  Write-Host @"
deploy.ps1 is disabled by default so it does not fight NOMM.

For host + players:
  1. .\pack-nomm.ps1
  2. NOMM Import: $root\handout\DarkSkies-FactionGate-*.zip
  3. Enable DarkSkies.FactionGate in NOSMR if it lands in disabledPlugins

Dev hot-copy only (bypasses NOMM):
  .\deploy.ps1 -ForceDev
"@
  exit 0
}

$dll = Join-Path $root 'bin\FactionGate.dll'
$pluginsRoot = 'C:\Steam\steamapps\common\Nuclear Option\BepInEx\plugins'
$disabledRoot = 'C:\Steam\steamapps\common\Nuclear Option\BepInEx\disabledPlugins'

$pluginCs = Get-Content (Join-Path $root 'src\FactionGatePlugin.cs') -Raw
if ($pluginCs -notmatch 'PluginVersion\s*=\s*"([^"]+)"') {
  Write-Error 'Could not find PluginVersion in FactionGatePlugin.cs'
}
$version = $Matches[1]
$modId = 'DarkSkies.FactionGate'
$destDir = Join-Path $pluginsRoot $modId

if (-not (Test-Path $dll)) {
  Write-Error "Build first: dotnet build `"$root\FactionGate.csproj`" -c Release"
}

# Never leave loose zips in plugins/disabled — they show up as a second "mod" in NOMM.
Get-ChildItem $pluginsRoot, $disabledRoot -File -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -match 'FactionGate.*\.zip$' } |
  ForEach-Object {
    Remove-Item -Force $_.FullName
    Write-Host "Removed loose zip: $($_.FullName)"
  }

# If NOSMR disabled the folder, move it back before copying.
$disabledDir = Join-Path $disabledRoot $modId
if ((Test-Path $disabledDir) -and -not (Test-Path $destDir)) {
  Move-Item -Force $disabledDir $destDir
  Write-Host "Re-enabled $modId from disabledPlugins"
}

New-Item -ItemType Directory -Force -Path $destDir | Out-Null
$destDll = Join-Path $destDir 'FactionGate.dll'
try {
  Copy-Item -Force $dll $destDll
} catch {
  $staged = Join-Path $destDir 'FactionGate.dll.new'
  Copy-Item -Force $dll $staged
  Write-Warning "FactionGate.dll is locked (game running). Staged: $staged"
  Write-Warning "Close the game, then: Move-Item -Force `"$staged`" `"$destDll`""
}

$presets = Join-Path $root 'presets.json'
if (Test-Path $presets) {
  Copy-Item -Force $presets (Join-Path $destDir 'presets.json')
}

$skinSrc = Join-Path $root 'skin-profiles.json'
$skinDst = Join-Path $destDir 'skin-profiles.json'
if ((Test-Path $skinSrc) -and -not (Test-Path $skinDst)) {
  Copy-Item -Force $skinSrc $skinDst
}

$flagsSrc = Join-Path $root 'Flags'
$flagsDst = Join-Path $destDir 'Flags'
if (Test-Path $flagsSrc) {
  New-Item -ItemType Directory -Force -Path $flagsDst | Out-Null
  Copy-Item -Force (Join-Path $flagsSrc '*') $flagsDst
}

$metaPath = Join-Path $destDir 'meta.json'
@"
{
  "id": "$modId",
  "artifact": {
    "fileName": "DarkSkies-FactionGate-$version.zip",
    "version": "$version",
    "category": "release",
    "type": "plugin",
    "gameVersion": "0.34.2",
    "downloadUrl": "",
    "downloadURL": null,
    "hash": "",
    "extends": null,
    "dependencies": [],
    "incompatibilities": []
  }
}
"@ | Set-Content -Path $metaPath -Encoding utf8

Write-Host "ForceDev deployed to $destDir\FactionGate.dll"
Write-Host "NOMM/NOSMR mod id: $modId (version $version)"
