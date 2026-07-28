$ErrorActionPreference = 'Stop'

try {
    $projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $exePath = Join-Path $projectRoot 'bin\QuietReader.exe'
    $buildScript = Join-Path $projectRoot 'build.ps1'
    $initializeScript = Join-Path $projectRoot 'initialize.ps1'

    $existingProcess = Get-Process -Name 'QuietReader' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($existingProcess) {
        if ($existingProcess.MainWindowHandle -ne [IntPtr]::Zero) {
            if (-not ('WordReaderLauncherNativeMethods' -as [type])) {
                Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class WordReaderLauncherNativeMethods
{
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
}
'@
            }
            [WordReaderLauncherNativeMethods]::ShowWindowAsync($existingProcess.MainWindowHandle, 9) | Out-Null
            [WordReaderLauncherNativeMethods]::SetForegroundWindow($existingProcess.MainWindowHandle) | Out-Null
        }
        Write-Host '[WordReader] An existing instance is already running. Skipping initialization and build.'
        return
    }

    if (Test-Path -LiteralPath $initializeScript) {
        & $initializeScript
    }

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
        "WordReader could not start.`r`n`r`n$($_.Exception.Message)",
        'WordReader',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error
    ) | Out-Null
    Write-Error $_
    exit 1
}
