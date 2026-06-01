# AI Strategy

## Current Algorithm

The current players use a deterministic senior-player heuristic with one-trick tactical search.

The flow is:

1. Generate only legal candidate plays from the rule engine.
2. Detect the current winning player in the trick.
3. Count exposed score cards on the table.
4. If an opponent is winning points, choose the cheapest legal play that can overtake.
5. If the partner is already winning, unload score cards into the partner's trick and avoid wasting control cards.
6. If leading, prefer tractors and pairs when they create pressure, otherwise lead low safe cards or high trump when the score pressure is high.
7. Validate the selected play again before applying it.

This is intentionally fast and stable. It avoids obvious beginner mistakes such as failing to follow suit, breaking required pairs, throwing points to opponents, or wasting trump when the partner is already winning.

## Personas

- `PointHunter`: more aggressive about overtaking and capturing 5/10/K cards.
- `TrumpController`: more willing to spend trump to control the table when the scoring side is close to danger.
- `PartnerProtector`: conservative partner behavior, preserving control and feeding points only when partner is already winning.

## Why This Fits The Current Game

Tractor is an imperfect-information partnership game. A strong full AI would use information-set search, but that requires sampling hidden hands and simulating future tricks. For the current local Windows game, the heuristic engine gives low latency and predictable rule-safe behavior while still acting like an experienced player in common tactical spots.

## Best Future Upgrade

The best next AI step is a hybrid `ISMCTS + heuristic evaluator`:

- Use Information Set Monte Carlo Tree Search to sample plausible hidden hands.
- Preserve the current legal-play generator so simulations never cheat with illegal moves.
- Use the current senior heuristics as the rollout/evaluation policy.
- Cache results per trick to keep UI latency under roughly 150 ms.

That would make the AI stronger in long-term planning: saving tractors, reading void suits, deciding when to draw trump, and setting up partner captures.
