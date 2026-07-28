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

# 06 - Discovery and filters
$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = New-Graphics $bitmap
try {
    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.Point]::new(0, 0)), ([System.Drawing.Point]::new($width, $height)), ([System.Drawing.Color]::FromArgb(24, 53, 98)), ([System.Drawing.Color]::FromArgb(61, 116, 194))
    try { $graphics.FillRectangle($gradient, 0, 0, $width, $height) } finally { $gradient.Dispose() }
    Draw-Brand $graphics $white
    $titleFont = New-Object System.Drawing.Font $fontBold, 38, ([System.Drawing.FontStyle]::Bold)
    $subtitleFont = New-Object System.Drawing.Font $fontRegular, 23
    try {
        Draw-Text $graphics "搜索 / 分类 / 排行`n一条命令找新书" $titleFont $white ([System.Drawing.RectangleF]::new(76, 174, 500, 132))
        Draw-Text $graphics '命令驱动的全站发现' $subtitleFont $white ([System.Drawing.RectangleF]::new(82, 345, 430, 44))
    } finally { $titleFont.Dispose(); $subtitleFont.Dispose() }
    Draw-Chip $graphics '多级筛选' 82 438 150 $brightBlue $white
    Draw-Chip $graphics '排序方式' 248 438 150 $brightBlue $white
    Draw-Chip $graphics '分页聚合' 82 502 150 $brightBlue $white
    Draw-Chip $graphics '序号选择' 248 502 150 $brightBlue $white
    Fill-RoundedRectangle $graphics $white ([System.Drawing.RectangleF]::new(620, 110, 900, 680)) 30
    $paper = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(249, 251, 254))
    $line = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(218, 228, 242)), 2
    $commandFont = New-Object System.Drawing.Font 'Consolas', 22, ([System.Drawing.FontStyle]::Bold)
    $listTitleFont = New-Object System.Drawing.Font $fontBold, 24, ([System.Drawing.FontStyle]::Bold)
    $listTextFont = New-Object System.Drawing.Font $fontRegular, 18
    try {
        Fill-RoundedRectangle $graphics $paper ([System.Drawing.RectangleF]::new(650, 140, 840, 620)) 20
        Fill-RoundedRectangle $graphics $softBlue ([System.Drawing.RectangleF]::new(690, 176, 740, 62)) 14
        Draw-Text $graphics '/分类 玄幻  ·  /排序 人气' $commandFont $blue ([System.Drawing.RectangleF]::new(716, 181, 690, 50)) 'Near' 'Center'
        Draw-Text $graphics '发现小说 · 当前第 2 页' $listTitleFont $ink ([System.Drawing.RectangleF]::new(692, 278, 690, 46))
        $books = @(
            @('01', '分类结果一', '连载 · 玄幻 · 最新章节已更新'),
            @('02', '分类结果二', '完本 · 轻小说 · 可直接查看详情'),
            @('03', '分类结果三', '连载 · 都市 · 支持加入书架'),
            @('04', '分类结果四', '完本 · 科幻 · 输入序号进入详情')
        )
        for ($index = 0; $index -lt $books.Count; $index++) {
            $y = 350 + $index * 88
            $graphics.DrawLine($line, 692, $y + 76, 1440, $y + 76)
            Fill-RoundedRectangle $graphics $blue ([System.Drawing.RectangleF]::new(694, $y + 4, 52, 52)) 14
            Draw-Text $graphics $books[$index][0] $listTextFont $white ([System.Drawing.RectangleF]::new(694, $y + 4, 52, 52)) 'Center' 'Center'
            Draw-Text $graphics $books[$index][1] $listTitleFont $ink ([System.Drawing.RectangleF]::new(772, $y, 500, 38))
            Draw-Text $graphics $books[$index][2] $listTextFont $muted ([System.Drawing.RectangleF]::new(774, $y + 40, 630, 30))
        }
        Draw-Text $graphics 'N 下一页   P 上一页   /筛选   /结果' $commandFont $blue ([System.Drawing.RectangleF]::new(690, 704, 740, 36)) 'Center'
    } finally { $paper.Dispose(); $line.Dispose(); $commandFont.Dispose(); $listTitleFont.Dispose(); $listTextFont.Dispose() }
    Save-Poster $bitmap '06-discovery-and-filters.png'
} finally { $graphics.Dispose(); $bitmap.Dispose() }

