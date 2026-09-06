using Crest.Settings;

namespace Crest.Services;

/// <summary>
/// The pure visibility decision, shared by the editor (live show/hide) and the save
/// path (clear-on-save). Kept free of content plumbing because "when exactly does the
/// void-reason field count as visible" is the part that must not drift between the
/// two call sites.
/// </summary>
public static class CrestFieldVisibilityRules
{
    /// <summary>
    /// Whether a field is visible given its condition and the controlling field's
    /// RESOLVED values (keys for picker parents, stored values for scalars).
    /// No condition means always visible. Comparison is case-insensitive, matching
    /// how option keys compare everywhere else.
    /// </summary>
    public static bool IsVisible(CrestFieldVisibilitySettings? settings, IReadOnlyList<string> parentValues)
    {
        if (settings is null || !settings.HasCondition)
        {
            return true;
        }

        if (string.Equals(settings.Operator, CrestFieldVisibilityOperators.Any, StringComparison.OrdinalIgnoreCase))
        {
            return parentValues.Count > 0;
        }

        // Equals and In are one test: any resolved parent value matching any key.
        return parentValues.Any(value => settings.Keys.Any(key =>
            string.Equals(key, value, StringComparison.OrdinalIgnoreCase)));
    }
}
