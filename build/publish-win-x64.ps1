# Publishes FieldCheck as a self-contained, single-file Windows executable.
# Output: publish\win-x64\FieldCheck.exe  (no .NET install required on the target machine)
#
# Usage:  powershell -ExecutionPolicy Bypass -File build\publish-win-x64.ps1

$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "src\FieldCheck.App\FieldCheck.App.csproj"
$output = Join-Path $root "publish\win-x64"

Write-Host "Regenerating application icon..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "make-icon.ps1")

$publishArgs = @(
    "publish", $project,
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:DebugType=none",
    "-p:SatelliteResourceLanguages=en",
    "-o", $output
)

Write-Host "Publishing self-contained single-file build (win-x64)..." -ForegroundColor Cyan
dotnet @publishArgs

$exe = Join-Path $output "FieldCheck.exe"
if (Test-Path $exe) {
    $sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host ""
    Write-Host "Done. $exe ($sizeMb MB)" -ForegroundColor Green
    Write-Host "Double-click it to run - no .NET runtime install required." -ForegroundColor Green
}
else {
    throw "Publish finished but FieldCheck.exe was not found."
}
