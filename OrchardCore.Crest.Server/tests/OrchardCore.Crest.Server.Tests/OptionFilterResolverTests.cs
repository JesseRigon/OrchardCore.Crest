using System.Text.Json.Nodes;
using Crest.Services;
using Crest.Settings;
using Xunit;

namespace Crest.Server.Tests;

// Dependent-dropdown semantics. These are the rules most likely to be got subtly
// wrong - an empty parent silently matching everything, a stale child surviving a
// parent change - and none of them need a session, a provider or a browser to verify.
public class OptionFilterResolverTests
{
    private static Dictionary<string, IReadOnlyList<string>> State(params (string Path, string[] Values)[] entries) =>
        entries.ToDictionary(entry => entry.Path, entry => (IReadOnlyList<string>)entry.Values, StringComparer.OrdinalIgnoreCase);

    private static OptionFilter Dependent(string path, string valueFrom, bool requireParent = true) =>
        new() { Path = path, Operator = OptionFilterOperators.Equals, ValueFrom = valueFrom, RequireParentValue = requireParent };

    [Fact]
    public void A_static_filter_passes_its_literal_through()
    {
        var resolution = OptionFilterResolver.Resolve(
            [new OptionFilter { Path = "Status", Value = "Active" }],
            editorState: null);

        var filter = Assert.Single(resolution.Filters);
        Assert.Equal("Status", filter.Path);
        Assert.Equal(["Active"], filter.Values);
        Assert.False(resolution.HasUnmetDependencies);
    }

    [Fact]
    public void A_static_filter_with_no_value_is_skipped_rather_than_matching_empty()
    {
        // Comparing against "" would exclude every row whose value is set, which is
        // the opposite of what an unconfigured filter should do.
        var resolution = OptionFilterResolver.Resolve(
            [new OptionFilter { Path = "Status", Value = null }],
            editorState: null);

        Assert.Empty(resolution.Filters);
        Assert.False(resolution.HasUnmetDependencies);
    }

    [Fact]
    public void A_dependent_filter_takes_its_value_from_the_editing_item()
    {
        var resolution = OptionFilterResolver.Resolve(
            [Dependent("PartyId", "CustomerPart.Party")],
            State(("CustomerPart.Party", ["party-1"])));

        var filter = Assert.Single(resolution.Filters);
        Assert.Equal("PartyId", filter.Path);
        Assert.Equal(["party-1"], filter.Values);
    }

    [Fact]
    public void A_multi_valued_parent_contributes_every_value()
    {
        var resolution = OptionFilterResolver.Resolve(
            [Dependent("PartyId", "CustomerPart.Party")],
            State(("CustomerPart.Party", ["party-1", "party-2"])));

        Assert.Equal(["party-1", "party-2"], Assert.Single(resolution.Filters).Values);
    }

    [Fact]
    public void An_empty_required_parent_leaves_the_dependency_unmet()
    {
        // The caller must then return NOTHING. Returning everything would defeat the
        // point of requiring the parent.
        var resolution = OptionFilterResolver.Resolve(
            [Dependent("PartyId", "CustomerPart.Party")],
            State(("CustomerPart.Party", [])));

        Assert.Empty(resolution.Filters);
        Assert.True(resolution.HasUnmetDependencies);
        Assert.Equal(["CustomerPart.Party"], resolution.UnmetDependencies);
    }

    [Fact]
    public void An_absent_parent_is_treated_the_same_as_an_empty_one()
    {
        var resolution = OptionFilterResolver.Resolve(
            [Dependent("PartyId", "CustomerPart.Party")],
            editorState: null);

        Assert.True(resolution.HasUnmetDependencies);
    }

    [Fact]
    public void An_empty_optional_parent_simply_drops_the_filter()
    {
        var resolution = OptionFilterResolver.Resolve(
            [Dependent("PartyId", "CustomerPart.Party", requireParent: false)],
            State(("CustomerPart.Party", [])));

        Assert.Empty(resolution.Filters);
        Assert.False(resolution.HasUnmetDependencies);
    }

    [Fact]
    public void A_blank_parent_value_counts_as_empty()
    {
        // A picker that has been cleared may send "" rather than dropping the key.
        var resolution = OptionFilterResolver.Resolve(
            [Dependent("PartyId", "CustomerPart.Party")],
            State(("CustomerPart.Party", ["   "])));

        Assert.True(resolution.HasUnmetDependencies);
    }

    [Fact]
    public void Static_and_dependent_filters_compose()
    {
        var resolution = OptionFilterResolver.Resolve(
            [
                new OptionFilter { Path = "Status", Value = "Active" },
                Dependent("PartyId", "CustomerPart.Party"),
            ],
            State(("CustomerPart.Party", ["party-1"])));

        Assert.Equal(2, resolution.Filters.Count);
        Assert.False(resolution.HasUnmetDependencies);
    }

    [Fact]
    public void An_In_filter_splits_its_literal_but_other_operators_do_not()
    {
        var inFilter = OptionFilterResolver.Resolve(
            [new OptionFilter { Path = "Status", Operator = OptionFilterOperators.In, Value = "Active, Pending" }],
            editorState: null);

        Assert.Equal(["Active", "Pending"], Assert.Single(inFilter.Filters).Values);

        // A value that legitimately contains a comma must survive intact.
        var equalsFilter = OptionFilterResolver.Resolve(
            [new OptionFilter { Path = "Name", Operator = OptionFilterOperators.Equals, Value = "Smith, John" }],
            editorState: null);

        Assert.Equal(["Smith, John"], Assert.Single(equalsFilter.Filters).Values);
    }

