# Tractor 80 / 八十分

Windows 11 WPF implementation of the four-player double-deck Tractor / Sheng Ji / 80 Points game. The repo contains a playable desktop exe, a rules-and-AI core, unit and regression tests, coverage output, packaging scripts, and a Windows CI workflow.

Rules reference: the implementation is anchored on Pagat's Tractor rules page, including two-deck play, 5/10/K point cards, jokers and trump rank ordering, tractors, kitty scoring, and level movement: https://www.pagat.com/kt5/tractor.html.

## Ready To Play

The current self-contained build is here:

`artifacts\publish\win-x64\Tractor80.Windows.exe`

Packaged zip:

`artifacts\Tractor80-win-x64.zip`

It was smoke-launched locally after publishing. No .NET runtime installation is required for that published folder because it is self-contained.

## Current Game Scope

- Four players: South is the human player, North is partner AI, East/West are opponent AIs.
- Two decks, 108 cards, 25 cards per player plus 8-card kitty.
- Trump rank defaults to 2 for the first hand; the starter AI chooses the strongest trump suit from the dealt hand.
- Supported lead shapes: singles, identical pairs, and tractors made from consecutive identical pairs.
- Follow rules enforce suit/trump obligations and preserve pairs when responding to pair and tractor leads.
- Scoring tracks opponents' collected 5/10/K points and applies kitty multipliers when opponents win the last trick.
- UI language supports Chinese and English; Chinese is the default.
- `抢分局` starts with East as declarer, putting the human South/North team on the scoring side so the player can actively 抢分.
- Animated dealing, point-card badges, current-trick score state, and score-capture flashes make the table feel closer to classic Windows Tractor pacing.

## AI Design

The AI is deterministic, rule-safe, and uses three senior-player personalities:

- `TrumpController`: pressures with trump control when opponents approach a dangerous score.
- `PointHunter`: cheaply overtakes exposed point tricks and leads strong pair/tractor pressure.
- `PartnerProtector`: avoids wasting winners when the partner is already winning and dumps points into partner-owned tricks.

The first priority is never making illegal or obviously silly plays. Strategy evaluation happens after legal candidate generation, so each AI action is validated by the same engine used for the human player.

## Build

This machine did not have .NET on `PATH`, so a local SDK was installed into `.dotnet`. The scripts automatically use `.dotnet\dotnet.exe` when present, otherwise they use the system `dotnet`.

```powershell
.\eng\build.ps1 -Configuration Release
```

## Test And Coverage

```powershell
.\eng\test.ps1 -Configuration Release -MinimumLineCoverage 90
```

Latest local result:

- 38 tests passed
- Core line coverage: 95.29%
- Core branch coverage: 88.94%
- Cobertura XML: `artifacts\coverage\...\coverage.cobertura.xml`
- Summary: `artifacts\coverage-summary.md`

## Package

```powershell
.\eng\publish-win.ps1 -Configuration Release -Runtime win-x64
```

Output:

`artifacts\publish\win-x64\Tractor80.Windows.exe`

The publish command uses:

- `--self-contained true`
- `PublishSingleFile=true`
- `PublishReadyToRun=true`
- `EnableCompressionInSingleFile=true`

WPF still emits a few native runtime support DLLs next to the exe; ship the whole `artifacts\publish\win-x64` folder.

## CI/CD

`.github\workflows\windows-ci.yml` runs on `windows-latest` and performs:

1. Restore.
2. Release build.
3. Test with Cobertura coverage.
4. Fail if line coverage is below 90%.
5. Publish a self-contained `win-x64` build.
6. Upload the playable folder as the `Tractor80-win-x64` artifact.

## Project Layout

- `src\Tractor80.Core`: card model, rules engine, trick validator, scoring, AI, round orchestration.
- `src\Tractor80.Windows`: Windows 11 WPF game UI.
- `tests\Tractor80.Tests`: unit, integration, and regression tests for rules, AI, scoring, and full-hand simulation.
- `eng`: local build/test/publish automation.
- `artifacts`: generated coverage and playable publish output.

More details are in `docs\architecture.md` and `docs\build-package-ci.md`.
