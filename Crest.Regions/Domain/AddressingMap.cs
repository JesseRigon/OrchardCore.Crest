namespace Crest.Regions.Models;

/// <summary>What one level is called in one country and how it is entered.</summary>
public sealed class AddressingLevel
{
    public int Level { get; set; }
    public string Label { get; set; } = string.Empty;
    /// <summary>True: pick from the tree's nodes at this level. False: free text.</summary>
    public bool IsList { get; set; }
    public bool Required { get; set; }
    /// <summary>Whether the address FORM shows this level at all (a level can exist for tax and be invisible to entry).</summary>
    public bool OnForm { get; set; } = true;
}

public static class AddressingFields
{
    public const string Line1 = "Line1";
    public const string Line2 = "Line2";
    public const string Locality = "Locality";
    public const string PostalCode = "PostalCode";
    /// <summary>Prefix for a level field: "Level2", "Level3"...</summary>
    public const string LevelPrefix = "Level";

    public static bool IsLevel(string field, out int level)
    {
        level = 0;
        return field.StartsWith(LevelPrefix, StringComparison.Ordinal) && int.TryParse(field.AsSpan(LevelPrefix.Length), out level);
    }
}

/// <summary>Form field order and labels for one country.</summary>
public sealed class AddressingField
{
    /// <summary>One of <see cref="AddressingFields"/> or "Level{n}".</summary>
    public string Field { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool Required { get; set; }
}

/// <summary>The per-country addressing map: the pattern for both the UI and validation. Global-store reference data.</summary>
public sealed class AddressingMap
{
    public long Id { get; set; }
    public string Country { get; set; } = string.Empty;
    public List<AddressingLevel> Levels { get; set; } = [];
    public List<AddressingField> Fields { get; set; } = [];
    public string? PostalCodePattern { get; set; }
    public string PostalCodeLabel { get; set; } = "Postal code";
    /// <summary>Which level the free-text locality corresponds to, when the country treats city as a level (US 4). Null when locality is text only.</summary>
    public int? LocalityLevel { get; set; }
}
