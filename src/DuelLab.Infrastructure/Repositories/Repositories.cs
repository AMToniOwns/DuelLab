using DuelLab.Application.Common;
using DuelLab.Domain.Cards;
using DuelLab.Domain.Decks;
using DuelLab.Domain.Simulations;
using DuelLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DuelLab.Infrastructure.Repositories;

internal sealed class DeckRepository(DuelLabDbContext db) : IDeckRepository
{
    private IQueryable<Deck> Decks => db.Decks
        .Include(d => d.Versions).ThenInclude(v => v.Cards)
        .Include(d => d.Versions).ThenInclude(v => v.Roles)
        .Include(d => d.Versions).ThenInclude(v => v.ComboLines)
        .AsSplitQuery();

    public async Task<IReadOnlyList<Deck>> ListAsync(CancellationToken cancellationToken) =>
        await Decks.OrderByDescending(d => d.UpdatedAt).ToListAsync(cancellationToken);

    public Task<Deck?> GetAsync(Guid deckId, CancellationToken cancellationToken) =>
        Decks.FirstOrDefaultAsync(d => d.Id == deckId, cancellationToken);

    public Task<Deck?> GetByVersionIdAsync(Guid versionId, CancellationToken cancellationToken) =>
        Decks.FirstOrDefaultAsync(d => d.Versions.Any(v => v.Id == versionId), cancellationToken);

    public void Add(Deck deck) => db.Decks.Add(deck);

    public void Remove(Deck deck) => db.Decks.Remove(deck);
}

internal sealed class CardRepository(DuelLabDbContext db) : ICardRepository
{
    public async Task<Dictionary<int, Card>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToList();
        return list.Count == 0
            ? []
            : await db.Cards.Where(c => list.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Card> Items, int Total)> SearchAsync(string? search, CardKind? kind, int skip, int take, CancellationToken cancellationToken)
    {
        var query = db.Cards.AsNoTracking();
        if (kind is { } k)
        {
            query = query.Where(c => c.Kind == k);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search)}%";
            var prefix = $"{EscapeLike(search)}%";
            query = query
                .Where(c => EF.Functions.ILike(c.Name, pattern, "\\"))
                .OrderByDescending(c => EF.Functions.ILike(c.Name, EscapeLike(search), "\\"))
                .ThenByDescending(c => EF.Functions.ILike(c.Name, prefix, "\\"))
                .ThenBy(c => c.Name);
        }
        else
        {
            query = query.OrderBy(c => c.Name);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<Dictionary<long, Card>> GetAllByExternalIdAsync(CancellationToken cancellationToken) =>
        db.Cards.ToDictionaryAsync(c => c.ExternalId, cancellationToken);

    public async Task<(int Count, DateTimeOffset? LastSyncedAt)> GetStatusAsync(CancellationToken cancellationToken)
    {
        var count = await db.Cards.CountAsync(cancellationToken);
        var last = count == 0 ? null : await db.Cards.MaxAsync(c => (DateTimeOffset?)c.LastSyncedAt, cancellationToken);
        return (count, last);
    }

    public void AddRange(IEnumerable<Card> cards) => db.Cards.AddRange(cards);

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}

internal sealed class SimulationRunRepository(DuelLabDbContext db) : ISimulationRunRepository
{
    public Task<SimulationRun?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.SimulationRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SimulationRun>> ListAsync(Guid? deckVersionId, int take, CancellationToken cancellationToken)
    {
        var query = db.SimulationRuns.AsNoTracking();
        if (deckVersionId is { } id)
        {
            query = query.Where(r => r.DeckVersionId == id);
        }

        return await query.OrderByDescending(r => r.CreatedAt).Take(take).ToListAsync(cancellationToken);
    }

    public void Add(SimulationRun run) => db.SimulationRuns.Add(run);
}
