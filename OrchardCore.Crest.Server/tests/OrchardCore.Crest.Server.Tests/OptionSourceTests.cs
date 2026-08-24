using Crest.Models;
using Crest.Services;
using Crest.Settings;
using Xunit;

namespace Crest.Server.Tests;

public class OptionSourceKeyTests
{
    [Fact]
    public void Builds_a_qualified_key_for_an_option_list() =>
        Assert.Equal("optionlist:pricing.modifier-kind", CrestOptionSourceKeys.ForOptionList("pricing.modifier-kind"));

    [Fact]
    public void Splits_provider_from_qualifier()
    {
        var (provider, qualifier) = CrestOptionSourceKeys.Split("optionlist:pricing.side");

        Assert.Equal("optionlist", provider);
        Assert.Equal("pricing.side", qualifier);
    }

    [Fact]
    public void A_qualifier_may_itself_contain_a_colon()
    {
        // Only the FIRST colon separates provider from qualifier, so provider-specific
        // keys are free to use colons of their own.
        var (provider, qualifier) = CrestOptionSourceKeys.Split("contenttype:Item:SalesCategory");

        Assert.Equal("contenttype", provider);
        Assert.Equal("Item:SalesCategory", qualifier);
    }

    [Fact]
    public void A_bare_key_is_all_provider_and_no_qualifier()
    {
        var (provider, qualifier) = CrestOptionSourceKeys.Split("users");

        Assert.Equal("users", provider);
        Assert.Equal(string.Empty, qualifier);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_missing_key_splits_to_empties(string? sourceKey)
    {
        var (provider, qualifier) = CrestOptionSourceKeys.Split(sourceKey);

        Assert.Equal(string.Empty, provider);
        Assert.Equal(string.Empty, qualifier);
    }
}

public class OptionFilterMatcherTests
{
    private static OptionRow Row(params (string Path, string? Value)[] values) =>
        new("id", values.ToDictionary(pair => pair.Path, pair => pair.Value, StringComparer.OrdinalIgnoreCase));

    private static ResolvedOptionFilter Filter(string path, string op, params string[] values) =>
        new(path, op, values);

    [Fact]
    public void Equals_matches_case_insensitively() =>
        Assert.True(OptionFilterMatcher.Matches(Row(("Dept", "Sales")), Filter("Dept", OptionFilterOperators.Equals, "sales")));

    [Fact]
    public void Equals_rejects_a_different_value() =>
        Assert.False(OptionFilterMatcher.Matches(Row(("Dept", "Sales")), Filter("Dept", OptionFilterOperators.Equals, "Support")));

    [Fact]
    public void NotEquals_is_the_inverse() =>
        Assert.True(OptionFilterMatcher.Matches(Row(("Dept", "Sales")), Filter("Dept", OptionFilterOperators.NotEquals, "Support")));

    [Fact]
    public void In_matches_any_of_the_values() =>
        Assert.True(OptionFilterMatcher.Matches(Row(("Dept", "Sales")), Filter("Dept", OptionFilterOperators.In, "Support", "Sales")));

    [Fact]
    public void Contains_matches_a_substring() =>
        Assert.True(OptionFilterMatcher.Matches(Row(("Name", "Ada Lovelace")), Filter("Name", OptionFilterOperators.Contains, "love")));

    [Fact]
    public void GreaterThan_and_LessThan_compare_numerically()
    {
        // String comparison would call "9" greater than "10"; these must not.
        Assert.True(OptionFilterMatcher.Matches(Row(("Limit", "10")), Filter("Limit", OptionFilterOperators.GreaterThan, "9")));
        Assert.False(OptionFilterMatcher.Matches(Row(("Limit", "9")), Filter("Limit", OptionFilterOperators.GreaterThan, "10")));
        Assert.True(OptionFilterMatcher.Matches(Row(("Limit", "9")), Filter("Limit", OptionFilterOperators.LessThan, "10")));
    }

    [Fact]
    public void An_unknown_operator_keeps_the_row_rather_than_silently_dropping_it() =>
        Assert.True(OptionFilterMatcher.Matches(Row(("Dept", "Sales")), Filter("Dept", "SomethingUnsupported", "x")));

    [Fact]
    public void A_missing_column_is_treated_as_empty() =>
        Assert.False(OptionFilterMatcher.Matches(Row(("Dept", "Sales")), Filter("Absent", OptionFilterOperators.Equals, "x")));

    [Fact]
    public void Filters_are_combined_with_AND()
    {
        var rows = new[]
        {
            new OptionRow("a", new Dictionary<string, string?> { ["Dept"] = "Sales", ["Active"] = "true" }),
            new OptionRow("b", new Dictionary<string, string?> { ["Dept"] = "Sales", ["Active"] = "false" }),
            new OptionRow("c", new Dictionary<string, string?> { ["Dept"] = "Support", ["Active"] = "true" }),
        };

        var filtered = OptionFilterMatcher.Apply(rows,
        [
            Filter("Dept", OptionFilterOperators.Equals, "Sales"),
            Filter("Active", OptionFilterOperators.Equals, "true"),
        ]);

        Assert.Equal(["a"], filtered.Select(row => row.Id));
    }

    [Fact]
    public void No_filters_keeps_every_row()
    {
        var rows = new[] { Row(("Dept", "Sales")), Row(("Dept", "Support")) };

        Assert.Equal(2, OptionFilterMatcher.Apply(rows, null).Count);
        Assert.Equal(2, OptionFilterMatcher.Apply(rows, []).Count);
    }

    [Fact]
    public void Search_looks_only_at_the_named_columns()
    {
        var rows = new[]
        {
            new OptionRow("a", new Dictionary<string, string?> { ["Name"] = "Ada", ["Badge"] = "700" }),
            new OptionRow("b", new Dictionary<string, string?> { ["Name"] = "Grace", ["Badge"] = "ada-2" }),
        };

        var found = OptionFilterMatcher.Search(rows, "ada", ["Name"]);

        Assert.Equal(["a"], found.Select(row => row.Id));
    }

    [Fact]
    public void Search_falls_back_to_every_column_when_none_are_named()
    {
        var rows = new[]
        {
            new OptionRow("a", new Dictionary<string, string?> { ["Name"] = "Ada", ["Badge"] = "700" }),
            new OptionRow("b", new Dictionary<string, string?> { ["Name"] = "Grace", ["Badge"] = "ada-2" }),
        };

        Assert.Equal(2, OptionFilterMatcher.Search(rows, "ada", null).Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_search_keeps_every_row(string? searchText)
    {
        var rows = new[] { Row(("Name", "Ada")), Row(("Name", "Grace")) };

        Assert.Equal(2, OptionFilterMatcher.Search(rows, searchText, null).Count);
    }
}
