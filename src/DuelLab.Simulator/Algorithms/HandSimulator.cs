namespace DuelLab.Simulator.Algorithms;

/// <summary>
/// Draws opening hands from a deck without replacement.
/// </summary>
/// <remarks>
/// <para>
/// The deck is a list of <em>physical card copies</em>: three copies of the same card appear as three
/// separate elements. The simulator is generic so it stays independent of the domain's card model.
/// </para>
/// <para>
/// Sampling uses a partial Fisher–Yates shuffle. It shuffles only the first <c>handSize</c> positions
/// of a private copy of the deck. Every ordered hand of that size is equally likely, as it would be
/// after a full shuffle followed by taking the top cards, but the work is O(handSize) swaps rather than
/// O(deck size).
/// </para>
/// <para>
/// The class holds no state and no randomness of its own. All randomness comes from the
/// <see cref="Random"/> passed in, so a seeded <see cref="Random"/> gives a reproducible result.
/// </para>
/// </remarks>
public sealed class HandSimulator
{
    /// <summary>
    /// Draws <paramref name="handSize"/> cards from <paramref name="deck"/> without replacement.
    /// </summary>
    /// <typeparam name="T">The type that represents one physical card copy.</typeparam>
    /// <param name="deck">The deck to draw from. It is never modified.</param>
    /// <param name="handSize">The number of cards to draw, from 0 to the deck size inclusive.</param>
    /// <param name="random">The source of randomness. Pass a seeded instance for reproducible draws.</param>
    /// <returns>A new list with the drawn cards, in draw order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="deck"/> or <paramref name="random"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="handSize"/> is negative or larger than the deck.
    /// </exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Instance member so the simulator can be injected and replaced behind an abstraction later.")]
    public IReadOnlyList<T> Draw<T>(IReadOnlyList<T> deck, int handSize, Random random)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegative(handSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(handSize, deck.Count);

        // Work on a copy so the caller's deck is never modified.
        var cards = deck.ToArray();

        // Partial Fisher–Yates (Durstenfeld's version, run from the front): position i takes a card
        // chosen uniformly from the cards not yet placed, at indices i..n-1. The upper bound of
        // Random.Next is exclusive. Choosing j from all of 0..n-1 instead is the classic mistake
        // that biases the result.
        for (var i = 0; i < handSize; i++)
        {
            var j = random.Next(i, cards.Length);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }

        return cards.AsSpan(0, handSize).ToArray();
    }
}
