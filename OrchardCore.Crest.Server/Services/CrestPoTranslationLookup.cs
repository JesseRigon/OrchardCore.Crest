using System.Globalization;
using OrchardCore.Localization;

namespace Crest.Services;

/// <summary>
/// Shared PO-catalog lookup for the localization layering
/// (store &#8594; PO &#8594; invariant literal): matches a key against a culture's PO
/// catalogs context-insensitively, with a tier preference, and is used by the caption
/// resolver, the provider-menu seeder, and the translations editor so all three agree
/// on what PO would supply for a key.
/// </summary>
/// <remarks>
/// Context-insensitive by necessity, not preference: PO <c>msgctxt</c> values are
/// code-location scopes (the contributing class's full type name), which are
/// unrecoverable for a merged menu item (merging folds many contributors into one) and
/// unknowable for a Crest component (upstream never filed strings under Crest types).
/// The tiers reduce the homonym risk that a flat scan carries:
/// <list type="number">
/// <item>callers may prefer domain-affine contexts - for menu captions,
/// <c>msgctxt</c> values ending <c>.AdminMenu</c>, upstream's naming convention for
/// navigation provider classes (a convention heuristic, not a guarantee);</item>
/// <item>otherwise the flat most-common value wins, ordinally tie-broken for
/// determinism.</item>
/// </list>
/// Empty and identity (value == key) entries are skipped, so an untranslated entry can
/// never outvote a real translation - identity renders the same as the literal
/// fallback anyway. Any wrong pick is correctable above (a tenant edit in the
/// Translations editor) or within (an authored entry in one of our own .po files, which
/// callers can prefer via their own context tier).
/// </remarks>
internal static class CrestPoTranslationLookup
{
    /// <summary>Suffix of upstream navigation provider class names - the domain-affine
    /// context tier for menu captions.</summary>
    public const string AdminMenuContextSuffix = ".AdminMenu";

    /// <summary>
    /// Per-culture index: message id &#8594; every (msgctxt, value) pair carrying it.
    /// Iterates <c>Translations</c> directly, never the dictionary's enumerator, whose
    /// rebuilt records carry the composite "context|messageid" string as the message id.
    /// </summary>
    public static Dictionary<string, List<(string Context, string Value)>> BuildIndex(CultureDictionary dictionary)
    {
        var index = new Dictionary<string, List<(string, string)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, translations) in dictionary.Translations)
        {
            var value = translations is { Length: > 0 } ? translations[0] : null;
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, key.MessageId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!index.TryGetValue(key.MessageId, out var list))
            {
                index[key.MessageId] = list = [];
            }

            list.Add((key.Context ?? string.Empty, value));
        }

        return index;
    }

    /// <summary>
    /// Indexes for the culture and its parents, most specific first (es-ES, then es) -
    /// the same walk the PO localizer itself performs.
    /// </summary>
    public static List<Dictionary<string, List<(string Context, string Value)>>> BuildCultureChainIndexes(
        ILocalizationManager localizationManager,
        CultureInfo culture)
    {
        var indexes = new List<Dictionary<string, List<(string, string)>>>();
        for (; !string.IsNullOrEmpty(culture.Name); culture = culture.Parent)
        {
            indexes.Add(BuildIndex(localizationManager.GetDictionary(culture)));
        }

        return indexes;
    }

    /// <summary>
    /// The culture's best PO value for <paramref name="key"/>, or <c>null</c> when no
    /// catalog in the chain translates it. A hit at a more specific culture always wins
    /// over any parent-culture entry.
    /// </summary>
    /// <param name="preferredContexts">Tier 0: the item's RECORDED source contexts (the
    /// declaring provider classes, captured at sync time in upstream Merge's value-authority
    /// order). Walked in order, first context with a matching entry wins - reproducing
    /// exactly the translation upstream's merge would have displayed. Falls through to the
    /// suffix tier and the flat scan when empty or missing.</param>
    public static string? Resolve(
        IReadOnlyList<Dictionary<string, List<(string Context, string Value)>>> cultureChainIndexes,
        string key,
        string? preferContextSuffix,
        IReadOnlyList<string>? preferredContexts = null)
    {
        foreach (var index in cultureChainIndexes)
        {
            if (!index.TryGetValue(key, out var candidates) || candidates.Count == 0)
            {
                continue;
            }

            // Tier 0: exact recorded declarers, authority order, first hit wins.
            if (preferredContexts is { Count: > 0 })
            {
                foreach (var context in preferredContexts)
                {
                    var exact = candidates
                        .Where(candidate => string.Equals(candidate.Context, context, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (exact.Count > 0)
                    {
                        return MostCommon(exact);
                    }
                }
            }

            // Tier 1: domain-affine contexts (a convention heuristic, not a guarantee).
            var pool = candidates;
            if (!string.IsNullOrEmpty(preferContextSuffix))
            {
                var affine = candidates
                    .Where(candidate => candidate.Context.EndsWith(preferContextSuffix, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (affine.Count > 0)
                {
                    pool = affine;
                }
            }

            // Tier 2: flat most-common.
            return MostCommon(pool);
        }

        return null;
    }

    private static string MostCommon(List<(string Context, string Value)> pool) => pool
        .GroupBy(candidate => candidate.Value, StringComparer.Ordinal)
        .OrderByDescending(group => group.Count())
        .ThenBy(group => group.Key, StringComparer.Ordinal)
        .First().Key;
}
