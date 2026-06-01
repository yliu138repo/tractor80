# Architecture

## Goals

The architecture keeps rule correctness separate from presentation. The WPF app is intentionally thin: it renders the table, captures selected cards, and sends actions to `Tractor80.Core`. All legal-play validation, trick resolution, scoring, and AI decisions live in the core library so they are testable without UI automation.

## Core Library

`Cards.cs`

- `Card`, `Suit`, `Rank`, and `JokerColor`.
- `DeckFactory` creates and shuffles the two-deck, 108-card Tractor deck.
- Point values are encoded on the card model: 5 is 5 points; 10 and K are 10 points.

`Rules.cs`

- `TrumpConfig` models trump rank and optional trump suit.
- `CardRules` computes trump membership, effective suit group, power ordering, point totals, and tractor sequence values.
- `LeadAnalyzer` recognizes singles, identical pairs, and tractors.
- `PlayValidator` enforces lead/follow rules.
- `Trick` stores the four plays and resolves the winner.

`Ai.cs`

- `SeniorAiPlayer` chooses leads, follows, trump suit, and kitty discard.
- `LegalPlayGenerator` supplies validated candidate plays.
- Personas tune risk appetite without bypassing the rules engine.

`GameRound.cs`

- Deals a full hand.
- Lets the starter pick up and discard the kitty.
- Applies plays to the current trick.
- Tracks opponent points and produces a `RoundResult`.
- `RoundScorer` is public and directly tested because score movement is high-risk game logic.

## UI Layer

The WPF app uses code-behind rather than a heavy MVVM framework for this first desktop release. The window has:

- A modern dark table surface with soft shadows and layered felt.
- North/East/West player count panels.
- A central trick display.
- A right-side table log.
- A scoring-race panel showing current trick points and progress to 80.
- A clickable human hand area.
- New, Rush Hand, Hint, Rush/Defend, Play, and language commands.
- Chinese and English UI text, with Chinese selected by default.
- Animated card handout, trick fade-in, selected-card lift, and point-capture flash.

This keeps latency low and avoids framework weight while still leaving a clean path to MVVM if the UI expands to settings, replay, or network play.

## AI Decision Flow

1. Generate legal candidate plays from the current hand and trick shape.
2. If following, identify the current winning side.
3. If an opponent is winning a valuable trick, find the cheapest winning play.
4. If partner is winning, prefer unloading point cards without wasting control.
5. If leading, prefer tractors/pairs according to persona and score pressure.
6. Validate the selected play through `PlayValidator` before it is applied.

The AI is deliberately conservative about hidden information. It does not cheat by reading other hands, but it does use played-card memory and the current score pressure.

## Rule Decisions

The first release implements the core Pagat-backed game flow: singles, pairs, tractors, trump order, point cards, kitty, and level movement. Complex "throwing"/mixed top-card set leads vary by table and are left out of the first playable version so the engine can be strict, predictable, and well-tested.

`抢分局` is a UI mode over the same engine: East starts as declarer, which makes North/South the scoring team. Because South is the human seat, this mode lets the player actively capture score cards instead of always defending as the first declarer.

## Quality Strategy

- Core logic is tested independently from WPF.
- Full AI hand simulation checks that the engine and AI can finish a hand without illegal moves.
- Coverage gate is set at 90% line coverage for the core.
- CI publishes a playable `win-x64` artifact after tests pass.
