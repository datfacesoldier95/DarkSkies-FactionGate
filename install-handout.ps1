# Reliable install for host + players when NOMM leaves only a .zip in plugins.
# Extracts handout\DarkSkies-FactionGate-*.zip -> BepInEx\plugins\DarkSkies.FactionGate
# and removes any loose FactionGate zips from plugins/disabledPlugins.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$handout = Join-Path $root 'handout'
$plugins = 'C:\Steam\steamapps\common\Nuclear Option\BepInEx\plugins'
$disabled = 'C:\Steam\steamapps\common\Nuclear Option\BepInEx\disabledPlugins'
$modId = 'DarkSkies.FactionGate'

$zip = Get-ChildItem $handout -Filter 'DarkSkies-FactionGate-*.zip' -ErrorAction Stop |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
if (-not $zip) { throw "No zip in $handout - run .\pack-nomm.ps1 first" }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$tmp = Join-Path $env:TEMP ('fg-install-' + [guid]::NewGuid().ToString('n'))
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
try {
  [IO.Compression.ZipFile]::ExtractToDirectory($zip.FullName, $tmp)
  $inner = Join-Path $tmp $modId
  if (-not (Test-Path $inner)) { throw "Zip root missing folder $modId" }

  $dest = Join-Path $plugins $modId
  $disabledDest = Join-Path $disabled $modId
  # Keep host skin rules across reinstalls.
  $preserveSkin = $null
  foreach ($existing in @($dest, $disabledDest)) {
    $skinFile = Join-Path $existing 'skin-profiles.json'
    if (Test-Path $skinFile) {
      $preserveSkin = Get-Content $skinFile -Raw
      break
    }
  }
  if (Test-Path $disabledDest) { Remove-Item -Recurse -Force $disabledDest }
  if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
  New-Item -ItemType Directory -Force -Path $plugins | Out-Null
  Move-Item -Force $inner $dest
  if ($null -ne $preserveSkin -and $preserveSkin.Trim().Length -gt 0) {
    Set-Content -Path (Join-Path $dest 'skin-profiles.json') -Value $preserveSkin -Encoding utf8
    Write-Host "Preserved existing skin-profiles.json"
  }
} finally {
  if (Test-Path $tmp) { Remove-Item -Recurse -Force $tmp }
}

Get-ChildItem $plugins, $disabled -File -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -match 'FactionGate.*\.zip$' } |
  ForEach-Object {
    Remove-Item -Force $_.FullName
    Write-Host "Removed loose zip: $($_.Name)"
  }

Write-Host "Installed: $dest"
Write-Host "From: $($zip.FullName)"
Write-Host "Restart the game - F1 should list DarkSkies FactionGate."
