$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$screenshotDirectory = Join-Path $projectRoot 'assets\screenshots'
$outputDirectory = Join-Path $projectRoot 'assets\store-posters'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$width = 1600
$height = 900
$fontRegular = 'Microsoft YaHei UI'
$fontBold = 'Microsoft YaHei UI'

function New-RoundedPath([System.Drawing.RectangleF]$rectangle, [float]$radius) {
    $diameter = $radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rectangle.X, $rectangle.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($rectangle.Right - $diameter, $rectangle.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($rectangle.Right - $diameter, $rectangle.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($rectangle.X, $rectangle.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Fill-RoundedRectangle($graphics, $brush, [System.Drawing.RectangleF]$rectangle, [float]$radius) {
    $path = New-RoundedPath $rectangle $radius
    try { $graphics.FillPath($brush, $path) } finally { $path.Dispose() }
}

function Draw-RoundedRectangle($graphics, $pen, [System.Drawing.RectangleF]$rectangle, [float]$radius) {
    $path = New-RoundedPath $rectangle $radius
    try { $graphics.DrawPath($pen, $path) } finally { $path.Dispose() }
}

function Draw-Text($graphics, [string]$text, $font, $brush, [System.Drawing.RectangleF]$rectangle, [string]$horizontal = 'Near', [string]$vertical = 'Near') {
    $format = New-Object System.Drawing.StringFormat
    try {
        $format.Alignment = [System.Drawing.StringAlignment]::$horizontal
        $format.LineAlignment = [System.Drawing.StringAlignment]::$vertical
        $format.Trimming = [System.Drawing.StringTrimming]::EllipsisCharacter
        $graphics.DrawString($text, $font, $brush, $rectangle, $format)
    } finally { $format.Dispose() }
}

function New-Graphics($bitmap) {
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    return $graphics
}

function Draw-ScreenshotCard($graphics, [string]$imagePath, [System.Drawing.RectangleF]$rectangle, [float]$radius = 24) {
    $shadow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(55, 15, 37, 72))
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $border = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(55, 50, 79, 121)), 1.5
    try {
        Fill-RoundedRectangle $graphics $shadow ([System.Drawing.RectangleF]::new($rectangle.X + 18, $rectangle.Y + 20, $rectangle.Width, $rectangle.Height)) $radius
        Fill-RoundedRectangle $graphics $white $rectangle $radius
        $image = [System.Drawing.Image]::FromFile($imagePath)
        try {
            $padding = 14
            $inner = [System.Drawing.RectangleF]::new($rectangle.X + $padding, $rectangle.Y + $padding, $rectangle.Width - $padding * 2, $rectangle.Height - $padding * 2)
            $scale = [Math]::Min($inner.Width / $image.Width, $inner.Height / $image.Height)
            $drawWidth = $image.Width * $scale
            $drawHeight = $image.Height * $scale
            $destination = [System.Drawing.RectangleF]::new($inner.X + ($inner.Width - $drawWidth) / 2, $inner.Y + ($inner.Height - $drawHeight) / 2, $drawWidth, $drawHeight)
            $state = $graphics.Save()
            $clip = New-RoundedPath $inner ([Math]::Max(4, $radius - 8))
            try {
                $graphics.SetClip($clip)
                $graphics.DrawImage($image, $destination)
            } finally {
                $graphics.Restore($state)
                $clip.Dispose()
            }
        } finally { $image.Dispose() }
        Draw-RoundedRectangle $graphics $border $rectangle $radius
    } finally {
        $shadow.Dispose(); $white.Dispose(); $border.Dispose()
    }
}

function Draw-Chip($graphics, [string]$text, [float]$x, [float]$y, [float]$chipWidth, $background, $foreground) {
    Fill-RoundedRectangle $graphics $background ([System.Drawing.RectangleF]::new($x, $y, $chipWidth, 48)) 24
    $font = New-Object System.Drawing.Font $fontRegular, 18, ([System.Drawing.FontStyle]::Regular)
    try { Draw-Text $graphics $text $font $foreground ([System.Drawing.RectangleF]::new($x, $y, $chipWidth, 48)) 'Center' 'Center' } finally { $font.Dispose() }
}

function Draw-Brand($graphics, $foreground, [float]$x = 70, [float]$y = 52) {
    $badge = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(238, 246, 255))
    $blueText = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(41, 92, 164))
    $fontQ = New-Object System.Drawing.Font 'Segoe UI', 28, ([System.Drawing.FontStyle]::Bold)
    $fontBrand = New-Object System.Drawing.Font 'Segoe UI', 17, ([System.Drawing.FontStyle]::Bold)
    try {
        Fill-RoundedRectangle $graphics $badge ([System.Drawing.RectangleF]::new($x, $y, 58, 58)) 15
        Draw-Text $graphics 'Q' $fontQ $blueText ([System.Drawing.RectangleF]::new($x, $y - 1, 58, 58)) 'Center' 'Center'
        Draw-Text $graphics 'WORDREADER' $fontBrand $foreground ([System.Drawing.RectangleF]::new($x + 76, $y + 10, 240, 40)) 'Near' 'Center'
    } finally { $badge.Dispose(); $blueText.Dispose(); $fontQ.Dispose(); $fontBrand.Dispose() }
}

