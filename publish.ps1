<#
.SYNOPSIS
    Builds Screentime Tracker as a single self-contained ScreentimeTracker.exe and installs it to
    %LOCALAPPDATA%\Programs\ScreentimeTracker, replacing and relaunching a running copy.

.DESCRIPTION
    The exe needs no installer and no separate .NET install. Tracked data lives in
    %LOCALAPPDATA%\ScreentimeTracker and is never touched. If the build fails, a running copy is left
    as it is. A running copy is asked to quit cleanly, so it flushes tracked time, before it is replaced.
#>
[CmdletBinding()]
param(
    # Install without starting the app afterwards.
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'src\ScreentimeTracker.App\ScreentimeTracker.App.csproj'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\ScreentimeTracker'
$stagingDir = Join-Path ([System.IO.Path]::GetTempPath()) "ScreentimeTracker-publish-$([guid]::NewGuid().ToString('N'))"
$exeName = 'ScreentimeTracker.exe'

try {
    Write-Host 'Building...'
    dotnet publish $project `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        --output $stagingDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

    # 'ScreentimeTracker.App' is the name copies had before the exe was renamed; they would otherwise keep
    # tracking into the same database alongside the new one.
    $running = @(Get-Process -Name 'ScreentimeTracker', 'ScreentimeTracker.App' -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) {
        Write-Host 'Asking the running copy to quit...'
        $quit = $null
        if ([System.Threading.EventWaitHandle]::TryOpenExisting('Local\ScreentimeTracker.Quit', [ref]$quit)) {
            $quit.Set() | Out-Null
            $quit.Dispose()
        }
        foreach ($process in $running) {
            if (-not $process.WaitForExit(10000)) {
                Write-Warning "Copy $($process.Id) did not quit in time; stopping it. Up to a few seconds of tracking may be lost."
                Stop-Process -Id $process.Id -Force
                $process.WaitForExit()
            }
        }
    }

    New-Item -ItemType Directory -Force -Path $installDir | Out-Null
    Copy-Item -Path (Join-Path $stagingDir '*') -Destination $installDir -Recurse -Force
}
finally {
    if (Test-Path $stagingDir) { Remove-Item -Recurse -Force $stagingDir }
}

$exe = Join-Path $installDir $exeName
Write-Host "Installed $exe"

if (-not $NoLaunch) {
    Start-Process -FilePath $exe
    Write-Host 'Started. Screentime Tracker is in the tray.'
}
