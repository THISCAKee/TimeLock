[CmdletBinding()]
param(
    [string]$InnoSetupPath
)

$ErrorActionPreference = 'Stop'

$installerRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$projectPath = Join-Path $installerRoot 'TimeLockApp.csproj'
$publishDir = Join-Path $PSScriptRoot 'publish'
$outputDir = Join-Path $PSScriptRoot 'output'
$scriptPath = Join-Path $PSScriptRoot 'TimeLock.iss'

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Project file not found: $projectPath"
}

$null = & dotnet --version
if ($LASTEXITCODE -ne 0) {
    throw 'The .NET SDK is required but dotnet --version failed.'
}

function Find-InnoSetupCompiler {
    param([string]$RequestedPath)

    if ($RequestedPath) {
        $resolved = (Resolve-Path -LiteralPath $RequestedPath -ErrorAction Stop).Path
        if ((Get-Item -LiteralPath $resolved).PSIsContainer) {
            $resolved = Join-Path $resolved 'ISCC.exe'
        }
        if (Test-Path -LiteralPath $resolved -PathType Leaf) {
            return $resolved
        }
        throw "Inno Setup compiler not found at: $resolved"
    }

    $onPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($onPath) {
        return $onPath.Source
    }

    $standardPaths = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    )
    foreach ($candidate in $standardPaths) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return $candidate
        }
    }

    throw 'Inno Setup 6 compiler (ISCC.exe) was not found. Install Inno Setup 6 or pass -InnoSetupPath.'
}

$isccPath = Find-InnoSetupCompiler -RequestedPath $InnoSetupPath

foreach ($ownedPath in @($publishDir, $outputDir)) {
    if (Test-Path -LiteralPath $ownedPath) {
        Remove-Item -LiteralPath $ownedPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $ownedPath -Force | Out-Null
}

Write-Host 'Publishing TimeLockApp (Release, win-x64, self-contained)...'
& dotnet publish $projectPath -c Release -r win-x64 --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet publish failed.'
}

# The application project includes this file for local development. Remove it
# from the installer staging directory so credentials are never packaged.
Get-ChildItem -LiteralPath $publishDir -Filter 'service-account.json' -File -Recurse -ErrorAction SilentlyContinue |
    Remove-Item -Force

$credentialFiles = @(Get-ChildItem -LiteralPath $publishDir -Filter 'service-account.json' -File -Recurse -ErrorAction SilentlyContinue)
if ($credentialFiles.Count -gt 0) {
    throw 'Credential file was found in the publish directory after cleanup.'
}

Write-Host 'Compiling Inno Setup installer...'
& $isccPath "/DSourceDir=$publishDir" $scriptPath
if ($LASTEXITCODE -ne 0) {
    throw 'Inno Setup compilation failed.'
}

$installerPath = Join-Path $outputDir 'TimeLock-Setup.exe'
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "Installer output was not created: $installerPath"
}

$sizeMb = [Math]::Round((Get-Item -LiteralPath $installerPath).Length / 1MB, 2)
Write-Host "Installer created: $installerPath ($sizeMb MB)"
