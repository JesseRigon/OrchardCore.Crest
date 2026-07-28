#nullable enable

using System.Collections.Generic;

namespace Crest.Components.Primitives.Documents.Spreadsheet;

class MinIfsFunction : ConditionalAggregateFunctionBase
{
    public override string Name => "MINIFS";

    protected override CellData Aggregate(List<double> values) => AggregationMethods.Min(values);
}
