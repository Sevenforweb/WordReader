$ErrorActionPreference = 'Stop'

try {
    $projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $exePath = Join-Path $projectRoot 'bin\QuietReader.exe'
    $buildScript = Join-Path $projectRoot 'build.ps1'
    $buildInputs = @(
        (Join-Path $projectRoot 'QuietReader.cs'),
        $buildScript
    )
    $needsBuild = -not (Test-Path -LiteralPath $exePath)

    if (-not $needsBuild) {
        $executableTime = (Get-Item -LiteralPath $exePath).LastWriteTimeUtc
        $needsBuild = $buildInputs | Where-Object {
            (Test-Path -LiteralPath $_) -and (Get-Item -LiteralPath $_).LastWriteTimeUtc -gt $executableTime
        } | Select-Object -First 1
        $needsBuild = [bool]$needsBuild
    }

    if ($needsBuild) {
        & $buildScript
        if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
    }

    Start-Process -FilePath $exePath -WorkingDirectory $projectRoot
}
catch {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show(
        "Quiet Reader could not start.`r`n`r`n$($_.Exception.Message)",
        'Quiet Reader',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error
    ) | Out-Null
    Write-Error $_
    exit 1
}
