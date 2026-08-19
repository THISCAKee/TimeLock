[CmdletBinding()]
param(
    [string]$StateDirectory = "$env:LOCALAPPDATA\TimeLockApp\Shell"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$modulePath = Join-Path (Split-Path -Parent $packageRoot) 'windows-home-lockdown\TimeLockHomeLockdown.psm1'
Import-Module $modulePath -Force

$resolvedStateDirectory = [IO.Path]::GetFullPath(
    [Environment]::ExpandEnvironmentVariables($StateDirectory))
$backupPath = Join-Path $resolvedStateDirectory 'backup.json'

if (-not (Test-Path -LiteralPath $backupPath -PathType Leaf)) {
    throw "Shell backup does not exist: $backupPath"
}

$backup = Get-Content -LiteralPath $backupPath -Raw | ConvertFrom-Json
$currentSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value

if ($backup.CurrentUserSid -ne $currentSid) {
    throw 'The shell backup belongs to a different Windows user.'
}

$shellKey = Join-Path $backup.RegistryRoot 'Winlogon'
Restore-RegistryValueSnapshot -KeyPath $shellKey -Name 'Shell' -Snapshot $backup.Shell
Remove-Item -LiteralPath $backupPath -Force

Write-Output 'Windows Shell mode removed and the prior Shell value restored.'