function Save-Poster($bitmap, [string]$name) {
    $path = Join-Path $outputDirectory $name
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output "Generated: $path"
}

$navy = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(28, 58, 103))
$blue = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(47, 92, 158))
$brightBlue = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(54, 108, 190))
$cyan = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(54, 192, 222))
$white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$ink = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(18, 38, 70))
$muted = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(86, 112, 151))
$pale = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(241, 246, 253))
$softBlue = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(220, 233, 251))
$green = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(69, 190, 133))
$orange = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 170, 70))

# 01 - Word shell
$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = New-Graphics $bitmap
try {
    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.Point]::new(0, 0)), ([System.Drawing.Point]::new($width, $height)), ([System.Drawing.Color]::FromArgb(25, 55, 101)), ([System.Drawing.Color]::FromArgb(52, 101, 174))
    try { $graphics.FillRectangle($gradient, 0, 0, $width, $height) } finally { $gradient.Dispose() }
    Draw-Brand $graphics $white
    $titleFont = New-Object System.Drawing.Font $fontBold, 53, ([System.Drawing.FontStyle]::Bold)
    $questionFont = New-Object System.Drawing.Font $fontRegular, 25
    $answerFont = New-Object System.Drawing.Font $fontBold, 29, ([System.Drawing.FontStyle]::Bold)
    $descriptionFont = New-Object System.Drawing.Font $fontRegular, 23
    try {
        Draw-Text $graphics '摸鱼神器' $titleFont $white ([System.Drawing.RectangleF]::new(74, 170, 480, 82))
        Draw-Text $graphics '上班想摸鱼？' $questionFont $white ([System.Drawing.RectangleF]::new(78, 282, 450, 46))
        Draw-Text $graphics '不如试试 WordReader！' $answerFont $white ([System.Drawing.RectangleF]::new(78, 334, 490, 54))
        Draw-Text $graphics '像编辑 Word 一样看小说' $descriptionFont $white ([System.Drawing.RectangleF]::new(78, 420, 480, 44))
    } finally { $titleFont.Dispose(); $questionFont.Dispose(); $answerFont.Dispose(); $descriptionFont.Dispose() }
    Fill-RoundedRectangle $graphics $orange ([System.Drawing.RectangleF]::new(78, 488, 448, 58)) 18
    $highlightFont = New-Object System.Drawing.Font $fontBold, 20, ([System.Drawing.FontStyle]::Bold)
    try { Draw-Text $graphics '老板看了以为你在工作！' $highlightFont $ink ([System.Drawing.RectangleF]::new(78, 488, 448, 58)) 'Center' 'Center' } finally { $highlightFont.Dispose() }
    Draw-Chip $graphics '正版书架' 78 588 150 $brightBlue $white
    Draw-Chip $graphics '本地 OCR' 244 588 150 $brightBlue $white
    Draw-Chip $graphics '打字显字' 78 652 176 $brightBlue $white
    Draw-Chip $graphics '完整目录' 270 652 150 $brightBlue $white
    Draw-ScreenshotCard $graphics (Join-Path $screenshotDirectory 'main-interface.png') ([System.Drawing.RectangleF]::new(600, 110, 920, 680)) 30
    Save-Poster $bitmap '01-word-like-interface.png'
} finally { $graphics.Dispose(); $bitmap.Dispose() }

