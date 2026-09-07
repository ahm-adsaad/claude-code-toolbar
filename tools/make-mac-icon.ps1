param([string]$Out = (Join-Path $PSScriptRoot "..\mac\Resources\AppIcon.png"))
Add-Type -AssemblyName System.Drawing
$Out = [System.IO.Path]::GetFullPath($Out)
New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null

$s = 1024; $m = 64
$bmp = New-Object System.Drawing.Bitmap $s, $s
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

$w = $s - 2 * $m; $r = [int]($w * 0.22); $d = $r * 2
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddArc($m, $m, $d, $d, 180, 90)
$path.AddArc($m + $w - $d, $m, $d, $d, 270, 90)
$path.AddArc($m + $w - $d, $m + $w - $d, $d, $d, 0, 90)
$path.AddArc($m, $m + $w - $d, $d, $d, 90, 90)
$path.CloseFigure()
$bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 217, 119, 87))
$g.FillPath($bg, $path)

$fg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
$pad = [int]($w * 0.22); $h = [int]($w * 0.12); $gap = [int]($w * 0.14)
$y1 = $m + [int]($w * 0.30); $y2 = $y1 + $h + $gap
$bar1 = New-Object System.Drawing.Drawing2D.GraphicsPath
$bar1.AddArc($m + $pad, $y1, $h, $h, 90, 180)
$bar1.AddArc($m + $w - $pad - $h, $y1, $h, $h, 270, 180)
$bar1.CloseFigure()
$g.FillPath($fg, $bar1)
$w2 = [int](($w - 2 * $pad) * 0.6)
$bar2 = New-Object System.Drawing.Drawing2D.GraphicsPath
$bar2.AddArc($m + $pad, $y2, $h, $h, 90, 180)
$bar2.AddArc($m + $pad + $w2 - $h, $y2, $h, $h, 270, 180)
$bar2.CloseFigure()
$g.FillPath($fg, $bar2)
$g.Dispose()

$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
"wrote $Out"
