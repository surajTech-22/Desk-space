$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class DeskSpaceIconNative {
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr handle);
}
'@
$path='DeskSpace_Windows_R1/DeskSpace/DeskSpace.ico'
$bitmap = [System.Drawing.Bitmap]::new(64,64,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gfx=[System.Drawing.Graphics]::FromImage($bitmap)
$gfx.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$gfx.Clear([System.Drawing.Color]::Transparent)
function Tile([System.Drawing.Graphics]$g,[int]$x,[int]$y,[int]$size,[int]$radius,[System.Drawing.Color]$color) {
    $p=[System.Drawing.Drawing2D.GraphicsPath]::new()
    $d=2*$radius
    $p.AddArc($x,$y,$d,$d,180,90)
    $p.AddArc($x+$size-$d,$y,$d,$d,270,90)
    $p.AddArc($x+$size-$d,$y+$size-$d,$d,$d,0,90)
    $p.AddArc($x,$y+$size-$d,$d,$d,90,90)
    $p.CloseFigure()
    $brush=[System.Drawing.SolidBrush]::new($color)
    try { $g.FillPath($brush,$p) } finally { $brush.Dispose();$p.Dispose() }
}
$bg=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(27,36,77))
$gfx.FillEllipse($bg,2,2,60,60)
$bg.Dispose()
Tile $gfx 13 13 18 5 ([System.Drawing.Color]::FromArgb(46,155,253))
Tile $gfx 33 13 18 5 ([System.Drawing.Color]::FromArgb(144,95,228))
Tile $gfx 13 33 18 5 ([System.Drawing.Color]::FromArgb(36,207,184))
Tile $gfx 33 33 18 5 ([System.Drawing.Color]::FromArgb(255,176,59))
$handle=$bitmap.GetHicon()
try {
    $icon=[System.Drawing.Icon]::FromHandle($handle)
    $stream=[IO.File]::Create($path)
    try {$icon.Save($stream)} finally {$stream.Dispose();$icon.Dispose()}
} finally {
    [void][DeskSpaceIconNative]::DestroyIcon($handle)
    $gfx.Dispose();$bitmap.Dispose()
}
if((Get-Item $path).Length -lt 500){throw 'Generated DeskSpace icon is invalid'}
$iss='installer/DeskSpace.iss'
$s=[IO.File]::ReadAllText($iss)
if (!$s.Contains('[Setup]')) {throw 'Inno Setup section not found'}
if (!$s.Contains('SetupIconFile=')) {
    $s=$s.Replace('[Setup]','[Setup]'+[Environment]::NewLine+'SetupIconFile=..\DeskSpace_Windows_R1\DeskSpace\DeskSpace.ico')
}
[IO.File]::WriteAllText($iss,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'Brand icon generated and linked to the app executable and setup installer.'
