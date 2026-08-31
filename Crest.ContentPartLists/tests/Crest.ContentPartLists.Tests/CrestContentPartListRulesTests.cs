using Crest.Models;
using Crest.Services;
using Xunit;

namespace Crest.ContentPartLists.Tests;

public class OptionKeyValidationTests
{
    private static readonly string[] Existing = ["Markup", "Margin", "CostPlus"];

    [Fact]
    public void Accepts_a_new_unique_key() =>
        Assert.True(CrestContentPartListRules.ValidateKey("AdjustmentAmount", Existing).IsValid);

    [Fact]
    public void Rejects_a_duplicate_key() =>
        Assert.False(CrestContentPartListRules.ValidateKey("Margin", Existing).IsValid);

    [Fact]
    public void Duplicate_detection_ignores_case_and_surrounding_space()
    {
        // Orchard has no unique index for a content field, so this check is the only
        // thing standing between a tenant and two "Markup" options in one set.
        Assert.False(CrestContentPartListRules.ValidateKey("markup", Existing).IsValid);
        Assert.False(CrestContentPartListRules.ValidateKey("  MARKUP  ", Existing).IsValid);
    }

    [Fact]
    public void An_option_may_keep_its_own_key_when_edited() =>
        Assert.True(CrestContentPartListRules.ValidateKey("Margin", Existing, currentKey: "Margin").IsValid);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_missing_key(string? key) =>
        Assert.False(CrestContentPartListRules.ValidateKey(key, Existing).IsValid);

    [Theory]
    [InlineData("pricing.modifier-kind")]
    [InlineData("Net_30")]
    [InlineData("Net30")]
    public void Accepts_dotted_dashed_and_underscored_keys(string key) =>
        Assert.True(CrestContentPartListRules.ValidateKey(key, Existing).IsValid);

    [Theory]
    [InlineData("has space")]
    [InlineData("slash/es")]
    [InlineData("punctuation!")]
    public void Rejects_keys_outside_the_allowed_shape(string key) =>
        Assert.False(CrestContentPartListRules.ValidateKey(key, Existing).IsValid);

    [Fact]
    public void Invalid_results_carry_an_explanation() =>
        Assert.False(string.IsNullOrWhiteSpace(CrestContentPartListRules.ValidateKey("Margin", Existing).Error));
}

public class OptionOrderingTests
{
    private static CrestOptionModel Option(string key, string text, bool hidden = false, int position = 0, string category = "Uncategorized") =>
        new($"id-{key}", key, text, CrestOptionSources.Module, hidden, position, category);

    [Fact]
    public void Orders_by_position_then_display_text()
    {
        var ordered = CrestContentPartListRules.Ordered([
            Option("c", "Zulu", position: 2),
            Option("a", "Bravo", position: 1),
            Option("b", "Alpha", position: 1),
        ]);

        Assert.Equal(["Alpha", "Bravo", "Zulu"], ordered.Select(option => option.DisplayText));
    }

    [Fact]
    public void Ordered_keeps_hidden_options_so_history_can_still_resolve_them()
    {
        var ordered = CrestContentPartListRules.Ordered([Option("a", "Alpha"), Option("b", "Bravo", hidden: true)]);

        Assert.Equal(2, ordered.Count);
    }

    [Fact]
    public void Selectable_drops_hidden_options()
    {
        var selectable = CrestContentPartListRules.Selectable([Option("a", "Alpha"), Option("b", "Bravo", hidden: true)]);

        Assert.Equal(["Alpha"], selectable.Select(option => option.DisplayText));
    }

    [Fact]
    public void Selectable_preserves_the_order_it_receives()
    {
        // The model's options arrive in the list's manual (Position) order;
        // Selectable must only filter - any other ordering is the consuming
        // instance's SortColumns, applied later.
        var selectable = CrestContentPartListRules.Selectable([
            Option("z", "Zulu", position: 5),
            Option("a", "Alpha", position: 1),
        ]);

        Assert.Equal(["Zulu", "Alpha"], selectable.Select(option => option.DisplayText));
    }

}

public class ContentPartListLockTests
{
    private static CrestOptionModel Option(string key, string category) =>
        new($"id-{key}", key, key, CrestOptionSources.Module, false, 0, category);

