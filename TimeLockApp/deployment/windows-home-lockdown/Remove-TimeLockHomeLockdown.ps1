[CmdletBinding()]
param(
    [string]$StateDirectory = "$env:LOCALAPPDATA\TimeLockApp\Lockdown"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $packageRoot 'TimeLockHomeLockdown.psm1') -Force

$resolvedStateDirectory = [IO.Path]::GetFullPath(
    [Environment]::ExpandEnvironmentVariables($StateDirectory))
$backupPath = Join-Path $resolvedStateDirectory 'backup.json'

if (-not (Test-Path -LiteralPath $backupPath -PathType Leaf)) {
    throw "Lockdown backup does not exist: $backupPath"
}

$backup = Get-Content -LiteralPath $backupPath -Raw | ConvertFrom-Json
$currentSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value

if ($backup.CurrentUserSid -ne $currentSid) {
    throw 'The backup belongs to a different Windows user. Run removal in the configured Standard user context.'
}

$policyKey = Join-Path $backup.RegistryRoot 'Policies\System'
$runKey = Join-Path $backup.RegistryRoot 'Run'
$pidPath = Join-Path $resolvedStateDirectory 'watchdog.pid'
$watchdogPath = Join-Path $resolvedStateDirectory 'TimeLockWatchdog.ps1'
$watchdogPid = 0
$watchdogIsRunning = $false

if (Test-Path -LiteralPath $pidPath) {
    $pidText = Get-Content -LiteralPath $pidPath -Raw

    if ([int]::TryParse($pidText, [ref]$watchdogPid)) {
        $process = Get-CimInstance Win32_Process -Filter "ProcessId = $watchdogPid" -ErrorAction SilentlyContinue

        if ($null -ne $process) {
            if ($null -eq $process.CommandLine -or
                $process.CommandLine.IndexOf(
                    $watchdogPath,
                    [StringComparison]::OrdinalIgnoreCase) -lt 0) {
                throw 'Recorded PID belongs to a different process. Registry was not changed.'
            }

            $watchdogIsRunning = $true
        }
    }
}

Restore-RegistryValueSnapshot -KeyPath $policyKey -Name 'DisableTaskMgr' -Snapshot $backup.DisableTaskMgr
Restore-RegistryValueSnapshot -KeyPath $runKey -Name 'TimeLockWatchdog' -Snapshot $backup.Run

if ($watchdogIsRunning) {
    Stop-Process -Id $watchdogPid -Force
}

Remove-Item -LiteralPath $pidPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $watchdogPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $resolvedStateDirectory 'watchdog.log') -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $backupPath -Force

Write-Output 'Windows Home lockdown removed and prior Registry values restored.'
