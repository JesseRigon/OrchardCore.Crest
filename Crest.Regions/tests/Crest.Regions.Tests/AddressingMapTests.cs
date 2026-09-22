using System.Text.Json;
using Crest.Regions.Models;
using Xunit;

namespace Crest.Regions.Tests;

/// <summary>
/// plans/regions-and-locations.md phase 4: a US, a UK and a French address each enter and
/// validate against the shipped map. Tree resolution (node exists, one per level) is the
/// server's and is not exercised here.
/// </summary>
public class AddressingMapTests
{
    private static readonly Dictionary<string, AddressingMap> Maps = Load();

    private static Dictionary<string, AddressingMap> Load()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Data", "addressing-maps.json"));
        var file = JsonSerializer.Deserialize<MapsFile>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        return file.Maps.ToDictionary(map => map.Country, StringComparer.Ordinal);
    }

    private sealed class MapsFile
    {
        public List<AddressingMap> Maps { get; set; } = [];
    }

    [Fact]
    public void A_US_address_validates_with_state_and_ZIP()
    {
        var errors = AddressRules.Validate(Maps["US"], new AddressInput("US", "233 S Wacker Dr", null, "Chicago", "60606",
            new Dictionary<int, string?> { [1] = "US", [2] = "US-IL" }));

        Assert.Empty(errors);
    }

    [Fact]
    public void A_US_address_without_a_state_or_with_a_bad_ZIP_is_rejected()
    {
        var noState = AddressRules.Validate(Maps["US"], new AddressInput("US", "233 S Wacker Dr", null, "Chicago", "60606", new Dictionary<int, string?> { [1] = "US" }));
        var badZip = AddressRules.Validate(Maps["US"], new AddressInput("US", "233 S Wacker Dr", null, "Chicago", "SW1A 1AA", new Dictionary<int, string?> { [1] = "US", [2] = "US-IL" }));

        Assert.Contains(noState, error => error.Field == "Level2");
        Assert.Contains(badZip, error => error.Field == AddressingFields.PostalCode);
    }

    [Fact]
    public void A_UK_address_validates_with_a_postcode_and_no_region_level()
    {
        var map = Maps["GB"];
        var errors = AddressRules.Validate(map, new AddressInput("GB", "10 Downing Street", null, "London", "SW1A 2AA", new Dictionary<int, string?> { [1] = "GB" }));

        Assert.Empty(errors);
        // The UK form never requires a county or nation pick: the only required level
        // above the country is the post town, which is free text (the Locality field).
        Assert.DoesNotContain(map.Levels, level => level.Level > 1 && level.Required && level.IsList);
    }

    [Fact]
    public void A_French_address_validates_with_a_five_digit_code_postal()
    {
        var errors = AddressRules.Validate(Maps["FR"], new AddressInput("FR", "55 Rue du Faubourg Saint-Honoré", null, "Paris", "75008", new Dictionary<int, string?> { [1] = "FR" }));
        var bad = AddressRules.Validate(Maps["FR"], new AddressInput("FR", "55 Rue du Faubourg Saint-Honoré", null, "Paris", "7500", new Dictionary<int, string?> { [1] = "FR" }));

        Assert.Empty(errors);
        Assert.Contains(bad, error => error.Field == AddressingFields.PostalCode);
    }

    [Fact]
    public void Every_shipped_map_labels_level_1_as_the_country()
    {
        Assert.All(Maps.Values, map => Assert.Contains(map.Levels, level => level.Level == 1));
    }
}
