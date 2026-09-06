using Crest.Settings;

namespace Crest.Services;

/// <summary>
/// Rewrites an attachment's dependent filters so their comparison values are the
/// PARENT'S COMPARISON VALUES rather than its raw editor values. This is what closes
/// the cascade gap: an OptionPickerField parent contributes stored ids, while the
/// child filter compares machine data (a Category holding a country code, a Value,
/// a Key) - so each dependent filter's values are resolved through
/// <see cref="OptionParentValueResolver"/> and re-keyed under a composite state key,
/// which also lets two filters read DIFFERENT columns of the same parent.
/// The pure rewrite is separated from the async resolution (a delegate) so the
/// re-keying and clone semantics are testable without providers.
/// </summary>
public static class OptionDependentFilterTranslator
{
    // Control character: cannot appear in a configured path, so composite keys can
    // never collide with a real editor-state entry.
    public const char KeySeparator = '\u0001';

    public static string TranslatedKey(string valueFrom, string? column) =>
        $"{valueFrom}{KeySeparator}{(string.IsNullOrWhiteSpace(column) ? OptionParentValueResolver.KeyColumn : column)}";

    /// <summary>
    /// Translates every dependent filter: its raw parent values (read from
    /// <paramref name="editorState"/> by ValueFrom) go through
    /// <paramref name="resolveParentValues"/> (path, column, raw values), and the
    /// filter is CLONED to read the translated values from a composite key. Filters
    /// and state the resolver does not touch pass through unchanged; the original
    /// state entries are kept, so untranslated consumers still see them.
    /// </summary>
    public static async Task<(IReadOnlyList<OptionFilter> Filters, IReadOnlyDictionary<string, IReadOnlyList<string>>? State)> TranslateAsync(
        IReadOnlyList<OptionFilter> filters,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? editorState,
        Func<string, string?, IReadOnlyList<string>, Task<IReadOnlyList<string>>> resolveParentValues)
    {
        if (!filters.Any(filter => filter.IsDependent))
        {
            return (filters, editorState);
        }

        var state = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (editorState is not null)
        {
            foreach (var (key, values) in editorState)
            {
                state[key] = values;
            }
        }

        var rewritten = new List<OptionFilter>(filters.Count);
        foreach (var filter in filters)
        {
            if (!filter.IsDependent)
            {
                rewritten.Add(filter);
                continue;
            }

            IReadOnlyList<string> raw = [];
            if (editorState is not null && editorState.TryGetValue(filter.ValueFrom!, out var values) && values is not null)
            {
                raw = [.. values.Where(value => !string.IsNullOrWhiteSpace(value))];
            }

            var key = TranslatedKey(filter.ValueFrom!, filter.ValueFromColumn);
            if (!state.ContainsKey(key))
            {
                // An empty parent stays empty - RequireParentValue semantics are the
                // resolver's to interpret, not ours to preempt.
                state[key] = raw.Count == 0
                    ? raw
                    : await resolveParentValues(filter.ValueFrom!, filter.ValueFromColumn, raw);
            }

            rewritten.Add(Clone(filter, key));
        }

        return (rewritten, state);
    }

    // OptionFilter is a settings POCO shared with the stored definition - never
    // mutate the instance the definition manager handed out.
    private static OptionFilter Clone(OptionFilter filter, string translatedValueFrom) => new()
    {
        Path = filter.Path,
        Operator = filter.Operator,
        Value = filter.Value,
        ValueFrom = translatedValueFrom,
        ValueFromColumn = filter.ValueFromColumn,
        RequireParentValue = filter.RequireParentValue,
        OnParentChange = filter.OnParentChange,
    };
}
