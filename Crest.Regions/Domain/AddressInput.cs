namespace Crest.Regions.Models;

/// <summary>An address as entered, before geo resolution: text plus the node id chosen for each list-typed level.</summary>
public sealed record AddressInput(
    string Country,
    string? Line1,
    string? Line2,
    string? Locality,
    string? PostalCode,
    IReadOnlyDictionary<int, string?> LevelNodeIds)
{
    public string? NodeAt(int level) => LevelNodeIds.TryGetValue(level, out var id) ? id : null;
}

public sealed record AddressValidationError(string Field, string Message);
