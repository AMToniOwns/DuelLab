# Statistics: what DuelLab measures

## The model

- An opening hand is a **uniformly random set of cards from the Main Deck, drawn without
  replacement**. Every physical copy is equally likely. Extra and Side Deck cards are never drawn.
- Each copy carries the **roles you assigned** to its card in that version. Roles are your own
  labels, not game rules.
- A **scenario** is met when **any** of its alternatives holds (logical OR); an alternative holds
  when **all** of its requirements do (logical AND). Each requirement says a hand has at least *m*
  (and optionally at most *M*) cards that **match**: cards with any of the chosen roles, or that are
  any of the chosen cards.
- A **combo line** is a scenario built from a deck's saved line: one requirement per piece ("1 of
  Ash, Diabellstar or Original Sinful Spoils"), all needed, from different cards. "Any combo line" is
  the OR of all lines.

### Distinct cards or overlapping roles

When a scenario has several minimums, DuelLab must decide whether one card may count toward more
than one. Each scenario states it explicitly:

- **Different cards (default).** Each minimum must be met by *different* physical cards. A card
  labelled Starter and Extender can count as one or the other. This is usually what players mean by
  "a starter and an extender". The engine checks it exactly with Hall's theorem: the requirements can
  all be met if and only if, for every subset of them, enough cards in the hand can serve that
  subset.
- **Overlapping.** One card counts toward every role it has.

Maximums always count every matching card ("at most 0 bricks"). The same card named in two pieces
needs two different copies. Each alternative of an OR scenario has its own setting.

## Exact and simulated values

**Exact.** Every role and card a run refers to gets a bit; cards that match exactly the same set
are interchangeable, so they form one group. For one role, P(at least *m*) is the hypergeometric tail; for example, 9 starters in 40
cards with a 5-card hand gives 1 − C(31,5)/C(40,5) = 74.18%. For scenarios with several roles,
cards are grouped by the set of relevant roles they carry, and the engine sums the multivariate
hypergeometric probability of every possible count of each group in the hand (a few hundred
combinations for a typical deck). Binomial coefficients are exact 128-bit integers, so there is no
rounding until the final division. If a run would need more than 2 million combinations (only
possible with dozens of distinct roles), the exact value is reported as unavailable.

**Simulated.** The share of simulated hands that met the scenario, with a **95% Wilson score
interval**. The interval behaves well near 0% and 100%, unlike "estimate ± 1.96 standard errors".
With 10,000 hands a probability near 50% is pinned to about ±1 percentage point; with 100,000 hands,
about ±0.3.

Since the exact value is available, why simulate? The simulation provides real sample hands to
inspect, and it checks the exact calculation independently. The two must agree within the interval
about 95% of the time. The test suite also checks both against brute force over all 658,008
five-card hands of a 40-card deck.

## Comparing versions

A comparison runs both versions with the **same seed**. Differences are reported in **percentage
points** (candidate − base). Use the exact difference to make decisions. The simulated difference
still contains sampling noise, because changing the deck changes which hands the same seed
produces.

## Reproducibility

The random generator is DuelLab's own xoshiro256** (see
[ADR 0003](adr/0003-own-seeded-random-generator.md)), so a seed gives the same hands on any machine
and .NET version. Each run records the engine version, generator, seed, configuration and a
snapshot of the deck. "Reproduce" re-runs from that record and checks every count, probability and
sample hand matches exactly.

## Limitations

- **Not a combo engine.** A hand that meets "starter + extender", or a combo line, contains the
  cards you listed. Whether it can actually make your end board depends on rules, ordering, costs,
  once-per-turn limits and the opponent, none of which DuelLab models. The opening hand has no
  order, so a line is checked as "all pieces present", not as steps.
- **You define what "searches it" means.** Listing a searcher as an option of a piece says the
  searcher is as good as the piece. DuelLab does not check that it can actually find it (for
  example, that the target is still in the deck).
- **Opening hand only.** No draws after the first hand, no searchers resolving, no mulligans, no
  "draw on turn 1 going second" except by choosing a hand size of 6.
- **Labels are subjective.** The result is only as meaningful as the roles you assign.
- **The Forbidden & Limited list is the provider's.** DuelLab enforces the TCG list as YGOPRODeck
  reports it at each import and dates each version by when DuelLab first saw it; it does not know
  official effective dates, and it starts recording history from the day it is installed. Check the
  official list before a tournament.