# 07 - Precise chapter navigation
$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = New-Graphics $bitmap
try {
    $graphics.Clear([System.Drawing.Color]::FromArgb(244, 248, 254))
    Draw-Brand $graphics $blue
    $titleFont = New-Object System.Drawing.Font $fontBold, 48, ([System.Drawing.FontStyle]::Bold)
    $subtitleFont = New-Object System.Drawing.Font $fontRegular, 22
    try {
        Draw-Text $graphics '选中第几章，就打开第几章' $titleFont $ink ([System.Drawing.RectangleF]::new(70, 132, 1460, 76)) 'Center'
        Draw-Text $graphics '分页聚合后仍保持显示序号与真实章节一一对应' $subtitleFont $muted ([System.Drawing.RectangleF]::new(70, 215, 1460, 42)) 'Center'
    } finally { $titleFont.Dispose(); $subtitleFont.Dispose() }
    $flowTitles = @('发现列表', '书籍详情', '完整目录', '正版阅读')
    $flowNotes = @('搜索 / 分类 / 排行', '简介与加入书架', '历史进度与 VIP 标记', '按键显示正文')
    $flowColors = @($blue, $cyan, $orange, $green)
    $flowTitleFont = New-Object System.Drawing.Font $fontBold, 24, ([System.Drawing.FontStyle]::Bold)
    $flowNoteFont = New-Object System.Drawing.Font $fontRegular, 17
    $arrowFont = New-Object System.Drawing.Font 'Segoe UI Symbol', 30, ([System.Drawing.FontStyle]::Bold)
    try {
        for ($index = 0; $index -lt 4; $index++) {
            $x = 74 + $index * 382
            Fill-RoundedRectangle $graphics $white ([System.Drawing.RectangleF]::new($x, 330, 318, 250)) 24
            Fill-RoundedRectangle $graphics $flowColors[$index] ([System.Drawing.RectangleF]::new($x + 28, 360, 64, 64)) 18
            Draw-Text $graphics ([string]($index + 1)) $flowTitleFont $white ([System.Drawing.RectangleF]::new($x + 28, 360, 64, 64)) 'Center' 'Center'
            Draw-Text $graphics $flowTitles[$index] $flowTitleFont $ink ([System.Drawing.RectangleF]::new($x + 28, 454, 262, 42)) 'Center'
            Draw-Text $graphics $flowNotes[$index] $flowNoteFont $muted ([System.Drawing.RectangleF]::new($x + 24, 514, 270, 34)) 'Center'
            if ($index -lt 3) { Draw-Text $graphics '›' $arrowFont $blue ([System.Drawing.RectangleF]::new($x + 318, 414, 64, 70)) 'Center' 'Center' }
        }
    } finally { $flowTitleFont.Dispose(); $flowNoteFont.Dispose(); $arrowFont.Dispose() }
    Fill-RoundedRectangle $graphics $softBlue ([System.Drawing.RectangleF]::new(150, 666, 1300, 118)) 24
    $footerTitleFont = New-Object System.Drawing.Font $fontBold, 25, ([System.Drawing.FontStyle]::Bold)
    $footerTextFont = New-Object System.Drawing.Font $fontRegular, 19
    try {
        Draw-Text $graphics '/返回' $footerTitleFont $blue ([System.Drawing.RectangleF]::new(190, 692, 150, 48)) 'Center'
        Draw-Text $graphics '回到进入详情、目录或阅读前的命令页面，不丢失导航上下文' $footerTextFont $ink ([System.Drawing.RectangleF]::new(350, 692, 1010, 48)) 'Near' 'Center'
        Draw-Text $graphics '完整长目录 · 精确序号映射 · 上次进度继续' $footerTextFont $muted ([System.Drawing.RectangleF]::new(190, 742, 1170, 34)) 'Center'
    } finally { $footerTitleFont.Dispose(); $footerTextFont.Dispose() }
    Save-Poster $bitmap '07-precise-chapter-navigation.png'
} finally { $graphics.Dispose(); $bitmap.Dispose() }

