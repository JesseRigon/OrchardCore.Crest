namespace Crest.Services;

using Crest.Models;

/// <summary>
/// The pure rules behind Content Part Lists - key normalization, uniqueness, seed layering
/// and effective-list projection. Kept free of Orchard content plumbing so the
/// behaviour that matters (a tenant's relabel surviving a module reseed, a hidden
/// option staying resolvable) is directly unit-testable.
/// </summary>
public static class CrestContentPartListRules
{
    /// <summary>Keys are compared case-insensitively and stored trimmed; comparing
    /// them any other way would make "markup" and "Markup" two different options in
    /// one set.</summary>
    public static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

    public static string NormalizeKey(string? key) => key?.Trim() ?? string.Empty;

    /// <summary>Categories are labels, not keys: trimmed as typed, and blank means
    /// the option belongs to the default Uncategorized bucket. Never returns
    /// null or empty.</summary>
    public static string NormalizeCategory(string? category)
    {
        var value = category?.Trim();
        return string.IsNullOrEmpty(value) ? CrestOptionCategories.Uncategorized : value;
    }

    /// <summary>
    /// Validates a full manual reorder: the proposed keys must be exactly the list's
    /// current keys (same set, no omissions, no strangers, no duplicates). Anything
    /// less would let a partial payload silently collapse positions.
    /// </summary>
    public static CrestOptionKeyValidation ValidateReorder(IEnumerable<string> existingKeys, IReadOnlyList<string>? proposedKeys)
    {
        var existing = new HashSet<string>(existingKeys.Select(NormalizeKey), KeyComparer);
        var proposed = (proposedKeys ?? []).Select(NormalizeKey).ToArray();

        if (proposed.Length != existing.Count || proposed.Distinct(KeyComparer).Count() != proposed.Length)
        {
            return CrestOptionKeyValidation.Invalid("A reorder must name every option in the list exactly once.");
        }

        return proposed.All(existing.Contains)
            ? CrestOptionKeyValidation.Valid
            : CrestOptionKeyValidation.Invalid("A reorder may only name options that are in the list.");
    }

    /// <summary>
    /// Validates a key about to be written into <paramref name="existingKeys"/>.
    /// Orchard has no unique index for a content field, so uniqueness is enforced
    /// here, on write.
    /// </summary>
    /// <param name="key">The candidate key.</param>
    /// <param name="existingKeys">Keys already present in the set.</param>
    /// <param name="currentKey">The key being replaced, when editing an existing
    /// option - lets an option keep its own key.</param>
    public static CrestOptionKeyValidation ValidateKey(string? key, IEnumerable<string> existingKeys, string? currentKey = null)
    {
        var candidate = NormalizeKey(key);
        if (candidate.Length == 0)
        {
            return CrestOptionKeyValidation.Invalid("A key is required.");
        }

        // The key travels in URLs and is compared in code; keep it to a predictable
        // shape rather than accepting arbitrary display text.
        foreach (var character in candidate)
        {
            if (!char.IsLetterOrDigit(character) && character is not ('.' or '-' or '_'))
            {
                return CrestOptionKeyValidation.Invalid("A key may only contain letters, digits, dot, dash and underscore.");
            }
        }

        var current = NormalizeKey(currentKey);
        if (current.Length > 0 && KeyComparer.Equals(candidate, current))
        {
            return CrestOptionKeyValidation.Valid;
        }

        return existingKeys.Any(existing => KeyComparer.Equals(NormalizeKey(existing), candidate))
            ? CrestOptionKeyValidation.Invalid($"The key '{candidate}' is already used in this list.")
            : CrestOptionKeyValidation.Valid;
    }

    /// <summary>
    /// Orders options in the list's MANUAL order: Position, ties broken by display
    /// text (current culture). This is the list's only intrinsic order - any other
    /// sort (alphabetical, categorized, by key) is an INSTANCE parameter applied by
    /// the consuming picker via its SortColumns, not list data. Hidden options keep
    /// their place - callers decide whether to drop them (see
    /// <see cref="Selectable"/>), because history must still resolve them.
    /// </summary>
    public static IReadOnlyList<CrestOptionModel> Ordered(IEnumerable<CrestOptionModel> options) =>
        [.. options
            .OrderBy(option => option.Position)
            .ThenBy(option => option.DisplayText, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>The options an editor should offer: everything not hidden, in the
    /// order they arrive - the model's options already carry the list's configured
    /// sort, so re-sorting here would override it.</summary>
    public static IReadOnlyList<CrestOptionModel> Selectable(IEnumerable<CrestOptionModel> options) =>
        [.. options.Where(option => !option.Hidden)];

    /// <summary>Whether a lock value means the lock is active, regardless of who
    /// placed it.</summary>
    public static bool LockActive(string? lockSource) =>
        !string.IsNullOrEmpty(lockSource)
        && !string.Equals(lockSource, CrestContentPartListLockSources.None, StringComparison.OrdinalIgnoreCase);

    /// <summary>The category vocabulary of a data-locked list: the distinct
    /// categories its options already carry. Under a data lock this set is frozen -
    /// a tenant-added option must pick from it.</summary>
    public static IReadOnlyList<string> CategoryVocabulary(IEnumerable<CrestOptionModel> options) =>
        [.. options
            .Select(option => NormalizeCategory(option.Category))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(category => category, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>
    /// Validates the category of an option being ADDED to a list. Unlocked lists take
    /// any category (blank files under Uncategorized); a data-locked list only takes
    /// one of its existing categories, so logic keyed on the category set still
    /// covers every tenant addition.
    /// </summary>
    public static CrestOptionKeyValidation ValidateAddedCategory(CrestContentPartListModel list, string? category)
    {
        if (!LockActive(list.DataLock))
        {
            return CrestOptionKeyValidation.Valid;
        }

        var normalized = NormalizeCategory(category);
        var vocabulary = CategoryVocabulary(list.Options);
        return vocabulary.Any(existing => string.Equals(existing, normalized, StringComparison.CurrentCultureIgnoreCase))
            ? CrestOptionKeyValidation.Valid
            : CrestOptionKeyValidation.Invalid(
                $"This list's categories are locked. Pick one of: {string.Join(", ", vocabulary)}.");
    }

    /// <summary>
    /// Layers a module's seed declaration over what the tenant currently has, and
    /// returns what should change. Existing options are matched BY KEY and never
    /// overwritten - a tenant's relabel or hide survives every reseed, which is the
    /// whole point of provenance. Only genuinely new keys are added.
    /// </summary>
    public static CrestOptionSeedPlan PlanSeed(CrestContentPartListSeed seed, IEnumerable<CrestOptionModel> existing)
    {
        var present = new HashSet<string>(existing.Select(option => NormalizeKey(option.Key)), KeyComparer);
        var additions = new List<CrestOptionSeed>();
        foreach (var option in seed.Options)
        {
            var key = NormalizeKey(option.Key);
            if (key.Length == 0 || !present.Add(key))
            {
                continue;
            }

            additions.Add(option with { Key = key });
        }

        return new CrestOptionSeedPlan(additions);
    }
}

/// <summary>What a reseed would add. Empty additions means the seed is fully applied
/// and reseeding is a no-op, so module seeding stays idempotent.</summary>
public sealed record CrestOptionSeedPlan(IReadOnlyList<CrestOptionSeed> Additions)
{
    public bool HasChanges => Additions.Count > 0;
}
