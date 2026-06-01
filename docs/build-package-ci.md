# Build, Package, And CI

## Prerequisites

- Windows 11 or Windows Server runner.
- .NET SDK 8.0.x.

This workspace includes a local SDK under `.dotnet` because the machine did not have `dotnet` on `PATH`. The repo scripts prefer that local SDK when present.

## Local Commands

Build:

```powershell
.\eng\build.ps1 -Configuration Release
```

Test with coverage:

```powershell
.\eng\test.ps1 -Configuration Release -MinimumLineCoverage 90
```

Publish:

```powershell
.\eng\publish-win.ps1 -Configuration Release -Runtime win-x64
```

Run:

```powershell
.\artifacts\publish\win-x64\Tractor80.Windows.exe
```

## Packaging Notes

The app is published as a self-contained Windows desktop app. WPF single-file publishing still needs several native support DLLs beside the exe, so distribute the complete publish folder:

`artifacts\publish\win-x64`

For a release zip:

```powershell
Compress-Archive -Path .\artifacts\publish\win-x64\* -DestinationPath .\artifacts\Tractor80-win-x64.zip -Force
```

For an installer later, use WiX Toolset or MSIX packaging over the publish folder. MSIX is preferred for Windows 11 Store-style distribution; WiX is preferred for classic enterprise installers.

## CI Workflow

The GitHub Actions workflow is at:

`.github\workflows\windows-ci.yml`

It:

1. Installs .NET 8.
2. Restores packages.
3. Builds in Release.
4. Runs tests with Cobertura coverage.
5. Fails under 90% line coverage.
6. Publishes a self-contained Windows build.
7. Uploads the playable folder as an artifact.

## Release Checklist

1. Run `.\eng\test.ps1 -Configuration Release -MinimumLineCoverage 90`.
2. Run `.\eng\publish-win.ps1 -Configuration Release -Runtime win-x64`.
3. Smoke-launch `artifacts\publish\win-x64\Tractor80.Windows.exe`.
4. Zip the entire publish folder.
5. Attach the zip and coverage summary to the release.
