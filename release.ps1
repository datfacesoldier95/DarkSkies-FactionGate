# Build -> pack Linux-safe NOMM zip -> push GitHub -> create/upload release.
# Does NOT deploy into BepInEx (NOMM owns the live install).
# Usage: .\release.ps1 [-Notes "changelog"]
param(
  [string]$Notes = ""
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
  [System.Environment]::GetEnvironmentVariable('Path', 'User')

$git = 'C:\Program Files\Git\cmd\git.exe'
if (-not (Test-Path $git)) { $git = 'git' }

$pluginCs = Get-Content (Join-Path $root 'src\FactionGatePlugin.cs') -Raw
if ($pluginCs -notmatch 'PluginVersion\s*=\s*"([^"]+)"') {
  throw 'Could not find PluginVersion in FactionGatePlugin.cs'
}
$version = $Matches[1]
$tag = "v$version"
$zipName = "DarkSkies-FactionGate-$version.zip"
$displayName = "DarkSkies.FactionGate $version"

Write-Host "=== $displayName release ==="

& (Join-Path $root 'pack-nomm.ps1')

$zipPath = Join-Path $root "handout\$zipName"
if (-not (Test-Path $zipPath)) {
  $zipPath = Join-Path $root "dist\$zipName"
}
if (-not (Test-Path $zipPath)) { throw "Missing $zipName after pack-nomm.ps1" }

& $git add -A
$user = gh api user --jq .login
$emailId = gh api user --jq .id
$env:GIT_AUTHOR_NAME = $user
$env:GIT_AUTHOR_EMAIL = "$emailId+$user@users.noreply.github.com"
$env:GIT_COMMITTER_NAME = $env:GIT_AUTHOR_NAME
$env:GIT_COMMITTER_EMAIL = $env:GIT_AUTHOR_EMAIL

$status = & $git status --porcelain
if ($status) {
  if (-not $Notes) { $Notes = $displayName }
  & $git commit --trailer "Co-authored-by: Cursor <cursoragent@cursor.com>" -m "Release $displayName"
  & $git push
} else {
  & $git push
}

if (-not $Notes) {
  $Notes = @"
$displayName — host and joiners need the same version.

NOMM: install DarkSkies.FactionGate. Host sets factions + skin profiles in F1; joiners sync automatically.
"@
}

$releaseExists = $false
cmd /c "gh release view $tag >NUL 2>&1"
if ($LASTEXITCODE -eq 0) { $releaseExists = $true }

if ($releaseExists) {
  Write-Host "Release $tag already exists - uploading asset..."
  gh release upload $tag $zipPath --clobber
} else {
  gh release create $tag $zipPath --title $displayName --notes $Notes
}

Write-Host ""
Write-Host "GitHub: https://github.com/datfacesoldier95/DarkSkies-FactionGate/releases/tag/$tag"
Write-Host "Zip: $zipPath"
