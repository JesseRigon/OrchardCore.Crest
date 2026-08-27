using System.Text.Json.Nodes;
using Crest.Services;
using Crest.Settings;
using OrchardCore.ContentManagement;
using Xunit;

namespace Crest.Server.Tests;

// Whether a filter reaches the DATABASE is a performance contract, not an
// implementation detail: a filter that silently falls back to in-memory matching is
// invisible until the table gets large, which is exactly when it matters. These tests
// pin the split.
public class ContentItemOptionSourceProviderPushdownTests
{
    private static ResolvedOptionFilter Filter(string path, string @operator = OptionFilterOperators.Equals, params string[] values) =>
        new(path, @operator, values.Length == 0 ? ["x"] : values);

    [Fact]
    public void Indexed_paths_are_pushed_into_the_query()
    {
        var plan = ContentItemOptionSourceProvider.Pushdown(
        [
            Filter(ContentItemOptionSourceProvider.OptionRowPaths.Owner),
            Filter(ContentItemOptionSourceProvider.OptionRowPaths.Published),
            Filter(ContentItemOptionSourceProvider.OptionRowPaths.DisplayText),
        ]);

        Assert.Equal(3, plan.Indexed.Count);
        Assert.Empty(plan.Residual);
    }

    [Fact]
    public void Document_paths_stay_in_memory()
    {
        // "Field:" values live inside the item's own JSON, which the content item
        // index cannot answer.
        var plan = ContentItemOptionSourceProvider.Pushdown(
        [
            Filter("Field:CustomerPart.Status"),
        ]);

        Assert.Empty(plan.Indexed);
        Assert.Single(plan.Residual);
    }

    [Fact]
    public void Negation_and_multi_value_operators_are_not_translated()
    {
        // These do not map onto the single-column predicates the query builder emits,
        // so they stay in memory rather than becoming something subtly different.
        var plan = ContentItemOptionSourceProvider.Pushdown(
        [
            new(ContentItemOptionSourceProvider.OptionRowPaths.Owner, OptionFilterOperators.NotEquals, ["someone"]),
            new(ContentItemOptionSourceProvider.OptionRowPaths.Owner, OptionFilterOperators.In, ["a", "b"]),
            new(ContentItemOptionSourceProvider.OptionRowPaths.Owner, OptionFilterOperators.Equals, ["a", "b"]),
        ]);

        Assert.Empty(plan.Indexed);
        Assert.Equal(3, plan.Residual.Count);
    }

    [Fact]
    public void A_mixed_set_is_split_rather_than_all_or_nothing()
    {
        var plan = ContentItemOptionSourceProvider.Pushdown(
        [
            Filter(ContentItemOptionSourceProvider.OptionRowPaths.Owner),
            Filter("Field:CustomerPart.Status"),
        ]);

        Assert.Single(plan.Indexed);
        Assert.Single(plan.Residual);
    }

    [Fact]
    public void No_filters_means_no_work()
    {
        var plan = ContentItemOptionSourceProvider.Pushdown(null);

        Assert.Empty(plan.Indexed);
        Assert.Empty(plan.Residual);
    }
}

public class ContentItemOptionSourceProviderProjectionTests
{
    private static ContentItem Item(string id, string displayText, string? json = null)
    {
        var item = new ContentItem
        {
            ContentItemId = id,
            ContentType = "Customer",
            DisplayText = displayText,
            Owner = "owner-1",
            Published = true,
        };

        // ContentItem.Content is a read-only DYNAMIC view over the item's JsonObject
        // (Data itself is internal to OrchardCore), so parts are written through the
        // dynamic indexer - the same way stock field handlers do.
        if (json is not null)
        {
            foreach (var property in JsonNode.Parse(json)!.AsObject())
            {
                item.Content[property.Key] = property.Value?.DeepClone();
            }
        }

        return item;
    }

    [Fact]
    public void Projects_the_requested_columns()
    {
        var row = ContentItemOptionSourceProvider.ToRow(
            Item("item-1", "Acme Corp"),
            [
                ContentItemOptionSourceProvider.OptionRowPaths.DisplayText,
                ContentItemOptionSourceProvider.OptionRowPaths.Owner,
            ]);

        Assert.Equal("item-1", row.Id);
        Assert.Equal("Acme Corp", row.Values["DisplayText"]);
        Assert.Equal("owner-1", row.Values["Owner"]);
    }

    [Fact]
    public void An_items_identity_is_its_id_so_Key_stays_null()
    {
        // Entity sources have no technical key distinct from the id - which is why the
        // index records a null SelectedKey for them.
        Assert.Null(ContentItemOptionSourceProvider.ToRow(Item("item-1", "Acme"), []).Key);
    }

    [Fact]
    public void Falls_back_to_the_default_display_column_when_none_are_asked_for()
    {
        var row = ContentItemOptionSourceProvider.ToRow(Item("item-1", "Acme"), []);

        Assert.Equal("Acme", row.Values["DisplayText"]);
    }

    [Fact]
    public void Reads_a_value_from_the_items_own_document()
    {
        var row = ContentItemOptionSourceProvider.ToRow(
            Item("item-1", "Acme", """{ "CustomerPart": { "Status": { "Text": "Active" } } }"""),
            ["Field:CustomerPart.Status"]);

        Assert.Equal("Active", row.Values["Field:CustomerPart.Status"]);
    }

    [Fact]
    public void Reads_a_referenced_id_from_an_option_picker_inside_the_document()
    {
        // This is what makes "customers whose Type is the type chosen above" work.
        var row = ContentItemOptionSourceProvider.ToRow(
            Item("item-1", "Acme", """{ "CustomerPart": { "Type": { "SelectedIds": ["type-1"] } } }"""),
            ["Field:CustomerPart.Type"]);

        Assert.Equal("type-1", row.Values["Field:CustomerPart.Type"]);
    }

    [Fact]
    public void A_missing_document_path_reads_as_null_rather_than_throwing()
    {
        var row = ContentItemOptionSourceProvider.ToRow(
            Item("item-1", "Acme", """{ "CustomerPart": {} }"""),
            ["Field:CustomerPart.Status"]);

        Assert.Null(row.Values["Field:CustomerPart.Status"]);
    }
}
