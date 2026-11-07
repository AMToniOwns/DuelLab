namespace DuelLab.Simulator.Analysis;

/// <summary>An interval estimate for a probability.</summary>
public readonly record struct ConfidenceInterval(double Lower, double Upper, double Level);

/// <summary>Estimation helpers for Monte Carlo results.</summary>
public static class Statistics
{
    /// <summary>z for a two-sided 95% interval.</summary>
    public const double Z95 = 1.959963984540054;

    /// <summary>
    /// The 95% Wilson score interval for a binomial proportion. Unlike the textbook "estimate ± 1.96 SE"
    /// interval, it behaves well near 0% and 100% and never leaves [0, 1].
    /// </summary>
    public static ConfidenceInterval Wilson95(int successes, int trials)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(trials);
        ArgumentOutOfRangeException.ThrowIfNegative(successes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(successes, trials);

        const double z = Z95;
        double n = trials;
        var p = successes / n;
        var z2 = z * z;
        var denominator = 1 + (z2 / n);
        var centre = (p + (z2 / (2 * n))) / denominator;
        var halfWidth = z * Math.Sqrt((p * (1 - p) / n) + (z2 / (4 * n * n))) / denominator;

        return new ConfidenceInterval(Math.Max(0, centre - halfWidth), Math.Min(1, centre + halfWidth), 0.95);
    }
}
