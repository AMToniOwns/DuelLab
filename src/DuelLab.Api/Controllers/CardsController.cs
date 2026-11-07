using DuelLab.Application.Cards;
using DuelLab.Application.Common;
using DuelLab.Domain.Cards;
using Microsoft.AspNetCore.Mvc;

namespace DuelLab.Api.Controllers;

/// <summary>The card catalogue.</summary>
[ApiController]
[Route("api/cards")]
public sealed class CardsController(CardCatalogService catalog) : ControllerBase
{
    /// <summary>Searches cards by name, optionally filtered by Monster, Spell or Trap.</summary>
    [HttpGet]
    public Task<PagedResult<CardDto>> Search(
        [FromQuery] string? search,
        [FromQuery] CardKind? kind,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24) =>
        catalog.SearchAsync(search, kind, page, pageSize, cancellationToken);

    /// <summary>One card.</summary>
    [HttpGet("{id:int}")]
    public Task<CardDto> Get(int id, CancellationToken cancellationToken) => catalog.GetAsync(id, cancellationToken);

    /// <summary>A card's artwork, served from DuelLab's own copy.</summary>
    [HttpGet("{id:int}/image")]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Image(int id, CancellationToken cancellationToken, [FromQuery] CardImageSize size = CardImageSize.Small)
    {
        var image = await catalog.GetImageAsync(id, size, cancellationToken);
        return image is null ? NotFound() : PhysicalFile(image.Path, image.ContentType);
    }

    /// <summary>Number of cards and last import time.</summary>
    [HttpGet("status")]
    public Task<CatalogStatusDto> Status(CancellationToken cancellationToken) => catalog.GetStatusAsync(cancellationToken);

    /// <summary>Re-imports the catalogue from the provider (at most once an hour).</summary>
    [HttpPost("sync")]
    public Task<CatalogSyncResultDto> Sync(CancellationToken cancellationToken) => catalog.SyncAsync(force: false, cancellationToken);
}
