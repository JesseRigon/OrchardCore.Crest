using Crest.Models;

namespace Crest.Services;

/// <summary>
/// Crest's tenant-editable enum system. Modules DECLARE the sets they own and consume
/// them by logical key; tenants add, relabel, reorder and hide options at runtime.
/// </summary>
public interface ICrestContentPartListService
{
    /// <summary>Every set in the tenant.</summary>
    Task<IReadOnlyList<CrestContentPartListModel>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Resolves a set by its stable logical key (never by ContentItemId,
    /// which differs per tenant). Null when the set does not exist.</summary>
    Task<CrestContentPartListModel?> GetAsync(string key, CancellationToken cancellationToken = default);

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
    Task<CrestContentPartListModel> SeedAsync(CrestContentPartListSeed seed, CancellationToken cancellationToken = default);

    /// <summary>Creates a tenant-owned set. Fails when the key is invalid or taken.</summary>
    Task<CrestContentPartListModel> CreateListAsync(string key, string displayText, CancellationToken cancellationToken = default);

    /// <summary>Adds a tenant-owned option to a set. Fails when the key is invalid or
    /// already used in that set. A null or blank category files the option under
    /// Uncategorized.</summary>
    Task<CrestOptionModel> AddOptionAsync(string listKey, string optionKey, string displayText, int position = 0, string? category = null, CancellationToken cancellationToken = default);

    /// <summary>Relabels, repositions, recategorizes, or hides/unhides an option.
    /// Null leaves a value unchanged. The technical key is deliberately NOT editable
    /// here - it is the module's contract with code.</summary>
    Task<CrestOptionModel> UpdateOptionAsync(string listKey, string optionKey, string? displayText, int? position, bool? hidden, string? category = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rewrites the list's MANUAL order: each option's Position becomes its index in
    /// <paramref name="orderedKeys"/>, which must name every option exactly once.
    /// This is the only list-owned ordering - how an instance SORTS the list for
    /// display is the consuming picker's SortColumns, not list data.
    /// </summary>
    Task<CrestContentPartListModel> ReorderOptionsAsync(string key, IReadOnlyList<string> orderedKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Places or lifts TENANT-authority locks (null leaves a lock unchanged). Fails
    /// when the named lock was placed by the owning module - the module contract is
    /// out of the tenant's reach in both directions.
    /// NOTE: lock ENFORCEMENT lives at the tenant API surface (the controller);
    /// service methods trust their callers, because module code and migrations
    /// legitimately edit what tenants must not.
    /// </summary>
    Task<CrestContentPartListModel> UpdateListLocksAsync(string key, bool? dataLock, bool? editLock, CancellationToken cancellationToken = default);

    /// <summary>Deletes a tenant-owned set and the options contained in it. Fails for
    /// module-seeded sets - their keys are a module's contract, so they can only be
    /// hidden, never removed.</summary>
    Task DeleteListAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches a set to a content type as a TaxonomyField, so any type can reference
    /// an Option List at runtime.
    /// </summary>
    Task AttachToContentTypeAsync(string listKey, string contentType, string fieldName, string? displayName = null, CancellationToken cancellationToken = default);
}
