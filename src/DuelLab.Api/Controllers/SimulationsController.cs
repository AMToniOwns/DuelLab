using DuelLab.Application.Simulations;
using Microsoft.AspNetCore.Mvc;

namespace DuelLab.Api.Controllers;

/// <summary>Stored simulation runs.</summary>
[ApiController]
[Route("api/simulations")]
public sealed class SimulationsController(SimulationService simulations) : ControllerBase
{
    /// <summary>Recent runs across all decks, newest first.</summary>
    [HttpGet]
    public Task<IReadOnlyList<SimulationRunSummaryDto>> Recent(CancellationToken cancellationToken, [FromQuery] int take = 10) =>
        simulations.ListAsync(null, take, cancellationToken);

    /// <summary>The scenarios used when a request names none.</summary>
    [HttpGet("default-scenarios")]
    public IReadOnlyList<ScenarioDto> DefaultScenarios() => SimulationService.DefaultScenarios;

    /// <summary>A stored run.</summary>
    [HttpGet("{id:guid}")]
    public Task<SimulationRunDto> Get(Guid id, CancellationToken cancellationToken) => simulations.GetAsync(id, cancellationToken);

    /// <summary>Re-runs a stored simulation from its saved configuration and checks the results match.</summary>
    [HttpPost("{id:guid}/reproduce")]
    public Task<ReproductionDto> Reproduce(Guid id, CancellationToken cancellationToken) => simulations.ReproduceAsync(id, cancellationToken);
}
