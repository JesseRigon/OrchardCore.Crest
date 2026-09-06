using System.Globalization;
using Crest.Services;
using Xunit;

namespace Crest.Tests;

// The platform-owned option sources (time zones, cultures): row projection,
// qualifier semantics, and the identity rule (id IS the technical key for both).
public class TimeZoneOptionSourceProviderTests
{
    [Fact]
    public void Rows_carry_id_as_key_and_requested_columns()
    {
        var zone = TimeZoneInfo.Utc;
        var row = TimeZoneOptionSourceProvider.ToRow(zone, [
            TimeZoneOptionSourceProvider.Columns.Id,
            TimeZoneOptionSourceProvider.Columns.DisplayName,
            TimeZoneOptionSourceProvider.Columns.Offset,
            TimeZoneOptionSourceProvider.Columns.SupportsDst,
        ]);

        Assert.Equal(zone.Id, row.Id);
        Assert.Equal(zone.Id, row.Key);
        Assert.Equal(zone.Id, row.Values[TimeZoneOptionSourceProvider.Columns.Id]);
        Assert.Equal("+00:00", row.Values[TimeZoneOptionSourceProvider.Columns.Offset]);
        Assert.Equal("false", row.Values[TimeZoneOptionSourceProvider.Columns.SupportsDst]);
    }

    [Theory]
    [InlineData(-5, "-05:00")]
    [InlineData(0, "+00:00")]
    [InlineData(5, "+05:00")]
    public void Offsets_format_signed(int hours, string expected) =>
        Assert.Equal(expected, TimeZoneOptionSourceProvider.FormatOffset(TimeSpan.FromHours(hours)));

    [Fact]
    public async Task Unknown_ids_are_absent_not_hollow()
    {
        var provider = new TimeZoneOptionSourceProvider();
        var rows = await provider.GetByIdsAsync("", ["UTC", "No/Such_Zone"], [TimeZoneOptionSourceProvider.Columns.Id], TestContext.Current.CancellationToken);

        Assert.Single(rows);
        Assert.Equal("UTC", rows[0].Id);
    }
}

public class CultureOptionSourceProviderTests
{
    [Fact]
    public void Rows_carry_name_as_id_and_key()
    {
        var row = CultureOptionSourceProvider.ToRow(CultureInfo.GetCultureInfo("en-US"), [
            CultureOptionSourceProvider.Columns.Name,
            CultureOptionSourceProvider.Columns.EnglishName,
            CultureOptionSourceProvider.Columns.TwoLetterIso,
        ]);

        Assert.Equal("en-US", row.Id);
        Assert.Equal("en-US", row.Key);
        Assert.Equal("en-US", row.Values[CultureOptionSourceProvider.Columns.Name]);
        Assert.Equal("en", row.Values[CultureOptionSourceProvider.Columns.TwoLetterIso]);
    }

    [Fact]
    public void Qualifier_selects_the_culture_set()
    {
        // The invariant culture (empty name) is never offered.
        Assert.DoesNotContain(CultureOptionSourceProvider.Cultures("all"), culture => culture.Name.Length == 0);

        // Specific (the default) excludes neutrals; neutral excludes specifics.
        Assert.DoesNotContain(CultureOptionSourceProvider.Cultures(null), culture => culture.IsNeutralCulture);
        Assert.All(CultureOptionSourceProvider.Cultures("neutral"), culture => Assert.True(culture.IsNeutralCulture));
    }

    [Fact]
    public async Task Unknown_ids_are_absent_not_hollow()
    {
        var provider = new CultureOptionSourceProvider();
        var rows = await provider.GetByIdsAsync("", ["en-US", "zz-NOPE-INVALID-!"], [CultureOptionSourceProvider.Columns.Name], TestContext.Current.CancellationToken);

        Assert.Single(rows);
        Assert.Equal("en-US", rows[0].Id);
    }
}