    [Fact]
    public void Dependency_paths_are_listed_once_for_the_editor_to_watch()
    {
        var paths = OptionFilterResolver.DependencyPaths(
        [
            Dependent("PartyId", "CustomerPart.Party"),
            Dependent("Other", "CustomerPart.Party"),
            new OptionFilter { Path = "Status", Value = "Active" },
        ]);

        Assert.Equal(["CustomerPart.Party"], paths);
    }

    [Fact]
    public void A_child_is_required_when_its_parent_is()
    {
        // Derived rather than configured: completing a document with the pair
        // half-filled would save an unresolved reference.
        Assert.True(OptionFilterResolver.IsRequiredBecauseParentIs(
            [Dependent("PartyId", "CustomerPart.Party")],
            path => path == "CustomerPart.Party"));

        Assert.False(OptionFilterResolver.IsRequiredBecauseParentIs(
            [Dependent("PartyId", "CustomerPart.Party")],
            path => false));
    }

    [Fact]
    public void A_static_only_attachment_is_never_required_by_derivation()
    {
        // Nothing to derive from: requiredness only propagates along a DEPENDENCY.
        Assert.False(OptionFilterResolver.IsRequiredBecauseParentIs(
            [new OptionFilter { Path = "Status", Value = "Active" }],
            path => true));
    }

    [Fact]
    public void Required_is_a_declared_setting_rather_than_an_assumed_json_key()
    {
        // Regression guard. The first cut of the controller read
        // Settings["OptionPickerFieldSettings"]["Required"] by hand - a key that did
        // not exist on the settings type at all, so it read false forever and the
        // derived-required rule silently never fired. Stock field settings carry no
        // shared Required flag, so ours declares its own.
        var settings = new OptionPickerFieldSettings { Required = true };

        Assert.True(settings.Required);
        Assert.False(new OptionPickerFieldSettings().Required);
    }

    public class SelectionValidity
    {
        private static OptionRow Row(string id, string partyId) =>
            new(id, new Dictionary<string, string?> { ["PartyId"] = partyId });

        [Fact]
        public void An_empty_selection_is_always_valid()
        {
            var resolution = OptionFilterResolver.Resolve(
                [Dependent("PartyId", "CustomerPart.Party")],
                State(("CustomerPart.Party", [])));

            Assert.True(OptionFilterResolver.SelectionStillValid(resolution, [], []));
        }

        [Fact]
        public void A_selection_matching_the_new_parent_survives()
        {
            var resolution = OptionFilterResolver.Resolve(
                [Dependent("PartyId", "CustomerPart.Party")],
                State(("CustomerPart.Party", ["party-1"])));

            Assert.True(OptionFilterResolver.SelectionStillValid(
                resolution, ["contact-1"], [Row("contact-1", "party-1")]));
        }

        [Fact]
        public void A_selection_belonging_to_the_old_parent_is_stale()
        {
            var resolution = OptionFilterResolver.Resolve(
                [Dependent("PartyId", "CustomerPart.Party")],
                State(("CustomerPart.Party", ["party-2"])));

            Assert.False(OptionFilterResolver.SelectionStillValid(
                resolution, ["contact-1"], [Row("contact-1", "party-1")]));
        }

        [Fact]
        public void Clearing_the_parent_invalidates_the_child()
        {
            var resolution = OptionFilterResolver.Resolve(
                [Dependent("PartyId", "CustomerPart.Party")],
                State(("CustomerPart.Party", [])));

            Assert.False(OptionFilterResolver.SelectionStillValid(
                resolution, ["contact-1"], [Row("contact-1", "party-1")]));
        }

        [Fact]
        public void A_selection_the_source_can_no_longer_produce_is_stale()
        {
            // Nothing came back for the id - it was deleted, or it is now out of
            // scope. Either way it cannot be shown as a valid choice.
            var resolution = OptionFilterResolver.Resolve(
                [Dependent("PartyId", "CustomerPart.Party")],
                State(("CustomerPart.Party", ["party-1"])));

            Assert.False(OptionFilterResolver.SelectionStillValid(resolution, ["contact-1"], []));
        }

        [Fact]
        public void With_no_filters_any_resolvable_selection_stands()
        {
            Assert.True(OptionFilterResolver.SelectionStillValid(
                OptionFilterResolution.Unrestricted, ["contact-1"], [Row("contact-1", "party-1")]));
        }
    }

    public class StateFromContent
    {
        [Fact]
        public void Reads_selected_ids_from_an_option_picker_field()
        {
            var content = JsonNode.Parse("""
                { "CustomerPart": { "Party": { "SourceKey": "contentitem:Party", "SelectedIds": ["party-1"] } } }
                """);

            var state = OptionFilterResolver.StateFromContent(content, ["CustomerPart.Party"]);

            Assert.Equal(["party-1"], state["CustomerPart.Party"]);
        }

        [Fact]
        public void Reads_a_text_field_value()
        {
            var content = JsonNode.Parse("""
                { "CustomerPart": { "Region": { "Text": "West" } } }
                """);

            var state = OptionFilterResolver.StateFromContent(content, ["CustomerPart.Region"]);

            Assert.Equal(["West"], state["CustomerPart.Region"]);
        }

        [Fact]
        public void A_missing_path_contributes_nothing_rather_than_an_empty_entry()
        {
            var content = JsonNode.Parse("""{ "CustomerPart": {} }""");

            var state = OptionFilterResolver.StateFromContent(content, ["CustomerPart.Party"]);

            Assert.False(state.ContainsKey("CustomerPart.Party"));
        }
    }
}
