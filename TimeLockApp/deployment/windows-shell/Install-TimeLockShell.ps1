[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AppPath,
    [string]$StateDirectory = "$env:LOCALAPPDATA\TimeLockApp\Shell",
    [switch]$ValidateOnly,
    [string]$RegistryRoot = 'HKCU:\Software\Microsoft\Windows NT\CurrentVersion'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$modulePath = Join-Path (Split-Path -Parent $packageRoot) 'windows-home-lockdown\TimeLockHomeLockdown.psm1'
Import-Module $modulePath -Force

$resolvedAppPath = Assert-NormalizedExecutablePath -Path $AppPath
$resolvedStateDirectory = [IO.Path]::GetFullPath(
    [Environment]::ExpandEnvironmentVariables($StateDirectory))

if ($ValidateOnly) {
    Write-Output "Validation successful for $resolvedAppPath"
    return
}

$shellKey = Join-Path $RegistryRoot 'Winlogon'
$backupPath = Join-Path $resolvedStateDirectory 'backup.json'

if (Test-Path -LiteralPath $backupPath -PathType Leaf) {
    throw "An unresolved shell backup already exists: $backupPath"
}

$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$shellSnapshot = Get-RegistryValueSnapshot -KeyPath $shellKey -Name 'Shell'
$backup = [ordered]@{
    Version = 1
    CurrentUserSid = $currentIdentity.User.Value
    RegistryRoot = $RegistryRoot
    AppPath = $resolvedAppPath
    StateDirectory = $resolvedStateDirectory
    Shell = $shellSnapshot
}

Write-JsonAtomically -Path $backupPath -Value $backup

try {
    New-Item -Path $shellKey -Force | Out-Null
    New-ItemProperty -Path $shellKey -Name 'Shell' -Value ('"{0}"' -f $resolvedAppPath) -PropertyType String -Force | Out-Null
    Write-Output "Windows Shell mode installed for $($currentIdentity.User.Value). Sign out and back in to apply it."
}
catch {
    Restore-RegistryValueSnapshot -KeyPath $shellKey -Name 'Shell' -Snapshot $shellSnapshot
    Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
    throw
}
