[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AppPath,
    [Parameter(Mandatory)][string]$StateDirectory,
    [string]$PidPath,
    [string]$LogPath,
    [ValidateRange(1, 60)][int]$PollSeconds = 2
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedAppPath = [IO.Path]::GetFullPath(
    [Environment]::ExpandEnvironmentVariables($AppPath))
$resolvedStateDirectory = [IO.Path]::GetFullPath(
    [Environment]::ExpandEnvironmentVariables($StateDirectory))

if (-not (Test-Path -LiteralPath $resolvedAppPath -PathType Leaf)) {
    throw "TimeLockApp executable does not exist: $resolvedAppPath"
}

if ([string]::IsNullOrWhiteSpace($PidPath)) {
    $PidPath = Join-Path $resolvedStateDirectory 'watchdog.pid'
}
if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $resolvedStateDirectory 'watchdog.log'
}

$PidPath = [IO.Path]::GetFullPath($PidPath)
$LogPath = [IO.Path]::GetFullPath($LogPath)
New-Item -ItemType Directory -Path $resolvedStateDirectory -Force | Out-Null

if (Test-Path -LiteralPath $PidPath) {
    $existingPidText = Get-Content -LiteralPath $PidPath -Raw -ErrorAction SilentlyContinue
    $existingPid = 0

    if ([int]::TryParse($existingPidText, [ref]$existingPid)) {
        $existing = Get-CimInstance Win32_Process -Filter "ProcessId = $existingPid" -ErrorAction SilentlyContinue
        if ($null -ne $existing -and
            $null -ne $existing.CommandLine -and
            $existing.CommandLine.IndexOf(
                $MyInvocation.MyCommand.Path,
                [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            exit 0
        }
    }
}

$temporaryPidPath = "$PidPath.tmp.$([Guid]::NewGuid().ToString('N'))"
try {
    Set-Content -LiteralPath $temporaryPidPath -Value $PID -Encoding ASCII
    Move-Item -LiteralPath $temporaryPidPath -Destination $PidPath -Force
}
finally {
    Remove-Item -LiteralPath $temporaryPidPath -Force -ErrorAction SilentlyContinue
}

while ($true) {
    try {
        $running = Get-CimInstance Win32_Process |
            Where-Object {
                $_.ExecutablePath -and
                ([IO.Path]::GetFullPath($_.ExecutablePath).Equals(
                    $resolvedAppPath,
                    [StringComparison]::OrdinalIgnoreCase))
            } |
            Select-Object -First 1

        if ($null -eq $running) {
            Start-Process -FilePath $resolvedAppPath
        }
    }
    catch {
        $entry = '{0:u} {1}' -f [DateTime]::Now, $_.Exception.Message
        Add-Content -LiteralPath $LogPath -Value $entry -Encoding UTF8
    }

    Start-Sleep -Seconds $PollSeconds
}
