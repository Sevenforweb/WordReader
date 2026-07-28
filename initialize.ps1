param(
    [switch]$SkipOcr
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsDirectory = Join-Path $projectRoot '.tools'
$webView2Installer = Join-Path $toolsDirectory 'MicrosoftEdgeWebview2Setup.exe'
$webView2BootstrapperUrl = 'https://go.microsoft.com/fwlink/p/?LinkId=2124703'
$webView2ClientId = '{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'
$ocrRuntimeDirectory = Join-Path $projectRoot 'ocr-runtime'
$ocrFiles = @(
    (Join-Path $ocrRuntimeDirectory 'OcrHelper.exe'),
    (Join-Path $ocrRuntimeDirectory 'models\v6\PP-OCRv6_det_small.onnx'),
    (Join-Path $ocrRuntimeDirectory 'models\v6\PP-OCRv6_rec_small.onnx'),
    (Join-Path $ocrRuntimeDirectory 'models\v6\ppocrv6_small_dict.txt')
)

function Write-CheckResult([string]$message) {
    Write-Host "[WordReader] $message"
}

function Get-WebView2RuntimeVersion {
    $registryPaths = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\$webView2ClientId",
        "HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\$webView2ClientId",
        "HKCU:\Software\Microsoft\EdgeUpdate\Clients\$webView2ClientId"
    )

    foreach ($registryPath in $registryPaths) {
        $version = (Get-ItemProperty -Path $registryPath -ErrorAction SilentlyContinue).pv
        if ($version -and $version -ne '0.0.0.0') { return [string]$version }
    }

    $applicationRoots = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft\EdgeWebView\Application'),
        (Join-Path $env:LOCALAPPDATA 'Microsoft\EdgeWebView\Application')
    )
    foreach ($applicationRoot in $applicationRoots) {
        if (-not (Test-Path -LiteralPath $applicationRoot)) { continue }
        $versionDirectory = Get-ChildItem -LiteralPath $applicationRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^\d+(\.\d+){3}$' } |
            Sort-Object { [version]$_.Name } -Descending |
            Select-Object -First 1
        if ($versionDirectory) { return $versionDirectory.Name }
    }

    return $null
}

function Ensure-WebView2Runtime {
    $version = Get-WebView2RuntimeVersion
    if ($version) {
        Write-CheckResult "WebView2 Runtime detected: $version. Skipping installation."
        return
    }

    Write-CheckResult 'WebView2 Runtime is missing. Downloading the official Microsoft Evergreen Bootstrapper...'
    New-Item -ItemType Directory -Force -Path $toolsDirectory | Out-Null
    Invoke-WebRequest -UseBasicParsing -Uri $webView2BootstrapperUrl -OutFile $webView2Installer

    $signature = Get-AuthenticodeSignature -LiteralPath $webView2Installer
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
        -not $signature.SignerCertificate -or
        $signature.SignerCertificate.Subject -notmatch 'Microsoft') {
        throw 'The downloaded WebView2 installer does not have a valid Microsoft signature.'
    }

    Write-CheckResult 'Installing WebView2 Runtime...'
    $process = Start-Process -FilePath $webView2Installer -ArgumentList @('/silent', '/install') -Wait -PassThru
    if ($process.ExitCode -notin @(0, 3010)) {
        throw "WebView2 Runtime installation failed with exit code $($process.ExitCode)."
    }

    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        Start-Sleep -Seconds 2
        $version = Get-WebView2RuntimeVersion
        if ($version) {
            Write-CheckResult "WebView2 Runtime installed: $version."
            return
        }
    }

    throw 'WebView2 Runtime installation finished, but the runtime could not be detected. Restart Windows and try again.'
}

function Ensure-OcrRuntime {
    $missingFiles = @($ocrFiles | Where-Object { -not (Test-Path -LiteralPath $_) })
    if ($missingFiles.Count -eq 0) {
        Write-CheckResult 'PP-OCRv6 runtime detected. Skipping OCR setup.'
        return
    }

    if ($SkipOcr) {
        Write-Warning 'PP-OCRv6 runtime is incomplete. OCR setup was skipped; Windows OCR will be used as a fallback.'
        return
    }

    $buildOcrScript = Join-Path $projectRoot 'build-ocr.ps1'
    $ocrProject = Join-Path $projectRoot 'ocr-helper\OcrHelper.csproj'
    if (-not (Test-Path -LiteralPath $buildOcrScript) -or -not (Test-Path -LiteralPath $ocrProject)) {
        Write-Warning 'PP-OCRv6 runtime is incomplete and source build files are unavailable. Windows OCR will be used as a fallback.'
        return
    }

    Write-CheckResult 'PP-OCRv6 runtime is incomplete. Starting one-time local OCR setup...'
    try {
        & $buildOcrScript
        $missingFiles = @($ocrFiles | Where-Object { -not (Test-Path -LiteralPath $_) })
        if ($missingFiles.Count -gt 0) {
            throw 'OCR setup completed without all required files.'
        }
        Write-CheckResult 'PP-OCRv6 runtime setup completed.'
    }
    catch {
        Write-Warning "PP-OCRv6 setup failed: $($_.Exception.Message) Windows OCR will remain available as a fallback."
    }
}

Write-CheckResult 'Checking required runtime components. Node.js and npm are not required.'
Ensure-WebView2Runtime
Ensure-OcrRuntime
Write-CheckResult 'Environment check completed.'