    private static CrestContentPartListModel List(string dataLock, params CrestOptionModel[] options) =>
        new("id", "global.uom", "Units", CrestOptionSources.Module,
            dataLock, CrestContentPartListLockSources.None, options);

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("None", false)]
    [InlineData("none", false)]
    [InlineData("Tenant", true)]
    [InlineData("Module", true)]
    public void A_lock_is_active_unless_its_source_is_none(string? source, bool expected) =>
        Assert.Equal(expected, CrestContentPartListRules.LockActive(source));

    [Fact]
    public void An_unlocked_list_takes_any_added_category()
    {
        var list = List(CrestContentPartListLockSources.None, Option("kg", "Mass"));

        Assert.True(CrestContentPartListRules.ValidateAddedCategory(list, "Brand new").IsValid);
    }

    [Fact]
    public void A_data_locked_list_only_takes_existing_categories()
    {
        var list = List(CrestContentPartListLockSources.Module, Option("kg", "Mass"), Option("l", "Volume"));

        Assert.True(CrestContentPartListRules.ValidateAddedCategory(list, "Mass").IsValid);
        Assert.True(CrestContentPartListRules.ValidateAddedCategory(list, "  volume ").IsValid);
        Assert.False(CrestContentPartListRules.ValidateAddedCategory(list, "Made up").IsValid);
    }

    [Fact]
    public void A_data_locked_list_rejects_a_blank_category_unless_uncategorized_exists()
    {
        // Blank normalizes to Uncategorized; a locked list without that bucket must
        // refuse it, or tenant additions would escape the category coverage logic
        // depends on.
        var locked = List(CrestContentPartListLockSources.Module, Option("kg", "Mass"));
        Assert.False(CrestContentPartListRules.ValidateAddedCategory(locked, "").IsValid);

        var withBucket = List(CrestContentPartListLockSources.Module, Option("misc", "Uncategorized"));
        Assert.True(CrestContentPartListRules.ValidateAddedCategory(withBucket, null).IsValid);
    }

    [Fact]
    public void The_rejection_names_the_allowed_categories()
    {
        var list = List(CrestContentPartListLockSources.Tenant, Option("kg", "Mass"), Option("l", "Volume"));

        var validation = CrestContentPartListRules.ValidateAddedCategory(list, "Nope");
        Assert.Contains("Mass", validation.Error);
        Assert.Contains("Volume", validation.Error);
    }

    [Fact]
    public void The_category_vocabulary_is_distinct_and_case_insensitive()
    {
        var vocabulary = CrestContentPartListRules.CategoryVocabulary(
            [Option("a", "Mass"), Option("b", "mass"), Option("c", "Volume")]);

        Assert.Equal(2, vocabulary.Count);
    }
}

public class OptionNormalizationTests
{
    [Theory]
    [InlineData(null, "Uncategorized")]
    [InlineData("", "Uncategorized")]
    [InlineData("   ", "Uncategorized")]
    [InlineData("  Weight ", "Weight")]
    public void Categories_normalize_to_a_non_null_value(string? category, string expected) =>
        Assert.Equal(expected, CrestContentPartListRules.NormalizeCategory(category));

}

public class OptionReorderTests
{
    private static readonly string[] Existing = ["kg", "g", "lb"];

    [Fact]
    public void A_full_permutation_is_valid() =>
        Assert.True(CrestContentPartListRules.ValidateReorder(Existing, ["lb", "kg", "g"]).IsValid);

    [Fact]
    public void Matching_is_case_insensitive() =>
        Assert.True(CrestContentPartListRules.ValidateReorder(Existing, ["LB", "KG", "G"]).IsValid);

    [Fact]
    public void A_partial_payload_is_rejected() =>
        Assert.False(CrestContentPartListRules.ValidateReorder(Existing, ["kg", "g"]).IsValid);

    [Fact]
    public void A_stranger_key_is_rejected() =>
        Assert.False(CrestContentPartListRules.ValidateReorder(Existing, ["kg", "g", "oz"]).IsValid);

    [Fact]
    public void A_duplicate_key_is_rejected() =>
        Assert.False(CrestContentPartListRules.ValidateReorder(Existing, ["kg", "kg", "g"]).IsValid);

    [Fact]
    public void An_empty_payload_against_a_non_empty_list_is_rejected() =>
        Assert.False(CrestContentPartListRules.ValidateReorder(Existing, null).IsValid);
}

