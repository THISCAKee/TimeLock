$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
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

Invoke-Test 'main window is hidden from the taskbar' {
    $xaml = Get-Content -LiteralPath (Join-Path $projectRoot 'MainWindow.xaml') -Raw
    Assert-True ($xaml -match 'ShowInTaskbar="False"') 'MainWindow must set ShowInTaskbar to False.'
}

Invoke-Test 'usage window is hidden from the taskbar' {
    $xaml = Get-Content -LiteralPath (Join-Path $projectRoot 'UsageWindow.xaml') -Raw
    Assert-True ($xaml -match 'ShowInTaskbar="False"') 'UsageWindow must set ShowInTaskbar to False.'
}

Invoke-Test 'usage window cannot be minimized' {
    $xaml = Get-Content -LiteralPath (Join-Path $projectRoot 'UsageWindow.xaml') -Raw
    $code = Get-Content -LiteralPath (Join-Path $projectRoot 'UsageWindow.xaml.cs') -Raw
    Assert-True ($xaml -notmatch 'MinimizeButton_Click') 'UsageWindow must not expose a minimize button.'
    Assert-True ($code -notmatch 'MinimizeButton_Click') 'UsageWindow must not implement minimize behavior.'
}

if ($failures -gt 0) { exit 1 }
exit 0
