# Generates src/TaLLon.App/Assets/default-bg.png and tallon.ico with System.Drawing.
# Run: powershell -ExecutionPolicy Bypass -File tools/make-assets.ps1
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root "src\TaLLon.App\Assets"
New-Item -ItemType Directory -Force $assets | Out-Null

# --- background: dark diagonal gradient with a soft grid and a faint "TaLLon" mark ---
$w = 2560; $h = 1440
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 24, 28, 34)), ([System.Drawing.Color]::FromArgb(255, 12, 14, 18)), 35
$g.FillRectangle($brush, $rect)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(14, 255, 255, 255)), 1
for ($x = 0; $x -lt $w; $x += 80) { $g.DrawLine($pen, $x, 0, $x, $h) }
for ($y = 0; $y -lt $h; $y += 80) { $g.DrawLine($pen, 0, $y, $w, $y) }
$glow = New-Object System.Drawing.Drawing2D.GraphicsPath
$glow.AddEllipse(($w * 0.55), ($h * 0.15), ($w * 0.7), ($h * 0.9))
$pgb = New-Object System.Drawing.Drawing2D.PathGradientBrush $glow
$pgb.CenterColor = [System.Drawing.Color]::FromArgb(40, 76, 155, 232)
$pgb.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 76, 155, 232))
$g.FillPath($pgb, $glow)
$font = New-Object System.Drawing.Font "Segoe UI", 120, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$tb = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(22, 255, 255, 255))
$g.DrawString("TaLLon", $font, $tb, ($w - 720), ($h - 260))
$bmp.Save((Join-Path $assets "default-bg.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()

# --- icon: rounded dark square with a blue T (256px PNG inside an ICO container) ---
$s = 256
$ib = New-Object System.Drawing.Bitmap $s, $s
$ig = [System.Drawing.Graphics]::FromImage($ib)
$ig.SmoothingMode = 'AntiAlias'
$ig.TextRenderingHint = 'AntiAliasGridFit'
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$r = 56
$path.AddArc(0, 0, $r, $r, 180, 90); $path.AddArc($s - $r, 0, $r, $r, 270, 90)
$path.AddArc($s - $r, $s - $r, $r, $r, 0, 90); $path.AddArc(0, $s - $r, $r, $r, 90, 90); $path.CloseFigure()
$ig.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 43, 43, 43))), $path)
$if = New-Object System.Drawing.Font "Segoe UI", 190, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
$ig.DrawString("T", $if, (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 76, 155, 232))), (New-Object System.Drawing.RectangleF 0, -8, $s, $s), $fmt)
$ms = New-Object System.IO.MemoryStream
$ib.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $ms.ToArray()
$ico = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ico
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]1)          # ICONDIR
$bw.Write([Byte]0); $bw.Write([Byte]0); $bw.Write([Byte]0); $bw.Write([Byte]0)  # 256x256 -> 0, colors, reserved
$bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]$png.Length); $bw.Write([UInt32]22)
$bw.Write($png); $bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $assets "tallon.ico"), $ico.ToArray())
$ig.Dispose(); $ib.Dispose()
Write-Host "assets written to $assets"
