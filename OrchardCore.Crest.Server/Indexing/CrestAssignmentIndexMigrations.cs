using OrchardCore.ContentManagement.Records;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace Crest.Indexing;

/// <summary>
/// Creates the assignment index table. Composite indexes are ordered for the two
/// queries that matter: "what is assigned to this target" (scoping a list) and "what
/// is this item assigned to" (checking one item).
/// </summary>
public sealed class CrestAssignmentIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CrestAssignmentIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(ContentItemIndex.MaxContentTypeSize))
            .Column<string>("Kind", column => column.WithLength(255))
            .Column<string>("TargetType", column => column.WithLength(ContentItemIndex.MaxContentTypeSize))
            .Column<string>("TargetId", column => column.WithLength(26))
            .Column<bool>("Latest", column => column.Nullable()));

        await SchemaBuilder.AlterIndexTableAsync<CrestAssignmentIndex>(table => table
            .CreateIndex("IDX_CrestAssignmentIndex_DocumentId", "DocumentId"));

        // The scoping query: everything assigned to this target, optionally narrowed
        // by kind and by the assigned item's own type. TargetId leads because it is
        // the most selective column.
        await SchemaBuilder.AlterIndexTableAsync<CrestAssignmentIndex>(table => table
            .CreateIndex("IDX_CrestAssignmentIndex_Target",
                "TargetId",
                "Kind",
                "ContentType",
                "Latest"));

        // The reverse lookup: what this item is assigned to, for checking a single
        // item without scanning by target.
        await SchemaBuilder.AlterIndexTableAsync<CrestAssignmentIndex>(table => table
            .CreateIndex("IDX_CrestAssignmentIndex_Item",
                "ContentItemId",
                "Kind",
                "Latest"));

        return 1;
    }
}
