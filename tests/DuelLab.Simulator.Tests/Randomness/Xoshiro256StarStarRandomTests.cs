using DuelLab.Simulator.Randomness;

namespace DuelLab.Simulator.Tests.Randomness;

public sealed class Xoshiro256StarStarRandomTests
{
    // Expected outputs come from an independent Python port of the reference C code of SplitMix64 and
    // xoshiro256** (its SplitMix64 matches the published sequence for seed 1234567:
    // 6457827717110365317, 3203168211198807973, ...). If these values change, seeded simulation
    // runs stop being reproducible.
    [Theory]
    [InlineData(12345L, 13720838825685603483UL, 2398916695208396998UL, 17770384849984869256UL, 891717726879801395UL)]
    public void NextUInt64_MatchesReferenceImplementation(long seed, ulong a, ulong b, ulong c, ulong d)
    {
        var random = new Xoshiro256StarStarRandom(seed);

        Assert.Equal([a, b, c, d], [random.NextUInt64(), random.NextUInt64(), random.NextUInt64(), random.NextUInt64()]);
    }

    [Theory]
    [InlineData(0L, 11091344671253066420UL, 13793997310169335082UL)]
    [InlineData(-1L, 10328197420357168392UL, 14156678507024973869UL)]
    public void EdgeSeeds_AreValidAndMatchReference(long seed, ulong first, ulong second)
    {
        var random = new Xoshiro256StarStarRandom(seed);

        Assert.Equal(first, random.NextUInt64());
        Assert.Equal(second, random.NextUInt64());
    }

    [Fact]
    public void SameSeed_GivesSameSequenceThroughEveryMethod()
    {
        var a = new Xoshiro256StarStarRandom(42);
        var b = new Xoshiro256StarStarRandom(42);

        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(a.Next(i, i * 2 + 1), b.Next(i, i * 2 + 1));
            Assert.Equal(a.NextDouble(), b.NextDouble());
            Assert.Equal(a.NextInt64(), b.NextInt64());
        }
    }

    [Fact]
    public void NextRange_StaysInBounds()
    {
        var random = new Xoshiro256StarStarRandom(7);

        for (var i = 0; i < 100_000; i++)
        {
            Assert.InRange(random.Next(3, 9), 3, 8);
            Assert.InRange(random.Next(5), 0, 4);
            Assert.InRange(random.NextDouble(), 0.0, 0.9999999999999999);
        }
    }

    [Fact]
    public void EmptyRange_ReturnsMinimum()
    {
        var random = new Xoshiro256StarStarRandom(7);

        Assert.Equal(5, random.Next(5, 5));
        Assert.Equal(0, random.Next(0));
    }

    [Fact]
    public void FullIntRange_Works()
    {
        var random = new Xoshiro256StarStarRandom(7);

        _ = random.Next(int.MinValue, int.MaxValue); // must not overflow or throw
    }

    [Fact]
    public void InvalidRanges_Throw()
    {
        var random = new Xoshiro256StarStarRandom(7);

        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(5, 4));
    }

    [Fact]
    public void NextSmallRange_IsUniform()
    {
        // 600,000 rolls of a six-sided die; chi-square critical value at 5 df, alpha = 0.001, is 20.515.
        var random = new Xoshiro256StarStarRandom(2026);
        var counts = new int[6];
        for (var i = 0; i < 600_000; i++)
        {
            counts[random.Next(0, 6)]++;
        }

        var chiSquare = counts.Sum(c => (c - 100_000.0) * (c - 100_000.0) / 100_000.0);
        Assert.InRange(chiSquare, 0, 20.515);
    }

    [Fact]
    public void NextBytes_IsDeterministicForPartialWords()
    {
        var a = new byte[13];
        var b = new byte[13];

        new Xoshiro256StarStarRandom(9).NextBytes(a);
        new Xoshiro256StarStarRandom(9).NextBytes(b);

        Assert.Equal(a, b);
        Assert.Contains(a, x => x != 0);
    }
}
