param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path "$PSScriptRoot\.."
$LocalDotnet = Join-Path $Root ".dotnet\dotnet.exe"
$Dotnet = if (Test-Path $LocalDotnet) { $LocalDotnet } else { "dotnet" }

& $Dotnet build (Join-Path $Root "Tractor80.sln") --configuration $Configuration
exit $LASTEXITCODE
