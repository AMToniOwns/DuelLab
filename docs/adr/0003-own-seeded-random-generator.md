# ADR 0003 – Own seeded random generator

- **Status:** Accepted (resolves the risk recorded in ADR 0002)
- **Date:** 2026-11-07

## Context

Stored simulation runs must reproduce exactly from their seed, possibly years later. .NET does not
guarantee that `new Random(seed)` produces the same sequence across versions.

## Decision

- Use **xoshiro256\*\* 1.0** (Blackman and Vigna), seeded from a 64-bit seed with **SplitMix64**, as
  its authors recommend: `Xoshiro256StarStarRandom`.
- It **subclasses `System.Random`**, so `HandSimulator` is unchanged.
- Bounded integers use **Lemire's multiply-and-reject** method: no modulo bias.
- Pin the behaviour with tests whose expected values come from an independent Python port of the
  reference C code. That port's SplitMix64 matches the published reference sequence. The first
  sample hand of a seeded run is pinned too.
- Seeds are limited to 0 – 2⁵³ − 1 so they survive the browser (JavaScript numbers) unchanged.

## Consequences

- Seeded results depend only on DuelLab's code. Any change to the generator, the sampling loop or the
  evaluation must bump `SimulationEngine.EngineVersion`. The pinned tests fail until it is bumped
  deliberately.
- The generator is not cryptographically secure, which is fine for simulations. Server-chosen seeds
  come from `Random.Shared`, since they only need to vary, not be secret.
