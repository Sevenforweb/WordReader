$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsDirectory = Join-Path $projectRoot '.tools'
$dotnetDirectory = Join-Path $toolsDirectory 'dotnet'
$dotnet = Join-Path $dotnetDirectory 'dotnet.exe'
$installer = Join-Path $toolsDirectory 'dotnet-install.ps1'
$runtimeDirectory = Join-Path $projectRoot 'ocr-runtime'
$modelDirectory = Join-Path $runtimeDirectory 'models\v6'

New-Item -ItemType Directory -Force -Path $toolsDirectory, $modelDirectory | Out-Null

if (-not (Test-Path $dotnet)) {
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
    & $installer -Channel '8.0' -Quality 'GA' -InstallDir $dotnetDirectory -NoPath
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& $dotnet publish (Join-Path $projectRoot 'ocr-helper\OcrHelper.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o $runtimeDirectory
if ($LASTEXITCODE -ne 0) { throw "OCR helper publish failed with exit code $LASTEXITCODE." }

$models = @(
    @{
        Name = 'PP-OCRv6_det_small.onnx'
        Url = 'https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.2/onnx/PP-OCRv6/det/PP-OCRv6_det_small.onnx'
        Sha256 = '090F04ABCD9D9A7498BC4EBF677E4CB9BDCE1FE4197DDB7E529F1EF44E1FF94F'
    },
    @{
        Name = 'PP-OCRv6_rec_small.onnx'
        Url = 'https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.2/onnx/PP-OCRv6/rec/PP-OCRv6_rec_small.onnx'
        Sha256 = '6F327246B50388F3C176AE304BD95767EA6DC0C9AE92153EF8CBE210B3C14884'
    },
    @{
        Name = 'ppocrv6_small_dict.txt'
        Url = 'https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.2/paddle/PP-OCRv6/rec/PP-OCRv6_rec_small/ppocrv6_dict.txt'
        Sha256 = 'B5F2BFE2BDD9448429E3E82B51C789775D9B42F2403D082B00662EB77E401C5D'
    }
)

foreach ($model in $models) {
    $target = Join-Path $modelDirectory $model.Name
    $valid = Test-Path $target
    if ($valid) { $valid = (Get-FileHash -Algorithm SHA256 -LiteralPath $target).Hash -eq $model.Sha256 }
    if (-not $valid) { Invoke-WebRequest -Uri $model.Url -OutFile $target }
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $target).Hash
    if ($actual -ne $model.Sha256) { throw "OCR model hash mismatch: $($model.Name)" }
}

Write-Host "Built PP-OCRv6 runtime: $runtimeDirectory"
