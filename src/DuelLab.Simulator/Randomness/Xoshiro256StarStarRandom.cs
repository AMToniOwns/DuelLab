namespace DuelLab.Simulator.Randomness;

/// <summary>
/// A seeded pseudo-random number generator whose output is fixed by DuelLab itself, so the same seed
/// gives the same sequence on every .NET version and platform.
/// </summary>
/// <remarks>
/// <para>
/// The generator is xoshiro256** 1.0 (Blackman and Vigna). Its 256-bit state is seeded from a 64-bit
/// seed with SplitMix64, as its authors recommend. It is fast and statistically strong, but it is
/// <b>not</b> cryptographically secure, which is fine for simulations.
/// </para>
/// <para>
/// It subclasses <see cref="Random"/> so it can be passed anywhere a <see cref="Random"/> is expected.
/// All integer ranges are produced without modulo bias (Lemire's multiply-and-reject method).
/// </para>
/// <para>
/// Changing anything here changes seeded output. Any such change must bump
/// <see cref="Analysis.SimulationEngine.EngineVersion"/>.
/// </para>
/// </remarks>
public sealed class Xoshiro256StarStarRandom : Random
{
    /// <summary>A short identifier recorded with simulation runs.</summary>
    public const string AlgorithmName = "xoshiro256** 1.0 (SplitMix64 seeding)";

    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    /// <summary>Creates a generator from a 64-bit seed. Every seed, including 0, is valid.</summary>
    public Xoshiro256StarStarRandom(long seed)
    {
        var sm = unchecked((ulong)seed);
        _s0 = SplitMix64(ref sm);
        _s1 = SplitMix64(ref sm);
        _s2 = SplitMix64(ref sm);
        _s3 = SplitMix64(ref sm);
    }

    /// <summary>Returns the next 64 random bits.</summary>
    public ulong NextUInt64()
    {
        var result = RotateLeft(_s1 * 5, 7) * 9;
        var t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);

        return result;
    }

    /// <inheritdoc />
    public override int Next() => (int)NextBelow(int.MaxValue);

    /// <inheritdoc />
    public override int Next(int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        return (int)NextBelow((ulong)maxValue);
    }

    /// <inheritdoc />
    public override int Next(int minValue, int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);
        return (int)(minValue + (long)NextBelow((ulong)((long)maxValue - minValue)));
    }

    /// <inheritdoc />
    public override long NextInt64() => (long)NextBelow(long.MaxValue);

    /// <inheritdoc />
    public override long NextInt64(long maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        return (long)NextBelow((ulong)maxValue);
    }

    /// <inheritdoc />
    public override long NextInt64(long minValue, long maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);
        return unchecked(minValue + (long)NextBelow(unchecked((ulong)(maxValue - minValue))));
    }

    /// <inheritdoc />
    public override double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <inheritdoc />
    public override float NextSingle() => (NextUInt64() >> 40) * (1.0f / (1U << 24));

    /// <inheritdoc />
    public override void NextBytes(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        NextBytes(buffer.AsSpan());
    }

    /// <inheritdoc />
    public override void NextBytes(Span<byte> buffer)
    {
        while (buffer.Length >= 8)
        {
            BitConverter.TryWriteBytes(buffer, NextUInt64());
            buffer = buffer[8..];
        }

        if (!buffer.IsEmpty)
        {
            Span<byte> last = stackalloc byte[8];
            BitConverter.TryWriteBytes(last, NextUInt64());
            last[..buffer.Length].CopyTo(buffer);
        }
    }

    /// <inheritdoc />
    protected override double Sample() => NextDouble();

    /// <summary>A uniformly distributed value in [0, <paramref name="bound"/>), or 0 when the bound is 0.</summary>
    private ulong NextBelow(ulong bound)
    {
        if (bound == 0)
        {
            return 0;
        }

        // Lemire, "Fast Random Integer Generation in an Interval" (2019).
        var product = (UInt128)NextUInt64() * bound;
        var low = (ulong)product;
        if (low < bound)
        {
            var threshold = (0UL - bound) % bound;
            while (low < threshold)
            {
                product = (UInt128)NextUInt64() * bound;
                low = (ulong)product;
            }
        }

        return (ulong)(product >> 64);
    }

    private static ulong SplitMix64(ref ulong state)
    {
        var z = state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static ulong RotateLeft(ulong x, int k) => (x << k) | (x >> (64 - k));
}
