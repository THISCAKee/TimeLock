[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AppPath,
    [string]$StateDirectory = "$env:LOCALAPPDATA\TimeLockApp\Lockdown",
    [switch]$ValidateOnly,
    [string]$RegistryRoot = 'HKCU:\Software\Microsoft\Windows\CurrentVersion'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $packageRoot 'TimeLockHomeLockdown.psm1') -Force

$resolvedAppPath = Assert-NormalizedExecutablePath -Path $AppPath
$watchdogSource = Join-Path $packageRoot 'TimeLockWatchdog.ps1'

if (-not (Test-Path -LiteralPath $watchdogSource -PathType Leaf)) {
    throw "Watchdog script does not exist: $watchdogSource"
}

$resolvedStateDirectory = [IO.Path]::GetFullPath(
    [Environment]::ExpandEnvironmentVariables($StateDirectory))
$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
$isAdministrator = $currentPrincipal.IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if ($isAdministrator -and -not $ValidateOnly) {
    throw 'Run setup while signed in as the dedicated Standard user, not an Administrator.'
}

if ($ValidateOnly) {
    Write-Output "Validation successful for $resolvedAppPath"
    return
}

$policyKey = Join-Path $RegistryRoot 'Policies\System'
$runKey = Join-Path $RegistryRoot 'Run'
$backupPath = Join-Path $resolvedStateDirectory 'backup.json'
$watchdogDestination = Join-Path $resolvedStateDirectory 'TimeLockWatchdog.ps1'
$pidPath = Join-Path $resolvedStateDirectory 'watchdog.pid'
$logPath = Join-Path $resolvedStateDirectory 'watchdog.log'

if (Test-Path -LiteralPath $backupPath) {
    throw "An unresolved lockdown backup already exists: $backupPath"
}

New-Item -ItemType Directory -Path $resolvedStateDirectory -Force | Out-Null

$disableTaskManagerSnapshot = Get-RegistryValueSnapshot `
    -KeyPath $policyKey `
    -Name 'DisableTaskMgr'
$runSnapshot = Get-RegistryValueSnapshot `
    -KeyPath $runKey `
    -Name 'TimeLockWatchdog'

$backup = [ordered]@{
    Version = 1
    CurrentUserSid = $currentIdentity.User.Value
    RegistryRoot = $RegistryRoot
    AppPath = $resolvedAppPath
    StateDirectory = $resolvedStateDirectory
    DisableTaskMgr = $disableTaskManagerSnapshot
    Run = $runSnapshot
}

Write-JsonAtomically -Path $backupPath -Value $backup

try {
    Copy-Item -LiteralPath $watchdogSource -Destination $watchdogDestination -Force
    New-Item -Path $policyKey -Force | Out-Null
    New-Item -Path $runKey -Force | Out-Null
    New-ItemProperty -Path $policyKey -Name 'DisableTaskMgr' -Value 1 -PropertyType DWord -Force | Out-Null

    $powerShellPath = Join-Path $PSHOME 'powershell.exe'
    $watchdogCommand = '"{0}" -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "{1}" -AppPath "{2}" -StateDirectory "{3}"' -f `
        $powerShellPath,
        $watchdogDestination,
        $resolvedAppPath,
        $resolvedStateDirectory
    New-ItemProperty -Path $runKey -Name 'TimeLockWatchdog' -Value $watchdogCommand -PropertyType String -Force | Out-Null

    $watchdogArguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -AppPath "{1}" -StateDirectory "{2}" -PidPath "{3}" -LogPath "{4}"' -f `
        $watchdogDestination,
        $resolvedAppPath,
        $resolvedStateDirectory,
        $pidPath,
        $logPath

    Start-Process `
        -FilePath $powerShellPath `
        -WindowStyle Hidden `
        -ArgumentList $watchdogArguments

    Write-Output 'Windows Home lockdown installed. Sign out and back in to enforce Task Manager policy consistently.'
}
catch {
    Restore-RegistryValueSnapshot -KeyPath $policyKey -Name 'DisableTaskMgr' -Snapshot $disableTaskManagerSnapshot
    Restore-RegistryValueSnapshot -KeyPath $runKey -Name 'TimeLockWatchdog' -Snapshot $runSnapshot
    throw
}
