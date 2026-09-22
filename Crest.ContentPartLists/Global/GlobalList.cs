using YesSql.Indexes;

namespace Crest.Global.Lists;

/// <summary>
/// A standard list in the tenant-less store: the rows every tenant shares. Loaded from
/// the module's data file; edited only by the Default tenant. Tenants never write here -
/// their relabels, hides, ordering and additions are an overlay in their own store.
/// </summary>
public sealed class GlobalList
{
    public long Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    /// <summary>True when keys, values and categories are the machine contract logic evaluates against.</summary>
    public bool DataLock { get; set; }
    public List<GlobalOption> Options { get; set; } = [];
}

public sealed class GlobalOption
{
    public string Key { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public string? DisplayTextPlural { get; set; }
    public string? Value { get; set; }
    public string? Category { get; set; }
    public int Position { get; set; }
    /// <summary>"Standard" from the data file; "Host" when added by the Default tenant. The loader replaces only Standard rows.</summary>
    public string Source { get; set; } = GlobalOptionSources.Standard;
}

public static class GlobalOptionSources
{
    public const string Standard = "Standard";
    public const string Host = "Host";
}

public sealed class GlobalListIndex : MapIndex
{
    public string Key { get; set; } = string.Empty;
}

public sealed class GlobalListIndexProvider : IndexProvider<GlobalList>
{
    public override void Describe(DescribeContext<GlobalList> context) =>
        context.For<GlobalListIndex>().Map(list => new GlobalListIndex { Key = list.Key });
}

/// <summary>The ten lists that ship in the global store. Consumers name them by these keys.</summary>
public static class GlobalLists
{
    public const string CountryCodes = "global.country-codes";
    public const string UnitsOfMeasure = "global.uom";
    public const string PhoneCountryCodes = "global.phone-country-codes";
    public const string PostalFormats = "global.postal-formats";
    public const string Languages = "global.languages";
    public const string UsAreaCodes = "global.us-area-codes";
    public const string Subdivisions = "global.subdivisions";
    public const string Honorifics = "global.honorifics";
    public const string NameSuffixes = "global.name-suffixes";
    public const string UomRec20 = "global.uom-rec20";

    /// <summary>The ONE canonical item classification every consumer maps from (tax, GDSN). Ships empty until an open dataset (NAPCS candidate) is loaded; an unclassified item is undecidable, never silently untaxed (plans/fruitful-modules.md).</summary>
    public const string ItemBaseClassification = "items.base-classification";

    /// <summary>Deterministic, stable id for a global option - what pickers store instead of a tenant content item id.</summary>
    public static string OptionId(string listKey, string optionKey) => $"global:{listKey}:{optionKey}";

    public static bool IsGlobalOptionId(string? id) => id is not null && id.StartsWith("global:", StringComparison.Ordinal);
}
