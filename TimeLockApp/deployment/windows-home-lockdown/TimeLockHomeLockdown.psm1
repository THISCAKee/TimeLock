Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-NormalizedExecutablePath {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    $expanded = [Environment]::ExpandEnvironmentVariables($Path)
    $resolved = [IO.Path]::GetFullPath($expanded)

    if ([IO.Path]::GetExtension($resolved) -ine '.exe') {
        throw "Executable path must end with .exe: $resolved"
    }

    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "Executable does not exist: $resolved"
    }

    return $resolved
}

function Get-RegistryValueSnapshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$KeyPath,
        [Parameter(Mandatory)][string]$Name
    )

    if (-not (Test-Path -LiteralPath $KeyPath)) {
        return [pscustomobject]@{ Exists = $false; Kind = $null; Value = $null }
    }

    $key = Get-Item -LiteralPath $KeyPath

    if (-not ($key.GetValueNames() -contains $Name)) {
        return [pscustomobject]@{ Exists = $false; Kind = $null; Value = $null }
    }

    return [pscustomobject]@{
        Exists = $true
        Kind = $key.GetValueKind($Name).ToString()
        Value = $key.GetValue(
            $Name,
            $null,
            [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
    }
}

function Restore-RegistryValueSnapshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$KeyPath,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)]$Snapshot
    )

    if (-not [bool]$Snapshot.Exists) {
        if (Test-Path -LiteralPath $KeyPath) {
            Remove-ItemProperty -LiteralPath $KeyPath -Name $Name -ErrorAction SilentlyContinue
        }
        return
    }

    New-Item -Path $KeyPath -Force | Out-Null
    New-ItemProperty `
        -Path $KeyPath `
        -Name $Name `
        -Value $Snapshot.Value `
        -PropertyType ([string]$Snapshot.Kind) `
        -Force | Out-Null
}

function Write-JsonAtomically {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)]$Value
    )

    $resolved = [IO.Path]::GetFullPath($Path)
    $directory = Split-Path -Parent $resolved
    New-Item -ItemType Directory -Path $directory -Force | Out-Null

    $temporaryPath = "$resolved.tmp.$([Guid]::NewGuid().ToString('N'))"

    try {
        $Value |
            ConvertTo-Json -Depth 8 |
            Set-Content -LiteralPath $temporaryPath -Encoding UTF8
        Move-Item -LiteralPath $temporaryPath -Destination $resolved -Force
    }
    finally {
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
    }
}

function Test-ProcessExecutablePath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int]$ProcessId,
        [Parameter(Mandatory)][string]$ExpectedPath
    )

    $process = Get-CimInstance Win32_Process `
        -Filter "ProcessId = $ProcessId" `
        -ErrorAction SilentlyContinue

    if ($null -eq $process -or [string]::IsNullOrWhiteSpace($process.ExecutablePath)) {
        return $false
    }

    $actual = [IO.Path]::GetFullPath($process.ExecutablePath)
    $expected = [IO.Path]::GetFullPath($ExpectedPath)
    return $actual.Equals($expected, [StringComparison]::OrdinalIgnoreCase)
}

Export-ModuleMember -Function `
    Assert-NormalizedExecutablePath, `
    Get-RegistryValueSnapshot, `
    Restore-RegistryValueSnapshot, `
    Write-JsonAtomically, `
    Test-ProcessExecutablePath
