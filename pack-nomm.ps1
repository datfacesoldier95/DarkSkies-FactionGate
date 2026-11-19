# Pack DarkSkies-FactionGate-<version>.zip for NOMM (host + clients).
# Does NOT touch BepInEx\plugins — install only by importing this zip in NOMM.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

dotnet build (Join-Path $root 'FactionGate.csproj') -c Release
if ($LASTEXITCODE -ne 0) { Write-Error 'Build failed' }

$pluginCs = Get-Content (Join-Path $root 'src\FactionGatePlugin.cs') -Raw
if ($pluginCs -notmatch 'PluginVersion\s*=\s*"([^"]+)"') {
  Write-Error 'Could not find PluginVersion in FactionGatePlugin.cs'
}
$version = $Matches[1]
$modId = 'DarkSkies.FactionGate'
$zipName = "DarkSkies-FactionGate-$version.zip"
$dist = Join-Path $root 'dist'
$handout = Join-Path $root 'handout'
$archiveZip = Join-Path $dist $zipName
$handoutZip = Join-Path $handout $zipName
$staging = Join-Path $env:TEMP 'FactionGate-nomm-pack'
$tempZip = Join-Path $env:TEMP $zipName

if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
if (Test-Path $tempZip) { Remove-Item -Force $tempZip }
$modStaging = Join-Path $staging $modId
New-Item -ItemType Directory -Force -Path $dist, $handout, $modStaging | Out-Null

$dll = Join-Path $root 'bin\FactionGate.dll'
Copy-Item -Force $dll (Join-Path $modStaging 'FactionGate.dll')
Copy-Item -Force (Join-Path $root 'presets.json') (Join-Path $modStaging 'presets.json')

$flagsSrc = Join-Path $root 'Flags'
$flagsDst = Join-Path $modStaging 'Flags'
New-Item -ItemType Directory -Force -Path $flagsDst | Out-Null
Copy-Item -Force (Join-Path $flagsSrc '*') $flagsDst

# Fresh empty skin profile — hosts configure their own; don't ship Matthew's local rules.
@'
{
  "enabled": false,
  "factions": {}
}
'@ | Set-Content -Path (Join-Path $modStaging 'skin-profiles.json') -Encoding utf8

$metaPath = Join-Path $modStaging 'meta.json'
@"
{
  "id": "$modId",
  "artifact": {
    "fileName": "$zipName",
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

Copy-Item -Force (Join-Path $root 'README.md') (Join-Path $modStaging 'README.md')

# Compress with forward-slash entry names. Compress-Archive uses '\', which Linux
# treats as literal filename characters (flat folder + "prefixed" names).
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $tempZip) { Remove-Item -Force $tempZip }
$zip = [System.IO.Compression.ZipFile]::Open($tempZip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
  $stagingFull = (Resolve-Path $staging).Path.TrimEnd('\')
  Get-ChildItem $staging -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($stagingFull.Length).TrimStart('\').Replace('\', '/')
    [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
      $zip,
      $_.FullName,
      $rel,
      [System.IO.Compression.CompressionLevel]::Optimal)
  }
} finally {
  $zip.Dispose()
}
Remove-Item -Recurse -Force $staging

Copy-Item -Force $tempZip $archiveZip
Copy-Item -Force $tempZip $handoutZip
Remove-Item -Force $tempZip

# Remove older handout zips so only the current version sits there.
Get-ChildItem $handout -Filter 'DarkSkies-FactionGate-*.zip' -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -ne $zipName } |
  ForEach-Object {
    Remove-Item -Force $_.FullName
    Write-Host "Removed old handout zip: $($_.Name)"
  }

Write-Host ""
Write-Host "ONE file for host + players (NOMM Import this):"
Write-Host "  $handoutZip"
Write-Host ""
Write-Host "This script does not install into the game. After Import, enable DarkSkies.FactionGate in NOSMR/NOMM if it shows disabled."
Write-Host "Archive copy: $archiveZip"
