$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$packageRoot = Join-Path $projectRoot 'deployment\windows-shell'
$modulePath = Join-Path $projectRoot 'deployment\windows-home-lockdown\TimeLockHomeLockdown.psm1'
$installPath = Join-Path $packageRoot 'Install-TimeLockShell.ps1'
$removePath = Join-Path $packageRoot 'Remove-TimeLockShell.ps1'
$fixtureId = [Guid]::NewGuid().ToString('N')
$registryRoot = "HKCU:\Software\TimeLockApp.Tests\Shell\$fixtureId"
$testRoot = Join-Path ([IO.Path]::GetTempPath()) "TimeLockApp.Tests\Shell\$fixtureId"
$validExe = Join-Path $projectRoot 'bin\Debug\net10.0-windows\TimeLockApp.exe'
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

    Invoke-Test 'shell validation mode makes no changes' {
        $statePath = Join-Path $testRoot 'validate-only-state'
        & $installPath -AppPath $validExe -StateDirectory $statePath -ValidateOnly -RegistryRoot $registryRoot
        Assert-True (-not (Test-Path -LiteralPath $statePath)) 'Validation mode created state files.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $registryRoot 'Winlogon'))) 'Validation mode changed Registry.'
    }

    Invoke-Test 'shell install writes quoted executable and backup' {
        $statePath = Join-Path $testRoot 'install-state'
        $keyPath = Join-Path $registryRoot 'Winlogon'
        New-Item -Path $keyPath -Force | Out-Null
        New-ItemProperty -Path $keyPath -Name 'Shell' -Value 'explorer.exe' -PropertyType String -Force | Out-Null

        & $installPath -AppPath $validExe -StateDirectory $statePath -RegistryRoot $registryRoot

        $shell = (Get-Item -LiteralPath $keyPath).GetValue('Shell')
        Assert-True ($shell -eq ('"{0}"' -f [IO.Path]::GetFullPath($validExe))) 'Shell was not set to the quoted application path.'
        $backup = Get-Content -LiteralPath (Join-Path $statePath 'backup.json') -Raw | ConvertFrom-Json
        Assert-True ($backup.Shell.Value -eq 'explorer.exe') 'Original Shell was not backed up.'
    }

    Invoke-Test 'duplicate backup is rejected without changing Shell' {
        $statePath = Join-Path $testRoot 'install-state'
        $keyPath = Join-Path $registryRoot 'Winlogon'
        $before = (Get-Item -LiteralPath $keyPath).GetValue('Shell')
        $threw = $false
        try { & $installPath -AppPath $validExe -StateDirectory $statePath -RegistryRoot $registryRoot }
        catch { $threw = $true }
        Assert-True $threw 'Duplicate backup must be rejected.'
        Assert-True ((Get-Item -LiteralPath $keyPath).GetValue('Shell') -eq $before) 'Duplicate install changed Shell.'
    }

    Invoke-Test 'SID mismatch prevents shell removal' {
        $statePath = Join-Path $testRoot 'install-state'
        $backupPath = Join-Path $statePath 'backup.json'
        $backup = Get-Content -LiteralPath $backupPath -Raw | ConvertFrom-Json
        $backup.CurrentUserSid = 'S-1-5-21-different-user'
        $backup | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $backupPath -Encoding UTF8
        $threw = $false
        try { & $removePath -StateDirectory $statePath }
        catch { $threw = $true }
        Assert-True $threw 'SID mismatch must be rejected.'
        Assert-True (Test-Path -LiteralPath $backupPath) 'SID mismatch deleted the backup.'
        $backup.CurrentUserSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        $backup | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $backupPath -Encoding UTF8
    }

    Invoke-Test 'shell removal restores the exact original value' {
        $statePath = Join-Path $testRoot 'install-state'
        $keyPath = Join-Path $registryRoot 'Winlogon'
        & $removePath -StateDirectory $statePath
        Assert-True ((Get-Item -LiteralPath $keyPath).GetValue('Shell') -eq 'explorer.exe') 'Original Shell was not restored.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $statePath 'backup.json'))) 'Successful removal retained backup.'
    }
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $registryRoot -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures -gt 0) { exit 1 }
exit 0
