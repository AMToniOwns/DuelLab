# DuelLab

A personal Yu-Gi-Oh! TCG deck-building and **consistency-analysis** web application: build a deck,
simulate thousands of opening hands, and see how consistent it is before you play it.

> **Status:** TASK 001 done. The opening-hand engine (`DuelLab.Simulator`) and its unit tests are
> in place. The API, persistence, card catalogue and UI are not started yet.

## Repository layout

```text
DuelLab/
├── src/
│   └── DuelLab.Simulator/          # Sampling algorithms; no dependency on the UI or database
│       └── Algorithms/HandSimulator.cs
├── tests/
│   └── DuelLab.Simulator.Tests/    # xUnit tests, including statistical checks
├── docs/adr/                       # Architecture Decision Records
├── Directory.Build.props           # Shared compiler/analyzer settings (warnings are errors)
├── global.json                     # Pins the .NET 10 SDK feature band
└── DuelLab.slnx
```

The full target structure (Api / Application / Domain / Infrastructure / Simulator, plus the Vue
frontend) is described in the project specification. Each project is added when the milestone that
needs it starts.

## Prerequisites

- .NET 10 SDK (10.0.100 or later in the 10.0 feature bands)

## Build and test

```bash
dotnet build
dotnet test
```

## The opening-hand engine

```csharp
var simulator = new HandSimulator();
var random = new Random(12345);                 // a seed makes the draw reproducible
IReadOnlyList<CardCopy> hand = simulator.Draw(deck, handSize: 5, random);
```

- `deck` is a list of **physical card copies**. Three copies of a card are three elements.
- Cards are drawn **without replacement** using a partial Fisher–Yates shuffle.
- The input deck is **never modified**.
- `handSize` must be between `0` and `deck.Count`. Anything else throws `ArgumentOutOfRangeException`.
- The same seed and the same deck order give the same hand. Drawing many hands from one `Random`
  instance gives the same *sequence* of hands.

See [ADR 0002](docs/adr/0002-hand-sampling-algorithm-and-randomness.md) for the design decisions.

## Statistical assumptions and limitations

- Every physical card copy in the deck is equally likely to be in any hand position. Opening hands
  are uniform random samples without replacement, the setting in which hypergeometric probabilities
  apply.
- The engine draws cards. It does not decide whether a hand is "good". Role-based conditions
  (starters, extenders, bricks) come later, and they will never be presented as proof that a hand
  can complete a real combo.
- Reproducibility currently depends on `System.Random`'s seeded algorithm. See ADR 0002 for the
  caveat.

## Architecture decisions

- [ADR 0001 – Modular monolith on .NET 10 LTS](docs/adr/0001-modular-monolith-on-dotnet-10.md)
- [ADR 0002 – Hand sampling algorithm and source of randomness](docs/adr/0002-hand-sampling-algorithm-and-randomness.md)
