$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $PSScriptRoot
$screenshots = Join-Path $projectRoot 'assets\screenshots'
$output = Join-Path $projectRoot 'assets\posters'
New-Item -ItemType Directory -Force -Path $output | Out-Null

function New-RoundedPath {
    param([System.Drawing.RectangleF]$Rect, [float]$Radius)
    $diameter = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($Rect.X, $Rect.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rect.X, $Rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Fill-RoundedRectangle {
    param($Graphics, $Brush, [System.Drawing.RectangleF]$Rect, [float]$Radius)
    $path = New-RoundedPath $Rect $Radius
    try { $Graphics.FillPath($Brush, $path) } finally { $path.Dispose() }
}

function Draw-RoundedRectangle {
    param($Graphics, $Pen, [System.Drawing.RectangleF]$Rect, [float]$Radius)
    $path = New-RoundedPath $Rect $Radius
    try { $Graphics.DrawPath($Pen, $path) } finally { $path.Dispose() }
}

function Draw-Text {
    param($Graphics, [string]$Text, $Font, $Brush, [System.Drawing.RectangleF]$Rect, [string]$Align = 'Near', [string]$LineAlign = 'Near')
    $format = New-Object System.Drawing.StringFormat
    try {
        $format.Alignment = [System.Drawing.StringAlignment]::$Align
        $format.LineAlignment = [System.Drawing.StringAlignment]::$LineAlign
        $format.Trimming = [System.Drawing.StringTrimming]::EllipsisWord
        $Graphics.DrawString($Text, $Font, $Brush, $Rect, $format)
    } finally { $format.Dispose() }
}

function Draw-ImageContain {
    param($Graphics, [string]$Path, [System.Drawing.RectangleF]$Rect)
    $image = [System.Drawing.Image]::FromFile($Path)
    try {
        $scale = [Math]::Min($Rect.Width / $image.Width, $Rect.Height / $image.Height)
        $width = [float]($image.Width * $scale)
        $height = [float]($image.Height * $scale)
        $x = [float]($Rect.X + ($Rect.Width - $width) / 2)
        $y = [float]($Rect.Y + ($Rect.Height - $height) / 2)
        $Graphics.DrawImage($image, $x, $y, $width, $height)
    } finally { $image.Dispose() }
}

function New-Graphics {
    param($Bitmap)
    $graphics = [System.Drawing.Graphics]::FromImage($Bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
    return $graphics
}

function Save-Poster {
    param($Bitmap, [string]$Name)
    $path = Join-Path $output $Name
    $Bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host "Generated: $path"
}

$fontRegular = 'Microsoft YaHei'
$fontBold = 'Microsoft YaHei'
$white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$ink = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(28, 39, 58))
$muted = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(96, 111, 135))
$blue = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(43, 87, 154))
$cyan = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(69, 193, 221))
$orange = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 172, 76))
$green = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(72, 190, 132))
$card = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 255))
$softBlue = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(232, 239, 251))