# 08 - Safe subscription
$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = New-Graphics $bitmap
try {
    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.Point]::new(0, 0)), ([System.Drawing.Point]::new($width, 0)), ([System.Drawing.Color]::FromArgb(27, 56, 102)), ([System.Drawing.Color]::FromArgb(46, 94, 164))
    try { $graphics.FillRectangle($gradient, 0, 0, $width, $height) } finally { $gradient.Dispose() }
    Draw-Brand $graphics $white
    $titleFont = New-Object System.Drawing.Font $fontBold, 49, ([System.Drawing.FontStyle]::Bold)
    $subtitleFont = New-Object System.Drawing.Font $fontRegular, 22
    try {
        Draw-Text $graphics "VIP 章节`n安全单章订阅" $titleFont $white ([System.Drawing.RectangleF]::new(78, 170, 500, 148))
        Draw-Text $graphics '始终由起点官方页面完成购买' $subtitleFont $white ([System.Drawing.RectangleF]::new(82, 342, 480, 44))
    } finally { $titleFont.Dispose(); $subtitleFont.Dispose() }
    Draw-Chip $graphics '余额支付' 82 438 150 $brightBlue $white
    Draw-Chip $graphics '顶部提示' 248 438 150 $brightBlue $white
    Draw-Chip $graphics '/返回取消' 82 502 176 $brightBlue $white
    Draw-Chip $graphics '恢复原章节' 274 502 176 $brightBlue $white
    Fill-RoundedRectangle $graphics $white ([System.Drawing.RectangleF]::new(650, 110, 870, 680)) 30
    $safeTitleFont = New-Object System.Drawing.Font $fontBold, 28, ([System.Drawing.FontStyle]::Bold)
    $safeItemFont = New-Object System.Drawing.Font $fontBold, 22, ([System.Drawing.FontStyle]::Bold)
    $safeNoteFont = New-Object System.Drawing.Font $fontRegular, 18
    try {
        Draw-Text $graphics '订阅前安全检查' $safeTitleFont $ink ([System.Drawing.RectangleF]::new(710, 162, 750, 52)) 'Center'
        $checks = @(
            @('单章范围', '仅打开当前 VIP 章节的官方订阅入口', $blue),
            @('自动订阅', '检测到勾选时要求先取消', $orange),
            @('批量订阅', '发现多章范围时阻止继续确认', $cyan),
            @('人工确认', '核对价格后由用户点击官方确认', $green)
        )
        for ($index = 0; $index -lt $checks.Count; $index++) {
            $y = 242 + $index * 118
            Fill-RoundedRectangle $graphics $pale ([System.Drawing.RectangleF]::new(700, $y, 770, 94)) 18
            Fill-RoundedRectangle $graphics $checks[$index][2] ([System.Drawing.RectangleF]::new(724, $y + 19, 56, 56)) 16
            Draw-Text $graphics '✓' $safeItemFont $white ([System.Drawing.RectangleF]::new(724, $y + 19, 56, 56)) 'Center' 'Center'
            Draw-Text $graphics $checks[$index][0] $safeItemFont $ink ([System.Drawing.RectangleF]::new(810, $y + 12, 250, 38))
            Draw-Text $graphics $checks[$index][1] $safeNoteFont $muted ([System.Drawing.RectangleF]::new(812, $y + 52, 610, 30))
        }
        Draw-Text $graphics '程序不会代替你点击支付' $safeItemFont $blue ([System.Drawing.RectangleF]::new(700, 724, 770, 38)) 'Center'
    } finally { $safeTitleFont.Dispose(); $safeItemFont.Dispose(); $safeNoteFont.Dispose() }
    Save-Poster $bitmap '08-safe-subscription.png'
} finally { $graphics.Dispose(); $bitmap.Dispose() }

$navy.Dispose(); $blue.Dispose(); $brightBlue.Dispose(); $cyan.Dispose(); $white.Dispose(); $ink.Dispose(); $muted.Dispose(); $pale.Dispose(); $softBlue.Dispose(); $green.Dispose(); $orange.Dispose()
