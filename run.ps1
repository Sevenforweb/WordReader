$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$exePath = Join-Path $projectRoot 'bin\QuietReader.exe'
if (-not (Test-Path $exePath)) { & (Join-Path $projectRoot 'build.ps1') }
Start-Process -FilePath $exePath
