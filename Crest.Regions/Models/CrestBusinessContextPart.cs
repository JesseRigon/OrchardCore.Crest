using OrchardCore.ContentManagement;

namespace Crest.Regions.Models;

/// <summary>
/// What the context ITSELF declares: the geo-tied defaults that are true for the party
/// regardless of which features are enabled. Everything else (currency, tax, ...) is a
/// facet a downstream module attaches to the CrestBusinessContext type from its own
/// migration and reads back as optional - the context is a composition point.
/// </summary>
public class CrestBusinessContextPart : ContentPart
{
    /// <summary>Stable logical key, e.g. "us", "eu". Resolution and seeds address contexts by key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Level-1 geo node id (ISO 3166-1 alpha-2). Supplies the default country for a NEW address; the address's own country selects its map.</summary>
    public string? DefaultCountry { get; set; }

    /// <summary>"Metric" or "Imperial".</summary>
    public string? MeasurementSystem { get; set; }

    /// <summary>IANA time zone id.</summary>
    public string? TimeZone { get; set; }

    /// <summary>Default UI/document language for parties in this context (BCP 47).</summary>
    public string? Language { get; set; }

    public bool Enabled { get; set; } = true;
}

/// <summary>
/// The reference a party (or any content item) carries to its context. Attached by the
/// module that owns the referencing type; read by <see cref="Services.IBusinessContextResolver"/>.
/// </summary>
public class CrestBusinessContextReferencePart : ContentPart
{
    public string? BusinessContextId { get; set; }
}
