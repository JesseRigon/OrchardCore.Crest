namespace Crest.Services;

using Crest.Models;

/// <summary>
/// The pure rules behind Option Lists - key normalization, uniqueness, seed layering
/// and effective-list projection. Kept free of Orchard content plumbing so the
/// behaviour that matters (a tenant's relabel surviving a module reseed, a hidden
/// option staying resolvable) is directly unit-testable.
/// </summary>
public static class CrestOptionListRules
{
    /// <summary>Keys are compared case-insensitively and stored trimmed; comparing
    /// them any other way would make "markup" and "Markup" two different options in
    /// one set.</summary>
    public static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

    public static string NormalizeKey(string? key) => key?.Trim() ?? string.Empty;

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
    /// Orders options for display: by position, then display text. Hidden options
    /// keep their place - callers decide whether to drop them (see
    /// <see cref="Selectable"/>), because history must still resolve them.
    /// </summary>
    public static IReadOnlyList<CrestOptionModel> Ordered(IEnumerable<CrestOptionModel> options) =>
        [.. options
            .OrderBy(option => option.Position)
            .ThenBy(option => option.DisplayText, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>The options an editor should offer: everything not hidden.</summary>
    public static IReadOnlyList<CrestOptionModel> Selectable(IEnumerable<CrestOptionModel> options) =>
        [.. Ordered(options).Where(option => !option.Hidden)];

    /// <summary>
    /// Layers a module's seed declaration over what the tenant currently has, and
    /// returns what should change. Existing options are matched BY KEY and never
    /// overwritten - a tenant's relabel or hide survives every reseed, which is the
    /// whole point of provenance. Only genuinely new keys are added.
    /// </summary>
    public static CrestOptionSeedPlan PlanSeed(CrestOptionListSeed seed, IEnumerable<CrestOptionModel> existing)
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