# 02 - OCR
$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = New-Graphics $bitmap
try {
    $graphics.Clear([System.Drawing.Color]::FromArgb(245, 249, 255))
    Draw-Brand $graphics $blue
    $titleFont = New-Object System.Drawing.Font $fontBold, 52, ([System.Drawing.FontStyle]::Bold)
    $subtitleFont = New-Object System.Drawing.Font $fontRegular, 23
    try {
        Draw-Text $graphics '本地 OCR' $titleFont $ink ([System.Drawing.RectangleF]::new(78, 165, 470, 75))
        Draw-Text $graphics '还原纯文字阅读' $titleFont $blue ([System.Drawing.RectangleF]::new(78, 238, 570, 75))
        Draw-Text $graphics 'PP-OCRv6 Small · Windows OCR 兜底' $subtitleFont $muted ([System.Drawing.RectangleF]::new(82, 337, 580, 46))
        Draw-Text $graphics '仅识别当前已授权页面' $subtitleFont $ink ([System.Drawing.RectangleF]::new(82, 416, 460, 42))
        Draw-Text $graphics '正文只在内存中处理' $subtitleFont $ink ([System.Drawing.RectangleF]::new(82, 468, 460, 42))
    } finally { $titleFont.Dispose(); $subtitleFont.Dispose() }
    Draw-Chip $graphics '视口预缓存' 82 548 176 $softBlue $blue
    Draw-Chip $graphics '边界去重' 274 548 150 $softBlue $blue
    Draw-Chip $graphics '自然段合并' 440 548 176 $softBlue $blue
    Draw-ScreenshotCard $graphics (Join-Path $screenshotDirectory 'scrolling-mode.png') ([System.Drawing.RectangleF]::new(690, 105, 830, 690)) 30
    Save-Poster $bitmap '02-local-ocr.png'
} finally { $graphics.Dispose(); $bitmap.Dispose() }

# 03 - Reading modes
$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = New-Graphics $bitmap
try {
    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.Point]::new(0, 0)), ([System.Drawing.Point]::new($width, 0)), ([System.Drawing.Color]::FromArgb(30, 64, 116)), ([System.Drawing.Color]::FromArgb(72, 120, 191))
    try { $graphics.FillRectangle($gradient, 0, 0, $width, $height) } finally { $gradient.Dispose() }
    Draw-Brand $graphics $white
    $titleFont = New-Object System.Drawing.Font $fontBold, 49, ([System.Drawing.FontStyle]::Bold)
    $subtitleFont = New-Object System.Drawing.Font $fontRegular, 22
    try {
        Draw-Text $graphics '沉浸 / 滚动 双模式' $titleFont $white ([System.Drawing.RectangleF]::new(70, 135, 1460, 76)) 'Center'
        Draw-Text $graphics '专注推进，或随时回看已读内容' $subtitleFont $white ([System.Drawing.RectangleF]::new(70, 218, 1460, 42)) 'Center'
    } finally { $titleFont.Dispose(); $subtitleFont.Dispose() }
    Draw-ScreenshotCard $graphics (Join-Path $screenshotDirectory 'main-interface.png') ([System.Drawing.RectangleF]::new(70, 315, 700, 455)) 24
    Draw-ScreenshotCard $graphics (Join-Path $screenshotDirectory 'scrolling-mode.png') ([System.Drawing.RectangleF]::new(830, 315, 700, 455)) 24
    $labelFont = New-Object System.Drawing.Font $fontBold, 23, ([System.Drawing.FontStyle]::Bold)
    try {
        Draw-Text $graphics '沉浸模式 · 按键推进' $labelFont $white ([System.Drawing.RectangleF]::new(70, 792, 700, 46)) 'Center'
        Draw-Text $graphics '滚动模式 · 连续 A4 页面' $labelFont $white ([System.Drawing.RectangleF]::new(830, 792, 700, 46)) 'Center'
    } finally { $labelFont.Dispose() }
    Save-Poster $bitmap '03-dual-reading-modes.png'
} finally { $graphics.Dispose(); $bitmap.Dispose() }

