$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$miuAssets = Join-Path $PSScriptRoot '..\assets'
[IO.Directory]::CreateDirectory($miuAssets) | Out-Null
$bitmap = New-Object Drawing.Bitmap 128,128
$g = [Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$ink = New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml('#0D0E12'))
$shell = New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml('#3A3C44'))
$light = New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml('#F5F5FA'))
$white = New-Object Drawing.Pen ([Drawing.ColorTranslator]::FromHtml('#A7A9B3')),9
$white.StartCap = [Drawing.Drawing2D.LineCap]::Round
$white.EndCap = [Drawing.Drawing2D.LineCap]::Round
function RoundRect([Drawing.Graphics]$Graphics,[Drawing.Brush]$Brush,[single]$X,[single]$Y,[single]$Width,[single]$Height,[single]$Radius) {
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $diameter = $Radius * 2
    $path.AddArc($X,$Y,$diameter,$diameter,180,90)
    $path.AddArc(($X+$Width-$diameter),$Y,$diameter,$diameter,270,90)
    $path.AddArc(($X+$Width-$diameter),($Y+$Height-$diameter),$diameter,$diameter,0,90)
    $path.AddArc($X,($Y+$Height-$diameter),$diameter,$diameter,90,90)
    $path.CloseFigure()
    $Graphics.FillPath($Brush,$path)
    $path.Dispose()
}
$antenna = New-Object Drawing.Drawing2D.GraphicsPath
$antenna.AddBezier(17,33,20,18,29,10,43,8)
$g.DrawPath($white,$antenna)
RoundRect $g $shell 19 33 103 84 27
RoundRect $g $ink 25 39 91 72 22
RoundRect $g $light 47 62 9 20 4.5
RoundRect $g $light 84 62 9 20 4.5
$png = New-Object IO.MemoryStream
$bitmap.Save($png,[Drawing.Imaging.ImageFormat]::Png)
$data = $png.ToArray()
[IO.File]::WriteAllBytes((Join-Path $miuAssets 'miu.png'),$data)
$file = [IO.File]::Create((Join-Path $miuAssets 'miu.ico'))
$writer = New-Object IO.BinaryWriter $file
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]128); $writer.Write([byte]128); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$data.Length); $writer.Write([uint32]22)
$writer.Write($data)
$writer.Dispose(); $png.Dispose(); $g.Dispose(); $bitmap.Dispose(); $ink.Dispose(); $shell.Dispose(); $white.Dispose(); $light.Dispose(); $antenna.Dispose()
Write-Host 'Miu icon generated from vector drawing.'
