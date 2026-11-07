using DuelLab.Simulator.Analysis;

namespace DuelLab.Simulator.Tests.Analysis;

public sealed class CombinatoricsTests
{
    [Theory]
    [InlineData(40, 5, 658_008)]
    [InlineData(31, 5, 169_911)]
    [InlineData(60, 6, 50_063_860)]
    [InlineData(5, 0, 1)]
    [InlineData(5, 5, 1)]
    [InlineData(5, 6, 0)]
    [InlineData(5, -1, 0)]
    public void Choose_KnownValues(int n, int k, long expected)
    {
        Assert.Equal((UInt128)expected, Combinatorics.Choose(n, k));
    }

    [Fact]
    public void Choose_IsExactForTheLargestSupportedDeck()
    {
        // C(60, 30) = 118,264,581,564,861,424 (exceeds double precision, fits in UInt128).
        Assert.Equal(UInt128.Parse("118264581564861424", System.Globalization.CultureInfo.InvariantCulture), Combinatorics.Choose(60, 30));
        Assert.Equal(Combinatorics.Choose(120, 50), Combinatorics.Choose(120, 70));
    }

    [Fact]
    public void HypergeometricAtLeastOne_MatchesSpecExample()
    {
        // Spec section 6.1: 1 - C(31,5)/C(40,5).
        var expected = 1.0 - (169_911.0 / 658_008.0);

        Assert.Equal(expected, Combinatorics.HypergeometricAtLeast(40, 9, 5, 1), precision: 15);
    }

    [Fact]
    public void Hypergeometric_EdgeCases()
    {
        Assert.Equal(1.0, Combinatorics.HypergeometricAtLeast(40, 9, 5, 0));
        Assert.Equal(0.0, Combinatorics.HypergeometricAtLeast(40, 0, 5, 1));
        Assert.Equal(1.0, Combinatorics.HypergeometricAtLeast(40, 40, 5, 5));
        Assert.Equal(0.0, Combinatorics.HypergeometricAtLeast(40, 9, 5, 6));
    }
}

public sealed class StatisticsTests
{
    [Fact]
    public void Wilson_HalfwayCase()
    {
        var ci = Statistics.Wilson95(50, 100);

        Assert.Equal(0.4038, ci.Lower, precision: 4);
        Assert.Equal(0.5962, ci.Upper, precision: 4);
    }

    [Fact]
    public void Wilson_ZeroSuccessesHasPositiveUpperBound()
    {
        var ci = Statistics.Wilson95(0, 10);

        Assert.Equal(0.0, ci.Lower);
        Assert.Equal(0.2775, ci.Upper, precision: 4);
    }

    [Fact]
    public void Wilson_AllSuccessesStaysWithinOne()
    {
        var ci = Statistics.Wilson95(10, 10);

        Assert.Equal(1.0, ci.Upper, precision: 12);
        Assert.Equal(0.7225, ci.Lower, precision: 4);
    }
}
