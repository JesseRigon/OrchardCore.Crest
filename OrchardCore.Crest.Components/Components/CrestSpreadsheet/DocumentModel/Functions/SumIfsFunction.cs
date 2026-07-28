#nullable enable

using System.Collections.Generic;

namespace Crest.Components.Primitives.Documents.Spreadsheet;

class SumIfsFunction : ConditionalAggregateFunctionBase
{
    public override string Name => "SUMIFS";

    protected override CellData Aggregate(List<double> values) => AggregationMethods.Sum(values);
}
