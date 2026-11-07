using DuelLab.Application.Banlists;
using Microsoft.AspNetCore.Mvc;

namespace DuelLab.Api.Controllers;

/// <summary>TCG Forbidden &amp; Limited list snapshots.</summary>
[ApiController]
[Route("api/banlists")]
public sealed class BanlistsController(BanlistService banlists) : ControllerBase
{
    /// <summary>All snapshots DuelLab has recorded, newest first.</summary>
    [HttpGet]
    public Task<IReadOnlyList<BanlistSummaryDto>> List(CancellationToken cancellationToken) => banlists.ListAsync(cancellationToken);

    /// <summary>The current list with card names.</summary>
    [HttpGet("current")]
    public Task<BanlistDto> Current(CancellationToken cancellationToken) => banlists.GetAsync(null, cancellationToken);

    /// <summary>One snapshot with card names.</summary>
    [HttpGet("{id:guid}")]
    public Task<BanlistDto> Get(Guid id, CancellationToken cancellationToken) => banlists.GetAsync(id, cancellationToken);
}
