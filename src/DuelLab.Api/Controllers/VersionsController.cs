using DuelLab.Application.Decks;
using DuelLab.Application.Simulations;
using Microsoft.AspNetCore.Mvc;

namespace DuelLab.Api.Controllers;

/// <summary>Deck versions: contents, roles, simulation and comparison.</summary>
[ApiController]
[Route("api/versions")]
public sealed class VersionsController(DeckService decks, SimulationService simulations) : ControllerBase
{
    /// <summary>A version with its cards, roles and validation.</summary>
    [HttpGet("{id:guid}")]
    public Task<VersionDetailDto> Get(Guid id, CancellationToken cancellationToken) => decks.GetVersionAsync(id, cancellationToken);

    /// <summary>Changes a version's name and notes.</summary>
    [HttpPut("{id:guid}")]
    public Task<VersionDetailDto> Update(Guid id, UpdateVersionRequest request, CancellationToken cancellationToken) =>
        decks.UpdateVersionAsync(id, request, cancellationToken);

    /// <summary>Deletes a version (a deck keeps at least one).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await decks.DeleteVersionAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Replaces the version's card list.</summary>
    [HttpPut("{id:guid}/cards")]
    public Task<VersionDetailDto> UpdateCards(Guid id, UpdateCardsRequest request, CancellationToken cancellationToken) =>
        decks.ReplaceCardsAsync(id, request, cancellationToken);

    /// <summary>Replaces the version's role assignments.</summary>
    [HttpPut("{id:guid}/roles")]
    public Task<VersionDetailDto> UpdateRoles(Guid id, UpdateRolesRequest request, CancellationToken cancellationToken) =>
        decks.ReplaceRolesAsync(id, request, cancellationToken);

    /// <summary>Runs and stores an opening-hand simulation.</summary>
    [HttpPost("{id:guid}/simulate")]
    public Task<SimulationRunDto> Simulate(Guid id, SimulationConfigDto? request, CancellationToken cancellationToken) =>
        simulations.RunAsync(id, request, cancellationToken);

    /// <summary>The version's simulation history, newest first.</summary>
    [HttpGet("{id:guid}/simulations")]
    public Task<IReadOnlyList<SimulationRunSummaryDto>> History(Guid id, CancellationToken cancellationToken, [FromQuery] int take = 20) =>
        simulations.ListAsync(id, take, cancellationToken);

    /// <summary>Runs the same configuration and seed on two versions and reports the differences.</summary>
    [HttpPost("compare")]
    public Task<ComparisonDto> Compare(CompareRequest request, CancellationToken cancellationToken) =>
        simulations.CompareAsync(request, cancellationToken);
}
