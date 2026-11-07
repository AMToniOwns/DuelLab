# ADR 0002 – Hand sampling algorithm and source of randomness

- **Status:** Accepted
- **Date:** 2026-11-07

## Context

The opening-hand engine must draw cards without replacement, without bias, reproducibly from a
seed, and without modifying the caller's deck. Later milestones will call it 100,000 times or more
per simulation run, so how much work each draw does matters.

## Decision

### Algorithm: partial Fisher–Yates

`HandSimulator.Draw<T>` copies the deck to a private array, then runs Fisher–Yates **from the front
for only `handSize` steps**. Step `i` swaps position `i` with a position chosen uniformly from
`i..n-1`. It returns the first `handSize` elements.

- **Unbiased:** each ordered hand of size *k* has probability `(n-k)! / n!`, the same as a full
  shuffle followed by taking the top *k* cards.
- **Cheaper:** *k* random numbers and swaps instead of *n − 1* (5 instead of 39 for a typical draw).
  The O(n) copy of the deck is still needed so the input is never modified.
- **Tested against known mistakes:** the tests include chi-square uniformity checks and a comparison
  with the exact hypergeometric probability. They were confirmed to fail for the classic
  `random.Next(n)` bias and for the off-by-one `random.Next(i + 1, n)`.

### Generic over card type

`Draw<T>` accepts `IReadOnlyList<T>` of physical copies, so the Simulator does not depend on the
Domain's card model. The caller expands quantities into copies. A specific deck abstraction can be
added later if the Domain needs one, without changing the sampling code.

### Randomness: an injected `System.Random`

The caller supplies the `Random`. The simulator holds no static or global randomness.

- Simple, and it meets the specification ("an injected `Random` instance or a suitable
  abstraction").
- `Random` is not sealed. If we need our own generator later, it can subclass `Random` without
  changing `HandSimulator`'s signature.

## Consequences and risks

- **Long-term reproducibility risk:** .NET documents that the algorithm behind `new Random(seed)`
  is **not guaranteed to stay the same across .NET versions**. In practice the seeded path has kept
  the legacy algorithm for compatibility, but that is not a contract. Saved runs that must
  reproduce exactly across runtime upgrades need **our own documented PRNG** (for example,
  a PCG or xoshiro variant subclassing `Random`). Decide this before the `SimulationRuns` table
  (spec §5.6) stores seeds for users.
- **Changing the algorithm changes seeded output.** Any change to the sampling loop or the PRNG
  must bump the recorded `EngineVersion` (spec §5.6), so old runs are never silently reinterpreted.
- The hand is returned **in draw order**. Analytics should treat hands as multisets, since order
  does not matter for opening-hand conditions.
