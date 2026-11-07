using System.Globalization;
using System.Numerics;
using System.Text;

namespace DuelLab.Simulator.Analysis;

/// <summary>
/// Decides whether a hand meets a <see cref="Scenario"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every role and card a scenario refers to is an <em>atom</em> with its own bit. A card's mask holds
/// the bits of the atoms it matches (its relevant roles, and its own bit if a requirement names it), and
/// a requirement's mask holds the bits of its targets; a card matches a requirement when the masks
/// intersect. Cards with the same mask are interchangeable, so a hand is summarised by how many of its
/// cards fall into each <em>group</em> of equal masks. The same code serves the Monte Carlo loop and
/// exact enumeration.
/// </para>
/// <para>
/// With distinct-card semantics, the minimums of a clause form a supply–demand problem: each card can
/// serve one requirement it matches. By Hall's theorem (supply–demand form) they can all be met if and
/// only if, for every subset S of requirements, the cards matching the union of S's targets are at least
/// as many as S needs in total. With at most <see cref="MaxRequirementsPerClause"/> requirements,
/// checking every subset is cheap and exact.
/// </para>
/// </remarks>
internal sealed class HandEvaluator
{
    public const int MaxRequirementsPerClause = 10;

    public const int MaxClausesPerScenario = 16;

    private readonly UInt128[] _groupMasks;
    private readonly CompiledClause[][] _scenarios;

    public HandEvaluator(UInt128[] groupMasks, IReadOnlyList<Scenario> scenarios, Func<Requirement, UInt128> maskOf)
    {
        _groupMasks = groupMasks;
        _scenarios = scenarios.Select(s => s.AnyOf.Select(c => Compile(c, maskOf)).ToArray()).ToArray();
    }

    public int ScenarioCount => _scenarios.Length;

    /// <summary>True when a hand with the given per-group card counts meets scenario <paramref name="index"/>.</summary>
    public bool Meets(int index, ReadOnlySpan<int> groupCounts)
    {
        foreach (var clause in _scenarios[index])
        {
            if (Meets(clause, groupCounts))
            {
                return true;
            }
        }

        return false;
    }

    private bool Meets(CompiledClause clause, ReadOnlySpan<int> groupCounts)
    {
        foreach (var (mask, maximum) in clause.Maximums)
        {
            if (CountMatching(mask, groupCounts) > maximum)
            {
                return false;
            }
        }

        if (!clause.RequireDistinct)
        {
            foreach (var (mask, minimum) in clause.Minimums)
            {
                if (CountMatching(mask, groupCounts) < minimum)
                {
                    return false;
                }
            }

            return true;
        }

        // Distinct cards: check Hall's condition for every non-empty subset of minimum requirements.
        for (var subset = 1; subset < clause.SubsetDemand.Length; subset++)
        {
            if (CountMatching(clause.SubsetMask[subset], groupCounts) < clause.SubsetDemand[subset])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A plain-language description of a scenario.</summary>
    /// <param name="scenario">The scenario.</param>
    /// <param name="cardName">Display name for a card key, or null if the card is not in the deck.</param>
    public static string Describe(Scenario scenario, Func<string, string?> cardName)
    {
        if (scenario.AnyOf.Count == 1)
        {
            return "Hand contains " + DescribeClause(scenario.AnyOf[0], cardName, inGroup: false);
        }

        return "Hand contains either " + string.Join(
            ", or ",
            scenario.AnyOf.Select(c => c.Requirements.Count > 1 ? $"({DescribeClause(c, cardName, inGroup: true)})" : DescribeClause(c, cardName, inGroup: true)));
    }

    private static string DescribeClause(Clause clause, Func<string, string?> cardName, bool inGroup)
    {
        var text = new StringBuilder(string.Join(" AND ", clause.Requirements.Select(r => DescribeRequirement(r, cardName))));

        // The distinct/overlapping choice only matters when two or more minimums compete for cards.
        if (clause.Requirements.Count(r => r.MinimumCount > 0) > 1)
        {
            text.Append(clause.RequireDistinctCards
                ? inGroup ? ", from different cards" : " (each minimum met by different cards)"
                : inGroup ? ", cards may count twice" : " (one card can count toward several roles)");
        }

        return text.ToString();
    }

    private static string DescribeRequirement(Requirement r, Func<string, string?> cardName)
    {
        var count = r.MaximumCount ?? r.MinimumCount;
        string target;
        if (r.Roles.Count == 1 && r.Cards.Count == 0)
        {
            target = RoleLabel(r.Roles[0], plural: count != 1);
        }
        else
        {
            var options = r.Cards.Select(k => cardName(k) ?? "a card not in the Main Deck")
                .Concat(r.Roles.Select(role => "any " + RoleLabel(role)))
                .ToList();
            target = "of " + (options.Count == 1 ? options[0] : string.Join(", ", options[..^1]) + " or " + options[^1]);
        }

        return r.MaximumCount switch
        {
            null => $"at least {r.MinimumCount} {target}",
            0 when r.MinimumCount == 0 => target.StartsWith("of ", StringComparison.Ordinal) ? $"none {target}" : $"no {target}",
            { } max when max == r.MinimumCount => $"exactly {max} {target}",
            { } max when r.MinimumCount == 0 => $"at most {max} {target}",
            { } max => $"{r.MinimumCount}–{max} {target}",
        };
    }

    internal static string RoleLabel(string roleKey, bool plural = false)
    {
        if (roleKey.StartsWith("Custom:", StringComparison.Ordinal))
        {
            // Custom names are the user's own words; quote them and leave them unchanged.
            return $"\"{roleKey["Custom:".Length..]}\" {(plural ? "cards" : "card")}";
        }

        var label = roleKey switch
        {
            "HandTrap" => "hand trap",
            "BoardBreaker" => "board breaker",
            _ => roleKey.ToLower(CultureInfo.InvariantCulture),
        };
        return plural ? label + "s" : label;
    }

    private int CountMatching(UInt128 mask, ReadOnlySpan<int> groupCounts)
    {
        var total = 0;
        for (var g = 0; g < groupCounts.Length; g++)
        {
            if ((_groupMasks[g] & mask) != UInt128.Zero)
            {
                total += groupCounts[g];
            }
        }

        return total;
    }

    private static CompiledClause Compile(Clause clause, Func<Requirement, UInt128> maskOf)
    {
        var minimums = clause.Requirements.Where(r => r.MinimumCount > 0).Select(r => (maskOf(r), r.MinimumCount)).ToArray();
        var maximums = clause.Requirements.Where(r => r.MaximumCount is not null).Select(r => (maskOf(r), r.MaximumCount!.Value)).ToArray();

        var subsetCount = 1 << minimums.Length;
        var subsetMask = new UInt128[subsetCount];
        var subsetDemand = new int[subsetCount];
        for (var subset = 1; subset < subsetCount; subset++)
        {
            var lowest = BitOperations.TrailingZeroCount(subset);
            var rest = subset & (subset - 1);
            subsetMask[subset] = subsetMask[rest] | minimums[lowest].Item1;
            subsetDemand[subset] = subsetDemand[rest] + minimums[lowest].MinimumCount;
        }

        return new CompiledClause(clause.RequireDistinctCards, minimums, maximums, subsetMask, subsetDemand);
    }

    private sealed record CompiledClause(
        bool RequireDistinct,
        (UInt128 Mask, int MinimumCount)[] Minimums,
        (UInt128 Mask, int MaximumCount)[] Maximums,
        UInt128[] SubsetMask,
        int[] SubsetDemand);
}
