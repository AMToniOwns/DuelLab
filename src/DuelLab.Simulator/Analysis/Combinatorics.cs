namespace DuelLab.Simulator.Analysis;

/// <summary>Exact binomial coefficients and hypergeometric probabilities.</summary>
/// <remarks>
/// Coefficients come from a precomputed Pascal's triangle in 128-bit integers. That is exact (no
/// rounding, no overflow) for every deck size DuelLab supports; C(120, 60) is about 9.7e34, well below
/// the UInt128 maximum of about 3.4e38.
/// </remarks>
public static class Combinatorics
{
    /// <summary>The largest <c>n</c> for which <see cref="Choose"/> is defined.</summary>
    public const int MaxN = 120;

    private static readonly UInt128[][] Pascal = BuildPascal();

    /// <summary>C(n, k), the number of ways to choose k items from n. Zero when k is outside 0..n.</summary>
    public static UInt128 Choose(int n, int k)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(n, MaxN);
        return k < 0 || k > n ? UInt128.Zero : Pascal[n][k];
    }

    /// <summary>
    /// P(X ≥ <paramref name="atLeast"/>) when drawing <paramref name="handSize"/> cards without replacement
    /// from <paramref name="deckSize"/> cards, of which <paramref name="successes"/> count as a success.
    /// </summary>
    public static double HypergeometricAtLeast(int deckSize, int successes, int handSize, int atLeast)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(successes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(successes, deckSize);
        ArgumentOutOfRangeException.ThrowIfNegative(handSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(handSize, deckSize);

        var favourable = UInt128.Zero;
        for (var x = Math.Max(atLeast, 0); x <= Math.Min(successes, handSize); x++)
        {
            favourable += Choose(successes, x) * Choose(deckSize - successes, handSize - x);
        }

        return Ratio(favourable, Choose(deckSize, handSize));
    }

    /// <summary>Converts an exact ratio of two counts to the nearest double.</summary>
    public static double Ratio(UInt128 numerator, UInt128 denominator) =>
        denominator == UInt128.Zero ? throw new DivideByZeroException() : (double)numerator / (double)denominator;

    private static UInt128[][] BuildPascal()
    {
        var rows = new UInt128[MaxN + 1][];
        for (var n = 0; n <= MaxN; n++)
        {
            rows[n] = new UInt128[n + 1];
            rows[n][0] = rows[n][n] = UInt128.One;
            for (var k = 1; k < n; k++)
            {
                rows[n][k] = rows[n - 1][k - 1] + rows[n - 1][k];
            }
        }

        return rows;
    }
}