public class OptionRowSorterTests
{
    private static OptionRow Row(string id, string label, string? category = null) =>
        new(id, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["DisplayText"] = label,
            ["Category"] = category,
        });

    [Fact]
    public void No_sort_columns_preserves_the_incoming_order()
    {
        var rows = OptionRowSorter.Apply([Row("1", "Zulu"), Row("2", "Alpha")], []);

        Assert.Equal(["1", "2"], rows.Select(row => row.Id));
    }

    [Fact]
    public void Sorts_by_the_requested_column_culture_aware()
    {
        // Culture collation keeps "élan" with "e" where ordinal would push it past "z".
        var rows = OptionRowSorter.Apply(
            [Row("1", "zebra"), Row("2", "élan"), Row("3", "Apple")],
            ["DisplayText"]);

        Assert.Equal(["Apple", "élan", "zebra"], rows.Select(row => row.Values["DisplayText"]));
    }

    [Fact]
    public void Later_columns_break_ties_so_category_grouping_is_a_two_column_sort()
    {
        // The instance-layer spelling of "Categorized": ["Category", "DisplayText"].
        var rows = OptionRowSorter.Apply(
            [
                Row("kg", "Kilogram", "Mass"),
                Row("ea", "Each", "Count"),
                Row("g", "Gram", "Mass"),
            ],
            ["Category", "DisplayText"]);

        Assert.Equal(["ea", "g", "kg"], rows.Select(row => row.Id));
    }

    [Fact]
    public void A_column_absent_from_the_row_sorts_as_empty()
    {
        var rows = OptionRowSorter.Apply([Row("1", "B"), Row("2", "A")], ["Nonexistent", "DisplayText"]);

        Assert.Equal(["2", "1"], rows.Select(row => row.Id));
    }
}

public class OptionSeedLayeringTests
{
    private static CrestOptionModel Existing(string key, string text, bool hidden = false) =>
        new($"id-{key}", key, text, CrestOptionSources.Module, hidden, 0, "Uncategorized");

    private static CrestContentPartListSeed Seed(params string[] keys) =>
        new("pricing.modifier-kind", "Modifier kind", [.. keys.Select(key => new CrestOptionSeed(key, key))]);

    [Fact]
    public void Seeding_an_empty_list_adds_every_option()
    {
        var plan = CrestContentPartListRules.PlanSeed(Seed("Markup", "Margin"), []);

        Assert.Equal(["Markup", "Margin"], plan.Additions.Select(option => option.Key));
        Assert.True(plan.HasChanges);
    }

    [Fact]
    public void Reseeding_an_applied_seed_is_a_no_op()
    {
        var plan = CrestContentPartListRules.PlanSeed(Seed("Markup", "Margin"), [Existing("Markup", "Markup"), Existing("Margin", "Margin")]);

        Assert.False(plan.HasChanges);
    }

    [Fact]
    public void Only_genuinely_new_keys_are_added()
    {
        var plan = CrestContentPartListRules.PlanSeed(Seed("Markup", "Margin", "CostPlus"), [Existing("Markup", "Markup")]);

        Assert.Equal(["Margin", "CostPlus"], plan.Additions.Select(option => option.Key));
    }

    [Fact]
    public void A_tenants_relabel_survives_reseeding()
    {
        // The tenant renamed the module's "Markup" option; reseeding must not
        // resurrect the original label, so the option is not re-added at all.
        var plan = CrestContentPartListRules.PlanSeed(Seed("Markup"), [Existing("Markup", "Standard uplift")]);

        Assert.False(plan.HasChanges);
    }

    [Fact]
    public void A_hidden_module_option_is_not_re_added_by_reseeding()
    {
        var plan = CrestContentPartListRules.PlanSeed(Seed("Markup"), [Existing("Markup", "Markup", hidden: true)]);

        Assert.False(plan.HasChanges);
    }

    [Fact]
    public void Matching_is_case_insensitive_and_space_tolerant()
    {
        var plan = CrestContentPartListRules.PlanSeed(Seed("markup", "  Margin  "), [Existing("Markup", "Markup"), Existing("MARGIN", "Margin")]);

        Assert.False(plan.HasChanges);
    }

    [Fact]
    public void Duplicate_keys_within_one_seed_are_added_once()
    {
        var plan = CrestContentPartListRules.PlanSeed(Seed("Markup", "markup"), []);

        Assert.Single(plan.Additions);
    }

    [Fact]
    public void Blank_keys_in_a_seed_are_skipped()
    {
        var plan = CrestContentPartListRules.PlanSeed(new CrestContentPartListSeed("k", "K", [new CrestOptionSeed("  ", "Blank"), new CrestOptionSeed("Real", "Real")]), []);

        Assert.Equal(["Real"], plan.Additions.Select(option => option.Key));
    }

    [Fact]
    public void Added_keys_are_stored_trimmed()
    {
        var plan = CrestContentPartListRules.PlanSeed(Seed("  Markup  "), []);

        Assert.Equal("Markup", plan.Additions.Single().Key);
    }
}
