using Elsa.Workflows.Runtime.Entities;
using Elsa.Workflows.Runtime.Filters;
using OrchardCore.Crest.Workflows.Documents;
using OrchardCore.Crest.Workflows.Indexes;
using YesSql;

namespace OrchardCore.Crest.Workflows.Extensions;

public static class StoredBookmarkQueryExtensions
{
    public static IQuery<StoredBookmarkDocument, StoredBookmarkIndex> Apply(this IQuery<StoredBookmarkDocument, StoredBookmarkIndex> query, BookmarkFilter filter)
    {
        return filter.Apply(query);
    }
}