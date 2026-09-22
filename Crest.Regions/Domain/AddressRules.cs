using System.Text.RegularExpressions;

namespace Crest.Regions.Models;

/// <summary>
/// The rule engine both sides run: structural validation of an address against its
/// country's map. Pure; anything needing the tree (does this node exist, is it beneath
/// that one) is the server's <c>IGeoService.ResolveStackAsync</c>, not this.
/// </summary>
public static class AddressRules
{
    public static IReadOnlyList<AddressValidationError> Validate(AddressingMap map, AddressInput address)
    {
        var errors = new List<AddressValidationError>();

        foreach (var field in map.Fields.OrderBy(field => field.Order))
        {
            if (!field.Required)
            {
                continue;
            }

            var present = field.Field switch
            {
                AddressingFields.Line1 => !string.IsNullOrWhiteSpace(address.Line1),
                AddressingFields.Line2 => !string.IsNullOrWhiteSpace(address.Line2),
                AddressingFields.Locality => !string.IsNullOrWhiteSpace(address.Locality),
                AddressingFields.PostalCode => !string.IsNullOrWhiteSpace(address.PostalCode),
                _ when AddressingFields.IsLevel(field.Field, out var level) =>
                    level == 1 ? !string.IsNullOrWhiteSpace(address.Country) : !string.IsNullOrWhiteSpace(address.NodeAt(level)),
                _ => true,
            };

            if (!present)
            {
                errors.Add(new AddressValidationError(field.Field, $"{field.Label} is required."));
            }
        }

        if (!string.IsNullOrWhiteSpace(address.PostalCode) && !string.IsNullOrWhiteSpace(map.PostalCodePattern))
        {
            var ok = Regex.IsMatch(address.PostalCode.Trim(), map.PostalCodePattern, RegexOptions.None, TimeSpan.FromMilliseconds(250));
            if (!ok)
            {
                errors.Add(new AddressValidationError(AddressingFields.PostalCode, $"'{address.PostalCode}' is not a valid {map.PostalCodeLabel} for {address.Country}."));
            }
        }

        return errors;
    }

    /// <summary>The fields the form shows for this country, in order.</summary>
    public static IReadOnlyList<AddressingField> FormFields(AddressingMap map) =>
        map.Fields.OrderBy(field => field.Order).ToArray();

    public static AddressingLevel? LevelDefinition(AddressingMap map, int level) =>
        map.Levels.FirstOrDefault(definition => definition.Level == level);
}
