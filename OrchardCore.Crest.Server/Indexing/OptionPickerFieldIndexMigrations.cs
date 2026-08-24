using OrchardCore.ContentManagement.Records;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace Crest.Indexing;

// Creates the index table(s) for OptionPickerField selections. Modelled on stock
// UserPickerMigrations, including the MySQL key-length caution below.
//
// A dedicated partition for a content type is created by calling
// CreatePartitionAsync<TIndex> from that module's own migration - the schema is
// identical, only the table differs.
public sealed class OptionPickerFieldIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await CreatePartitionAsync<OptionPickerFieldIndex>(SchemaBuilder);
        return 1;
    }

    /// <summary>
    /// Creates the table and indexes for one option-picker index partition. Public so
    /// a module can declare a dedicated partition for a high-volume content type
    /// (transactions being the obvious case) from its own migration.
    /// </summary>
    public static async Task CreatePartitionAsync<TIndex>(ISchemaBuilder schemaBuilder)
        where TIndex : OptionPickerFieldIndexBase
    {
        var indexName = typeof(TIndex).Name;

        await schemaBuilder.CreateMapIndexTableAsync<TIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentItemVersionId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(ContentItemIndex.MaxContentTypeSize))
            .Column<string>("ContentPart", column => column.WithLength(ContentItemIndex.MaxContentPartSize))
            .Column<string>("ContentField", column => column.WithLength(ContentItemIndex.MaxContentFieldSize))
            .Column<bool>("Published", column => column.Nullable())
            .Column<bool>("Latest", column => column.Nullable())
            .Column<string>("SourceKey", column => column.WithLength(255))
            .Column<string>("SelectedId", column => column.WithLength(26))
            .Column<string>("SelectedKey", column => column.WithLength(255)));

        await schemaBuilder.AlterIndexTableAsync<TIndex>(table => table
            .CreateIndex($"IDX_{indexName}_DocumentId",
                "DocumentId",
                "ContentItemId",
                "ContentItemVersionId",
                "Published",
                "Latest"));

        // MySQL indexes accommodate up to 768 characters; the prefixed lengths below
        // keep this within that budget (same arithmetic as UserPickerMigrations).
        await schemaBuilder.AlterIndexTableAsync<TIndex>(table => table
            .CreateIndex($"IDX_{indexName}_DocumentId_ContentType",
                "DocumentId",
                "ContentType(254)",
                "ContentPart(254)",
                "ContentField(254)",
                "Published",
                "Latest"));

        // The query the identity rule implies: find items whose selection is a given
        // KEY within a given source.
        await schemaBuilder.AlterIndexTableAsync<TIndex>(table => table
            .CreateIndex($"IDX_{indexName}_DocumentId_SourceKey_SelectedKey",
                "DocumentId",
                "SourceKey(200)",
                "SelectedKey(200)",
                "Published",
                "Latest"));

        // And the same lookup by id, for sources whose identity IS the id.
        await schemaBuilder.AlterIndexTableAsync<TIndex>(table => table
            .CreateIndex($"IDX_{indexName}_DocumentId_SelectedId",
                "DocumentId",
                "SelectedId",
                "Published",
                "Latest"));
    }
}
