# ADR 0008 – OR conditions, card targets and combo lines

- **Status:** Accepted (extends ADR 0004)
- **Date:** 2026-11-07

## Context

Players wanted "(starter AND extender) OR two starters", requirements on specific cards, and saved
**combo lines**, the spec's first future-roadmap item: "let users define successful combinations or
sequences and evaluate hands against them". The spec also says to keep automated combo solving
separate from consistency analysis, and never to claim that a hand completes a combo.

## Decision

1. **Scenarios are an OR of ANDs.** A scenario has alternatives (`anyOf`), each a list of
   requirements with its own distinct-cards setting. The API still accepts the plain
   `requirements` form; it is one alternative.
2. **Requirements target roles and/or cards.** A card matches when it has any listed role or is any
   listed card. Internally every referenced role and card is an *atom* with its own bit (128-bit
   masks, up to 128 atoms per run). Hall's theorem still decides distinct-card feasibility, because
   "can serve some requirement in S" is "matches the union of S's targets".
3. **Combo lines are saved per deck version**: a name, notes and up to 10 pieces, each "*n* of these
   cards or roles". The simulator turns each line into `Combo: <name>` (all pieces, different cards)
   plus `Any combo line`. The expanded scenarios are stored with the run, so it reproduces without
   the line. A comparison uses one shared set: the base's lines, then any only the candidate has.
4. **"Sequence" means the pieces, not the steps.** An opening hand is unordered, so a line is
   checked as "every piece is in hand". Searchers are modelled by listing them as alternatives for
   a piece. DuelLab never resolves effects, checks costs or claims the line completes.
5. **Card references stay stable.** New references must be cards in the version. Removing a card
   keeps the lines that use it (validation warns), and those lines can still be re-saved.
6. **Engine version 1.1.0.** Existing request shapes evaluate exactly as before (pinned tests
   unchanged), so the minor version changes. The version policy is now: MAJOR when an existing
   seeded request's outcome can change.

## Consequences

- Exact probabilities cover all of this. Naming specific cards adds groups, so very large OR
  scenarios over many single cards can exceed the enumeration limit and report "exact unavailable".
- Tests check the new evaluator against a different reference algorithm (a backtracking assignment
  search, not Hall's theorem) over all 658,008 hands.
- A real combo engine (game state, effects, ordering) would be a separate component that produces
  "does this hand reach board X" answers. The scenario model would only consume them.
