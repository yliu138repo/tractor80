param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path "$PSScriptRoot\.."
$LocalDotnet = Join-Path $Root ".dotnet\dotnet.exe"
$Dotnet = if (Test-Path $LocalDotnet) { $LocalDotnet } else { "dotnet" }
$Output = Join-Path $Root "artifacts\publish\$Runtime"

if (Test-Path $Output) {
    Remove-Item -Recurse -Force $Output
}

& $Dotnet publish (Join-Path $Root "src\Tractor80.Windows\Tractor80.Windows.csproj") `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=true `
    -p:EnableCompressionInSingleFile=true `
    --output $Output

exit $LASTEXITCODE
