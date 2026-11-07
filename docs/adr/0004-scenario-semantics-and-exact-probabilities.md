# ADR 0004 – Scenario semantics and exact probabilities

- **Status:** Accepted; extended by [ADR 0008](0008-or-conditions-card-targets-and-combo-lines.md) (OR, card targets, combo lines)
- **Date:** 2026-11-07

## Context

The specification left open (§14) whether a card with two roles may satisfy two conditions, and
whether conditions should be simple or arbitrary logic. It asked for exact probabilities "where
possible" alongside Monte Carlo estimates.

## Decision

1. **Roles are not exclusive.** A card may have several roles per version.
2. A **scenario** is an AND of requirements; each is "at least *m*, optionally at most *M*, cards
   with role R". There is no OR or nesting yet (start simple); several scenarios can be measured
   per run.
3. **Distinct-card semantics are explicit per scenario** (`requireDistinctCards`, default `true`).
   With it, minimums must be met by different physical cards, checked exactly by Hall's theorem over
   subsets of requirements (at most 10, so at most 1,024 subsets). Maximums always count every card
   with the role.
4. **Exact probabilities for every scenario**, not just single-role ones: cards are grouped by their
   set of relevant roles, and the engine enumerates every possible group count in the hand
   (multivariate hypergeometric) with exact 128-bit binomials. The number of combinations is counted
   first; above 2 million the exact value is reported as unavailable instead of slowing the run.
5. Monte Carlo estimates carry **95% Wilson intervals**. Results always show the estimate and the
   exact value side by side, never one presented as the other.

## Consequences

- One evaluator serves both the simulation (per hand) and the exact enumeration (per group count),
  so they cannot disagree about what a scenario means. Tests check both against brute force over all
  658,008 hands.
- Exact values make comparisons noise-free. The UI uses them for decisions and shows simulated
  values as a cross-check.
- OR conditions or combo sequences would need a new condition model; the evaluator is internal, so
  that change stays inside the Simulator.
