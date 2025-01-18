param(
    [string]$Configuration = "Debug"
)

$PSStyle.OutputRendering = 'PlainText'

$solutionDir = (Resolve-Path "$PSScriptRoot\..").Path
$buildPath = Join-Path $solutionDir "Build"

if (Test-Path $buildPath) {
    Write-Host "Removing existing Build folder: $buildPath"
    Get-ChildItem -Path $buildPath -Recurse -Force | Remove-Item -Recurse -Force
}

Write-Host "Copying ReCodeItCLI output..."
$cliOutputDir = Join-Path (Join-Path $solutionDir "ReCodeItCLI") "bin\$Configuration\net9.0"
if (Test-Path $cliOutputDir) {
    New-Item -Path $buildPath -ItemType Directory -ErrorAction SilentlyContinue
    Copy-Item -Path (Join-Path $cliOutputDir "*.*") -Destination $buildPath -Recurse
} else {
    Write-Warning "CLI output directory not found: $cliOutputDir"
}

Write-Host "Copying de4dot output..."
$de4dotOutputDir = Join-Path $solutionDir "de4dot\$Configuration\net48"
$de4dotDest = Join-Path $buildPath "de4dot"
if (Test-Path $de4dotOutputDir) {
    New-Item -Path $de4dotDest -ItemType Directory -ErrorAction SilentlyContinue
    Copy-Item -Path (Join-Path $de4dotOutputDir "*.*") -Destination $de4dotDest -Recurse
} else {
    Write-Warning "de4dot output directory not found: $de4dotOutputDir"
}

Write-Host "Copying template assets..."
$templatesDir = Join-Path $solutionDir "Assets\Templates"
$templatesDest= Join-Path $buildPath "Data"
if (Test-Path $templatesDir) {
    New-Item -Path $templatesDest -ItemType Directory -ErrorAction SilentlyContinue
    Copy-Item -Path (Join-Path $templatesDir "*.*") -Destination $templatesDest -Recurse
} else {
    Write-Warning "Templates directory not found: $templatesDir"
}

Write-Host "Copying dumper configuration files..."
$configDir = Join-Path $solutionDir "DumpLib\DUMPDATA"
$configDest= Join-Path $buildPath "DUMPDATA"
if (Test-Path $configDir) {
    New-Item -Path $configDest -ItemType Directory -ErrorAction SilentlyContinue
    Copy-Item -Path (Join-Path $configDir "*.*") -Destination $configDest -Recurse
} else {
    Write-Warning "Config directory not found: $configDir"
}

Write-Host "Build Process Completed"
