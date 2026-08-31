using System.Globalization;

namespace Crest.Services;

/// <summary>
/// Resolves which measurement system a culture uses and the preferred default unit
/// per dimension - the initial culture-to-units dataset, so localization (or later
/// per-tenant/per-user settings) can adapt units. Unit values are option KEYS in the
/// global.uom list; dimensions are its category names.
/// </summary>
public interface IMeasurementPreferenceService
{
    /// <summary>Preferences for an explicit culture, or for
    /// <see cref="CultureInfo.CurrentCulture"/> (the request's resolved culture,
    /// set by DisplayManager) when null/blank/unknown.</summary>
    MeasurementPreferences Resolve(string? cultureName = null);
}

/// <summary>The system a region measures in, per CLDR's measurement data. .NET's own
/// RegionInfo.IsMetric only knows metric-or-not; CLDR additionally separates the UK's
/// mixed system, so the three-value shape is kept even though the UK defaults below
/// currently match metric - refining them later is a data edit, not a shape change.</summary>
public static class MeasurementSystems
{
    public const string Metric = "Metric";
    public const string US = "US";
    public const string UK = "UK";
}

/// <summary>What a culture measures in: the system, and the preferred unit key per
/// dimension (keys into global.uom; dimensions are its categories).</summary>
public sealed record MeasurementPreferences(
    string Culture,
    string Region,
    string System,
    IReadOnlyDictionary<string, string> PreferredUnits);

public sealed class MeasurementPreferenceService : IMeasurementPreferenceService
{
    // CLDR measurementData: ussystem = US, Liberia, Myanmar; uksystem = GB;
    // everything else is metric.
    private static readonly HashSet<string> UsSystemRegions = new(["US", "LR", "MM"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> UkSystemRegions = new(["GB"], StringComparer.OrdinalIgnoreCase);

    // Preferred default unit per dimension, per system. Values are global.uom option
    // keys; dimension names match that list's categories. Count/Time have no
    // system-dependent variant but are included so a caller can resolve a default for
    // EVERY dimension from one map.
    private static readonly Dictionary<string, string> MetricUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mass"] = "kg",
        ["Volume"] = "l",
        ["Length"] = "m",
        ["Area"] = "m2",
        ["Temperature"] = "cel",
        ["Time"] = "hr",
        ["Count"] = "ea",
    };

    private static readonly Dictionary<string, string> UsUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mass"] = "lb",
        ["Volume"] = "gal",
        ["Length"] = "ft",
        ["Area"] = "ft2",
        ["Temperature"] = "fah",
        ["Time"] = "hr",
        ["Count"] = "ea",
    };

    // The UK's mixed system: metric for trade (which is what an ERP measures), so the
    // defaults track metric today; kept as its own table so refining (miles, pints)
    // is a one-line data change.
    private static readonly Dictionary<string, string> UkUnits = new(MetricUnits, StringComparer.OrdinalIgnoreCase);

    public MeasurementPreferences Resolve(string? cultureName = null)
    {
        var culture = ResolveCulture(cultureName);
        var region = ResolveRegion(culture);

        var system = region is not null && UsSystemRegions.Contains(region.TwoLetterISORegionName)
            ? MeasurementSystems.US
            : region is not null && UkSystemRegions.Contains(region.TwoLetterISORegionName)
                ? MeasurementSystems.UK
                : MeasurementSystems.Metric;

        var units = system switch
        {
            MeasurementSystems.US => UsUnits,
            MeasurementSystems.UK => UkUnits,
            _ => MetricUnits,
        };

        return new MeasurementPreferences(
            culture.Name,
            region?.TwoLetterISORegionName ?? string.Empty,
            system,
            units);
    }

    private static CultureInfo ResolveCulture(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return CultureInfo.CurrentCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(cultureName.Trim());
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.CurrentCulture;
        }
    }

    private static RegionInfo? ResolveRegion(CultureInfo culture)
    {
        // A neutral culture ("en", "es") has no region; walk to a specific culture
        // first so "en" resolves via its default specific culture rather than failing.
        try
        {
            var specific = culture.IsNeutralCulture
                ? CultureInfo.CreateSpecificCulture(culture.Name)
                : culture;
            return specific.Name.Length == 0 ? null : new RegionInfo(specific.Name);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