# 04 - Catalog and cache
$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = New-Graphics $bitmap
try {
    $graphics.Clear([System.Drawing.Color]::FromArgb(239, 245, 253))
    Draw-Brand $graphics $blue
    Draw-ScreenshotCard $graphics (Join-Path $screenshotDirectory 'main-interface.png') ([System.Drawing.RectangleF]::new(70, 160, 900, 620)) 28
    $titleFont = New-Object System.Drawing.Font $fontBold, 36, ([System.Drawing.FontStyle]::Bold)
    $cardTitleFont = New-Object System.Drawing.Font $fontBold, 24, ([System.Drawing.FontStyle]::Bold)
    $cardNoteFont = New-Object System.Drawing.Font $fontRegular, 18
    try {
        Draw-Text $graphics '长篇阅读，也要顺畅' $titleFont $ink ([System.Drawing.RectangleF]::new(1015, 148, 535, 70))
        $items = @(
            @('完整目录', '聚合分页与章节分组', $blue),
            @('历史进度', '直接继续上次章节', $green),
            @('四页缓存', '提前识别后续视口', $cyan),
            @('智能去重', '处理上下页重叠文本', $orange)
        )
        for ($index = 0; $index -lt $items.Count; $index++) {
            $y = 285 + $index * 128
            Fill-RoundedRectangle $graphics $white ([System.Drawing.RectangleF]::new(1030, $y, 500, 105)) 20
            Fill-RoundedRectangle $graphics $items[$index][2] ([System.Drawing.RectangleF]::new(1052, $y + 22, 58, 58)) 16
            Draw-Text $graphics ([string]($index + 1)) $cardTitleFont $white ([System.Drawing.RectangleF]::new(1052, $y + 22, 58, 58)) 'Center' 'Center'
            Draw-Text $graphics $items[$index][0] $cardTitleFont $ink ([System.Drawing.RectangleF]::new(1132, $y + 17, 360, 40))
            Draw-Text $graphics $items[$index][1] $cardNoteFont $muted ([System.Drawing.RectangleF]::new(1134, $y + 60, 350, 30))
        }
    } finally { $titleFont.Dispose(); $cardTitleFont.Dispose(); $cardNoteFont.Dispose() }
    Save-Poster $bitmap '04-catalog-and-cache.png'
} finally { $graphics.Dispose(); $bitmap.Dispose() }

# 05 - Commands and guide
$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = New-Graphics $bitmap
try {
    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.Point]::new(0, 0)), ([System.Drawing.Point]::new(0, $height)), ([System.Drawing.Color]::FromArgb(247, 250, 255)), ([System.Drawing.Color]::FromArgb(224, 235, 251))
    try { $graphics.FillRectangle($gradient, 0, 0, $width, $height) } finally { $gradient.Dispose() }
    Draw-Brand $graphics $blue
    $titleFont = New-Object System.Drawing.Font $fontBold, 50, ([System.Drawing.FontStyle]::Bold)
    $subtitleFont = New-Object System.Drawing.Font $fontRegular, 22
    try {
        Draw-Text $graphics '所有操作，都在顶部' $titleFont $ink ([System.Drawing.RectangleF]::new(70, 132, 1460, 76)) 'Center'
        Draw-Text $graphics '命令补全、样式菜单与新手指引' $subtitleFont $muted ([System.Drawing.RectangleF]::new(70, 215, 1460, 44)) 'Center'
    } finally { $titleFont.Dispose(); $subtitleFont.Dispose() }
    Draw-ScreenshotCard $graphics (Join-Path $screenshotDirectory 'style-gallery.png') ([System.Drawing.RectangleF]::new(70, 310, 700, 455)) 24
    Draw-ScreenshotCard $graphics (Join-Path $screenshotDirectory 'new-user-guide.png') ([System.Drawing.RectangleF]::new(830, 310, 700, 455)) 24
    Draw-Chip $graphics '/ 命令候选' 295 792 180 $blue $white
    Draw-Chip $graphics 'Tab 补全' 492 792 150 $blue $white
    Draw-Chip $graphics '新手指引' 958 792 150 $blue $white
    Draw-Chip $graphics '无刷新返回' 1124 792 176 $blue $white
    Save-Poster $bitmap '05-commands-and-guide.png'
} finally { $graphics.Dispose(); $bitmap.Dispose() }

$navy.Dispose(); $blue.Dispose(); $brightBlue.Dispose(); $cyan.Dispose(); $white.Dispose(); $ink.Dispose(); $muted.Dispose(); $pale.Dispose(); $softBlue.Dispose(); $green.Dispose(); $orange.Dispose()
