param(
    [string]$Configuration = "Release",
    [double]$MinimumLineCoverage = 90
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path "$PSScriptRoot\.."
$LocalDotnet = Join-Path $Root ".dotnet\dotnet.exe"
$Dotnet = if (Test-Path $LocalDotnet) { $LocalDotnet } else { "dotnet" }
$CoverageDir = Join-Path $Root "artifacts\coverage"
$SummaryFile = Join-Path $Root "artifacts\coverage-summary.md"

if (Test-Path $CoverageDir) {
    Remove-Item -Recurse -Force $CoverageDir
}

& $Dotnet test (Join-Path $Root "tests\Tractor80.Tests\Tractor80.Tests.csproj") `
    --configuration $Configuration `
    --collect:"XPlat Code Coverage" `
    --results-directory $CoverageDir

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$CoverageFile = Get-ChildItem $CoverageDir -Recurse -Filter coverage.cobertura.xml | Select-Object -First 1
if (-not $CoverageFile) {
    throw "Coverage file was not produced."
}

[xml]$Coverage = Get-Content $CoverageFile.FullName
$LineCoverage = [math]::Round(([double]$Coverage.coverage.'line-rate') * 100, 2)
$BranchCoverage = [math]::Round(([double]$Coverage.coverage.'branch-rate') * 100, 2)

$Summary = @"
# Coverage Summary

- Test project: tests/Tractor80.Tests
- Coverage file: $($CoverageFile.FullName)
- Line coverage: $LineCoverage%
- Branch coverage: $BranchCoverage%
- Required line coverage: $MinimumLineCoverage%
"@

New-Item -ItemType Directory -Force -Path (Split-Path $SummaryFile) | Out-Null
Set-Content -Path $SummaryFile -Value $Summary -Encoding UTF8

if ($LineCoverage -lt $MinimumLineCoverage) {
    throw "Line coverage $LineCoverage% is below the $MinimumLineCoverage% threshold."
}

Write-Host $Summary
