using Elsa.Common.Entities;
using Elsa.Workflows.Management.Entities;
using Elsa.Workflows.Management.Filters;
using OrchardCore.Crest.Workflows.Documents;
using OrchardCore.Crest.Workflows.Indexes;
using YesSql;

namespace OrchardCore.Crest.Workflows.Extensions;

public static class WorkflowInstanceQueryExtensions
{
    public static IQuery<WorkflowInstanceDocument, WorkflowInstanceIndex> Apply(this IQuery<WorkflowInstanceDocument, WorkflowInstanceIndex> query, WorkflowInstanceFilter filter)
    {
        return filter.Apply(query);
    }

    public static IQuery<WorkflowInstanceDocument, WorkflowInstanceIndex> Apply<TOrderBy>(this IQuery<WorkflowInstanceDocument, WorkflowInstanceIndex> query, WorkflowInstanceOrder<TOrderBy> order)
    {
        var keySelector = ExpressionConverter.Convert<WorkflowInstance, WorkflowInstanceIndex, TOrderBy>(order.KeySelector);
        return order.Direction == OrderDirection.Ascending
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);
    }

    public static IQueryIndex<WorkflowInstanceIndex> Apply(this IQueryIndex<WorkflowInstanceIndex> query, WorkflowInstanceFilter filter)
    {
        return filter.Apply(query);
    }

    public static IQueryIndex<WorkflowInstanceIndex> Apply<TOrderBy>(this IQueryIndex<WorkflowInstanceIndex> query, WorkflowInstanceOrder<TOrderBy> order)
    {
        var keySelector = ExpressionConverter.Convert<WorkflowInstance, WorkflowInstanceIndex, TOrderBy>(order.KeySelector);
        return order.Direction == OrderDirection.Ascending
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);
    }
}