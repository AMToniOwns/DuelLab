using DuelLab.Application.Decks;
using Microsoft.AspNetCore.Mvc;

namespace DuelLab.Api.Controllers;

/// <summary>Decks.</summary>
[ApiController]
[Route("api/decks")]
public sealed class DecksController(DeckService decks) : ControllerBase
{
    /// <summary>All decks.</summary>
    [HttpGet]
    public Task<IReadOnlyList<DeckSummaryDto>> List(CancellationToken cancellationToken) => decks.ListAsync(cancellationToken);

    /// <summary>Creates a deck with an empty first version.</summary>
    [HttpPost]
    public async Task<ActionResult<DeckSummaryDto>> Create(CreateDeckRequest request, CancellationToken cancellationToken)
    {
        var deck = await decks.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = deck.Id }, deck);
    }

    /// <summary>Creates a deck from a .ydk file's text.</summary>
    [HttpPost("import")]
    public async Task<ActionResult<DeckImportResultDto>> Import(ImportYdkRequest request, CancellationToken cancellationToken)
    {
        var result = await decks.ImportYdkAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Deck.Id }, result);
    }

    /// <summary>Reads a .ydk file's cards without saving anything (used to load a file into the editor).</summary>
    [HttpPost("ydk/resolve")]
    public Task<YdkResolutionDto> ResolveYdk(ResolveYdkRequest request, CancellationToken cancellationToken) =>
        decks.ResolveYdkAsync(request, cancellationToken);

    /// <summary>Adds a version to a deck from a .ydk file's text.</summary>
    [HttpPost("{id:guid}/versions/import")]
    public Task<DeckImportResultDto> ImportVersion(Guid id, ImportYdkVersionRequest request, CancellationToken cancellationToken) =>
        decks.ImportYdkVersionAsync(id, request, cancellationToken);

    /// <summary>One deck with its versions.</summary>
    [HttpGet("{id:guid}")]
    public Task<DeckSummaryDto> Get(Guid id, CancellationToken cancellationToken) => decks.GetAsync(id, cancellationToken);

    /// <summary>Renames a deck.</summary>
    [HttpPut("{id:guid}")]
    public Task<DeckSummaryDto> Rename(Guid id, RenameDeckRequest request, CancellationToken cancellationToken) =>
        decks.RenameAsync(id, request, cancellationToken);

    /// <summary>Sets the format: TCG Advanced (optionally pinned to a list snapshot) or Casual.</summary>
    [HttpPut("{id:guid}/format")]
    public Task<DeckSummaryDto> UpdateFormat(Guid id, UpdateFormatRequest request, CancellationToken cancellationToken) =>
        decks.UpdateFormatAsync(id, request, cancellationToken);

    /// <summary>Deletes a deck, its versions and their simulation history.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await decks.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Creates a version: empty, or a copy of <c>sourceVersionId</c>.</summary>
    [HttpPost("{id:guid}/versions")]
    public async Task<ActionResult<VersionDetailDto>> CreateVersion(Guid id, CreateVersionRequest request, CancellationToken cancellationToken)
    {
        var version = await decks.CreateVersionAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(VersionsController.Get), "Versions", new { id = version.Id }, version);
    }
}
