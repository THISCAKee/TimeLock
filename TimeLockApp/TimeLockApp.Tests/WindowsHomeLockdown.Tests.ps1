$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$packageRoot = Join-Path $projectRoot 'deployment\windows-home-lockdown'
$modulePath = Join-Path $packageRoot 'TimeLockHomeLockdown.psm1'
$installPath = Join-Path $packageRoot 'Install-TimeLockHomeLockdown.ps1'
$removePath = Join-Path $packageRoot 'Remove-TimeLockHomeLockdown.ps1'
$fixtureId = [Guid]::NewGuid().ToString('N')
$registryRoot = "HKCU:\Software\TimeLockApp.Tests\$fixtureId"
$testRoot = Join-Path ([IO.Path]::GetTempPath()) "TimeLockApp.Tests\Lockdown\$fixtureId"
$failures = 0

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Invoke-Test {
    param([string]$Name, [scriptblock]$Test)
    try {
        & $Test
        Write-Output "PASS: $Name"
    }
    catch {
        $script:failures++
        Write-Error "FAIL: $Name`n$($_.Exception.Message)" -ErrorAction Continue
    }
}

try {
    Import-Module $modulePath -Force
    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

    Invoke-Test 'executable paths are normalized and validated' {
        $validExe = Join-Path $projectRoot 'bin\Debug\net10.0-windows\TimeLockApp.exe'
        $resolved = Assert-NormalizedExecutablePath -Path $validExe
        Assert-True ($resolved -eq [IO.Path]::GetFullPath($validExe)) 'Valid executable was not normalized.'

        $threw = $false
        try { Assert-NormalizedExecutablePath -Path (Join-Path $testRoot 'missing.exe') | Out-Null }
        catch { $threw = $true }
        Assert-True $threw 'Missing executable must be rejected.'
    }

    Invoke-Test 'registry snapshots restore missing and existing values' {
        $keyPath = Join-Path $registryRoot 'Values'
        New-Item -Path $keyPath -Force | Out-Null

        $missing = Get-RegistryValueSnapshot -KeyPath $keyPath -Name 'MissingValue'
        New-ItemProperty -Path $keyPath -Name 'MissingValue' -Value 1 -PropertyType DWord -Force | Out-Null
        Restore-RegistryValueSnapshot -KeyPath $keyPath -Name 'MissingValue' -Snapshot $missing
        $names = (Get-Item -LiteralPath $keyPath).GetValueNames()
        Assert-True (-not ($names -contains 'MissingValue')) 'Originally missing value was not removed.'

        New-ItemProperty -Path $keyPath -Name 'ExistingValue' -Value 'before' -PropertyType String -Force | Out-Null
        $existing = Get-RegistryValueSnapshot -KeyPath $keyPath -Name 'ExistingValue'
        Set-ItemProperty -Path $keyPath -Name 'ExistingValue' -Value 'after'
        Restore-RegistryValueSnapshot -KeyPath $keyPath -Name 'ExistingValue' -Snapshot $existing
        $item = Get-Item -LiteralPath $keyPath
        Assert-True ($item.GetValue('ExistingValue') -eq 'before') 'String value was not restored.'
        Assert-True ($item.GetValueKind('ExistingValue').ToString() -eq 'String') 'Registry kind was not restored.'
    }

    Invoke-Test 'process matching uses the full executable path' {
        $actualPath = (Get-Process -Id $PID).Path
        Assert-True (Test-ProcessExecutablePath -ProcessId $PID -ExpectedPath $actualPath) 'Current process path must match.'
        Assert-True (-not (Test-ProcessExecutablePath -ProcessId $PID -ExpectedPath (Join-Path $testRoot 'powershell.exe'))) 'Different full path must not match.'
    }

    Invoke-Test 'atomic backup JSON preserves required fields' {
        $backupPath = Join-Path $testRoot 'backup.json'
        $backup = [ordered]@{
            CurrentUserSid = 'S-1-5-21-test'
            AppPath = 'C:\Apps\TimeLockApp.exe'
            DisableTaskMgr = @{ Exists = $true; Kind = 'DWord'; Value = 0 }
            Run = @{ Exists = $false; Kind = $null; Value = $null }
        }
        Write-JsonAtomically -Path $backupPath -Value $backup
        $loaded = Get-Content -LiteralPath $backupPath -Raw | ConvertFrom-Json
        Assert-True ($loaded.CurrentUserSid -eq 'S-1-5-21-test') 'SID did not round-trip.'
        Assert-True ($loaded.DisableTaskMgr.Value -eq 0) 'DWORD zero did not round-trip.'
        Assert-True (-not $loaded.Run.Exists) 'Missing Run value did not round-trip.'
    }

    Invoke-Test 'install validation mode makes no changes' {
        $statePath = Join-Path $testRoot 'validate-only-state'
        $validExe = Join-Path $projectRoot 'bin\Debug\net10.0-windows\TimeLockApp.exe'
        & $installPath -AppPath $validExe -StateDirectory $statePath -ValidateOnly -RegistryRoot $registryRoot
        Assert-True (-not (Test-Path -LiteralPath $statePath)) 'Validation mode created state files.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $registryRoot 'Policies\System'))) 'Validation mode changed policy Registry.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $registryRoot 'Run'))) 'Validation mode changed Run Registry.'
    }

    Invoke-Test 'removal restores exact fixture Registry state' {
        $removeRegistryRoot = Join-Path $registryRoot 'Removal'
        $policyKey = Join-Path $removeRegistryRoot 'Policies\System'
        $runKey = Join-Path $removeRegistryRoot 'Run'
        $removeState = Join-Path $testRoot 'remove-state'
        New-Item -Path $policyKey -Force | Out-Null
        New-ItemProperty -Path $policyKey -Name 'DisableTaskMgr' -Value 0 -PropertyType DWord -Force | Out-Null

        $policySnapshot = Get-RegistryValueSnapshot -KeyPath $policyKey -Name 'DisableTaskMgr'
        $runSnapshot = Get-RegistryValueSnapshot -KeyPath $runKey -Name 'TimeLockWatchdog'
        $backup = [ordered]@{
            Version = 1
            CurrentUserSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
            RegistryRoot = $removeRegistryRoot
            AppPath = 'C:\Apps\TimeLockApp.exe'
            StateDirectory = $removeState
            DisableTaskMgr = $policySnapshot
            Run = $runSnapshot
        }
        Write-JsonAtomically -Path (Join-Path $removeState 'backup.json') -Value $backup

        Set-ItemProperty -Path $policyKey -Name 'DisableTaskMgr' -Value 1
        New-Item -Path $runKey -Force | Out-Null
        New-ItemProperty -Path $runKey -Name 'TimeLockWatchdog' -Value 'changed' -PropertyType String -Force | Out-Null

        & $removePath -StateDirectory $removeState

        $restored = Get-Item -LiteralPath $policyKey
        Assert-True ($restored.GetValue('DisableTaskMgr') -eq 0) 'DWORD zero was not restored by removal.'
        Assert-True (-not ((Get-Item -LiteralPath $runKey).GetValueNames() -contains 'TimeLockWatchdog')) 'Originally missing Run value was not removed.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $removeState 'backup.json'))) 'Successful removal retained backup.'
    }
}
finally {
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    $allowedRoot = [IO.Path]::GetFullPath(
        (Join-Path ([IO.Path]::GetTempPath()) 'TimeLockApp.Tests\Lockdown'))

    if ($resolvedTestRoot.StartsWith(
        $allowedRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    Remove-Item -LiteralPath $registryRoot -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures -gt 0) { exit 1 }
exit 0
