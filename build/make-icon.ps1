# Generates Assets\fieldcheck.ico — a stylized white checkmark on a rounded slate-blue square.
# Run from anywhere; output path is resolved relative to this script.
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\src\FieldCheck.App\Assets\fieldcheck.ico")
)

Add-Type -AssemblyName System.Drawing

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Rounded-square background in the app accent (#2E6E8E).
    $accent = [System.Drawing.Color]::FromArgb(255, 46, 110, 142)
    $pad = [single]($size * 0.06)
    $side = [single]($size - 2 * $pad)
    $radius = [single]($size * 0.22)
    $d = [single]($radius * 2)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($pad, $pad, $d, $d, 180, 90)
    $path.AddArc($pad + $side - $d, $pad, $d, $d, 270, 90)
    $path.AddArc($pad + $side - $d, $pad + $side - $d, $d, $d, 0, 90)
    $path.AddArc($pad, $pad + $side - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $brush = New-Object System.Drawing.SolidBrush($accent)
    $g.FillPath($brush, $path)

    # White checkmark.
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, [single]($size * 0.10))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $p1 = New-Object System.Drawing.PointF([single]($size * 0.30), [single]($size * 0.53))
    $p2 = New-Object System.Drawing.PointF([single]($size * 0.44), [single]($size * 0.67))
    $p3 = New-Object System.Drawing.PointF([single]($size * 0.72), [single]($size * 0.34))
    $g.DrawLines($pen, @($p1, $p2, $p3))

    $pen.Dispose(); $brush.Dispose(); $path.Dispose(); $g.Dispose()
    return $bmp
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,($ms.ToArray())
    $bmp.Dispose(); $ms.Dispose()
}

$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$dir = [System.IO.Path]::GetDirectoryName($OutputPath)
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }

$fs = [System.IO.File]::Open($OutputPath, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)

# ICONDIR
$bw.Write([uint16]0)            # reserved
$bw.Write([uint16]1)            # type = icon
$bw.Write([uint16]$sizes.Count) # image count

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $len = $pngs[$i].Length
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))  # width
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))  # height
    $bw.Write([byte]0)          # palette count
    $bw.Write([byte]0)          # reserved
    $bw.Write([uint16]1)        # color planes
    $bw.Write([uint16]32)       # bits per pixel
    $bw.Write([uint32]$len)     # bytes in resource
    $bw.Write([uint32]$offset)  # offset
    $offset += $len
}
foreach ($png in $pngs) { $bw.Write($png) }

$bw.Flush(); $bw.Close(); $fs.Close()
Write-Output "Wrote $OutputPath ($([System.IO.FileInfo]::new($OutputPath).Length) bytes, $($sizes.Count) sizes)"
