using Crest.Services;
using Crest.Settings;
using Xunit;

namespace Crest.Server.Tests;

// The id->key translation behind cascading dropdowns: a picker parent's editor state
// carries stored ids, but dependent filters compare machine data (keys, values,
// categories), so parent rows project to a comparison column - Key by default.
public class OptionParentValueProjectionTests
{
    private static OptionRow Row(string id, string? key = null, params (string Path, string? Value)[] values) =>
        new(id, values.ToDictionary(entry => entry.Path, entry => entry.Value, StringComparer.OrdinalIgnoreCase)) { Key = key };

    [Fact]
    public void Default_column_projects_the_option_key()
    {
        var values = OptionParentValueResolver.Project([Row("id-1", "US"), Row("id-2", "CA")], null);

        Assert.Equal(new[] { "US", "CA" }, values);
    }

    [Fact]
    public void Entity_rows_without_keys_fall_back_to_their_id()
    {
        // A user's or content item's identity IS its id - the identity rule.
        var values = OptionParentValueResolver.Project([Row("user-7")], "Key");

        Assert.Equal(new[] { "user-7" }, values);
    }

    [Fact]
    public void A_named_column_projects_that_column()
    {
        var values = OptionParentValueResolver.Project(
            [Row("id-1", "US", ("Value", "+1")), Row("id-2", "AG", ("Value", "+1-268"))],
            "Value");

        Assert.Equal(new[] { "+1", "+1-268" }, values);
    }

    [Fact]
    public void Blank_and_duplicate_projections_drop()
    {
        var values = OptionParentValueResolver.Project(
            [Row("a", "US"), Row("b", "us"), Row("c", "  ")],
            null);

        Assert.Equal(new[] { "US", "c" }, values);
    }
}

public class OptionDependentFilterTranslatorTests
{
    private static OptionFilter Dependent(string valueFrom, string? column = null) => new()
    {
        Path = "Category",
        Operator = OptionFilterOperators.Equals,
        ValueFrom = valueFrom,
        ValueFromColumn = column,
    };

    private static Task<IReadOnlyList<string>> FakeResolve(string path, string? column, IReadOnlyList<string> raw) =>
        Task.FromResult<IReadOnlyList<string>>([.. raw.Select(value => $"KEY({value})")]);

    [Fact]
    public async Task Static_filters_pass_through_untouched()
    {
        var filters = new[] { new OptionFilter { Path = "Category", Value = "Mass" } };
        var (rewritten, state) = await OptionDependentFilterTranslator.TranslateAsync(filters, null, FakeResolve);

        Assert.Same(filters[0], rewritten[0]);
        Assert.Null(state);
    }

    [Fact]
    public async Task A_dependent_filter_is_cloned_onto_translated_values()
    {
        var original = Dependent("Organization.Country");
        var state = new Dictionary<string, IReadOnlyList<string>> { ["Organization.Country"] = ["id-1"] };

        var (rewritten, translated) = await OptionDependentFilterTranslator.TranslateAsync([original], state, FakeResolve);

        // The stored settings object is never mutated.
        Assert.Equal("Organization.Country", original.ValueFrom);
        Assert.NotEqual("Organization.Country", rewritten[0].ValueFrom);
        Assert.Equal(new[] { "KEY(id-1)" }, translated![rewritten[0].ValueFrom!]);
        // Resolving through the pure resolver yields the translated comparison value.
        var resolution = OptionFilterResolver.Resolve(rewritten, translated);
        Assert.Equal(new[] { "KEY(id-1)" }, resolution.Filters[0].Values);
    }

    [Fact]
    public async Task An_empty_parent_stays_empty_so_RequireParentValue_still_gates()
    {
        var (rewritten, translated) = await OptionDependentFilterTranslator.TranslateAsync(
            [Dependent("Organization.Country")], null, FakeResolve);

        var resolution = OptionFilterResolver.Resolve(rewritten, translated);
        Assert.True(resolution.HasUnmetDependencies);
    }

    [Fact]
    public async Task Two_filters_reading_different_columns_of_one_parent_get_distinct_keys()
    {
        var state = new Dictionary<string, IReadOnlyList<string>> { ["Country"] = ["id-1"] };
        var (rewritten, _) = await OptionDependentFilterTranslator.TranslateAsync(
            [Dependent("Country"), Dependent("Country", "Value")], state, FakeResolve);

        Assert.NotEqual(rewritten[0].ValueFrom, rewritten[1].ValueFrom);
    }
}

// The void-reason rule: a field shows only while its controlling field's resolved
// value satisfies the condition; hidden fields are cleared on save.
public class CrestFieldVisibilityRulesTests
{
    private static CrestFieldVisibilitySettings Condition(string @operator, params string[] keys) =>
        new() { Path = "Status", Operator = @operator, Keys = keys };

    [Fact]
    public void No_condition_means_always_visible()
    {
        Assert.True(CrestFieldVisibilityRules.IsVisible(null, []));
        Assert.True(CrestFieldVisibilityRules.IsVisible(new CrestFieldVisibilitySettings(), []));
    }

    [Fact]
    public void Any_shows_once_the_parent_has_a_value()
    {
        Assert.False(CrestFieldVisibilityRules.IsVisible(Condition(CrestFieldVisibilityOperators.Any), []));
        Assert.True(CrestFieldVisibilityRules.IsVisible(Condition(CrestFieldVisibilityOperators.Any), ["anything"]));
    }

    [Fact]
    public void Equals_matches_keys_case_insensitively()
    {
        var voided = Condition(CrestFieldVisibilityOperators.Equals, "Voided");

        Assert.True(CrestFieldVisibilityRules.IsVisible(voided, ["voided"]));
        Assert.False(CrestFieldVisibilityRules.IsVisible(voided, ["Posted"]));
        Assert.False(CrestFieldVisibilityRules.IsVisible(voided, []));
    }

    [Fact]
    public void In_matches_any_configured_key()
    {
        var settings = Condition(CrestFieldVisibilityOperators.In, "Voided", "Cancelled");

        Assert.True(CrestFieldVisibilityRules.IsVisible(settings, ["Cancelled"]));
        Assert.False(CrestFieldVisibilityRules.IsVisible(settings, ["Draft"]));
    }
}
