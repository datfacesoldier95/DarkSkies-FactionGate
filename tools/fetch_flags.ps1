# Download national flags (flagcdn) and generate simple unofficial placeholders for fiction.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$flags = Join-Path $root 'Flags'
New-Item -ItemType Directory -Force -Path $flags | Out-Null

Add-Type -AssemblyName System.Drawing

function Save-Placeholder([string]$path, [int]$r, [int]$g, [int]$b, [string]$label) {
  $bmp = New-Object System.Drawing.Bitmap 512, 512
  $gfx = [System.Drawing.Graphics]::FromImage($bmp)
  $gfx.Clear([System.Drawing.Color]::FromArgb(255, $r, $g, $b))
  $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(230, 255, 255, 255))
  $font = New-Object System.Drawing.Font 'Segoe UI', 28, ([System.Drawing.FontStyle]::Bold)
  $sf = New-Object System.Drawing.StringFormat
  $sf.Alignment = [System.Drawing.StringAlignment]::Center
  $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
  $gfx.DrawString($label, $font, $brush, (New-Object System.Drawing.RectangleF 16, 16, 480, 480), $sf)
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $gfx.Dispose(); $bmp.Dispose(); $font.Dispose(); $brush.Dispose()
}

function Save-Stripes([string]$path, [int[][]]$colors) {
  $bmp = New-Object System.Drawing.Bitmap 512, 341
  $gfx = [System.Drawing.Graphics]::FromImage($bmp)
  $h = [Math]::Floor(341 / $colors.Count)
  for ($i = 0; $i -lt $colors.Count; $i++) {
    $c = $colors[$i]
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, $c[0], $c[1], $c[2]))
    $y = $i * $h
    $hh = if ($i -eq $colors.Count - 1) { 341 - $y } else { $h }
    $gfx.FillRectangle($brush, 0, $y, 512, $hh)
    $brush.Dispose()
  }
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $gfx.Dispose(); $bmp.Dispose()
}

$nationals = @{
  'uk.png' = 'gb'
  'usa.png' = 'us'
  'russia.png' = 'ru'
  'china.png' = 'cn'
  'north_korea.png' = 'kp'
  'south_korea.png' = 'kr'
  'japan.png' = 'jp'
  'germany.png' = 'de'
  'france.png' = 'fr'
  'italy.png' = 'it'
  'spain.png' = 'es'
  'poland.png' = 'pl'
  'ukraine.png' = 'ua'
  'switzerland.png' = 'ch'
  'canada.png' = 'ca'
  'mexico.png' = 'mx'
  'brazil.png' = 'br'
  'argentina.png' = 'ar'
  'iraq.png' = 'iq'
  'iran.png' = 'ir'
  'denmark.png' = 'dk'
  'norway.png' = 'no'
  'sweden.png' = 'se'
  'finland.png' = 'fi'
  'greece.png' = 'gr'
  'australia.png' = 'au'
  'india.png' = 'in'
  'pakistan.png' = 'pk'
  'indonesia.png' = 'id'
  'malaysia.png' = 'my'
  'saudi_arabia.png' = 'sa'
}

foreach ($kv in $nationals.GetEnumerator()) {
  $out = Join-Path $flags $kv.Key
  $url = "https://flagcdn.com/w640/$($kv.Value).png"
  try {
    Invoke-WebRequest -Uri $url -OutFile $out -UseBasicParsing
    Write-Host "OK $($kv.Key)"
  } catch {
    Write-Warning "FAIL $($kv.Key): $($_.Exception.Message)"
    Save-Placeholder $out 80 80 80 $kv.Key.Replace('.png','')
  }
}

# NATO / USSR — simple unofficial geometric stand-ins (not official emblems)
Save-Placeholder (Join-Path $flags 'nato.png') 0 61 165 'NATO'
Save-Stripes (Join-Path $flags 'ussr.png') @(
  @(205, 0, 0),
  @(255, 216, 0),
  @(205, 0, 0)
)

# Fiction — original unofficial color placeholders (replace PNGs anytime)
$fictionList = @(
  @{ f='chernarus.png'; r=40; g=90; b=40; l='CHER' },
  @{ f='takistan.png'; r=180; g=140; b=60; l='TAKI' },
  @{ f='csat.png'; r=120; g=40; b=40; l='CSAT' },
  @{ f='unsc.png'; r=40; g=70; b=120; l='UNSC' },
  @{ f='insurrectionists.png'; r=90; g=40; b=40; l='INS' },
  @{ f='osea.png'; r=30; g=60; b=140; l='OSEA' },
  @{ f='yuktobania.png'; r=160; g=30; b=30; l='YUK' },
  @{ f='belka.png'; r=50; g=50; b=50; l='BELKA' },
  @{ f='ustio.png'; r=200; g=180; b=40; l='USTIO' },
  @{ f='super_earth.png'; r=20; g=50; b=120; l='SE' },
  @{ f='automatons.png'; r=140; g=40; b=40; l='AUTO' },
  @{ f='usg_navy.png'; r=20; g=20; b=40; l='USG' },
  @{ f='gdi.png'; r=200; g=160; b=40; l='GDI' },
  @{ f='nod.png'; r=20; g=20; b=20; l='NOD' },
  @{ f='ncr.png'; r=180; g=120; b=40; l='NCR' },
  @{ f='bos.png'; r=90; g=90; b=100; l='BoS' },
  @{ f='enclave.png'; r=40; g=80; b=40; l='ENCL' },
  @{ f='imperium.png'; r=180; g=160; b=40; l='IMP' },
  @{ f='chaos.png'; r=100; g=20; b=20; l='CHAOS' },
  @{ f='renegades.png'; r=80; g=40; b=40; l='REN' },
  @{ f='tau.png'; r=40; g=120; b=140; l='TAU' },
  @{ f='eldar.png'; r=120; g=40; b=140; l='ELDAR' },
  @{ f='orks.png'; r=40; g=120; b=40; l='ORKS' },
  @{ f='necrons.png'; r=40; g=100; b=60; l='NEC' },
  @{ f='arasaka.png'; r=160; g=20; b=20; l='ARA' },
  @{ f='militech.png'; r=40; g=80; b=140; l='MILI' }
)

foreach ($item in $fictionList) {
  Save-Placeholder (Join-Path $flags $item.f) $item.r $item.g $item.b $item.l
  Write-Host "PH $($item.f)"
}

Write-Host "Flags written to $flags"
