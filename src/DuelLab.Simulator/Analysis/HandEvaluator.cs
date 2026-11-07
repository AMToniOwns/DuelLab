using System.Globalization;
using System.Numerics;
using System.Text;

namespace DuelLab.Simulator.Analysis;

/// <summary>
/// Decides whether a hand meets a <see cref="Scenario"/>.
/// </summary>
/// <remarks>
/// <para>
/// A hand is summarised by how many of its cards fall into each <em>role group</em>: cards with exactly
/// the same set of (relevant) roles are interchangeable for evaluation. Working on group counts rather
/// than individual cards lets the same code serve both the Monte Carlo loop and exact enumeration.
/// </para>
/// <para>
/// With distinct-card semantics, minimum requirements form a supply–demand problem: each card can serve
/// one requirement whose role it has. By Hall's theorem (in its supply–demand form) the requirements
/// can all be met if and only if, for every subset S of requirements, the cards that can serve some
/// requirement in S are at least as many as S needs in total. With at most
/// <see cref="MaxRequirementsPerScenario"/> requirements, checking every subset is cheap and exact.
/// </para>
/// </remarks>
internal sealed class HandEvaluator
{
    public const int MaxRequirementsPerScenario = 10;

    private readonly ulong[] _groupMasks;
    private readonly CompiledScenario[] _scenarios;

    public HandEvaluator(ulong[] groupMasks, IReadOnlyList<Scenario> scenarios, IReadOnlyDictionary<string, int> roleBits)
    {
        _groupMasks = groupMasks;
        _scenarios = scenarios.Select(s => Compile(s, roleBits)).ToArray();
    }

    public int ScenarioCount => _scenarios.Length;

    /// <summary>True when a hand with the given per-group card counts meets scenario <paramref name="index"/>.</summary>
    public bool Meets(int index, ReadOnlySpan<int> groupCounts)
    {
        var scenario = _scenarios[index];

        foreach (var (mask, maximum) in scenario.Maximums)
        {
            if (CountMatching(mask, groupCounts) > maximum)
            {
                return false;
            }
        }

        if (!scenario.RequireDistinct)
        {
            foreach (var (mask, minimum) in scenario.Minimums)
            {
                if (CountMatching(mask, groupCounts) < minimum)
                {
                    return false;
                }
            }

            return true;
        }

        // Distinct cards: check Hall's condition for every non-empty subset of minimum requirements.
        for (var subset = 1; subset < scenario.SubsetDemand.Length; subset++)
        {
            if (CountMatching(scenario.SubsetMask[subset], groupCounts) < scenario.SubsetDemand[subset])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A plain-language description of a scenario.</summary>
    public static string Describe(Scenario scenario)
    {
        var parts = scenario.Requirements.Select(r =>
        {
            string Role(int n) => RoleLabel(r.Role, plural: n != 1);
            return r.MaximumCount switch
            {
                null => $"at least {r.MinimumCount} {Role(r.MinimumCount)}",
                0 when r.MinimumCount == 0 => $"no {Role(0)}",
                { } max when max == r.MinimumCount => $"exactly {max} {Role(max)}",
                { } max when r.MinimumCount == 0 => $"at most {max} {Role(max)}",
                { } max => $"{r.MinimumCount}–{max} {Role(max)}",
            };
        }).ToList();

        var text = new StringBuilder("Hand contains ");
        text.Append(string.Join(" AND ", parts));

        var minimums = scenario.Requirements.Count(r => r.MinimumCount > 0);
        if (minimums > 1 || scenario.Requirements.Any(r => r.MinimumCount > 1))
        {
            text.Append(scenario.RequireDistinctCards
                ? " (each minimum met by different cards)"
                : " (one card can count toward several roles)");
        }

        return text.ToString();
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

    private int CountMatching(ulong roleMask, ReadOnlySpan<int> groupCounts)
    {
        var total = 0;
        for (var g = 0; g < groupCounts.Length; g++)
        {
            if ((_groupMasks[g] & roleMask) != 0)
            {
                total += groupCounts[g];
            }
        }

        return total;
    }

    private static CompiledScenario Compile(Scenario scenario, IReadOnlyDictionary<string, int> roleBits)
    {
        ulong Bit(string role) => 1UL << roleBits[role];

        var minimums = scenario.Requirements.Where(r => r.MinimumCount > 0).Select(r => (Bit(r.Role), r.MinimumCount)).ToArray();
        var maximums = scenario.Requirements.Where(r => r.MaximumCount is not null).Select(r => (Bit(r.Role), r.MaximumCount!.Value)).ToArray();

        var subsetCount = 1 << minimums.Length;
        var subsetMask = new ulong[subsetCount];
        var subsetDemand = new int[subsetCount];
        for (var subset = 1; subset < subsetCount; subset++)
        {
            var lowest = BitOperations.TrailingZeroCount(subset);
            var rest = subset & (subset - 1);
            subsetMask[subset] = subsetMask[rest] | minimums[lowest].Item1;
            subsetDemand[subset] = subsetDemand[rest] + minimums[lowest].MinimumCount;
        }

        return new CompiledScenario(scenario.RequireDistinctCards, minimums, maximums, subsetMask, subsetDemand);
    }

    private sealed record CompiledScenario(
        bool RequireDistinct,
        (ulong Mask, int MinimumCount)[] Minimums,
        (ulong Mask, int MaximumCount)[] Maximums,
        ulong[] SubsetMask,
        int[] SubsetDemand);
}