# Poster 1: overview
$bitmap = New-Object System.Drawing.Bitmap 1080, 1440
$graphics = New-Graphics $bitmap
try {
    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle 0, 0, 1080, 1440), [System.Drawing.Color]::FromArgb(23, 47, 91), [System.Drawing.Color]::FromArgb(43, 87, 154), 90)
    try { $graphics.FillRectangle($gradient, 0, 0, 1080, 1440) } finally { $gradient.Dispose() }
    Fill-RoundedRectangle $graphics $white ([System.Drawing.RectangleF]::new(72, 64, 76, 76)) 16
    Draw-Text $graphics 'Q' (New-Object System.Drawing.Font $fontBold, 34, ([System.Drawing.FontStyle]::Bold)) $blue ([System.Drawing.RectangleF]::new(72, 64, 76, 76)) 'Center' 'Center'
    Draw-Text $graphics 'QUIET READER' (New-Object System.Drawing.Font 'Segoe UI', 18, ([System.Drawing.FontStyle]::Bold)) $white ([System.Drawing.RectangleF]::new(170, 72, 500, 34))
    Draw-Text $graphics '把正版阅读藏进 Word' (New-Object System.Drawing.Font $fontBold, 52, ([System.Drawing.FontStyle]::Bold)) $white ([System.Drawing.RectangleF]::new(72, 185, 936, 90))
    Draw-Text $graphics '登录 · 书架 · 目录 · OCR 文字阅读' (New-Object System.Drawing.Font $fontRegular, 25) $white ([System.Drawing.RectangleF]::new(76, 292, 900, 44))
    $pillText = @('正版账号', '完整目录', '本地 OCR', '双阅读模式')
    for ($index = 0; $index -lt $pillText.Count; $index++) {
        $x = 74 + $index * 226
        $pillBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(38, 255, 255, 255))
        Fill-RoundedRectangle $graphics $pillBrush ([System.Drawing.RectangleF]::new($x, 354, 202, 54)) 27
        $pillBrush.Dispose()
        Draw-Text $graphics $pillText[$index] (New-Object System.Drawing.Font $fontRegular, 17) $white ([System.Drawing.RectangleF]::new($x, 354, 202, 54)) 'Center' 'Center'
    }
    $shadow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(55, 0, 0, 0))
    Fill-RoundedRectangle $graphics $shadow ([System.Drawing.RectangleF]::new(66, 454, 948, 642)) 24
    Fill-RoundedRectangle $graphics $card ([System.Drawing.RectangleF]::new(54, 442, 948, 642)) 24
    Draw-ImageContain $graphics (Join-Path $screenshots 'main-interface.png') ([System.Drawing.RectangleF]::new(70, 458, 916, 610))
    Draw-Text $graphics '像 Word 一样自然，像阅读器一样完整' (New-Object System.Drawing.Font $fontBold, 30, ([System.Drawing.FontStyle]::Bold)) $white ([System.Drawing.RectangleF]::new(72, 1150, 936, 48)) 'Center'
    Draw-Text $graphics 'Windows · WebView2 · PP-OCRv6' (New-Object System.Drawing.Font 'Segoe UI', 20) $white ([System.Drawing.RectangleF]::new(72, 1220, 936, 40)) 'Center'
    Draw-Text $graphics 'Quiet Reader' (New-Object System.Drawing.Font 'Segoe UI', 16) $white ([System.Drawing.RectangleF]::new(72, 1340, 936, 30)) 'Center'
    Save-Poster $bitmap '01-overview.png'
} finally { $graphics.Dispose(); $bitmap.Dispose() }

# Poster 2: features
$bitmap = New-Object System.Drawing.Bitmap 1080, 1440
$graphics = New-Graphics $bitmap
try {
    $graphics.Clear([System.Drawing.Color]::FromArgb(243, 246, 251))
    Draw-Text $graphics '四个核心特点' (New-Object System.Drawing.Font $fontBold, 48, ([System.Drawing.FontStyle]::Bold)) $ink ([System.Drawing.RectangleF]::new(60, 58, 960, 72))
    Draw-Text $graphics '隐蔽，但不牺牲完整体验' (New-Object System.Drawing.Font $fontRegular, 23) $muted ([System.Drawing.RectangleF]::new(64, 132, 900, 40))
    Fill-RoundedRectangle $graphics $card ([System.Drawing.RectangleF]::new(60, 205, 960, 390)) 24
    Draw-ImageContain $graphics (Join-Path $screenshots 'scrolling-mode.png') ([System.Drawing.RectangleF]::new(82, 225, 916, 350))
    $featureTitles = @('Word 级伪装', '本地 OCR 识别', '完整书架目录', '沉浸 / 滚动双模式')
    $featureNotes = @('全蓝标题栏与功能区', '正文仅在内存中处理', '长目录自动分页聚合', '随时回看已读内容')
    $featureBrushes = @($blue, $cyan, $orange, $green)
    for ($index = 0; $index -lt 4; $index++) {
        $column = $index % 2
        $row = [Math]::Floor($index / 2)
        $x = 60 + $column * 490
        $y = 640 + $row * 310
        Fill-RoundedRectangle $graphics $card ([System.Drawing.RectangleF]::new($x, $y, 470, 270)) 22
        Fill-RoundedRectangle $graphics $featureBrushes[$index] ([System.Drawing.RectangleF]::new($x + 28, $y + 30, 66, 66)) 18
        Draw-Text $graphics ('0' + ($index + 1)) (New-Object System.Drawing.Font 'Segoe UI', 20, ([System.Drawing.FontStyle]::Bold)) $white ([System.Drawing.RectangleF]::new($x + 28, $y + 30, 66, 66)) 'Center' 'Center'
        Draw-Text $graphics $featureTitles[$index] (New-Object System.Drawing.Font $fontBold, 25, ([System.Drawing.FontStyle]::Bold)) $ink ([System.Drawing.RectangleF]::new($x + 28, $y + 120, 414, 42))
        Draw-Text $graphics $featureNotes[$index] (New-Object System.Drawing.Font $fontRegular, 19) $muted ([System.Drawing.RectangleF]::new($x + 28, $y + 172, 414, 58))
    }
    Draw-Text $graphics '正版权限 · 本地识别 · 不保存正文' (New-Object System.Drawing.Font $fontBold, 22, ([System.Drawing.FontStyle]::Bold)) $blue ([System.Drawing.RectangleF]::new(60, 1290, 960, 52)) 'Center'
    Save-Poster $bitmap '02-features.png'
} finally { $graphics.Dispose(); $bitmap.Dispose() }

