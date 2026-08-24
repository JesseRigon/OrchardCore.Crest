using Crest.Models;
using Crest.Services;
using Xunit;

namespace Crest.Server.Tests;

public class OptionKeyValidationTests
{
    private static readonly string[] Existing = ["Markup", "Margin", "CostPlus"];

    [Fact]
    public void Accepts_a_new_unique_key() =>
        Assert.True(CrestOptionListRules.ValidateKey("AdjustmentAmount", Existing).IsValid);

    [Fact]
    public void Rejects_a_duplicate_key() =>
        Assert.False(CrestOptionListRules.ValidateKey("Margin", Existing).IsValid);

    [Fact]
    public void Duplicate_detection_ignores_case_and_surrounding_space()
    {
        // Orchard has no unique index for a content field, so this check is the only
        // thing standing between a tenant and two "Markup" options in one set.
        Assert.False(CrestOptionListRules.ValidateKey("markup", Existing).IsValid);
        Assert.False(CrestOptionListRules.ValidateKey("  MARKUP  ", Existing).IsValid);
    }

    [Fact]
    public void An_option_may_keep_its_own_key_when_edited() =>
        Assert.True(CrestOptionListRules.ValidateKey("Margin", Existing, currentKey: "Margin").IsValid);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_missing_key(string? key) =>
        Assert.False(CrestOptionListRules.ValidateKey(key, Existing).IsValid);

    [Theory]
    [InlineData("pricing.modifier-kind")]
    [InlineData("Net_30")]
    [InlineData("Net30")]
    public void Accepts_dotted_dashed_and_underscored_keys(string key) =>
        Assert.True(CrestOptionListRules.ValidateKey(key, Existing).IsValid);

    [Theory]
    [InlineData("has space")]
    [InlineData("slash/es")]
    [InlineData("punctuation!")]
    public void Rejects_keys_outside_the_allowed_shape(string key) =>
        Assert.False(CrestOptionListRules.ValidateKey(key, Existing).IsValid);

    [Fact]
    public void Invalid_results_carry_an_explanation() =>
        Assert.False(string.IsNullOrWhiteSpace(CrestOptionListRules.ValidateKey("Margin", Existing).Error));
}

public class OptionOrderingTests
{
    private static CrestOptionModel Option(string key, string text, bool hidden = false, int position = 0) =>
        new($"id-{key}", key, text, CrestOptionSources.Module, hidden, position);

    [Fact]
    public void Orders_by_position_then_display_text()
    {
        var ordered = CrestOptionListRules.Ordered([
            Option("c", "Zulu", position: 2),
            Option("a", "Bravo", position: 1),
            Option("b", "Alpha", position: 1),
        ]);

        Assert.Equal(["Alpha", "Bravo", "Zulu"], ordered.Select(option => option.DisplayText));
    }

    [Fact]
    public void Ordered_keeps_hidden_options_so_history_can_still_resolve_them()
    {
        var ordered = CrestOptionListRules.Ordered([Option("a", "Alpha"), Option("b", "Bravo", hidden: true)]);

        Assert.Equal(2, ordered.Count);
    }

    [Fact]
    public void Selectable_drops_hidden_options()
    {
        var selectable = CrestOptionListRules.Selectable([Option("a", "Alpha"), Option("b", "Bravo", hidden: true)]);

        Assert.Equal(["Alpha"], selectable.Select(option => option.DisplayText));
    }
}

public class OptionSeedLayeringTests
{
    private static CrestOptionModel Existing(string key, string text, bool hidden = false) =>
        new($"id-{key}", key, text, CrestOptionSources.Module, hidden, 0);

    private static CrestOptionListSeed Seed(params string[] keys) =>
        new("pricing.modifier-kind", "Modifier kind", [.. keys.Select(key => new CrestOptionSeed(key, key))]);

    [Fact]
    public void Seeding_an_empty_list_adds_every_option()
    {
        var plan = CrestOptionListRules.PlanSeed(Seed("Markup", "Margin"), []);

        Assert.Equal(["Markup", "Margin"], plan.Additions.Select(option => option.Key));
        Assert.True(plan.HasChanges);
    }

    [Fact]
    public void Reseeding_an_applied_seed_is_a_no_op()
    {
        var plan = CrestOptionListRules.PlanSeed(Seed("Markup", "Margin"), [Existing("Markup", "Markup"), Existing("Margin", "Margin")]);

        Assert.False(plan.HasChanges);
    }

    [Fact]
    public void Only_genuinely_new_keys_are_added()
    {
        var plan = CrestOptionListRules.PlanSeed(Seed("Markup", "Margin", "CostPlus"), [Existing("Markup", "Markup")]);

        Assert.Equal(["Margin", "CostPlus"], plan.Additions.Select(option => option.Key));
    }

    [Fact]
    public void A_tenants_relabel_survives_reseeding()
    {
        // The tenant renamed the module's "Markup" option; reseeding must not
        // resurrect the original label, so the option is not re-added at all.
        var plan = CrestOptionListRules.PlanSeed(Seed("Markup"), [Existing("Markup", "Standard uplift")]);

        Assert.False(plan.HasChanges);
    }

    [Fact]
    public void A_hidden_module_option_is_not_re_added_by_reseeding()
    {
        var plan = CrestOptionListRules.PlanSeed(Seed("Markup"), [Existing("Markup", "Markup", hidden: true)]);

        Assert.False(plan.HasChanges);
    }

    [Fact]
    public void Matching_is_case_insensitive_and_space_tolerant()
    {
        var plan = CrestOptionListRules.PlanSeed(Seed("markup", "  Margin  "), [Existing("Markup", "Markup"), Existing("MARGIN", "Margin")]);

        Assert.False(plan.HasChanges);
    }

    [Fact]
    public void Duplicate_keys_within_one_seed_are_added_once()
    {
        var plan = CrestOptionListRules.PlanSeed(Seed("Markup", "markup"), []);

        Assert.Single(plan.Additions);
    }

    [Fact]
    public void Blank_keys_in_a_seed_are_skipped()
    {
        var plan = CrestOptionListRules.PlanSeed(new CrestOptionListSeed("k", "K", [new CrestOptionSeed("  ", "Blank"), new CrestOptionSeed("Real", "Real")]), []);

        Assert.Equal(["Real"], plan.Additions.Select(option => option.Key));
    }

    [Fact]
    public void Added_keys_are_stored_trimmed()
    {
        var plan = CrestOptionListRules.PlanSeed(Seed("  Markup  "), []);

        Assert.Equal("Markup", plan.Additions.Single().Key);
    }
}
