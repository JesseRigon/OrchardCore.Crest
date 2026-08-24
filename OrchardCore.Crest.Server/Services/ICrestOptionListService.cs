using Crest.Models;

namespace Crest.Services;

/// <summary>
/// Crest's tenant-editable enum system. Modules DECLARE the sets they own and consume
/// them by logical key; tenants add, relabel, reorder and hide options at runtime.
/// </summary>
public interface ICrestOptionListService
{
    /// <summary>Every set in the tenant.</summary>
    Task<IReadOnlyList<CrestOptionListModel>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Resolves a set by its stable logical key (never by ContentItemId,
    /// which differs per tenant). Null when the set does not exist.</summary>
    Task<CrestOptionListModel?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>The options an editor should offer for a set: ordered, hidden ones
    /// dropped. Empty when the set does not exist.</summary>
    Task<IReadOnlyList<CrestOptionModel>> GetSelectableAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Translates a stored ContentItemId back to the option's technical key
    /// - the lookup content-driven code needs, since content stores ids while code
    /// compares keys. Null when the id is not an option of that set.</summary>
    Task<string?> ResolveKeyAsync(string listKey, string optionContentItemId, CancellationToken cancellationToken = default);

    /// <summary>Translates a technical key to the ContentItemId to store in a
    /// TaxonomyField. Null when the set has no such option.</summary>
    Task<string?> ResolveContentItemIdAsync(string listKey, string optionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a set if absent and adds any options it does not yet have, marking
    /// both as <see cref="CrestOptionSources.Module"/>. Idempotent, and it NEVER
    /// overwrites an existing option: a tenant's relabel or hide survives reseeding.
    /// This is how a module declares the lists it owns (call from a migration or a
    /// feature-enable path).
    /// </summary>
    Task<CrestOptionListModel> SeedAsync(CrestOptionListSeed seed, CancellationToken cancellationToken = default);

    /// <summary>Creates a tenant-owned set. Fails when the key is invalid or taken.</summary>
    Task<CrestOptionListModel> CreateListAsync(string key, string displayText, CancellationToken cancellationToken = default);

    /// <summary>Adds a tenant-owned option to a set. Fails when the key is invalid or
    /// already used in that set.</summary>
    Task<CrestOptionModel> AddOptionAsync(string listKey, string optionKey, string displayText, int position = 0, CancellationToken cancellationToken = default);

    /// <summary>Relabels, repositions, or hides/unhides an option. The technical key
    /// is deliberately NOT editable here - it is the module's contract with code.</summary>
    Task<CrestOptionModel> UpdateOptionAsync(string listKey, string optionKey, string? displayText, int? position, bool? hidden, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches a set to a content type as a TaxonomyField, so any type can reference
    /// an Option List at runtime.
    /// </summary>
    Task AttachToContentTypeAsync(string listKey, string contentType, string fieldName, string? displayName = null, CancellationToken cancellationToken = default);
}
