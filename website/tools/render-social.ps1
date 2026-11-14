$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$siteRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$bitmap = New-Object Drawing.Bitmap 1200,630
$canvas = [Drawing.Graphics]::FromImage($bitmap)
$canvas.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$canvas.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$fonts = New-Object Drawing.Text.PrivateFontCollection
$fonts.AddFontFile((Join-Path $siteRoot 'dist/assets/manrope.ttf'))
function Brush([string]$color) { return New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml($color)) }
function Rounded([float]$x,[float]$y,[float]$w,[float]$h,[float]$r,[string]$color) {
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $diameter = 2 * $r
    $path.AddArc($x,$y,$diameter,$diameter,180,90)
    $path.AddArc(($x+$w-$diameter),$y,$diameter,$diameter,270,90)
    $path.AddArc(($x+$w-$diameter),($y+$h-$diameter),$diameter,$diameter,0,90)
    $path.AddArc($x,($y+$h-$diameter),$diameter,$diameter,90,90)
    $path.CloseFigure()
    $fill = Brush $color
    $canvas.FillPath($fill,$path)
    $fill.Dispose(); $path.Dispose()
}
function Label([string]$text,[float]$size,[float]$x,[float]$y,[string]$color) {
    $font = New-Object Drawing.Font $fonts.Families[0],$size,([Drawing.FontStyle]::Regular),([Drawing.GraphicsUnit]::Pixel)
    $fill = Brush $color
    $canvas.DrawString($text,$font,$fill,$x,$y)
    $font.Dispose(); $fill.Dispose()
}
try {
    $canvas.Clear([Drawing.ColorTranslator]::FromHtml('#f7f6f2'))
    Label 'miu' 46 65 42 '#292830'
    Label 'A little company,' 57 65 190 '#292830'
    Label 'wherever you are.' 57 65 263 '#817b89'
    Label 'Your offline AI companion.' 24 68 365 '#6c6574'
    Rounded 69 430 302 56 14 '#292830'
    Label 'Free for Windows + Android' 18 91 445 '#ffffff'
    $halo = Brush '#e8e1f0'; $canvas.FillEllipse($halo,709,112,428,428); $halo.Dispose()
    $state = $canvas.Save()
    $canvas.TranslateTransform(917,310); $canvas.RotateTransform(-8); $canvas.TranslateTransform(-917,-310)
    $pen = New-Object Drawing.Pen ([Drawing.ColorTranslator]::FromHtml('#a5a0b1')),26
    $pen.StartCap = [Drawing.Drawing2D.LineCap]::Round; $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
    $canvas.DrawBezier($pen,788,190,796,142,820,117,855,112); $pen.Dispose()
    Rounded 776 208 290 248 73 '#3d3c47'
    Rounded 782 214 278 236 68 '#56535f'
    Rounded 790 222 262 220 62 '#101117'
    Rounded 847 292 24 48 12 '#f5f3ff'
    Rounded 963 292 24 48 12 '#f5f3ff'
    $smile = New-Object Drawing.Pen ([Drawing.ColorTranslator]::FromHtml('#f5f3ff')),6
    $smile.StartCap = [Drawing.Drawing2D.LineCap]::Round; $smile.EndCap = [Drawing.Drawing2D.LineCap]::Round
    $canvas.DrawBezier($smile,896,367,906,390,927,390,938,367); $smile.Dispose()
    $canvas.Restore($state)
    Label 'A friendly face. Right here.' 17 802 548 '#78638d'
    $bitmap.Save((Join-Path $siteRoot 'dist/assets/social.png'),[Drawing.Imaging.ImageFormat]::Png)
} finally { $canvas.Dispose(); $bitmap.Dispose(); $fonts.Dispose() }
Write-Output 'Rendered social.png (1200 x 630).'
