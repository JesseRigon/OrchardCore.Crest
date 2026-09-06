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

// Whether a SORT reaches the database decides cross-page correctness: SQL pages
// before the in-memory sorter runs, so any column the plan does not push is only
// ordered within a page. These tests pin what the plan promises.
public class ContentItemOptionSourceProviderSortPushdownTests
{
    private static readonly Dictionary<string, FieldIndexTarget?> NoTargets = new(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, FieldIndexTarget?> Targets(params (string Path, FieldIndexTarget? Target)[] entries) =>
        entries.ToDictionary(entry => entry.Path, entry => entry.Target, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void No_sort_columns_means_no_pushdown_and_the_DisplayText_default()
    {
        var plan = ContentItemOptionSourceProvider.PlanSqlSort([], NoTargets);

        Assert.Equal(0, plan.PushedColumns);
        Assert.Empty(plan.IndexedChain);
        Assert.Null(plan.FieldTarget);
    }

    [Fact]
    public void A_chain_of_indexed_paths_is_fully_pushed()
    {
        var plan = ContentItemOptionSourceProvider.PlanSqlSort(
            [ContentItemOptionSourceProvider.OptionRowPaths.DisplayText, ContentItemOptionSourceProvider.OptionRowPaths.CreatedUtc],
            NoTargets);

        Assert.Equal(2, plan.PushedColumns);
        Assert.Equal(2, plan.IndexedChain.Count);
        Assert.Null(plan.FieldTarget);
    }

    [Fact]
    public void A_non_indexed_tail_cuts_the_chain_but_keeps_the_prefix()
    {
        // The primary cross-page order stays SQL's; the unpushed tail refines in-page.
        var plan = ContentItemOptionSourceProvider.PlanSqlSort(
            [ContentItemOptionSourceProvider.OptionRowPaths.DisplayText, "Field:CustomerPart.Badge"],
            NoTargets);

        Assert.Equal(1, plan.PushedColumns);
        Assert.Equal(new[] { ContentItemOptionSourceProvider.OptionRowPaths.DisplayText }, plan.IndexedChain);
    }

    [Fact]
    public void A_resolvable_field_as_the_first_column_becomes_a_field_index_sort()
    {
        var badge = new FieldIndexTarget("CustomerPart", "Badge", FieldIndexKind.Text);
        var plan = ContentItemOptionSourceProvider.PlanSqlSort(
            ["Field:CustomerPart.Badge", ContentItemOptionSourceProvider.OptionRowPaths.DisplayText],
            Targets(("Field:CustomerPart.Badge", badge)));

        // One join per index type is the YesSql constraint, so only the primary
        // column is pushed; the DisplayText tiebreak refines in-page.
        Assert.Equal(1, plan.PushedColumns);
        Assert.Equal(badge, plan.FieldTarget);
    }

    [Fact]
    public void An_unresolvable_first_column_pushes_nothing()
    {
        var plan = ContentItemOptionSourceProvider.PlanSqlSort(
            ["Field:CustomerPart.Notes"],
            Targets(("Field:CustomerPart.Notes", null)));

        Assert.Equal(0, plan.PushedColumns);
        Assert.Null(plan.FieldTarget);
    }
}

// Residual field filters promote into field-index joins only when SQL semantics
// match the in-memory matcher exactly - a filter that changes meaning when it
// reaches the database is worse than one that stays in memory.
public class ContentItemOptionSourceProviderFilterPromotionTests
{
    private static readonly FieldIndexTarget Credit = new("CustomerPart", "CreditLimit", FieldIndexKind.Numeric);
    private static readonly FieldIndexTarget Active = new("CustomerPart", "Active", FieldIndexKind.Boolean);
    private static readonly FieldIndexTarget Badge = new("CustomerPart", "Badge", FieldIndexKind.Text);

    private static Dictionary<string, FieldIndexTarget?> Targets(params (string Path, FieldIndexTarget? Target)[] entries) =>
        entries.ToDictionary(entry => entry.Path, entry => entry.Target, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Numeric_and_boolean_filters_promote()
    {
        var promotion = ContentItemOptionSourceProvider.PlanFieldFilterPromotion(
            [
                new("Field:CustomerPart.CreditLimit", OptionFilterOperators.GreaterThan, ["1000"]),
                new("Field:CustomerPart.Active", OptionFilterOperators.Equals, ["true"]),
            ],
            Targets(("Field:CustomerPart.CreditLimit", Credit), ("Field:CustomerPart.Active", Active)),
            new HashSet<FieldIndexKind>());

        Assert.Equal(2, promotion.Promoted.Count);
        Assert.Empty(promotion.Remaining);
    }

    [Fact]
    public void Text_filters_never_promote()
    {
        // The in-memory matcher compares case-insensitively; SQL string equality
        // follows database collation (case-sensitive on SQLite). Same predicate,
        // different answers - so text stays in memory.
        var promotion = ContentItemOptionSourceProvider.PlanFieldFilterPromotion(
            [new("Field:CustomerPart.Badge", OptionFilterOperators.Equals, ["B-1"])],
            Targets(("Field:CustomerPart.Badge", Badge)),
            new HashSet<FieldIndexKind>());

        Assert.Empty(promotion.Promoted);
        Assert.Single(promotion.Remaining);
    }

    [Fact]
    public void A_kind_reserved_by_the_sort_join_is_not_reused()
    {
        // YesSql merges same-type joins, so a numeric sort and a numeric filter on
        // DIFFERENT fields would collapse into one contradictory join.
        var promotion = ContentItemOptionSourceProvider.PlanFieldFilterPromotion(
            [new("Field:CustomerPart.CreditLimit", OptionFilterOperators.Equals, ["1000"])],
            Targets(("Field:CustomerPart.CreditLimit", Credit)),
            new HashSet<FieldIndexKind> { FieldIndexKind.Numeric });

        Assert.Empty(promotion.Promoted);
        Assert.Single(promotion.Remaining);
    }

    [Fact]
    public void Only_one_filter_per_index_type_promotes()
    {
        var otherNumeric = new FieldIndexTarget("CustomerPart", "Discount", FieldIndexKind.Numeric);
        var promotion = ContentItemOptionSourceProvider.PlanFieldFilterPromotion(
            [
                new("Field:CustomerPart.CreditLimit", OptionFilterOperators.Equals, ["1000"]),
                new("Field:CustomerPart.Discount", OptionFilterOperators.Equals, ["5"]),
            ],
            Targets(("Field:CustomerPart.CreditLimit", Credit), ("Field:CustomerPart.Discount", otherNumeric)),
            new HashSet<FieldIndexKind>());

        Assert.Single(promotion.Promoted);
        Assert.Single(promotion.Remaining);
    }

    [Fact]
    public void Unparseable_or_multi_valued_filters_stay_residual()
    {
        var promotion = ContentItemOptionSourceProvider.PlanFieldFilterPromotion(
            [
                new("Field:CustomerPart.CreditLimit", OptionFilterOperators.Equals, ["abc"]),
                new("Field:CustomerPart.CreditLimit", OptionFilterOperators.In, ["1", "2"]),
            ],
            Targets(("Field:CustomerPart.CreditLimit", Credit)),
            new HashSet<FieldIndexKind>());

        Assert.Empty(promotion.Promoted);
        Assert.Equal(2, promotion.Remaining.Count);
    }
}

// Field: path resolution against the type definition - which part carries the field
// and which stock index table holds its value.
public class ContentItemOptionSourceProviderFieldResolutionTests
{
    private static OrchardCore.ContentManagement.Metadata.Models.ContentTypeDefinition Customer()
    {
        var part = new OrchardCore.ContentManagement.Metadata.Builders.ContentPartDefinitionBuilder()
            .Named("CustomerPart")
            .WithField("Badge", field => field.OfType("TextField"))
            .WithField("CreditLimit", field => field.OfType("NumericField"))
            .WithField("Active", field => field.OfType("BooleanField"))
            .WithField("Notes", field => field.OfType("HtmlField"))
            .Build();

        return new OrchardCore.ContentManagement.Metadata.Builders.ContentTypeDefinitionBuilder()
            .WithName("Customer")
            .WithPart("CustomerPart", part, _ => { })
            .Build();
    }

    // The kind arrives as a string because FieldIndexKind is internal and a public
    // theory signature cannot carry it.
    [Theory]
    [InlineData("Field:CustomerPart.Badge", "Text")]
    [InlineData("CustomerPart.Badge", "Text")]
    [InlineData("Field:Badge", "Text")]
    [InlineData("Field:CustomerPart.CreditLimit", "Numeric")]
    [InlineData("Field:CustomerPart.Active", "Boolean")]
    public void Resolves_part_and_bare_field_paths(string path, string kind)
    {
        var target = ContentItemOptionSourceProvider.ResolveFieldTarget(Customer(), path);

        Assert.NotNull(target);
        Assert.Equal("CustomerPart", target.PartName);
        Assert.Equal(Enum.Parse<FieldIndexKind>(kind), target.Kind);
    }

    [Theory]
    [InlineData("Field:CustomerPart.Missing")]
    [InlineData("Field:OtherPart.Badge")]
    [InlineData("Field:CustomerPart.Notes")] // HtmlField has no stock SQL index.
    [InlineData("Field:A.B.C")]
    public void Unresolvable_paths_return_null(string path) =>
        Assert.Null(ContentItemOptionSourceProvider.ResolveFieldTarget(Customer(), path));
}
