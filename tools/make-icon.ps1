# Draws the chest icon and writes src/ChestTracker/Assets/app.ico (PNG-compressed, 16-256 px).
# Same drawing as ChestArt.cs. Windows only (System.Drawing). Run: pwsh tools/make-icon.ps1
Add-Type -AssemblyName System.Drawing

function New-ChestBitmap([int]$size) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $pad = $size * 0.06
    $x = $pad; $y = $pad + $size * 0.08; $w = $size - 2 * $pad; $h = $size - 2 * $pad - $size * 0.16
    $stroke = [Math]::Max(1, $w / 14)
    $lidH = $h * 0.42
    $gold = [System.Drawing.Color]::FromArgb(255, 205, 70)
    $goldDark = [System.Drawing.Color]::FromArgb(214, 150, 30)
    $wood = [System.Drawing.Color]::FromArgb(140, 82, 40)
    $ink = [System.Drawing.Color]::FromArgb(28, 20, 12)

    $r = $w * 0.22; $d = $r * 2
    $lid = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $lid.AddArc($x, $y, $d, $d, 180, 90); $lid.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $lid.AddLine($x + $w, $y + $lidH, $x, $y + $lidH); $lid.CloseFigure()
    $r2 = $w * 0.08; $d2 = $r2 * 2; $by = $y + $lidH; $bh = $h - $lidH
    $body = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $body.AddLine($x, $by, $x + $w, $by); $body.AddArc($x + $w - $d2, $by + $bh - $d2, $d2, $d2, 0, 90)
    $body.AddArc($x, $by + $bh - $d2, $d2, $d2, 90, 90); $body.CloseFigure()

    $g.FillPath([System.Drawing.SolidBrush]::new($wood), $body)
    $lidRect = [System.Drawing.RectangleF]::new($x, $y, $w, $lidH)
    $g.FillPath([System.Drawing.Drawing2D.LinearGradientBrush]::new($lidRect, $gold, $goldDark, [System.Drawing.Drawing2D.LinearGradientMode]::Vertical), $lid)
    $band = [System.Drawing.SolidBrush]::new($gold)
    $bw = $w * 0.14
    $g.FillRectangle($band, [single]($x + $w * 0.12), [single]$by, [single]$bw, [single]$bh)
    $g.FillRectangle($band, [single]($x + $w - $w * 0.12 - $bw), [single]$by, [single]$bw, [single]$bh)
    $lock = [System.Drawing.RectangleF]::new($x + $w * 0.39, $y + $lidH - $h * 0.12, $w * 0.22, $h * 0.3)
    $g.FillRectangle($band, $lock)
    $pen = [System.Drawing.Pen]::new($ink, $stroke); $pen.LineJoin = 'Round'
    $g.DrawPath($pen, $body); $g.DrawPath($pen, $lid)
    $g.DrawRectangle($pen, $lock.X, $lock.Y, $lock.Width, $lock.Height)
    $g.Dispose()
    $bmp
}

$sizes = 16, 24, 32, 48, 64, 256
$images = foreach ($s in $sizes) {
    $bmp = New-ChestBitmap $s
    $ms = [System.IO.MemoryStream]::new()
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

$out = Join-Path $PSScriptRoot '..\src\ChestTracker\Assets\app.ico'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
$fs = [System.IO.File]::Create($out)
$bw = [System.IO.BinaryWriter]::new($fs)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $bw.Write([byte]($s % 256)); $bw.Write([byte]($s % 256)) # 0 means 256
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$images[$i].Length); $bw.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $bw.Write($img) }
$bw.Dispose()
Write-Host "wrote $out"
