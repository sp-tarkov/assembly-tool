param(
    [string]$Configuration = "Debug"
)

$PSStyle.OutputRendering = 'PlainText'

$solutionDir = (Resolve-Path "$PSScriptRoot").Path

Write-Host "Cleaning Solution before starting build"

#------------------------------------------------------------------------------Clean de4dot folder and build them
Write-Host "Cleaning de4dot output..."
$de4dotOutputDir = Join-Path $solutionDir "de4dot\$Configuration\net48"

if (Test-Path $de4dotOutputDir) {
    Remove-Item -Path (Join-Path $de4dotOutputDir "*.*") -Recurse -Force
} else {
    Write-Warning "de4dot output directory not found: $de4dotOutputDir"
}

Write-Host "Building de4dot output..."
$de4dot = Join-Path $solutionDir "de4dot\de4dot-x64\de4dot-x64.csproj"
$absolutePath = Resolve-Path -Path $de4dot
dotnet build $absolutePath --configuration $Configuration
#------------------------------------------------------------------------------Done

#------------------------------------------------------------------------------Clean all other project folders
Write-Host "Cleaning ReCodeItCLI output..."
$cliOutputDir = Join-Path (Join-Path $solutionDir "ReCodeItCLI") "bin\$Configuration\net9.0"
if (Test-Path $cliOutputDir) {
    Remove-Item -Path (Join-Path $cliOutputDir "*.*") -Recurse -Force
} else {
    Write-Warning "CLI output directory not found: $cliOutputDir"
}

Write-Host "Cleaning ReCodeItLib output..."
$cliOutputDir = Join-Path (Join-Path $solutionDir "ReCodeItLib") "bin\$Configuration\net9.0"
if (Test-Path $cliOutputDir) {
    Remove-Item -Path (Join-Path $cliOutputDir "*.*") -Recurse -Force
} else {
    Write-Warning "CLI output directory not found: $cliOutputDir"
}

Write-Host "Cleaning DumpLib output..."
$cliOutputDir = Join-Path (Join-Path $solutionDir "DumpLib") "bin\$Configuration\net471"
if (Test-Path $cliOutputDir) {
    Remove-Item -Path (Join-Path $cliOutputDir "*.*") -Recurse -Force
} else {
    Write-Warning "CLI output directory not found: $cliOutputDir"
}

Write-Host "Cleaning Process Completed"

#------------------------------------------------------------------------------Done

#------------------------------------------------------------------------------Build ReCodeItCLI and that will rebuild all

Write-Host "Building output..."
$builder = Join-Path $solutionDir "Builder\Builder.csproj"
$absolutePath = Resolve-Path -Path $builder
dotnet build $absolutePath --configuration $Configuration --verbosity detailed

Write-Host "Solution build complete - see Build folder at solution dir"

#------------------------------------------------------------------------------Done