# Poster 3: tutorial
$bitmap = New-Object System.Drawing.Bitmap 1080, 1440
$graphics = New-Graphics $bitmap
try {
    $graphics.Clear([System.Drawing.Color]::FromArgb(248, 250, 253))
    $graphics.FillRectangle($blue, 0, 0, 1080, 250)
    Draw-Text $graphics '四步开始阅读' (New-Object System.Drawing.Font $fontBold, 50, ([System.Drawing.FontStyle]::Bold)) $white ([System.Drawing.RectangleF]::new(64, 62, 952, 76))
    Draw-Text $graphics '命令行操作，简单直接' (New-Object System.Drawing.Font $fontRegular, 24) $white ([System.Drawing.RectangleF]::new(68, 148, 900, 42))
    $steps = @(
        @('01', '登录账号', '/登录'),
        @('02', '读取书架', '/书架'),
        @('03', '选择书籍与章节', '输入序号'),
        @('04', '敲键显示正文', '/字数 或 /行数')
    )
    for ($index = 0; $index -lt $steps.Count; $index++) {
        $y = 310 + $index * 205
        Fill-RoundedRectangle $graphics $card ([System.Drawing.RectangleF]::new(64, $y, 952, 164)) 22
        Fill-RoundedRectangle $graphics $softBlue ([System.Drawing.RectangleF]::new(92, $y + 32, 98, 98)) 28
        Draw-Text $graphics $steps[$index][0] (New-Object System.Drawing.Font 'Segoe UI', 26, ([System.Drawing.FontStyle]::Bold)) $blue ([System.Drawing.RectangleF]::new(92, $y + 32, 98, 98)) 'Center' 'Center'
        Draw-Text $graphics $steps[$index][1] (New-Object System.Drawing.Font $fontBold, 26, ([System.Drawing.FontStyle]::Bold)) $ink ([System.Drawing.RectangleF]::new(226, $y + 30, 520, 48))
        Fill-RoundedRectangle $graphics $blue ([System.Drawing.RectangleF]::new(226, $y + 91, 370, 48)) 12
        Draw-Text $graphics $steps[$index][2] (New-Object System.Drawing.Font $fontRegular, 18) $white ([System.Drawing.RectangleF]::new(226, $y + 91, 370, 48)) 'Center' 'Center'
    }
    Fill-RoundedRectangle $graphics $card ([System.Drawing.RectangleF]::new(64, 1150, 952, 176)) 22
    Draw-Text $graphics '阅读中随时可用' (New-Object System.Drawing.Font $fontBold, 23, ([System.Drawing.FontStyle]::Bold)) $ink ([System.Drawing.RectangleF]::new(92, 1178, 300, 38))
    Draw-Text $graphics '/目录   /下一章   /上一章   /订阅' (New-Object System.Drawing.Font 'Consolas', 20, ([System.Drawing.FontStyle]::Bold)) $blue ([System.Drawing.RectangleF]::new(92, 1234, 850, 42))
    Draw-Text $graphics '输入 / 查看全部命令' (New-Object System.Drawing.Font $fontRegular, 18) $muted ([System.Drawing.RectangleF]::new(64, 1360, 952, 34)) 'Center'
    Save-Poster $bitmap '03-tutorial.png'
} finally { $graphics.Dispose(); $bitmap.Dispose() }

$white.Dispose(); $ink.Dispose(); $muted.Dispose(); $blue.Dispose(); $cyan.Dispose(); $orange.Dispose(); $green.Dispose(); $card.Dispose(); $softBlue.Dispose()
