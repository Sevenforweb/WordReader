$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$packagesDir = Join-Path $projectRoot 'packages'
$outputDir = Join-Path $projectRoot 'bin'
$packageVersion = '1.0.4078.44'
$packageName = "microsoft.web.webview2.$packageVersion"
$packageArchive = Join-Path $packagesDir "$packageName.nupkg"
$packageDir = Join-Path $packagesDir $packageName

New-Item -ItemType Directory -Force -Path $packagesDir, $outputDir | Out-Null

if (-not (Test-Path $packageDir)) {
    Write-Host "Downloading Microsoft.Web.WebView2 $packageVersion..."
    Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$packageVersion/$packageName.nupkg" -OutFile $packageArchive
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($packageArchive, $packageDir)
}

$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$coreDll = Join-Path $packageDir 'lib\net462\Microsoft.Web.WebView2.Core.dll'
$winFormsDll = Join-Path $packageDir 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'
$loaderDll = Join-Path $packageDir 'runtimes\win-x64\native\WebView2Loader.dll'
$exePath = Join-Path $outputDir 'QuietReader.exe'
$windowsMetadata = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\UnionMetadata' -Recurse -Filter Windows.winmd -ErrorAction SilentlyContinue |
    Where-Object { $_.DirectoryName -notmatch '\\Facade$' } |
    Sort-Object Length -Descending | Select-Object -First 1 -ExpandProperty FullName
$windowsRuntime = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.WindowsRuntime.dll'
$facadeDirectory = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The .NET Framework C# compiler was not found. Use the prebuilt portable release, or install the .NET Framework 4.8 developer pack.'
}

if (-not $windowsMetadata -or -not (Test-Path $windowsRuntime) -or -not (Test-Path $facadeDirectory)) {
    throw 'Source compilation requires the Windows 10/11 SDK and .NET Framework 4.8 developer pack. These large developer tools are not installed automatically. The prebuilt portable release does not require them.'
}

$facadeReferences = Get-ChildItem $facadeDirectory -Filter *.dll | ForEach-Object { '/reference:' + $_.FullName }

& $compiler /nologo /target:winexe /platform:x64 /out:$exePath /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll /reference:$windowsRuntime /reference:$windowsMetadata $facadeReferences /reference:$coreDll /reference:$winFormsDll (Join-Path $projectRoot 'QuietReader.cs')
if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE." }

Copy-Item $coreDll $outputDir -Force
Copy-Item $winFormsDll $outputDir -Force
Copy-Item $loaderDll $outputDir -Force
if (-not (Test-Path (Join-Path $projectRoot 'ocr-runtime\OcrHelper.exe'))) {
    Write-Warning 'PP-OCRv6 runtime is missing. Run .\build-ocr.ps1 to enable high-accuracy OCR; Windows OCR will remain available as a fallback.'
}
Write-Host "Built: $exePath"
